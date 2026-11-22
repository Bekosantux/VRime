using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRime.App.View;
using VRime.Core;
using VRime.Core.Input;
using Object = UnityEngine.Object;

namespace VRime.App.Tests
{
    /// <summary>シーンに保存した uGUI の構成を、起動時と同じ検証にかける。シーンは保存せずに閉じる。</summary>
    public sealed class MainSceneTests
    {
        const string ScenePath = "Assets/VRime/Scenes/Main.unity";

        Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void シーンの構成が正しい()
        {
            KeyboardApp app = Single<KeyboardApp>();
            Assert.DoesNotThrow(app.ValidateScene);
        }

        [Test]
        public void ボードの中心はテクスチャの中心より下にある()
        {
            Assert.Greater(Single<KeyboardView>().BoardCenterOffset, 0f);
        }

        /// <summary>
        /// シーンのセルの仮の文字（文字・ヒント）とセルの有効・無効を、実行時に開いた直後の表示と照合する。
        /// 期待値は、既定の設定のセッションの表示を、シーンのパネルに実行時と同じ処理で描画して作る（シーンは保存しない）。
        /// 「貼付」は、主な利用状況である、使える状態（前面のウィンドウが VRChat）に合わせる。
        /// </summary>
        [Test]
        public void グリップのパネルのセルの仮の文字は開いた直後の表示と一致する()
        {
            GripPanelView panels = Single<GripPanelView>();
            Dictionary<string, string> sceneState = CellState(panels);

            var settings = new KeyboardSettings();
            var session = new KeyboardSession(settings, new NullEffects());
            session.SetPasteModeAvailable(true);
            KeyboardTheme theme = ScriptableObject.CreateInstance<KeyboardTheme>();
            try
            {
                panels.Initialize(settings.ConsonantHand, new ActiveTheme(theme));
                panels.Refresh(session.GripInput, false);
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }

            Dictionary<string, string> runtimeState = CellState(panels);
            var differences = new List<string>();
            foreach (KeyValuePair<string, string> pair in runtimeState)
            {
                if (sceneState[pair.Key] != pair.Value)
                    differences.Add(pair.Key + ": シーン「" + sceneState[pair.Key] + "」・実行時「" + pair.Value + "」");
            }
            Assert.IsEmpty(differences, "シーンの仮の文字が実行時の表示と異なります。\n" + string.Join("\n", differences));
        }

        /// <summary>セルの有効・無効と、セルの下の文字（無効なセルの文字は表示されないため含めない）。キーは階層のパス。</summary>
        static Dictionary<string, string> CellState(GripPanelView panels)
        {
            var state = new Dictionary<string, string>();
            foreach (GripPanelCell cell in panels.GetComponentsInChildren<GripPanelCell>(true))
            {
                bool active = cell.gameObject.activeSelf;
                state.Add(SceneChecks.PathOf(cell.transform), active ? "有効" : "無効");
                if (!active) continue;
                foreach (TMP_Text text in cell.GetComponentsInChildren<TMP_Text>(true))
                    state.Add(SceneChecks.PathOf(text.transform), text.text);
            }
            return state;
        }

        T Single<T>() where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in _scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            Assert.AreEqual(1, found.Count, typeof(T).Name + " の数");
            return found[0];
        }

        sealed class NullEffects : IKeyboardEffects
        {
            public void OnOpenChanged(bool isOpen) { }
            public void OnHaptic(Hand hand, HapticKind kind) { }
            public void OnConversionRequested(int requestId, string reading) { }
            public bool OnSendRequested(string text) { return true; }
        }
    }
}
