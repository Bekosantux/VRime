using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRime.App.View;

namespace VRime.Editor
{
    /// <summary>
    /// テーマのアセットのインスペクターに、そのテーマの色をアクティブなシーンに反映するボタンを加える。
    /// 実行中は設定のテーマで描くため、反映できない。
    /// </summary>
    [CustomEditor(typeof(KeyboardTheme))]
    public sealed class KeyboardThemeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(playing))
            {
                if (GUILayout.Button("シーンに反映")) Apply((KeyboardTheme)target);
            }
            if (playing) EditorGUILayout.HelpBox("実行中は設定のテーマで描くため、反映できません。", MessageType.Info);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        static void Apply(KeyboardTheme theme)
        {
            try
            {
                ThemeScenePainter.Paint(SceneManager.GetActiveScene(), theme);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("テーマを反映できません", e.Message, "OK");
            }
        }
    }
}
