using System;
using System.Collections.Generic;
using System.Globalization;
using VRKeyboard.Core.Input;
using VRKeyboard.Core.Text;

namespace VRKeyboard.Core.Grip
{
    public enum InputLanguage
    {
        Japanese,
        English,
    }

    public enum GripRequestKind
    {
        None,

        /// <summary>変換（未確定のかなを変換する）。セッションが処理する。</summary>
        Convert,

        /// <summary>確定（未確定の確定、または送信）。セッションが処理する。</summary>
        Commit,
    }

    /// <summary>グリップ入力がセッションに依頼する処理と、そのトリガーを引いた手。</summary>
    public readonly struct GripRequest
    {
        public GripRequest(GripRequestKind kind, Hand hand)
        {
            Kind = kind;
            Hand = hand;
        }

        public GripRequestKind Kind { get; }
        public Hand Hand { get; }
    }

    /// <summary>
    /// 入力中のグリップ入力（SPEC 5.2）。毎フレームの手の状態から、セル・方向の選択、
    /// 機能ページの切り替え、トリガーによる入力を処理し、入力バッファを更新する。
    /// 変換・確定はバッファだけでは完結しないため、要求として返す。
    /// </summary>
    public sealed class GripInputMethod
    {
        readonly InputBuffer _buffer;
        KeyboardSettings _settings;

        InputLanguage _language = InputLanguage.Japanese;
        bool _numberPage;
        bool _fullWidth;
        bool _upperCase;

        int _cell = StickClassifier.NeutralCell;
        StickDirection _direction = StickDirection.Center;
        int _vowelCell = StickClassifier.NeutralCell;
        bool _consonantFunction;
        bool _vowelFunction;

        int _viewBuiltVersion = -1;
        GripViewState _view;

        public GripInputMethod(KeyboardSettings settings, InputBuffer buffer)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public InputLanguage Language => _language;
        public bool IsNumberPage => _numberPage;

        /// <summary>全角で入力するか。英字ページと数字ページでのみ作用する。</summary>
        public bool IsFullWidth => _fullWidth;

        /// <summary>大文字で入力するか。英字ページでのみ作用する。</summary>
        public bool IsUpperCase => _upperCase;

        /// <summary>
        /// 現在のページ。数字ページを閉じると言語に応じたページに戻るため、開く前のページを別に保持する必要はない。
        /// </summary>
        public GripPage Page
        {
            get
            {
                if (_numberPage) return GripPage.Number;
                return _language == InputLanguage.Japanese ? GripPage.Kana : GripPage.Alpha;
            }
        }

        public int ConsonantCell => _cell;
        public StickDirection VowelDirection => _direction;

        /// <summary>母音パネルが機能ページのときの選択セル（0..8）。</summary>
        public int VowelFunctionCell => _vowelCell;

        /// <summary>表示に関わる状態が変わるたびに増える。</summary>
        public int Version { get; private set; }

        public GripViewState View
        {
            get
            {
                if (_view == null || _viewBuiltVersion != Version)
                {
                    _view = BuildView();
                    _viewBuiltVersion = Version;
                }
                return _view;
            }
        }

