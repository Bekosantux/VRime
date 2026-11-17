using System;

namespace VRime.Core.Input
{
    /// <summary>1フレーム分の両手の状態。</summary>
    public readonly struct ControllerFrame
    {
        public ControllerFrame(HandState left, HandState right)
        {
            Left = left;
            Right = right;
        }

        public HandState Left { get; }
        public HandState Right { get; }

        public HandState Get(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return Left;
                case Hand.Right: return Right;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }
    }
}
