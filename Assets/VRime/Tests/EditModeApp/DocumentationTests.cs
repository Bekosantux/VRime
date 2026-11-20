using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VRime.App.Tests
{
    /// <summary>
    /// 文書の参照が切れていないことを確かめる。対象は、文書（README.md・AGENTS.md・ARCHITECTURE.md・docs/ 以下の Markdown）の間のリンクと、
    /// コードのコメントから文書への参照（<c>docs/spec/grip-input.md「長押し（連続入力）」</c> の形式）。
    /// 見出しのアンカーは GitHub と同じ規則で作る。
    /// </summary>
    public sealed class DocumentationTests
    {
        static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static readonly Regex FencedCode = new Regex(@"^```.*?^```", RegexOptions.Multiline | RegexOptions.Singleline);
        static readonly Regex InlineCode = new Regex(@"`[^`\n]*`");
        static readonly Regex MarkdownLink = new Regex(@"\]\(([^)\s]+)\)");
        static readonly Regex Heading = new Regex(@"^#{1,6}\s+(.+?)\s*$", RegexOptions.Multiline);
        static readonly Regex CodeDocumentReference = new Regex(@"([A-Za-z0-9_./-]+\.md)(?:「([^」]+)」)?");

        [Test]
        public void 文書間のリンクが解決する()
        {
            var failures = new List<string>();
            foreach (string document in Documents())
            {
                string text = StripCode(File.ReadAllText(document));
                foreach (Match match in MarkdownLink.Matches(text))
                {
                    string target = match.Groups[1].Value;
                    if (target.StartsWith("http://", StringComparison.Ordinal) || target.StartsWith("https://", StringComparison.Ordinal)
                        || target.StartsWith("mailto:", StringComparison.Ordinal)) continue;

                    int hash = target.IndexOf('#');
                    string path = hash < 0 ? target : target.Substring(0, hash);
                    string anchor = hash < 0 ? null : Uri.UnescapeDataString(target.Substring(hash + 1));
                    string resolved = path.Length == 0 ? document : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(document), Uri.UnescapeDataString(path)));

                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                        failures.Add(Relative(document) + ": リンク先がない: " + target);
                    else if (anchor != null && resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !Anchors(resolved).Contains(anchor))
                        failures.Add(Relative(document) + ": 見出しがない: " + target);
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void コードのコメントの文書の参照が解決する()
        {
            var failures = new List<string>();
            string sourceRoot = Path.Combine(Application.dataPath, "VRime");
            foreach (string source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (source.Replace('\\', '/').Contains("/ThirdParty/")) continue;
                string[] lines = File.ReadAllLines(source);
                for (int i = 0; i < lines.Length; i++)
                {
                    int comment = lines[i].IndexOf("//", StringComparison.Ordinal);
                    if (comment < 0) continue;
                    foreach (Match match in CodeDocumentReference.Matches(lines[i].Substring(comment)))
                    {
                        string path = match.Groups[1].Value;
                        string document = Path.Combine(RepositoryRoot, path);
                        string location = Relative(source) + ":" + (i + 1);
                        if (!File.Exists(document))
                            failures.Add(location + ": 文書がない: " + path);
                        else if (match.Groups[2].Success && !Headings(document).Contains(match.Groups[2].Value))
                            failures.Add(location + ": 見出しがない: " + match.Value);
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [TestCase("SteamVR への登録と自動起動", "steamvr-への登録と自動起動")]
        [TestCase("長押し（連続入力）", "長押し連続入力")]
        [TestCase("`Platform/`・`Input/`", "platforminput")]
        [TestCase("Player Settings", "player-settings")]
        public void アンカーはGitHubと同じ規則で作る(string heading, string expected)
        {
            Assert.AreEqual(expected, ToAnchor(heading));
        }

        static IEnumerable<string> Documents()
        {
            foreach (string name in new[] { "README.md", "AGENTS.md", "ARCHITECTURE.md" })
            {
                string path = Path.Combine(RepositoryRoot, name);
                if (File.Exists(path)) yield return path;
            }
            foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "docs"), "*.md", SearchOption.AllDirectories))
                yield return path;
        }

        static List<string> Headings(string document)
        {
            string text = FencedCode.Replace(File.ReadAllText(document), "");
            return Heading.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }

        /// <summary>見出しのアンカーの一覧。同じアンカーが続くときは、GitHub と同じく 2つ目から -1・-2 … を付ける。</summary>
        static HashSet<string> Anchors(string document)
        {
            var anchors = new HashSet<string>();
            var counts = new Dictionary<string, int>();
            foreach (string heading in Headings(document))
            {
                string anchor = ToAnchor(heading);
                counts.TryGetValue(anchor, out int count);
                anchors.Add(count == 0 ? anchor : anchor + "-" + count);
                counts[anchor] = count + 1;
            }
            return anchors;
        }

        /// <summary>GitHub の見出しのアンカー。小文字にし、文字・数字・結合文字・空白・ハイフン・下線以外を除き、空白をハイフンにする。</summary>
        static string ToAnchor(string heading)
        {
            var builder = new StringBuilder(heading.Length);
            foreach (char c in heading.Trim().ToLowerInvariant())
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                bool keep = char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' '
                    || category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark
                    || category == UnicodeCategory.EnclosingMark;
                if (keep) builder.Append(c == ' ' ? '-' : c);
            }
            return builder.ToString();
        }

        static string StripCode(string markdown) => InlineCode.Replace(FencedCode.Replace(markdown, ""), "");

        static string Relative(string path) => path.Substring(RepositoryRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
    }
}
