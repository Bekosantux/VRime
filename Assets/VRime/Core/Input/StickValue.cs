namespace VRime.Core.Input
{
    /// <summary>スティックの傾き。X は右が正、Y は上が正（OpenVR の向き）。</summary>
    public readonly struct StickValue
    {
        public StickValue(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public static StickValue Zero => new StickValue(0f, 0f);
    }
}
