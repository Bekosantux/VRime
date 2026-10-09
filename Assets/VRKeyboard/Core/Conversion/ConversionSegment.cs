using System;
using System.Collections.Generic;

namespace VRKeyboard.Core.Conversion
{
    /// <summary>変換結果の1文節。読みと候補の一覧（先頭が第1候補）。</summary>
    public sealed class ConversionSegment
    {
        public ConversionSegment(string reading, IReadOnlyList<string> candidates)
        {
            if (reading == null) throw new ArgumentNullException(nameof(reading));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0) throw new ArgumentException("候補が空です。", nameof(candidates));
            var copy = new string[candidates.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = candidates[i] ?? throw new ArgumentException("候補に null が含まれています。", nameof(candidates));
            }
            Reading = reading;
            Candidates = copy;
        }

        public string Reading { get; }
        public IReadOnlyList<string> Candidates { get; }
    }
}
