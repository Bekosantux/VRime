using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面の配置。座標はテクスチャのピクセルで、左上を原点とし、下向きを正とする（<see cref="View.UiBuilder"/> と同じ）。
    /// 表示（<see cref="View.SettingsView"/>）と、ポインターの当たり判定（<see cref="SettingsForm"/>）が同じ配置を使う。
    /// </summary>
    public sealed class SettingsLayout
    {
        public const int Width = 1280;
        public const int Height = 800;

        const float PaddingX = 48f;
        const float ContentWidth = Width - PaddingX * 2f;
        const float TitleTop = 24f;
        const float TitleHeight = 56f;
        const float GroupGap = 16f;
        const float HeaderHeight = 48f;
        const float RowHeight = 72f;
        const float LabelWidth = 300f;
        const float ButtonSize = 64f;
        const float DecreaseX = 360f;
        const float SliderX = 440f;
        const float SliderWidth = 520f;
        const float IncreaseX = 976f;
        const float ValueX = 1056f;
        const float FooterTop = 720f;
        const float ResetWidth = 240f;
        const float ResetHeight = 52f;

        /// <summary>つまみの半分の幅。つまみが端からはみ出さないよう、スライダーの両端をこの分だけ内側にした範囲を値の範囲とする。</summary>
        public const float ThumbHalfWidth = 18f;

        public SettingsLayout(IReadOnlyList<SettingGroup> groups)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (groups.Count == 0) throw new ArgumentException("項目のまとまりがありません。", nameof(groups));

            Title = new Rect(PaddingX, TitleTop, ContentWidth, TitleHeight);

            var groupLayouts = new List<SettingsGroupLayout>(groups.Count);
            var rows = new List<SettingsRowLayout>();
            float y = TitleTop + TitleHeight + GroupGap;
            foreach (SettingGroup group in groups)
            {
                if (group == null) throw new ArgumentException("null のまとまりがあります。", nameof(groups));
                groupLayouts.Add(new SettingsGroupLayout(group,
                    new Rect(PaddingX, y, LabelWidth, HeaderHeight),
                    new Rect(DecreaseX, y, Width - PaddingX - DecreaseX, HeaderHeight)));
                y += HeaderHeight;
                foreach (SettingItem item in group.Items)
                {
                    float buttonTop = y + (RowHeight - ButtonSize) / 2f;
                    rows.Add(new SettingsRowLayout(item,
                        new Rect(PaddingX, y, LabelWidth, RowHeight),
                        new Rect(DecreaseX, buttonTop, ButtonSize, ButtonSize),
                        new Rect(SliderX, y, SliderWidth, RowHeight),
                        new Rect(IncreaseX, buttonTop, ButtonSize, ButtonSize),
                        new Rect(ValueX, y, Width - PaddingX - ValueX, RowHeight)));
                    y += RowHeight;
                }
                y += GroupGap;
            }
            if (y - GroupGap > FooterTop)
                throw new InvalidOperationException("設定の項目が画面に収まりません。");

            Groups = groupLayouts;
            Rows = rows;
            Notice = new Rect(PaddingX, FooterTop, Width - PaddingX * 3f - ResetWidth, Height - FooterTop);
            Reset = new Rect(Width - PaddingX - ResetWidth, FooterTop + 12f, ResetWidth, ResetHeight);
        }

        public Rect Title { get; }
        public IReadOnlyList<SettingsGroupLayout> Groups { get; }
        public IReadOnlyList<SettingsRowLayout> Rows { get; }

        /// <summary>通知（設定ファイルの問題など）を表示する領域。</summary>
        public Rect Notice { get; }

        /// <summary>「既定に戻す」のボタン。</summary>
        public Rect Reset { get; }

        /// <summary>点 (x, y) にある操作の対象。</summary>
        public SettingsHit HitTest(Vector2 point)
        {
            if (Reset.Contains(point)) return new SettingsHit(SettingsTarget.Reset, null);
            foreach (SettingsRowLayout row in Rows)
            {
                if (row.Slider.Contains(point)) return new SettingsHit(SettingsTarget.Slider, row.Item);
                if (row.Decrease.Contains(point)) return new SettingsHit(SettingsTarget.Decrease, row.Item);
                if (row.Increase.Contains(point)) return new SettingsHit(SettingsTarget.Increase, row.Item);
            }
            return SettingsHit.None;
        }

        public SettingsRowLayout RowOf(SettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            foreach (SettingsRowLayout row in Rows)
            {
                if (row.Item == item) return row;
            }
            throw new ArgumentException("この配置にない項目です: " + item.Label, nameof(item));
        }
    }

    /// <summary>見出しの配置。</summary>
    public sealed class SettingsGroupLayout
    {
        internal SettingsGroupLayout(SettingGroup group, Rect title, Rect note)
        {
            Group = group;
            Title = title;
            Note = note;
        }

        public SettingGroup Group { get; }
        public Rect Title { get; }
        public Rect Note { get; }
    }

    /// <summary>1項目の行の配置。左から 項目名・［−］・スライダー・［＋］・値。</summary>
    public sealed class SettingsRowLayout
    {
        internal SettingsRowLayout(SettingItem item, Rect label, Rect decrease, Rect slider, Rect increase, Rect value)
        {
            Item = item;
            Label = label;
            Decrease = decrease;
            Slider = slider;
            Increase = increase;
            Value = value;
        }

        public SettingItem Item { get; }
        public Rect Label { get; }
        public Rect Decrease { get; }

        /// <summary>スライダーの操作を受ける領域（行の高さいっぱい）。</summary>
        public Rect Slider { get; }

        public Rect Increase { get; }
        public Rect Value { get; }

        /// <summary>値の範囲の左端（最小値）の x。</summary>
        public float TrackLeft => Slider.xMin + SettingsLayout.ThumbHalfWidth;

        /// <summary>値の範囲の幅。</summary>
        public float TrackWidth => Slider.width - SettingsLayout.ThumbHalfWidth * 2f;

        /// <summary>x の位置が値の範囲のどこにあたるか（0..1。外側は端に収める）。</summary>
        public float FractionAt(float x)
        {
            return Mathf.Clamp01((x - TrackLeft) / TrackWidth);
        }
    }

    public enum SettingsTarget
    {
        None,
        Slider,
        Decrease,
        Increase,
        Reset,
    }

    /// <summary>当たり判定の結果。<see cref="Item"/> は項目の操作（スライダー・［−］・［＋］）のときだけ持つ。</summary>
    public readonly struct SettingsHit : IEquatable<SettingsHit>
    {
        public static readonly SettingsHit None = new SettingsHit(SettingsTarget.None, null);

        public SettingsHit(SettingsTarget target, SettingItem item)
        {
            bool needsItem = target == SettingsTarget.Slider || target == SettingsTarget.Decrease || target == SettingsTarget.Increase;
            if (needsItem != (item != null))
                throw new ArgumentException(needsItem ? "項目の操作には項目が必要です。" : "この操作は項目を持ちません。", nameof(item));
            Target = target;
            Item = item;
        }

        public SettingsTarget Target { get; }
        public SettingItem Item { get; }

        public bool Equals(SettingsHit other) => Target == other.Target && Item == other.Item;
        public override bool Equals(object obj) => obj is SettingsHit other && Equals(other);
        public override int GetHashCode() => ((int)Target * 397) ^ (Item != null ? Item.GetHashCode() : 0);
        public static bool operator ==(SettingsHit a, SettingsHit b) => a.Equals(b);
        public static bool operator !=(SettingsHit a, SettingsHit b) => !a.Equals(b);
    }
}
