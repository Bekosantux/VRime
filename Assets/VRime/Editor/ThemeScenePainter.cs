using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRime.App.Settings;
using VRime.App.View;
using VRime.Core;
using VRime.Core.Input;
using Object = UnityEngine.Object;

namespace VRime.Editor
{
    /// <summary>
    /// シーンの uGUI をテーマの色に設定する（エディタでの確認用。Undo に記録する）。
    /// 状態によらない色（<see cref="ThemeColor"/>）に加え、状態で色が変わる部品も、操作していない状態の色に設定する。
    /// 操作していない状態は、セル・チップ・候補は選んでいない状態、タブは表示しているページ（GameObject が有効なページ）だけ選んでいる状態、
    /// スイッチとドロップダウンは既定の設定値（<see cref="UserSettings.Default"/>）、グリップのパネルは既定の子音の手で基本レイヤーを表示している状態とする。
    /// 状態で変わる部品は、ビューのシリアライズフィールドの名前で色の対象を探す。ビューのフィールドや色の扱いを変えたら、ここも合わせる。
    /// 対象が見つからない・同じ部品の色を二重に設定する（<see cref="ThemeColor"/> を状態で変わる部品に付けている）ときは、何も設定せずに例外にする。
    /// </summary>
    public static class ThemeScenePainter
    {
        public static void Paint(Scene scene, KeyboardTheme theme)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("読み込まれたシーンではありません: " + scene.path, nameof(scene));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            theme.Validate();

            var colors = new Dictionary<Graphic, Color>();
            void Set(Graphic graphic, Color color, string source)
            {
                if (colors.ContainsKey(graphic))
                    throw new InvalidOperationException(SceneChecks.PathOf(graphic.transform) + " の色を二重に設定しようとしています（" + source + "）。" +
                        "状態で色が変わる部品に ThemeColor を付けていないか確かめてください。");
                colors.Add(graphic, color);
            }

            foreach (ThemeColor themeColor in All<ThemeColor>(scene))
                Set(themeColor.GetComponent<Graphic>(), theme.ColorOf(themeColor.Role), nameof(ThemeColor));
            AddKeyboard(scene, theme, Set);
            AddSettings(scene, theme, Set);

            var graphics = new List<Graphic>(colors.Keys);
            Undo.RecordObjects(graphics.ToArray(), "テーマをシーンに反映（" + theme.name + "）");
            foreach (KeyValuePair<Graphic, Color> pair in colors) pair.Key.color = pair.Value;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void AddKeyboard(Scene scene, KeyboardTheme theme, Action<Graphic, Color, string> set)
        {
            // 母音側の基本レイヤーの四隅はセルを置かず（背景は透明）、機能レイヤーのヒントだけを示す。
            var vowelCorners = new HashSet<GripPanelCell>();
            string vowelCellsField = new KeyboardSettings().ConsonantHand == Hand.Left ? "_rightCells" : "_leftCells";
            foreach (GripPanelView panels in All<GripPanelView>(scene))
            {
                IReadOnlyList<GripPanelCell> vowelCells = References<GripPanelCell>(panels, vowelCellsField);
                for (int i = 0; i < vowelCells.Count; i++)
                {
                    if (!GripPanelView.HasVowelDirection(i)) vowelCorners.Add(vowelCells[i]);
                }
            }

            foreach (GripPanelCell cell in All<GripPanelCell>(scene))
            {
                set(Field(cell, "_background"), vowelCorners.Contains(cell) ? Color.clear : theme.Cell, nameof(GripPanelCell));
                set(Field(cell, "_label"), theme.CellText, nameof(GripPanelCell));
                set(Field(cell, "_hint"), theme.CellHint, nameof(GripPanelCell));
                foreach (Graphic hint in References<Graphic>(cell, "_directionHints"))
                    set(hint, theme.CellHint, nameof(GripPanelCell));
            }
            foreach (GripPanelChip chip in All<GripPanelChip>(scene))
            {
                set(Field(chip, "_background"), theme.Chip, nameof(GripPanelChip));
                set(Field(chip, "_label"), theme.ChipTextInactive, nameof(GripPanelChip));
            }
            foreach (GripPanelCaption caption in All<GripPanelCaption>(scene))
                set(Field(caption, "_role"), theme.CaptionRole, nameof(GripPanelCaption));
            foreach (CandidateLabel label in All<CandidateLabel>(scene))
                set(Field(label, "_background"), theme.Candidate, nameof(CandidateLabel));
            foreach (InputAreaView input in All<InputAreaView>(scene))
                set(Field(input, "_length"), theme.Length, nameof(InputAreaView));
        }

