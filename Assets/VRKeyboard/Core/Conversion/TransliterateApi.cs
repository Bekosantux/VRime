using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRKeyboard.Core.Conversion
{
    /// <summary>Google CGI API for Japanese Input（SPEC 6.1）のリクエストの組み立てと応答の解析。</summary>
    public static class TransliterateApi
    {
        // langpair の「|」は URL として正しい %7C にしておく（サーバーは同じ値として解釈する）。
        const string UrlPrefix = "https://www.google.com/transliterate?langpair=ja-Hira%7Cja&text=";

        public static string BuildRequestUrl(string reading)
        {
            if (string.IsNullOrEmpty(reading)) throw new ArgumentException("読みが空です。", nameof(reading));
            return UrlPrefix + Uri.EscapeDataString(reading);
        }

        /// <summary>応答 JSON を文節の一覧にする。想定外の形式は FormatException。</summary>
        public static IReadOnlyList<ConversionSegment> ParseResponse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException("応答が JSON として解析できません。", e);
            }

            if (!(root is JArray segments) || segments.Count == 0)
                throw new FormatException("応答が文節の配列ではありません。");

            var result = new List<ConversionSegment>(segments.Count);
            foreach (JToken segment in segments)
            {
                if (!(segment is JArray pair) || pair.Count != 2
                    || pair[0].Type != JTokenType.String || !(pair[1] is JArray candidates))
                {
                    throw new FormatException("文節が［読み, 候補の配列］の形式ではありません。");
                }
                if (candidates.Count == 0)
                    throw new FormatException("候補が空の文節があります。");

                var list = new List<string>(candidates.Count);
                foreach (JToken candidate in candidates)
                {
                    if (candidate.Type != JTokenType.String)
                        throw new FormatException("候補が文字列ではありません。");
                    list.Add((string)candidate);
                }
                result.Add(new ConversionSegment((string)pair[0], list));
            }
            return result;
        }
    }
}
