using System;
using UnityEngine;
using UnityEngine.UI;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 設定画面のボタン（［−］・［＋］・「既定に戻す」・「アプリを終了」・同意の画面のボタン）。背景の色で、指している・押していることを示す。
    /// 当たり判定はこのオブジェクトの矩形を使う（<see cref="SettingsView.CreateLayout"/>）。ラベルはシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsButton : MonoBehaviour
    {
        [SerializeField] Image _background;

        KeyboardTheme _theme;
        SettingsHit _hit;

        public RectTransform Rect => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
        }

        public void Initialize(KeyboardTheme theme, SettingsHit hit)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            if (hit.Target != SettingsTarget.Decrease && hit.Target != SettingsTarget.Increase && hit.Target != SettingsTarget.Reset
                && hit.Target != SettingsTarget.Quit && hit.Target != SettingsTarget.ConsentAccept && hit.Target != SettingsTarget.ConsentCancel)
                throw new ArgumentException("ボタンの操作ではありません: " + hit.Target, nameof(hit));
            _hit = hit;
        }

        public void Refresh(SettingsForm form)
        {
            if (form.Pressed == _hit) _background.color = _theme.SettingsButtonPressed;
            else if (form.Hover == _hit) _background.color = _theme.SettingsButtonHover;
            else _background.color = _theme.SettingsButton;
        }
    }
}
