using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRime.App.View
{
    /// <summary>
    /// 候補欄の文節の見出し、または候補の1行。背景と文字を持ち、幅は文字に合わせて決まる（<see cref="LayoutElement"/> に設定する）。
    /// 左右の余白は <see cref="HorizontalLayoutGroup"/> の padding を使う。
    /// </summary>
    public sealed class CandidateLabel : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _label;
        [SerializeField] HorizontalLayoutGroup _layout;
        [SerializeField] LayoutElement _size;

        ActiveTheme _theme;
        float _maxWidth;
        string _text;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
            SceneChecks.Require(_label, this, nameof(_label));
            SceneChecks.Require(_layout, this, nameof(_layout));
            SceneChecks.Require(_size, this, nameof(_size));
        }

        /// <param name="maxWidth">最大の幅。これを超える文字は省略記号にする。</param>
        public void Initialize(ActiveTheme theme, float maxWidth)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            if (!(maxWidth > _layout.padding.horizontal))
                throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth, "左右の余白より狭い幅です。");
            _maxWidth = maxWidth;
            _text = null;
        }

        public void Set(string text, bool selected)
        {
            if (_text != text)
            {
                _text = text;
                Fit(text);
            }
            _background.color = selected ? _theme.Current.CandidateSelected : _theme.Current.Candidate;
        }

        // 幅は文字の長さに合わせる。上限を超えるときは、収まる長さまで切って省略記号を付ける。
        void Fit(string text)
        {
            float padding = _layout.padding.horizontal;
            _size.minWidth = TextFitting.Fit(_label, text, _maxWidth - padding) + padding;
            _size.preferredWidth = _size.minWidth;
        }
    }
}
