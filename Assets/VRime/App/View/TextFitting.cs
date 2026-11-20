using System;
using TMPro;

namespace VRime.App.View
{
    /// <summary>
    /// 1行の文字を幅に収める。収まらないときは、収まる長さまで切り詰めて省略記号を付ける。
    /// TextMeshPro の省略記号（Ellipsis）は、レイアウトで幅が決まる文字では文字が描画されなくなる（Unity 6.3 で確認）ため使わない。
    /// </summary>
    public static class TextFitting
    {
        /// <summary>label に text を設定し、maxWidth を超えるなら切り詰める。設定した文字の幅を返す。</summary>
        public static float Fit(TMP_Text label, string text, float maxWidth)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (!(maxWidth > 0f)) throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth, "幅が正ではありません。");

            label.text = text;
            if (label.preferredWidth > maxWidth)
            {
                int low = 0;
                int high = text.Length - 1;
                while (low < high)
                {
                    int mid = (low + high + 1) / 2;
                    label.text = Truncate(text, mid);
                    if (label.preferredWidth <= maxWidth) low = mid;
                    else high = mid - 1;
                }
                label.text = Truncate(text, low);
            }
            return label.preferredWidth;
        }

        static string Truncate(string text, int length)
        {
            // サロゲートペアを分断しない。
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
            return text.Substring(0, length) + "…";
        }
    }
}
