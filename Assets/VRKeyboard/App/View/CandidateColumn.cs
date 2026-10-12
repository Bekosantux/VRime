using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 候補欄の1文節の列。見出しの下に候補を縦に並べる（<see cref="VerticalLayoutGroup"/>）。
    /// 候補の行は、シーンに置いた雛形（<see cref="_candidateTemplate"/>）を必要な数だけ複製する。雛形自身は表示しない。
    /// </summary>
    public sealed class CandidateColumn : MonoBehaviour
    {
        [SerializeField] CandidateLabel _heading;

        [Tooltip("候補の1行の雛形。この列の直下に置く。")]
        [SerializeField] CandidateLabel _candidateTemplate;

        readonly List<CandidateLabel> _candidates = new List<CandidateLabel>();
        KeyboardTheme _theme;
        float _maxWidth;

        public CandidateLabel Heading => _heading;

        public void Validate()
        {
            SceneChecks.Require(_heading, this, nameof(_heading));
            SceneChecks.Require(_candidateTemplate, this, nameof(_candidateTemplate));
            if (_candidateTemplate.transform.parent != transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_candidateTemplate.transform) + " は " + SceneChecks.PathOf(transform) + " の直下に置く必要があります。");
            _heading.Validate();
            _candidateTemplate.Validate();
        }

        public void Initialize(KeyboardTheme theme, float maxWidth)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _maxWidth = maxWidth;
            _heading.Initialize(theme, maxWidth);
            _candidateTemplate.gameObject.SetActive(false);
        }

        public CandidateLabel GetCandidate(int index)
        {
            while (_candidates.Count <= index)
            {
                CandidateLabel candidate = Instantiate(_candidateTemplate, transform);
                candidate.name = _candidateTemplate.name + " " + _candidates.Count;
                // 雛形は非表示のため、複製も非表示になる。文字の幅を測れるよう、一度も有効にしていない状態で使わない。
                candidate.gameObject.SetActive(true);
                candidate.Initialize(_theme, _maxWidth);
                _candidates.Add(candidate);
            }
            return _candidates[index];
        }

        /// <summary>候補の行を count 行だけ表示する（0 なら非表示）。</summary>
        public void ShowCandidates(int count)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                bool active = i < count;
                if (_candidates[i].gameObject.activeSelf != active) _candidates[i].gameObject.SetActive(active);
            }
        }
    }
}
