using System;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面のカテゴリのタブ。背景の色で、選んでいる・指している・押していることを示す。
    /// 当たり判定はこのオブジェクトの矩形を使う。ラベルはシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsTab : MonoBehaviour
    {
        [SerializeField] Image _background;

        KeyboardTheme _theme;
        SettingsCategory _category;
        SettingsHit _hit;

        public RectTransform Rect => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
        }

        public void Initialize(KeyboardTheme theme, SettingsCategory category)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _category = category ?? throw new ArgumentNullException(nameof(category));
            _hit = SettingsHit.Tab(category);
        }

        public void Refresh(SettingsForm form)
        {
            if (form.Category == _category) _background.color = _theme.SettingsTabSelected;
            else if (form.Pressed == _hit) _background.color = _theme.SettingsButtonPressed;
            else if (form.Hover == _hit) _background.color = _theme.SettingsButtonHover;
            else _background.color = _theme.SettingsTab;
        }
    }
}
