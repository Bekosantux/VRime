using System;
using TMPro;
using UnityEngine;

namespace VRime.App.View
{
    /// <summary>
    /// パネルの見出し。役割（「子音」「母音」「機能」）を示し、機能レイヤーを表示している間は役割の色を変える。
    /// </summary>
    public sealed class GripPanelCaption : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI _role;

        ActiveTheme _theme;

        public void Validate()
        {
            SceneChecks.Require(_role, this, nameof(_role));
        }

        public void Initialize(ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public void Set(string role, bool functionLayer)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (_role.text != role) _role.text = role;
            _role.color = functionLayer ? _theme.Current.CaptionRoleFunction : _theme.Current.CaptionRole;
        }
    }
}
