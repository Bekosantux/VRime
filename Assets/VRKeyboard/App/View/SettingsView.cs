using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 設定画面（<see cref="SettingsForm"/>）の状態を uGUI に反映する。設定画面のキャンバスに置く。
    /// 操作の対象の配置（<see cref="SettingsLayout"/>）はシーンの uGUI の矩形から作るため、シーンで動かした位置がそのまま当たり判定になる。
    /// 毎フレーム <see cref="RefreshIfChanged"/> を呼ぶと、<see cref="SettingsForm.Version"/> が変わったときだけ uGUI を更新する。
    /// 題名・見出し・説明・項目名はシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        [Tooltip("項目の行。各項目に1行ずつ、ちょうど揃える。")]
        [SerializeField] SettingsRow[] _rows = Array.Empty<SettingsRow>();

        [SerializeField] SettingsButton _reset;

        [Tooltip("通知（設定ファイルの問題・保存の失敗）を表示する。")]
        [SerializeField] TextMeshProUGUI _notice;

        readonly List<SettingsRow> _activeRows = new List<SettingsRow>();
        SettingsForm _form;
        int _renderedVersion = -1;

        RectTransform Canvas => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_reset, this, nameof(_reset));
            SceneChecks.Require(_notice, this, nameof(_notice));
            if (_rows == null || _rows.Length == 0) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_rows) + " が空です。");
            SceneChecks.RequireAll(_rows, _rows.Length, this, nameof(_rows));
            SceneChecks.RequireUnder(_reset, this, this, nameof(_reset));
            SceneChecks.RequireUnder(_notice, this, this, nameof(_notice));
            for (int i = 0; i < _rows.Length; i++)
            {
                SceneChecks.RequireUnder(_rows[i], this, this, nameof(_rows) + "[" + i + "]");
                _rows[i].Validate();
            }
            _reset.Validate();
        }

        /// <summary>
        /// シーンの uGUI の矩形から、items の操作の対象の配置を作る。行と項目はキーで対応させ、過不足・重複は例外にする。
        /// シーンの状態は変えない。
        /// </summary>
        public SettingsLayout CreateLayout(IReadOnlyList<SettingItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Validate();
            Dictionary<string, SettingsRow> rows = RowsByKey();

            var layouts = new List<SettingsRowLayout>(items.Count);
            var used = new HashSet<string>();
            foreach (SettingItem item in items)
            {
                if (item == null) throw new ArgumentException("null の項目があります。", nameof(items));
                if (!rows.TryGetValue(item.Key, out SettingsRow row))
                    throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に " + item.Key + " の行がありません。");
                used.Add(item.Key);
                layouts.Add(row.BuildLayout(item, Canvas));
            }
            foreach (string key in rows.Keys)
            {
                if (!used.Contains(key))
                    throw new InvalidOperationException(SceneChecks.PathOf(rows[key].transform) + " の " + key + " は設定の項目にありません。");
            }

            return new SettingsLayout(Canvas.rect.size, layouts, UiRects.RectIn(_reset.Rect, Canvas));
        }

        /// <param name="form">このビューの <see cref="CreateLayout"/> で作った配置を持つ設定画面。</param>
        public void Initialize(SettingsForm form, KeyboardTheme theme)
        {
            Validate();
            _form = form ?? throw new ArgumentNullException(nameof(form));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            Dictionary<string, SettingsRow> rows = RowsByKey();

            _activeRows.Clear();
            foreach (SettingsRowLayout layout in form.Layout.Rows)
            {
                if (!rows.TryGetValue(layout.Item.Key, out SettingsRow row))
                    throw new InvalidOperationException(SceneChecks.PathOf(transform) + " に " + layout.Item.Key + " の行がありません。");
                row.Initialize(layout.Item, theme);
                _activeRows.Add(row);
            }
            if (_activeRows.Count != _rows.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の行の数が設定の項目の数と一致しません。");
            _reset.Initialize(theme, new SettingsHit(SettingsTarget.Reset, null));
            _renderedVersion = -1;
        }

        /// <summary>表示に関わる状態が変わっていれば uGUI を更新して true を返す。</summary>
        public bool RefreshIfChanged()
        {
            int version = _form.Version;
            if (version == _renderedVersion) return false;
            _renderedVersion = version;

            foreach (SettingsRow row in _activeRows) row.Refresh(_form);
            _reset.Refresh(_form);
            string notice = _form.Notice ?? string.Empty;
            if (_notice.text != notice) _notice.text = notice;
            return true;
        }

        Dictionary<string, SettingsRow> RowsByKey()
        {
            var rows = new Dictionary<string, SettingsRow>();
            foreach (SettingsRow row in _rows)
            {
                if (rows.TryGetValue(row.Key, out SettingsRow other))
                    throw new InvalidOperationException(SceneChecks.PathOf(row.transform) + " と " + SceneChecks.PathOf(other.transform) + " の項目（" + row.Key + "）が重複しています。");
                rows.Add(row.Key, row);
            }
            return rows;
        }
    }
}
