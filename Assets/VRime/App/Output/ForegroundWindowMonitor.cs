using System;
using VRime.App.Native;

namespace VRime.App.Output
{
    /// <summary>前面のウィンドウの取得元。テストで差し替える。</summary>
    public interface IForegroundWindowSource
    {
        /// <summary>前面のウィンドウのハンドルとプロセス ID。前面のウィンドウがなければ false。毎フレーム呼ぶ。</summary>
        bool TryGetForeground(out IntPtr handle, out int processId);

        /// <summary>ウィンドウの詳細を調べる。前面のウィンドウが変わったときだけ呼ぶ。</summary>
        ForegroundWindowInfo Describe(IntPtr handle, int processId);
    }

    public sealed class SystemForegroundWindowSource : IForegroundWindowSource
    {
        public bool TryGetForeground(out IntPtr handle, out int processId)
        {
            handle = Win32.GetForegroundWindow();
            processId = 0;
            if (handle == IntPtr.Zero) return false;
            if (Win32.GetWindowThreadProcessId(handle, out uint pid) == 0 || pid == 0) return false;
            processId = (int)pid;
            return true;
        }

        public ForegroundWindowInfo Describe(IntPtr handle, int processId)
        {
            return ForegroundWindowInfo.Describe(handle, processId);
        }
    }

    /// <summary>
    /// 前面のウィンドウを監視し、送信先（<see cref="OutputTarget"/>）を保つ。送信・表示・文字数の上限はこの送信先に従う。
    /// <see cref="Update"/> を毎フレームの最初に呼ぶ。ハンドルかプロセスが変わったときだけ詳細を再取得し、
    /// 前面のウィンドウか貼り付けモードが変わったときに送信先を決め直す。
    /// </summary>
    public sealed class ForegroundWindowMonitor
    {
        readonly IForegroundWindowSource _source;
        readonly int _ownProcessId;
        IntPtr _handle;
        int _processId;
        ForegroundWindowInfo _window;
        bool _pasteMode;
        OutputTarget _target = OutputTarget.None;

        public ForegroundWindowMonitor(IForegroundWindowSource source, int ownProcessId)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _ownProcessId = ownProcessId;
            Update(pasteMode: false);
        }

        public static ForegroundWindowMonitor CreateForSystem()
        {
            return new ForegroundWindowMonitor(new SystemForegroundWindowSource(), ForegroundWindowInfo.CurrentProcessId);
        }

        public OutputTarget Target => _target;

        /// <summary>送信先が変わるたびに増える。</summary>
        public int Version { get; private set; }

        /// <summary>pasteMode は貼り付けモード（docs/spec/output.md「貼り付けモード」）がオンか。</summary>
        public void Update(bool pasteMode)
        {
            if (!_source.TryGetForeground(out IntPtr handle, out int processId))
            {
                handle = IntPtr.Zero;
                processId = 0;
            }
            bool windowChanged = handle != _handle || processId != _processId;
            if (!windowChanged && pasteMode == _pasteMode) return;

            if (windowChanged)
            {
                _handle = handle;
                _processId = processId;
                _window = handle == IntPtr.Zero ? null : _source.Describe(handle, processId);
            }
            _pasteMode = pasteMode;
            OutputTarget target = OutputTarget.Classify(_window, _ownProcessId, pasteMode);
            // 前面が VRChat でなければ、貼り付けモードが変わっても送信先は変わらない。
            if (!windowChanged && target.Kind == _target.Kind) return;
            _target = target;
            Version++;
        }
    }
}
