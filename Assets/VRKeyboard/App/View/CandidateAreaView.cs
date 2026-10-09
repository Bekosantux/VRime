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
    /// 文節が入力欄の幅を超えるときは、選択中の文節が見える位置まで横にずらす。
    /// 変換に失敗して代わりの候補を出しているときは、その理由を表示する。
    /// </summary>
    public sealed class CandidateAreaView
    {
        const float RowSpacing = 8f;

        const int Padding = 18;

        // 1つの文節・候補の最大幅。これを超える文字は省略記号にする。
        static readonly float MaxBoxWidth = KeyboardTheme.Width - KeyboardTheme.Margin * 2f;

        // 文節の見出しと候補の1行。背景と文字を持ち、幅は文字に合わせて決まる。
        sealed class LabelBox
        {
            readonly Image _background;
            readonly TextMeshProUGUI _label;
            readonly LayoutElement _size;
            readonly float _maxWidth;
            string _text;

            public LabelBox(Transform parent, TMP_FontAsset font, float fontSize, float height, float maxWidth)
            {
                Root = UiBuilder.Rect(parent, "Box", 0f, 0f, 100f, height);
                _background = UiBuilder.AddImage(Root.gameObject, KeyboardTheme.Chip);
                var layout = Root.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(Padding, Padding, 4, 4);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                _maxWidth = maxWidth;
                _size = Root.gameObject.AddComponent<LayoutElement>();
                _size.minHeight = height;
                _size.preferredHeight = height;

                RectTransform labelRect = UiBuilder.Rect(Root, "Label", 0f, 0f, 100f, height);
                _label = UiBuilder.AddText(labelRect.gameObject, font, fontSize, KeyboardTheme.Text, TextAlignmentOptions.MidlineLeft);
            }

            public RectTransform Root { get; }

            public void Set(string text, bool selected)
            {
                if (_text != text)
                {
                    _text = text;
                    Fit(text);
                }
                _background.color = selected ? KeyboardTheme.ChipSelected : KeyboardTheme.Chip;
            }

            // 幅は文字の長さに合わせる。上限を超えるときは、収まる長さまで切って省略記号を付ける。
            // TextMeshPro の省略記号（Ellipsis）は、レイアウトで幅が決まる文字では文字が描かれなくなる（Unity 6.3 で確認）ため使わない。
            void Fit(string text)
            {
                float limit = _maxWidth - Padding * 2f;
                _label.text = text;
                if (_label.preferredWidth > limit)
                {
                    int low = 0;
                    int high = text.Length - 1;
                    while (low < high)
                    {
                        int mid = (low + high + 1) / 2;
                        _label.text = Truncate(text, mid);
                        if (_label.preferredWidth <= limit) low = mid;
                        else high = mid - 1;
                    }
                    _label.text = Truncate(text, low);
                }
                _size.minWidth = _label.preferredWidth + Padding * 2f;
                _size.preferredWidth = _size.minWidth;
            }

            static string Truncate(string text, int length)
            {
                // サロゲートペアを分断しない。
                if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
                return text.Substring(0, length) + "…";
            }
        }

        sealed class SegmentColumn
        {
            readonly TMP_FontAsset _font;
            readonly List<LabelBox> _candidates = new List<LabelBox>();

            public SegmentColumn(Transform parent, TMP_FontAsset font)
            {
                _font = font;
                Root = UiBuilder.Rect(parent, "Segment", 0f, 0f, 100f, KeyboardTheme.SegmentHeight);
                var layout = Root.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = KeyboardTheme.CandidateGap;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                Heading = new LabelBox(Root, font, KeyboardTheme.SegmentFontSize, KeyboardTheme.SegmentHeight, MaxBoxWidth);
            }

            public RectTransform Root { get; }
            public LabelBox Heading { get; }

            /// <summary>候補の行を count 行だけ表示する（0 なら非表示）。</summary>
            public LabelBox GetCandidate(int index)
            {
                while (_candidates.Count <= index)
                {
                    _candidates.Add(new LabelBox(Root, _font, KeyboardTheme.CandidateFontSize, KeyboardTheme.CandidateHeight, MaxBoxWidth));
                }
                return _candidates[index];
            }

            public void ShowCandidates(int count)
            {
                for (int i = 0; i < _candidates.Count; i++)
                {
                    bool active = i < count;
                    if (_candidates[i].Root.gameObject.activeSelf != active) _candidates[i].Root.gameObject.SetActive(active);
                }
            }
        }

        readonly TMP_FontAsset _font;
        readonly RectTransform _root;
        readonly LabelBox _waiting;
        readonly Image _bannerBackground;
        readonly TextMeshProUGUI _bannerText;
        readonly RectTransform _viewport;
        readonly RectTransform _row;
        readonly List<SegmentColumn> _columns = new List<SegmentColumn>();
        readonly float _viewportWidth;

        public CandidateAreaView(Transform parent, KeyboardFonts fonts)
        {
            if (fonts == null) throw new ArgumentNullException(nameof(fonts));
            _font = fonts.Regular;
            _viewportWidth = KeyboardTheme.Width - KeyboardTheme.Margin * 2f;

            _root = UiBuilder.Rect(parent, "CandidateArea", 0f, 0f, KeyboardTheme.Width, KeyboardTheme.CandidateAreaHeight);

            _waiting = new LabelBox(_root, _font, KeyboardTheme.SegmentFontSize, KeyboardTheme.SegmentHeight, MaxBoxWidth);
            UiBuilder.Place(_waiting.Root, KeyboardTheme.Margin, KeyboardTheme.CandidateAreaHeight - KeyboardTheme.SegmentHeight, 240f, KeyboardTheme.SegmentHeight);
            _waiting.Set("変換中…", false);
            // 応答待ちの表示は、入力欄の直上に置く。
            _waiting.Root.GetComponent<LayoutElement>().enabled = false;

            RectTransform banner = UiBuilder.Rect(_root, "Banner", KeyboardTheme.Margin, 0f, _viewportWidth, KeyboardTheme.BannerHeight);
            _bannerBackground = UiBuilder.AddImage(banner.gameObject, KeyboardTheme.BannerBackground);
            RectTransform bannerTextRect = UiBuilder.Rect(banner, "Text", 18f, 0f, _viewportWidth - 36f, KeyboardTheme.BannerHeight);
            _bannerText = UiBuilder.AddText(bannerTextRect.gameObject, _font, KeyboardTheme.StatusFontSize, KeyboardTheme.Warning, TextAlignmentOptions.MidlineLeft);
            _bannerText.overflowMode = TextOverflowModes.Ellipsis;

            _viewport = UiBuilder.Rect(_root, "Viewport", KeyboardTheme.Margin, 0f, _viewportWidth, KeyboardTheme.CandidateAreaHeight);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _row = UiBuilder.Rect(_viewport, "Row", 0f, 0f, _viewportWidth, KeyboardTheme.CandidateAreaHeight);
            var rowLayout = _row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = RowSpacing;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            // 横幅は文節の並びの全長に合わせる（はみ出した分は RectMask2D で隠し、位置をずらして見せる）。
            var rowFit = _row.gameObject.AddComponent<ContentSizeFitter>();
            rowFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        public void Refresh(KeyboardSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            switch (session.Mode)
            {
                case KeyboardMode.Input:
                    SetActive(_root.gameObject, false);
                    break;

                case KeyboardMode.WaitingConversion:
                    SetActive(_root.gameObject, true);
                    SetActive(_waiting.Root.gameObject, true);
                    SetActive(_bannerBackground.gameObject, false);
                    SetActive(_viewport.gameObject, false);
                    break;

                case KeyboardMode.Converting:
                    SetActive(_root.gameObject, true);
                    SetActive(_waiting.Root.gameObject, false);
                    RefreshSegments(session.CurrentConversion, session.ConversionFailureReason);
                    break;

                default:
                    throw new InvalidOperationException("未知のモード: " + session.Mode);
            }
        }

        void RefreshSegments(ConversionSession conversion, string failureReason)
        {
            if (conversion == null) throw new InvalidOperationException("変換中なのに変換結果がありません。");

            float top = 0f;
            bool failed = failureReason != null;
            SetActive(_bannerBackground.gameObject, failed);
            if (failed)
            {
                string banner = "変換に失敗しました: " + failureReason;
                if (_bannerText.text != banner) _bannerText.text = banner;
                top = KeyboardTheme.BannerHeight + 6f;
            }
            UiBuilder.Place(_viewport, KeyboardTheme.Margin, top, _viewportWidth, KeyboardTheme.CandidateAreaHeight - top);
            SetActive(_viewport.gameObject, true);

            IReadOnlyList<ConversionSegment> segments = conversion.Segments;
            while (_columns.Count < segments.Count) _columns.Add(new SegmentColumn(_row, _font));

            for (int i = 0; i < _columns.Count; i++)
            {
                SegmentColumn column = _columns[i];
                bool used = i < segments.Count;
                SetActive(column.Root.gameObject, used);
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
            RectTransform selected = _columns[conversion.SelectedSegment].Root;
            float left = selected.anchoredPosition.x;
            float right = left + selected.rect.width;
            float scroll = Mathf.Clamp(right - _viewportWidth, 0f, left);
            _row.anchoredPosition = new Vector2(-scroll, 0f);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
