namespace VRime.Core.Text
{
    /// <summary>
    /// 直前の文字の切り替え（小゛゜）。かなは濁点・半濁点・小書きの順に巡回し、
    /// 英字（半角・全角）は大文字と小文字を切り替える。
    /// </summary>
    public static class CharacterModifier
    {
        // 各文字列が1つの巡回。末尾の次は先頭に戻る。
        static readonly string[] KanaCycles =
        {
            "かが", "きぎ", "くぐ", "けげ", "こご", "さざ", "しじ", "すず", "せぜ", "そぞ",
            "ただ", "ちぢ", "つっづ", "てで", "とど", "はばぱ", "ひびぴ", "ふぶぷ", "へべぺ", "ほぼぽ",
            "あぁ", "いぃ", "うぅゔ", "えぇ", "おぉ", "やゃ", "ゆゅ", "よょ", "わゎ",
        };

        /// <summary>切り替えの対象なら次の文字を返す。対象外の文字は false。</summary>
        public static bool TryNext(char c, out char next)
        {
            foreach (string cycle in KanaCycles)
            {
                int index = cycle.IndexOf(c);
                if (index < 0) continue;
                next = cycle[(index + 1) % cycle.Length];
                return true;
            }
            return TryToggleCase(c, out next);
        }

        static bool TryToggleCase(char c, out char next)
        {
            if (c >= 'a' && c <= 'z') next = (char)(c - 'a' + 'A');
            else if (c >= 'A' && c <= 'Z') next = (char)(c - 'A' + 'a');
            else if (c >= 'ａ' && c <= 'ｚ') next = (char)(c - 'ａ' + 'Ａ');
            else if (c >= 'Ａ' && c <= 'Ｚ') next = (char)(c - 'Ａ' + 'ａ');
            else
            {
                next = c;
                return false;
            }
            return true;
        }
    }
}
