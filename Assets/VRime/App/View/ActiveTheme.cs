using System;

namespace VRime.App.View
{
    /// <summary>
    /// 表示に使っているテーマ。設定でテーマを変えると <see cref="Set"/> で差し替わる。
    /// ビューはこれを共有して保持し、描くたびに <see cref="Current"/> を読む。<see cref="Version"/> の変化で、描き直しが必要なことを知る。
    /// </summary>
    public sealed class ActiveTheme
    {
        public ActiveTheme(KeyboardTheme initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            Current = initial;
        }

        public KeyboardTheme Current { get; private set; }

        /// <summary>テーマが差し替わるたびに増える。</summary>
        public int Version { get; private set; }

        public void Set(KeyboardTheme theme)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (theme == Current) return;
            Current = theme;
            Version++;
        }
    }
}
