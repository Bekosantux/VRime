using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.Core;
using VRime.Core.Conversion;

namespace VRime.App.View
{
    /// <summary>
    /// 候補欄（docs/spec/conversion.md「候補欄」）。変換の応答待ち・変換中のみ表示する。
    /// 文節を横に並べて選択中を強調し、選択中の文節の下にその候補を縦に並べる。文節の並びは下端を入力欄の直上にそろえる。
    /// 文節が表示の範囲の幅を超えるときは、選択中の文節が見える位置まで横にずらす。
    /// 変換に失敗して代わりの候補を出しているときは、その理由を文節の並びのすぐ上のバナーに表示する。
    /// 文節の列は、シーンに置いたテンプレート（<see cref="_columnTemplate"/>）を必要な数だけ複製する。テンプレート自身は表示しない。
    /// </summary>
    public sealed class CandidateAreaView : MonoBehaviour
    {
        [Tooltip("変換の応答待ちに表示するもの。")]
        [SerializeField] GameObject _waiting;

        [Tooltip("変換に失敗したときのバナー。候補欄の直下に置く。位置と幅はこのビューが決める（高さはシーンの値を使う）。")]
        [SerializeField] RectTransform _banner;

        [Tooltip("バナーの文字。_banner の子で、左右に余白を空けてバナーいっぱいに広げる（その余白をバナーの幅に足す）。")]
        [SerializeField] TextMeshProUGUI _bannerText;

        [Tooltip("文節の並びを表示する範囲。候補欄いっぱいに広げ、はみ出した部分は隠す。")]
        [SerializeField] RectTransform _viewport;

        [Tooltip("文節の列を横に並べるもの（HorizontalLayoutGroup と ContentSizeFitter）。_viewport の直下に置き、左下を基準にする。" +
                 "シーンでの位置が、ずらしていないときの位置になる。その左の余白と同じ幅を右にも空け、間を文節を表示する幅とする。")]
        [SerializeField] RectTransform _row;

        [Tooltip("文節の列のテンプレート。_row の直下に置く。")]
        [SerializeField] CandidateColumn _columnTemplate;

        [Tooltip("バナーと文節の並びの間。")]
        [SerializeField] float _bannerGap = 8f;

        readonly List<CandidateColumn> _columns = new List<CandidateColumn>();
        ActiveTheme _theme;
        Vector2 _rowOrigin;
        float _visibleWidth;
        float _bannerPadding;

        public void Validate()
        {
            SceneChecks.Require(_waiting, this, nameof(_waiting));
            SceneChecks.Require(_banner, this, nameof(_banner));
            SceneChecks.Require(_bannerText, this, nameof(_bannerText));
            SceneChecks.Require(_viewport, this, nameof(_viewport));
            SceneChecks.Require(_row, this, nameof(_row));
            SceneChecks.Require(_columnTemplate, this, nameof(_columnTemplate));
            if (_banner.parent != transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_banner) + " は " + SceneChecks.PathOf(transform) + " の直下に置く必要があります。");
            if (_bannerText.transform.parent != _banner)
                throw new InvalidOperationException(SceneChecks.PathOf(_bannerText.transform) + " は " + SceneChecks.PathOf(_banner) + " の直下に置く必要があります。");
            if (_viewport.parent != transform || _viewport.anchorMin != Vector2.zero || _viewport.anchorMax != Vector2.one
                || _viewport.offsetMin != Vector2.zero || _viewport.offsetMax != Vector2.zero)
                throw new InvalidOperationException(SceneChecks.PathOf(_viewport) + " は " + SceneChecks.PathOf(transform) + " の直下に、いっぱいに広げて置く必要があります。");
            if (_row.anchorMin != Vector2.zero || _row.anchorMax != Vector2.zero || _row.pivot != Vector2.zero)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " のアンカーとピボットは左下（0, 0）にする必要があります。");
            if (_row.parent != _viewport)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " は " + SceneChecks.PathOf(_viewport) + " の直下に置く必要があります。");
            if (_columnTemplate.transform.parent != _row)
                throw new InvalidOperationException(SceneChecks.PathOf(_columnTemplate.transform) + " は " + SceneChecks.PathOf(_row) + " の直下に置く必要があります。");
            if (_row.GetComponent<HorizontalLayoutGroup>() == null)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " に HorizontalLayoutGroup がありません。");
            if (_row.GetComponent<ContentSizeFitter>() == null)
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " に ContentSizeFitter がありません。");
            if (_bannerGap < 0f)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_bannerGap) + " が負です。");
            _columnTemplate.Validate();
        }

        public void Initialize(ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _rowOrigin = _row.anchoredPosition;
            _visibleWidth = _viewport.rect.width - 2f * _rowOrigin.x;
            if (!(_rowOrigin.x >= 0f && _visibleWidth > 0f))
                throw new InvalidOperationException(SceneChecks.PathOf(_row) + " の左の余白が負か、文節を表示する幅が残りません。");
            RectTransform bannerTextRect = _bannerText.rectTransform;
            if (bannerTextRect.anchorMin != Vector2.zero || bannerTextRect.anchorMax != Vector2.one)
                throw new InvalidOperationException(SceneChecks.PathOf(bannerTextRect) + " は " + SceneChecks.PathOf(_banner) + " いっぱいに広げる（アンカーを 0〜1 にする）必要があります。");
            _bannerPadding = bannerTextRect.offsetMin.x - bannerTextRect.offsetMax.x;
            if (!(_visibleWidth > _bannerPadding))
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の文節を表示する幅が、バナーの文字の左右の余白より狭いです。");
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

            SetActive(_viewport.gameObject, true);
            IReadOnlyList<ConversionSegment> segments = conversion.Segments;
            while (_columns.Count < segments.Count)
            {
                CandidateColumn column = Instantiate(_columnTemplate, _row);
                column.name = _columnTemplate.name + " " + _columns.Count;
                // テンプレートは非表示のため、複製も非表示になる。文字の幅を測れるよう、一度も有効にしていない状態で使わない。
                column.gameObject.SetActive(true);
                column.Initialize(_theme, _visibleWidth);
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
            float scroll = Mathf.Clamp(right - _visibleWidth, 0f, left);
            _row.anchoredPosition = new Vector2(_rowOrigin.x - scroll, _rowOrigin.y);

            bool failed = failureReason != null;
            SetActive(_banner.gameObject, failed);
            if (failed) PlaceBanner("変換に失敗しました: " + failureReason);
        }

        // バナーは文節の並びの上端の _bannerGap 上に、文節の並びの左端（ずらしていないとき）にそろえて置く。
        void PlaceBanner(string text)
        {
            var area = (RectTransform)transform;
            float width = TextFitting.Fit(_bannerText, text, _visibleWidth - _bannerPadding) + _bannerPadding;
            float height = _banner.rect.height;
            float rowTop = area.rect.height - (_rowOrigin.y + _row.rect.height);
            UiRects.Place(_banner, _rowOrigin.x, rowTop - _bannerGap - height, width, height);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
