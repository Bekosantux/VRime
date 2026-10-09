using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 実行時に生成する TMP の Dynamic SDF フォントアセット（Noto Sans JP）。
    /// アセットとして保存しない。Play 中にアトラスが書き換わり、アセットに差分が出るのを避けるため。
    /// 元の <see cref="Font"/> はシーンのコンポーネントから受け取る（Include Font Data が有効である必要がある）。
    /// </summary>
    public sealed class KeyboardFonts : IDisposable
    {
        // 日本語は字種が多いため、複数のアトラスに分けて追加する。サンプリングサイズはオーバーレイ上の最大の文字より大きく取る。
        const int SamplingPointSize = 64;
        const int AtlasPadding = 6;
        const int AtlasSize = 2048;

        public KeyboardFonts(Font regular, Font bold)
        {
            Regular = Create(regular, nameof(regular));
            Bold = Create(bold, nameof(bold));
        }

        public TMP_FontAsset Regular { get; private set; }
        public TMP_FontAsset Bold { get; private set; }

        public void Dispose()
        {
            Destroy(Regular);
            Destroy(Bold);
            Regular = null;
            Bold = null;
        }

        static TMP_FontAsset Create(Font font, string parameterName)
        {
            if (font == null) throw new ArgumentNullException(parameterName, "フォントが設定されていません。");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                throw new InvalidOperationException("フォントアセットを生成できませんでした: " + font.name + "（Include Font Data を確認してください）");
            asset.name = font.name + " SDF (runtime)";
            return asset;
        }

        // フォントアセットが保持するアトラスのテクスチャとマテリアルは、アセット自身の破棄では破棄されない。
        static void Destroy(TMP_FontAsset asset)
        {
            if (asset == null) return;
            if (asset.atlasTextures != null)
            {
                foreach (Texture2D texture in asset.atlasTextures)
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                }
            }
            if (asset.material != null) UnityEngine.Object.Destroy(asset.material);
            UnityEngine.Object.Destroy(asset);
        }
    }
}
