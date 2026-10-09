using System;

namespace VRKeyboard.Core.Input
{
    public enum Hand
    {
        Left,
        Right,
    }

    public static class HandExtensions
    {
        public static Hand Opposite(this Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return Hand.Right;
                case Hand.Right: return Hand.Left;
                default: throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }
    }
}
