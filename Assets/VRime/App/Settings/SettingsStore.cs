using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VRime.App.Settings
{
    /// <summary>
    /// 設定ファイルの読み書き（SPEC 8章）。
    /// 内容が正しくないファイルは、別名に移して既定値で起動する（次の保存で上書きされて失われないようにするため）。
    /// ファイルの読み書き自体の失敗（権限など）は例外にする。
    /// </summary>
    public sealed class SettingsStore
    {
        public const string FileName = "settings.json";
        const string InvalidFilePrefix = "settings.invalid-";
        const string InvalidFileExtension = ".json";
        const string TemporaryExtension = ".tmp";

        static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <param name="path">設定ファイルの絶対パス。</param>
        public SettingsStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("設定ファイルのパスが空です。", nameof(path));
            if (!System.IO.Path.IsPathRooted(path)) throw new ArgumentException("設定ファイルのパスは絶対パスである必要があります。", nameof(path));
            Path = path;
        }

        /// <summary>%AppData%\VRime\settings.json</summary>
        public static string DefaultPath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRime", FileName);

        public string Path { get; }

        /// <summary>
        /// 読み込む。ファイルがなければ既定値。内容が正しくなければ、ファイルを
        /// <c>settings.invalid-yyyyMMdd-HHmmss.json</c> に移し、既定値と利用者向けの通知を返す。
        /// </summary>
        public SettingsLoadResult Load()
        {
            if (!File.Exists(Path)) return new SettingsLoadResult(UserSettings.Default, null, null);

            string json = File.ReadAllText(Path, Encoding.UTF8);
            try
            {
                return new SettingsLoadResult(SettingsSerializer.Deserialize(json), null, null);
            }
            catch (FormatException e)
            {
                string moved = MoveInvalidFile();
                string notice = "設定ファイルの内容が正しくないため、既定の設定で起動しました。元のファイルは "
                    + System.IO.Path.GetFileName(moved) + " として残しています。";
                return new SettingsLoadResult(UserSettings.Default, notice, e);
            }
        }

        /// <summary>保存する。書き込みの途中で失敗しても元のファイルが壊れないよう、一時ファイルに書いてから置き換える。</summary>
        public void Save(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            string directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporary = Path + TemporaryExtension;
            File.WriteAllText(temporary, SettingsSerializer.Serialize(settings), Utf8NoBom);
            if (File.Exists(Path)) File.Replace(temporary, Path, null);
            else File.Move(temporary, Path);
        }

        // 同じ秒に複数回移す場合（起動の繰り返しなど）に備え、既存のファイルと重ならない名前を選ぶ。
        string MoveInvalidFile()
        {
            string directory = System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string destination = System.IO.Path.Combine(directory, InvalidFilePrefix + stamp + InvalidFileExtension);
            for (int n = 2; File.Exists(destination); n++)
                destination = System.IO.Path.Combine(directory, InvalidFilePrefix + stamp + "-" + n + InvalidFileExtension);
            File.Move(Path, destination);
            return destination;
        }
    }

    /// <summary><see cref="SettingsStore.Load"/> の結果。</summary>
    public sealed class SettingsLoadResult
    {
        public SettingsLoadResult(UserSettings settings, string notice, FormatException error)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if ((notice == null) != (error == null))
                throw new ArgumentException("通知と原因は、両方を指定するか両方を省略する必要があります。");
            Notice = notice;
            Error = error;
        }

        public UserSettings Settings { get; }

        /// <summary>設定画面に表示する通知。ファイルを読めたときは null。</summary>
        public string Notice { get; }

        /// <summary>ファイルの内容が正しくなかった理由（ログ用）。ファイルを読めたときは null。</summary>
        public FormatException Error { get; }
    }
}
