using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面の数値の項目の行。［−］・スライダー・［＋］と現在の値を表示する。
    /// どの項目の行かは <see cref="_key"/>（<see cref="SettingsCatalog"/> の項目のキー）で決める。項目名などのラベルはシーンに置いたまま変えない。
    /// スライダーの塗りとつまみは、値の範囲（<see cref="_track"/>）に対する割合をアンカーの x に設定して置く。
    /// </summary>
    public sealed class SettingsRow : MonoBehaviour
    {
        [Tooltip("項目のキー（設定ファイルの項目名と同じ）。downMeters / forwardMeters / rightMeters / tiltDegrees / widthMeters / triggerThreshold / stickThreshold / repeatDelaySeconds。")]
        [SerializeField] string _key;

        [SerializeField] SettingsButton _decrease;
        [SerializeField] SettingsButton _increase;

        [Tooltip("スライダーの操作を受ける領域。")]
        [SerializeField] RectTransform _slider;

        [Tooltip("値の範囲。左端が最小値、右端が最大値。")]
        [SerializeField] RectTransform _track;

        [Tooltip("値までの塗り。_track の直下に置き、アンカーの x を 0 から値の割合までにする。")]
        [SerializeField] RectTransform _fill;

        [Tooltip("つまみ。_track の直下に置き、アンカーの x を値の割合にする。")]
        [SerializeField] Image _thumb;

        [SerializeField] TextMeshProUGUI _value;

        NumberSettingItem _item;
        SettingsHit _sliderHit;
        ActiveTheme _theme;

        public string Key => _key;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_key)) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_key) + " が設定されていません。");
            SceneChecks.Require(_decrease, this, nameof(_decrease));
            SceneChecks.Require(_increase, this, nameof(_increase));
            SceneChecks.Require(_slider, this, nameof(_slider));
            SceneChecks.Require(_track, this, nameof(_track));
            SceneChecks.Require(_fill, this, nameof(_fill));
            SceneChecks.Require(_thumb, this, nameof(_thumb));
            SceneChecks.Require(_value, this, nameof(_value));
            if (_fill.parent != _track)
                throw new InvalidOperationException(SceneChecks.PathOf(_fill) + " は " + SceneChecks.PathOf(_track) + " の直下に置く必要があります。");
            if (_thumb.transform.parent != _track)
                throw new InvalidOperationException(SceneChecks.PathOf(_thumb.transform) + " は " + SceneChecks.PathOf(_track) + " の直下に置く必要があります。");
            _decrease.Validate();
            _increase.Validate();
        }

        /// <summary>この行の操作の対象の配置を、canvas の左上を原点とする座標で作る。</summary>
        public SettingsRowLayout BuildLayout(NumberSettingItem item, RectTransform canvas)
        {
            CheckItem(item);
            return new SettingsRowLayout(item,
                UiRects.RectIn(_decrease.Rect, canvas),
                UiRects.RectIn(_slider, canvas),
                UiRects.RectIn(_increase.Rect, canvas),
                UiRects.RectIn(_track, canvas));
        }

        public void Initialize(NumberSettingItem item, ActiveTheme theme)
        {
            Validate();
            CheckItem(item);
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _item = item;
            _sliderHit = SettingsHit.Slider(item);
            _decrease.Initialize(theme, SettingsHit.Decrease(item));
            _increase.Initialize(theme, SettingsHit.Increase(item));
        }

        public void Refresh(SettingsForm form)
        {
            _decrease.Refresh(form);
            _increase.Refresh(form);

            float value = _item.Get(form.Settings);
            float fraction = _item.Range.FractionOf(value);
            _fill.anchorMax = new Vector2(fraction, _fill.anchorMax.y);
            RectTransform thumb = _thumb.rectTransform;
            thumb.anchorMin = new Vector2(fraction, thumb.anchorMin.y);
            thumb.anchorMax = new Vector2(fraction, thumb.anchorMax.y);
            bool active = form.DraggingItem == _item || form.Hover == _sliderHit;
            _thumb.color = active ? _theme.Current.SettingsThumbActive : _theme.Current.SettingsThumb;

            string text = _item.Format(value);
            if (_value.text != text) _value.text = text;
        }

        void CheckItem(NumberSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Key != _key)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " は " + _key + " の行です（渡された項目: " + item.Key + "）。", nameof(item));
        }
    }
}
