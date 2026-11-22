using System;
using System.Collections.Generic;
using VRime.Core.Grip;
using VRime.Core.Input;

namespace VRime.Core.Tests
{
    /// <summary>通知を記録するだけの IKeyboardEffects。</summary>
    public sealed class FakeEffects : IKeyboardEffects
    {
        public readonly List<bool> OpenChanges = new List<bool>();
        public readonly List<HapticEvent> Haptics = new List<HapticEvent>();
        public readonly List<KeyValuePair<int, string>> ConversionRequests = new List<KeyValuePair<int, string>>();
        public readonly List<string> Sends = new List<string>();

        public void OnOpenChanged(bool isOpen) { OpenChanges.Add(isOpen); }
        public void OnHaptic(Hand hand, HapticKind kind) { Haptics.Add(new HapticEvent(hand, kind)); }
        public void OnConversionRequested(int requestId, string reading) { ConversionRequests.Add(new KeyValuePair<int, string>(requestId, reading)); }
        public bool SendSucceeds = true;
        public Action<string> SendHook;
        public bool OnSendRequested(string text) { Sends.Add(text); SendHook?.Invoke(text); return SendSucceeds; }

        public int CountHaptics(HapticKind kind)
        {
            int count = 0;
            foreach (HapticEvent haptic in Haptics) if (haptic.Kind == kind) count++;
            return count;
        }

        public int CountHaptics(Hand hand, HapticKind kind)
        {
            int count = 0;
            foreach (HapticEvent haptic in Haptics) if (haptic.Hand == hand && haptic.Kind == kind) count++;
            return count;
        }

        public void ClearAll()
        {
            OpenChanges.Clear();
            Haptics.Clear();
            ConversionRequests.Clear();
            Sends.Clear();
        }
    }

    /// <summary>子音側の機能ページのセル番号（docs/spec/grip-input.md「子音側の機能ページ」）。かなページのセルの名前で呼ぶ。</summary>
    public static class ConsonantFunctionCells
    {
        public const int Erase = 2;
        public const int Modifier = 3;
        public const int Wa = 4;
        public const int Symbol = 5;
        public const int Space = 8;
    }

    /// <summary>母音側の機能ページのセル番号（docs/spec/grip-input.md「母音側の機能ページ」）。</summary>
    public static class VowelFunctionCells
    {
        public const int ToggleWidth = 0;
        public const int Language = 1;
        public const int NumberPage = 2;
        public const int CursorLeft = 3;
        public const int Commit = 4;
        public const int CursorRight = 5;
        public const int ToggleCase = 6;
        public const int Convert = 7;
        public const int PasteMode = 8;
    }

    public static class Sticks
    {
        /// <summary>セル番号（0..8）の方向に最大まで倒した値。</summary>
        public static StickValue Cell(int cell)
        {
            float x = cell % 3 - 1;
            float y = 1 - cell / 3;
            float length = (float)Math.Sqrt(x * x + y * y);
            return length == 0f ? StickValue.Zero : new StickValue(x / length, y / length);
        }

        public static StickValue Dir(StickDirection direction)
        {
            switch (direction)
            {
                case StickDirection.Left: return new StickValue(-1f, 0f);
                case StickDirection.Up: return new StickValue(0f, 1f);
                case StickDirection.Right: return new StickValue(1f, 0f);
                case StickDirection.Down: return new StickValue(0f, -1f);
                default: return StickValue.Zero;
            }
        }
    }

    /// <summary>
    /// KeyboardSession にコントローラーの操作を与えるテスト用の操作盤。1回の Tick は 16ms 進む。
    /// 「グリップ」は既定のバインドと同じく、展開・収納とパネルの切り替えの両方を押す。
    /// 別のボタンに割り当てた場合は <see cref="SetOpenClose"/> と <see cref="SetPanel"/> で個別に押す。
    /// </summary>
    public sealed class Rig
    {
        public readonly FakeEffects Effects = new FakeEffects();
        public readonly KeyboardSession Session;
        public readonly KeyboardSettings Settings;
        public double Time;

        StickValue _leftStick, _rightStick;
        float _leftTrigger, _rightTrigger;
        bool _leftOpenClose, _rightOpenClose;
        bool _leftPanel, _rightPanel;

