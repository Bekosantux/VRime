namespace VRKeyboard.Core.Input
{
    /// <summary>
    /// 毎フレームの状態から押下エッジを求める。入力を受け付けない間も毎フレーム呼び、
    /// 状態が切り替わった直後に古い押下状態で誤ったエッジが出ないようにする。
    /// </summary>
    public sealed class EdgeTracker
    {
        bool _leftTrigger;
        bool _rightTrigger;
        bool _leftGrip;
        bool _rightGrip;

        public FrameEdges Update(ControllerFrame frame)
        {
            var edges = new FrameEdges(
                frame.Left.Trigger && !_leftTrigger,
                frame.Right.Trigger && !_rightTrigger,
                frame.Left.Grip && !_leftGrip,
                frame.Right.Grip && !_rightGrip);
            _leftTrigger = frame.Left.Trigger;
            _rightTrigger = frame.Right.Trigger;
            _leftGrip = frame.Left.Grip;
            _rightGrip = frame.Right.Grip;
            return edges;
        }
    }
}
