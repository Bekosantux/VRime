namespace VRKeyboard.Core.Input
{
    /// <summary>片手のコントローラーの生の状態。トリガーを押したかの判定と、押した瞬間（エッジ）の検出は Core 側で行う。</summary>
    public readonly struct HandState
    {
        public HandState(StickValue stick, float triggerPull, bool grip)
        {
            Stick = stick;
            TriggerPull = triggerPull;
            Grip = grip;
        }

        public StickValue Stick { get; }

        /// <summary>トリガーの引き量。0（離している）〜1（引き切っている）。</summary>
        public float TriggerPull { get; }

        public bool Grip { get; }

        public HandState WithGrip(bool grip)
        {
            return new HandState(Stick, TriggerPull, grip);
        }
    }
}
