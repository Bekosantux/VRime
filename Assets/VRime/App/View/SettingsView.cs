using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面（<see cref="SettingsForm"/>）の状態を uGUI に反映する。設定画面のキャンバスに置く。
    /// 操作の対象の配置（<see cref="SettingsLayout"/>）はシーンの uGUI の矩形から作るため、シーンで動かした位置がそのまま当たり判定になる。
    /// 毎フレーム <see cref="RefreshIfChanged"/> を呼ぶと、<see cref="SettingsForm.Version"/> が変わったときだけ uGUI を更新する。
    /// 題名・見出し・説明・項目名はシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        [Tooltip("カテゴリのページ。各カテゴリに1つずつ、カテゴリの順に並べる（先頭を最初に表示する）。")]
        [SerializeField] SettingsPage[] _pages = Array.Empty<SettingsPage>();

        [SerializeField] SettingsButton _reset;

        [SerializeField] SettingsButton _quit;

        [Tooltip("同意の画面。オンにする前に同意が必要な項目ごとに 1 つずつ、カテゴリの項目の順に並べる。ページより後（下）に置く。")]
        [SerializeField] SettingsConsentDialog[] _consents = Array.Empty<SettingsConsentDialog>();

        [Tooltip("通知（設定ファイルの問題・保存の失敗）を表示する。")]
        [SerializeField] TextMeshProUGUI _notice;

        SettingsForm _form;
        int _renderedVersion = -1;

        RectTransform Canvas => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_reset, this, nameof(_reset));
            SceneChecks.Require(_quit, this, nameof(_quit));
            SceneChecks.Require(_notice, this, nameof(_notice));
            if (_pages == null || _pages.Length == 0) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_pages) + " が空です。");
            SceneChecks.RequireAll(_pages, _pages.Length, this, nameof(_pages));
            SceneChecks.RequireUnder(_reset, this, this, nameof(_reset));
            SceneChecks.RequireUnder(_quit, this, this, nameof(_quit));
            SceneChecks.RequireUnder(_notice, this, this, nameof(_notice));
            for (int i = 0; i < _pages.Length; i++)
            {
                SceneChecks.RequireUnder(_pages[i], this, this, nameof(_pages) + "[" + i + "]");
                _pages[i].Validate();
            }
            _reset.Validate();
            _quit.Validate();
            if (_consents == null) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_consents) + " が null です。");
            SceneChecks.RequireAll(_consents, _consents.Length, this, nameof(_consents));
            for (int i = 0; i < _consents.Length; i++)
            {
                SceneChecks.RequireUnder(_consents[i], this, this, nameof(_consents) + "[" + i + "]");
                _consents[i].Validate();
            }
        }

        /// <summary>
        /// シーンの uGUI の矩形から、categories の操作の対象の配置を作る。ページとカテゴリは同じ順に並べ、キーで照合する。
        /// 過不足・順序の違いは例外にする。シーンの状態は変えない。
        /// </summary>
        public SettingsLayout CreateLayout(IReadOnlyList<SettingsCategory> categories)
        {
            if (categories == null) throw new ArgumentNullException(nameof(categories));
            Validate();
            if (categories.Count != _pages.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " のページの数（" + _pages.Length + "）が設定のカテゴリの数（" + categories.Count + "）と一致しません。");

            var pages = new List<SettingsPageLayout>(categories.Count);
            for (int i = 0; i < categories.Count; i++)
            {
                SettingsCategory category = categories[i] ?? throw new ArgumentException("null のカテゴリがあります。", nameof(categories));
                if (_pages[i].Key != category.Key)
                    throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_pages) + "[" + i + "] は " + category.Key + " のページである必要があります（" + _pages[i].Key + " です）。");
                pages.Add(_pages[i].BuildLayout(category, Canvas));
            }

            var consents = new List<SettingsConsentLayout>();
            foreach (SettingsCategory category in categories)
            {
                foreach (SettingItem item in category.Items)
                {
                    if (!(item is ToggleSettingItem toggle) || !toggle.RequiresConsentToEnable) continue;
                    int i = consents.Count;
                    if (i >= _consents.Length)
                        throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_consents) + " に " + toggle.Key + " の同意の画面がありません。");
                    if (_consents[i].Key != toggle.Key)
                        throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_consents) + "[" + i + "] は " + toggle.Key + " の同意の画面である必要があります（" + _consents[i].Key + " です）。");
                    consents.Add(_consents[i].BuildLayout(toggle, Canvas));
                }
            }
            if (consents.Count != _consents.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_consents) + " に、同意が必要な項目にない画面があります。");

            return new SettingsLayout(Canvas.rect.size, pages, UiRects.RectIn(_reset.Rect, Canvas), UiRects.RectIn(_quit.Rect, Canvas), consents);
        }

        /// <param name="form">このビューの <see cref="CreateLayout"/> で作った配置を持つ設定画面。</param>
        public void Initialize(SettingsForm form, KeyboardTheme theme)
        {
            Validate();
            _form = form ?? throw new ArgumentNullException(nameof(form));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            IReadOnlyList<SettingsPageLayout> pages = form.Layout.Pages;
            if (pages.Count != _pages.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " のページの数が配置のページの数と一致しません。");
            for (int i = 0; i < pages.Count; i++) _pages[i].Initialize(pages[i], theme);
            _reset.Initialize(theme, SettingsHit.Reset);
            _quit.Initialize(theme, SettingsHit.Quit);
            IReadOnlyList<SettingsConsentLayout> consents = form.Layout.Consents;
            if (consents.Count != _consents.Length)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の同意の画面の数が配置の同意の画面の数と一致しません。");
            for (int i = 0; i < consents.Count; i++) _consents[i].Initialize(consents[i], theme);
            _renderedVersion = -1;
        }

        /// <summary>表示に関わる状態が変わっていれば uGUI を更新して true を返す。</summary>
        public bool RefreshIfChanged()
        {
            int version = _form.Version;
            if (version == _renderedVersion) return false;
            _renderedVersion = version;

            foreach (SettingsPage page in _pages) page.Refresh(_form);
            _reset.Refresh(_form);
            _quit.Refresh(_form);
            foreach (SettingsConsentDialog consent in _consents) consent.Refresh(_form);
            string notice = _form.Notice ?? string.Empty;
            if (_notice.text != notice) _notice.text = notice;
            return true;
        }
    }
}
