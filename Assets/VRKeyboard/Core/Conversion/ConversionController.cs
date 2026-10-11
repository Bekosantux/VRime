using System;
using System.Collections.Generic;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Conversion
{
    public enum ConversionOutcome
    {
        None,

        /// <summary>選択中の候補で確定する。</summary>
        Commit,

        /// <summary>変換を取り消して未確定のかなに戻す。</summary>
        Cancel,
    }

    /// <summary>
    /// 変換中の操作（SPEC 6.3）。左右どちらかのスティックの左右で文節、上下で候補を動かし
    /// （中央から倒した瞬間に1つだけ）、トリガーで確定、グリップで取り消す。
    /// 応答待ちの間は候補がないため、トリガーを無視し、グリップでの取り消しだけを受け付ける。
    /// </summary>
    public sealed class ConversionController
    {
        readonly StickDirectionTracker _leftStick = new StickDirectionTracker();
        readonly StickDirectionTracker _rightStick = new StickDirectionTracker();
        KeyboardSettings _settings;

        public ConversionController(KeyboardSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void ApplySettings(KeyboardSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 変換の操作の開始時に呼ぶ。その時点で既に倒されているスティックは、倒した瞬間として扱わない。
        /// </summary>
        public void Begin(ControllerFrame frame)
        {
            Track(frame);
        }

        /// <param name="session">候補があるとき。応答待ちの間は null。</param>
        public ConversionOutcome Update(
            ControllerFrame frame, FrameEdges edges, ConversionSession session, List<HapticEvent> haptics)
        {
            if (haptics == null) throw new ArgumentNullException(nameof(haptics));

            StickDirection left = _leftStick.Update(frame.Left.Stick, _settings.StickEnterThreshold, _settings.StickExitThreshold);
            StickDirection right = _rightStick.Update(frame.Right.Stick, _settings.StickEnterThreshold, _settings.StickExitThreshold);

            if (edges.AnyGrip) return ConversionOutcome.Cancel;
            if (session == null) return ConversionOutcome.None;
            if (edges.AnyTrigger) return ConversionOutcome.Commit;

            Move(session, left, Hand.Left, haptics);
            Move(session, right, Hand.Right, haptics);
            return ConversionOutcome.None;
        }

        void Track(ControllerFrame frame)
        {
            _leftStick.Update(frame.Left.Stick, _settings.StickEnterThreshold, _settings.StickExitThreshold);
            _rightStick.Update(frame.Right.Stick, _settings.StickEnterThreshold, _settings.StickExitThreshold);
        }

        static void Move(ConversionSession session, StickDirection pushed, Hand hand, List<HapticEvent> haptics)
        {
            bool moved;
            switch (pushed)
            {
                case StickDirection.Left: moved = session.MoveSegment(-1); break;
                case StickDirection.Right: moved = session.MoveSegment(1); break;
                case StickDirection.Up: moved = session.MoveCandidate(-1); break;
                case StickDirection.Down: moved = session.MoveCandidate(1); break;
                default: return;
            }
            if (moved) haptics.Add(new HapticEvent(hand, HapticKind.Move));
        }
    }
}
