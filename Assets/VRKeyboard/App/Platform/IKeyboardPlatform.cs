using System;
using VRKeyboard.App.Input;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.Platform
{
    /// <summary>
    /// 実行環境（OpenVR 実機、PC のキーボードによる開発用）への接続。
    /// 入力元・振動の出力先に加えて、環境からのイベント・ダッシュボードと設定画面の状態・開閉に伴う表示と入力の切り替えをまとめる。
    /// <see cref="KeyboardApp"/> は環境の違いによる分岐を持たず、この境界だけを通す。
    /// </summary>
    public interface IKeyboardPlatform : IDisposable
    {
        IControllerSource Controller { get; }
        IHapticOutput Haptics { get; }

        /// <summary>
        /// 環境がアプリの終了を求めた（SteamVR の終了など）。
        /// <see cref="BeginFrame"/> の中で発生する。受けたあとは環境の API を呼ばず、終了の処理に移ること。
        /// </summary>
        event Action QuitRequested;

        /// <summary>
        /// SteamVR のダッシュボードが入力を持っているか（表示中と、閉じた後に入力がまだアプリへ戻っていない間）。
        /// true の間、キーボードは一時停止する（SPEC 4.4）。<see cref="BeginFrame"/> で更新される。
        /// </summary>
        bool IsDashboardHoldingInput { get; }

        /// <summary>設定画面（ダッシュボードの設定タブ）が表示されているか。<see cref="BeginFrame"/> で更新される。</summary>
        bool IsSettingsVisible { get; }

        /// <summary>
        /// 入力を読むのに、本アプリのウィンドウが前面である必要があるか（PC のキーボードで入力する開発用の環境）。
        /// true のときは、起動時に VRChat を前面にしない（SPEC 8.1）。
        /// </summary>
        bool RequiresOwnWindowFocus { get; }

        /// <summary>毎フレームの最初に呼ぶ。環境のイベントを処理し、状態を読み取る。</summary>
        void BeginFrame();

        /// <summary>キーボードの開閉が変わった。オーバーレイの表示と入力の優先度を切り替える。</summary>
        void OnOpenChanged(bool isOpen);

        /// <summary>
        /// 位置の確認用にキーボードを表示するか。true の間は、キーボードを閉じていてもオーバーレイを表示する（入力は占有しない）。
        /// 毎フレーム <see cref="BeginFrame"/> の後に呼ぶ。
        /// </summary>
        void SetPlacementPreviewVisible(bool visible);

        /// <summary>キーボードのオーバーレイの位置・傾き・大きさを変える。</summary>
        void SetPlacement(KeyboardPlacement placement);
    }
}
