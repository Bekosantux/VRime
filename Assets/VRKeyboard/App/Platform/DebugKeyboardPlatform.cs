using System;
using VRKeyboard.App.Input;

namespace VRKeyboard.App.Platform
{
    /// <summary>
    /// VR 機器なしで開発するための環境。PC のキーボードで入力し、振動は表示用に保持するだけ。
    /// 表示は Game ビューのプレビュー（<see cref="View.KeyboardPreview"/>）が担うため、開閉に伴う処理はない。
    /// </summary>
    public sealed class DebugKeyboardPlatform : IKeyboardPlatform
    {
        readonly DebugKeyboardControllerSource _controller = new DebugKeyboardControllerSource();
        readonly DebugHapticOutput _haptics = new DebugHapticOutput();

        public IControllerSource Controller => _controller;
        public IHapticOutput Haptics => _haptics;

        /// <summary>プレビューの表示用。</summary>
        public DebugHapticOutput DebugHaptics => _haptics;

        public bool IsDashboardVisible => false;

        public event Action QuitRequested
        {
            add { }
            remove { }
        }

        public void BeginFrame()
        {
        }

        public void OnOpenChanged(bool isOpen)
        {
        }

        public void Dispose()
        {
        }
    }
}
