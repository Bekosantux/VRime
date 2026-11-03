using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面の操作の対象の配置と当たり判定。座標はテクスチャのピクセルで、左上を原点とし、下向きを正とする。
    /// 配置はシーンの uGUI から作る（<see cref="View.SettingsView.CreateLayout"/>）。表示とポインターの当たり判定（<see cref="SettingsForm"/>）が同じ配置を使う。
    /// 画面はカテゴリのタブ・「既定に戻す」と、選んでいるカテゴリのページからなる。ページどうしは同じ場所に重ねて置き、選んでいるページだけを当たり判定に使う。
    /// 操作の対象が画面の外にある・同時に表示されるものどうしが重なる配置は、どれを指しているか決まらないため例外にする。
    /// </summary>
    public sealed class SettingsLayout
    {
        /// <param name="size">画面（テクスチャ）の大きさ。</param>
        /// <param name="pages">カテゴリのページ。カテゴリの重複は許さない。先頭のページを最初に表示する。</param>
        /// <param name="reset">「既定に戻す」のボタン。</param>
        public SettingsLayout(Vector2 size, IReadOnlyList<SettingsPageLayout> pages, Rect reset)
        {
            if (!(size.x > 0f) || !(size.y > 0f)) throw new ArgumentOutOfRangeException(nameof(size), size, "画面の大きさが正ではありません。");
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (pages.Count == 0) throw new ArgumentException("ページがありません。", nameof(pages));

            var screen = new Rect(Vector2.zero, size);
            var shared = new List<(string name, Rect rect)> { ("既定に戻す", reset) };
            var categories = new HashSet<SettingsCategory>();
            foreach (SettingsPageLayout page in pages)
            {
                if (page == null) throw new ArgumentException("null のページがあります。", nameof(pages));
                if (!categories.Add(page.Category)) throw new ArgumentException("ページが重複しています: " + page.Category.Key, nameof(pages));
                shared.Add((page.Category.Key + " のタブ", page.Tab));
            }
            CheckTargets(screen, shared, 0);

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
                CheckTargets(screen, targets, shared.Count);
            }

            Size = size;
            Pages = pages;
            Reset = reset;
        }

        public Vector2 Size { get; }
        public IReadOnlyList<SettingsPageLayout> Pages { get; }

        /// <summary>「既定に戻す」のボタン。</summary>
        public Rect Reset { get; }

        public SettingsPageLayout PageOf(SettingsCategory category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            foreach (SettingsPageLayout page in Pages)
            {
                if (page.Category == category) return page;
            }
            throw new ArgumentException("この配置にないカテゴリです: " + category.Key, nameof(category));
        }

        /// <summary>カテゴリ category のページを表示しているときに、点 (x, y) にある操作の対象。</summary>
        public SettingsHit HitTest(Vector2 point, SettingsCategory category)
        {
            SettingsPageLayout current = PageOf(category);
            if (Reset.Contains(point)) return SettingsHit.Reset;
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
        static void CheckTargets(Rect screen, List<(string name, Rect rect)> targets, int checkedCount)
        {
            for (int i = checkedCount; i < targets.Count; i++)
            {
                (string name, Rect rect) = targets[i];
                if (!(rect.width > 0f) || !(rect.height > 0f))
                    throw new ArgumentException(name + " の大きさが正ではありません: " + rect, "pages");
                if (rect.xMin < screen.xMin || rect.yMin < screen.yMin || rect.xMax > screen.xMax || rect.yMax > screen.yMax)
                    throw new ArgumentException(name + " が画面の外にあります: " + rect, "pages");
                for (int j = 0; j < i; j++)
                {
                    if (rect.Overlaps(targets[j].rect))
                        throw new ArgumentException(name + " と " + targets[j].name + " が重なっています。", "pages");
                }
            }
        }
    }

    /// <summary>1カテゴリのタブとページの、操作の対象の配置。ページはカテゴリの項目をちょうど 1 行ずつ持つ。</summary>
    public sealed class SettingsPageLayout
    {
        public SettingsPageLayout(SettingsCategory category, Rect tab, IReadOnlyList<SettingsRowLayout> rows,
            IReadOnlyList<SettingsToggleLayout> toggles)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (toggles == null) throw new ArgumentNullException(nameof(toggles));

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
            foreach (SettingItem item in category.Items)
            {
                if (!items.Contains(item)) throw new ArgumentException(category.Key + " のページに " + item.Key + " の行がありません。", nameof(rows));
            }

            Tab = tab;
            Rows = rows;
            Toggles = toggles;
        }

        public SettingsCategory Category { get; }

        /// <summary>このカテゴリを選ぶタブ。</summary>
        public Rect Tab { get; }

        /// <summary>数値の項目の行。</summary>
        public IReadOnlyList<SettingsRowLayout> Rows { get; }

        /// <summary>オン・オフの項目の行。</summary>
        public IReadOnlyList<SettingsToggleLayout> Toggles { get; }

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
        public SettingsToggleLayout(ToggleSettingItem item, Rect area)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Area = area;
        }

        public ToggleSettingItem Item { get; }

        /// <summary>押すと切り替わる領域。</summary>
        public Rect Area { get; }
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
    }

    /// <summary>
    /// 当たり判定の結果。<see cref="Item"/> は項目の操作（スライダー・［−］・［＋］・スイッチ）のときだけ、
    /// <see cref="Category"/> はタブのときだけ持つ。
    /// </summary>
    public readonly struct SettingsHit : IEquatable<SettingsHit>
    {
        public static readonly SettingsHit None = new SettingsHit(SettingsTarget.None, null, null);
        public static readonly SettingsHit Reset = new SettingsHit(SettingsTarget.Reset, null, null);

        SettingsHit(SettingsTarget target, SettingItem item, SettingsCategory category)
        {
            Target = target;
            Item = item;
            Category = category;
        }

        public static SettingsHit Tab(SettingsCategory category) =>
            new SettingsHit(SettingsTarget.Tab, null, category ?? throw new ArgumentNullException(nameof(category)));

        public static SettingsHit Slider(NumberSettingItem item) => ForItem(SettingsTarget.Slider, item);
        public static SettingsHit Decrease(NumberSettingItem item) => ForItem(SettingsTarget.Decrease, item);
        public static SettingsHit Increase(NumberSettingItem item) => ForItem(SettingsTarget.Increase, item);
        public static SettingsHit Toggle(ToggleSettingItem item) => ForItem(SettingsTarget.Toggle, item);

        public SettingsTarget Target { get; }
        public SettingItem Item { get; }
        public SettingsCategory Category { get; }

        /// <summary>数値の項目の操作（スライダー・［−］・［＋］）の項目。それ以外では例外。</summary>
        public NumberSettingItem NumberItem => Item as NumberSettingItem
            ?? throw new InvalidOperationException("数値の項目の操作ではありません: " + Target);

        /// <summary>スイッチの項目。それ以外では例外。</summary>
        public ToggleSettingItem ToggleItem => Item as ToggleSettingItem
            ?? throw new InvalidOperationException("スイッチの操作ではありません: " + Target);

        public bool Equals(SettingsHit other) => Target == other.Target && Item == other.Item && Category == other.Category;
        public override bool Equals(object obj) => obj is SettingsHit other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Target * 397;
                hash ^= Item != null ? Item.GetHashCode() : 0;
                hash = hash * 31 + (Category != null ? Category.GetHashCode() : 0);
                return hash;
            }
        }

        public static bool operator ==(SettingsHit a, SettingsHit b) => a.Equals(b);
        public static bool operator !=(SettingsHit a, SettingsHit b) => !a.Equals(b);

        static SettingsHit ForItem(SettingsTarget target, SettingItem item) =>
            new SettingsHit(target, item ?? throw new ArgumentNullException(nameof(item)), null);
    }
}
