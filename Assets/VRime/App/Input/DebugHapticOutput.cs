using VRime.Core;
using VRime.Core.Input;

namespace VRime.App.Input
{
    /// <summary>
    /// 振動を実際には出さず、直近の振動の手・種類・時刻を保持する開発用の出力先。
    /// エディタのプレビューが短く表示するために読む。
    /// </summary>
    public sealed class DebugHapticOutput : IHapticOutput
    {
        readonly HapticKind[] _kinds = new HapticKind[2];
        readonly double[] _times = { double.NegativeInfinity, double.NegativeInfinity };

        public void Play(Hand hand, HapticKind kind)
        {
            int index = Index(hand);
            _kinds[index] = kind;
            _times[index] = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>直近の振動が withinSeconds 秒以内であれば、その種類を返す。</summary>
        public bool TryGetRecent(Hand hand, double withinSeconds, out HapticKind kind)
        {
            int index = Index(hand);
            kind = _kinds[index];
            return UnityEngine.Time.realtimeSinceStartupAsDouble - _times[index] <= withinSeconds;
        }

        static int Index(Hand hand)
        {
            switch (hand)
            {
                case Hand.Left: return 0;
                case Hand.Right: return 1;
                default: throw new System.ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }
    }
}
