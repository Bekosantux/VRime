using System;
using Valve.VR;
using VRKeyboard.App.Input;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>振動の出力先。<c>TriggerHapticVibrationAction</c> で、種類ごとの長さ・周波数・強さのパターンを鳴らす。</summary>
    public sealed class OpenVRHapticOutput : IHapticOutput
    {
        /// <summary>振動の 1パターン。長さは秒、周波数は Hz、強さは 0〜1。</summary>
        public readonly struct Pattern
        {
            public Pattern(float seconds, float frequency, float amplitude)
            {
                Seconds = seconds;
                Frequency = frequency;
                Amplitude = amplitude;
            }

            public float Seconds { get; }
            public float Frequency { get; }
            public float Amplitude { get; }
        }

        // Move と OpenClose は試作2の値（実機で確認済み）。Input と Invalid は試作になかったため、
        // 他の種類と区別できるように決めた暫定値（SPEC 5.1）。Input は Move より少し長く強い。
        // Invalid は低い周波数で長くし、操作が受け付けられなかったことが触覚で分かるようにしている。
        static readonly Pattern MovePattern = new Pattern(0.01f, 160f, 0.6f);
        static readonly Pattern InputPattern = new Pattern(0.03f, 160f, 0.8f);
        static readonly Pattern OpenClosePattern = new Pattern(0.08f, 160f, 0.6f);
        static readonly Pattern InvalidPattern = new Pattern(0.12f, 60f, 0.8f);

        readonly OpenVRActions _actions;

        public OpenVRHapticOutput(OpenVRActions actions)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public static Pattern GetPattern(HapticKind kind)
        {
            switch (kind)
            {
                case HapticKind.Move: return MovePattern;
                case HapticKind.Input: return InputPattern;
                case HapticKind.OpenClose: return OpenClosePattern;
                case HapticKind.Invalid: return InvalidPattern;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public void Play(Hand hand, HapticKind kind)
        {
            Pattern pattern = GetPattern(kind);
            EVRInputError error = OpenVR.Input.TriggerHapticVibrationAction(
                HapticAction(hand), 0f, pattern.Seconds, pattern.Frequency, pattern.Amplitude, OpenVR.k_ulInvalidInputValueHandle);
            OpenVRErrors.CheckAllowingNoData(error, "TriggerHapticVibrationAction(" + hand + ")");
        }

        ulong HapticAction(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return _actions.LeftHaptic;
                case Hand.Right: return _actions.RightHaptic;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }
    }
}
