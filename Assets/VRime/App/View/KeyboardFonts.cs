using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace VRime.App.View
{
    /// <summary>
    /// シーンの文字のフォントを、実行時に生成する TMP の Dynamic SDF フォントアセット（Noto Sans JP）に差し替える。
    /// シーンには編集用のフォントアセット（Dynamic・小さいアトラス）を設定しておき、起動時に同じ元のフォントから作った実行用のアセットに置き換える。
    /// 実行用のアセットは保存しない。実行中にアトラスへ文字が追加され、アセットに差分が出るのを避けるため。
    /// 元のフォントは編集用のアセットの <see cref="TMP_FontAsset.sourceFontFile"/> を使う（Include Font Data が有効である必要がある）。
    /// </summary>
    public sealed class KeyboardFonts : IDisposable
    {
        // 日本語は字種が多いため、複数のアトラスに分けて追加する。サンプリングサイズはオーバーレイ上の最大の文字より大きく取る。
        const int SamplingPointSize = 64;
        const int AtlasPadding = 6;
        const int AtlasSize = 2048;

        readonly Dictionary<TMP_FontAsset, TMP_FontAsset> _runtimeByEdit = new Dictionary<TMP_FontAsset, TMP_FontAsset>();

        /// <param name="regularEdit">Regular の編集用のフォントアセット。</param>
        /// <param name="boldEdit">Bold の編集用のフォントアセット。</param>
        public KeyboardFonts(TMP_FontAsset regularEdit, TMP_FontAsset boldEdit)
        {
            ValidateEditFont(regularEdit, nameof(regularEdit));
            ValidateEditFont(boldEdit, nameof(boldEdit));
            if (regularEdit == boldEdit) throw new ArgumentException("Regular と Bold に同じフォントアセットが設定されています。", nameof(boldEdit));
            try
            {
                _runtimeByEdit.Add(regularEdit, Create(regularEdit));
                _runtimeByEdit.Add(boldEdit, Create(boldEdit));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>編集用のフォントアセットとして使えることを確かめる。</summary>
        public static void ValidateEditFont(TMP_FontAsset font, string label)
        {
            if (font == null) throw new InvalidOperationException(label + " のフォントアセットが設定されていません。");
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                throw new InvalidOperationException(font.name + " は Dynamic のフォントアセットではありません。");
            if (font.sourceFontFile == null)
                throw new InvalidOperationException(font.name + " に元のフォント（Source Font File）がありません。");
        }

        /// <summary>root の下のすべての文字（非表示のものを含む）が、編集用のフォントアセットのいずれかを使っていることを確かめる。</summary>
        public static void ValidateTexts(Transform root, TMP_FontAsset regularEdit, TMP_FontAsset boldEdit)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != regularEdit && text.font != boldEdit)
                    throw new InvalidOperationException(SceneChecks.PathOf(text.transform) + " のフォントが " +
                        NameOf(regularEdit) + " と " + NameOf(boldEdit) + " のどちらでもありません（" + NameOf(text.font) + "）。");
            }
        }

        /// <summary>root の下のすべての文字（非表示のものを含む）のフォントを、実行用のアセットに置き換える。</summary>
        public void Apply(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (_runtimeByEdit.Count == 0) throw new ObjectDisposedException(nameof(KeyboardFonts));
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!_runtimeByEdit.TryGetValue(text.font, out TMP_FontAsset runtime))
                    throw new InvalidOperationException(SceneChecks.PathOf(text.transform) + " のフォントが編集用のフォントアセットではありません（" + NameOf(text.font) + "）。");
                text.font = runtime;
            }
        }

        public void Dispose()
        {
            foreach (TMP_FontAsset runtime in _runtimeByEdit.Values) Destroy(runtime);
            _runtimeByEdit.Clear();
        }

        static TMP_FontAsset Create(TMP_FontAsset edit)
        {
            Font font = edit.sourceFontFile;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                throw new InvalidOperationException("フォントアセットを生成できませんでした: " + font.name + "（Include Font Data を確認してください）");
            asset.name = font.name + " SDF (runtime)";
            return asset;
        }

        static string NameOf(TMP_FontAsset font) => font != null ? font.name : "なし";

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
