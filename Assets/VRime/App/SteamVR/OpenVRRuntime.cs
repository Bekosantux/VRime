using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

        // app key の読み取りに使うバッファの大きさ（OpenVR の k_unMaxApplicationKeyLength）。
        const uint AppKeyBufferLength = 128;

        // 接続はプロセスに 1つ。破棄されないまま作り直すと OpenVR の状態が食い違うため、検出して例外にする。
        static bool s_connected;

        bool _disposed;
        bool _quitAcknowledged;

        OpenVRRuntime(bool isApplicationRegistered)
        {
            IsApplicationRegistered = isApplicationRegistered;
        }

        /// <summary>
        /// SteamVR にアプリとして登録したか（<see cref="Initialize"/> にマニフェストを渡したか）。
        /// 登録していなければ、自動で起動するかどうかは読み書きできない。
        /// </summary>
        public bool IsApplicationRegistered { get; }

        /// <summary>SteamVR から終了を求められ、<c>AcknowledgeQuit_Exiting</c> を返した。</summary>
        public event Action QuitRequested;

        /// <summary>
        /// オーバーレイアプリとして接続し、アクションマニフェストを登録する。
        /// SteamVR が起動していなければ失敗する。<c>VRApplication_Overlay</c> は SteamVR を起動しない
        /// （<c>VRApplication_Scene</c> だと起動してしまい、VR 機器を装着していないときも動き出す）。
        /// applicationManifestPath を渡したときは、アクションマニフェストより先に、アプリを SteamVR に登録してこのプロセスと結び付ける
        /// （アクションとバインドは、登録の時点でプロセスに結び付いている app key に属するため）。
        /// 初めて登録したときは、自動で起動しない状態にする。
        /// 失敗時は接続を残さない。
        /// </summary>
        /// <param name="actionManifestPath">アクションマニフェスト（<c>actions.json</c>）の絶対パス。</param>
        /// <param name="applicationManifestPath">
        /// 書き出し済みのアプリのマニフェスト（<see cref="OpenVRApplicationManifest"/>）の絶対パス。null なら登録しない（エディターでは Unity のエディター自体を登録しないようにするため）。
        /// </param>
        public static OpenVRRuntime Initialize(string actionManifestPath, string applicationManifestPath)
        {
            if (string.IsNullOrEmpty(actionManifestPath)) throw new ArgumentException("パスが空です。", nameof(actionManifestPath));
            if (!Path.IsPathRooted(actionManifestPath))
                throw new ArgumentException("アクションマニフェストのパスは絶対パスで指定してください: " + actionManifestPath, nameof(actionManifestPath));
            if (!File.Exists(actionManifestPath))
                throw new FileNotFoundException("アクションマニフェストが見つかりません。", actionManifestPath);
            if (applicationManifestPath != null)
            {
                if (!Path.IsPathRooted(applicationManifestPath))
                    throw new ArgumentException("アプリのマニフェストのパスは絶対パスで指定してください: " + applicationManifestPath, nameof(applicationManifestPath));
                if (!File.Exists(applicationManifestPath))
                    throw new FileNotFoundException("アプリのマニフェストが見つかりません。", applicationManifestPath);
            }
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
            var runtime = new OpenVRRuntime(applicationManifestPath != null);
            try
            {
                if (applicationManifestPath != null) RegisterApplication(applicationManifestPath);
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

        /// <summary>SteamVR の起動時に、本アプリを自動で起動するか。値は SteamVR が持ち、SteamVR の設定画面でも変えられる。</summary>
        public bool GetAutoLaunch()
        {
            ThrowIfNotRegistered();
            return OpenVR.Applications.GetApplicationAutoLaunch(OpenVRApplicationManifest.AppKey);
        }

        /// <summary>SteamVR の起動時に、本アプリを自動で起動するかを変える。</summary>
        public void SetAutoLaunch(bool enabled)
        {
            ThrowIfNotRegistered();
            OpenVRErrors.Check(OpenVR.Applications.SetApplicationAutoLaunch(OpenVRApplicationManifest.AppKey, enabled), "SetApplicationAutoLaunch");
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

        void ThrowIfNotRegistered()
        {
            ThrowIfDisposed();
            if (!IsApplicationRegistered) throw new InvalidOperationException("アプリを SteamVR に登録していない接続です。");
        }

        static void RegisterApplication(string manifestPath)
        {
            string appKey = OpenVRApplicationManifest.AppKey;
            bool installed = OpenVR.Applications.IsApplicationInstalled(appKey);
            // 同じパスの再登録は、内容（exe のパスなど）の更新になる。
            OpenVRErrors.Check(OpenVR.Applications.AddApplicationManifest(manifestPath, false), "AddApplicationManifest");
            if (!installed) OpenVRErrors.Check(OpenVR.Applications.SetApplicationAutoLaunch(appKey, false), "SetApplicationAutoLaunch");

            // SteamVR が起動した場合は、起動の時点で app key と結び付いている。利用者が起動した場合は、ここで結び付ける。
            uint processId = (uint)Process.GetCurrentProcess().Id;
            var current = new StringBuilder((int)AppKeyBufferLength);
            EVRApplicationError keyError = OpenVR.Applications.GetApplicationKeyByProcessId(processId, current, AppKeyBufferLength);
            if (keyError == EVRApplicationError.None && current.ToString() == appKey) return;
            OpenVRErrors.Check(OpenVR.Applications.IdentifyApplication(processId, appKey), "IdentifyApplication");
        }
    }
}
