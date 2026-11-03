using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VRKeyboard.App.Output
{
    public enum VRChatFocusResult
    {
        /// <summary>VRChat を前面にした。</summary>
        Focused,

        /// <summary>VRChat が起動していない。</summary>
        NotRunning,

        /// <summary>VRChat のプロセスはあるが、前面にできるウィンドウがない（起動中など）。</summary>
        NoWindow,

        /// <summary>Windows が前面の切り替えを許さなかった。</summary>
        Refused,
    }

    /// <summary>起動中の VRChat のウィンドウを前面にする（SPEC 8.1 の「起動時に VRChat にフォーカス」）。</summary>
    public static class VRChatWindow
    {
        public static VRChatFocusResult Focus()
        {
            HashSet<int> processIds = FindProcessIds();
            if (processIds.Count == 0) return VRChatFocusResult.NotRunning;

            IntPtr window = FindMainWindow(processIds);
            if (window == IntPtr.Zero) return VRChatFocusResult.NoWindow;

            if (Win32.IsIconic(window)) Win32.ShowWindow(window, Win32.SW_RESTORE);
            if (TrySetForeground(window)) return VRChatFocusResult.Focused;

            // 前面の切り替えは、最後に入力を受けたプロセスなどにしか許されない。
            // Alt キーを押している間は許されるため、Alt を押して切り替えてから離す。
            SendAlt(0);
            bool focused = TrySetForeground(window);
            SendAlt(Win32.KEYEVENTF_KEYUP);
            return focused ? VRChatFocusResult.Focused : VRChatFocusResult.Refused;
        }

        static HashSet<int> FindProcessIds()
        {
            var ids = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName(OutputTarget.VRChatProcessName))
            {
                using (process) ids.Add(process.Id);
            }
            return ids;
        }

        // 所有者を持たない、表示中のトップレベルウィンドウを VRChat の画面とみなす。
        static IntPtr FindMainWindow(HashSet<int> processIds)
        {
            var search = new WindowSearch(processIds);
            GCHandle handle = GCHandle.Alloc(search);
            try
            {
                Win32.EnumWindows(OnWindow, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                handle.Free();
            }
            return search.Found;
        }

        [AOT.MonoPInvokeCallback(typeof(Win32.EnumWindowsProc))]
        static bool OnWindow(IntPtr window, IntPtr lParam)
        {
            var search = (WindowSearch)GCHandle.FromIntPtr(lParam).Target;
            if (!Win32.IsWindowVisible(window)) return true;
            if (Win32.GetWindow(window, Win32.GW_OWNER) != IntPtr.Zero) return true;
            if (Win32.GetWindowThreadProcessId(window, out uint pid) == 0) return true;
            if (!search.ProcessIds.Contains((int)pid)) return true;
            search.Found = window;
            return false;
        }

        static bool TrySetForeground(IntPtr window)
        {
            return Win32.SetForegroundWindow(window) && Win32.GetForegroundWindow() == window;
        }

        static void SendAlt(uint flags)
        {
            var inputs = new[] { new Win32.INPUT { type = Win32.INPUT_KEYBOARD, wVk = Win32.VK_MENU, dwFlags = flags } };
            Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        }

        sealed class WindowSearch
        {
            public WindowSearch(HashSet<int> processIds)
            {
                ProcessIds = processIds;
            }

            public HashSet<int> ProcessIds { get; }
            public IntPtr Found { get; set; }
        }
    }
}
