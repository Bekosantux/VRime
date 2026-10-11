using System;
using VRKeyboard.App.Settings;
using VRKeyboard.App.View;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// キーボードのオーバーレイの、HMD に対する相対位置（頭部固定。SPEC 4.1）。値は <see cref="KeyboardPlacement"/>（設定画面で変更できる）。
    /// 位置は「キーボード本体（盤面）の中心」の位置として指定する。テクスチャの上部は候補欄の透明な領域のため、
    /// OpenVR が基準にするテクスチャの中心は盤面の中心よりも上にある。その差は <see cref="KeyboardTheme"/> の寸法から求める。
    /// </summary>
    public static class OverlayPlacement
    {
        /// <summary>
        /// オーバーレイの座標系から HMD の座標系への変換（3x4 の行優先。<c>HmdMatrix34_t</c> の m0〜m11 の並び）。
        /// HMD の座標系は右手系で、+X が右、+Y が上、-Z が前。
        /// </summary>
        public static float[] ComputeOverlayToHmd(KeyboardPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));

            // 傾きは X 軸まわりの回転。負の角度で、法線（オーバーレイの +Z）が上向きになり、下前方の盤面が目の方を向く。
            float pitch = -placement.TiltDegrees * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(pitch);
            float sin = (float)Math.Sin(pitch);

            // テクスチャの中心から盤面の中心までの、オーバーレイ面内の下向きの距離。
            float boardOffsetDown = BoardOffsetDownMeters(placement);

            // 盤面の中心が (右, -下, -前) に来るよう、テクスチャの中心の位置を決める。
            // オーバーレイの下向き（-Y）は、回転後に (0, -cos, -sin) の向きになる。
            return new[]
            {
                1f, 0f, 0f, placement.RightMeters,
                0f, cos, -sin, -placement.DownMeters + cos * boardOffsetDown,
                0f, sin, cos, -placement.ForwardMeters + sin * boardOffsetDown,
            };
        }

        /// <summary>テクスチャの中心から盤面の中心までの、オーバーレイ面内の下向きの距離。</summary>
        public static float BoardOffsetDownMeters(KeyboardPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            float metersPerPixel = placement.WidthMeters / KeyboardTheme.Width;
            return (KeyboardTheme.BoardCenterY - KeyboardTheme.Height / 2f) * metersPerPixel;
        }

        /// <summary>変換 <paramref name="matrix"/>（<see cref="ComputeOverlayToHmd"/> の形式）で点を変換する。</summary>
        public static (float x, float y, float z) Transform(float[] matrix, float x, float y, float z)
        {
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));
            if (matrix.Length != 12) throw new ArgumentException("3x4 の行列ではありません。", nameof(matrix));
            return (
                matrix[0] * x + matrix[1] * y + matrix[2] * z + matrix[3],
                matrix[4] * x + matrix[5] * y + matrix[6] * z + matrix[7],
                matrix[8] * x + matrix[9] * y + matrix[10] * z + matrix[11]);
        }
    }
}
