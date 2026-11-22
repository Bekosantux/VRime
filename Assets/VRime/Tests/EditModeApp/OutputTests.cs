using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using VRime.App.Conversion;
using VRime.App.Output;
using VRime.Core.Output;

namespace VRime.App.Tests
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
            public int ClearCount;
            public void Send(string text) { Sent.Add(text); }
            public void Clear() { ClearCount++; }
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
            return new ForegroundWindowInfo(new IntPtr(1), pid, name, display);
        }

        static TextOutputRouter Router(AppSettings settings, FakeChatbox chatbox, FakeDesktop desktop, ForegroundWindowInfo window,
            bool pasteMode = false)
        {
            OutputTarget target = OutputTarget.Classify(window, OwnPid, pasteMode);
            return new TextOutputRouter(settings, chatbox, desktop, () => target);
        }

        [Test]
        public void VRChatが前面ならチャットボックスへ送る()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            OutputResult result = Router(new AppSettings(), chatbox, desktop, Window(200, "vrchat")).Send("こんにちは");
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "こんにちは" }, chatbox.Sent);
            Assert.AreEqual(0, desktop.Sent.Count);
        }

        [Test]
        public void VRChatが前面ならチャットボックスの表示を消せる()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            Assert.IsTrue(Router(new AppSettings(), chatbox, desktop, Window(200, "vrchat")).Clear());
            Assert.AreEqual(1, chatbox.ClearCount);
            Assert.AreEqual(0, chatbox.Sent.Count);
            Assert.AreEqual(0, desktop.Sent.Count);
        }

        [Test]
        public void チャットボックス以外の送信先では表示を消さない()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            Assert.IsFalse(Router(new AppSettings(), chatbox, desktop, Window(200, "vrchat"), pasteMode: true).Clear(), "貼り付けモード");
            Assert.IsFalse(Router(new AppSettings(), chatbox, desktop, Window(300, "notepad")).Clear(), "デスクトップのアプリ");
            Assert.IsFalse(Router(new AppSettings(), chatbox, desktop, Window(OwnPid, "VRime")).Clear(), "本アプリ自身");
            Assert.IsFalse(Router(new AppSettings(), chatbox, desktop, null).Clear(), "前面のウィンドウなし");
            Assert.AreEqual(0, chatbox.ClearCount);
            Assert.AreEqual(0, desktop.Sent.Count);
        }

        [Test]
        public void 貼り付けモードのVRChatには貼り付けで送る()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            OutputResult result = Router(new AppSettings(), chatbox, desktop, Window(200, "vrchat"), pasteMode: true).Send("ワールド");
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "ワールド" }, desktop.Sent);
            Assert.AreEqual(0, chatbox.Sent.Count);
        }

        [Test]
        public void 貼り付けモードのVRChatでも管理者権限のウィンドウは失敗として通知する()
        {
            var desktop = new FakeDesktop { Result = DesktopSendResult.TargetElevated };
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "vrchat"), pasteMode: true).Send("a");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(OutputFailure.ElevatedWindow, result.Failure);
        }

        [Test]
        public void それ以外はデスクトップへ送り設定のEnterを渡す()
        {
            var chatbox = new FakeChatbox();
            var desktop = new FakeDesktop();
            var settings = new AppSettings { PressEnterAfterDesktopSend = true };
            OutputResult result = Router(settings, chatbox, desktop, Window(200, "notepad", "メモ帳")).Send("a");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(desktop.LastPressEnter);
            Assert.AreEqual(0, chatbox.Sent.Count);
        }

        [Test]
        public void 成功の結果は通知を持たない()
        {
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, "notepad")).Send("a");
            Assert.IsTrue(result.Success);
            Assert.Throws<InvalidOperationException>(() => _ = result.Notice);
        }

        [Test]
        public void プロセス名が不明でも送れる()
        {
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, null)).Send("a");
            Assert.IsTrue(result.Success);
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
        public void クリップボードが使用中なら失敗として通知する()
        {
            var desktop = new FakeDesktop { Result = DesktopSendResult.ClipboardBusy };
            OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "notepad")).Send("a");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(OutputFailure.ClipboardBusy, result.Failure);
        }

        [Test]
        public void 通知に送信内容を含めない()
        {
            const string secret = "ひみつの文章";
            foreach (DesktopSendResult r in Enum.GetValues(typeof(DesktopSendResult)))
            {
                var desktop = new FakeDesktop { Result = r };
                OutputResult result = Router(new AppSettings(), new FakeChatbox(), desktop, Window(200, "notepad")).Send(secret);
                if (!result.Success) StringAssert.DoesNotContain(secret, result.Notice);
            }
        }

        [Test]
        public void 空の文字列は例外()
        {
            var router = Router(new AppSettings(), new FakeChatbox(), new FakeDesktop(), Window(200, "notepad"));
            Assert.Throws<ArgumentException>(() => router.Send(""));
        }
    }

    public sealed class ClipboardRestoreScheduleTests
    {
        const double Delay = 1.0;
        const double GiveUp = 5.0;

        static ClipboardRestoreSchedule<string> Armed(string snapshot = "元の内容", uint sequence = 10, double now = 100.0)
        {
            var schedule = new ClipboardRestoreSchedule<string>(Delay, GiveUp);
            schedule.Arm(snapshot, sequence, now);
            return schedule;
        }

        [Test]
        public void 復元待ちがなければ何もしない()
        {
            var schedule = new ClipboardRestoreSchedule<string>(Delay, GiveUp);
            Assert.IsFalse(schedule.HasPending);
            Assert.AreEqual(ClipboardRestoreAction.None, schedule.Poll(100.0, 10));
        }

        [Test]
        public void 時間が経つまでは戻さず経ったら戻す()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.AreEqual(ClipboardRestoreAction.None, schedule.Poll(100.5, 10));
            Assert.AreEqual(ClipboardRestoreAction.Restore, schedule.Poll(101.0, 10));
            Assert.AreEqual("元の内容", schedule.Pending);

            schedule.Complete();
            Assert.IsFalse(schedule.HasPending);
            Assert.AreEqual(ClipboardRestoreAction.None, schedule.Poll(102.0, 10));
        }

        [Test]
        public void 戻せないうちは繰り返し戻すよう求め期限を過ぎたら諦める()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.AreEqual(ClipboardRestoreAction.Restore, schedule.Poll(101.0, 10));
            Assert.AreEqual(ClipboardRestoreAction.Restore, schedule.Poll(105.9, 10));
            Assert.AreEqual(ClipboardRestoreAction.GaveUp, schedule.Poll(106.0, 10));
            Assert.IsFalse(schedule.HasPending);
        }

        [Test]
        public void 書き換えられていたら時刻を待たずに捨てる()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.AreEqual(ClipboardRestoreAction.Discarded, schedule.Poll(100.1, 11));
            Assert.IsFalse(schedule.HasPending);
        }

        [Test]
        public void 書き換えられていなければ続けて送るときに退避を引き継ぐ()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.IsTrue(schedule.TryTakePending(10, out string snapshot));
            Assert.AreEqual("元の内容", snapshot);
            Assert.IsFalse(schedule.HasPending);

            // 引き継いだ退避で再度書き込むと、戻す時刻は新しい書き込みから数える。
            schedule.Arm(snapshot, 12, 100.8);
            Assert.AreEqual(ClipboardRestoreAction.None, schedule.Poll(101.0, 12));
            Assert.AreEqual(ClipboardRestoreAction.Restore, schedule.Poll(101.8, 12));
        }

        [Test]
        public void 書き換えられていれば続けて送るときに退避を引き継がない()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.IsFalse(schedule.TryTakePending(11, out string snapshot));
            Assert.IsNull(snapshot);
            Assert.IsFalse(schedule.HasPending);
        }

        [Test]
        public void 復元待ちがなければ引き継がない()
        {
            var schedule = new ClipboardRestoreSchedule<string>(Delay, GiveUp);
            Assert.IsFalse(schedule.TryTakePending(10, out string snapshot));
            Assert.IsNull(snapshot);
        }

        [Test]
        public void シーケンス番号が書き込んだときのままかを調べられる()
        {
            ClipboardRestoreSchedule<string> schedule = Armed();
            Assert.IsTrue(schedule.IsUnchanged(10));
            Assert.IsFalse(schedule.IsUnchanged(11));
            Assert.IsFalse(new ClipboardRestoreSchedule<string>(Delay, GiveUp).IsUnchanged(0));
        }

        [Test]
        public void 不正な引数と状態は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClipboardRestoreSchedule<string>(0, GiveUp));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClipboardRestoreSchedule<string>(Delay, double.NaN));
            var schedule = new ClipboardRestoreSchedule<string>(Delay, GiveUp);
            Assert.Throws<ArgumentNullException>(() => schedule.Arm(null, 1, 0));
            Assert.Throws<InvalidOperationException>(() => _ = schedule.Pending);
            Assert.Throws<InvalidOperationException>(() => schedule.Complete());
        }
    }

    public sealed class OutputTargetTests
    {
        const int OwnPid = 100;

        static ForegroundWindowInfo Window(int pid, string name, string display = null)
        {
            return new ForegroundWindowInfo(new IntPtr(1), pid, name, display);
        }

        [Test]
        public void 前面のウィンドウから送信先を決める()
        {
            Assert.AreEqual(OutputTargetKind.None, OutputTarget.Classify(null, OwnPid, false).Kind);
            Assert.AreEqual(OutputTargetKind.OwnWindow, OutputTarget.Classify(Window(OwnPid, "VRime"), OwnPid, false).Kind);
            Assert.AreEqual(OutputTargetKind.VRChat, OutputTarget.Classify(Window(200, "VRChat"), OwnPid, false).Kind);
            Assert.AreEqual(OutputTargetKind.VRChat, OutputTarget.Classify(Window(200, "vrchat"), OwnPid, false).Kind);
            Assert.AreEqual(OutputTargetKind.Desktop, OutputTarget.Classify(Window(200, "notepad"), OwnPid, false).Kind);
            Assert.AreEqual(OutputTargetKind.Desktop, OutputTarget.Classify(Window(200, null), OwnPid, false).Kind);
        }

        [Test]
        public void 貼り付けモードはVRChatの送信先だけを変える()
        {
            Assert.AreEqual(OutputTargetKind.VRChatPaste, OutputTarget.Classify(Window(200, "VRChat"), OwnPid, true).Kind);
            Assert.AreEqual(OutputTargetKind.Desktop, OutputTarget.Classify(Window(200, "notepad"), OwnPid, true).Kind);
            Assert.AreEqual(OutputTargetKind.OwnWindow, OutputTarget.Classify(Window(OwnPid, "VRime"), OwnPid, true).Kind);
            Assert.AreEqual(OutputTargetKind.None, OutputTarget.Classify(null, OwnPid, true).Kind);
        }

        [Test]
        public void 送り方を問わずVRChatかを判定できる()
        {
            Assert.IsTrue(OutputTarget.Classify(Window(200, "VRChat"), OwnPid, false).IsVRChat);
            Assert.IsTrue(OutputTarget.Classify(Window(200, "VRChat"), OwnPid, true).IsVRChat);
            Assert.IsFalse(OutputTarget.Classify(Window(200, "notepad"), OwnPid, true).IsVRChat);
            Assert.IsFalse(OutputTarget.None.IsVRChat);
        }

        [Test]
        public void 名前はアプリ名_プロセス名の順に使う()
        {
            Assert.AreEqual("メモ帳", OutputTarget.Classify(Window(200, "notepad", "メモ帳"), OwnPid, false).Name);
            Assert.AreEqual("notepad", OutputTarget.Classify(Window(200, "notepad"), OwnPid, false).Name);
            Assert.IsNotEmpty(OutputTarget.Classify(Window(200, null), OwnPid, false).Name);
        }

        [Test]
        public void VRChatと本アプリとなしは決まった名前を使う()
        {
            Assert.AreEqual("VRChat", OutputTarget.Classify(Window(200, "vrchat", "VRChat Client"), OwnPid, false).Name);
            Assert.AreEqual("VRChat（貼り付け）", OutputTarget.Classify(Window(200, "vrchat", "VRChat Client"), OwnPid, true).Name);
            Assert.IsNotEmpty(OutputTarget.Classify(Window(OwnPid, "VRime"), OwnPid, false).Name);
            Assert.IsNotEmpty(OutputTarget.None.Name);
        }
    }

    public sealed class ProcessImageTests
    {
        [Test]
        public void 自プロセスの実行ファイル名を調べられる()
        {
            string expected = System.IO.Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            Assert.AreEqual(expected, ProcessImage.QueryName((uint)ForegroundWindowInfo.CurrentProcessId));
        }

        [Test]
        public void 存在しないプロセスはnull()
        {
            // プロセス ID は 4 の倍数で割り当てられるため、4 の倍数でない値は存在しない。
            Assert.IsNull(ProcessImage.QueryName(uint.MaxValue));
        }
    }

    public sealed class ForegroundWindowMonitorTests
    {
        sealed class FakeSource : IForegroundWindowSource
        {
            public IntPtr Handle;
            public int ProcessId;
            public string ProcessName = "notepad";
            public int DescribeCount;

            public bool TryGetForeground(out IntPtr handle, out int processId)
            {
                handle = Handle;
                processId = ProcessId;
                return Handle != IntPtr.Zero;
            }

            public ForegroundWindowInfo Describe(IntPtr handle, int processId)
            {
                DescribeCount++;
                return new ForegroundWindowInfo(handle, processId, ProcessName, null);
            }
        }

        const int OwnPid = 100;

        [Test]
        public void 作成時に前面のウィンドウを調べる()
        {
            var source = new FakeSource { Handle = new IntPtr(1), ProcessId = 200, ProcessName = "VRChat" };
            var monitor = new ForegroundWindowMonitor(source, OwnPid);
            Assert.AreEqual(OutputTargetKind.VRChat, monitor.Target.Kind);
            Assert.AreEqual(1, source.DescribeCount);
        }

        [Test]
        public void 前面のウィンドウがなければ送信先はなし()
        {
            var monitor = new ForegroundWindowMonitor(new FakeSource(), OwnPid);
            Assert.AreEqual(OutputTargetKind.None, monitor.Target.Kind);
        }

        [Test]
        public void 前面が変わったときだけ再取得してVersionを増やす()
        {
            var source = new FakeSource { Handle = new IntPtr(1), ProcessId = 200 };
            var monitor = new ForegroundWindowMonitor(source, OwnPid);
            int version = monitor.Version;

            monitor.Update(false);
            Assert.AreEqual(version, monitor.Version);
            Assert.AreEqual(1, source.DescribeCount);

            source.Handle = new IntPtr(2);
            source.ProcessId = 300;
            source.ProcessName = "VRChat";
            monitor.Update(false);
            Assert.AreEqual(version + 1, monitor.Version);
            Assert.AreEqual(OutputTargetKind.VRChat, monitor.Target.Kind);
            Assert.AreEqual(2, source.DescribeCount);

            source.Handle = IntPtr.Zero;
            monitor.Update(false);
            Assert.AreEqual(version + 2, monitor.Version);
            Assert.AreEqual(OutputTargetKind.None, monitor.Target.Kind);
            Assert.AreEqual(2, source.DescribeCount);
        }

        [Test]
        public void 貼り付けモードが変わると再取得せずに送信先を決め直す()
        {
            var source = new FakeSource { Handle = new IntPtr(1), ProcessId = 200, ProcessName = "VRChat" };
            var monitor = new ForegroundWindowMonitor(source, OwnPid);
            int version = monitor.Version;

            monitor.Update(true);
            Assert.AreEqual(OutputTargetKind.VRChatPaste, monitor.Target.Kind);
            Assert.AreEqual(version + 1, monitor.Version);

            monitor.Update(false);
            Assert.AreEqual(OutputTargetKind.VRChat, monitor.Target.Kind);
            Assert.AreEqual(version + 2, monitor.Version);
            Assert.AreEqual(1, source.DescribeCount);
        }

        [Test]
        public void VRChat以外では貼り付けモードが変わってもVersionは変わらない()
        {
            var source = new FakeSource { Handle = new IntPtr(1), ProcessId = 200 };
            var monitor = new ForegroundWindowMonitor(source, OwnPid);
            int version = monitor.Version;

            monitor.Update(true);
            Assert.AreEqual(OutputTargetKind.Desktop, monitor.Target.Kind);
            Assert.AreEqual(version, monitor.Version);

            // 貼り付けモードのまま VRChat が前面になれば、貼り付けで送る。
            source.Handle = new IntPtr(2);
            source.ProcessId = 300;
            source.ProcessName = "VRChat";
            monitor.Update(true);
            Assert.AreEqual(OutputTargetKind.VRChatPaste, monitor.Target.Kind);
            Assert.AreEqual(version + 1, monitor.Version);
        }

        [Test]
        public void 同じハンドルでもプロセスが変われば再取得する()
        {
            var source = new FakeSource { Handle = new IntPtr(1), ProcessId = 200 };
            var monitor = new ForegroundWindowMonitor(source, OwnPid);
            source.ProcessId = OwnPid;
            monitor.Update(false);
            Assert.AreEqual(OutputTargetKind.OwnWindow, monitor.Target.Kind);
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
                Assert.AreEqual(1, chatbox.PendingSendCount);

                chatbox.Update(11.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "い", true, true), Receive());
                Assert.AreEqual(0, chatbox.PendingSendCount);
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
        public void 入力中の表示は通知音を鳴らさずに送る()
        {
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.SetPreview("あい");
                chatbox.Update(1.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "あい", true, false), Receive());

                chatbox.SetPreview(null);
                chatbox.Update(2.0);
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "", true, false), Receive());
            }
        }

        [Test]
        public void 表示中の入力中の表示は間隔を待たずに消せる()
        {
            using (var chatbox = new VRChatChatbox(_settings))
            {
                chatbox.ClearPreviewNow();
                Assert.IsFalse(HasPending());

                chatbox.SetPreview("あ");
                chatbox.Update(1.0);
                Receive();
                chatbox.ClearPreviewNow();
                CollectionAssert.AreEqual(OscCodec.Encode("/chatbox/input", "", true, false), Receive());
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