        static void AddSettings(Scene scene, KeyboardTheme theme, Action<Graphic, Color, string> set)
        {
            var quits = new HashSet<SettingsButton>();
            foreach (SettingsView view in All<SettingsView>(scene)) quits.Add(Reference<SettingsButton>(view, "_quit"));
            foreach (SettingsButton button in All<SettingsButton>(scene))
                set(Field(button, "_background"), quits.Contains(button) ? theme.SettingsQuit : theme.SettingsButton, nameof(SettingsButton));

            foreach (SettingsPage page in All<SettingsPage>(scene))
            {
                SettingsTab tab = Reference<SettingsTab>(page, "_tab");
                set(Field(tab, "_background"), page.gameObject.activeSelf ? theme.SettingsTabSelected : theme.SettingsTab, nameof(SettingsTab));
            }
            foreach (SettingsRow row in All<SettingsRow>(scene))
            {
                set(Field(row, "_thumb"), theme.SettingsThumbRing, nameof(SettingsRow));
                set(Field(row, "_thumbFill"), theme.SettingsThumb, nameof(SettingsRow));
            }
            foreach (SettingsToggleRow row in All<SettingsToggleRow>(scene))
            {
                bool on = SettingsCatalog.FindSwitch(row.Key) switch
                {
                    ToggleSettingItem stored => stored.Get(UserSettings.Default),
                    ExternalToggleSettingItem external => external.DefaultValue,
                    _ => throw new InvalidOperationException("未知の種類の項目です: " + row.Key),
                };
                set(Field(row, "_switch"), on ? theme.SettingsSwitchOn : theme.SettingsSwitchOff, nameof(SettingsToggleRow));
                set(Field(row, "_knob"), theme.SettingsThumb, nameof(SettingsToggleRow));
            }
            foreach (SettingsDropdownRow row in All<SettingsDropdownRow>(scene))
            {
                set(Field(row, "_button"), theme.SettingsButton, nameof(SettingsDropdownRow));
                int selected = SettingsCatalog.FindChoice(row.Key).GetIndex(UserSettings.Default);
                IReadOnlyList<SettingsDropdownOption> options = References<SettingsDropdownOption>(row, "_options");
                for (int i = 0; i < options.Count; i++)
                    set(Field(options[i], "_background"), i == selected ? theme.SettingsOptionSelected : theme.SettingsOption, nameof(SettingsDropdownOption));
            }
        }

        // 非表示のものと、複製のテンプレートを含む。
        static List<T> All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        static Graphic Field(Component owner, string field) => Reference<Graphic>(owner, field);

        static T Reference<T>(Component owner, string field) where T : Object
        {
            SerializedProperty property = Property(owner, field);
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException(owner.GetType().Name + " の " + field + " が参照ではありません。");
            return property.objectReferenceValue as T
                ?? throw new InvalidOperationException(SceneChecks.PathOf(owner.transform) + " の " + field + " が " + typeof(T).Name + " を参照していません。");
        }

        static IReadOnlyList<T> References<T>(Component owner, string field) where T : Object
        {
            SerializedProperty property = Property(owner, field);
            if (!property.isArray) throw new InvalidOperationException(owner.GetType().Name + " の " + field + " が配列ではありません。");
            var values = new List<T>(property.arraySize);
            for (int i = 0; i < property.arraySize; i++)
            {
                values.Add(property.GetArrayElementAtIndex(i).objectReferenceValue as T
                    ?? throw new InvalidOperationException(SceneChecks.PathOf(owner.transform) + " の " + field + "[" + i + "] が " + typeof(T).Name + " を参照していません。"));
            }
            return values;
        }

        static SerializedProperty Property(Component owner, string field)
        {
            return new SerializedObject(owner).FindProperty(field)
                ?? throw new InvalidOperationException(owner.GetType().Name + " に " + field + " がありません（フィールドの名前を変えたら、" + nameof(ThemeScenePainter) + " も合わせてください）。");
        }
    }
}
