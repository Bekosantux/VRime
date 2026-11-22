using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRime.App.View
{
    public enum GripChipState
    {
        Selected,
        Active,
        Inactive,
    }

    /// <summary>モード行のチップ（入力モード・全角/半角・大文字/小文字）。</summary>
    public sealed class GripPanelChip : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _label;

        ActiveTheme _theme;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
            SceneChecks.Require(_label, this, nameof(_label));
        }

        public void Initialize(ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public void Set(string label, GripChipState state)
        {
            switch (state)
            {
                case GripChipState.Selected:
                    _background.color = _theme.Current.ChipSelected;
                    _label.color = _theme.Current.ChipText;
                    break;
                case GripChipState.Active:
                    _background.color = _theme.Current.ChipActive;
                    _label.color = _theme.Current.ChipText;
                    break;
                case GripChipState.Inactive:
                    _background.color = _theme.Current.Chip;
                    _label.color = _theme.Current.ChipTextInactive;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
            if (_label.text != label) _label.text = label;
        }
    }
}
