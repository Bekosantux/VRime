using System;
using NUnit.Framework;
using VRime.Core.Conversion;

namespace VRime.Core.Tests
{
    public class TransliterateApiTests
    {
        const string SampleJson =
            "[[\"きょうは\",[\"今日は\",\"きょうは\",\"教は\",\"強は\",\"凶は\"]]," +
            "[\"いいてんき\",[\"いい天気\",\"良い天気\",\"イイ天気\",\"好い天気\",\"いいてんき\"]]]";

        [Test]
        public void リクエストURLは読みをUTF8でパーセントエンコードする()
        {
            Assert.AreEqual(
                "https://www.google.com/transliterate?langpair=ja-Hira%7Cja&text=%E3%81%8D%E3%82%87%E3%81%86%E3%81%AF",
                TransliterateApi.BuildRequestUrl("きょうは"));
        }

        [Test]
        public void 文節区切りのカンマもエンコードする()
        {
            string url = TransliterateApi.BuildRequestUrl("あ,い");
            StringAssert.EndsWith("text=%E3%81%82%2C%E3%81%84", url);
        }

        [Test]
        public void 空の読みは例外()
        {
            Assert.Throws<ArgumentException>(() => TransliterateApi.BuildRequestUrl(""));
            Assert.Throws<ArgumentException>(() => TransliterateApi.BuildRequestUrl(null));
        }

        [Test]
        public void 仕様の応答例を解析する()
        {
            var segments = TransliterateApi.ParseResponse(SampleJson);
            Assert.AreEqual(2, segments.Count);
            Assert.AreEqual("きょうは", segments[0].Reading);
            CollectionAssert.AreEqual(new[] { "今日は", "きょうは", "教は", "強は", "凶は" }, segments[0].Candidates);
            Assert.AreEqual("いいてんき", segments[1].Reading);
            CollectionAssert.AreEqual(new[] { "いい天気", "良い天気", "イイ天気", "好い天気", "いいてんき" }, segments[1].Candidates);
        }

        [Test]
        public void 空白を含むJSONも解析できる()
        {
            var segments = TransliterateApi.ParseResponse(" [ [ \"あ\" , [ \"亜\" ] ] ] \n");
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("亜", segments[0].Candidates[0]);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[[\"あ\",[\"亜\"]")]
        [TestCase("{}")]
        [TestCase("\"文字列\"")]
        [TestCase("[]")]
        [TestCase("[1]")]
        [TestCase("[[\"あ\"]]")]
        [TestCase("[[\"あ\",[\"亜\"],1]]")]
        [TestCase("[[1,[\"亜\"]]]")]
        [TestCase("[[\"あ\",\"亜\"]]")]
        [TestCase("[[\"あ\",[]]]")]
        [TestCase("[[\"あ\",[1]]]")]
        [TestCase("[[\"あ\",[null]]]")]
        public void 不正な応答はFormatException(string json)
        {
            Assert.Throws<FormatException>(() => TransliterateApi.ParseResponse(json));
        }

        [Test]
        public void nullは例外()
        {
            Assert.Throws<ArgumentNullException>(() => TransliterateApi.ParseResponse(null));
        }
    }

    public class ConversionSessionTests
    {
        static ConversionSegment Segment(string reading, params string[] candidates)
        {
            return new ConversionSegment(reading, candidates);
        }

        static ConversionSession Create()
        {
            return new ConversionSession(new[]
            {
                Segment("きょうは", "今日は", "きょうは", "教は"),
                Segment("いいてんき", "いい天気", "良い天気"),
            });
        }

        [Test]
        public void 初期選択は先頭の文節の第1候補()
        {
            ConversionSession session = Create();
            Assert.AreEqual(0, session.SelectedSegment);
            Assert.AreEqual(0, session.SelectedCandidate(0));
            Assert.AreEqual(0, session.SelectedCandidate(1));
            Assert.AreEqual("今日はいい天気", session.Result);
        }

        [Test]
        public void 候補は文節ごとに記憶する()
        {
            ConversionSession session = Create();
            Assert.IsTrue(session.MoveCandidate(1));
            Assert.IsTrue(session.MoveSegment(1));
            Assert.IsTrue(session.MoveCandidate(1));
            Assert.IsTrue(session.MoveSegment(-1));
            Assert.AreEqual(1, session.SelectedCandidate(0));
            Assert.AreEqual(1, session.SelectedCandidate(1));
            Assert.AreEqual("きょうは良い天気", session.Result);
        }

        [Test]
        public void 文節の移動は端で止まる()
        {
            ConversionSession session = Create();
            Assert.IsFalse(session.MoveSegment(-1));
            Assert.AreEqual(0, session.SelectedSegment);
            Assert.IsTrue(session.MoveSegment(1));
            Assert.IsFalse(session.MoveSegment(1));
            Assert.AreEqual(1, session.SelectedSegment);
        }

        [Test]
        public void 候補の移動は端で止まる()
        {
            ConversionSession session = Create();
            Assert.IsFalse(session.MoveCandidate(-1));
            Assert.IsTrue(session.MoveCandidate(1));
            Assert.IsTrue(session.MoveCandidate(1));
            Assert.IsFalse(session.MoveCandidate(1));
            Assert.AreEqual(2, session.SelectedCandidate(0));
            Assert.AreEqual("教はいい天気", session.Result);
        }

        [Test]
        public void 動いたときだけVersionが増える()
        {
            ConversionSession session = Create();
            int version = session.Version;
            session.MoveCandidate(-1);
            Assert.AreEqual(version, session.Version);
            session.MoveCandidate(1);
            Assert.Greater(session.Version, version);
        }

        [Test]
        public void 範囲外の文節番号と空の文節は例外()
        {
            ConversionSession session = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SelectedCandidate(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SelectedCandidate(-1));
            Assert.Throws<ArgumentException>(() => new ConversionSession(new ConversionSegment[0]));
            Assert.Throws<ArgumentException>(() => new ConversionSegment("あ", new string[0]));
        }

        [Test]
        public void 代わりの候補はひらがなのままとカタカナ()
        {
            ConversionSession session = ConversionSession.CreateFallback("きょう");
            Assert.AreEqual(1, session.Segments.Count);
            CollectionAssert.AreEqual(new[] { "きょう", "キョウ" }, session.Segments[0].Candidates);
            Assert.AreEqual("きょう", session.Result);
            session.MoveCandidate(1);
            Assert.AreEqual("キョウ", session.Result);
        }

        [Test]
        public void 代わりの候補でカタカナが同じなら1件()
        {
            ConversionSession session = ConversionSession.CreateFallback("ー");
            CollectionAssert.AreEqual(new[] { "ー" }, session.Segments[0].Candidates);
            Assert.Throws<ArgumentException>(() => ConversionSession.CreateFallback(""));
        }
    }
}
