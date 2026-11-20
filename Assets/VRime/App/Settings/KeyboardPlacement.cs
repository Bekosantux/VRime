using System;

namespace VRime.App.Settings
{
    /// <summary>
    /// キーボードのオーバーレイの位置・傾き・大きさ（docs/spec/keyboard.md「表示位置」）。位置は HMD を基準にした「ボードの中心」の位置。
    /// 値は生成時に範囲を検査し、範囲外は例外にする。
    /// </summary>
    public sealed class KeyboardPlacement
    {
        public static readonly SettingRange DownRange = new SettingRange(0f, 0.60f, 0.01f);
        public static readonly SettingRange ForwardRange = new SettingRange(0.30f, 1.50f, 0.01f);
        public static readonly SettingRange RightRange = new SettingRange(-0.50f, 0.50f, 0.01f);
        public static readonly SettingRange TiltRange = new SettingRange(0f, 60f, 1f);
        public static readonly SettingRange WidthRange = new SettingRange(0.25f, 0.90f, 0.01f);

        /// <summary>既定の位置（下 0.26m・前 0.85m・傾き 17°・幅 0.45m）。</summary>
        public static readonly KeyboardPlacement Default = new KeyboardPlacement(0.26f, 0.85f, 0f, 17f, 0.45f);

        public KeyboardPlacement(float downMeters, float forwardMeters, float rightMeters, float tiltDegrees, float widthMeters)
        {
            Require(DownRange, downMeters, nameof(downMeters));
            Require(ForwardRange, forwardMeters, nameof(forwardMeters));
            Require(RightRange, rightMeters, nameof(rightMeters));
            Require(TiltRange, tiltDegrees, nameof(tiltDegrees));
            Require(WidthRange, widthMeters, nameof(widthMeters));
            DownMeters = downMeters;
            ForwardMeters = forwardMeters;
            RightMeters = rightMeters;
            TiltDegrees = tiltDegrees;
            WidthMeters = widthMeters;
        }

        /// <summary>ボードの中心の位置: HMD から下へ。</summary>
        public float DownMeters { get; }

        /// <summary>ボードの中心の位置: HMD から前へ。</summary>
        public float ForwardMeters { get; }

        /// <summary>ボードの中心の位置: HMD から右へ（負は左）。</summary>
        public float RightMeters { get; }

        /// <summary>ボードの傾き。上端が奥へ倒れる向きを正とし、0° は HMD の正面に垂直に立つ。</summary>
        public float TiltDegrees { get; }

        /// <summary>オーバーレイ（テクスチャ全体）の幅。高さはテクスチャの縦横比で決まる。</summary>
        public float WidthMeters { get; }

        public KeyboardPlacement WithDown(float value) => new KeyboardPlacement(value, ForwardMeters, RightMeters, TiltDegrees, WidthMeters);
        public KeyboardPlacement WithForward(float value) => new KeyboardPlacement(DownMeters, value, RightMeters, TiltDegrees, WidthMeters);
        public KeyboardPlacement WithRight(float value) => new KeyboardPlacement(DownMeters, ForwardMeters, value, TiltDegrees, WidthMeters);
        public KeyboardPlacement WithTilt(float value) => new KeyboardPlacement(DownMeters, ForwardMeters, RightMeters, value, WidthMeters);
        public KeyboardPlacement WithWidth(float value) => new KeyboardPlacement(DownMeters, ForwardMeters, RightMeters, TiltDegrees, value);

        public bool Equals(KeyboardPlacement other)
        {
            return other != null
                && DownMeters == other.DownMeters
                && ForwardMeters == other.ForwardMeters
                && RightMeters == other.RightMeters
                && TiltDegrees == other.TiltDegrees
                && WidthMeters == other.WidthMeters;
        }

        public override bool Equals(object obj) => Equals(obj as KeyboardPlacement);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DownMeters.GetHashCode();
                hash = hash * 31 + ForwardMeters.GetHashCode();
                hash = hash * 31 + RightMeters.GetHashCode();
                hash = hash * 31 + TiltDegrees.GetHashCode();
                hash = hash * 31 + WidthMeters.GetHashCode();
                return hash;
            }
        }

        static void Require(SettingRange range, float value, string name)
        {
            if (!range.Contains(value))
                throw new ArgumentOutOfRangeException(name, value, range.Min + "〜" + range.Max + " である必要があります。");
        }
    }
}
