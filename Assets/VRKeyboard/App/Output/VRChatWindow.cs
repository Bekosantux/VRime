using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VRKeyboard.App.Output
{
    public enum VRChatFocusResult
    {
        /// <summary>VRChat を前面にした。</summary>
        Focused,

        /// <summary>VRChat のウィンドウがない（起動していない、または起動の途中）。</summary>
        NotFound,

        /// <summary>Windows が前面の切り替えを許さなかった。</summary>
        Refused,
    }

    /// <summary>起動中の VRChat のウィンドウを前面にする（SPEC 8.1 の「起動時に VRChat にフォーカス」）。</summary>
    public static class VRChatWindow
    {
        public static VRChatFocusResult Focus()
        {
            IntPtr window = FindWindow();
            if (window == IntPtr.Zero) return VRChatFocusResult.NotFound;

            if (Win32.IsIconic(window)) Win32.ShowWindow(window, Win32.SW_RESTORE);
            if (TrySetForeground(window)) return VRChatFocusResult.Focused;

            // 前面の切り替えは、最後に入力を受けたプロセスなどにしか許されない。
            // Alt キーを押している間は許されるため、Alt を押して切り替えてから離す。
            SendAlt(0);
            bool focused = TrySetForeground(window);
            SendAlt(Win32.KEYEVENTF_KEYUP);
            return focused ? VRChatFocusResult.Focused : VRChatFocusResult.Refused;
        }

        // 所有者を持たない、表示中のトップレベルウィンドウのうち、プロセス名が VRChat のものを VRChat の画面とみなす。
        static IntPtr FindWindow()
        {
            var search = new WindowSearch();
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
            if (!search.IsVRChat(pid)) return true;
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
            // 1 つのプロセスが複数のウィンドウを持つため、プロセスごとの判定を覚えておく。
            readonly Dictionary<uint, bool> _isVRChat = new Dictionary<uint, bool>();

            public IntPtr Found { get; set; }

            public bool IsVRChat(uint processId)
            {
                if (_isVRChat.TryGetValue(processId, out bool known)) return known;
                bool isVRChat = OutputTarget.IsVRChatProcess(ProcessImage.QueryName(processId));
                _isVRChat.Add(processId, isVRChat);
                return isVRChat;
            }
        }
    }
}
