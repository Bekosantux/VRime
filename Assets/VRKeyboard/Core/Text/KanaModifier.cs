namespace VRKeyboard.Core.Text
{
    /// <summary>小゛゜キー。直前の1文字を、濁点・半濁点・小書きの順に巡回して切り替える。</summary>
    public static class KanaModifier
    {
        // 各文字列が1つの巡回。末尾の次は先頭に戻る。
        static readonly string[] Cycles =
        {
            "かが", "きぎ", "くぐ", "けげ", "こご", "さざ", "しじ", "すず", "せぜ", "そぞ",
            "ただ", "ちぢ", "つっづ", "てで", "とど", "はばぱ", "ひびぴ", "ふぶぷ", "へべぺ", "ほぼぽ",
            "あぁ", "いぃ", "うぅゔ", "えぇ", "おぉ", "やゃ", "ゆゅ", "よょ", "わゎ",
        };

        /// <summary>巡回の対象なら次の文字を返す。対象外の文字は false。</summary>
        public static bool TryNext(char c, out char next)
        {
            foreach (string cycle in Cycles)
            {
                int index = cycle.IndexOf(c);
                if (index < 0) continue;
                next = cycle[(index + 1) % cycle.Length];
                return true;
            }
            next = c;
            return false;
        }
    }
}
