using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRime.App.Settings
{
    /// <summary>
    /// 設定画面の操作の対象の配置と当たり判定。座標はテクスチャのピクセルで、左上を原点とし、下向きを正とする。
    /// 配置はシーンの uGUI から作る（<see cref="View.SettingsView.CreateLayout"/>）。表示とポインターの当たり判定（<see cref="SettingsForm"/>）が同じ配置を使う。
    /// 画面はカテゴリのタブ・「既定に戻す」・「アプリを終了」と、選んでいるカテゴリのページからなる。ページどうしは同じ場所に重ねて置き、選んでいるページだけを当たり判定に使う。
    /// 同意の画面（<see cref="SettingsConsentLayout"/>）を開いている間は、その画面のボタンだけを当たり判定に使う。
    /// ドロップダウンの選択肢の一覧（<see cref="SettingsDropdownLayout"/>）を開いている間は、その選択肢だけを当たり判定に使い、ほかの場所はすべて一覧を閉じる対象にする。
    /// 操作の対象が画面の外にある・同時に表示されるものどうしが重なる配置は、どれを指しているか決まらないため例外にする。
    /// </summary>
    public sealed class SettingsLayout
    {
        /// <param name="size">画面（テクスチャ）の大きさ。</param>
        /// <param name="pages">カテゴリのページ。カテゴリの重複は許さない。先頭のページを最初に表示する。</param>
        /// <param name="reset">「既定に戻す」のボタン。</param>
        /// <param name="quit">「アプリを終了」のボタン。</param>
        /// <param name="consents">同意の画面。オンにする前に同意が必要な項目（<see cref="ToggleSettingItem.RequiresConsentToEnable"/>）ごとに 1 つずつ。</param>
        public SettingsLayout(Vector2 size, IReadOnlyList<SettingsPageLayout> pages, Rect reset, Rect quit,
            IReadOnlyList<SettingsConsentLayout> consents)
        {
            if (!(size.x > 0f) || !(size.y > 0f)) throw new ArgumentOutOfRangeException(nameof(size), size, "画面の大きさが正ではありません。");
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (pages.Count == 0) throw new ArgumentException("ページがありません。", nameof(pages));
            if (consents == null) throw new ArgumentNullException(nameof(consents));

            var screen = new Rect(Vector2.zero, size);
            var shared = new List<(string name, Rect rect)> { ("既定に戻す", reset), ("終了", quit) };
            var categories = new HashSet<SettingsCategory>();
            foreach (SettingsPageLayout page in pages)
            {
                if (page == null) throw new ArgumentException("null のページがあります。", nameof(pages));
                if (!categories.Add(page.Category)) throw new ArgumentException("ページが重複しています: " + page.Category.Key, nameof(pages));
                shared.Add((page.Category.Key + " のタブ", page.Tab));
            }
            CheckTargets(screen, shared, 0, nameof(pages));

            foreach (SettingsPageLayout page in pages)
            {
                var targets = new List<(string name, Rect rect)>(shared);
                foreach (SettingsRowLayout row in page.Rows)
                {
                    targets.Add((row.Item.Key + " の［−］", row.Decrease));
                    targets.Add((row.Item.Key + " のスライダー", row.Slider));
                    targets.Add((row.Item.Key + " の［＋］", row.Increase));
                }
                foreach (SettingsToggleLayout toggle in page.Toggles) targets.Add((toggle.Item.Key + " のスイッチ", toggle.Area));
                foreach (SettingsDropdownLayout dropdown in page.Dropdowns) targets.Add((dropdown.Item.Key + " のドロップダウン", dropdown.Button));
                CheckTargets(screen, targets, shared.Count, nameof(pages));

                // 選択肢の一覧はページの上に重ねて開くため、ページの対象と重なってよい。
                foreach (SettingsDropdownLayout dropdown in page.Dropdowns)
                {
                    var options = new List<(string name, Rect rect)>();
                    for (int i = 0; i < dropdown.Options.Count; i++) options.Add((dropdown.Item.Key + " の選択肢 " + i, dropdown.Options[i]));
                    CheckTargets(screen, options, 0, nameof(pages));
                }
            }

            var consentItems = new HashSet<ToggleSettingItem>();
            foreach (SettingsConsentLayout consent in consents)
            {
                if (consent == null) throw new ArgumentException("null の同意の画面があります。", nameof(consents));
                if (!consentItems.Add(consent.Item)) throw new ArgumentException("同意の画面が重複しています: " + consent.Item.Key, nameof(consents));
                CheckTargets(screen, new List<(string name, Rect rect)>
                {
                    (consent.Item.Key + " の同意のボタン", consent.Accept),
                    (consent.Item.Key + " の同意の取り消しのボタン", consent.Cancel),
                }, 0, nameof(consents));
            }
            foreach (SettingsPageLayout page in pages)
            {
                foreach (SettingsToggleLayout toggle in page.Toggles)
                {
                    bool requiresConsent = toggle.Item is ToggleSettingItem stored && stored.RequiresConsentToEnable;
                    bool hasConsent = toggle.Item is ToggleSettingItem item && consentItems.Remove(item);
                    if (requiresConsent != hasConsent)
                        throw new ArgumentException(requiresConsent
                            ? toggle.Item.Key + " の同意の画面がありません。"
                            : toggle.Item.Key + " は同意の確認が必要な項目ではありません。", nameof(consents));
                }
            }
            foreach (ToggleSettingItem item in consentItems)
                throw new ArgumentException(item.Key + " の同意の画面に対応する行がありません。", nameof(consents));

            Size = size;
            Pages = pages;
            Reset = reset;
            Quit = quit;
            Consents = consents;
        }

        public Vector2 Size { get; }
        public IReadOnlyList<SettingsPageLayout> Pages { get; }

        /// <summary>「既定に戻す」のボタン。</summary>
        public Rect Reset { get; }

        /// <summary>「アプリを終了」のボタン。</summary>
        public Rect Quit { get; }

        public IReadOnlyList<SettingsConsentLayout> Consents { get; }

        /// <summary>item の同意の画面。ない場合は例外。</summary>
        public SettingsConsentLayout ConsentOf(ToggleSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            foreach (SettingsConsentLayout consent in Consents)
            {
                if (consent.Item == item) return consent;
            }
            throw new ArgumentException("この配置に同意の画面がない項目です: " + item.Key, nameof(item));
        }

        public SettingsPageLayout PageOf(SettingsCategory category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            foreach (SettingsPageLayout page in Pages)
            {
                if (page.Category == category) return page;
            }
            throw new ArgumentException("この配置にないカテゴリです: " + category.Key, nameof(category));
        }

        /// <summary>
        /// カテゴリ category のページを表示しているときに、点 (x, y) にある操作の対象。
        /// consent が null でない間はその項目の同意の画面を開いているとして、その画面のボタンだけを返す。
        /// dropdown が null でない間はその項目の選択肢の一覧を開いているとして、選択肢か、一覧を閉じる対象（<see cref="SettingsHit.DropdownDismiss"/>）を返す。
        /// </summary>
        public SettingsHit HitTest(Vector2 point, SettingsCategory category, ToggleSettingItem consent, ChoiceSettingItem dropdown)
        {
            SettingsPageLayout current = PageOf(category);
            if (consent != null && dropdown != null)
                throw new ArgumentException("同意の画面と選択肢の一覧は同時に開けません: " + consent.Key + " / " + dropdown.Key, nameof(dropdown));
            if (dropdown != null)
            {
                SettingsDropdownLayout list = current.DropdownOf(dropdown);
                for (int i = 0; i < list.Options.Count; i++)
                {
                    if (list.Options[i].Contains(point)) return SettingsHit.Option(dropdown, i);
                }
                return SettingsHit.DropdownDismiss;
            }
            if (consent != null)
            {
                SettingsConsentLayout dialog = ConsentOf(consent);
                if (dialog.Accept.Contains(point)) return SettingsHit.ConsentAccept;
                if (dialog.Cancel.Contains(point)) return SettingsHit.ConsentCancel;
                return SettingsHit.None;
            }
            if (Reset.Contains(point)) return SettingsHit.Reset;
            if (Quit.Contains(point)) return SettingsHit.Quit;
            foreach (SettingsPageLayout page in Pages)
            {
                if (page.Tab.Contains(point)) return SettingsHit.Tab(page.Category);
            }
            foreach (SettingsRowLayout row in current.Rows)
            {
                if (row.Slider.Contains(point)) return SettingsHit.Slider(row.Item);
                if (row.Decrease.Contains(point)) return SettingsHit.Decrease(row.Item);
                if (row.Increase.Contains(point)) return SettingsHit.Increase(row.Item);
            }
            foreach (SettingsToggleLayout toggle in current.Toggles)
            {
                if (toggle.Area.Contains(point)) return SettingsHit.Toggle(toggle.Item);
            }
            foreach (SettingsDropdownLayout list in current.Dropdowns)
            {
                if (list.Button.Contains(point)) return SettingsHit.Dropdown(list.Item);
            }
            return SettingsHit.None;
        }

        public SettingsRowLayout RowOf(NumberSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            foreach (SettingsPageLayout page in Pages)
            {
                foreach (SettingsRowLayout row in page.Rows)
                {
                    if (row.Item == item) return row;
                }
            }
            throw new ArgumentException("この配置にない項目です: " + item.Key, nameof(item));
        }

        // checkedCount より前は検査済み（互いに重ならない）として、それ以降を検査する。
        static void CheckTargets(Rect screen, List<(string name, Rect rect)> targets, int checkedCount, string paramName)
        {
            for (int i = checkedCount; i < targets.Count; i++)
            {
                (string name, Rect rect) = targets[i];
                if (!(rect.width > 0f) || !(rect.height > 0f))
                    throw new ArgumentException(name + " の大きさが正ではありません: " + rect, paramName);
                if (rect.xMin < screen.xMin || rect.yMin < screen.yMin || rect.xMax > screen.xMax || rect.yMax > screen.yMax)
                    throw new ArgumentException(name + " が画面の外にあります: " + rect, paramName);
                for (int j = 0; j < i; j++)
                {
                    if (rect.Overlaps(targets[j].rect))
                        throw new ArgumentException(name + " と " + targets[j].name + " が重なっています。", paramName);
                }
            }
        }
    }

    /// <summary>1カテゴリのタブとページの、操作の対象の配置。ページはカテゴリの項目をちょうど 1 行ずつ持つ。</summary>
    public sealed class SettingsPageLayout
    {
        public SettingsPageLayout(SettingsCategory category, Rect tab, IReadOnlyList<SettingsRowLayout> rows,
            IReadOnlyList<SettingsToggleLayout> toggles, IReadOnlyList<SettingsDropdownLayout> dropdowns)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (toggles == null) throw new ArgumentNullException(nameof(toggles));
            if (dropdowns == null) throw new ArgumentNullException(nameof(dropdowns));

            var items = new HashSet<SettingItem>();
            foreach (SettingsRowLayout row in rows)
            {
                if (row == null) throw new ArgumentException("null の行があります: " + category.Key, nameof(rows));
                AddItem(items, row.Item);
            }
            foreach (SettingsToggleLayout toggle in toggles)
            {
                if (toggle == null) throw new ArgumentException("null の行があります: " + category.Key, nameof(toggles));
                AddItem(items, toggle.Item);
            }
            foreach (SettingsDropdownLayout dropdown in dropdowns)
            {
                if (dropdown == null) throw new ArgumentException("null の行があります: " + category.Key, nameof(dropdowns));
                AddItem(items, dropdown.Item);
            }
            foreach (SettingItem item in category.Items)
            {
                if (!items.Contains(item)) throw new ArgumentException(category.Key + " のページに " + item.Key + " の行がありません。", nameof(rows));
            }

            Tab = tab;
            Rows = rows;
            Toggles = toggles;
            Dropdowns = dropdowns;
        }

        public SettingsCategory Category { get; }

        /// <summary>このカテゴリを選ぶタブ。</summary>
        public Rect Tab { get; }

        /// <summary>数値の項目の行。</summary>
        public IReadOnlyList<SettingsRowLayout> Rows { get; }

        /// <summary>オン・オフの項目の行。</summary>
        public IReadOnlyList<SettingsToggleLayout> Toggles { get; }

        /// <summary>選択肢の項目の行。</summary>
        public IReadOnlyList<SettingsDropdownLayout> Dropdowns { get; }

        /// <summary>このページにある item の行。ない場合は例外。</summary>
        public SettingsDropdownLayout DropdownOf(ChoiceSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            foreach (SettingsDropdownLayout dropdown in Dropdowns)
            {
                if (dropdown.Item == item) return dropdown;
            }
            throw new ArgumentException(Category.Key + " のページにない項目です: " + item.Key, nameof(item));
        }

        void AddItem(HashSet<SettingItem> items, SettingItem item)
        {
            if (!Category.Contains(item)) throw new ArgumentException(item.Key + " は " + Category.Key + " の項目ではありません。", "rows");
            if (!items.Add(item)) throw new ArgumentException("項目の行が重複しています: " + item.Key, "rows");
        }
    }

    /// <summary>数値の項目の行の、操作の対象の配置。</summary>
    public sealed class SettingsRowLayout
    {
        /// <param name="slider">スライダーの操作を受ける領域。</param>
        /// <param name="track">値の範囲（左端が最小値、右端が最大値）。</param>
        public SettingsRowLayout(NumberSettingItem item, Rect decrease, Rect slider, Rect increase, Rect track)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (!(track.width > 0f)) throw new ArgumentException("値の範囲の幅が正ではありません: " + item.Key, nameof(track));
            Decrease = decrease;
            Slider = slider;
            Increase = increase;
            Track = track;
        }

        public NumberSettingItem Item { get; }
        public Rect Decrease { get; }

        /// <summary>スライダーの操作を受ける領域。</summary>
        public Rect Slider { get; }

        public Rect Increase { get; }

        /// <summary>値の範囲。</summary>
        public Rect Track { get; }

        /// <summary>x の位置が値の範囲のどこにあたるか（0..1。外側は端に収める）。</summary>
        public float FractionAt(float x)
        {
            return Mathf.Clamp01((x - Track.xMin) / Track.width);
        }
    }

    /// <summary>オン・オフの項目の行の、操作の対象の配置。</summary>
    public sealed class SettingsToggleLayout
    {
        /// <param name="area">押すと切り替わる領域。</param>
        public SettingsToggleLayout(SwitchSettingItem item, Rect area)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Area = area;
        }

        public SwitchSettingItem Item { get; }

        /// <summary>押すと切り替わる領域。</summary>
        public Rect Area { get; }
    }

    /// <summary>
    /// 選択肢の項目の行の、操作の対象の配置。ボタンを押すと選択肢の一覧を開く。一覧はページの上に重ねて開き、開いている間は選択肢だけを操作できる。
    /// </summary>
    public sealed class SettingsDropdownLayout
    {
        /// <param name="button">現在の値を示し、押すと一覧を開くボタン。</param>
        /// <param name="options">選択肢。項目の選択肢（<see cref="ChoiceSettingItem.Labels"/>）と同じ数・同じ順。</param>
        public SettingsDropdownLayout(ChoiceSettingItem item, Rect button, IReadOnlyList<Rect> options)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Count != item.Labels.Count)
                throw new ArgumentException(item.Key + " の選択肢の数（" + item.Labels.Count + "）と配置の数（" + options.Count + "）が一致しません。", nameof(options));
            Button = button;
            Options = options;
        }

        public ChoiceSettingItem Item { get; }

        /// <summary>現在の値を示し、押すと一覧を開くボタン。</summary>
        public Rect Button { get; }

        /// <summary>選択肢。項目の選択肢と同じ順。</summary>
        public IReadOnlyList<Rect> Options { get; }
    }

    /// <summary>
    /// オンにする前に同意が必要な項目の、同意の画面の配置。画面はページの上に重ねて表示し、開いている間はこの画面のボタンだけを操作できる。
    /// </summary>
    public sealed class SettingsConsentLayout
    {
        /// <param name="accept">同意して項目をオンにするボタン。</param>
        /// <param name="cancel">同意せずに閉じるボタン。</param>
        public SettingsConsentLayout(ToggleSettingItem item, Rect accept, Rect cancel)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (!item.RequiresConsentToEnable) throw new ArgumentException(item.Key + " は同意の確認が必要な項目ではありません。", nameof(item));
            Accept = accept;
            Cancel = cancel;
        }

        public ToggleSettingItem Item { get; }

        /// <summary>同意して項目をオンにするボタン。</summary>
        public Rect Accept { get; }

        /// <summary>同意せずに閉じるボタン。</summary>
        public Rect Cancel { get; }
    }

    public enum SettingsTarget
    {
        None,
        Tab,
        Slider,
        Decrease,
        Increase,
        Toggle,
        Reset,
        Quit,
        ConsentAccept,
        ConsentCancel,

        /// <summary>選択肢の一覧を開くボタン。</summary>
        Dropdown,

        /// <summary>開いている一覧の選択肢。</summary>
        DropdownOption,

        /// <summary>選択肢の一覧を開いている間の、選択肢以外の場所。押して離すと一覧を閉じる。</summary>
        DropdownDismiss,
    }

    /// <summary>
    /// 当たり判定の結果。<see cref="Item"/> は項目の操作（スライダー・［−］・［＋］・スイッチ・ドロップダウン・選択肢）のときだけ、
    /// <see cref="Category"/> はタブのときだけ、選択肢の位置（<see cref="OptionIndex"/>）は選択肢のときだけ持つ。
    /// </summary>
    public readonly struct SettingsHit : IEquatable<SettingsHit>
    {
        const int NoOption = -1;

        public static readonly SettingsHit None = new SettingsHit(SettingsTarget.None, null, null, NoOption);
        public static readonly SettingsHit Reset = new SettingsHit(SettingsTarget.Reset, null, null, NoOption);
        public static readonly SettingsHit Quit = new SettingsHit(SettingsTarget.Quit, null, null, NoOption);

        /// <summary>開いている同意の画面の、同意するボタン。</summary>
        public static readonly SettingsHit ConsentAccept = new SettingsHit(SettingsTarget.ConsentAccept, null, null, NoOption);

        /// <summary>開いている同意の画面の、同意せずに閉じるボタン。</summary>
        public static readonly SettingsHit ConsentCancel = new SettingsHit(SettingsTarget.ConsentCancel, null, null, NoOption);

        /// <summary>選択肢の一覧を開いている間の、選択肢以外の場所。</summary>
        public static readonly SettingsHit DropdownDismiss = new SettingsHit(SettingsTarget.DropdownDismiss, null, null, NoOption);

        readonly int _option;

        SettingsHit(SettingsTarget target, SettingItem item, SettingsCategory category, int option)
        {
            Target = target;
            Item = item;
            Category = category;
            _option = option;
        }

        public static SettingsHit Tab(SettingsCategory category) =>
            new SettingsHit(SettingsTarget.Tab, null, category ?? throw new ArgumentNullException(nameof(category)), NoOption);

        public static SettingsHit Slider(NumberSettingItem item) => ForItem(SettingsTarget.Slider, item);
        public static SettingsHit Decrease(NumberSettingItem item) => ForItem(SettingsTarget.Decrease, item);
        public static SettingsHit Increase(NumberSettingItem item) => ForItem(SettingsTarget.Increase, item);
        public static SettingsHit Toggle(SwitchSettingItem item) => ForItem(SettingsTarget.Toggle, item);
        public static SettingsHit Dropdown(ChoiceSettingItem item) => ForItem(SettingsTarget.Dropdown, item);

        /// <summary>item の index 番目の選択肢。</summary>
        public static SettingsHit Option(ChoiceSettingItem item, int index)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (index < 0 || index >= item.Labels.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, item.Key + " の選択肢は " + item.Labels.Count + " 個です。");
            return new SettingsHit(SettingsTarget.DropdownOption, item, null, index);
        }

        public SettingsTarget Target { get; }
        public SettingItem Item { get; }
        public SettingsCategory Category { get; }

        /// <summary>数値の項目の操作（スライダー・［−］・［＋］）の項目。それ以外では例外。</summary>
        public NumberSettingItem NumberItem => Item as NumberSettingItem
            ?? throw new InvalidOperationException("数値の項目の操作ではありません: " + Target);

        /// <summary>スイッチの項目。それ以外では例外。</summary>
        public SwitchSettingItem ToggleItem => Item as SwitchSettingItem
            ?? throw new InvalidOperationException("スイッチの操作ではありません: " + Target);

        /// <summary>ドロップダウン・選択肢の項目。それ以外では例外。</summary>
        public ChoiceSettingItem ChoiceItem => Item as ChoiceSettingItem
            ?? throw new InvalidOperationException("ドロップダウンの操作ではありません: " + Target);

        /// <summary>選択肢の位置（項目の選択肢の順）。選択肢以外では例外。</summary>
        public int OptionIndex => Target == SettingsTarget.DropdownOption ? _option
            : throw new InvalidOperationException("選択肢ではありません: " + Target);

        public bool Equals(SettingsHit other) =>
            Target == other.Target && Item == other.Item && Category == other.Category && _option == other._option;
        public override bool Equals(object obj) => obj is SettingsHit other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Target * 397;
                hash ^= Item != null ? Item.GetHashCode() : 0;
                hash = hash * 31 + (Category != null ? Category.GetHashCode() : 0);
                hash = hash * 31 + _option;
                return hash;
            }
        }

        public static bool operator ==(SettingsHit a, SettingsHit b) => a.Equals(b);
        public static bool operator !=(SettingsHit a, SettingsHit b) => !a.Equals(b);

        static SettingsHit ForItem(SettingsTarget target, SettingItem item) =>
            new SettingsHit(target, item ?? throw new ArgumentNullException(nameof(item)), null, NoOption);
    }
}
