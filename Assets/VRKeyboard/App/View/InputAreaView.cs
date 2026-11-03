using System;
using System.Text;
using TMPro;
using UnityEngine;
using VRKeyboard.App.Output;
using VRKeyboard.Core;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 入力欄（確定済みの文字列・未確定のかな・キャレット）と、その下の状態行（通知・送信先・文字数）。
    /// 文字数は、上限があれば「文字数 / 上限」、なければ「文字数 文字」と表示する。
    /// 未確定のかなは色と下線で確定済みと区別する。行数が入力欄を超えるときは、キャレットの行が見える位置までずらす。
    /// </summary>
    public sealed class InputAreaView : MonoBehaviour
    {
        [Tooltip("入力欄の文字を表示する範囲。はみ出した行は隠す。")]
        [SerializeField] RectTransform _viewport;

        [Tooltip("入力した文字。_viewport の子で、リッチテキストを有効にする（未確定のかなの色と下線に使う）。")]
        [SerializeField] TextMeshProUGUI _text;

        [Tooltip("キャレット。_text の子。幅はシーンの値を使い、位置と高さは文字に合わせる。")]
        [SerializeField] RectTransform _caret;

        [SerializeField] TextMeshProUGUI _notice;

        [Tooltip("送信先の名前。")]
        [SerializeField] TextMeshProUGUI _target;

        [Tooltip("文字数（上限があれば上限も）。")]
        [SerializeField] TextMeshProUGUI _length;

        KeyboardTheme _theme;
        RectTransform _textRect;
        float _caretWidth;
        string _compositionOpenTag;
        readonly StringBuilder _builder = new StringBuilder();

        public void Validate()
        {
            SceneChecks.Require(_viewport, this, nameof(_viewport));
            SceneChecks.Require(_text, this, nameof(_text));
            SceneChecks.Require(_caret, this, nameof(_caret));
            SceneChecks.Require(_notice, this, nameof(_notice));
            SceneChecks.Require(_target, this, nameof(_target));
            SceneChecks.Require(_length, this, nameof(_length));
            if (_text.transform.parent != _viewport)
                throw new InvalidOperationException(SceneChecks.PathOf(_text.transform) + " は " + SceneChecks.PathOf(_viewport) + " の直下に置く必要があります。");
            if (_caret.parent != _text.transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_caret) + " は " + SceneChecks.PathOf(_text.transform) + " の直下に置く必要があります。");
            if (!_text.richText)
                throw new InvalidOperationException(SceneChecks.PathOf(_text.transform) + " のリッチテキストが無効です。");
        }

        public void Initialize(KeyboardTheme theme)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _textRect = _text.rectTransform;
            _caretWidth = _caret.sizeDelta.x;
            _compositionOpenTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(theme.Composition) + "><u>";
        }

        public void Refresh(KeyboardSession session, OutputTarget target)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (target == null) throw new ArgumentNullException(nameof(target));
            RefreshText(session);

            if (_notice.text != session.Notice) _notice.text = session.Notice;
            string targetText = "送信先: " + target.Name;
            if (_target.text != targetText) _target.text = targetText;
            RefreshLength(session);
        }

        void RefreshLength(KeyboardSession session)
        {
            int length = session.Length;
            int? limit = session.LengthLimit;
            string text;
            Color color;
            if (limit.HasValue)
            {
                text = length + " / " + limit.Value;
                color = length > limit.Value ? _theme.LengthOverLimit
                    : length == limit.Value ? _theme.LengthAtLimit
                    : _theme.Length;
            }
            else
            {
                text = length + " 文字";
                color = _theme.Length;
            }
            if (_length.text != text) _length.text = text;
            _length.color = color;
        }

        void RefreshText(KeyboardSession session)
        {
            string committed = session.Committed;
            int committedCursor = session.CommittedCursor;
            string composition = session.Composition;

            _builder.Clear();
            AppendEscaped(_builder, committed, 0, committedCursor);
            if (composition.Length > 0)
            {
                _builder.Append(_compositionOpenTag);
                AppendEscaped(_builder, composition, 0, composition.Length);
                _builder.Append("</u></color>");
            }
            AppendEscaped(_builder, committed, committedCursor, committed.Length - committedCursor);
            _text.text = _builder.ToString();
            // キャレットの位置は文字の配置から求めるため、すぐにメッシュを作らせる。
            _text.ForceMeshUpdate();

            // キャレットの直前までの文字数（サロゲートペアは1文字）。
            int caretUtf16 = committedCursor + (composition.Length > 0 ? session.CompositionCursor : 0);
            string plain = committed.Substring(0, committedCursor) + composition + committed.Substring(committedCursor);
            PlaceCaret(CountCharacters(plain, caretUtf16));
        }

        void PlaceCaret(int visibleIndex)
        {
            TMP_TextInfo info = _text.textInfo;
            float x;
            float top;
            float bottom;
            if (info.characterCount == 0)
            {
                // 空のときは、先頭行の位置を字体の寸法から求める。
                TMP_FontAsset font = _text.font;
                float scale = _text.fontSize / font.faceInfo.pointSize;
                x = 0f;
                top = 0f;
                bottom = -font.faceInfo.lineHeight * scale;
            }
            else
            {
                TMP_CharacterInfo character;
                if (visibleIndex < info.characterCount)
                {
                    character = info.characterInfo[visibleIndex];
                    x = character.origin;
                }
                else
                {
                    character = info.characterInfo[info.characterCount - 1];
                    x = character.xAdvance;
                }
                TMP_LineInfo line = info.lineInfo[character.lineNumber];
                top = line.ascender;
                bottom = line.descender;
            }

            UiRects.Place(_caret, x, -top, _caretWidth, top - bottom);

            // キャレットの行の下端が見える位置まで、ただし行の上端は隠さない範囲でずらす。
            float scroll = Mathf.Clamp(-bottom - _viewport.rect.height, 0f, -top);
            _textRect.anchoredPosition = new Vector2(0f, scroll);
        }

        static void AppendEscaped(StringBuilder builder, string text, int start, int count)
        {
            for (int i = start; i < start + count; i++)
            {
                // 入力した文字を TMP のタグとして解釈させない。
                if (text[i] == '<') builder.Append("<noparse><</noparse>");
                else builder.Append(text[i]);
            }
        }

        static int CountCharacters(string text, int utf16Length)
        {
            int count = 0;
            for (int i = 0; i < utf16Length; i++)
            {
                if (char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1])) continue;
                count++;
            }
            return count;
        }
    }
}
