using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VRime.App.Native
{
    /// <summary>
    /// 本アプリのプロセスを、ログオンのセッションごとに 1 つに限るための名前付きミューテックス。
    /// 2 つ目のプロセスは SteamVR のオーバーレイのキーが重なって起動の途中で失敗するため、起動の最初に確かめて終了する。
    /// ミューテックスはプロセスが終了すると OS が閉じるため、異常終了した後も次の起動を妨げない。
    /// </summary>
    public sealed class SingleInstanceLock : IDisposable
    {
        const string MutexName = @"Local\VRime.SingleInstance";

        IntPtr _handle;

        SingleInstanceLock(IntPtr handle)
        {
            _handle = handle;
        }

        /// <summary>
        /// ほかに本アプリのプロセスがなければ、ロックを取得して true を返す。あれば false（lockHandle は null）。
        /// ミューテックスを作れないときは例外。
        /// </summary>
        public static bool TryAcquire(out SingleInstanceLock lockHandle)
        {
            IntPtr handle = Win32.CreateMutex(IntPtr.Zero, false, MutexName);
            int error = Marshal.GetLastWin32Error();
            if (handle == IntPtr.Zero) throw new Win32Exception(error, "多重起動の確認用のミューテックスを作れませんでした。");
            if (error == Win32.ERROR_ALREADY_EXISTS)
            {
                Win32.CloseHandle(handle);
                lockHandle = null;
                return false;
            }
            lockHandle = new SingleInstanceLock(handle);
            return true;
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            Win32.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
