using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面のオン・オフの項目の行。スイッチと現在の値を表示する。行の矩形全体が、押すと切り替わる領域になる。
    /// 値をアプリの外が持つ項目で値を利用できない間は、スイッチをオフの色にし、値の欄に「利用不可」と表示する。
    /// どの項目の行かは <see cref="_key"/>（<see cref="SettingsCatalog"/> の項目のキー）で決める。項目名などのラベルはシーンに置いたまま変えない。
    /// ハンドルは、アンカーとピボットの x をオフで 0（左端）、オンで 1（右端）にして置き、上下の余白と同じ幅だけ内側へ寄せる。
    /// </summary>
    public sealed class SettingsToggleRow : MonoBehaviour
    {
        [Tooltip("項目のキー（SettingsCatalog の項目のキー）。focusOnStartup / showTypingText / resetPasteModeAfterSend / autoLaunch / useGoogleConversion。")]
        [SerializeField] string _key;

        [Tooltip("スイッチの背景。色でオン・オフを示す。")]
        [SerializeField] Image _switch;

        [Tooltip("スイッチのハンドル。_switch の直下に置く。")]
        [SerializeField] Image _knob;

        [SerializeField] TextMeshProUGUI _value;

        const string UnavailableText = "利用不可";

        SwitchSettingItem _item;
        SettingsHit _hit;
        ActiveTheme _theme;

        public string Key => _key;

        RectTransform Area => (RectTransform)transform;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_key)) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_key) + " が設定されていません。");
            SceneChecks.Require(_switch, this, nameof(_switch));
            SceneChecks.Require(_knob, this, nameof(_knob));
            SceneChecks.Require(_value, this, nameof(_value));
            if (_knob.transform.parent != _switch.transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_knob.transform) + " は " + SceneChecks.PathOf(_switch.transform) + " の直下に置く必要があります。");
        }

        /// <summary>この行の操作の対象の配置を、canvas の左上を原点とする座標で作る。</summary>
        public SettingsToggleLayout BuildLayout(SwitchSettingItem item, RectTransform canvas)
        {
            CheckItem(item);
            return new SettingsToggleLayout(item, UiRects.RectIn(Area, canvas));
        }

        public void Initialize(SwitchSettingItem item, ActiveTheme theme)
        {
            Validate();
            CheckItem(item);
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _item = item;
            _hit = SettingsHit.Toggle(item);
        }

        public void Refresh(SettingsForm form)
        {
            bool? value = form.IsOn(_item);
            bool on = value == true;
            _switch.color = on ? _theme.Current.SettingsSwitchOn : _theme.Current.SettingsSwitchOff;

            float x = on ? 1f : 0f;
            RectTransform knob = _knob.rectTransform;
            float inset = (_switch.rectTransform.rect.height - knob.rect.height) / 2f;
            knob.anchorMin = new Vector2(x, knob.anchorMin.y);
            knob.anchorMax = new Vector2(x, knob.anchorMax.y);
            knob.pivot = new Vector2(x, knob.pivot.y);
            knob.anchoredPosition = new Vector2(on ? -inset : inset, knob.anchoredPosition.y);
            bool active = form.Pressed == _hit || form.Hover == _hit;
            _knob.color = active ? _theme.Current.SettingsThumbActive : _theme.Current.SettingsThumb;

            string text = !value.HasValue ? UnavailableText : on ? "オン" : "オフ";
            if (_value.text != text) _value.text = text;
        }

        void CheckItem(SwitchSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Key != _key)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " は " + _key + " の行です（渡された項目: " + item.Key + "）。", nameof(item));
        }
    }
}
