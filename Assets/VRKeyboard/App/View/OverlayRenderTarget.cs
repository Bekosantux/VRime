using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// uGUI を専用のカメラで透明な RenderTexture に描く。カメラは常に無効にしておき、<see cref="Render"/> を呼んだときだけ 1回描く。
    /// 描かれたテクスチャ（<see cref="Texture"/>）は、OpenVR のオーバーレイへ渡すもの。
    /// </summary>
    public sealed class OverlayRenderTarget : IDisposable
    {
        /// <summary>このレイヤーの描画物だけをカメラに映す。ProjectSettings の Tags and Layers に登録されている必要がある。</summary>
        public const string LayerName = "VRKeyboardOverlay";

        readonly GameObject _root;
        readonly Camera _camera;
        readonly UniversalRenderPipeline.SingleCameraRequest _request;

        public OverlayRenderTarget(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, null);
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, null);

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
                throw new InvalidOperationException("レイヤー '" + LayerName + "' が登録されていません（Project Settings の Tags and Layers）。");

            // HDR・MSAA なし。カラーはリニア色空間では sRGB のテクスチャになる。
            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "VRKeyboard Overlay",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Texture.Create();

            _root = new GameObject("VRKeyboard Overlay") { layer = layer };

            // 他のカメラ・レイヤーに映らないよう、専用レイヤーだけを映す。
            _camera = _root.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = height / 2f;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 10f;
            _camera.cullingMask = 1 << layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            // Screen Space - Camera のキャンバスは、カメラの出力先の大きさに合わせて広がる。常に出力先を指定しておく。
            _camera.targetTexture = Texture;

            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)) { layer = layer };
            canvasObject.transform.SetParent(_root.transform, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = _camera;
            Canvas.planeDistance = 1f;
            Canvas.pixelPerfect = false;

            // 描画の要求。出力先は常に同じテクスチャ。
            _request = new UniversalRenderPipeline.SingleCameraRequest { destination = Texture };
        }

        /// <summary>uGUI の部品を置くキャンバス。サイズはテクスチャのピクセルと同じ（スケール 1）。</summary>
        public Canvas Canvas { get; }

        /// <summary>描画先。オーバーレイのテクスチャとして渡す。</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>これまでに描いた回数。</summary>
        public int RenderCount { get; private set; }

        /// <summary>描き終わるたびに呼ばれる。OpenVR のオーバーレイへ反映するのに使う。</summary>
        public event Action Rendered;

        /// <summary>
        /// uGUI を確定してから 1回描く。
        /// カメラは無効のため、通常のフレームの描画に任せるとキャンバスの再構築が描画のタイミングに依存する。
        /// <see cref="Canvas.ForceUpdateCanvases"/> でレイアウト・メッシュの更新を先に済ませておく。
        /// 描画には <see cref="RenderPipeline.SubmitRenderRequest{RequestData}"/> を使う。Camera.Render() と違い、
        /// 出力先を要求に含められ、URP の描画要求として対応が確認できる（非対応なら例外にする）。
        /// </summary>
        public void Render()
        {
            if (Texture == null) throw new ObjectDisposedException(nameof(OverlayRenderTarget));
            Canvas.ForceUpdateCanvases();
            if (!RenderPipeline.SupportsRenderRequest(_camera, _request))
                throw new InvalidOperationException("現在のレンダーパイプラインは SingleCameraRequest に対応していません。");
            RenderPipeline.SubmitRenderRequest(_camera, _request);
            RenderCount++;
            Rendered?.Invoke();
        }

        public void Dispose()
        {
            if (Texture == null) return;
            // シーンの終了時は、このオブジェクトが先に破棄されていることがある。
            if (_camera != null) _camera.targetTexture = null;
            Texture.Release();
            UnityEngine.Object.Destroy(Texture);
            Texture = null;
            UnityEngine.Object.Destroy(_root);
        }
    }
}
