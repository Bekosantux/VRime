using UnityEngine;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.Input
{
    /// <summary>
    /// PC のキーボードでコントローラーを模擬する開発用の入力元（旧 Input Manager を使う）。
    /// 左手: W/A/S/D がスティック、Q がトリガー、E がグリップ。右手: I/J/K/L、U、O。
    /// Space は両手のグリップの同時押し。スティックは押している間だけ大きさ 1.0 で倒し、トリガーは押している間だけ引き切る（1.0）。
    /// グリップは既定のバインドと同じく、展開・収納とパネルの切り替えの両方を押す。
    /// </summary>
    public sealed class DebugKeyboardControllerSource : IControllerSource
    {
        public ControllerFrame Read()
        {
            bool bothGrips = UnityEngine.Input.GetKey(KeyCode.Space);
            return new ControllerFrame(
                ReadHand(KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.Q, bothGrips || UnityEngine.Input.GetKey(KeyCode.E)),
                ReadHand(KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K, KeyCode.U, bothGrips || UnityEngine.Input.GetKey(KeyCode.O)));
        }

        static HandState ReadHand(KeyCode left, KeyCode right, KeyCode up, KeyCode down, KeyCode trigger, bool grip)
        {
            return new HandState(Stick(left, right, up, down), Trigger(trigger), grip, grip);
        }

        static float Trigger(KeyCode key)
        {
            return UnityEngine.Input.GetKey(key) ? 1f : 0f;
        }

        static StickValue Stick(KeyCode left, KeyCode right, KeyCode up, KeyCode down)
        {
            float x = (UnityEngine.Input.GetKey(right) ? 1f : 0f) - (UnityEngine.Input.GetKey(left) ? 1f : 0f);
            float y = (UnityEngine.Input.GetKey(up) ? 1f : 0f) - (UnityEngine.Input.GetKey(down) ? 1f : 0f);
            if (x == 0f && y == 0f) return StickValue.Zero;
            // 斜めでも大きさを 1.0 にそろえる。
            float length = Mathf.Sqrt(x * x + y * y);
            return new StickValue(x / length, y / length);
        }
    }
}
