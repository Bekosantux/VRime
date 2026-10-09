namespace VRKeyboard.Core.Input
{
    /// <summary>片手のコントローラーの生の状態。押した瞬間（エッジ）の検出は Core 側で行う。</summary>
    public readonly struct HandState
    {
        public HandState(StickValue stick, bool trigger, bool grip)
        {
            Stick = stick;
            Trigger = trigger;
            Grip = grip;
        }

        public StickValue Stick { get; }
        public bool Trigger { get; }
        public bool Grip { get; }

        public HandState WithGrip(bool grip)
        {
            return new HandState(Stick, Trigger, grip);
        }
    }
}
