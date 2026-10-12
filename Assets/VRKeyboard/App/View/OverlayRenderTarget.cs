using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// シーンに置いた World Space のキャンバスを、専用のカメラで透明な RenderTexture に描く。
    /// カメラは常に無効にしておき、<see cref="Render"/> を呼んだときだけ 1回描く。描かれたテクスチャ（<see cref="Texture"/>）は OpenVR のオーバーレイへ渡す。
    /// テクスチャの大きさはキャンバスの大きさ（ピクセル）と同じにする。カメラの向き・位置・映す範囲は、実行時にキャンバスに合わせる。
    /// 描画先ごとに別のレイヤーを使う。カメラは自分のレイヤーだけを映すため、複数の描画先の uGUI が互いに映り込まない。
    /// テクスチャの破棄は <see cref="Release"/> で行う。OpenVR のオーバーレイが参照しているため、破棄の順序を呼び出し側が決める。
    /// </summary>
    public sealed class OverlayRenderTarget : MonoBehaviour
    {
        [Tooltip("無効にした正投影のカメラ。カリングマスクはこの描画先のレイヤーだけにする。")]
        [SerializeField] Camera _camera;

        [Tooltip("World Space のキャンバス。大きさはテクスチャのピクセル数（整数）にする。")]
        [SerializeField] Canvas _canvas;

        UniversalRenderPipeline.SingleCameraRequest _request;

        /// <summary>uGUI の部品を置くキャンバス。</summary>
        public Canvas Canvas => _canvas;

        /// <summary>描画先。オーバーレイのテクスチャとして渡す。<see cref="Initialize"/> の前と <see cref="Release"/> の後は null。</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>これまでに描いた回数。</summary>
        public int RenderCount { get; private set; }

        /// <summary>描き終わるたびに呼ばれる。OpenVR のオーバーレイへ反映するのに使う。</summary>
        public event Action Rendered;

        /// <param name="layerName">この描画先の uGUI を置くレイヤー。他の描画先と重ならず、Project Settings の Tags and Layers に登録されている必要がある。</param>
        public void Validate(string layerName)
        {
            SceneChecks.Require(_camera, this, nameof(_camera));
            SceneChecks.Require(_canvas, this, nameof(_canvas));
            SceneChecks.RequireUnder(_camera, this, this, nameof(_camera));
            SceneChecks.RequireUnder(_canvas, this, this, nameof(_canvas));
            string path = SceneChecks.PathOf(transform);

            int layer = LayerOf(layerName);
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer != layer)
                    throw new InvalidOperationException(SceneChecks.PathOf(t) + " のレイヤーが " + layerName + " ではありません。");
            }

            if (_camera.enabled) throw new InvalidOperationException(path + " のカメラが有効です。描画は Render で行うため、無効にしてください。");
            if (!_camera.orthographic) throw new InvalidOperationException(path + " のカメラが正投影ではありません。");
            if (_camera.cullingMask != 1 << layer) throw new InvalidOperationException(path + " のカメラのカリングマスクが " + layerName + " だけではありません。");
            if (!(_camera.nearClipPlane > 0f) || !(_camera.farClipPlane > _camera.nearClipPlane))
                throw new InvalidOperationException(path + " のカメラの描画範囲（Clipping Planes）が正しくありません。");
            if (_camera.clearFlags != CameraClearFlags.SolidColor || _camera.backgroundColor.a != 0f)
                throw new InvalidOperationException(path + " のカメラは、透明な単色（Solid Color・アルファ 0）で消去する必要があります。");
            // GetUniversalAdditionalCameraData は無ければ追加するため、検証では使わない。
            if (!_camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                throw new InvalidOperationException(path + " のカメラに URP のカメラ設定（UniversalAdditionalCameraData）がありません。");
            if (cameraData.renderPostProcessing)
                throw new InvalidOperationException(path + " のカメラのポストプロセスが有効です。透明度が失われるため、無効にしてください。");

            if (_canvas.renderMode != RenderMode.WorldSpace) throw new InvalidOperationException(path + " のキャンバスが World Space ではありません。");
            Vector2 size = ((RectTransform)_canvas.transform).rect.size;
            if (!IsPositiveInteger(size.x) || !IsPositiveInteger(size.y))
                throw new InvalidOperationException(path + " のキャンバスの大きさが正の整数ではありません: " + size);
        }

        /// <summary>テクスチャを作り、カメラをキャンバスに合わせる。</summary>
        public void Initialize(string layerName)
        {
            Validate(layerName);
            if (Texture != null) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " は初期化済みです。");

            var canvasRect = (RectTransform)_canvas.transform;
            Rect rect = canvasRect.rect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);

            // HDR・MSAA なし。カラーはリニア色空間では sRGB のテクスチャになる。
            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = gameObject.name,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Texture.Create();
            _camera.targetTexture = Texture;
            _camera.aspect = (float)width / height;

            // キャンバスの正面（-Z の側）から、キャンバスの中心を向く。キャンバスが描画範囲の中央に来る距離に置く。
            Transform cameraTransform = _camera.transform;
            Vector3 center = canvasRect.TransformPoint(rect.center);
            float distance = (_camera.nearClipPlane + _camera.farClipPlane) / 2f;
            cameraTransform.SetPositionAndRotation(center - canvasRect.forward * distance, canvasRect.rotation);
            _camera.orthographicSize = rect.height * canvasRect.lossyScale.y / 2f;

            // 描画の要求。出力先は常に同じテクスチャ。
            _request = new UniversalRenderPipeline.SingleCameraRequest { destination = Texture };
        }

        /// <summary>
        /// uGUI を確定してから 1回描く。
        /// カメラは無効のため、通常のフレームの描画に任せるとキャンバスの再構築が描画のタイミングに依存する。
        /// <see cref="Canvas.ForceUpdateCanvases"/> でレイアウト・メッシュの更新を先に済ませておく。
        /// 描画には <see cref="RenderPipeline.SubmitRenderRequest{RequestData}"/> を使う。Camera.Render() と違い、
        /// 出力先を要求に含められ、URP の描画要求として対応が確認できる（非対応なら例外にする）。
        /// </summary>
        public void Render()
        {
            if (Texture == null) throw new InvalidOperationException(SceneChecks.PathOf(transform) + " は初期化されていないか、破棄済みです。");
            Canvas.ForceUpdateCanvases();
            if (!RenderPipeline.SupportsRenderRequest(_camera, _request))
                throw new InvalidOperationException("現在のレンダーパイプラインは SingleCameraRequest に対応していません。");
            RenderPipeline.SubmitRenderRequest(_camera, _request);
            RenderCount++;
            Rendered?.Invoke();
        }

        /// <summary>テクスチャを破棄する。オーバーレイ（OpenVR 側）がテクスチャを参照しなくなってから呼ぶ。</summary>
        public void Release()
        {
            if (Texture == null) return;
            // シーンの終了時は、カメラが先に破棄されていることがある。
            if (_camera != null) _camera.targetTexture = null;
            Texture.Release();
            Destroy(Texture);
            Texture = null;
        }

        static int LayerOf(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) throw new ArgumentException("レイヤー名が空です。", nameof(layerName));
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                throw new InvalidOperationException("レイヤー '" + layerName + "' が登録されていません（Project Settings の Tags and Layers）。");
            return layer;
        }

        static bool IsPositiveInteger(float value)
        {
            return value >= 1f && Mathf.Approximately(value, Mathf.Round(value));
        }
    }
}
