using System;
using VRKeyboard.App.Native;

namespace VRKeyboard.App.Output
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
    /// 前面のウィンドウを見張り、送信先（<see cref="OutputTarget"/>）を保つ。送信・表示・文字数の上限はこの送信先に従う。
    /// <see cref="Update"/> を毎フレームの最初に呼ぶ。ハンドルかプロセスが変わったときだけ詳細を調べ直す。
    /// </summary>
    public sealed class ForegroundWindowMonitor
    {
        readonly IForegroundWindowSource _source;
        readonly int _ownProcessId;
        IntPtr _handle;
        int _processId;
        OutputTarget _target = OutputTarget.None;

        public ForegroundWindowMonitor(IForegroundWindowSource source, int ownProcessId)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _ownProcessId = ownProcessId;
            Update();
        }

        public static ForegroundWindowMonitor CreateForSystem()
        {
            return new ForegroundWindowMonitor(new SystemForegroundWindowSource(), ForegroundWindowInfo.CurrentProcessId);
        }

        public OutputTarget Target => _target;

        /// <summary>送信先が変わるたびに増える。</summary>
        public int Version { get; private set; }

        public void Update()
        {
            if (!_source.TryGetForeground(out IntPtr handle, out int processId))
            {
                handle = IntPtr.Zero;
                processId = 0;
            }
            if (handle == _handle && processId == _processId) return;

            _handle = handle;
            _processId = processId;
            _target = handle == IntPtr.Zero
                ? OutputTarget.None
                : OutputTarget.Classify(_source.Describe(handle, processId), _ownProcessId);
            Version++;
        }
    }
}
