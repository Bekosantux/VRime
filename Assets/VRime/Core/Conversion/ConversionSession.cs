using System;
using System.Collections.Generic;
using System.Text;
using VRime.Core.Text;

namespace VRime.Core.Conversion
{
    /// <summary>
    /// 変換中の選択状態。文節ごとに選択中の候補を持ち、選択中の文節を1つ持つ。
    /// 文節・候補の移動は端で止まり、巡回しない。
    /// </summary>
    public sealed class ConversionSession
    {
        readonly ConversionSegment[] _segments;
        readonly int[] _selectedCandidates;

        public ConversionSession(IReadOnlyList<ConversionSegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            if (segments.Count == 0) throw new ArgumentException("文節が空です。", nameof(segments));
            _segments = new ConversionSegment[segments.Count];
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i] = segments[i] ?? throw new ArgumentException("文節に null が含まれています。", nameof(segments));
            }
            _selectedCandidates = new int[_segments.Length];
        }

        /// <summary>
        /// 変換に失敗したときの代わりの候補。1文節で、候補は［ひらがなのまま, カタカナ］（SPEC 6.4）。
        /// </summary>
        public static ConversionSession CreateFallback(string reading)
        {
            if (string.IsNullOrEmpty(reading)) throw new ArgumentException("読みが空です。", nameof(reading));
            string katakana = Katakana.FromHiragana(reading);
            var candidates = katakana == reading ? new[] { reading } : new[] { reading, katakana };
            return new ConversionSession(new[] { new ConversionSegment(reading, candidates) });
        }

        public IReadOnlyList<ConversionSegment> Segments => _segments;
        public int SelectedSegment { get; private set; }

        /// <summary>選択が変わるたびに増える。</summary>
        public int Version { get; private set; }

        public int SelectedCandidate(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Length)
                throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex, null);
            return _selectedCandidates[segmentIndex];
        }

        /// <summary>各文節の選択中の候補を連結した文字列。</summary>
        public string Result
        {
            get
            {
                var builder = new StringBuilder();
                for (int i = 0; i < _segments.Length; i++)
                {
                    builder.Append(_segments[i].Candidates[_selectedCandidates[i]]);
                }
                return builder.ToString();
            }
        }

        /// <summary>選択中の文節を delta だけ動かす。端で止まり、動いたときだけ true。</summary>
        public bool MoveSegment(int delta)
        {
            int target = Math.Max(0, Math.Min(_segments.Length - 1, SelectedSegment + delta));
            if (target == SelectedSegment) return false;
            SelectedSegment = target;
            Version++;
            return true;
        }

        /// <summary>選択中の文節の候補を delta だけ動かす。端で止まり、動いたときだけ true。</summary>
        public bool MoveCandidate(int delta)
        {
            int count = _segments[SelectedSegment].Candidates.Count;
            int current = _selectedCandidates[SelectedSegment];
            int target = Math.Max(0, Math.Min(count - 1, current + delta));
            if (target == current) return false;
            _selectedCandidates[SelectedSegment] = target;
            Version++;
            return true;
        }
    }
}
