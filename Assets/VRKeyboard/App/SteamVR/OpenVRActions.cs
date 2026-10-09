using System;
using Valve.VR;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// アクションマニフェスト（<c>actions.json</c>）のアクションセットとアクションのハンドル。
    /// ハンドルは接続（<see cref="OpenVRRuntime"/>）ごとに取り直す。
    /// </summary>
    public sealed class OpenVRActions
    {
        /// <summary>アクションセットは 1つだけ使う（SPEC 4.3）。</summary>
        public const string ActionSetName = "/actions/keyboard";

        public ulong ActionSet { get; }
        public ulong LeftStick { get; }
        public ulong RightStick { get; }
        public ulong LeftTrigger { get; }
        public ulong RightTrigger { get; }
        public ulong LeftGrip { get; }
        public ulong RightGrip { get; }
        public ulong LeftHaptic { get; }
        public ulong RightHaptic { get; }

        /// <summary>マニフェストに存在しない名前は例外にする。</summary>
        public OpenVRActions()
        {
            ulong set = 0;
            OpenVRErrors.Check(OpenVR.Input.GetActionSetHandle(ActionSetName, ref set), "GetActionSetHandle(" + ActionSetName + ")");
            ActionSet = RequireValid(set, ActionSetName);
            LeftStick = Get("/actions/keyboard/in/leftstick");
            RightStick = Get("/actions/keyboard/in/rightstick");
            LeftTrigger = Get("/actions/keyboard/in/lefttrigger");
            RightTrigger = Get("/actions/keyboard/in/righttrigger");
            LeftGrip = Get("/actions/keyboard/in/leftgrip");
            RightGrip = Get("/actions/keyboard/in/rightgrip");
            LeftHaptic = Get("/actions/keyboard/out/hapticleft");
            RightHaptic = Get("/actions/keyboard/out/hapticright");
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
