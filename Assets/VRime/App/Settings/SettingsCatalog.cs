using System;
using System.Collections.Generic;
using System.Globalization;

namespace VRime.App.Settings
{
    /// <summary>
    /// 設定画面の項目。画面の行（<see cref="View.SettingsRow"/> / <see cref="View.SettingsToggleRow"/> / <see cref="View.SettingsDropdownRow"/>）とは <see cref="Key"/> で対応させる。
    /// 項目名などの見た目はシーンに置く。
    /// </summary>
    public abstract class SettingItem
    {
        protected SettingItem(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("キーが空です。", nameof(key));
            Key = key;
        }

        /// <summary>項目を識別するキー。設定ファイルに保存する項目では、設定ファイルの値の名前（docs/spec/settings.md「設定ファイル」）と同じ。</summary>
        public string Key { get; }
    }

    /// <summary>数値の項目。［−］・スライダー・［＋］で変える。</summary>
    public sealed class NumberSettingItem : SettingItem
    {
        readonly Func<UserSettings, float> _get;
        readonly Func<UserSettings, float, UserSettings> _set;
        readonly Func<float, string> _format;

        public NumberSettingItem(string key, SettingRange range, Func<UserSettings, float> get,
            Func<UserSettings, float, UserSettings> set, Func<float, string> format) : base(key)
        {
            Range = range;
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            _format = format ?? throw new ArgumentNullException(nameof(format));
        }

        public SettingRange Range { get; }

        public float Get(UserSettings settings) => _get(settings);

        /// <summary>value を刻みに揃えてから設定する。</summary>
        public UserSettings Set(UserSettings settings, float value) => _set(settings, Range.Snap(value));

        public string Format(float value) => _format(value);

        /// <summary>この項目だけを <see cref="UserSettings.Default"/> の値に戻す。既定値は刻みに揃えずにそのまま使う。</summary>
        public UserSettings ResetToDefault(UserSettings settings) => _set(settings, _get(UserSettings.Default));
    }

    /// <summary>
    /// オン・オフの項目（スイッチ）。値を設定ファイルに保存する <see cref="ToggleSettingItem"/> と、
    /// 値をアプリの外が持つ <see cref="ExternalToggleSettingItem"/> がある。画面の行（<see cref="View.SettingsToggleRow"/>）はどちらにも使う。
    /// </summary>
    public abstract class SwitchSettingItem : SettingItem
    {
        private protected SwitchSettingItem(string key) : base(key)
        {
        }
    }

    /// <summary>設定ファイルに保存するオン・オフの項目。押すたびに切り替える。</summary>
    public sealed class ToggleSettingItem : SwitchSettingItem
    {
        readonly Func<UserSettings, bool> _get;
        readonly Func<UserSettings, bool, UserSettings> _set;

        /// <param name="requiresConsentToEnable">オンにする前に同意の画面で確認するか。オフにするときは確認しない。</param>
        public ToggleSettingItem(string key, Func<UserSettings, bool> get, Func<UserSettings, bool, UserSettings> set,
            bool requiresConsentToEnable = false) : base(key)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            RequiresConsentToEnable = requiresConsentToEnable;
        }

        /// <summary>オンにする前に同意の画面で確認するか（<see cref="SettingsForm"/>）。</summary>
        public bool RequiresConsentToEnable { get; }

        public bool Get(UserSettings settings) => _get(settings);

        public UserSettings Set(UserSettings settings, bool value) => _set(settings, value);

        public UserSettings Toggle(UserSettings settings) => _set(settings, !_get(settings));

