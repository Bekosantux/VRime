namespace VRime.App.Settings
{
    /// <summary>個人情報に関わる設定（SPEC 6.2・8.1）。</summary>
    public sealed class PrivacySettings
    {
        /// <summary>同意を得るまで、外部へは何も送らない。</summary>
        public static readonly PrivacySettings Default = new PrivacySettings(useGoogleConversion: false);

        public PrivacySettings(bool useGoogleConversion)
        {
            UseGoogleConversion = useGoogleConversion;
        }

        /// <summary>
        /// 変換に Google の API を使う（変換を押したときの読みが Google に送信される）。
        /// 設定画面でオンにするには同意が必要。オフの間は変換を使えない。
        /// </summary>
        public bool UseGoogleConversion { get; }

        public PrivacySettings WithUseGoogleConversion(bool value) => new PrivacySettings(value);

        public bool Equals(PrivacySettings other) => other != null && UseGoogleConversion == other.UseGoogleConversion;

        public override bool Equals(object obj) => Equals(obj as PrivacySettings);

        public override int GetHashCode() => UseGoogleConversion ? 1 : 0;
    }
}
