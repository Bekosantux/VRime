using System;
using TMPro;
using UnityEngine;
using VRime.Core.Input;

namespace VRime.App.View
{
    /// <summary>
    /// パネルの見出し。手の名前（「左手」）と役割（「子音」「母音」「機能」）を別の文字で示し、機能ページを開いている間は役割の色を変える。
    /// 2つの文字の並びと寄せ（左右のパネルで逆）はシーンの HorizontalLayoutGroup で決める。
    /// </summary>
    public sealed class GripPanelCaption : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI _hand;
        [SerializeField] TextMeshProUGUI _role;

        KeyboardTheme _theme;

        public void Validate()
        {
            SceneChecks.Require(_hand, this, nameof(_hand));
            SceneChecks.Require(_role, this, nameof(_role));
        }

        public void Initialize(KeyboardTheme theme)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
        }

        public void Set(Hand hand, string role, bool functionPage)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            string handText = hand == Hand.Left ? "左手" : "右手";
            if (_hand.text != handText) _hand.text = handText;
            if (_role.text != role) _role.text = role;
            _role.color = functionPage ? _theme.CaptionRoleFunction : _theme.CaptionRole;
        }
    }
}
