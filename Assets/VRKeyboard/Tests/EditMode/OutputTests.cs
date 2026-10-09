using System;
using NUnit.Framework;
using VRKeyboard.Core.Output;

namespace VRKeyboard.Core.Tests
{
    public class OscCodecTests
    {
        [Test]
        public void チャットボックス入力のバイト列()
        {
            byte[] actual = OscCodec.Encode("/chatbox/input", "あ", true, true);
            var expected = new byte[]
            {
                // "/chatbox/input" (14 バイト) + NUL 2 つで 16
                0x2F, 0x63, 0x68, 0x61, 0x74, 0x62, 0x6F, 0x78, 0x2F, 0x69, 0x6E, 0x70, 0x75, 0x74, 0x00, 0x00,
                // ",sTT" + NUL 4 つ
                0x2C, 0x73, 0x54, 0x54, 0x00, 0x00, 0x00, 0x00,
                // "あ" (UTF-8 で 3 バイト) + NUL 1 つ
                0xE3, 0x81, 0x82, 0x00,
            };
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void typingのbool()
        {
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x2F, 0x63, 0x68, 0x61, 0x74, 0x62, 0x6F, 0x78, 0x2F, 0x74, 0x79, 0x70, 0x69, 0x6E, 0x67, 0x00, // "/chatbox/typing" (15) + NUL
                    0x2C, 0x46, 0x00, 0x00, // ",F"
                },
                OscCodec.Encode("/chatbox/typing", false));
        }

        [Test]
        public void int32とfloat32はビッグエンディアン()
        {
            CollectionAssert.AreEqual(
                new byte[] { 0x2F, 0x61, 0x00, 0x00, 0x2C, 0x69, 0x66, 0x00, 0x00, 0x00, 0x01, 0x02, 0x3F, 0x80, 0x00, 0x00 },
                OscCodec.Encode("/a", 258, 1.0f));
        }

        [Test]
        public void 文字列の長さが4の倍数のときも終端のNULを付ける()
        {
            // "abcd" は 4 バイトなので、NUL 4 つで次の 4 バイト境界になる。
            CollectionAssert.AreEqual(
                new byte[] { 0x2F, 0x61, 0x00, 0x00, 0x2C, 0x73, 0x00, 0x00, 0x61, 0x62, 0x63, 0x64, 0x00, 0x00, 0x00, 0x00 },
                OscCodec.Encode("/a", "abcd"));
        }

        [Test]
        public void 引数なし()
        {
            CollectionAssert.AreEqual(
                new byte[] { 0x2F, 0x61, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00 },
                OscCodec.Encode("/a"));
        }

        [Test]
        public void 未対応の型と空のアドレスは例外()
        {
            Assert.Throws<ArgumentException>(() => OscCodec.Encode("/a", 1.5));
            Assert.Throws<ArgumentException>(() => OscCodec.Encode("/a", new object[] { null }));
            Assert.Throws<ArgumentException>(() => OscCodec.Encode("", "x"));
        }
    }

    public class ChatboxRateLimiterTests
    {
        [Test]
        public void 最初の送信は待たない()
        {
            var limiter = new ChatboxRateLimiter();
            limiter.Enqueue("a");
            Assert.IsTrue(limiter.TryDequeue(0.0, out string message));
            Assert.AreEqual("a", message);
        }

        [Test]
        public void キューが空なら取り出せない()
        {
            var limiter = new ChatboxRateLimiter();
            Assert.IsFalse(limiter.TryDequeue(100.0, out string message));
            Assert.IsNull(message);
        }

        [Test]
        public void 前回から1秒未満の送信は待たせる()
        {
            var limiter = new ChatboxRateLimiter();
            limiter.Enqueue("a");
            limiter.Enqueue("b");
            Assert.IsTrue(limiter.TryDequeue(10.0, out _));
            Assert.IsFalse(limiter.TryDequeue(10.5, out _));
            Assert.IsFalse(limiter.TryDequeue(10.99, out _));
            Assert.AreEqual(1, limiter.PendingCount);
            Assert.IsTrue(limiter.TryDequeue(11.0, out string message));
            Assert.AreEqual("b", message);
        }

        [Test]
        public void 時間が来たら順に取り出す()
        {
            var limiter = new ChatboxRateLimiter();
            limiter.Enqueue("a");
            limiter.Enqueue("b");
            limiter.Enqueue("c");
            var sent = new System.Collections.Generic.List<string>();
            for (double t = 0.0; t < 5.0; t += 0.25)
            {
                if (limiter.TryDequeue(t, out string message)) sent.Add(message + "@" + t);
            }
            CollectionAssert.AreEqual(new[] { "a@0", "b@1", "c@2" }, sent);
        }

        [Test]
        public void 間隔が空いていれば直ちに送る()
        {
            var limiter = new ChatboxRateLimiter();
            limiter.Enqueue("a");
            limiter.TryDequeue(0.0, out _);
            limiter.Enqueue("b");
            Assert.IsTrue(limiter.TryDequeue(30.0, out string message));
            Assert.AreEqual("b", message);
        }

        [Test]
        public void 不正な引数は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatboxRateLimiter(-1.0));
            Assert.Throws<ArgumentNullException>(() => new ChatboxRateLimiter().Enqueue(null));
        }
    }
}
