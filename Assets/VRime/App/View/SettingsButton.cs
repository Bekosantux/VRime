using System;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面のボタン（［−］・［＋］・「既定に戻す」・「アプリを終了」・同意の画面のボタン）。背景の色で、指している・押していることを示す。
    /// 「アプリを終了」は、ほかのボタンと見分けられる色（<see cref="KeyboardTheme.SettingsQuit"/>）を使う。
    /// 当たり判定はこのオブジェクトの矩形を使う（<see cref="SettingsView.CreateLayout"/>）。ラベルはシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsButton : MonoBehaviour
    {
        [SerializeField] Image _background;

        SettingsHit _hit;
        ActiveTheme _theme;

        public RectTransform Rect => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
        }

        public void Initialize(ActiveTheme theme, SettingsHit hit)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            if (hit.Target != SettingsTarget.Decrease && hit.Target != SettingsTarget.Increase && hit.Target != SettingsTarget.Reset
                && hit.Target != SettingsTarget.Quit && hit.Target != SettingsTarget.ConsentAccept && hit.Target != SettingsTarget.ConsentCancel)
                throw new ArgumentException("ボタンの操作ではありません: " + hit.Target, nameof(hit));
            _hit = hit;
        }

        public void Refresh(SettingsForm form)
        {
            KeyboardTheme theme = _theme.Current;
            bool quit = _hit.Target == SettingsTarget.Quit;
            if (form.Pressed == _hit) _background.color = quit ? theme.SettingsQuitPressed : theme.SettingsButtonPressed;
            else if (form.Hover == _hit) _background.color = quit ? theme.SettingsQuitHover : theme.SettingsButtonHover;
            else _background.color = quit ? theme.SettingsQuit : theme.SettingsButton;
        }
    }
}
