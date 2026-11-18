using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VRime.App.Native
{
    /// <summary>
    /// エラーを示すメッセージボックス。ビルドで、起動の処理の失敗を利用者に示すために使う（SPEC 3.1）。
    /// オーナーのウィンドウは持たせない。Unity のプレイヤーのウィンドウを探す処理（<see cref="PlayerWindow"/>）自体が失敗の原因になりうるため。
    /// </summary>
    public static class ErrorDialog
    {
        /// <summary>メッセージボックスを最前面に表示し、閉じられるまで呼び出したスレッドを止める。表示できなければ例外。</summary>
        public static void Show(string caption, string text)
        {
            if (string.IsNullOrEmpty(caption)) throw new ArgumentException("タイトルが空です。", nameof(caption));
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("本文が空です。", nameof(text));

            const uint type = Win32.MB_OK | Win32.MB_ICONERROR | Win32.MB_SETFOREGROUND | Win32.MB_TOPMOST;
            if (Win32.MessageBox(IntPtr.Zero, text, caption, type) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "エラーのメッセージボックスを表示できませんでした。");
        }
    }
}
