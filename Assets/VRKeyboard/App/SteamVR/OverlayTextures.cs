using System;
using UnityEngine;
using Valve.VR;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>オーバーレイに Unity の RenderTexture（uGUI の描画先）を渡すための共通の設定。</summary>
    public static class OverlayTextures
    {
        /// <summary>作成済みの RenderTexture の Direct3D11 のテクスチャ。取得できなければ例外。</summary>
        public static IntPtr GetNativeTexture(RenderTexture texture, string parameterName)
        {
            if (texture == null) throw new ArgumentNullException(parameterName);
            if (!texture.IsCreated()) throw new ArgumentException("RenderTexture が作成されていません。", parameterName);
            IntPtr nativeTexture = texture.GetNativeTexturePtr();
            if (nativeTexture == IntPtr.Zero) throw new InvalidOperationException("RenderTexture のネイティブハンドルを取得できません。");
            return nativeTexture;
        }

        /// <summary>RenderTexture の内容を正しく表示するためのオーバーレイの設定を行う。</summary>
        public static void Configure(ulong handle)
        {
            CVROverlay overlay = OpenVR.Overlay;

            // uGUI の出力は、アルファを乗算済みで RenderTexture に書き込まれる
            // （UI/Default・TextMeshPro のシェーダーはどちらも Blend One OneMinusSrcAlpha で、アルファ 0.5 の白は RGB 188・A 128 になる）。
            // SteamVR の既定は乗算済みでない前提のため、フラグで乗算済みであることを伝える。
            OpenVRErrors.Check(overlay.SetOverlayFlag(handle, VROverlayFlags.IsPremultiplied, true), "SetOverlayFlag(IsPremultiplied)");

            // D3D11 の RenderTexture は上下が逆になるため、v を反転する（SPEC 3.3）。
            var bounds = new VRTextureBounds_t { uMin = 0f, uMax = 1f, vMin = 1f, vMax = 0f };
            OpenVRErrors.Check(overlay.SetOverlayTextureBounds(handle, ref bounds), "SetOverlayTextureBounds");
        }
    }
}
