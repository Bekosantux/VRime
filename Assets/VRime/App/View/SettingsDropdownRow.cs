using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面の選択肢の項目の行。いまの値を示すボタンと、押すと開く選択肢の一覧を持つ。
    /// どの項目の行かは <see cref="_key"/>（<see cref="SettingsCatalog"/> の項目のキー）で決める。項目名などのラベルはシーンに置いたまま変えない。
    /// 一覧は <see cref="SettingsForm.OpenDropdown"/> がこの項目の間だけ表示する（GameObject を切り替える）。
    /// ほかの行の上に描くため、一覧はこの行の外の、ページの直下の末尾に置く（<see cref="SettingsPage"/> が検証する）。
    /// </summary>
    public sealed class SettingsDropdownRow : MonoBehaviour
    {
        [Tooltip("項目のキー（設定ファイルの項目名と同じ）。theme。")]
        [SerializeField] string _key;

        [Tooltip("いまの値を示し、押すと一覧を開くボタンの地。当たり判定はこの矩形を使う。")]
        [SerializeField] Image _button;

        [Tooltip("いまの値の表示名。")]
        [SerializeField] TextMeshProUGUI _value;

        [Tooltip("選択肢の一覧。ページの直下の末尾に置く。")]
        [SerializeField] RectTransform _list;

        [Tooltip("選択肢。項目の選択肢と同じ数を同じ順に並べ、_list の下に置く。")]
        [SerializeField] SettingsDropdownOption[] _options = Array.Empty<SettingsDropdownOption>();

        ChoiceSettingItem _item;
        SettingsHit _buttonHit;
        ActiveTheme _theme;

        public string Key => _key;

        /// <summary>選択肢の一覧。</summary>
        public RectTransform List => _list;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_key)) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_key) + " が設定されていません。");
            SceneChecks.Require(_button, this, nameof(_button));
            SceneChecks.Require(_value, this, nameof(_value));
            SceneChecks.Require(_list, this, nameof(_list));
            if (_list.IsChildOf(transform))
                throw new InvalidOperationException(SceneChecks.PathOf(_list) + " は " + SceneChecks.PathOf(transform) + " の外に置く必要があります。");
            if (_options == null || _options.Length == 0)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_options) + " が空です。");
            SceneChecks.RequireAll(_options, _options.Length, this, nameof(_options));
            for (int i = 0; i < _options.Length; i++)
            {
                if (!_options[i].transform.IsChildOf(_list))
                    throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_options) + "[" + i + "] は " + SceneChecks.PathOf(_list) + " の下に置く必要があります。");
                _options[i].Validate();
            }
        }

        /// <summary>この行の操作の対象の配置を、canvas の左上を原点とする座標で作る。選択肢の数が項目と違えば例外。</summary>
        public SettingsDropdownLayout BuildLayout(ChoiceSettingItem item, RectTransform canvas)
        {
            CheckItem(item);
            var options = new List<Rect>(_options.Length);
            foreach (SettingsDropdownOption option in _options) options.Add(UiRects.RectIn(option.Rect, canvas));
            return new SettingsDropdownLayout(item, UiRects.RectIn(_button.rectTransform, canvas), options);
        }

        public void Initialize(ChoiceSettingItem item, ActiveTheme theme)
        {
            Validate();
            CheckItem(item);
            if (_options.Length != item.Labels.Count)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " の選択肢の数（" + _options.Length + "）が " + item.Key + " の選択肢の数（" + item.Labels.Count + "）と一致しません。", nameof(item));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _item = item;
            _buttonHit = SettingsHit.Dropdown(item);
            for (int i = 0; i < _options.Length; i++) _options[i].SetLabel(item.Labels[i]);
        }

        public void Refresh(SettingsForm form)
        {
            KeyboardTheme theme = _theme.Current;
            bool open = form.OpenDropdown == _item;
            if (form.Pressed == _buttonHit) _button.color = theme.SettingsButtonPressed;
            else if (open || form.Hover == _buttonHit) _button.color = theme.SettingsButtonHover;
            else _button.color = theme.SettingsButton;

            int selected = _item.GetIndex(form.Settings);
            string text = _item.Labels[selected];
            if (_value.text != text) _value.text = text;

            if (_list.gameObject.activeSelf != open) _list.gameObject.SetActive(open);
            if (!open) return;
            for (int i = 0; i < _options.Length; i++)
            {
                SettingsHit hit = SettingsHit.Option(_item, i);
                Color color;
                if (form.Pressed == hit) color = theme.SettingsButtonPressed;
                else if (form.Hover == hit) color = theme.SettingsButtonHover;
                else if (i == selected) color = theme.SettingsOptionSelected;
                else color = theme.SettingsOption;
                _options[i].SetColor(color);
            }
        }

        void CheckItem(ChoiceSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Key != _key)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " は " + _key + " の行です（渡された項目: " + item.Key + "）。", nameof(item));
        }
    }
}
