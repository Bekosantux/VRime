namespace VRKeyboard.Core.Input
{
    /// <summary>
    /// 毎フレームの状態から押下エッジを求める。入力を受け付けない間も毎フレーム呼び、
    /// 状態が切り替わった直後に古い押下状態で誤ったエッジが出ないようにする。
    /// トリガーは引き量がしきい値以上で押したとみなす。
    /// </summary>
    public sealed class EdgeTracker
    {
        bool _leftTrigger;
        bool _rightTrigger;
        bool _leftPanel;
        bool _rightPanel;

        /// <param name="triggerThreshold">引き量がこの値以上で押したとみなす。</param>
        public FrameEdges Update(ControllerFrame frame, float triggerThreshold)
        {
            bool leftTrigger = frame.Left.TriggerPull >= triggerThreshold;
            bool rightTrigger = frame.Right.TriggerPull >= triggerThreshold;
            var edges = new FrameEdges(
                leftTrigger && !_leftTrigger,
                rightTrigger && !_rightTrigger,
                frame.Left.Panel && !_leftPanel,
                frame.Right.Panel && !_rightPanel);
            _leftTrigger = leftTrigger;
            _rightTrigger = rightTrigger;
            _leftPanel = frame.Left.Panel;
            _rightPanel = frame.Right.Panel;
            return edges;
        }
    }
}
