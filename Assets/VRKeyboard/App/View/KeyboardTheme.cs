using UnityEngine;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 表示の寸法と色。寸法はオーバーレイのテクスチャ上のピクセルで、幅 0.45m・0.85m 先に置いたとき
    /// 1ピクセルが約 0.44mm（0.85m で約 0.03°）になる。文字は 36px（約 1.6cm）以上にして読めるようにしている。
    /// </summary>
    public static class KeyboardTheme
    {
        public const int Width = 1024;
        public const int Height = 1024;

        // 上から: 候補欄 → 入力欄 → 状態行 → ページ行 → パネル。
        public const float CandidateAreaHeight = 336f;
        public const float BoardTop = 344f;

        /// <summary>キーボード本体（盤面）。候補欄の下から最下部まで。オーバーレイの位置はこの中心を基準にする。</summary>
        public const float BoardHeight = Height - BoardTop;
        public const float BoardCenterY = BoardTop + BoardHeight / 2f;

        public const float Margin = 20f;
        public const float InputTop = BoardTop + 12f;
        public const float InputHeight = 224f;
        public const float StatusTop = InputTop + InputHeight + 8f;
        public const float StatusHeight = 44f;
        public const float PageRowTop = StatusTop + StatusHeight + 6f;
        public const float PageRowHeight = 44f;
        public const float PanelTop = PageRowTop + PageRowHeight + 8f;
        public const float CellWidth = 148f;
        public const float CellHeight = 104f;
        public const float CellGap = 6f;
        public const float PanelWidth = CellWidth * 3f + CellGap * 2f;
        public const float PanelHeight = CellHeight * 3f + CellGap * 2f;

        public const float SegmentHeight = 64f;
        public const float CandidateHeight = 48f;
        public const float CandidateGap = 4f;
        public const float BannerHeight = 44f;

        public const float InputFontSize = 36f;
        public const float StatusFontSize = 30f;
        public const float SegmentFontSize = 40f;
        public const float CandidateFontSize = 36f;
        public const float CellFontSize = 56f;
        public const float CellFunctionFontSize = 40f;

        public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        public static readonly Color Board = new Color(0.06f, 0.07f, 0.09f, 0.90f);
        public static readonly Color Field = new Color(0.13f, 0.15f, 0.19f, 1f);
        public static readonly Color Text = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color TextDim = new Color(0.60f, 0.64f, 0.70f, 1f);
        public static readonly Color TextDisabled = new Color(0.36f, 0.39f, 0.44f, 1f);
        public static readonly Color Composition = new Color(1.00f, 0.88f, 0.51f, 1f);
        public static readonly Color Caret = new Color(0.45f, 0.78f, 1.00f, 1f);
        public static readonly Color Warning = new Color(1.00f, 0.60f, 0.35f, 1f);

        public static readonly Color Cell = new Color(0.20f, 0.23f, 0.29f, 1f);
        public static readonly Color CellDisabled = new Color(0.11f, 0.12f, 0.15f, 1f);
        public static readonly Color CellSelected = new Color(0.14f, 0.50f, 0.95f, 1f);
        public static readonly Color Lit = new Color(1.00f, 0.78f, 0.20f, 1f);

        public static readonly Color Chip = new Color(0.17f, 0.20f, 0.26f, 0.96f);
        public static readonly Color ChipSelected = new Color(0.14f, 0.50f, 0.95f, 0.98f);
        public static readonly Color ChipActive = new Color(0.20f, 0.34f, 0.52f, 1f);
        public static readonly Color BannerBackground = new Color(0.30f, 0.16f, 0.10f, 0.96f);

        /// <summary>変換中はパネルが操作を受けないため暗くする。</summary>
        public const float PanelDimAlpha = 0.35f;
    }
}
