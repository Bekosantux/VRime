using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VRime.Editor.Build
{
    /// <summary>
    /// 開発用のツールがプロジェクトに置く DLL を、エディター専用に保つ。
    /// 対象のフォルダーは git の管理外で、ツールが既定のインポート設定（全プラットフォーム）で置くため、
    /// .meta を手動で修正しても、再配置されるたびに戻る。そこで、インポートのたびにエディター専用へ変更し、
    /// ビルドの前にも検査して、Player に含まれる設定ならビルドを止める。
    /// </summary>
    public static class EditorOnlyPlugins
    {
        /// <summary>
        /// エディター専用にするフォルダー（末尾は "/"）。
        /// Assets/Plugins/Roslyn/: MCP for Unity の RoslynInstaller が置く Roslyn と依存 DLL（execute_code 用）。
        /// </summary>
        static readonly string[] Folders =
        {
            "Assets/Plugins/Roslyn/",
        };

        public static bool IsTarget(string assetPath)
        {
            if (!assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
            return Folders.Any(folder => assetPath.StartsWith(folder, StringComparison.Ordinal));
        }

        public static bool IsIncludedIn(PluginImporter importer, BuildTarget target)
        {
            return importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithPlatform(target);
        }

        public static void MakeEditorOnly(PluginImporter importer)
        {
            importer.ClearSettings();
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
        }

        public static IEnumerable<PluginImporter> FindTargets()
        {
            return PluginImporter.GetAllImporters().Where(importer => IsTarget(importer.assetPath));
        }
    }

    /// <summary>対象の DLL がインポートされたら、エディター専用に変更して保存する。</summary>
    sealed class EditorOnlyPluginPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets.Concat(movedAssets))
            {
                if (!EditorOnlyPlugins.IsTarget(path)) continue;
                if (!(AssetImporter.GetAtPath(path) is PluginImporter importer)) continue;
                if (!EditorOnlyPlugins.IsIncludedIn(importer, BuildTarget.StandaloneWindows64) && importer.GetCompatibleWithEditor()) continue;

                EditorOnlyPlugins.MakeEditorOnly(importer);
                importer.SaveAndReimport();
            }
        }
    }

    /// <summary>対象の DLL がビルド先の Player に含まれる設定なら、ビルドを止める。</summary>
    sealed class EditorOnlyPluginBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string[] included = EditorOnlyPlugins.FindTargets()
                .Where(importer => EditorOnlyPlugins.IsIncludedIn(importer, target))
                .Select(importer => importer.assetPath)
                .ToArray();
            if (included.Length == 0) return;

            throw new BuildFailedException(
                "エディター専用であるべき DLL が Player に含まれる設定です。インポート設定をエディター専用にしてください:\n"
                + string.Join("\n", included));
        }
    }
}
