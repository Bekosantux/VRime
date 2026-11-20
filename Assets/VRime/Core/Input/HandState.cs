namespace VRime.Core.Input
{
    /// <summary>
    /// 片手のコントローラーの生の状態。値は SteamVR のアクション（docs/spec/grip-input.md「コントローラーの割り当て」）ごとに持ち、物理的なボタンの情報は持たない。
    /// トリガーを押したかの判定と、押した瞬間（エッジ）の検出は Core 側で行う。
    /// </summary>
    public readonly struct HandState
    {
        public HandState(StickValue stick, float triggerPull, bool openClose, bool panel)
        {
            Stick = stick;
            TriggerPull = triggerPull;
            OpenClose = openClose;
            Panel = panel;
        }

        /// <summary>キーの選択（スティック）。</summary>
        public StickValue Stick { get; }

        /// <summary>キーの入力（トリガー）の引き量。0（離している）〜1（引き切っている）。</summary>
        public float TriggerPull { get; }

        /// <summary>展開・収納のボタン。両手で2回続けて同時押しすると開閉する（docs/spec/keyboard.md「開閉」）。</summary>
        public bool OpenClose { get; }

        /// <summary>パネルの切り替えのボタン。押している間、その手のパネルが機能ページになる（docs/spec/grip-input.md）。</summary>
        public bool Panel { get; }

        public HandState WithPanel(bool panel)
        {
            return new HandState(Stick, TriggerPull, OpenClose, panel);
        }
    }
}
