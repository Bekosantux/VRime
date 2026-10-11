using UnityEngine;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面へのポインター（SteamVR のダッシュボードのレーザー、開発用のプレビューのマウス）の入力。
    /// 座標は設定画面のテクスチャのピクセルで、左上を原点とし、下向きを正とする。
    /// </summary>
    public interface ISettingsPointer
    {
        void PointerMove(Vector2 position);
        void PointerDown(Vector2 position);
        void PointerUp(Vector2 position);

        /// <summary>ポインターが画面から外れた（ダッシュボードが閉じた場合を含む）。操作の途中であれば、そこで終える。</summary>
        void PointerLeave();
    }
}
