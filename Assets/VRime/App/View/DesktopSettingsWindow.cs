using System;
using UnityEngine;
using VRime.App.Native;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// デスクトップの設定ウィンドウ（SPEC 8.1）。隠している Unity のプレイヤーのウィンドウを表示し、設定画面のテクスチャを OnGUI で描いて、マウスで操作させる。
    /// 設定画面の状態と描画は、ダッシュボードの設定タブと共有する（同じテクスチャと <see cref="ISettingsPointer"/>）。
    /// メインカメラは使わない。ビルドでだけ使う（<see cref="PlayerWindow"/> はエディターでは使えないため）。
    /// </summary>
    public sealed class DesktopSettingsWindow
    {
        // 最初に開くときの大きさの上限（画面の解像度に対する割合）。テクスチャがこれより大きければ、縦横比を保って縮める。
        const float MaxScreenFraction = 0.8f;

        // ウィンドウの縦横比がテクスチャと違うときに、テクスチャの周りに見える色。
        static readonly Color Backdrop = new Color(0.08f, 0.09f, 0.11f, 1f);

        readonly RenderTexture _texture;
        readonly SettingsMouseInput _mouse;
        readonly string _title;
        bool _opened;

        /// <param name="texture">設定画面の描画先のテクスチャ。</param>
        /// <param name="pointer">マウスの操作の渡し先。</param>
        /// <param name="title">ウィンドウのタイトル。</param>
        public DesktopSettingsWindow(RenderTexture texture, ISettingsPointer pointer, string title)
        {
            _texture = texture != null ? texture : throw new ArgumentNullException(nameof(texture));
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("タイトルが空です。", nameof(title));
            _mouse = new SettingsMouseInput(pointer, new Vector2Int(texture.width, texture.height));
            _title = title;
        }

        /// <summary>ウィンドウを表示しているか（最小化している間を含む）。</summary>
        public bool IsVisible { get; private set; }

        /// <summary>ウィンドウを表示して前面にする。表示中に呼んでもよい（最小化していれば元に戻す）。</summary>
        public void Show()
        {
            // 前面にできなくても、ウィンドウは表示されているため続ける。
            if (!PlayerWindow.ShowAndActivate()) Debug.LogWarning("設定ウィンドウを前面にできませんでした。");
            IsVisible = true;
            if (_opened) return;

            // 大きさとタイトルは最初に開いたときだけ決める。以降は利用者が変えた大きさを保つ。
            _opened = true;
            PlayerWindow.SetTitle(_title);
            Vector2Int size = InitialSize();
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        /// <summary>ウィンドウを隠す。マウスで操作している途中であれば、そこで終える。</summary>
        public void Hide()
        {
            if (!IsVisible) return;
            PlayerWindow.Hide();
            IsVisible = false;
            _mouse.Leave();
        }

        /// <summary>OnGUI から呼ぶ。表示していなければ何もしない。</summary>
        public void Draw()
        {
            if (!IsVisible) return;
            var window = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect area = UiRects.FitInto(window, _texture.width, _texture.height);
            _mouse.Handle(Event.current, area);
            if (Event.current.type != EventType.Repaint) return;

            Color previous = GUI.color;
            GUI.color = Backdrop;
            GUI.DrawTexture(window, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.DrawTexture(area, _texture, ScaleMode.StretchToFill, true);
        }

        // テクスチャと同じ大きさ。画面に収まらなければ、縦横比を保って縮める。
        Vector2Int InitialSize()
        {
            Resolution screen = Screen.currentResolution;
            var limit = new Rect(0f, 0f, screen.width * MaxScreenFraction, screen.height * MaxScreenFraction);
            if (_texture.width <= limit.width && _texture.height <= limit.height) return new Vector2Int(_texture.width, _texture.height);
            Rect fitted = UiRects.FitInto(limit, _texture.width, _texture.height);
            return new Vector2Int(Mathf.FloorToInt(fitted.width), Mathf.FloorToInt(fitted.height));
        }
    }
}
