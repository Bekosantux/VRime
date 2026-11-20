using System;
using UnityEngine;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// OnGUI のマウスの操作を設定画面のテクスチャの座標に変換し、<see cref="ISettingsPointer"/> へ渡す。
    /// 設定画面をマウスで操作する表示（エディターのプレビュー、デスクトップの設定ウィンドウ）が使う。
    /// 左ボタンだけを扱う。ボタンを押していない間の移動は、描画のイベントのときに位置を見て渡す。
    /// </summary>
    public sealed class SettingsMouseInput
    {
        readonly ISettingsPointer _pointer;
        readonly Vector2 _textureSize;
        bool _inside;
        Vector2 _position;

        /// <param name="textureSize">設定画面のテクスチャのピクセル数。</param>
        public SettingsMouseInput(ISettingsPointer pointer, Vector2Int textureSize)
        {
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            if (textureSize.x <= 0 || textureSize.y <= 0) throw new ArgumentOutOfRangeException(nameof(textureSize), textureSize, "大きさが正ではありません。");
            _textureSize = textureSize;
        }

        /// <summary>OnGUI のイベントごとに呼ぶ。<paramref name="area"/> は設定画面のテクスチャを描画する矩形（GUI の座標）。</summary>
        public void Handle(Event e, Rect area)
        {
            bool inside = area.Contains(e.mousePosition);
            Vector2 position = new Vector2(
                (e.mousePosition.x - area.x) / area.width * _textureSize.x,
                (e.mousePosition.y - area.y) / area.height * _textureSize.y);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && inside:
                    _pointer.PointerDown(position);
                    e.Use();
                    break;
                case EventType.MouseDrag when e.button == 0:
                    _pointer.PointerMove(position);
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0:
                    _pointer.PointerUp(position);
                    e.Use();
                    break;
                case EventType.Repaint:
                    if (inside && (!_inside || position != _position)) _pointer.PointerMove(position);
                    else if (!inside && _inside) _pointer.PointerLeave();
                    break;
            }
            _inside = inside;
            _position = position;
        }

        /// <summary>設定画面を表示しなくなった。マウスが画面の上にあれば、離れたことにする（操作の途中であれば、そこで終える）。</summary>
        public void Leave()
        {
            if (_inside) _pointer.PointerLeave();
            _inside = false;
        }
    }
}
