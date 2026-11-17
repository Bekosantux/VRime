using System;

namespace VRime.Core.Text
{
    public enum EditResult
    {
        /// <summary>操作を実行した。</summary>
        Done,

        /// <summary>対象がなく、何も変わらなかった。</summary>
        NothingToDo,

        /// <summary>長さの上限を超えるため実行しなかった。</summary>
        TooLong,
    }

    /// <summary>
    /// 入力バッファ。確定済みの文字列とそのカーソル、確定前のかな（未確定）とその中のカーソルを持つ。
    /// 未確定のかなは確定済み文字列のカーソル位置にあり、未確定のかながある間は
    /// カーソル移動・消去・小゛゜は未確定の中で動作する。
    /// 文字数は UTF-16 単位で数える。長さの上限（<see cref="LengthLimit"/>）があるときは、確定済みと未確定の合計が上限を超える操作は実行しない。
    /// 上限は途中で変えられ、内容が上限を超えたままになることがある。その間も長さを増やさない操作（消去・移動・確定など）はできる。
    /// </summary>
    public sealed class InputBuffer
    {
        int? _lengthLimit;
        string _committed = string.Empty;
        int _committedCursor;
        string _composition = string.Empty;
        int _compositionCursor;

        public string Committed => _committed;
        public int CommittedCursor => _committedCursor;
        public string Composition => _composition;
        public int CompositionCursor => _compositionCursor;
        public int Length => _committed.Length + _composition.Length;
        public bool IsEmpty => Length == 0;
        public bool HasComposition => _composition.Length > 0;

        /// <summary>長さの上限（UTF-16 単位）。null は上限なし。</summary>
        public int? LengthLimit => _lengthLimit;

        /// <summary>内容が長さの上限を超えているか。</summary>
        public bool IsOverLengthLimit => _lengthLimit.HasValue && Length > _lengthLimit.Value;

        /// <summary>確定済みの文字列のカーソル位置に未確定のかなを挟んだ、表示どおりの文字列。</summary>
        public string Text => _composition.Length == 0 ? _committed : _committed.Insert(_committedCursor, _composition);

        /// <summary>内容が変わるたびに増える。上限の変更では増えない。</summary>
        public int Version { get; private set; }

        /// <summary>長さの上限を変える。null は上限なし。内容が上限を超えていても切り詰めない。</summary>
        public void SetLengthLimit(int? limit)
        {
            if (limit.HasValue && limit.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "正の値である必要があります。");
            _lengthLimit = limit;
        }

        /// <summary>未確定のかなの、カーソル位置に文字列を挿入する。</summary>
        public EditResult InsertComposition(string kana)
        {
            if (string.IsNullOrEmpty(kana)) throw new ArgumentException("空の文字列は挿入できません。", nameof(kana));
            if (Exceeds(Length + kana.Length)) return EditResult.TooLong;
            _composition = _composition.Insert(_compositionCursor, kana);
            _compositionCursor += kana.Length;
            Version++;
            return EditResult.Done;
        }

        /// <summary>確定済みの文字列に直接挿入する。未確定のかながあれば先にそのまま確定する。</summary>
        public EditResult InsertCommitted(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("空の文字列は挿入できません。", nameof(text));
            // 未確定の確定は長さを変えないため、挿入できるかを先に判定すれば全体として失敗時に何も変わらない。
            if (Exceeds(Length + text.Length)) return EditResult.TooLong;
            CommitComposition();
            _committed = _committed.Insert(_committedCursor, text);
            _committedCursor += text.Length;
            Version++;
            return EditResult.Done;
        }

        /// <summary>未確定のかなを、変換せずにそのまま確定済みにする。</summary>
        public EditResult CommitComposition()
        {
            if (_composition.Length == 0) return EditResult.NothingToDo;
            _committed = _committed.Insert(_committedCursor, _composition);
            _committedCursor += _composition.Length;
            _composition = string.Empty;
            _compositionCursor = 0;
            Version++;
            return EditResult.Done;
        }

