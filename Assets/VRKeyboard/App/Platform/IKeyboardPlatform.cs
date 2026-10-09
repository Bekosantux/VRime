using System;
using VRKeyboard.App.Input;

namespace VRKeyboard.App.Platform
{
    /// <summary>
    /// 実行環境（OpenVR 実機、PC のキーボードによる開発用）への接続。
    /// 入力元・振動の出力先に加えて、環境からのイベント・ダッシュボードの状態・開閉に伴う表示と入力の切り替えをまとめる。
    /// <see cref="KeyboardApp"/> は環境の違いを知らず、この境界だけを通す。
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

        /// <summary>SteamVR のダッシュボードが表示されているか。<see cref="BeginFrame"/> で更新される。</summary>
        bool IsDashboardVisible { get; }

        /// <summary>毎フレームの最初に呼ぶ。環境のイベントの処理と、状態の読み取りを行う。</summary>
        void BeginFrame();

        /// <summary>キーボードの開閉が変わった。オーバーレイの表示切り替えと入力の優先度の切り替えを行う。</summary>
        void OnOpenChanged(bool isOpen);
    }
}
