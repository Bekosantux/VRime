using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRime.App.View
{
    /// <summary>
    /// ドロップダウンの選択肢の一覧の 1 行（<see cref="SettingsDropdownRow"/>）。背景の色で、いまの値・指している・押していることを示す。
    /// 当たり判定はこのオブジェクトの矩形を使う。文字は項目の選択肢の表示名に置き換える。
    /// </summary>
    public sealed class SettingsDropdownOption : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _label;

        public RectTransform Rect => (RectTransform)transform;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
            SceneChecks.Require(_label, this, nameof(_label));
        }

        public void SetLabel(string label)
        {
            if (_label.text != label) _label.text = label;
        }

        public void SetColor(Color color)
        {
            _background.color = color;
        }
    }
}
