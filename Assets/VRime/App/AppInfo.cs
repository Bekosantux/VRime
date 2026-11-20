using System;
using System.IO;

namespace VRime.App
{
    /// <summary>アプリ全体で揃える情報（利用者向けの表示名と、データの保存先）。</summary>
    public static class AppInfo
    {
        /// <summary>
        /// アプリの表示名。SteamVR のオーバーレイとダッシュボードのタブ、タスクトレイのアイコンのツールチップ、設定ウィンドウのタイトルに使う。
        /// </summary>
        public const string DisplayName = "VRime";

        /// <summary>%AppData%\VRime。設定ファイルと SteamVR へ登録するマニフェストを置く。</summary>
        public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRime");
    }
}
