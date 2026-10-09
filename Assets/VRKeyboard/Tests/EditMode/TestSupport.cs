using System;
using System.Collections.Generic;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Tests
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
        public bool OnSendRequested(string text) { Sends.Add(text); return SendSucceeds; }

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

    /// <summary>KeyboardSession にコントローラーの操作を与えるテスト用の操作盤。1回の Tick は 16ms 進む。</summary>
    public sealed class Rig
    {
        public readonly FakeEffects Effects = new FakeEffects();
        public readonly KeyboardSession Session;
        public readonly KeyboardSettings Settings;
        public double Time;

        StickValue _leftStick, _rightStick;
        bool _leftTrigger, _rightTrigger, _leftGrip, _rightGrip;

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
                new HandState(_leftStick, _leftTrigger, _leftGrip),
                new HandState(_rightStick, _rightTrigger, _rightGrip)), Time);
        }

        public void TickMany(int count)
        {
            for (int i = 0; i < count; i++) Tick();
        }

        public void Grips(bool left, bool right)
        {
            _leftGrip = left;
            _rightGrip = right;
        }

        public void SetGrip(Hand hand, bool held)
        {
            if (hand == Hand.Left) _leftGrip = held; else _rightGrip = held;
        }

        public void SetTrigger(Hand hand, bool held)
        {
            if (hand == Hand.Left) _leftTrigger = held; else _rightTrigger = held;
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

        /// <summary>通常ページ: 子音のセルと母音の方向を選んで、子音側のトリガーで入力する。</summary>
        public void Type(int cell, StickDirection direction)
        {
            SetStick(Consonant, Sticks.Cell(cell));
            SetStick(Vowel, Sticks.Dir(direction));
            Tick();
            Pull(Consonant);
            SetStick(Consonant, StickValue.Zero);
            SetStick(Vowel, StickValue.Zero);
            Tick();
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
