using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRime.Editor.Build
{
    /// <summary>
    /// Build Profile を指定してプレイヤーをビルドする。
    /// スクリプトのバックエンドや Managed Stripping Level はプロジェクトの Player Settings で決まり、
    /// プロファイルは Development Build などのビルドの設定だけを持つ。ビルドの出力先はプロファイルに保存されないため、ここで決める。
    /// </summary>
    public static class PlayerBuilder
    {
        const string DevelopmentProfilePath = "Assets/Settings/Build Profiles/Windows Development.asset";
        const string ReleaseProfilePath = "Assets/Settings/Build Profiles/Windows Release.asset";

        /// <summary>出力先の親（プロジェクト直下。.gitignore で管理外）。</summary>
        const string BuildsRoot = "Builds";

        /// <summary>
        /// 出力に含まれるが、配布物に入れてはいけないフォルダーの接尾辞。
        /// IL2CPP のビルドが出すデバッグ用のシンボルと生成された C++、Burst のデバッグ情報。
        /// </summary>
        static readonly string[] DoNotShipSuffixes =
        {
            "_BackUpThisFolder_ButDontShipItWithYourGame",
            "_BurstDebugInformation_DoNotShip",
        };

        /// <summary>配布物に同梱するライセンス文（プロジェクト直下からのパス → zip 内のパス）。</summary>
        static readonly (string Source, string Entry)[] Licenses =
        {
            ("LICENSE", "LICENSE.txt"),
            ("Assets/VRime/ThirdParty/OpenVR/LICENSE", "ThirdPartyLicenses/OpenVR-LICENSE.txt"),
            ("Assets/VRime/Fonts/OFL.txt", "ThirdPartyLicenses/NotoSansJP-OFL.txt"),
        };

        [MenuItem("VRime/Build/Development")]
        static void BuildDevelopmentMenu()
        {
            RunFromMenu(() =>
            {
                string exe = Build(DevelopmentProfilePath, Path.Combine(BuildsRoot, "Development"));
                EditorUtility.RevealInFinder(exe);
            });
        }

        [MenuItem("VRime/Build/Release")]
        static void BuildReleaseMenu()
        {
            RunFromMenu(() =>
            {
                string zip = BuildRelease();
                EditorUtility.RevealInFinder(zip);
            });
        }

        /// <summary>Release のプロファイルでビルドし、配布用の zip を作る。作った zip のパスを返す。</summary>
        public static string BuildRelease()
        {
            string version = PlayerSettings.bundleVersion;
            if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("Player Settings の Version が空です。");

            string outputDir = Path.Combine(BuildsRoot, "Release");
            Build(ReleaseProfilePath, outputDir);

            string zipPath = Path.Combine(BuildsRoot, PlayerSettings.productName + "-v" + version + "-win64.zip");
            CreateZip(outputDir, zipPath);
            return Path.GetFullPath(zipPath);
        }

        /// <summary>
        /// プロファイルでビルドし、exe のパスを返す。出力先のフォルダーは空にしてからビルドする（前回の出力が残らないようにする）。
        /// </summary>
        public static string Build(string profilePath, string outputDir)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null) throw new InvalidOperationException("Build Profile が見つかりません: " + profilePath);

            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            string exePath = Path.GetFullPath(Path.Combine(outputDir, PlayerSettings.productName + ".exe"));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = exePath,
                options = BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("ビルドに失敗しました（" + summary.result + "）: " + profilePath);

            Debug.Log("ビルドしました: " + exePath + "（" + FormatMegabytes(summary.totalSize) + "）");
            return exePath;
        }

        /// <summary>出力先の中身とライセンス文を、製品名のフォルダーにまとめて zip にする（展開した場所にファイルが散らばらないようにする）。</summary>
        static void CreateZip(string outputDir, string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);

            string root = Path.GetFullPath(outputDir);
            string folder = PlayerSettings.productName + "/";
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string entry = file.Substring(root.Length + 1).Replace('\\', '/');
                    string topLevel = entry.Split('/')[0];
                    if (DoNotShipSuffixes.Any(suffix => topLevel.EndsWith(suffix, StringComparison.Ordinal))) continue;
                    archive.CreateEntryFromFile(file, folder + entry, System.IO.Compression.CompressionLevel.Optimal);
                }

                foreach ((string source, string entry) in Licenses)
                {
                    if (!File.Exists(source)) throw new FileNotFoundException("同梱するライセンス文が見つかりません。", source);
                    archive.CreateEntryFromFile(source, folder + entry, System.IO.Compression.CompressionLevel.Optimal);
                }
            }

            Debug.Log("配布用の zip を作りました: " + Path.GetFullPath(zipPath) + "（" + FormatMegabytes((ulong)new FileInfo(zipPath).Length) + "）");
        }

        static void RunFromMenu(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("ビルドに失敗しました", e.Message, "OK");
            }
        }

        static string FormatMegabytes(ulong bytes) => (bytes / 1024.0 / 1024.0).ToString("0.0") + "MB";
    }
}
