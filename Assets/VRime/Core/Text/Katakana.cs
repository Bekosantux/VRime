using System;
using System.Text;

namespace VRime.Core.Text
{
    /// <summary>ひらがなからカタカナへの変換（変換に失敗したときの代わりの候補）。</summary>
    public static class Katakana
    {
        /// <summary>ひらがなをカタカナにする（ゔ・ゝ・ゞ を含む）。長音「ー」などひらがな以外はそのまま。</summary>
        public static string FromHiragana(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                bool hiragana = (c >= 'ぁ' && c <= 'ゖ') || c == 'ゝ' || c == 'ゞ';
                builder.Append(hiragana ? (char)(c + 0x60) : c);
            }
            return builder.ToString();
        }
    }
}