        public Rig(KeyboardSettings settings = null, bool open = true)
        {
            Settings = settings ?? new KeyboardSettings();
            Session = new KeyboardSession(Settings, Effects);
            if (open) OpenByChord();
        }

        public Hand Consonant => Settings.ConsonantHand;
        public Hand Vowel => Settings.VowelHand;

        public void Tick(double dt = 0.016)
        {
            Time += dt;
            Session.Tick(new ControllerFrame(
                new HandState(_leftStick, _leftTrigger, _leftOpenClose, _leftPanel),
                new HandState(_rightStick, _rightTrigger, _rightOpenClose, _rightPanel)), Time);
        }

        public void TickMany(int count)
        {
            for (int i = 0; i < count; i++) Tick();
        }

        public void Grips(bool left, bool right)
        {
            SetGrip(Hand.Left, left);
            SetGrip(Hand.Right, right);
        }

        public void SetGrip(Hand hand, bool held)
        {
            SetOpenClose(hand, held);
            SetPanel(hand, held);
        }

        /// <summary>展開・収納のボタンだけを押す（パネルの切り替えを別のボタンに割り当てた場合）。</summary>
        public void SetOpenClose(Hand hand, bool held)
        {
            if (hand == Hand.Left) _leftOpenClose = held; else _rightOpenClose = held;
        }

        /// <summary>パネルの切り替えのボタンだけを押す（展開・収納を別のボタンに割り当てた場合）。</summary>
        public void SetPanel(Hand hand, bool held)
        {
            if (hand == Hand.Left) _leftPanel = held; else _rightPanel = held;
        }

        /// <summary>トリガーを引き切る（1）か、離す（0）。</summary>
        public void SetTrigger(Hand hand, bool held)
        {
            SetTriggerPull(hand, held ? 1f : 0f);
        }

        public void SetTriggerPull(Hand hand, float pull)
        {
            if (hand == Hand.Left) _leftTrigger = pull; else _rightTrigger = pull;
        }

        public void SetStick(Hand hand, StickValue value)
        {
            if (hand == Hand.Left) _leftStick = value; else _rightStick = value;
        }

        /// <summary>両手のグリップを2回続けて同時押しし、離す。</summary>
        public void ChordTwice()
        {
            Grips(true, true); Tick();
            Grips(false, false); Tick();
            Grips(true, true); Tick();
            Grips(false, false); Tick();
        }

        public void OpenByChord()
        {
            ChordTwice();
            if (!Session.IsOpen) throw new InvalidOperationException("開けませんでした。");
            Effects.ClearAll();
        }

        /// <summary>トリガーを引いて離す。</summary>
        public void Pull(Hand hand)
        {
            SetTrigger(hand, true); Tick();
            SetTrigger(hand, false); Tick();
        }

        /// <summary>通常ページ: 子音のセルと母音の方向を選んで、母音側のトリガーで入力する。</summary>
        public void Type(int cell, StickDirection direction)
        {
            SetStick(Consonant, Sticks.Cell(cell));
            SetStick(Vowel, Sticks.Dir(direction));
            Tick();
            Pull(Vowel);
            SetStick(Consonant, StickValue.Zero);
            SetStick(Vowel, StickValue.Zero);
            Tick();
        }

        /// <summary>通常ページ: 子音側のトリガーで直前の文字を切り替える。</summary>
        public void Modify()
        {
            Pull(Consonant);
        }

        /// <summary>子音側の機能ページ: 子音側のグリップを握ったまま、セルと母音の方向を選んでトリガーで実行する。</summary>
        public void ConsonantFunction(int cell, StickDirection direction = StickDirection.Center)
        {
            SetGrip(Consonant, true);
            SetStick(Consonant, Sticks.Cell(cell));
            SetStick(Vowel, Sticks.Dir(direction));
            Tick();
            Pull(Vowel);
            SetGrip(Consonant, false);
            SetStick(Consonant, StickValue.Zero);
            SetStick(Vowel, StickValue.Zero);
            Tick();
        }

        /// <summary>母音側の機能ページ: 母音側のグリップを握ったまま、セルを選んでトリガーで実行する。</summary>
        public void VowelFunction(int cell)
        {
            SetGrip(Vowel, true);
            SetStick(Vowel, Sticks.Cell(cell));
            Tick();
            Pull(Consonant);
            SetGrip(Vowel, false);
            SetStick(Vowel, StickValue.Zero);
            Tick();
        }
    }
}
