using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using VRKeyboard.App.Conversion;
using VRKeyboard.App.Output;
using VRKeyboard.Core.Output;

namespace VRKeyboard.App.Tests
{
    public sealed class AppSettingsTests
    {
        [Test]
        public void 既定値は仕様どおり()
        {
            var settings = new AppSettings();
            Assert.IsTrue(settings.PlayChatboxNotificationSound);
            Assert.IsFalse(settings.PressEnterAfterDesktopSend);
            Assert.AreEqual("127.0.0.1", settings.OscHost);
            Assert.AreEqual(9000, settings.OscPort);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(65536)]
        public void 範囲外のポートは例外(int port)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AppSettings().OscPort = port);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("bad host!")]
        public void 不正なホストは例外(string host)
        {
            Assert.Throws<ArgumentException>(() => new AppSettings().OscHost = host);
        }
    }

    public sealed class ConversionFailedExceptionTests
    {
        [TestCase(ConversionFailureKind.Connection)]
        [TestCase(ConversionFailureKind.Timeout)]
        [TestCase(ConversionFailureKind.HttpError)]
        [TestCase(ConversionFailureKind.InvalidResponse)]
        public void 種類ごとに異なるユーザー向けメッセージを持つ(ConversionFailureKind kind)
        {
            var e = new ConversionFailedException(kind, "開発用");
            Assert.AreEqual(kind, e.Kind);
            Assert.IsNotEmpty(e.UserMessage);
            Assert.AreNotEqual(e.Message, e.UserMessage);
        }

        [Test]
        public void ユーザー向けメッセージは種類ごとに重複しない()
        {
            var messages = new HashSet<string>();
            foreach (ConversionFailureKind kind in Enum.GetValues(typeof(ConversionFailureKind)))
                Assert.IsTrue(messages.Add(new ConversionFailedException(kind, "x").UserMessage));
        }

        [Test]
        public void クライアントのタイムアウトは正の値のみ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoogleTransliterateClient(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoogleTransliterateClient(float.NaN));
            Assert.AreEqual(2f, GoogleTransliterateClient.DefaultTimeoutSeconds);
        }
    }

    public sealed class TextOutputRouterTests
    {
        sealed class FakeChatbox : IChatboxOutput
        {
            public readonly List<string> Sent = new List<string>();
            public void Send(string text) { Sent.Add(text); }
        }

        sealed class FakeDesktop : IDesktopOutput
        {
            public DesktopSendResult Result = DesktopSendResult.Sent;
            public readonly List<string> Sent = new List<string>();
            public bool LastPressEnter;
            public DesktopSendResult Send(ForegroundWindowInfo target, string text, bool pressEnter)
            {
                Sent.Add(text);
                LastPressEnter = pressEnter;
                return Result;
            }
        }

        const int OwnPid = 100;

        static ForegroundWindowInfo Window(int pid, string name, string display = null)
        {
            return new ForegroundWindowInfo(new IntPtr(1), pid, name, display, "title");
        }

        static TextOutputRouter Router(AppSettings settings, FakeChatbox chatbox, FakeDesktop desktop, ForegroundWindowInfo window)
        {
            return new TextOutputRouter(settings, chatbox, desktop, () => window, OwnPid);
        }

        [Test]
        public void VRChatが前面ならチャットボックスへ送る()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            OutputResult result = Router(new AppSettings(), chatbox, desktop, Window(200, "vrchat")).Send("こんにちは");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("VRChat に送信しました", result.Notice);
            CollectionAssert.AreEqual(new[] { "こんにちは" }, chatbox.Sent);
            Assert.AreEqual(0, desktop.Sent.Count);
        }

        [Test]
        public void それ以外はデスクトップへ送り設定のEnterを渡す()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            var settings = new AppSettings { PressEnterAfterDesktopSend = true };
            OutputResult result = Router(settings, chatbox, desktop, Window(200, "notepad", "メモ帳")).Send("a");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("メモ帳 に送信しました", result.Notice);
            Assert.IsTrue(desktop.LastPressEnter);
            Assert.AreEqual(0, chatbox.Sent.Count);
        }

        [Test]
        public void 表示名がなければプロセス名を通知に使う()
        {
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, "notepad")).Send("a");
            Assert.AreEqual("notepad に送信しました", result.Notice);
        }

        [Test]
        public void プロセス名が不明でも通知を作れる()
        {
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, null)).Send("a");
            Assert.IsTrue(result.Success);
            Assert.IsNotEmpty(result.Notice);
        }

        [Test]
        public void 前面が自分自身なら送らない()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            OutputResult result = Router(new AppSettings(), chatbox, desktop, Window(OwnPid, "Unity")).Send("a");
            Assert.AreEqual(OutputFailure.OwnWindow, result.Failure);
            Assert.AreEqual(0, desktop.Sent.Count);
            Assert.AreEqual(0, chatbox.Sent.Count);
        }

        [Test]
        public void 前面のウィンドウがなければ送らない()
        {
            var desktop = new FakeDesktop();
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, null).Send("a");
            Assert.AreEqual(OutputFailure.NoWindow, result.Failure);
            Assert.AreEqual(0, desktop.Sent.Count);
        }

        [Test]
        public void 管理者権限のウィンドウは失敗として通知する()
        {
            var desktop = new FakeDesktop { Result = DesktopSendResult.TargetElevated };
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "taskmgr")).Send("a");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(OutputFailure.ElevatedWindow, result.Failure);
        }

        [Test]
        public void 送信APIの失敗は失敗として通知する()
        {
            var desktop = new FakeDesktop { Result = DesktopSendResult.Failed };
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "notepad")).Send("a");
            Assert.AreEqual(OutputFailure.SendFailed, result.Failure);
        }

        [Test]
        public void 通知に送信内容を含めない()
        {
            const string secret = "ひみつの文章";
            foreach (DesktopSendResult r in Enum.GetValues(typeof(DesktopSendResult)))
            {
                var desktop = new FakeDesktop { Result = r };
                OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "notepad")).Send(secret);
                StringAssert.DoesNotContain(secret, result.Notice);
            }
        }

        [Test]
        public void 空の文字列は例外()
        {
            var router = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, "notepad"));
            Assert.Throws<ArgumentException>(() => router.Send(""));
        }
    }

    public sealed class VRChatChatboxTests
    {
        UdpClient _receiver;
        AppSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            _receiver.Client.ReceiveTimeout = 1000;
            _settings = new AppSettings { OscPort = ((IPEndPoint)_receiver.Client.LocalEndPoint).Port };
        }

        [TearDown]
        public void TearDown()
        {
            _receiver.Dispose();
        }

        byte[] Receive()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            return _receiver.Receive(ref from);
        }

        bool HasPending()
        {
            return _receiver.Client.Poll(200 * 1000, SelectMode.SelectRead);
        }

        [Test]
        public void 送信は間隔を空けて順に行われる()
        {
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.Send("あ");
                chatbox.Send("い");
                chatbox.Update(10.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "あ", true, true), Receive());

                chatbox.Update(10.5);
                Assert.IsFalse(HasPending());
                Assert.AreEqual(1, chatbox.PendingCount);

                chatbox.Update(11.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "い", true, true), Receive());
                Assert.AreEqual(0, chatbox.PendingCount);
            }
        }

        [Test]
        public void 通知音の設定を送信に反映する()
        {
            _settings.PlayChatboxNotificationSound = false;
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.Send("a");
                chatbox.Update(5.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "a", true, false), Receive());
            }
        }

        [Test]
        public void 入力中の表示は値が変わったときだけ送る()
        {
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.SetTyping(true);
                chatbox.SetTyping(true);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/typing", true), Receive());
                Assert.IsFalse(HasPending());

                chatbox.SetTyping(false);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/typing", false), Receive());
            }
        }

        [Test]
        public void 宛先が未起動でも送信は例外にならない()
        {
            int port = ((IPEndPoint)_receiver.Client.LocalEndPoint).Port;
            _receiver.Dispose();
            _settings.OscPort = port;
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.Send("a");
                chatbox.Send("b");
                Assert.DoesNotThrow(() => chatbox.Update(1.0));
                Assert.DoesNotThrow(() => chatbox.Update(3.0));
                Assert.DoesNotThrow(() => chatbox.SetTyping(true));
            }
            _receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        }

        [Test]
        public void 破棄後の使用は例外()
        {
            var chatbox = new VRChatChatbox(_settings);
            chatbox.Dispose();
            Assert.Throws<ObjectDisposedException>(() => chatbox.Send("a"));
            Assert.DoesNotThrow(() => chatbox.Dispose());
        }

        [Test]
        public void 空の文字列は例外()
        {
            using (var chatbox = new VRChatChatbox(_settings))
                Assert.Throws<ArgumentException>(() => chatbox.Send(""));
        }
    }
}
