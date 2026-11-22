using System;
using NUnit.Framework;
using VRime.Core.Output;

namespace VRime.Core.Tests
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

    public class ChatboxOutboxTests
    {
        [Test]
        public void 最初の送信は待たない()
        {
            var outbox = new ChatboxOutbox();
            outbox.EnqueueSend("a");
            Assert.IsTrue(outbox.TryDequeue(0.0, out ChatboxMessage message));
            Assert.AreEqual(ChatboxMessageKind.Send, message.Kind);
            Assert.AreEqual("a", message.Text);
        }

        [Test]
        public void 何もなければ取り出せない()
        {
            var outbox = new ChatboxOutbox();
            Assert.IsFalse(outbox.TryDequeue(100.0, out _));
        }

        [Test]
        public void 前回から1秒未満の送信は待たせる()
        {
            var outbox = new ChatboxOutbox();
            outbox.EnqueueSend("a");
            outbox.EnqueueSend("b");
            Assert.IsTrue(outbox.TryDequeue(10.0, out _));
            Assert.IsFalse(outbox.TryDequeue(10.5, out _));
            Assert.IsFalse(outbox.TryDequeue(10.99, out _));
            Assert.AreEqual(1, outbox.PendingSendCount);
            Assert.IsTrue(outbox.TryDequeue(11.0, out ChatboxMessage message));
            Assert.AreEqual("b", message.Text);
        }

        [Test]
        public void 時間が来たら順に取り出す()
        {
            var outbox = new ChatboxOutbox();
            outbox.EnqueueSend("a");
            outbox.EnqueueSend("b");
            outbox.EnqueueSend("c");
            CollectionAssert.AreEqual(new[] { "Send:a@0", "Send:b@1", "Send:c@2" }, Drain(outbox, 0.0, 5.0));
        }

        [Test]
        public void 間隔が空いていれば直ちに送る()
        {
            var outbox = new ChatboxOutbox();
            outbox.EnqueueSend("a");
            outbox.TryDequeue(0.0, out _);
            outbox.EnqueueSend("b");
            Assert.IsTrue(outbox.TryDequeue(30.0, out ChatboxMessage message));
            Assert.AreEqual("b", message.Text);
        }

        [Test]
        public void 入力中の表示は最新の1件だけを出す()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            Assert.IsTrue(outbox.TryDequeue(0.0, out ChatboxMessage first));
            Assert.AreEqual(ChatboxMessageKind.Preview, first.Kind);
            Assert.AreEqual("あ", first.Text);

            outbox.SetPreview("あい");
            Assert.IsFalse(outbox.TryDequeue(0.5, out _));
            outbox.SetPreview("あいう");
            CollectionAssert.AreEqual(new[] { "Preview:あいう@1" }, Drain(outbox, 0.75, 5.0));
        }

        [Test]
        public void 出したものと同じ入力中の表示は出さない()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.SetPreview("あ");
            Assert.IsFalse(outbox.HasPendingPreview);
            Assert.IsFalse(outbox.TryDequeue(5.0, out _));

            // 出す前に元に戻った場合も出さない。
            outbox.SetPreview("あい");
            outbox.SetPreview("あ");
            Assert.IsFalse(outbox.TryDequeue(10.0, out _));
        }

        [Test]
        public void 表示中の入力中の表示は空文字列で消す()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.SetPreview(null);
            Assert.IsTrue(outbox.TryDequeue(1.0, out ChatboxMessage message));
            Assert.AreEqual(ChatboxMessageKind.Preview, message.Kind);
            Assert.AreEqual("", message.Text);
            Assert.IsFalse(outbox.TryDequeue(5.0, out _));
        }

        [Test]
        public void 何も表示していなければ消去を出さない()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview(null);
            Assert.IsFalse(outbox.TryDequeue(0.0, out _));
        }

        [Test]
        public void 送信は入力中の表示より先に出す()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.EnqueueSend("a");
            outbox.EnqueueSend("b");
            outbox.SetPreview("c");
            CollectionAssert.AreEqual(new[] { "Send:a@1", "Send:b@2", "Preview:c@3" }, Drain(outbox, 0.25, 5.0));
        }

        [Test]
        public void 送信すると入力中の表示を取り下げ_消去も出さない()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.SetPreview("あい");
            outbox.EnqueueSend("あい");
            outbox.SetPreview(null);
            Assert.IsFalse(outbox.HasPendingPreview);
            CollectionAssert.AreEqual(new[] { "Send:あい@1" }, Drain(outbox, 0.25, 5.0));
        }

        [Test]
        public void 表示の消去は送信と同じ順序で出し入力中の表示を取り下げる()
        {
            var outbox = new ChatboxOutbox();
            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.EnqueueSend("あ");
            outbox.EnqueueClear();
            Assert.IsFalse(outbox.HasPendingPreview);
            Assert.AreEqual(2, outbox.PendingSendCount);
            CollectionAssert.AreEqual(new[] { "Send:あ@1", "Clear:@2" }, Drain(outbox, 0.25, 5.0));
        }

        [Test]
        public void 表示中の入力中の表示だけを直ちに消す()
        {
            var outbox = new ChatboxOutbox();
            Assert.IsFalse(outbox.TryClearPreviewNow());

            outbox.SetPreview("あ");
            outbox.TryDequeue(0.0, out _);
            outbox.SetPreview("あい");
            Assert.IsTrue(outbox.TryClearPreviewNow());
            Assert.IsFalse(outbox.HasPendingPreview);
            Assert.IsFalse(outbox.TryClearPreviewNow());
            Assert.IsFalse(outbox.TryDequeue(5.0, out _));
        }

        [Test]
        public void 不正な引数は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatboxOutbox(-1.0));
            Assert.Throws<ArgumentNullException>(() => new ChatboxOutbox().EnqueueSend(null));
            Assert.Throws<ArgumentException>(() => new ChatboxOutbox().EnqueueSend(""));
            Assert.Throws<ArgumentException>(() => new ChatboxOutbox().SetPreview(""));
        }

        static string[] Drain(ChatboxOutbox outbox, double from, double to)
        {
            var sent = new System.Collections.Generic.List<string>();
            for (double t = from; t < to; t += 0.25)
            {
                if (outbox.TryDequeue(t, out ChatboxMessage message)) sent.Add(message.Kind + ":" + message.Text + "@" + t);
            }
            return sent.ToArray();
        }
    }
}
