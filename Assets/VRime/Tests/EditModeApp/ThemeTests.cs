using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRime.App.Settings;
using VRime.App.View;
using Object = UnityEngine.Object;

namespace VRime.App.Tests
{
    public sealed class ThemeCatalogTests
    {
        const string CatalogPath = "Assets/VRime/UI/Themes/ThemeCatalog.asset";

        static ThemeCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ThemeCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            return catalog;
        }

        [Test]
        public void どのテーマにもアセットがちょうど1つある()
        {
            ThemeCatalog catalog = LoadCatalog();
            Assert.DoesNotThrow(catalog.Validate);
            foreach (ColorTheme id in (ColorTheme[])Enum.GetValues(typeof(ColorTheme)))
                Assert.AreEqual(id, catalog.Get(id).Id);
        }

        [Test]
        public void どのテーマもすべての色の役割に色を持つ()
        {
            ThemeCatalog catalog = LoadCatalog();
            foreach (ColorTheme id in (ColorTheme[])Enum.GetValues(typeof(ColorTheme)))
            {
                KeyboardTheme theme = catalog.Get(id);
                foreach (ThemeColorRole role in (ThemeColorRole[])Enum.GetValues(typeof(ThemeColorRole)))
                    Assert.DoesNotThrow(() => theme.ColorOf(role), id + " / " + role);
            }
        }

        [Test]
        public void 未知の色の役割は例外()
        {
            var theme = ScriptableObject.CreateInstance<KeyboardTheme>();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => theme.ColorOf((ThemeColorRole)999));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }
    }

    public sealed class ActiveThemeTests
    {
        KeyboardTheme _dark;
        KeyboardTheme _lime;

        [SetUp]
        public void SetUp()
        {
            _dark = ScriptableObject.CreateInstance<KeyboardTheme>();
            _lime = ScriptableObject.CreateInstance<KeyboardTheme>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_dark);
            Object.DestroyImmediate(_lime);
        }

        [Test]
        public void 違うテーマに差し替えるとバージョンが増え_同じテーマでは増えない()
        {
            var active = new ActiveTheme(_dark);
            Assert.AreSame(_dark, active.Current);
            int version = active.Version;

            active.Set(_dark);
            Assert.AreEqual(version, active.Version);

            active.Set(_lime);
            Assert.AreSame(_lime, active.Current);
            Assert.Greater(active.Version, version);
        }

        [Test]
        public void テーマがないと例外()
        {
            Assert.Throws<ArgumentNullException>(() => new ActiveTheme(null));
            Assert.Throws<ArgumentNullException>(() => new ActiveTheme(_dark).Set(null));
        }
    }
}
