using System;
using UnityEngine;
using UnityEngine.UI;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 設定画面のボタン（［−］・［＋］・「既定に戻す」・「アプリを終了」・同意の画面のボタン）。背景の色で、指している・押していることを示す。
    /// 当たり判定はこのオブジェクトの矩形を使う（<see cref="SettingsView.CreateLayout"/>）。ラベルはシーンに置いたまま変えない。
    /// </summary>
    public sealed class SettingsButton : MonoBehaviour
    {
        [SerializeField] Image _background;

        SettingsHit _hit;
        Color _normal;
        Color _hover;
        Color _pressed;

        public RectTransform Rect => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
        }

        public void Initialize(KeyboardTheme theme, SettingsHit hit)
        {
            Validate();
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (hit.Target != SettingsTarget.Decrease && hit.Target != SettingsTarget.Increase && hit.Target != SettingsTarget.Reset
                && hit.Target != SettingsTarget.Quit && hit.Target != SettingsTarget.ConsentAccept && hit.Target != SettingsTarget.ConsentCancel)
                throw new ArgumentException("ボタンの操作ではありません: " + hit.Target, nameof(hit));
            _hit = hit;
            if (hit.Target == SettingsTarget.Quit)
            {
                _normal = theme.SettingsQuit;
                _hover = theme.SettingsQuitHover;
                _pressed = theme.SettingsQuitPressed;
            }
            else
            {
                _normal = theme.SettingsButton;
                _hover = theme.SettingsButtonHover;
                _pressed = theme.SettingsButtonPressed;
            }
        }

        public void Refresh(SettingsForm form)
        {
            if (form.Pressed == _hit) _background.color = _pressed;
            else if (form.Hover == _hit) _background.color = _hover;
            else _background.color = _normal;
        }
    }
}
