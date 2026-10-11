namespace VRKeyboard.Core.Input
{
    /// <summary>
    /// 毎フレームの状態から押下エッジを求める。入力を受け付けない間も毎フレーム呼び、
    /// 状態が切り替わった直後に古い押下状態で誤ったエッジが出ないようにする。
    /// トリガーは引き量をしきい値で判定する。押している間は離した判定のしきい値（押した判定より小さい）で判定し、チャタリングを防ぐ。
    /// </summary>
    public sealed class EdgeTracker
    {
        bool _leftTrigger;
        bool _rightTrigger;
        bool _leftGrip;
        bool _rightGrip;

        /// <param name="triggerPress">引き量がこの値以上で押したとみなす。</param>
        /// <param name="triggerRelease">押している間、引き量がこの値未満になると離したとみなす。</param>
        public FrameEdges Update(ControllerFrame frame, float triggerPress, float triggerRelease)
        {
            bool leftTrigger = IsTriggerHeld(frame.Left.TriggerPull, _leftTrigger, triggerPress, triggerRelease);
            bool rightTrigger = IsTriggerHeld(frame.Right.TriggerPull, _rightTrigger, triggerPress, triggerRelease);
            var edges = new FrameEdges(
                leftTrigger && !_leftTrigger,
                rightTrigger && !_rightTrigger,
                frame.Left.Grip && !_leftGrip,
                frame.Right.Grip && !_rightGrip);
            _leftTrigger = leftTrigger;
            _rightTrigger = rightTrigger;
            _leftGrip = frame.Left.Grip;
            _rightGrip = frame.Right.Grip;
            return edges;
        }

        static bool IsTriggerHeld(float pull, bool wasHeld, float press, float release)
        {
            return wasHeld ? pull >= release : pull >= press;
        }
    }
}
