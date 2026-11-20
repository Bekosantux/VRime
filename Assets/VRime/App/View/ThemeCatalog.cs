using System;
using UnityEngine;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 色のテーマ（<see cref="ColorTheme"/>）ごとのアセット（<see cref="KeyboardTheme"/>）の一覧。`Assets/VRime/UI/Themes/ThemeCatalog.asset`。
    /// テーマを足すときは、<see cref="ColorTheme"/> の値・アセット・設定の選択肢（<see cref="SettingsCatalog"/>）・設定ファイルの名前（<see cref="SettingsSerializer"/>）を揃えて加える。
    /// </summary>
    [CreateAssetMenu(menuName = "VRime/Theme Catalog", fileName = "ThemeCatalog")]
    public sealed class ThemeCatalog : ScriptableObject
    {
        [Tooltip("各テーマのアセット。ColorTheme の値ごとにちょうど 1 つずつ。")]
        [SerializeField] KeyboardTheme[] _themes = Array.Empty<KeyboardTheme>();

        /// <summary>どのテーマにもちょうど 1 つずつアセットがあり、それぞれの値が使える範囲にあることを確かめる。</summary>
        public void Validate()
        {
            if (_themes == null) throw new InvalidOperationException(name + " のテーマの一覧が null です。");
            for (int i = 0; i < _themes.Length; i++)
            {
                if (_themes[i] == null) throw new InvalidOperationException(name + " のテーマの一覧の " + i + " 番目が設定されていません。");
                _themes[i].Validate();
            }
            foreach (ColorTheme id in (ColorTheme[])Enum.GetValues(typeof(ColorTheme)))
            {
                int count = 0;
                foreach (KeyboardTheme theme in _themes)
                {
                    if (theme.Id == id) count++;
                }
                if (count != 1) throw new InvalidOperationException(name + " に " + id + " のテーマが " + count + " 個あります（1 個である必要があります）。");
            }
        }

        public KeyboardTheme Get(ColorTheme id)
        {
            foreach (KeyboardTheme theme in _themes)
            {
                if (theme != null && theme.Id == id) return theme;
            }
            throw new InvalidOperationException(name + " に " + id + " のテーマがありません。");
        }
    }
}