        /// <summary>この項目だけを <see cref="UserSettings.Default"/> の値に戻す。</summary>
        public UserSettings ResetToDefault(UserSettings settings) => _set(settings, _get(UserSettings.Default));
    }

    /// <summary>
    /// 値をアプリの外（SteamVR など）が持つオン・オフの項目。設定ファイルには保存しない。
    /// 値の読み書きは <see cref="SettingsForm"/> を通してアプリが行う（<see cref="SettingsForm.SetExternalValue"/> / <see cref="SettingsForm.ExternalChangeRequested"/>）。
    /// 外の値はアプリの外でも変わりうるため、アプリは表示の前に再取得して渡す。
    /// </summary>
    public sealed class ExternalToggleSettingItem : SwitchSettingItem
    {
        /// <param name="defaultValue">「既定に戻す」で設定する値。</param>
        public ExternalToggleSettingItem(string key, bool defaultValue) : base(key)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>「既定に戻す」で設定する値。</summary>
        public bool DefaultValue { get; }
    }

    /// <summary>
    /// 選択肢から 1 つを選ぶ項目。ドロップダウンの一覧から選ぶ。
    /// 選択肢の表示名は、値の表示（<see cref="NumberSettingItem.Format"/>）と同じく画面に出す文言で、一覧はこの順に並べる。
    /// </summary>
    public sealed class ChoiceSettingItem : SettingItem
    {
        readonly Func<UserSettings, int> _getIndex;
        readonly Func<UserSettings, int, UserSettings> _setIndex;

        ChoiceSettingItem(string key, IReadOnlyList<string> labels, Func<UserSettings, int> getIndex,
            Func<UserSettings, int, UserSettings> setIndex) : base(key)
        {
            Labels = labels;
            _getIndex = getIndex;
            _setIndex = setIndex;
        }

        /// <param name="choices">選択肢の値と表示名。2 つ以上で、値と表示名はどちらも重複しない。</param>
        /// <param name="get">設定の値。choices にない値を返すと、読み出しが例外になる。</param>
        public static ChoiceSettingItem Create<T>(string key, IReadOnlyList<(T value, string label)> choices,
            Func<UserSettings, T> get, Func<UserSettings, T, UserSettings> set)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (choices.Count < 2) throw new ArgumentException("選択肢が 2 つ未満です: " + key, nameof(choices));

            var values = new T[choices.Count];
            var labels = new string[choices.Count];
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < choices.Count; i++)
            {
                (T value, string label) = choices[i];
                if (string.IsNullOrEmpty(label)) throw new ArgumentException("表示名が空の選択肢があります: " + key, nameof(choices));
                for (int j = 0; j < i; j++)
                {
                    if (comparer.Equals(values[j], value)) throw new ArgumentException("選択肢の値が重複しています: " + key + " = " + value, nameof(choices));
                    if (labels[j] == label) throw new ArgumentException("選択肢の表示名が重複しています: " + key + " = " + label, nameof(choices));
                }
                values[i] = value;
                labels[i] = label;
            }

            int IndexOf(T value)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (comparer.Equals(values[i], value)) return i;
                }
                throw new InvalidOperationException(key + " の値が選択肢にありません: " + value);
            }

            return new ChoiceSettingItem(key, Array.AsReadOnly(labels), s => IndexOf(get(s)), (s, i) => set(s, values[i]));
        }

        /// <summary>選択肢の表示名。一覧の順。</summary>
        public IReadOnlyList<string> Labels { get; }

        /// <summary>settings の値が何番目の選択肢か。</summary>
        public int GetIndex(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return _getIndex(settings);
        }

        /// <summary>index 番目の選択肢の値にする。</summary>
        public UserSettings SetIndex(UserSettings settings, int index)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (index < 0 || index >= Labels.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, Key + " の選択肢は " + Labels.Count + " 個です。");
            return _setIndex(settings, index);
        }

        /// <summary>この項目だけを <see cref="UserSettings.Default"/> の値に戻す。</summary>
        public UserSettings ResetToDefault(UserSettings settings) => SetIndex(settings, GetIndex(UserSettings.Default));
    }

    /// <summary>設定画面のタブ 1 つ分の項目のまとまり。「既定に戻す」はカテゴリごとに作用する。</summary>
    public sealed class SettingsCategory
    {
        /// <param name="showsKeyboardPreview">このカテゴリを表示している間、位置の確認用にキーボードのオーバーレイを表示するか。</param>
        public SettingsCategory(string key, IReadOnlyList<SettingItem> items, bool showsKeyboardPreview = false)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("キーが空です。", nameof(key));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("項目がありません: " + key, nameof(items));
            var keys = new HashSet<string>();
            foreach (SettingItem item in items)
            {
                if (item == null) throw new ArgumentException("null の項目があります: " + key, nameof(items));
                if (!keys.Add(item.Key)) throw new ArgumentException("項目が重複しています: " + item.Key, nameof(items));
            }
            Key = key;
            Items = items;
            ShowsKeyboardPreview = showsKeyboardPreview;
        }

        /// <summary>カテゴリを識別するキー。画面のページ（<see cref="View.SettingsPage"/>）と対応させる。</summary>
        public string Key { get; }

        /// <summary>このカテゴリを表示している間、位置の確認用にキーボードのオーバーレイを表示するか。</summary>
        public bool ShowsKeyboardPreview { get; }

        public IReadOnlyList<SettingItem> Items { get; }

        public bool Contains(SettingItem item)
        {
            foreach (SettingItem candidate in Items)
            {
                if (candidate == item) return true;
            }
            return false;
        }

        /// <summary>
        /// このカテゴリの、設定ファイルに保存する項目だけを既定値に戻す。
        /// 値をアプリの外が持つ項目（<see cref="ExternalToggleSettingItem"/>）は戻さない（<see cref="SettingsForm"/> が外へ変更を求める）。
        /// </summary>
        public UserSettings ResetToDefault(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            foreach (SettingItem item in Items)
            {
                switch (item)
                {
                    case NumberSettingItem number:
                        settings = number.ResetToDefault(settings);
                        break;
                    case ToggleSettingItem toggle:
                        settings = toggle.ResetToDefault(settings);
                        break;
                    case ChoiceSettingItem choice:
                        settings = choice.ResetToDefault(settings);
                        break;
                    case ExternalToggleSettingItem _:
                        break;
                    default:
                        throw new InvalidOperationException("未知の種類の項目です: " + item.Key);
                }
            }
            return settings;
        }
    }

    /// <summary>設定画面に並べるカテゴリと項目（docs/spec/settings.md「項目」）。</summary>
    public static class SettingsCatalog
    {
        /// <summary>SteamVR の起動時に自動で起動するか（docs/spec/lifecycle.md「SteamVR への登録と自動起動」）。値は SteamVR が持つ。</summary>
        public static readonly ExternalToggleSettingItem AutoLaunch = new ExternalToggleSettingItem("autoLaunch", defaultValue: false);

        // AutoLaunch より後に初期化する（静的フィールドは宣言の順に初期化される）。
        public static readonly IReadOnlyList<SettingsCategory> Categories = new[]
        {
            new SettingsCategory("display", new SettingItem[]
            {
                ChoiceSettingItem.Create("theme", new[] { (ColorTheme.Lime, "ライム"), (ColorTheme.Dark, "ダーク") },
                    s => s.Theme, (s, v) => s.WithTheme(v)),
                new NumberSettingItem("downMeters", KeyboardPlacement.DownRange,
                    s => s.Placement.DownMeters, (s, v) => s.WithPlacement(s.Placement.WithDown(v)), Centimeters),
                new NumberSettingItem("forwardMeters", KeyboardPlacement.ForwardRange,
                    s => s.Placement.ForwardMeters, (s, v) => s.WithPlacement(s.Placement.WithForward(v)), Centimeters),
                new NumberSettingItem("rightMeters", KeyboardPlacement.RightRange,
                    s => s.Placement.RightMeters, (s, v) => s.WithPlacement(s.Placement.WithRight(v)), Sideways),
                new NumberSettingItem("tiltDegrees", KeyboardPlacement.TiltRange,
                    s => s.Placement.TiltDegrees, (s, v) => s.WithPlacement(s.Placement.WithTilt(v)), Degrees),
                new NumberSettingItem("widthMeters", KeyboardPlacement.WidthRange,
                    s => s.Placement.WidthMeters, (s, v) => s.WithPlacement(s.Placement.WithWidth(v)), Centimeters),
            }, showsKeyboardPreview: true),
            new SettingsCategory("input", new SettingItem[]
            {
                new NumberSettingItem("triggerThreshold", UserSettings.TriggerThresholdRange,
                    s => s.TriggerThreshold, (s, v) => s.WithTriggerThreshold(v), Percent),
                new NumberSettingItem("stickThreshold", UserSettings.StickThresholdRange,
                    s => s.StickThreshold, (s, v) => s.WithStickThreshold(v), Percent),
                new NumberSettingItem("repeatDelaySeconds", UserSettings.RepeatDelaySecondsRange,
                    s => s.RepeatDelaySeconds, (s, v) => s.WithRepeatDelaySeconds(v), Seconds),
            }),
            new SettingsCategory("integration", new SettingItem[]
            {
                new ToggleSettingItem("focusOnStartup",
                    s => s.VRChat.FocusOnStartup, (s, v) => s.WithVRChat(s.VRChat.WithFocusOnStartup(v))),
                new ToggleSettingItem("showTypingText",
                    s => s.VRChat.ShowTypingText, (s, v) => s.WithVRChat(s.VRChat.WithShowTypingText(v))),
                new ToggleSettingItem("resetPasteModeAfterSend",
                    s => s.VRChat.ResetPasteModeAfterSend, (s, v) => s.WithVRChat(s.VRChat.WithResetPasteModeAfterSend(v))),
                AutoLaunch,
            }),
            new SettingsCategory("privacy", new SettingItem[]
            {
                new ToggleSettingItem("useGoogleConversion",
                    s => s.Privacy.UseGoogleConversion, (s, v) => s.WithPrivacy(s.Privacy.WithUseGoogleConversion(v)),
                    requiresConsentToEnable: true),
            }),
        };

        /// <summary>全カテゴリの項目。</summary>
        public static readonly IReadOnlyList<SettingItem> Items = Flatten(Categories);

        /// <summary>キーに対応する項目。ない場合は例外。</summary>
        public static SettingItem Find(string key)
        {
            foreach (SettingItem item in Items)
            {
                if (item.Key == key) return item;
            }
            throw new ArgumentException("設定の項目がありません: " + key, nameof(key));
        }

        public static NumberSettingItem FindNumber(string key) => Find(key) as NumberSettingItem
            ?? throw new ArgumentException("数値の項目ではありません: " + key, nameof(key));

        public static ToggleSettingItem FindToggle(string key) => Find(key) as ToggleSettingItem
            ?? throw new ArgumentException("設定ファイルに保存するオン・オフの項目ではありません: " + key, nameof(key));

        /// <summary>キーに対応するオン・オフの項目（保存するもの・外が値を持つものの両方）。ない場合は例外。</summary>
        public static SwitchSettingItem FindSwitch(string key) => Find(key) as SwitchSettingItem
            ?? throw new ArgumentException("オン・オフの項目ではありません: " + key, nameof(key));

        public static ChoiceSettingItem FindChoice(string key) => Find(key) as ChoiceSettingItem
            ?? throw new ArgumentException("選択肢の項目ではありません: " + key, nameof(key));

        /// <summary>キーに対応するカテゴリ。ない場合は例外。</summary>
        public static SettingsCategory FindCategory(string key)
        {
            foreach (SettingsCategory category in Categories)
            {
                if (category.Key == key) return category;
            }
            throw new ArgumentException("設定のカテゴリがありません: " + key, nameof(key));
        }

        static IReadOnlyList<SettingItem> Flatten(IReadOnlyList<SettingsCategory> categories)
        {
            var items = new List<SettingItem>();
            var keys = new HashSet<string>();
            foreach (SettingsCategory category in categories)
            {
                foreach (SettingItem item in category.Items)
                {
                    if (!keys.Add(item.Key)) throw new InvalidOperationException("項目が複数のカテゴリにあります: " + item.Key);
                    items.Add(item);
                }
            }
            return items;
        }

        static string Centimeters(float meters) => Round(meters * 100f) + " cm";

        static string Sideways(float meters)
        {
            int cm = Round(meters * 100f);
            if (cm == 0) return "中央";
            return (cm > 0 ? "右 " : "左 ") + Math.Abs(cm).ToString(CultureInfo.InvariantCulture) + " cm";
        }

        static string Degrees(float degrees) => Round(degrees) + "°";

        static string Percent(float ratio) => Round(ratio * 100f) + "%";

        static string Seconds(float seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture) + " 秒";

        static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
