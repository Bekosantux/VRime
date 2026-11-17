using System;

namespace VRime.App.Settings
{
    /// <summary>
    /// 数値の設定項目がとりうる範囲と刻み。<see cref="Min"/> から <see cref="Step"/> ずつ <see cref="Max"/> まで並ぶ値を、設定画面で選べる値とする。
    /// 保存された値の検査は範囲だけで行い、刻みには揃っていなくてもよい（設定ファイルを手で書き換えた場合など）。
    /// </summary>
    public readonly struct SettingRange
    {
        readonly int _decimals;

        public SettingRange(float min, float max, float step)
        {
            if (float.IsNaN(min) || float.IsInfinity(min)) throw new ArgumentOutOfRangeException(nameof(min), min, null);
            if (!(max > min) || float.IsInfinity(max)) throw new ArgumentOutOfRangeException(nameof(max), max, "最小値より大きい有限の値である必要があります。");
            if (!(step > 0f) || float.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(step), step, "正の有限の値である必要があります。");
            double count = (max - min) / step;
            if (Math.Abs(count - Math.Round(count)) > 1e-4)
                throw new ArgumentException("最小値から最大値までが刻みで割り切れません。", nameof(step));

            Min = min;
            Max = max;
            Step = step;
            _decimals = DecimalsOf(step);
        }

        public float Min { get; }
        public float Max { get; }
        public float Step { get; }

        public bool Contains(float value)
        {
            return value >= Min && value <= Max;
        }

        /// <summary>範囲に収め、最も近い刻みの値にする。浮動小数点の誤差は刻みの桁数で丸める（保存したときに 0.26000002 などにならないため）。</summary>
        public float Snap(float value)
        {
            if (float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
            double clamped = Math.Max(Min, Math.Min(Max, (double)value));
            double steps = Math.Round((clamped - Min) / Step, MidpointRounding.AwayFromZero);
            double snapped = Math.Round(Min + steps * Step, _decimals, MidpointRounding.AwayFromZero);
            return (float)Math.Max(Min, Math.Min(Max, snapped));
        }

        /// <summary>value を刻み steps 個分ずらす（範囲の端で止まる）。</summary>
        public float Offset(float value, int steps)
        {
            return Snap(value + steps * Step);
        }

        /// <summary>範囲の中での位置（最小値で 0、最大値で 1）。</summary>
        public float FractionOf(float value)
        {
            float fraction = (value - Min) / (Max - Min);
            return Math.Max(0f, Math.Min(1f, fraction));
        }

        /// <summary>範囲の中での位置（0..1。外側は端に収める）から、刻みの値を求める。</summary>
        public float FromFraction(float fraction)
        {
            if (float.IsNaN(fraction)) throw new ArgumentOutOfRangeException(nameof(fraction), fraction, null);
            float clamped = Math.Max(0f, Math.Min(1f, fraction));
            return Snap(Min + clamped * (Max - Min));
        }

        // 刻みを表すのに必要な小数の桁数（0.01 → 2、0.05 → 2、1 → 0）。
        static int DecimalsOf(float step)
        {
            for (int decimals = 0; decimals < 7; decimals++)
            {
                double scaled = step * Math.Pow(10, decimals);
                if (Math.Abs(scaled - Math.Round(scaled)) < 1e-4) return decimals;
            }
            return 7;
        }
    }
}