        /// <summary>未確定のかなを、変換結果で置き換えて確定済みにする。</summary>
        public EditResult CommitConverted(string converted)
        {
            if (converted == null) throw new ArgumentNullException(nameof(converted));
            if (_composition.Length == 0) return EditResult.NothingToDo;
            if (Exceeds(_committed.Length + converted.Length)) return EditResult.TooLong;
            _committed = _committed.Insert(_committedCursor, converted);
            _committedCursor += converted.Length;
            _composition = string.Empty;
            _compositionCursor = 0;
            Version++;
            return EditResult.Done;
        }

        /// <summary>カーソルの直前の1文字を消す。</summary>
        public EditResult Erase()
        {
            if (_composition.Length > 0)
            {
                if (_compositionCursor == 0) return EditResult.NothingToDo;
                int start = PreviousIndex(_composition, _compositionCursor);
                _composition = _composition.Remove(start, _compositionCursor - start);
                _compositionCursor = start;
            }
            else
            {
                if (_committedCursor == 0) return EditResult.NothingToDo;
                int start = PreviousIndex(_committed, _committedCursor);
                _committed = _committed.Remove(start, _committedCursor - start);
                _committedCursor = start;
            }
            Version++;
            return EditResult.Done;
        }

        public EditResult MoveCursorLeft()
        {
            if (_composition.Length > 0)
            {
                if (_compositionCursor == 0) return EditResult.NothingToDo;
                _compositionCursor = PreviousIndex(_composition, _compositionCursor);
            }
            else
            {
                if (_committedCursor == 0) return EditResult.NothingToDo;
                _committedCursor = PreviousIndex(_committed, _committedCursor);
            }
            Version++;
            return EditResult.Done;
        }

        public EditResult MoveCursorRight()
        {
            if (_composition.Length > 0)
            {
                if (_compositionCursor == _composition.Length) return EditResult.NothingToDo;
                _compositionCursor = NextIndex(_composition, _compositionCursor);
            }
            else
            {
                if (_committedCursor == _committed.Length) return EditResult.NothingToDo;
                _committedCursor = NextIndex(_committed, _committedCursor);
            }
            Version++;
            return EditResult.Done;
        }

        /// <summary>カーソルの直前の1文字を切り替える（<see cref="CharacterModifier"/>）。</summary>
        public EditResult ApplyCharacterModifier()
        {
            if (_composition.Length > 0)
            {
                if (_compositionCursor == 0) return EditResult.NothingToDo;
                if (!CharacterModifier.TryNext(_composition[_compositionCursor - 1], out char next)) return EditResult.NothingToDo;
                _composition = Replace(_composition, _compositionCursor - 1, next);
            }
            else
            {
                if (_committedCursor == 0) return EditResult.NothingToDo;
                if (!CharacterModifier.TryNext(_committed[_committedCursor - 1], out char next)) return EditResult.NothingToDo;
                _committed = Replace(_committed, _committedCursor - 1, next);
            }
            Version++;
            return EditResult.Done;
        }

        public void Clear()
        {
            _committed = string.Empty;
            _committedCursor = 0;
            _composition = string.Empty;
            _compositionCursor = 0;
            Version++;
        }

        bool Exceeds(int length)
        {
            return _lengthLimit.HasValue && length > _lengthLimit.Value;
        }

        static string Replace(string text, int index, char c)
        {
            return text.Substring(0, index) + c + text.Substring(index + 1);
        }

        // サロゲートペア（絵文字など）は1文字として扱う。
        static int PreviousIndex(string text, int cursor)
        {
            int index = cursor - 1;
            if (index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1])) index--;
            return index;
        }

        static int NextIndex(string text, int cursor)
        {
            int index = cursor + 1;
            if (index < text.Length && char.IsHighSurrogate(text[cursor]) && char.IsLowSurrogate(text[index])) index++;
            return index;
        }
    }
}
