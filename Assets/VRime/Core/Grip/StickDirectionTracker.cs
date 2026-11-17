using VRime.Core.Input;

namespace VRime.Core.Grip
{
    /// <summary>
    /// 4方向分類の状態を持ち、中央から方向に倒した瞬間を検出する。
    /// 変換中の候補欄の操作（1回倒すごとに1つ動かす）に使う。
    /// </summary>
    public sealed class StickDirectionTracker
    {
        public StickDirection Current { get; private set; } = StickDirection.Center;

        /// <summary>
        /// 分類を更新する。中央から方向に倒したフレームではその方向を、それ以外は Center を返す。
        /// </summary>
        public StickDirection Update(StickValue stick, float threshold)
        {
            StickDirection previous = Current;
            Current = StickClassifier.ClassifyDirection(stick, threshold);
            return previous == StickDirection.Center ? Current : StickDirection.Center;
        }
    }
}
