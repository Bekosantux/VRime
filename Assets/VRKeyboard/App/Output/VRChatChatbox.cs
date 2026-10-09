using System;
using System.Net;
using System.Net.Sockets;
using VRKeyboard.Core.Output;

namespace VRKeyboard.App.Output
{
    public interface IChatboxOutput
    {
        void Send(string text);
    }

    /// <summary>
    /// VRChat のチャットボックスへ OSC（UDP）で送る（SPEC 7.3）。
    /// <see cref="Send"/> はキューに積み、毎フレーム呼ぶ <see cref="Update"/> が 1秒以上の間隔を空けて実際に送る。
    /// 送信の失敗（ソケットの例外）は <see cref="Update"/> / <see cref="SetTyping"/> から送出される。
    /// </summary>
    public sealed class VRChatChatbox : IChatboxOutput, IDisposable
    {
        readonly AppSettings _settings;
        readonly ChatboxRateLimiter _limiter = new ChatboxRateLimiter();

        // 接続しない UDP ソケットにする。接続済みだと、宛先が未起動のときの ICMP 到達不能が次の送信の例外になる。
        UdpClient _udp;
        IPEndPoint _endpoint;
        string _endpointHost;
        int _endpointPort;
        bool? _typing;
        bool _disposed;

        public VRChatChatbox(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>送信待ちの件数。</summary>
        public int PendingCount => _limiter.PendingCount;

        /// <summary>送信をキューに積む。実際の送信は <see cref="Update"/> で行う。</summary>
        public void Send(string text)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));
            _limiter.Enqueue(text);
        }

        /// <summary>毎フレーム呼ぶ。送信してよい時刻なら、キューの先頭を送る。</summary>
        public void Update(double nowSeconds)
        {
            ThrowIfDisposed();
            if (_limiter.TryDequeue(nowSeconds, out string text))
            {
                SendPacket(OscCodec.Encode("/chatbox/input", text, true, _settings.PlayChatboxNotificationSound));
            }
        }

        /// <summary>入力中の表示（SPEC 7.3）。値が変わったときだけ送る。</summary>
        public void SetTyping(bool typing)
        {
            ThrowIfDisposed();
            if (_typing == typing) return;
            SendPacket(OscCodec.Encode("/chatbox/typing", typing));
            _typing = typing;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _udp?.Dispose();
            _udp = null;
        }

        void SendPacket(byte[] packet)
        {
            EnsureEndpoint();
            _udp.Send(packet, packet.Length, _endpoint);
        }

        // 宛先の設定が変わっていたら解決し直す。
        void EnsureEndpoint()
        {
            if (_udp != null && _endpointHost == _settings.OscHost && _endpointPort == _settings.OscPort) return;

            string host = _settings.OscHost;
            int port = _settings.OscPort;
            IPAddress address = ResolveHost(host);
            _udp?.Dispose();
            _udp = new UdpClient(address.AddressFamily);
            _endpoint = new IPEndPoint(address, port);
            _endpointHost = host;
            _endpointPort = port;
        }

        static IPAddress ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out IPAddress parsed)) return parsed;
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            return addresses[0];
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VRChatChatbox));
        }
    }
}
