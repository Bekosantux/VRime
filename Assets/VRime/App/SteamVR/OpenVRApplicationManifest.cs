using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// SteamVR にアプリとして登録するためのマニフェスト（<c>.vrmanifest</c>）の内容と書き出し。
    /// SteamVR は登録した app key（<see cref="AppKey"/>）でアプリを識別し、ダッシュボードのオーバーレイとして、SteamVR の起動時に自動で起動する対象にする（docs/spec/lifecycle.md「SteamVR への登録と自動起動」）。
    /// パスはすべて絶対パスで書く。起動のたびに書き直して登録し直すため、フォルダを移動しても次の起動で登録が追従する。
    /// 複数の場所に置いた VRime を起動すると、最後に起動したものが登録される。
    /// </summary>
    public static class OpenVRApplicationManifest
    {
        /// <summary>
        /// SteamVR でのアプリの識別子。自動で起動するかどうかと、利用者が変えたコントローラーのバインドは、このキーに結び付いて SteamVR に保存される。
        /// 変えると、どちらも引き継がれない。
        /// </summary>
        public const string AppKey = "vrime.keyboard";

        /// <summary>書き出す場所（<see cref="AppInfo.DataDirectory"/>）でのファイル名。</summary>
        public const string FileName = "VRime.vrmanifest";

        /// <summary>SteamVR の設定画面などに出す説明。SteamVR の表示言語が英語の場合に使われる。</summary>
        const string Description = "Japanese input keyboard overlay";

        static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>%AppData%\VRime\VRime.vrmanifest</summary>
        public static string DefaultPath => Path.Combine(AppInfo.DataDirectory, FileName);

        /// <summary>マニフェストの JSON を作る。</summary>
        /// <param name="binaryPath">本アプリの exe の絶対パス。</param>
        /// <param name="actionManifestPath">アクションマニフェスト（<c>actions.json</c>）の絶対パス。</param>
        /// <param name="imagePath">SteamVR の一覧に出すアイコン（PNG）の絶対パス。</param>
        public static string Create(string binaryPath, string actionManifestPath, string imagePath)
        {
            RequireAbsolute(binaryPath, nameof(binaryPath));
            RequireAbsolute(actionManifestPath, nameof(actionManifestPath));
            RequireAbsolute(imagePath, nameof(imagePath));

            var application = new JObject
            {
                ["app_key"] = AppKey,
                ["launch_type"] = "binary",
                ["binary_path_windows"] = binaryPath,
                // 自動で起動できるのは、ダッシュボードのオーバーレイとして登録したアプリだけ。
                ["is_dashboard_overlay"] = true,
                ["action_manifest_path"] = actionManifestPath,
                ["image_path"] = imagePath,
                ["strings"] = new JObject
                {
                    ["en_us"] = new JObject
                    {
                        ["name"] = AppInfo.DisplayName,
                        ["description"] = Description,
                    },
                },
            };
            var root = new JObject
            {
                ["source"] = "builtin",
                ["applications"] = new JArray(application),
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>
        /// content を path に書く。同じ内容のファイルがあれば書かない。フォルダがなければ作る。
        /// 読み書きの失敗は例外のまま上げる。
        /// </summary>
        public static void Write(string path, string content)
        {
            RequireAbsolute(path, nameof(path));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (File.Exists(path) && File.ReadAllText(path, Utf8NoBom) == content) return;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, content, Utf8NoBom);
        }

        static void RequireAbsolute(string path, string paramName)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("パスが空です。", paramName);
            if (!Path.IsPathRooted(path)) throw new ArgumentException("絶対パスで指定してください: " + path, paramName);
        }
    }
}
