using VRKeyboard.Core.Input;

namespace VRKeyboard.Core
{
    public enum HapticKind
    {
        /// <summary>セル・方向・文節・候補の移動。</summary>
        Move,

        /// <summary>入力・操作の成功。</summary>
        Input,

        /// <summary>キーボードの開閉。</summary>
        OpenClose,

        /// <summary>無効な操作。</summary>
        Invalid,
    }

    public readonly struct HapticEvent
    {
        public HapticEvent(Hand hand, HapticKind kind)
        {
            Hand = hand;
            Kind = kind;
        }

        public Hand Hand { get; }
        public HapticKind Kind { get; }
    }
}
