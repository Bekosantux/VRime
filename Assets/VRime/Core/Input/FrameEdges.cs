using System;

namespace VRime.Core.Input
{
    /// <summary>そのフレームでトリガー・パネルの切り替えのボタンが押された瞬間（前フレームは離れていた）。</summary>
    public readonly struct FrameEdges
    {
        public FrameEdges(bool leftTrigger, bool rightTrigger, bool leftPanel, bool rightPanel)
        {
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftPanel = leftPanel;
            RightPanel = rightPanel;
        }

        public bool LeftTrigger { get; }
        public bool RightTrigger { get; }
        public bool LeftPanel { get; }
        public bool RightPanel { get; }

        public bool AnyTrigger => LeftTrigger || RightTrigger;
        public bool AnyPanel => LeftPanel || RightPanel;

        public bool TriggerPressed(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return LeftTrigger;
                case Hand.Right: return RightTrigger;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }

        public FrameEdges WithoutTriggers()
        {
            return new FrameEdges(false, false, LeftPanel, RightPanel);
        }
    }
}
