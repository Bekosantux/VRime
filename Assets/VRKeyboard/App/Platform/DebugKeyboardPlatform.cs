using System;
using UnityEngine;
using VRKeyboard.App.Input;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.Platform
{
    /// <summary>
    /// VR 機器なしで開発するための環境。PC のキーボードで入力し、振動は表示用に保持するだけ。
    /// 表示は Game ビューのプレビュー（<see cref="View.KeyboardPreview"/>）が担うため、開閉と位置に伴う処理はない。
    /// Tab キーで、ダッシュボードの設定タブを開いた状態（ダッシュボードの表示を含む）を模す。
    /// </summary>
    public sealed class DebugKeyboardPlatform : IKeyboardPlatform
    {
        public const KeyCode SettingsToggleKey = KeyCode.Tab;

        readonly DebugKeyboardControllerSource _controller = new DebugKeyboardControllerSource();
        readonly DebugHapticOutput _haptics = new DebugHapticOutput();

        public IControllerSource Controller => _controller;
        public IHapticOutput Haptics => _haptics;

        /// <summary>プレビューの表示用。</summary>
        public DebugHapticOutput DebugHaptics => _haptics;

        /// <summary>設定タブはダッシュボードの中にあるため、ダッシュボードが入力を持っているとみなす。</summary>
        public bool IsDashboardHoldingInput => IsSettingsVisible;

        public bool IsSettingsVisible { get; private set; }

        /// <summary>PC のキーボードの入力は、前面のウィンドウにしか届かない。</summary>
        public bool RequiresOwnWindowFocus => true;

        public event Action QuitRequested
        {
            add { }
            remove { }
        }

        public void BeginFrame()
        {
            if (UnityEngine.Input.GetKeyDown(SettingsToggleKey)) IsSettingsVisible = !IsSettingsVisible;
        }

        public void OnOpenChanged(bool isOpen)
        {
        }

        public void SetPlacementPreviewVisible(bool visible)
        {
        }

        public void SetPlacement(KeyboardPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
        }

        public void Dispose()
        {
        }
    }
}
