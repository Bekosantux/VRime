using System;

namespace VRKeyboard.Core.Input
{
    /// <summary>そのフレームでトリガー・グリップが押された瞬間（前フレームは離れていた）。</summary>
    public readonly struct FrameEdges
    {
        public FrameEdges(bool leftTrigger, bool rightTrigger, bool leftGrip, bool rightGrip)
        {
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftGrip = leftGrip;
            RightGrip = rightGrip;
        }

        public bool LeftTrigger { get; }
        public bool RightTrigger { get; }
        public bool LeftGrip { get; }
        public bool RightGrip { get; }

        public bool AnyTrigger => LeftTrigger || RightTrigger;
        public bool AnyGrip => LeftGrip || RightGrip;

        public bool TriggerPressed(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return LeftTrigger;
                case Hand.Right: return RightTrigger;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }

        public bool GripPressed(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return LeftGrip;
                case Hand.Right: return RightGrip;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }

        public FrameEdges WithoutTriggers()
        {
            return new FrameEdges(false, false, LeftGrip, RightGrip);
        }
    }
}
