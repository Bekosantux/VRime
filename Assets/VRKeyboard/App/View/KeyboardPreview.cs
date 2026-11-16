#if UNITY_EDITOR
using System;
using UnityEngine;
using VRKeyboard.App.Input;
using VRKeyboard.App.Platform;
using VRKeyboard.App.Settings;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// エディター専用の確認表示。VR 機器がなくても、Game ビューにオーバーレイのテクスチャ・振動・操作キーを表示する。
    /// 設定画面の表示中は、設定画面のテクスチャを表示し、開発用の環境ではマウスで操作できる。
    /// メインカメラは使わず、OnGUI から描く。
    /// </summary>
    public sealed class KeyboardPreview
    {
        const double HapticDisplaySeconds = 0.4;

        static readonly Color Backdrop = new Color(0.30f, 0.32f, 0.36f, 1f);

        readonly KeyboardSession _session;
        readonly IKeyboardPlatform _platform;
        readonly RenderTexture _texture;
        readonly RenderTexture _settingsTexture;
        readonly DebugHapticOutput _haptics;
        readonly SettingsMouseInput _settingsMouse;
        GUIStyle _label;
        GUIStyle _closed;

        /// <param name="haptics">開発用の振動の出力先。OpenVR のとき（実機が振動する）は null。</param>
        /// <param name="settingsPointer">設定画面へのマウスの操作の渡し先。OpenVR のとき（レーザーで操作する）は null。</param>
        public KeyboardPreview(KeyboardSession session, IKeyboardPlatform platform, RenderTexture texture, RenderTexture settingsTexture,
            DebugHapticOutput haptics, ISettingsPointer settingsPointer)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _texture = texture != null ? texture : throw new ArgumentNullException(nameof(texture));
            _settingsTexture = settingsTexture != null ? settingsTexture : throw new ArgumentNullException(nameof(settingsTexture));
            _haptics = haptics;
            if (settingsPointer != null)
                _settingsMouse = new SettingsMouseInput(settingsPointer, new Vector2Int(settingsTexture.width, settingsTexture.height));
        }

        /// <summary>OnGUI から呼ぶ。</summary>
        public void Draw()
        {
            float size = Mathf.Min(Screen.height, Screen.width * 0.7f);
            var area = new Rect(0f, 0f, size, size);
            Rect settingsArea = UiRects.FitInto(area, _settingsTexture.width, _settingsTexture.height);
            bool settingsVisible = _platform.IsSettingsVisible;

            if (_settingsMouse != null)
            {
                if (settingsVisible) _settingsMouse.Handle(Event.current, settingsArea);
                else _settingsMouse.Leave();
            }
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();

            Color previous = GUI.color;
            GUI.color = Backdrop;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (settingsVisible)
            {
                GUI.DrawTexture(settingsArea, _settingsTexture, ScaleMode.StretchToFill, true);
            }
            else if (_session.IsOpen)
            {
                GUI.DrawTexture(area, _texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                string grip = _haptics != null ? "両手のグリップ（Space）" : "両手のグリップ";
                GUI.Label(area, "キーボードは閉じています\n" + grip + "を\n素早く 2回押すと開きます", _closed);
            }

            DrawSide(new Rect(size + 16f, 12f, Screen.width - size - 28f, Screen.height - 24f));
        }

        void DrawSide(Rect rect)
        {
            // 振動の表示先がない（OpenVR）ときは、コントローラーで操作するため PC のキーの案内を出さない。
            string text = _haptics == null
                ? "SteamVR のコントローラーで操作します"
                : "操作（旧 Input Manager）\n"
                + "左手: WASD = スティック / Q = トリガー / E = グリップ\n"
                + "右手: IJKL = スティック / U = トリガー / O = グリップ\n"
                + "Space = 両手のグリップ\n"
                + "Tab = 設定画面（ダッシュボード）の表示切り替え\n\n"
                + "振動  左: " + HapticText(Hand.Left) + "   右: " + HapticText(Hand.Right);
            GUI.Label(rect, text, _label);
        }

        string HapticText(Hand hand)
        {
            if (_haptics == null) return "-";
            return _haptics.TryGetRecent(hand, HapticDisplaySeconds, out HapticKind kind) ? kind.ToString() : "-";
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = false };
            _label.normal.textColor = Color.white;
            _closed = new GUIStyle(_label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        }
    }
}
#endif
