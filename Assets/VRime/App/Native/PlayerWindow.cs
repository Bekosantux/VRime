using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace VRime.App.Native
{
    /// <summary>
    /// Unity のプレイヤーのウィンドウ。本アプリの表示は SteamVR のオーバーレイで行うため、ビルドでは起動後に隠す（docs/spec/lifecycle.md「起動」）。
    /// デスクトップの設定ウィンドウ（docs/spec/settings.md「デスクトップの設定ウィンドウ」）を開く間だけ表示する。
    /// ビルドのプレイヤーでだけ使う。エディターで呼ぶと、エディター自身のウィンドウが対象になりうる。
    /// 自プロセスに該当するウィンドウがちょうど 1 つでない場合は、どの操作も例外。
    /// </summary>
    public static class PlayerWindow
    {
        // Windows のプレイヤーが画面の描画に使うトップレベルウィンドウのクラス名。
        const string ClassName = "UnityWndClass";

        // ウィンドウのクラス名の最大の長さ（終端を含む）。
        const int MaxClassNameLength = 256;

        /// <summary>ウィンドウを隠す。タスクバーと Alt+Tab の一覧からも消える。隠した後も表示されていれば例外。</summary>
        public static void Hide()
        {
            IntPtr window = Find();
            // 戻り値は直前に表示されていたかどうかで、成否ではない。成否は表示の状態で確かめる。
            Win32.ShowWindow(window, Win32.SW_HIDE);
            if (Win32.IsWindowVisible(window))
                throw new InvalidOperationException("Unity のプレイヤーのウィンドウを隠せませんでした。");
        }

        /// <summary>
        /// ウィンドウを表示し（最小化されていれば元に戻し）、前面にする。表示できなければ例外。
        /// 前面にできたかを返す。Windows は、直前にユーザーが操作したプロセス以外による前面の切り替えを拒むことがある。
        /// </summary>
        public static bool ShowAndActivate()
        {
            IntPtr window = Find();
            Win32.ShowWindow(window, Win32.IsIconic(window) ? Win32.SW_RESTORE : Win32.SW_SHOW);
            if (!Win32.IsWindowVisible(window))
                throw new InvalidOperationException("Unity のプレイヤーのウィンドウを表示できませんでした。");
            return Win32.SetForegroundWindow(window);
        }

        /// <summary>ウィンドウのタイトルを変える。</summary>
        public static void SetTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("タイトルが空です。", nameof(title));
            if (!Win32.SetWindowText(Find(), title))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unity のプレイヤーのウィンドウのタイトルを変えられませんでした。");
        }

        static IntPtr Find()
        {
            var search = new WindowSearch(Win32.GetCurrentProcessId());
            GCHandle handle = GCHandle.Alloc(search);
            try
            {
                Win32.EnumWindows(OnWindow, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                handle.Free();
            }

            // コールバックの中で例外を投げると、ネイティブのコードを越えて伝わらないため、列挙を終えてから投げる。
            if (search.Error != 0) throw new Win32Exception(search.Error, "ウィンドウのクラス名を取得できませんでした。");
            if (search.Found.Count != 1)
                throw new InvalidOperationException("Unity のプレイヤーのウィンドウ（" + ClassName + "）が 1 つではありません: " + search.Found.Count + " 個");
            return search.Found[0];
        }

        [AOT.MonoPInvokeCallback(typeof(Win32.EnumWindowsProc))]
        static bool OnWindow(IntPtr window, IntPtr lParam)
        {
            var search = (WindowSearch)GCHandle.FromIntPtr(lParam).Target;
            if (Win32.GetWindowThreadProcessId(window, out uint pid) == 0 || pid != search.ProcessId) return true;

            var className = new StringBuilder(MaxClassNameLength);
            if (Win32.GetClassName(window, className, className.Capacity) == 0)
            {
                search.Error = Marshal.GetLastWin32Error();
                return false;
            }
            if (className.ToString() == ClassName) search.Found.Add(window);
            return true;
        }

        sealed class WindowSearch
        {
            public WindowSearch(uint processId)
            {
                ProcessId = processId;
            }

            public uint ProcessId { get; }
            public List<IntPtr> Found { get; } = new List<IntPtr>();

            /// <summary>列挙を中断した Win32 のエラーコード。中断していなければ 0。</summary>
            public int Error { get; set; }
        }
    }
}
