using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKeyboard.Core;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 入力欄（確定済みの文字列・未確定のかな・キャレット）と、その下の状態行（通知・残り文字数）。
    /// 未確定のかなは色と下線で確定済みと区別する。行数が入力欄を超えるときは、キャレットの行が見える位置までずらす。
    /// </summary>
    public sealed class InputAreaView
    {
        const float Padding = 16f;
        const float CaretWidth = 3f;

        readonly TextMeshProUGUI _text;
        readonly RectTransform _textRect;
        readonly RectTransform _caret;
        readonly TextMeshProUGUI _notice;
        readonly TextMeshProUGUI _remaining;
        readonly float _viewportHeight;
        readonly string _compositionOpenTag;
        readonly StringBuilder _builder = new StringBuilder();

        public InputAreaView(Transform parent, KeyboardFonts fonts)
        {
            if (fonts == null) throw new ArgumentNullException(nameof(fonts));
            _compositionOpenTag = "<color=#" + ColorUtility.ToHtmlStringRGB(KeyboardTheme.Composition) + "><u>";

            float fieldWidth = KeyboardTheme.Width - KeyboardTheme.Margin * 2f;
            UiBuilder.Image(parent, "InputField", KeyboardTheme.Margin, KeyboardTheme.InputTop, fieldWidth, KeyboardTheme.InputHeight, KeyboardTheme.Field);

            float viewportWidth = fieldWidth - Padding * 2f;
            _viewportHeight = KeyboardTheme.InputHeight - Padding * 2f;
            RectTransform viewport = UiBuilder.Rect(parent, "InputViewport",
                KeyboardTheme.Margin + Padding, KeyboardTheme.InputTop + Padding, viewportWidth, _viewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();

            _textRect = UiBuilder.Rect(viewport, "InputText", 0f, 0f, viewportWidth, _viewportHeight);
            _text = UiBuilder.AddText(_textRect.gameObject, fonts.Regular, KeyboardTheme.InputFontSize, KeyboardTheme.Text, TextAlignmentOptions.TopLeft);
            _text.richText = true;
            _text.textWrappingMode = TextWrappingModes.Normal;

            _caret = UiBuilder.Image(_textRect, "Caret", 0f, 0f, CaretWidth, KeyboardTheme.InputFontSize, KeyboardTheme.Caret).rectTransform;

            float statusWidth = KeyboardTheme.Width - KeyboardTheme.Margin * 2f;
            const float remainingWidth = 250f;
            _notice = UiBuilder.Text(parent, "Notice", KeyboardTheme.Margin, KeyboardTheme.StatusTop,
                statusWidth - remainingWidth, KeyboardTheme.StatusHeight,
                fonts.Regular, KeyboardTheme.StatusFontSize, KeyboardTheme.Composition, TextAlignmentOptions.MidlineLeft);
            _notice.overflowMode = TextOverflowModes.Ellipsis;
            _remaining = UiBuilder.Text(parent, "Remaining", KeyboardTheme.Width - KeyboardTheme.Margin - remainingWidth, KeyboardTheme.StatusTop,
                remainingWidth, KeyboardTheme.StatusHeight,
                fonts.Regular, KeyboardTheme.StatusFontSize, KeyboardTheme.TextDim, TextAlignmentOptions.MidlineRight);
        }

        public void Refresh(KeyboardSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RefreshText(session);

            if (_notice.text != session.Notice) _notice.text = session.Notice;
            string remaining = "あと " + session.RemainingLength + " 文字";
            if (_remaining.text != remaining) _remaining.text = remaining;
            _remaining.color = session.RemainingLength == 0 ? KeyboardTheme.Warning : KeyboardTheme.TextDim;
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

            UiBuilder.Place(_caret, x, -top, CaretWidth, top - bottom);

            // キャレットの行の下端が見える位置まで、ただし行の上端は隠さない範囲でずらす。
            float scroll = Mathf.Clamp(-bottom - _viewportHeight, 0f, -top);
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
