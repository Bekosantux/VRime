using VRime.Core.Input;

namespace VRime.App.Input
{
    /// <summary>
    /// コントローラーの入力元。毎フレーム <see cref="Read"/> でそのフレームの両手の状態を返す。
    /// 実機（OpenVR）とデバッグ用（PC のキーボード）を差し替えられるようにするための境界。
    /// </summary>
    public interface IControllerSource
    {
        ControllerFrame Read();
    }
}
