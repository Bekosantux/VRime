using System;
using System.Collections.Generic;
using UnityEngine;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面の 1 カテゴリのページ。カテゴリの項目の行を子に持ち、選んでいるカテゴリのページだけを表示する（GameObject を切り替える）。
    /// どのカテゴリのページかは <see cref="_key"/>（<see cref="SettingsCatalog"/> のカテゴリのキー）で決める。
    /// タブは表示を切り替えても見えている必要があるため、ページの外に置く。
    /// </summary>
    public sealed class SettingsPage : MonoBehaviour
    {
        [Tooltip("カテゴリのキー。display / grip / vrchat / privacy。")]
        [SerializeField] string _key;

        [Tooltip("このカテゴリを選ぶタブ。ページの外に置く。")]
        [SerializeField] SettingsTab _tab;

        [Tooltip("数値の項目の行。")]
        [SerializeField] SettingsRow[] _rows = Array.Empty<SettingsRow>();

        [Tooltip("オン・オフの項目の行。")]
        [SerializeField] SettingsToggleRow[] _toggles = Array.Empty<SettingsToggleRow>();

        SettingsCategory _category;

        public string Key => _key;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_key)) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_key) + " が設定されていません。");
            SceneChecks.Require(_tab, this, nameof(_tab));
            if (_tab.transform.IsChildOf(transform))
                throw new InvalidOperationException(SceneChecks.PathOf(_tab.transform) + " は " + SceneChecks.PathOf(transform) + " の外に置く必要があります。");
            if (_rows == null) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_rows) + " が null です。");
            if (_toggles == null) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_toggles) + " が null です。");
            SceneChecks.RequireAll(_rows, _rows.Length, this, nameof(_rows));
            SceneChecks.RequireAll(_toggles, _toggles.Length, this, nameof(_toggles));
            if (_rows.Length + _toggles.Length == 0) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に項目の行がありません。");
            for (int i = 0; i < _rows.Length; i++)
            {
                SceneChecks.RequireUnder(_rows[i], this, this, nameof(_rows) + "[" + i + "]");
                _rows[i].Validate();
            }
            for (int i = 0; i < _toggles.Length; i++)
            {
                SceneChecks.RequireUnder(_toggles[i], this, this, nameof(_toggles) + "[" + i + "]");
                _toggles[i].Validate();
            }
            _tab.Validate();
        }

        /// <summary>
        /// シーンの uGUI の矩形から、category のページの配置を作る。行と項目はキーで対応させ、過不足・重複・種類の違いは例外にする。
        /// シーンの状態は変えない。
        /// </summary>
        public SettingsPageLayout BuildLayout(SettingsCategory category, RectTransform canvas)
        {
            CheckCategory(category);
            Validate();
            Dictionary<string, SettingsRow> rows = ByKey(_rows, row => row.Key, row => row.transform);
            Dictionary<string, SettingsToggleRow> toggles = ByKey(_toggles, toggle => toggle.Key, toggle => toggle.transform);

            var rowLayouts = new List<SettingsRowLayout>();
            var toggleLayouts = new List<SettingsToggleLayout>();
            foreach (SettingItem item in category.Items)
            {
                switch (item)
                {
                    case NumberSettingItem number:
                        if (!rows.TryGetValue(item.Key, out SettingsRow row))
                            throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に " + item.Key + " の数値の行がありません。");
                        rowLayouts.Add(row.BuildLayout(number, canvas));
                        break;
                    case ToggleSettingItem toggle:
                        if (!toggles.TryGetValue(item.Key, out SettingsToggleRow toggleRow))
                            throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に " + item.Key + " のオン・オフの行がありません。");
                        toggleLayouts.Add(toggleRow.BuildLayout(toggle, canvas));
                        break;
                    default:
                        throw new InvalidOperationException("未知の種類の項目です: " + item.Key);
                }
            }
            if (rowLayouts.Count != _rows.Length || toggleLayouts.Count != _toggles.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に " + category.Key + " の項目にない行があります。");

            return new SettingsPageLayout(category, UiRects.RectIn(_tab.Rect, canvas), rowLayouts, toggleLayouts);
        }

        /// <param name="layout">このページの <see cref="BuildLayout"/> で作った配置。</param>
        public void Initialize(SettingsPageLayout layout, KeyboardTheme theme)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            CheckCategory(layout.Category);
            Validate();
            Dictionary<string, SettingsRow> rows = ByKey(_rows, row => row.Key, row => row.transform);
            Dictionary<string, SettingsToggleRow> toggles = ByKey(_toggles, toggle => toggle.Key, toggle => toggle.transform);
            foreach (SettingsRowLayout row in layout.Rows) rows[row.Item.Key].Initialize(row.Item, theme);
            foreach (SettingsToggleLayout toggle in layout.Toggles) toggles[toggle.Item.Key].Initialize(toggle.Item, theme);
            _tab.Initialize(theme, layout.Category);
            _category = layout.Category;
        }

        public void Refresh(SettingsForm form)
        {
            _tab.Refresh(form);
            bool selected = form.Category == _category;
            if (gameObject.activeSelf != selected) gameObject.SetActive(selected);
            if (!selected) return;
            foreach (SettingsRow row in _rows) row.Refresh(form);
            foreach (SettingsToggleRow toggle in _toggles) toggle.Refresh(form);
        }

        void CheckCategory(SettingsCategory category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (category.Key != _key)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " は " + _key + " のページです（渡されたカテゴリ: " + category.Key + "）。", nameof(category));
        }

        static Dictionary<string, T> ByKey<T>(T[] views, Func<T, string> key, Func<T, Transform> transformOf)
        {
            var byKey = new Dictionary<string, T>();
            foreach (T view in views)
            {
                if (byKey.TryGetValue(key(view), out T other))
                    throw new InvalidOperationException(SceneChecks.PathOf(transformOf(view)) + " と " + SceneChecks.PathOf(transformOf(other)) + " の項目（" + key(view) + "）が重複しています。");
                byKey.Add(key(view), view);
            }
            return byKey;
        }
    }
}
