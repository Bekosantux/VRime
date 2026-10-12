using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKeyboard.Core;
using VRKeyboard.Core.Conversion;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 候補欄（SPEC 6.3）。変換の応答待ち・変換中のみ表示する。
    /// 文節を横に並べて選択中を強調し、選択中の文節の下にその候補を縦に並べる。
    /// 文節が表示の範囲の幅を超えるときは、選択中の文節が見える位置まで横にずらす。
    /// 変換に失敗して代わりの候補を出しているときは、その理由を上部の帯に表示し、文節の並びをその下に下げる。
    /// 文節の列は、シーンに置いた雛形（<see cref="_columnTemplate"/>）を必要な数だけ複製する。雛形自身は表示しない。
    /// </summary>
    public sealed class CandidateAreaView : MonoBehaviour
    {
        [Tooltip("変換の応答待ちに表示するもの。")]
        [SerializeField] GameObject _waiting;

        [Tooltip("変換に失敗したときの帯。")]
        [SerializeField] RectTransform _banner;

        [SerializeField] TextMeshProUGUI _bannerText;

        [Tooltip("文節の並びを表示する範囲。はみ出した部分は隠す。帯を表示するときは、上端を帯の下端の _bannerGap 下まで下げる。")]
        [SerializeField] RectTransform _viewport;

        [Tooltip("文節の列を横に並べるもの（HorizontalLayoutGroup）。_viewport の直下に置き、左上を基準にする。")]
        [SerializeField] RectTransform _row;

        [Tooltip("文節の列の雛形。_row の直下に置く。")]
        [SerializeField] CandidateColumn _columnTemplate;

        [SerializeField] float _bannerGap = 6f;

        readonly List<CandidateColumn> _columns = new List<CandidateColumn>();
        KeyboardTheme _theme;
        float _viewportTopInset;

        public void Validate()
        {
            SceneChecks.Require(_waiting, this, nameof(_waiting));
            SceneChecks.Require(_banner, this, nameof(_banner));
            SceneChecks.Require(_bannerText, this, nameof(_bannerText));
            SceneChecks.Require(_viewport, this, nameof(_viewport));
            SceneChecks.Require(_row, this, nameof(_row));
            SceneChecks.Require(_columnTemplate, this, nameof(_columnTemplate));
            if (_row.parent != _viewport)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " は " + SceneChecks.PathOf(_viewport) + " の直下に置く必要があります。");
            if (_columnTemplate.transform.parent != _row)
                throw new InvalidOperationException(SceneChecks.PathOf(_columnTemplate.transform) + " は " + SceneChecks.PathOf(_row) + " の直下に置く必要があります。");
            if (_row.GetComponent<HorizontalLayoutGroup>() == null)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " に HorizontalLayoutGroup がありません。");
            if (_bannerGap < 0f)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_bannerGap) + " が負です。");
            _columnTemplate.Validate();
        }

        public void Initialize(KeyboardTheme theme)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _viewportTopInset = -_viewport.offsetMax.y;
            _columnTemplate.gameObject.SetActive(false);
        }

        public void Refresh(KeyboardSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            switch (session.Mode)
            {
                case KeyboardMode.Input:
                    SetActive(gameObject, false);
                    break;

                case KeyboardMode.WaitingConversion:
                    SetActive(gameObject, true);
                    SetActive(_waiting, true);
                    SetActive(_banner.gameObject, false);
                    SetActive(_viewport.gameObject, false);
                    break;

                case KeyboardMode.Converting:
                    SetActive(gameObject, true);
                    SetActive(_waiting, false);
                    RefreshSegments(session.CurrentConversion, session.ConversionFailureReason);
                    break;

                default:
                    throw new InvalidOperationException("未知のモード: " + session.Mode);
            }
        }

        void RefreshSegments(ConversionSession conversion, string failureReason)
        {
            if (conversion == null) throw new InvalidOperationException("変換中なのに変換結果がありません。");

            bool failed = failureReason != null;
            SetActive(_banner.gameObject, failed);
            float topInset = _viewportTopInset;
            if (failed)
            {
                string banner = "変換に失敗しました: " + failureReason;
                if (_bannerText.text != banner) _bannerText.text = banner;
                topInset = UiRects.RectIn(_banner, (RectTransform)transform).yMax + _bannerGap;
            }
            _viewport.offsetMax = new Vector2(_viewport.offsetMax.x, -topInset);
            SetActive(_viewport.gameObject, true);

            float viewportWidth = _viewport.rect.width;
            IReadOnlyList<ConversionSegment> segments = conversion.Segments;
            while (_columns.Count < segments.Count)
            {
                CandidateColumn column = Instantiate(_columnTemplate, _row);
                column.name = _columnTemplate.name + " " + _columns.Count;
                // 雛形は非表示のため、複製も非表示になる。文字の幅を測れるよう、一度も有効にしていない状態で使わない。
                column.gameObject.SetActive(true);
                column.Initialize(_theme, viewportWidth);
                _columns.Add(column);
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                CandidateColumn column = _columns[i];
                bool used = i < segments.Count;
                SetActive(column.gameObject, used);
                if (!used) continue;

                ConversionSegment segment = segments[i];
                bool selectedSegment = i == conversion.SelectedSegment;
                int selectedCandidate = conversion.SelectedCandidate(i);
                column.Heading.Set(segment.Candidates[selectedCandidate], selectedSegment);

                if (!selectedSegment)
                {
                    column.ShowCandidates(0);
                    continue;
                }
                for (int k = 0; k < segment.Candidates.Count; k++)
                {
                    column.GetCandidate(k).Set(segment.Candidates[k], k == selectedCandidate);
                }
                column.ShowCandidates(segment.Candidates.Count);
            }

            // 横幅は文字の長さで決まるため、配置を確定させてから選択中の文節の位置を読む。
            LayoutRebuilder.ForceRebuildLayoutImmediate(_row);
            var selected = (RectTransform)_columns[conversion.SelectedSegment].transform;
            float left = selected.anchoredPosition.x - selected.pivot.x * selected.rect.width;
            float right = left + selected.rect.width;
            float scroll = Mathf.Clamp(right - viewportWidth, 0f, left);
            _row.anchoredPosition = new Vector2(-scroll, 0f);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
