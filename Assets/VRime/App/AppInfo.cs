using System;
using System.IO;
using UnityEngine;

namespace VRime.App
{
    /// <summary>アプリ全体で揃える情報（利用者向けの表示名・バージョンと、データの保存先）。</summary>
    public static class AppInfo
    {
        /// <summary>
        /// アプリの表示名。SteamVR のオーバーレイとダッシュボードのタブ、タスクトレイのアイコンのツールチップ、設定ウィンドウのタイトルに使う。
        /// </summary>
        public const string DisplayName = "VRime";

        /// <summary>
        /// アプリのバージョン。正本は Player Settings の Version（<c>PlayerSettings.bundleVersion</c>）で、配布用の zip の名前と同じ値になる。
        /// </summary>
        public static string Version
        {
            get
            {
                string version = Application.version;
                if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("Player Settings の Version が空です。");
                return version;
            }
        }

        /// <summary>%AppData%\VRime。設定ファイルと SteamVR へ登録するマニフェストを置く。</summary>
        public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRime");
    }
}
