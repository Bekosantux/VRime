using System;
using UnityEngine;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面の状態と操作。ポインターの入力を <see cref="SettingsLayout"/> で当たり判定し、設定値を変える。
    /// スライダーはつまんでいる間、値を随時変える（<see cref="Changed"/>）。離したとき、変わっていれば確定する（<see cref="Committed"/>）。
    /// ボタン・スイッチ・タブは、押したのと同じものの上で離したときに働く。値が変わればその場で確定する。
    /// 表示するページはタブで選ぶ（<see cref="Category"/>）。「既定に戻す」は表示中のカテゴリの項目だけを戻す。
    /// 表示（<see cref="View.SettingsView"/>）は <see cref="Version"/> の変化を見て描き直す。
    /// </summary>
    public sealed class SettingsForm : ISettingsPointer
    {
        readonly SettingsLayout _layout;
        UserSettings _committed;

        public SettingsForm(SettingsLayout layout, UserSettings initial)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Settings = initial ?? throw new ArgumentNullException(nameof(initial));
            _committed = initial;
            Category = layout.Pages[0].Category;
        }

        public SettingsLayout Layout => _layout;

        /// <summary>表示しているページのカテゴリ。</summary>
        public SettingsCategory Category { get; private set; }

        /// <summary>現在の設定値（スライダーの操作中は、確定前の値）。</summary>
        public UserSettings Settings { get; private set; }

        /// <summary>ポインターが指している操作の対象。</summary>
        public SettingsHit Hover { get; private set; } = SettingsHit.None;

        /// <summary>つまんでいるスライダーの項目。つまんでいなければ null。</summary>
        public NumberSettingItem DraggingItem { get; private set; }

        /// <summary>押したまま離していないボタン・スイッチ・タブ。</summary>
        public SettingsHit Pressed { get; private set; } = SettingsHit.None;

        /// <summary>画面の下部に表示する通知。なければ null。</summary>
        public string Notice { get; private set; }

        /// <summary>表示に関わる状態が変わるたびに増える。</summary>
        public int Version { get; private set; }

        /// <summary>設定値が変わった（確定前を含む）。キーボードの表示と動作へ随時反映するのに使う。</summary>
        public event Action<UserSettings> Changed;

        /// <summary>設定値が確定した。保存に使う。</summary>
        public event Action<UserSettings> Committed;

        public void SetNotice(string notice)
        {
            if (notice == Notice) return;
            Notice = notice;
            Version++;
        }

        public void PointerMove(Vector2 position)
        {
            SetHover(_layout.HitTest(position, Category));
            if (DraggingItem != null) SetValueAt(DraggingItem, position.x);
        }

        public void PointerDown(Vector2 position)
        {
            PointerMove(position);
            // 前の操作が終わっていない（離したことが届かなかった）場合は、先に終える。
            EndOperation();

            switch (Hover.Target)
            {
                case SettingsTarget.Slider:
                    DraggingItem = Hover.NumberItem;
                    Version++;
                    SetValueAt(DraggingItem, position.x);
                    break;
                case SettingsTarget.Tab:
                case SettingsTarget.Decrease:
                case SettingsTarget.Increase:
                case SettingsTarget.Toggle:
                case SettingsTarget.Reset:
                    Pressed = Hover;
                    Version++;
                    break;
            }
        }

        public void PointerUp(Vector2 position)
        {
            PointerMove(position);
            SettingsHit pressed = Pressed;
            bool activate = pressed.Target != SettingsTarget.None && pressed == Hover;
            EndOperation();
            if (activate) Activate(pressed);
        }

        public void PointerLeave()
        {
            SetHover(SettingsHit.None);
            EndOperation();
        }

        // スライダーをつまんでいれば離して確定し、押しているボタンは働かせずに離す。
        void EndOperation()
        {
            if (DraggingItem != null)
            {
                DraggingItem = null;
                Version++;
                Commit();
            }
            if (Pressed.Target != SettingsTarget.None)
            {
                Pressed = SettingsHit.None;
                Version++;
            }
        }

        void Activate(SettingsHit hit)
        {
            switch (hit.Target)
            {
                case SettingsTarget.Tab:
                    SelectCategory(hit.Category);
                    return;
                case SettingsTarget.Decrease:
                    Step(hit.NumberItem, -1);
                    break;
                case SettingsTarget.Increase:
                    Step(hit.NumberItem, 1);
                    break;
                case SettingsTarget.Toggle:
                    SetSettings(hit.ToggleItem.Toggle(Settings));
                    break;
                case SettingsTarget.Reset:
                    SetSettings(Category.ResetToDefault(Settings));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hit), hit.Target, null);
            }
            Commit();
        }

        void SelectCategory(SettingsCategory category)
        {
            if (category == Category) return;
            Category = category;
            Version++;
        }

        void Step(NumberSettingItem item, int steps)
        {
            SetSettings(item.Set(Settings, item.Range.Offset(item.Get(Settings), steps)));
        }

        void SetValueAt(NumberSettingItem item, float x)
        {
            float fraction = _layout.RowOf(item).FractionAt(x);
            SetSettings(item.Set(Settings, item.Range.FromFraction(fraction)));
        }

        void SetHover(SettingsHit hit)
        {
            if (hit == Hover) return;
            Hover = hit;
            Version++;
        }

        void SetSettings(UserSettings settings)
        {
            if (settings.Equals(Settings)) return;
            Settings = settings;
            Version++;
            Changed?.Invoke(settings);
        }

        void Commit()
        {
            if (Settings.Equals(_committed)) return;
            _committed = Settings;
            Committed?.Invoke(Settings);
        }
    }
}
