using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.Input
{
    /// <summary>コントローラーの振動の出力先。強さや長さへの変換は実装が行う。</summary>
    public interface IHapticOutput
    {
        void Play(Hand hand, HapticKind kind);
    }
}
