using System;
using System.Text;
using TMPro;
using UnityEngine;
using VRime.App.Output;
using VRime.Core;

namespace VRime.App.View
{
    /// <summary>
    /// 入力欄（確定済みの文字列・未確定のかな・キャレット）と、その下の状態行（通知・送信先・文字数）。
    /// 状態行は左から、通知のバッジ（通知があるときだけ）・「送信先」のラベルと送信先の名前を詰めて並べ、文字数を右端に置く。
    /// 通知と送信先の名前は、収まらなければ切り詰める。
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

        [Tooltip("状態行。通知のバッジ・送信先・文字数はこの直下に置き、位置と幅はこのビューが決める（高さはシーンの値を使う）。")]
        [SerializeField] RectTransform _statusRow;

        [Tooltip("通知のバッジ（背景）。通知が空の間は隠す。")]
        [SerializeField] RectTransform _noticeBadge;

        [Tooltip("通知の文字。_noticeBadge の子で、左右に余白を空けてバッジいっぱいに広げる（その余白をバッジの幅に足す）。")]
        [SerializeField] TextMeshProUGUI _notice;

        [Tooltip("通知のバッジの最大の幅。超える通知は切り詰める。")]
        [SerializeField] float _noticeMaxWidth = 520f;

        [Tooltip("「送信先」のラベル（文字はシーンで決める）。")]
        [SerializeField] TextMeshProUGUI _targetLabel;

        [Tooltip("送信先の名前。")]
        [SerializeField] TextMeshProUGUI _target;

        [Tooltip("文字数（上限があれば上限も）。")]
        [SerializeField] TextMeshProUGUI _length;

        [Tooltip("通知のバッジと送信先の間、送信先と文字数の間。")]
        [SerializeField] float _statusGap = 14f;

        [Tooltip("「送信先」のラベルと名前の間。")]
        [SerializeField] float _targetLabelGap = 10f;

        ActiveTheme _theme;
        RectTransform _textRect;
        float _caretWidth;
        float _noticePadding;
        string _compositionOpenTag;
        KeyboardTheme _compositionTagTheme;
        readonly StringBuilder _builder = new StringBuilder();

        public void Validate()
        {
            SceneChecks.Require(_viewport, this, nameof(_viewport));
            SceneChecks.Require(_text, this, nameof(_text));
            SceneChecks.Require(_caret, this, nameof(_caret));
            SceneChecks.Require(_statusRow, this, nameof(_statusRow));
            SceneChecks.Require(_noticeBadge, this, nameof(_noticeBadge));
            SceneChecks.Require(_notice, this, nameof(_notice));
            SceneChecks.Require(_targetLabel, this, nameof(_targetLabel));
            SceneChecks.Require(_target, this, nameof(_target));
            SceneChecks.Require(_length, this, nameof(_length));
            RequireParent(_noticeBadge, _statusRow);
            RequireParent(_notice.rectTransform, _noticeBadge);
            RequireParent(_targetLabel.rectTransform, _statusRow);
            RequireParent(_target.rectTransform, _statusRow);
            RequireParent(_length.rectTransform, _statusRow);
            if (!(_noticeMaxWidth > 0f))
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_noticeMaxWidth) + " が正ではありません: " + _noticeMaxWidth);
            if (_statusGap < 0f)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_statusGap) + " が負です: " + _statusGap);
            if (_targetLabelGap < 0f)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_targetLabelGap) + " が負です: " + _targetLabelGap);
            if (_text.transform.parent != _viewport)
                throw new InvalidOperationException(SceneChecks.PathOf(_text.transform) + " は " + SceneChecks.PathOf(_viewport) + " の直下に置く必要があります。");
            if (_caret.parent != _text.transform)
                throw new InvalidOperationException(SceneChecks.PathOf(_caret) + " は " + SceneChecks.PathOf(_text.transform) + " の直下に置く必要があります。");
            if (!_text.richText)
                throw new InvalidOperationException(SceneChecks.PathOf(_text.transform) + " のリッチテキストが無効です。");
        }

        public void Initialize(ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _textRect = _text.rectTransform;
            _caretWidth = _caret.sizeDelta.x;
            RectTransform noticeRect = _notice.rectTransform;
            if (noticeRect.anchorMin != Vector2.zero || noticeRect.anchorMax != Vector2.one)
                throw new InvalidOperationException(SceneChecks.PathOf(noticeRect) + " は " + SceneChecks.PathOf(_noticeBadge) + " いっぱいに広げる（アンカーを 0〜1 にする）必要があります。");
            _noticePadding = noticeRect.offsetMin.x - noticeRect.offsetMax.x;
            if (!(_noticeMaxWidth > _noticePadding))
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + nameof(_noticeMaxWidth) + " が通知の文字の左右の余白より狭いです。");
            _compositionTagTheme = null;
        }

        public void Refresh(KeyboardSession session, OutputTarget target)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (target == null) throw new ArgumentNullException(nameof(target));
            RefreshText(session);
            RefreshLength(session);
            LayoutStatusRow(session.Notice, target.Name);
        }

        void LayoutStatusRow(string notice, string targetName)
        {
            float rowWidth = _statusRow.rect.width;
            float rowHeight = _statusRow.rect.height;
            float x = 0f;

            bool hasNotice = notice.Length != 0;
            if (_noticeBadge.gameObject.activeSelf != hasNotice) _noticeBadge.gameObject.SetActive(hasNotice);
            if (hasNotice)
            {
                float badgeWidth = TextFitting.Fit(_notice, notice, _noticeMaxWidth - _noticePadding) + _noticePadding;
                float badgeHeight = _noticeBadge.rect.height;
                UiRects.Place(_noticeBadge, x, (rowHeight - badgeHeight) / 2f, badgeWidth, badgeHeight);
                x += badgeWidth + _statusGap;
            }

            float lengthWidth = _length.preferredWidth;
            UiRects.Place(_length.rectTransform, rowWidth - lengthWidth, 0f, lengthWidth, rowHeight);

            float labelWidth = _targetLabel.preferredWidth;
            UiRects.Place(_targetLabel.rectTransform, x, 0f, labelWidth, rowHeight);
            x += labelWidth + _targetLabelGap;

            // 通知が長いと名前の場所が残らないことがある。そのときは名前を出さない。
            float available = rowWidth - lengthWidth - _statusGap - x;
            float targetWidth = available > 0f ? TextFitting.Fit(_target, targetName, available) : 0f;
            if (!(available > 0f)) _target.text = string.Empty;
            UiRects.Place(_target.rectTransform, x, 0f, targetWidth, rowHeight);
        }

        static void RequireParent(RectTransform child, RectTransform parent)
        {
            if (child.parent != parent)
                throw new InvalidOperationException(SceneChecks.PathOf(child) + " は " + SceneChecks.PathOf(parent) + " の直下に置く必要があります。");
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
                color = length > limit.Value ? _theme.Current.LengthOverLimit
                    : length == limit.Value ? _theme.Current.LengthAtLimit
                    : _theme.Current.Length;
            }
            else
            {
                text = length + " 文字";
                color = _theme.Current.Length;
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
                _builder.Append(CompositionOpenTag());
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
                // 空のときは、先頭行の位置をフォントのメトリクスから求める。
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

        // 未確定のかなの色のタグ。テーマが変わったときだけ再作成する。
        string CompositionOpenTag()
        {
            KeyboardTheme theme = _theme.Current;
            if (theme != _compositionTagTheme)
            {
                _compositionOpenTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(theme.Composition) + "><u>";
                _compositionTagTheme = theme;
            }
            return _compositionOpenTag;
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
