using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VRKeyboard.App.Output
{
    /// <summary>
    /// クリップボードの内容の複製（SPEC 7.2）。いずれもクリップボードを開いている間（<see cref="ClipboardAccess"/>）に呼ぶ。
    /// メモリ（HGLOBAL）の形式と拡張メタファイルを複製する。CF_BITMAP・CF_PALETTE は CF_DIB から Windows が作り直すため複製しない。
    /// それ以外の GDI ハンドルの形式・オーナー描画・アプリ固有のハンドル（CF_PRIVATEFIRST〜CF_GDIOBJLAST）は中身を複製できないため捨てる。
    /// </summary>
    internal sealed class ClipboardSnapshot
    {
        readonly struct Entry
        {
            public Entry(uint format, byte[] data)
            {
                Format = format;
                Data = data;
            }

            public uint Format { get; }
            public byte[] Data { get; }
        }

        readonly List<Entry> _entries;

        ClipboardSnapshot(List<Entry> entries)
        {
            _entries = entries;
        }

        /// <summary>複製した形式の数。空のクリップボードを退避したときは 0。</summary>
        public int FormatCount => _entries.Count;

        /// <summary>開いているクリップボードの内容を複製する。遅延描画の形式は、ここで元のアプリに描画させる。</summary>
        public static ClipboardSnapshot Capture()
        {
            var entries = new List<Entry>();
            uint format = 0;
            while (true)
            {
                format = Win32.EnumClipboardFormats(format);
                if (format == 0) break;
                if (!IsCopyable(format)) continue;

                // 元のアプリが遅延描画に失敗したときは null になる。その形式は戻せないため捨てる。
                IntPtr handle = Win32.GetClipboardData(format);
                if (handle == IntPtr.Zero) continue;

                byte[] data = format == Win32.CF_ENHMETAFILE ? CopyEnhMetaFile(handle) : CopyGlobal(handle);
                if (data != null) entries.Add(new Entry(format, data));
            }

            int error = Marshal.GetLastWin32Error();
            if (error != Win32.ERROR_SUCCESS) throw new Win32Exception(error, "クリップボードの形式を列挙できません。");
            return new ClipboardSnapshot(entries);
        }

        /// <summary>開いているクリップボードを空にし、複製した内容を置く。</summary>
        public void Restore()
        {
            if (!Win32.EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードを空にできません。");
            foreach (Entry entry in _entries)
            {
                if (entry.Format == Win32.CF_ENHMETAFILE) SetEnhMetaFile(entry.Data);
                else ClipboardAccess.SetGlobal(entry.Format, entry.Data);
            }
        }

        static bool IsCopyable(uint format)
        {
            switch (format)
            {
                case Win32.CF_BITMAP:
                case Win32.CF_PALETTE:
                case Win32.CF_METAFILEPICT: // 中身に別の GDI ハンドルを含む。
                case Win32.CF_OWNERDISPLAY:
                case Win32.CF_DSPBITMAP:
                case Win32.CF_DSPMETAFILEPICT:
                case Win32.CF_DSPENHMETAFILE:
                    return false;
            }
            return format < Win32.CF_PRIVATEFIRST || format > Win32.CF_GDIOBJLAST;
        }

        // ロックできないメモリ（破棄済みなど）は null。
        static byte[] CopyGlobal(IntPtr handle)
        {
            ulong size = Win32.GlobalSize(handle).ToUInt64();
            if (size == 0) return null;
            if (size > int.MaxValue) throw new InvalidOperationException("クリップボードの内容が大きすぎます。");

            IntPtr pointer = Win32.GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                var data = new byte[size];
                Marshal.Copy(pointer, data, 0, data.Length);
                return data;
            }
            finally
            {
                Win32.GlobalUnlock(handle);
            }
        }

        static byte[] CopyEnhMetaFile(IntPtr handle)
        {
            uint size = Win32.GetEnhMetaFileBits(handle, 0, null);
            if (size == 0) return null;
            var data = new byte[size];
            if (Win32.GetEnhMetaFileBits(handle, size, data) != size)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "拡張メタファイルを読み取れません。");
            return data;
        }

        static void SetEnhMetaFile(byte[] data)
        {
            IntPtr handle = Win32.SetEnhMetaFileBits((uint)data.Length, data);
            if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "拡張メタファイルを作れません。");
            // 置けたときはクリップボードが所有する。置けなかったときだけ自分で破棄する。
            if (Win32.SetClipboardData(Win32.CF_ENHMETAFILE, handle) != IntPtr.Zero) return;
            int error = Marshal.GetLastWin32Error();
            Win32.DeleteEnhMetaFile(handle);
            throw new Win32Exception(error, "クリップボードに拡張メタファイルを置けません。");
        }
    }
}
