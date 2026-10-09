using System;
using System.IO;
using System.Text;

namespace VRKeyboard.Core.Output
{
    /// <summary>
    /// OSC 1.0 のエンコーダ。対応型: s(string) i(int32) f(float32) T/F(bool)。
    /// </summary>
    public static class OscCodec
    {
        public static byte[] Encode(string address, params object[] args)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentException("アドレスが空です。", nameof(address));

            var body = new MemoryStream();
            var tags = new StringBuilder(",");
            foreach (object arg in args)
            {
                switch (arg)
                {
                    case string s:
                        tags.Append('s');
                        WritePaddedString(body, s);
                        break;
                    case int i:
                        tags.Append('i');
                        WriteBigEndian(body, BitConverter.GetBytes(i));
                        break;
                    case float f:
                        tags.Append('f');
                        WriteBigEndian(body, BitConverter.GetBytes(f));
                        break;
                    case bool b:
                        tags.Append(b ? 'T' : 'F');
                        break;
                    default:
                        throw new ArgumentException("OSC で扱えない引数の型です: " + (arg == null ? "null" : arg.GetType().FullName));
                }
            }

            var packet = new MemoryStream();
            WritePaddedString(packet, address);
            WritePaddedString(packet, tags.ToString());
            body.WriteTo(packet);
            return packet.ToArray();
        }

        static void WritePaddedString(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            // 終端の NUL を最低1つ含めて4バイト境界へそろえる。
            int padding = 4 - (bytes.Length % 4);
            for (int i = 0; i < padding; i++) stream.WriteByte(0);
        }

        static void WriteBigEndian(Stream stream, byte[] littleEndian)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(littleEndian);
            stream.Write(littleEndian, 0, littleEndian.Length);
        }
    }
}