        /// <summary>設定を差し替える。子音の手は変えられない（呼び出し側の <see cref="KeyboardSession.ApplySettings"/> で検査する）。</summary>
        internal void ApplySettings(KeyboardSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 入力を受け付けていなかった後（開いたとき・変換から戻ったとき）に呼び、
        /// スティックの分類を現在の傾きに合わせる。振動は出さない。
        /// </summary>
        public void Reset(ControllerFrame frame)
        {
            int cell = StickClassifier.ClassifyCell(frame.Get(_settings.ConsonantHand).Stick,
                StickClassifier.NeutralCell, _settings.StickEnterThreshold, _settings.StickExitThreshold);
            StickValue vowelStick = frame.Get(_settings.VowelHand).Stick;
            StickDirection direction = StickClassifier.ClassifyDirection(vowelStick,
                StickDirection.Center, _settings.StickEnterThreshold, _settings.StickExitThreshold);
            int vowelCell = StickClassifier.ClassifyCell(vowelStick,
                StickClassifier.NeutralCell, _settings.StickEnterThreshold, _settings.StickExitThreshold);
            bool consonantFunction = frame.Get(_settings.ConsonantHand).Grip;
            bool vowelFunction = frame.Get(_settings.VowelHand).Grip;

            if (cell != _cell || direction != _direction || vowelCell != _vowelCell
                || consonantFunction != _consonantFunction || vowelFunction != _vowelFunction)
            {
                Version++;
            }
            _cell = cell;
            _direction = direction;
            _vowelCell = vowelCell;
            _consonantFunction = consonantFunction;
            _vowelFunction = vowelFunction;
        }

        /// <summary>
        /// 1フレーム分を処理する。振動は haptics に追加する。
        /// edges は、両手のグリップを押している間のトリガーの抑止などを済ませたもの。
        /// </summary>
        public GripRequest Update(ControllerFrame frame, FrameEdges edges, List<HapticEvent> haptics)
        {
            if (haptics == null) throw new ArgumentNullException(nameof(haptics));

            Hand consonantHand = _settings.ConsonantHand;
            Hand vowelHand = _settings.VowelHand;
            HandState consonant = frame.Get(consonantHand);
            HandState vowel = frame.Get(vowelHand);
            float enter = _settings.StickEnterThreshold;
            float exit = _settings.StickExitThreshold;

            int cell = StickClassifier.ClassifyCell(consonant.Stick, _cell, enter, exit);
            StickDirection direction = StickClassifier.ClassifyDirection(vowel.Stick, _direction, enter, exit);
            int vowelCell = StickClassifier.ClassifyCell(vowel.Stick, _vowelCell, enter, exit);
            bool consonantFunction = consonant.Grip;
            bool vowelFunction = vowel.Grip;

            bool changed = consonantFunction != _consonantFunction || vowelFunction != _vowelFunction;
            if (cell != _cell)
            {
                haptics.Add(new HapticEvent(consonantHand, HapticKind.Move));
                changed = true;
            }
            // 機能ページのときは3×3のセル、それ以外は5方向の変化が母音パネルの選択の変化になる。
            bool vowelMoved = vowelFunction ? vowelCell != _vowelCell : direction != _direction;
            if (vowelMoved)
            {
                haptics.Add(new HapticEvent(vowelHand, HapticKind.Move));
                changed = true;
            }

            _cell = cell;
            _direction = direction;
            _vowelCell = vowelCell;
            _consonantFunction = consonantFunction;
            _vowelFunction = vowelFunction;
            if (changed) Version++;

            // 両手のグリップを押している間は、開閉の操作と区別するためトリガーを受け付けない。
            if (!edges.AnyTrigger || (consonant.Grip && vowel.Grip)) return default;

            // 機能ページでは、左右どちらのトリガーでもセルを実行する。
            if (consonantFunction || vowelFunction)
            {
                Hand pressedHand = edges.LeftTrigger ? Hand.Left : Hand.Right;
                return Press(pressedHand, haptics);
            }

            // 通常のページでは、母音側のトリガーで入力し、子音側のトリガーで直前の文字を切り替える。
            // 同じフレームで両方を引いたときは、入力してから切り替える。
            GripRequest request = default;
            if (edges.TriggerPressed(vowelHand)) request = Press(vowelHand, haptics);
            if (edges.TriggerPressed(consonantHand)) Report(_buffer.ApplyCharacterModifier(), consonantHand, haptics);
            return request;
        }

        GripRequest Press(Hand pressedHand, List<HapticEvent> haptics)
        {
            GripCell cell;
            if (_vowelFunction)
            {
                cell = GripLayout.GetVowelFunctionCell(_vowelCell);
            }
            else
            {
                cell = GripLayout.GetConsonantCell(Page, _consonantFunction, _cell);
            }

            switch (cell.Kind)
            {
                case GripCellKind.Empty:
                    return default;

                case GripCellKind.Characters:
                {
                    string character = cell.CharacterAt(_direction);
                    if (character == null) return default; // 未割り当て
                    Report(InsertCharacter(character), pressedHand, haptics);
                    return default;
                }

                case GripCellKind.Action:
                    return Execute(cell.Action, pressedHand, haptics);

                default:
                    throw new InvalidOperationException("未知のセルの種類: " + cell.Kind);
            }
        }

        GripRequest Execute(GripAction action, Hand pressedHand, List<HapticEvent> haptics)
        {
            switch (action)
            {
                case GripAction.Erase:
                    Report(_buffer.Erase(), pressedHand, haptics);
                    break;
                case GripAction.Space:
                    Report(InsertSpace(), pressedHand, haptics);
                    break;
                case GripAction.CharacterModifier:
                    Report(_buffer.ApplyCharacterModifier(), pressedHand, haptics);
                    break;
                case GripAction.CursorLeft:
                    Report(_buffer.MoveCursorLeft(), pressedHand, haptics);
                    break;
                case GripAction.CursorRight:
                    Report(_buffer.MoveCursorRight(), pressedHand, haptics);
                    break;
                case GripAction.ToggleWidth:
                    if (Page == GripPage.Kana) Invalid(pressedHand, haptics);
                    else
                    {
                        _fullWidth = !_fullWidth;
                        Done(pressedHand, haptics);
                    }
                    break;
                case GripAction.ToggleCase:
                    if (Page != GripPage.Alpha) Invalid(pressedHand, haptics);
                    else
                    {
                        _upperCase = !_upperCase;
                        Done(pressedHand, haptics);
                    }
                    break;
                case GripAction.NumberPage:
                    _numberPage = !_numberPage;
                    Done(pressedHand, haptics);
                    break;
                case GripAction.Language:
                    // 数字ページは閉じてから切り替える（1回の操作で両方）。
                    _numberPage = false;
                    _language = _language == InputLanguage.Japanese ? InputLanguage.English : InputLanguage.Japanese;
                    Done(pressedHand, haptics);
                    break;
                case GripAction.Convert:
                    return new GripRequest(GripRequestKind.Convert, pressedHand);
                case GripAction.Commit:
                    return new GripRequest(GripRequestKind.Commit, pressedHand);
                default:
                    throw new InvalidOperationException("未知の動作: " + action);
            }
            return default;
        }

        EditResult InsertCharacter(string character)
        {
            // かなページの文字は未確定のかなへ。英字・数字ページの文字は確定済みへ直接入る。
            return Page == GripPage.Kana
                ? _buffer.InsertComposition(character)
                : _buffer.InsertCommitted(ApplyCharacterOptions(character));
        }

        EditResult InsertSpace()
        {
            // かなページでは全角スペース（未確定があれば InsertCommitted が先にそのまま確定する）。
            bool fullWidth = Page == GripPage.Kana || _fullWidth;
            return _buffer.InsertCommitted(fullWidth ? "　" : " ");
        }

        string ApplyCharacterOptions(string character)
        {
            if (Page == GripPage.Alpha && _upperCase) character = character.ToUpperInvariant();
            if (Page != GripPage.Kana && _fullWidth) character = CharacterWidth.ToFullWidth(character);
            return character;
        }

        void Report(EditResult result, Hand hand, List<HapticEvent> haptics)
        {
            if (result == EditResult.Done) Done(hand, haptics);
            else Invalid(hand, haptics);
        }

        void Done(Hand hand, List<HapticEvent> haptics)
        {
            Version++;
            haptics.Add(new HapticEvent(hand, HapticKind.Input));
        }

        static void Invalid(Hand hand, List<HapticEvent> haptics)
        {
            haptics.Add(new HapticEvent(hand, HapticKind.Invalid));
        }

        GripViewState BuildView()
        {
            GripPage page = Page;
            var consonantCells = new GripCellView[GripLayout.CellCount];
            for (int i = 0; i < consonantCells.Length; i++)
            {
                consonantCells[i] = ToView(GripLayout.GetConsonantCell(page, _consonantFunction, i));
            }

            GripCellView[] vowelCells;
            int vowelSelected;
            if (_vowelFunction)
            {
                vowelCells = new GripCellView[GripLayout.CellCount];
                for (int i = 0; i < vowelCells.Length; i++)
                {
                    GripCell cell = GripLayout.GetVowelFunctionCell(i);
                    vowelCells[i] = new GripCellView(cell.Label, IsDisabled(cell), IsLit(cell));
                }
                vowelSelected = _vowelCell;
            }
            else
            {
                vowelCells = new GripCellView[GripCell.DirectionCount];
                GripCell selected = GripLayout.GetConsonantCell(page, _consonantFunction, _cell);
                for (int i = 0; i < vowelCells.Length; i++)
                {
                    string character = selected.Kind == GripCellKind.Characters ? selected.CharacterAt((StickDirection)i) : null;
                    vowelCells[i] = character == null
                        ? new GripCellView(string.Empty, true, false)
                        : new GripCellView(ApplyDisplayOptions(character), false, false);
                }
                vowelSelected = (int)_direction;
            }

            var consonantHints = new string[GripLayout.CellCount];
            var vowelHints = new string[GripLayout.CellCount];
            for (int i = 0; i < GripLayout.CellCount; i++)
            {
                consonantHints[i] = _consonantFunction ? string.Empty : ToHint(GripLayout.GetConsonantCell(page, true, i));
                vowelHints[i] = _vowelFunction ? string.Empty : ToHint(GripLayout.GetVowelFunctionCell(i));
            }

            return new GripViewState(page, _consonantFunction, _vowelFunction,
                consonantCells, _cell, vowelCells, vowelSelected, consonantHints, vowelHints);
        }

        /// <summary>通常のページに重ねて示す、機能ページのセルの表示名の頭文字（1文字）。空きセルには示さない。</summary>
        string ToHint(GripCell functionCell)
        {
            return functionCell.Kind == GripCellKind.Empty ? string.Empty : StringInfo.GetNextTextElement(ToView(functionCell).Label);
        }

        GripCellView ToView(GripCell cell)
        {
            switch (cell.Kind)
            {
                case GripCellKind.Characters:
                    // 表示名が決まっていないセルは、入力される文字（中央）をそのまま表示する。
                    return new GripCellView(cell.Label ?? ApplyDisplayOptions(cell.CharacterAt(StickDirection.Center)), false, false);
                default:
                    return new GripCellView(cell.Label, false, false);
            }
        }

        string ApplyDisplayOptions(string character)
        {
            return Page == GripPage.Kana ? character : ApplyCharacterOptions(character);
        }

        bool IsDisabled(GripCell cell)
        {
            if (cell.Kind != GripCellKind.Action) return false;
            switch (cell.Action)
            {
                case GripAction.ToggleWidth: return Page == GripPage.Kana;
                case GripAction.ToggleCase: return Page != GripPage.Alpha;
                default: return false;
            }
        }

        bool IsLit(GripCell cell)
        {
            if (cell.Kind != GripCellKind.Action) return false;
            switch (cell.Action)
            {
                case GripAction.ToggleWidth: return _fullWidth && Page != GripPage.Kana;
                case GripAction.ToggleCase: return _upperCase && Page == GripPage.Alpha;
                case GripAction.NumberPage: return _numberPage;
                default: return false;
            }
        }
    }
}
