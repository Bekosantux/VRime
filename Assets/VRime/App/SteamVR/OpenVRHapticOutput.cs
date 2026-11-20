using System;
using Valve.VR;
using VRime.App.Input;
using VRime.Core;
using VRime.Core.Input;

namespace VRime.App.SteamVR
{
    /// <summary>振動の出力先。<c>TriggerHapticVibrationAction</c> で、種類ごとの長さ・周波数・強さのパターンで振動させる。</summary>
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

        // Move と OpenClose は実機で確かめた値。Input と Invalid は他の種類と区別できるように決めた値で、
        // 実機での調整を経ていない（docs/spec/grip-input.md「振動」、docs/ROADMAP.md「見直し候補」）。Input は Move より少し長く強い。
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
