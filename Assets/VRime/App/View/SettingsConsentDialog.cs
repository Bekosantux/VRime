using System;
using UnityEngine;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// オンにする前に同意が必要な項目の、同意の画面。<see cref="SettingsForm.ConsentItem"/> がこの項目の間だけ表示する（GameObject を切り替える）。
    /// どの項目の画面かは <see cref="_key"/>（<see cref="SettingsCatalog"/> の項目のキー）で決める。注意事項の文章はシーンに置いたまま変えない。
    /// ページの上に重ねて描くため、ページより後（下）に置く。
    /// </summary>
    public sealed class SettingsConsentDialog : MonoBehaviour
    {
        [Tooltip("項目のキー（設定ファイルの項目名と同じ）。useGoogleConversion。")]
        [SerializeField] string _key;

        [Tooltip("同意して項目をオンにするボタン。")]
        [SerializeField] SettingsButton _accept;

        [Tooltip("同意せずに閉じるボタン。")]
        [SerializeField] SettingsButton _cancel;

        ToggleSettingItem _item;

        public string Key => _key;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_key)) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_key) + " が設定されていません。");
            SceneChecks.Require(_accept, this, nameof(_accept));
            SceneChecks.Require(_cancel, this, nameof(_cancel));
            SceneChecks.RequireUnder(_accept, this, this, nameof(_accept));
            SceneChecks.RequireUnder(_cancel, this, this, nameof(_cancel));
            _accept.Validate();
            _cancel.Validate();
        }

        /// <summary>この画面の操作の対象の配置を、canvas の左上を原点とする座標で作る。シーンの状態は変えない。</summary>
        public SettingsConsentLayout BuildLayout(ToggleSettingItem item, RectTransform canvas)
        {
            CheckItem(item);
            Validate();
            return new SettingsConsentLayout(item, UiRects.RectIn(_accept.Rect, canvas), UiRects.RectIn(_cancel.Rect, canvas));
        }

        /// <param name="layout">この画面の <see cref="BuildLayout"/> で作った配置。</param>
        public void Initialize(SettingsConsentLayout layout, KeyboardTheme theme)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            CheckItem(layout.Item);
            Validate();
            _accept.Initialize(theme, SettingsHit.ConsentAccept);
            _cancel.Initialize(theme, SettingsHit.ConsentCancel);
            _item = layout.Item;
        }

        public void Refresh(SettingsForm form)
        {
            bool open = form.ConsentItem == _item;
            if (gameObject.activeSelf != open) gameObject.SetActive(open);
            if (!open) return;
            _accept.Refresh(form);
            _cancel.Refresh(form);
        }

        void CheckItem(ToggleSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Key != _key)
                throw new ArgumentException(SceneChecks.PathOf(transform) + " は " + _key + " の同意の画面です（渡された項目: " + item.Key + "）。", nameof(item));
        }
    }
}
