using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面の操作の対象の配置と当たり判定。座標はテクスチャのピクセルで、左上を原点とし、下向きを正とする。
    /// 配置はシーンの uGUI から作る（<see cref="View.SettingsView.CreateLayout"/>）。表示とポインターの当たり判定（<see cref="SettingsForm"/>）が同じ配置を使う。
    /// 操作の対象が画面の外にある・互いに重なる配置は、どれを指しているか決まらないため例外にする。
    /// </summary>
    public sealed class SettingsLayout
    {
        /// <param name="size">画面（テクスチャ）の大きさ。</param>
        /// <param name="rows">項目の行。項目の重複は許さない。</param>
        /// <param name="reset">「既定に戻す」のボタン。</param>
        public SettingsLayout(Vector2 size, IReadOnlyList<SettingsRowLayout> rows, Rect reset)
        {
            if (!(size.x > 0f) || !(size.y > 0f)) throw new ArgumentOutOfRangeException(nameof(size), size, "画面の大きさが正ではありません。");
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var screen = new Rect(Vector2.zero, size);
            var targets = new List<(string name, Rect rect)> { ("既定に戻す", reset) };
            var items = new HashSet<SettingItem>();
            foreach (SettingsRowLayout row in rows)
            {
                if (row == null) throw new ArgumentException("null の行があります。", nameof(rows));
                if (!items.Add(row.Item)) throw new ArgumentException("項目の行が重複しています: " + row.Item.Key, nameof(rows));
                targets.Add((row.Item.Key + " の［−］", row.Decrease));
                targets.Add((row.Item.Key + " のスライダー", row.Slider));
                targets.Add((row.Item.Key + " の［＋］", row.Increase));
            }

            for (int i = 0; i < targets.Count; i++)
            {
                (string name, Rect rect) = targets[i];
                if (!(rect.width > 0f) || !(rect.height > 0f))
                    throw new ArgumentException(name + " の大きさが正ではありません: " + rect, nameof(rows));
                if (rect.xMin < screen.xMin || rect.yMin < screen.yMin || rect.xMax > screen.xMax || rect.yMax > screen.yMax)
                    throw new ArgumentException(name + " が画面の外にあります: " + rect, nameof(rows));
                for (int j = 0; j < i; j++)
                {
                    if (rect.Overlaps(targets[j].rect))
                        throw new ArgumentException(name + " と " + targets[j].name + " が重なっています。", nameof(rows));
                }
            }

            Size = size;
            Rows = rows;
            Reset = reset;
        }

        public Vector2 Size { get; }
        public IReadOnlyList<SettingsRowLayout> Rows { get; }

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
            throw new ArgumentException("この配置にない項目です: " + item.Key, nameof(item));
        }
    }

    /// <summary>1項目の行の、操作の対象の配置。</summary>
    public sealed class SettingsRowLayout
    {
        /// <param name="slider">スライダーの操作を受ける領域。</param>
        /// <param name="track">値の範囲（左端が最小値、右端が最大値）。</param>
        public SettingsRowLayout(SettingItem item, Rect decrease, Rect slider, Rect increase, Rect track)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (!(track.width > 0f)) throw new ArgumentException("値の範囲の幅が正ではありません: " + item.Key, nameof(track));
            Decrease = decrease;
            Slider = slider;
            Increase = increase;
            Track = track;
        }

        public SettingItem Item { get; }
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
