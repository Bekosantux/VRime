using System;
using VRime.Core.Input;

namespace VRime.Core.Grip
{
    /// <summary>
    /// スティックの傾きの分類。倒し量がしきい値以上で方向を決め、未満なら中央とする。
    /// 分類はその時点の傾きだけで決まり、直前の状態によらない（倒す・戻す判定に同じしきい値を使う）。
    /// </summary>
    public static class StickClassifier
    {
        /// <summary>3×3 のセル数。0..8 は行優先で左上が 0、中央が 4。</summary>
        public const int CellCount = 9;

        public const int NeutralCell = 4;

        /// <summary>8方向（45° ずつ）に分けて 3×3 のセル番号にする。</summary>
        public static int ClassifyCell(StickValue stick, float threshold)
        {
            if (Magnitude(stick) < threshold) return NeutralCell;

            // 0=右, 1=右上, 2=上 ... 反時計回り
            int sector = Sector(stick, 8);
            int dx = sector == 0 || sector == 1 || sector == 7 ? 1 : (sector == 3 || sector == 4 || sector == 5 ? -1 : 0);
            int dy = sector == 1 || sector == 2 || sector == 3 ? 1 : (sector == 5 || sector == 6 || sector == 7 ? -1 : 0);
            return (1 - dy) * 3 + (dx + 1);
        }

        /// <summary>4方向（90° ずつ）＋中央に分ける。</summary>
        public static StickDirection ClassifyDirection(StickValue stick, float threshold)
        {
            if (Magnitude(stick) < threshold) return StickDirection.Center;

            switch (Sector(stick, 4))
            {
                case 0: return StickDirection.Right;
                case 1: return StickDirection.Up;
                case 2: return StickDirection.Left;
                default: return StickDirection.Down;
            }
        }

        static float Magnitude(StickValue stick)
        {
            return (float)Math.Sqrt(stick.X * stick.X + stick.Y * stick.Y);
        }

        // 0°（右）を中心とする扇形から数え、反時計回りに番号を振る。
        static int Sector(StickValue stick, int count)
        {
            double degrees = Math.Atan2(stick.Y, stick.X) * 180.0 / Math.PI;
            if (degrees < 0) degrees += 360.0;
            double width = 360.0 / count;
            return (int)Math.Floor((degrees + width / 2.0) / width) % count;
        }
    }
}
