using System;
using System.Net;
using System.Net.Sockets;
using VRime.Core.Output;

namespace VRime.App.Output
{
    public interface IChatboxOutput
    {
        void Send(string text);
    }

    /// <summary>
    /// VRChat のチャットボックスへ OSC（UDP）で送る（docs/spec/output.md「VRChat」）。
    /// <see cref="Send"/> と <see cref="SetPreview"/> は予約だけを行い、毎フレーム呼ぶ <see cref="Update"/> が 1秒以上の間隔を空けて実際に送る。
    /// 送信の失敗（ソケットの例外）は <see cref="Update"/> / <see cref="SetTyping"/> から送出される。
    /// </summary>
    public sealed class VRChatChatbox : IChatboxOutput, IDisposable
    {
        /// <summary>チャットボックスに表示できる文字数（UTF-16 単位）。</summary>
        public const int MaxLength = 144;

        readonly AppSettings _settings;
        readonly ChatboxOutbox _outbox = new ChatboxOutbox();

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
        public int PendingSendCount => _outbox.PendingSendCount;

        /// <summary>送信をキューに積む。実際の送信は <see cref="Update"/> で行う。</summary>
        public void Send(string text)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));
            _outbox.EnqueueSend(text);
        }

        /// <summary>
        /// 入力中の文字をチャットボックスに表示する。null は表示なしで、表示中なら消す。
        /// 毎フレーム呼んでよく、間隔内の変更は最新のものだけを送る。<see cref="MaxLength"/> を超える分は表示しない。
        /// </summary>
        public void SetPreview(string text)
        {
            ThrowIfDisposed();
            _outbox.SetPreview(text != null ? Truncate(text) : null);
        }

        // サロゲートペアを分割しない。
        static string Truncate(string text)
        {
            if (text.Length <= MaxLength) return text;
            int length = char.IsHighSurrogate(text[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            return text.Substring(0, length);
        }

        /// <summary>
        /// 入力中の表示を出していれば、間隔を待たずに消す。終了の前に呼ぶ（チャットボックスに入力途中の文字が残らないようにするため）。
        /// </summary>
        public void ClearPreviewNow()
        {
            ThrowIfDisposed();
            if (!_outbox.TryClearPreviewNow()) return;
            SendPacket(OscCodec.Encode("/chatbox/input", string.Empty, true, false));
        }

        /// <summary>毎フレーム呼ぶ。送信してよい時刻なら、次のメッセージを送る。</summary>
        public void Update(double nowSeconds)
        {
            ThrowIfDisposed();
            if (!_outbox.TryDequeue(nowSeconds, out ChatboxMessage message)) return;
            switch (message.Kind)
            {
                case ChatboxMessageKind.Send:
                    SendPacket(OscCodec.Encode("/chatbox/input", message.Text, true, _settings.PlayChatboxNotificationSound));
                    break;
                case ChatboxMessageKind.Preview:
                    SendPacket(OscCodec.Encode("/chatbox/input", message.Text, true, false));
                    break;
                default:
                    throw new InvalidOperationException("未知のメッセージ: " + message.Kind);
            }
        }

        /// <summary>入力中の表示（docs/spec/output.md「VRChat」）。値が変わったときだけ送る。</summary>
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

        // 宛先の設定が変わっていたら再解決する。
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
