using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRime.App.View
{
    /// <summary>
    /// 候補欄の1文節の列（カード）。見出しの下に、候補を表示しているときだけ区切りの線を置き、候補を縦に並べる（<see cref="VerticalLayoutGroup"/>）。
    /// カードの地と影は、配置から外した（LayoutElement.ignoreLayout）子としてシーンに置く。
    /// 候補の行は、シーンに置いた雛形（<see cref="_candidateTemplate"/>）を必要な数だけ複製する。雛形自身は表示しない。
    /// </summary>
    public sealed class CandidateColumn : MonoBehaviour
    {
        [SerializeField] VerticalLayoutGroup _layout;
        [SerializeField] CandidateLabel _heading;

        [Tooltip("見出しと候補の間の区切り。この列の直下に置く。")]
        [SerializeField] GameObject _separator;

        [Tooltip("候補の1行の雛形。この列の直下に置く。")]
        [SerializeField] CandidateLabel _candidateTemplate;

        readonly List<CandidateLabel> _candidates = new List<CandidateLabel>();
        KeyboardTheme _theme;
        float _maxWidth;

        public CandidateLabel Heading => _heading;

        public void Validate()
        {
            SceneChecks.Require(_layout, this, nameof(_layout));
            SceneChecks.Require(_heading, this, nameof(_heading));
            SceneChecks.Require(_separator, this, nameof(_separator));
            if (_separator.transform.parent != transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_separator.transform) + " は " + SceneChecks.PathOf(transform) + " の直下に置く必要があります。");
            SceneChecks.Require(_candidateTemplate, this, nameof(_candidateTemplate));
            if (_candidateTemplate.transform.parent != transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_candidateTemplate.transform) + " は " + SceneChecks.PathOf(transform) + " の直下に置く必要があります。");
            _heading.Validate();
            _candidateTemplate.Validate();
        }

        /// <param name="maxWidth">カードの最大の幅。見出しと候補は、カードの左右の余白を除いた幅に収める。</param>
        public void Initialize(KeyboardTheme theme, float maxWidth)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _maxWidth = maxWidth - _layout.padding.horizontal;
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
            if (_separator.activeSelf != count > 0) _separator.SetActive(count > 0);
            for (int i = 0; i < _candidates.Count; i++)
            {
                bool active = i < count;
                if (_candidates[i].gameObject.activeSelf != active) _candidates[i].gameObject.SetActive(active);
            }
        }
    }
}
