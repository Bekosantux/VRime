using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRKeyboard.App.View;

namespace VRKeyboard.App.Tests
{
    /// <summary>シーンに保存した uGUI の構成を、起動時と同じ検証にかける。シーンは保存せずに閉じる。</summary>
    public sealed class MainSceneTests
    {
        const string ScenePath = "Assets/VRKeyboard/Scenes/Main.unity";

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
        public void 盤面の中心はテクスチャの中心より下にある()
        {
            Assert.Greater(Single<KeyboardView>().BoardCenterOffset, 0f);
        }

        T Single<T>() where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in _scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            Assert.AreEqual(1, found.Count, typeof(T).Name + " の数");
            return found[0];
        }
    }
}
