using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 設定画面（<see cref="SettingsForm"/>）の状態を uGUI に反映する。配置は <see cref="SettingsLayout"/> に従う。
    /// 毎フレーム <see cref="RefreshIfChanged"/> を呼ぶと、<see cref="SettingsForm.Version"/> が変わったときだけ uGUI を更新する。
    /// </summary>
    public sealed class SettingsView
    {
        public const string Title = "VR Keyboard の設定";

        sealed class Button
        {
            readonly SettingsHit _hit;
            readonly Image _background;

            public Button(Transform parent, Rect rect, string label, TMP_FontAsset font, float fontSize, SettingsHit hit)
            {
                _hit = hit;
                RectTransform root = UiBuilder.Rect(parent, "Button", rect.x, rect.y, rect.width, rect.height);
                _background = UiBuilder.AddImage(root.gameObject, KeyboardTheme.Cell);
                RectTransform labelRect = UiBuilder.Rect(root, "Label", 0f, 0f, rect.width, rect.height);
                UiBuilder.Stretch(labelRect);
                TextMeshProUGUI text = UiBuilder.AddText(labelRect.gameObject, font, fontSize, KeyboardTheme.Text, TextAlignmentOptions.Center);
                text.text = label;
            }

            public void Refresh(SettingsForm form)
            {
                if (form.Pressed == _hit) _background.color = KeyboardTheme.CellSelected;
                else if (form.Hover == _hit) _background.color = KeyboardTheme.SettingsButtonHover;
                else _background.color = KeyboardTheme.Cell;
            }
        }

        sealed class Row
        {
            readonly SettingsRowLayout _layout;
            readonly SettingsHit _sliderHit;
            readonly Button _decrease;
            readonly Button _increase;
            readonly RectTransform _fill;
            readonly RectTransform _thumb;
            readonly Image _thumbImage;
            readonly TextMeshProUGUI _value;

            public Row(Transform parent, SettingsRowLayout layout, KeyboardFonts fonts)
            {
                _layout = layout;
                SettingItem item = layout.Item;
                _sliderHit = new SettingsHit(SettingsTarget.Slider, item);

                Rect label = layout.Label;
                UiBuilder.Text(parent, "Label", label.x, label.y, label.width, label.height,
                    fonts.Regular, KeyboardTheme.SettingsLabelFontSize, KeyboardTheme.Text, TextAlignmentOptions.MidlineLeft).text = item.Label;

                _decrease = new Button(parent, layout.Decrease, "−", fonts.Bold, KeyboardTheme.SettingsButtonFontSize,
                    new SettingsHit(SettingsTarget.Decrease, item));
                _increase = new Button(parent, layout.Increase, "+", fonts.Bold, KeyboardTheme.SettingsButtonFontSize,
                    new SettingsHit(SettingsTarget.Increase, item));

                float trackTop = layout.Slider.center.y - KeyboardTheme.SettingsTrackHeight / 2f;
                UiBuilder.Image(parent, "Track", layout.TrackLeft, trackTop, layout.TrackWidth, KeyboardTheme.SettingsTrackHeight, KeyboardTheme.Field);
                _fill = UiBuilder.Image(parent, "Fill", layout.TrackLeft, trackTop, 0f, KeyboardTheme.SettingsTrackHeight, KeyboardTheme.CellSelected).rectTransform;
                _thumbImage = UiBuilder.Image(parent, "Thumb", 0f, 0f, SettingsLayout.ThumbHalfWidth * 2f, KeyboardTheme.SettingsThumbHeight, KeyboardTheme.Text);
                _thumb = _thumbImage.rectTransform;

                Rect value = layout.Value;
                _value = UiBuilder.Text(parent, "Value", value.x, value.y, value.width, value.height,
                    fonts.Bold, KeyboardTheme.SettingsLabelFontSize, KeyboardTheme.Text, TextAlignmentOptions.MidlineLeft);
            }

            public void Refresh(SettingsForm form)
            {
                _decrease.Refresh(form);
                _increase.Refresh(form);

                SettingItem item = _layout.Item;
                float value = item.Get(form.Settings);
                float thumbCenter = _layout.TrackLeft + item.Range.FractionOf(value) * _layout.TrackWidth;
                float trackTop = _layout.Slider.center.y - KeyboardTheme.SettingsTrackHeight / 2f;
                UiBuilder.Place(_fill, _layout.TrackLeft, trackTop, thumbCenter - _layout.TrackLeft, KeyboardTheme.SettingsTrackHeight);
                UiBuilder.Place(_thumb, thumbCenter - SettingsLayout.ThumbHalfWidth, _layout.Slider.center.y - KeyboardTheme.SettingsThumbHeight / 2f,
                    SettingsLayout.ThumbHalfWidth * 2f, KeyboardTheme.SettingsThumbHeight);
                bool active = form.DraggingItem == item || form.Hover == _sliderHit;
                _thumbImage.color = active ? KeyboardTheme.Caret : KeyboardTheme.Text;

                string text = item.Format(value);
                if (_value.text != text) _value.text = text;
            }
        }

        readonly SettingsForm _form;
        readonly List<Row> _rows = new List<Row>();
        readonly Button _reset;
        readonly TextMeshProUGUI _notice;
        int _renderedVersion = -1;

        public SettingsView(SettingsForm form, KeyboardFonts fonts, Transform canvasRoot)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
            if (fonts == null) throw new ArgumentNullException(nameof(fonts));
            if (canvasRoot == null) throw new ArgumentNullException(nameof(canvasRoot));
            SettingsLayout layout = form.Layout;

            RectTransform root = UiBuilder.Rect(canvasRoot, "Settings", 0f, 0f, SettingsLayout.Width, SettingsLayout.Height);
            UiBuilder.AddImage(root.gameObject, KeyboardTheme.SettingsBackground);

            Rect title = layout.Title;
            UiBuilder.Text(root, "Title", title.x, title.y, title.width, title.height,
                fonts.Bold, KeyboardTheme.SettingsTitleFontSize, KeyboardTheme.Text, TextAlignmentOptions.MidlineLeft).text = Title;

            foreach (SettingsGroupLayout group in layout.Groups)
            {
                Rect header = group.Title;
                UiBuilder.Text(root, "Header", header.x, header.y, header.width, header.height,
                    fonts.Bold, KeyboardTheme.SettingsHeaderFontSize, KeyboardTheme.Composition, TextAlignmentOptions.MidlineLeft).text = group.Group.Title;
                if (group.Group.Note != null)
                {
                    Rect note = group.Note;
                    UiBuilder.Text(root, "Note", note.x, note.y, note.width, note.height,
                        fonts.Regular, KeyboardTheme.SettingsNoteFontSize, KeyboardTheme.TextDim, TextAlignmentOptions.MidlineLeft).text = group.Group.Note;
                }
            }

            foreach (SettingsRowLayout row in layout.Rows) _rows.Add(new Row(root, row, fonts));

            _reset = new Button(root, layout.Reset, "既定に戻す", fonts.Regular, KeyboardTheme.SettingsLabelFontSize,
                new SettingsHit(SettingsTarget.Reset, null));

            Rect notice = layout.Notice;
            _notice = UiBuilder.Text(root, "Notice", notice.x, notice.y, notice.width, notice.height,
                fonts.Regular, KeyboardTheme.SettingsNoticeFontSize, KeyboardTheme.Warning, TextAlignmentOptions.MidlineLeft);
            _notice.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>表示に関わる状態が変わっていれば uGUI を更新して true を返す。</summary>
        public bool RefreshIfChanged()
        {
            int version = _form.Version;
            if (version == _renderedVersion) return false;
            _renderedVersion = version;

            foreach (Row row in _rows) row.Refresh(_form);
            _reset.Refresh(_form);
            string notice = _form.Notice ?? string.Empty;
            if (_notice.text != notice) _notice.text = notice;
            return true;
        }
    }
}
