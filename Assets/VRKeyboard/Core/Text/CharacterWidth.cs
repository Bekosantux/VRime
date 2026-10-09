using System;
using System.Text;

namespace VRKeyboard.Core.Text
{
    public static class CharacterWidth
    {
        /// <summary>ASCII の印字可能文字を全角にする（U+0021..007E → U+FF01..FF5E）。スペースは U+3000。他の文字はそのまま。</summary>
        public static char ToFullWidth(char c)
        {
            if (c == ' ') return '　';
            if (c >= '!' && c <= '~') return (char)(c - '!' + '！');
            return c;
        }

        public static string ToFullWidth(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var builder = new StringBuilder(text.Length);
            foreach (char c in text) builder.Append(ToFullWidth(c));
            return builder.ToString();
        }
    }
}
