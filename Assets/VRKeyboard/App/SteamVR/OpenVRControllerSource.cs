using System;
using System.Runtime.InteropServices;
using Valve.VR;
using VRKeyboard.App.Input;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// SteamVR Input（アクションマニフェスト）から両手のスティック・トリガー・グリップを読む入力元。
    /// アクションセットは 1つだけで、優先度を <see cref="IsExclusive"/> で切り替える。
    /// </summary>
    public sealed class OpenVRControllerSource : IControllerSource
    {
        static readonly uint ActionSetSize = (uint)Marshal.SizeOf<VRActiveActionSet_t>();
        static readonly uint AnalogDataSize = (uint)Marshal.SizeOf<InputAnalogActionData_t>();
        static readonly uint DigitalDataSize = (uint)Marshal.SizeOf<InputDigitalActionData_t>();

        readonly OpenVRActions _actions;
        readonly VRActiveActionSet_t[] _activeSets;

        public OpenVRControllerSource(OpenVRActions actions)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _activeSets = new[]
            {
                new VRActiveActionSet_t
                {
                    ulActionSet = actions.ActionSet,
                    ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
                    nPriority = 0,
                },
            };
        }

        /// <summary>
        /// true の間は、ゲームなど他のアプリへコントローラー入力を渡さない（優先度 <c>k_nActionSetOverlayGlobalPriorityMin</c>）。
        /// false の間は優先度 0 で、他のアプリと入力を共有する（開閉の操作だけを監視する）。
        /// キーボードを開くとき true、閉じるとき false にする。開いたまま false に戻してはならない
        /// （試作で、優先度だけを下げると再起動するまで入力が取得できなくなった。SPEC 4.3）。
        /// </summary>
        public bool IsExclusive
        {
            get => _activeSets[0].nPriority != 0;
            set => _activeSets[0].nPriority = value ? OpenVR.k_nActionSetOverlayGlobalPriorityMin : 0;
        }

        public ControllerFrame Read()
        {
            OpenVRErrors.Check(OpenVR.Input.UpdateActionState(_activeSets, ActionSetSize), "UpdateActionState");
            return new ControllerFrame(
                new HandState(Analog(_actions.LeftStick, "leftstick"), Digital(_actions.LeftTrigger, "lefttrigger"), Digital(_actions.LeftGrip, "leftgrip")),
                new HandState(Analog(_actions.RightStick, "rightstick"), Digital(_actions.RightTrigger, "righttrigger"), Digital(_actions.RightGrip, "rightgrip")));
        }

        // bActive が false のアクション（バインドなし・他のアプリが占有している間）は、0 / 押されていない として扱う。
        static StickValue Analog(ulong action, string name)
        {
            var data = new InputAnalogActionData_t();
            EVRInputError error = OpenVR.Input.GetAnalogActionData(action, ref data, AnalogDataSize, OpenVR.k_ulInvalidInputValueHandle);
            if (!OpenVRErrors.CheckAllowingNoData(error, "GetAnalogActionData(" + name + ")") || !data.bActive) return StickValue.Zero;
            return new StickValue(data.x, data.y);
        }

        static bool Digital(ulong action, string name)
        {
            var data = new InputDigitalActionData_t();
            EVRInputError error = OpenVR.Input.GetDigitalActionData(action, ref data, DigitalDataSize, OpenVR.k_ulInvalidInputValueHandle);
            if (!OpenVRErrors.CheckAllowingNoData(error, "GetDigitalActionData(" + name + ")")) return false;
            return data.bActive && data.bState;
        }
    }
}
