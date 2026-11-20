using System;
using System.Collections.Generic;
using Valve.VR;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// アクションマニフェスト（<c>actions.json</c>）のアクションセットとアクションのハンドル。
    /// アクションは物理的なボタンではなく役割ごとに定義し、ボタンとの対応は SteamVR のバインドに任せる（docs/spec/grip-input.md「コントローラーの割り当て」）。
    /// ハンドルは接続（<see cref="OpenVRRuntime"/>）ごとに取り直す。
    /// </summary>
    public sealed class OpenVRActions
    {
        /// <summary>アクションセットは 1つだけ使う（ARCHITECTURE.md「入力の横取り」）。</summary>
        public const string ActionSetName = "/actions/keyboard";

        public const string LeftOpenCloseName = ActionSetName + "/in/openclose_left";
        public const string RightOpenCloseName = ActionSetName + "/in/openclose_right";
        public const string LeftPanelName = ActionSetName + "/in/panel_left";
        public const string RightPanelName = ActionSetName + "/in/panel_right";
        public const string LeftSelectName = ActionSetName + "/in/select_left";
        public const string RightSelectName = ActionSetName + "/in/select_right";
        public const string LeftPressName = ActionSetName + "/in/press_left";
        public const string RightPressName = ActionSetName + "/in/press_right";
        public const string LeftHapticName = ActionSetName + "/out/haptic_left";
        public const string RightHapticName = ActionSetName + "/out/haptic_right";

        /// <summary>
        /// このアプリが読むアクションのすべて。マニフェストの <c>actions</c> と過不足なく一致させる。
        /// 一致と、同梱の既定のバインドがすべてのアクションを割り当てていることは <c>ActionManifestTests</c> が検査する。
        /// </summary>
        public static IReadOnlyList<string> Names { get; } = new[]
        {
            LeftOpenCloseName, RightOpenCloseName,
            LeftPanelName, RightPanelName,
            LeftSelectName, RightSelectName,
            LeftPressName, RightPressName,
            LeftHapticName, RightHapticName,
        };

        public ulong ActionSet { get; }
        public ulong LeftOpenClose { get; }
        public ulong RightOpenClose { get; }
        public ulong LeftPanel { get; }
        public ulong RightPanel { get; }
        public ulong LeftSelect { get; }
        public ulong RightSelect { get; }
        public ulong LeftPress { get; }
        public ulong RightPress { get; }
        public ulong LeftHaptic { get; }
        public ulong RightHaptic { get; }

        /// <summary>マニフェストに存在しない名前は例外にする。</summary>
        public OpenVRActions()
        {
            ulong set = 0;
            OpenVRErrors.Check(OpenVR.Input.GetActionSetHandle(ActionSetName, ref set), "GetActionSetHandle(" + ActionSetName + ")");
            ActionSet = RequireValid(set, ActionSetName);
            LeftOpenClose = Get(LeftOpenCloseName);
            RightOpenClose = Get(RightOpenCloseName);
            LeftPanel = Get(LeftPanelName);
            RightPanel = Get(RightPanelName);
            LeftSelect = Get(LeftSelectName);
            RightSelect = Get(RightSelectName);
            LeftPress = Get(LeftPressName);
            RightPress = Get(RightPressName);
            LeftHaptic = Get(LeftHapticName);
            RightHaptic = Get(RightHapticName);
        }

        static ulong Get(string name)
        {
            ulong handle = 0;
            OpenVRErrors.Check(OpenVR.Input.GetActionHandle(name, ref handle), "GetActionHandle(" + name + ")");
            return RequireValid(handle, name);
        }

        static ulong RequireValid(ulong handle, string name)
        {
            if (handle == OpenVR.k_ulInvalidInputValueHandle)
                throw new InvalidOperationException(name + " のハンドルが無効です。");
            return handle;
        }
    }
}
