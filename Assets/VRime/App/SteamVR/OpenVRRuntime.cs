using System;
using System.IO;
using System.Runtime.InteropServices;
using Valve.VR;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// OpenVR（SteamVR）との接続の寿命と、アプリ全体のイベント。
    /// <see cref="Initialize"/> から <see cref="Dispose"/> までが 1回の接続で、同じプロセス内（エディタの Play の繰り返し）で何度でも作り直せる。
    /// </summary>
    public sealed class OpenVRRuntime : IDisposable
    {
        static readonly uint EventSize = (uint)Marshal.SizeOf<VREvent_t>();

        // 接続はプロセスに 1つ。破棄されないまま作り直すと OpenVR の状態が食い違うため、検出して例外にする。
        static bool s_connected;

        bool _disposed;
        bool _quitAcknowledged;

        OpenVRRuntime()
        {
        }

        /// <summary>SteamVR から終了を求められ、<c>AcknowledgeQuit_Exiting</c> を返した。</summary>
        public event Action QuitRequested;

        /// <summary>
        /// オーバーレイアプリとして接続し、アクションマニフェストを登録する。
        /// SteamVR が起動していなければ失敗する。<c>VRApplication_Overlay</c> は SteamVR を起動しない
        /// （<c>VRApplication_Scene</c> だと起動してしまい、VR 機器を装着していないときも動き出す）。
        /// 失敗時は接続を残さない。
        /// </summary>
        /// <param name="actionManifestPath">アクションマニフェスト（<c>actions.json</c>）の絶対パス。</param>
        public static OpenVRRuntime Initialize(string actionManifestPath)
        {
            if (string.IsNullOrEmpty(actionManifestPath)) throw new ArgumentException("パスが空です。", nameof(actionManifestPath));
            if (!Path.IsPathRooted(actionManifestPath))
                throw new ArgumentException("アクションマニフェストのパスは絶対パスで指定してください: " + actionManifestPath, nameof(actionManifestPath));
            if (!File.Exists(actionManifestPath))
                throw new FileNotFoundException("アクションマニフェストが見つかりません。", actionManifestPath);
            if (s_connected)
                throw new InvalidOperationException("OpenVR の接続が破棄されないまま、新しい接続を作ろうとしました。");

            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);
            if (error != EVRInitError.None)
            {
                // OpenVR.Init は失敗時に自分で後始末をする。
                throw new InvalidOperationException(
                    "OpenVR の初期化に失敗しました: " + error + "（" + OpenVR.GetStringForHmdError(error) + "）。SteamVR が起動しているか確認してください。");
            }

            s_connected = true;
            var runtime = new OpenVRRuntime();
            try
            {
                // 登録は接続ごとに必要。同じ内容の再登録は SteamVR 側で問題なく受け付けられる。
                OpenVRErrors.Check(OpenVR.Input.SetActionManifestPath(actionManifestPath), "SetActionManifestPath");
            }
            catch
            {
                runtime.Dispose();
                throw;
            }
            return runtime;
        }

        /// <summary>
        /// 毎フレーム呼ぶ。SteamVR のイベントを処理する。
        /// <c>VREvent_Quit</c>（SteamVR の終了など）を受けたら <c>AcknowledgeQuit_Exiting</c> を返して <see cref="QuitRequested"/> を起こす。
        /// </summary>
        public void PollEvents()
        {
            ThrowIfDisposed();
            var ev = new VREvent_t();
            while (OpenVR.System.PollNextEvent(ref ev, EventSize))
            {
                if (ev.eventType != (uint)EVREventType.VREvent_Quit || _quitAcknowledged) continue;
                _quitAcknowledged = true;
                OpenVR.System.AcknowledgeQuit_Exiting();
                QuitRequested?.Invoke();
            }
        }

        /// <summary>
        /// 接続を閉じる。この前に、この接続で作ったオーバーレイをすべて破棄しておくこと
        /// （オーバーレイのキーは接続の間だけ有効で、同じ接続の中で重複すると作成が失敗する）。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OpenVR.Shutdown();
            s_connected = false;
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpenVRRuntime));
        }
    }
}
