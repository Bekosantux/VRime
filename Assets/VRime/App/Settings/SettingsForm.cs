using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRime.App.Settings
{
    /// <summary>
    /// 設定画面の状態と操作。ポインターの入力を <see cref="SettingsLayout"/> で当たり判定し、設定値を変える。
    /// スライダーはドラッグしている間、値を随時変える（<see cref="Changed"/>）。離したとき、変わっていれば確定する（<see cref="Committed"/>）。
    /// ボタン・スイッチ・タブは、押したのと同じものの上で離したときに動作する。値が変わればその場で確定する。
    /// 表示するページはタブで選ぶ（<see cref="Category"/>）。「既定に戻す」は表示中のカテゴリの項目だけを戻す。「アプリを終了」は確認せずにアプリの終了を求める（<see cref="QuitRequested"/>）。
    /// オンにする前に同意が必要なスイッチ（<see cref="ToggleSettingItem.RequiresConsentToEnable"/>）は、オフから押すと値を変えずに同意の画面を開く（<see cref="ConsentItem"/>）。
    /// 同意すればオンにして確定し、取り消せば何も変えずに閉じる。開いている間は同意の画面のボタンだけを操作できる。オフにするときと「既定に戻す」では確認しない。
    /// 選択肢の項目（<see cref="ChoiceSettingItem"/>）は、ボタンを押すと選択肢の一覧を開く（<see cref="OpenDropdown"/>）。
    /// 選択肢を押すと値を変えて確定し、一覧を閉じる。選択肢以外の場所を押す・ポインターが画面から離れると、何も変えずに閉じる。開いている間は選択肢だけを操作できる。
    /// 値をアプリの外が持つ項目（<see cref="ExternalToggleSettingItem"/>）は、値を自分では変えず、押す・「既定に戻す」で変更を求める（<see cref="ExternalChangeRequested"/>）。
    /// 値はアプリが外から読んで渡す（<see cref="SetExternalValue"/>）。渡されるまでと、外が値を扱えない環境では「利用できない」（null）で、押しても何もしない。
    /// 表示（<see cref="View.SettingsView"/>）は <see cref="Version"/> の変化を見て再描画する。
    /// </summary>
    public sealed class SettingsForm : ISettingsPointer
    {
        readonly SettingsLayout _layout;
        readonly Dictionary<ExternalToggleSettingItem, bool?> _external = new Dictionary<ExternalToggleSettingItem, bool?>();
        UserSettings _committed;

        public SettingsForm(SettingsLayout layout, UserSettings initial)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Settings = initial ?? throw new ArgumentNullException(nameof(initial));
            _committed = initial;
            Category = layout.Pages[0].Category;
            foreach (SettingsPageLayout page in layout.Pages)
            {
                foreach (SettingsToggleLayout toggle in page.Toggles)
                {
                    if (toggle.Item is ExternalToggleSettingItem external) _external.Add(external, null);
                }
            }
        }

        public SettingsLayout Layout => _layout;

        /// <summary>表示しているページのカテゴリ。</summary>
        public SettingsCategory Category { get; private set; }

        /// <summary>現在の設定値（スライダーの操作中は、確定前の値）。</summary>
        public UserSettings Settings { get; private set; }

        /// <summary>ポインターが指している操作の対象。</summary>
        public SettingsHit Hover { get; private set; } = SettingsHit.None;

        /// <summary>ドラッグしているスライダーの項目。ドラッグしていなければ null。</summary>
        public NumberSettingItem DraggingItem { get; private set; }

        /// <summary>押したまま離していないボタン・スイッチ・タブ。</summary>
        public SettingsHit Pressed { get; private set; } = SettingsHit.None;

        /// <summary>同意の画面を開いている項目。開いていなければ null。ポインターが画面から離れても閉じない。</summary>
        public ToggleSettingItem ConsentItem { get; private set; }

        /// <summary>選択肢の一覧を開いている項目。開いていなければ null。</summary>
        public ChoiceSettingItem OpenDropdown { get; private set; }

        /// <summary>画面の下部に表示する通知。なければ null。</summary>
        public string Notice { get; private set; }

        /// <summary>表示に関わる状態が変わるたびに増える。</summary>
        public int Version { get; private set; }

        /// <summary>設定値が変わった（確定前を含む）。キーボードの表示と動作へ随時反映するのに使う。</summary>
        public event Action<UserSettings> Changed;

        /// <summary>設定値が確定した。保存に使う。</summary>
        public event Action<UserSettings> Committed;

        /// <summary>「アプリを終了」が押された。</summary>
        public event Action QuitRequested;

        /// <summary>
        /// 値をアプリの外が持つ項目の値を変えるよう求められた（押された・「既定に戻す」）。
        /// フォームの値は変えないため、受けた側は外の値を変えた後、再取得して <see cref="SetExternalValue"/> で渡すこと。
        /// </summary>
        public event Action<ExternalToggleSettingItem, bool> ExternalChangeRequested;

        /// <summary>値をアプリの外が持つ項目の、いまの値。利用できなければ null。この配置にない項目は例外。</summary>
        public bool? GetExternalValue(ExternalToggleSettingItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_external.TryGetValue(item, out bool? value))
                throw new ArgumentException("この配置にない項目です: " + item.Key, nameof(item));
            return value;
        }

        /// <summary>値をアプリの外が持つ項目の値を、外から読んだ値にする。利用できなければ null。この配置にない項目は例外。</summary>
        public void SetExternalValue(ExternalToggleSettingItem item, bool? value)
        {
            if (GetExternalValue(item) == value) return;
            _external[item] = value;
            Version++;
        }

        /// <summary>スイッチがオンか。値をアプリの外が持つ項目で、利用できなければ null。</summary>
        public bool? IsOn(SwitchSettingItem item)
        {
            switch (item)
            {
                case ToggleSettingItem stored:
                    return stored.Get(Settings);
                case ExternalToggleSettingItem external:
                    return GetExternalValue(external);
                case null:
                    throw new ArgumentNullException(nameof(item));
                default:
                    throw new ArgumentException("未知の種類の項目です: " + item.Key, nameof(item));
            }
        }

        public void SetNotice(string notice)
        {
            if (notice == Notice) return;
            Notice = notice;
            Version++;
        }

        public void PointerMove(Vector2 position)
        {
            SetHover(_layout.HitTest(position, Category, ConsentItem, OpenDropdown));
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
                case SettingsTarget.Quit:
                case SettingsTarget.ConsentAccept:
                case SettingsTarget.ConsentCancel:
                case SettingsTarget.Dropdown:
                case SettingsTarget.DropdownOption:
                case SettingsTarget.DropdownDismiss:
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
            if (OpenDropdown != null) SetOpenDropdown(null);
        }

        // スライダーをドラッグしていれば離して確定し、押しているボタンは動作させずに離す。
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
                    if (hit.ToggleItem is ExternalToggleSettingItem external)
                    {
                        bool? on = GetExternalValue(external);
                        if (on.HasValue) ExternalChangeRequested?.Invoke(external, !on.Value);
                        return;
                    }
                    var toggle = (ToggleSettingItem)hit.ToggleItem;
                    if (toggle.RequiresConsentToEnable && !toggle.Get(Settings))
                    {
                        SetConsentItem(toggle);
                        return;
                    }
                    SetSettings(toggle.Toggle(Settings));
                    break;
                case SettingsTarget.Reset:
                    SetSettings(Category.ResetToDefault(Settings));
                    ResetExternalToDefault();
                    break;
                case SettingsTarget.Quit:
                    QuitRequested?.Invoke();
                    return;
                case SettingsTarget.ConsentAccept:
                    ToggleSettingItem accepted = ConsentItem;
                    SetConsentItem(null);
                    SetSettings(accepted.Set(Settings, true));
                    break;
                case SettingsTarget.ConsentCancel:
                    SetConsentItem(null);
                    return;
                case SettingsTarget.Dropdown:
                    SetOpenDropdown(hit.ChoiceItem);
                    return;
                case SettingsTarget.DropdownOption:
                    SetOpenDropdown(null);
                    SetSettings(hit.ChoiceItem.SetIndex(Settings, hit.OptionIndex));
                    break;
                case SettingsTarget.DropdownDismiss:
                    SetOpenDropdown(null);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hit), hit.Target, null);
            }
            Commit();
        }

        // 表示中のカテゴリの、外が値を持つ項目のうち、利用でき、既定値と異なるものの変更を求める。
        void ResetExternalToDefault()
        {
            foreach (SettingItem item in Category.Items)
            {
                if (!(item is ExternalToggleSettingItem external)) continue;
                bool? on = GetExternalValue(external);
                if (on.HasValue && on.Value != external.DefaultValue) ExternalChangeRequested?.Invoke(external, external.DefaultValue);
            }
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

        // 開閉で操作できる対象が入れ替わるため、指している対象は次にポインターが動くまで外す。
        void SetConsentItem(ToggleSettingItem item)
        {
            ConsentItem = item;
            Version++;
            SetHover(SettingsHit.None);
        }

        // 同意の画面と同じく、開閉で操作できる対象が入れ替わるため、指している対象は次にポインターが動くまで外す。
        void SetOpenDropdown(ChoiceSettingItem item)
        {
            OpenDropdown = item;
            Version++;
            SetHover(SettingsHit.None);
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
