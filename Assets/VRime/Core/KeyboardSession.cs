using System;
using System.Collections.Generic;
using VRime.Core.Control;
using VRime.Core.Conversion;
using VRime.Core.Grip;
using VRime.Core.Input;
using VRime.Core.Text;

namespace VRime.Core
{
    public enum KeyboardMode
    {
        /// <summary>入力中。入力方式（グリップ入力）が操作を受ける。</summary>
        Input,

        /// <summary>変換の応答待ち。</summary>
        WaitingConversion,

        /// <summary>変換中。候補欄の操作を受ける。</summary>
        Converting,
    }

    /// <summary>
    /// キーボード全体の状態機械。開閉・ダッシュボードによる一時停止・モードを管理し、
    /// 入力中は <see cref="GripInputMethod"/>、変換中は <see cref="ConversionController"/> に操作を振り分ける。
    /// 外部への作用は <see cref="IKeyboardEffects"/> で通知する。
    /// </summary>
    public sealed class KeyboardSession
    {
        readonly IKeyboardEffects _effects;
        readonly InputBuffer _buffer;
        readonly GripInputMethod _grip;
        readonly ConversionController _conversionController;
        readonly DoubleChordDetector _chord;
        readonly EdgeTracker _edges = new EdgeTracker();
        readonly List<HapticEvent> _haptics = new List<HapticEvent>();

        KeyboardSettings _settings;
        bool _isOpen;
        bool _dashboardVisible;
        bool _resumeAfterDashboard;
        KeyboardMode _mode = KeyboardMode.Input;

        // 開いたとき・変換を押したとき・取り消したときに押されていたパネルの切り替えのボタンは、離すまで押されていないものとして扱う。
        bool _ignoreLeftPanel;
        bool _ignoreRightPanel;
        ControllerFrame _lastFrame;

        int _nextRequestId;
        int _pendingRequestId;
        ConversionSession _conversion;
        string _conversionFailureReason;
        string _notice = string.Empty;
        int _noticeBufferVersion;

        int _viewVersion;
        int _observedSubVersions;

        public KeyboardSession(KeyboardSettings settings, IKeyboardEffects effects)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _buffer = new InputBuffer();
            _grip = new GripInputMethod(settings, _buffer);
            _conversionController = new ConversionController(settings);
            _chord = new DoubleChordDetector(settings.ChordWindowSeconds, settings.DoubleChordWindowSeconds);
        }

        public KeyboardSettings Settings => _settings;

        public bool IsOpen => _isOpen;
        public bool IsDashboardVisible => _dashboardVisible;
        public KeyboardMode Mode => _mode;

        public string Committed => _buffer.Committed;
        public int CommittedCursor => _buffer.CommittedCursor;
        public string Composition => _buffer.Composition;
        public int CompositionCursor => _buffer.CompositionCursor;

        /// <summary>確定済みと未確定の合計の文字数（UTF-16 単位）。</summary>
        public int Length => _buffer.Length;

        /// <summary>確定済みの文字列のカーソル位置に未確定のかなを挟んだ、表示どおりの文字列。</summary>
        public string Text => _buffer.Text;

        /// <summary>文字数の上限。null は上限なし。</summary>
        public int? LengthLimit => _buffer.LengthLimit;

        /// <summary>内容が文字数の上限を超えているか。超えている間は送信しない。</summary>
        public bool IsOverLengthLimit => _buffer.IsOverLengthLimit;

        /// <summary>変換中の文節と選択。変換中でなければ null。</summary>
        public ConversionSession CurrentConversion => _conversion;

        /// <summary>変換に失敗して代わりの候補を出しているときの理由。それ以外は null。</summary>
        public string ConversionFailureReason => _conversionFailureReason;

        /// <summary>送信結果などの表示用の文字列。入力バッファの内容が変わるか、キーボードを開くと消える。</summary>
        public string Notice => _notice;

        public GripInputMethod GripInput => _grip;
        public GripViewState GripView => _grip.View;

        /// <summary>表示に関わる状態が変わるたびに増える。</summary>
        public int ViewVersion
        {
            get
            {
                int sub = _buffer.Version + _grip.Version + (_conversion != null ? _conversion.Version : 0);
                if (sub != _observedSubVersions)
                {
                    _observedSubVersions = sub;
                    _viewVersion++;
                }
                return _viewVersion;
            }
        }

        /// <summary>毎フレーム呼ぶ。閉じている間も開閉の判定のために呼ぶ。</summary>
        public void Tick(ControllerFrame frame, double timeSeconds)
        {
            _lastFrame = frame;
            bool toggle = _chord.Update(frame.Left.OpenClose, frame.Right.OpenClose, timeSeconds);

            // 押下エッジは、操作を受け付けない間も追跡する。再開した直後に古い押下状態から誤ったエッジが出るのを防ぐ。
            if (_dashboardVisible)
            {
                UpdateEdges(frame);
                return;
            }

            if (toggle)
            {
                UpdateEdges(frame);
                if (_isOpen) Close();
                else Open();
                _effects.OnHaptic(Hand.Left, HapticKind.OpenClose);
                _effects.OnHaptic(Hand.Right, HapticKind.OpenClose);
                return;
            }

            if (!_isOpen)
            {
                UpdateEdges(frame);
                return;
            }

            ControllerFrame effective = ApplyPanelIgnore(frame);
            FrameEdges edges = UpdateEdges(effective);
            // 両手の展開・収納のボタンを押している間は、開閉の操作と区別するためトリガーを受け付けない。
            if (frame.Left.OpenClose && frame.Right.OpenClose) edges = edges.WithoutTriggers();

            _haptics.Clear();
            switch (_mode)
            {
                case KeyboardMode.Input:
                    UpdateInput(effective, edges, timeSeconds);
                    break;
                case KeyboardMode.WaitingConversion:
                case KeyboardMode.Converting:
                    UpdateConversion(frame, effective, edges);
                    break;
                default:
                    throw new InvalidOperationException("未知のモード: " + _mode);
            }
            ClearNoticeIfBufferChanged();
            foreach (HapticEvent haptic in _haptics) _effects.OnHaptic(haptic.Hand, haptic.Kind);
            _haptics.Clear();
        }

        /// <summary>変換の結果を渡す。古い requestId（取り消し・閉じた後など）は無視する。</summary>
        public void CompleteConversion(int requestId, IReadOnlyList<ConversionSegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            if (!IsAwaiting(requestId)) return;
            _conversion = new ConversionSession(segments);
            _mode = KeyboardMode.Converting;
            _viewVersion++;
        }

        /// <summary>変換の失敗を渡す。代わりの候補（ひらがなのまま・カタカナ）で変換中にする。古い requestId は無視する。</summary>
        public void FailConversion(int requestId, string reason)
        {
            if (reason == null) throw new ArgumentNullException(nameof(reason));
            if (!IsAwaiting(requestId)) return;
            _conversion = ConversionSession.CreateFallback(_buffer.Composition);
            _conversionFailureReason = reason;
            _mode = KeyboardMode.Converting;
            _viewVersion++;
        }

        /// <summary>
        /// ダッシュボードの表示状態を渡す。表示中は手動で閉じるのと同じ処理で閉じ、閉じたら再開する。
        /// 手動で閉じていた場合は再開しない。
        /// </summary>
        public void SetDashboardVisible(bool visible)
        {
            if (visible == _dashboardVisible) return;
            _dashboardVisible = visible;
            if (visible)
            {
                _resumeAfterDashboard = _isOpen;
                if (_isOpen) Close();
            }
            else if (_resumeAfterDashboard)
            {
                _resumeAfterDashboard = false;
                Open();
            }
            _viewVersion++;
        }

        /// <summary>
        /// 文字数の上限を変える。null は上限なし。入力済みの内容は切り詰めず、上限を超えている間は送信しない。
        /// </summary>
        public void SetLengthLimit(int? limit)
        {
            if (limit == _buffer.LengthLimit) return;
            _buffer.SetLengthLimit(limit);
            _viewVersion++;
        }

        /// <summary>
        /// 実行中に設定を差し替える。次の <see cref="Tick"/> から新しい値で判定する。
        /// 実行中に変えられるのはスティックとトリガーのしきい値・連続入力の時間・変換を使えるかだけで、それ以外の値が異なる設定は例外にする
        /// （子音の手は表示の組み立てに、同時押しの窓は開閉の判定に、生成時の値が使われているため）。
        /// </summary>
        public void ApplySettings(KeyboardSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.ConsonantHand != _settings.ConsonantHand)
                throw new NotSupportedException("子音の手は実行中に変更できません。");
            if (settings.ChordWindowSeconds != _settings.ChordWindowSeconds
                || settings.DoubleChordWindowSeconds != _settings.DoubleChordWindowSeconds)
                throw new NotSupportedException("同時押しの判定時間は実行中に変更できません。");
            _settings = settings;
            _grip.ApplySettings(settings);
            _conversionController.ApplySettings(settings);
        }

        public void SetNotice(string notice)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            // 通知を出した時点のバッファの版を覚え、その後に内容が変わったら消す。
            _noticeBufferVersion = _buffer.Version;
            if (notice == _notice) return;
            _notice = notice;
            _viewVersion++;
        }

        void ClearNoticeIfBufferChanged()
        {
            if (_notice.Length == 0 || _buffer.Version == _noticeBufferVersion) return;
            _notice = string.Empty;
            _viewVersion++;
        }

        void ClearNotice()
        {
            if (_notice.Length == 0) return;
            _notice = string.Empty;
            _viewVersion++;
        }

        bool IsAwaiting(int requestId)
        {
            return _mode == KeyboardMode.WaitingConversion && requestId == _pendingRequestId;
        }

        void Open()
        {
            ClearNotice();
            _isOpen = true;
            _mode = KeyboardMode.Input;
            _ignoreLeftPanel = _lastFrame.Left.Panel;
            _ignoreRightPanel = _lastFrame.Right.Panel;
            _grip.Reset(ApplyPanelIgnore(_lastFrame));
            _viewVersion++;
            _effects.OnOpenChanged(true);
        }

        void Close()
        {
            // 変換中に閉じたら取り消す。バッファは保持するため、未確定のかなに戻る。
            if (_mode != KeyboardMode.Input) EndConversion();
            _isOpen = false;
            _viewVersion++;
            _effects.OnOpenChanged(false);
        }

        void UpdateInput(ControllerFrame effective, FrameEdges edges, double timeSeconds)
        {
            GripRequest request = _grip.Update(effective, edges, timeSeconds, _haptics);
            switch (request.Kind)
            {
                case GripRequestKind.None:
                    break;
                case GripRequestKind.Convert:
                    StartConversion(effective, request.Hand);
                    break;
                case GripRequestKind.Commit:
                    PressCommitKey(request.Hand);
                    break;
                default:
                    throw new InvalidOperationException("未知の要求: " + request.Kind);
            }
        }

        // 確定キー（SPEC 7.1）: 未確定があればそのまま確定し、なければ送信を要求する。
        // 文字数の上限を超えている間は送信を要求しない。送信に失敗したらバッファを残す。
        void PressCommitKey(Hand hand)
        {
            if (_buffer.HasComposition)
            {
                _buffer.CommitComposition();
                _haptics.Add(new HapticEvent(hand, HapticKind.Input));
            }
            else if (_buffer.Committed.Length > 0 && !_buffer.IsOverLengthLimit)
            {
                if (_effects.OnSendRequested(_buffer.Committed))
                {
                    // バッファが変わるため、それまでの通知（前回の送信の失敗など）は消える。
                    _buffer.Clear();
                    _haptics.Add(new HapticEvent(hand, HapticKind.Input));
                }
                else
                {
                    _haptics.Add(new HapticEvent(hand, HapticKind.Invalid));
                }
            }
            else
            {
                _haptics.Add(new HapticEvent(hand, HapticKind.Invalid));
            }
        }

        void StartConversion(ControllerFrame effective, Hand hand)
        {
            if (!_buffer.HasComposition)
            {
                _haptics.Add(new HapticEvent(hand, HapticKind.Invalid));
                return;
            }

            _pendingRequestId = ++_nextRequestId;
            _mode = KeyboardMode.WaitingConversion;
            _conversion = null;
            _conversionFailureReason = null;
            IgnoreHeldPanels(effective);
            _conversionController.Begin(effective);
            _haptics.Add(new HapticEvent(hand, HapticKind.Input));
            _viewVersion++;
            _effects.OnConversionRequested(_pendingRequestId, _buffer.Composition);
        }

        void UpdateConversion(ControllerFrame raw, ControllerFrame effective, FrameEdges edges)
        {
            ConversionOutcome outcome = _conversionController.Update(effective, edges, _conversion, _haptics);
            switch (outcome)
            {
                case ConversionOutcome.None:
                    break;

                case ConversionOutcome.Commit:
                {
                    Hand hand = edges.LeftTrigger ? Hand.Left : Hand.Right;
                    if (_buffer.CommitConverted(_conversion.Result) == EditResult.Done)
                    {
                        EndConversion();
                        _grip.Reset(ApplyPanelIgnore(raw));
                        _haptics.Add(new HapticEvent(hand, HapticKind.Input));
                    }
                    else
                    {
                        // 最大長を超える変換結果は確定できない。候補を選び直せる。
                        _haptics.Add(new HapticEvent(hand, HapticKind.Invalid));
                    }
                    break;
                }

                case ConversionOutcome.Cancel:
                {
                    Hand hand = edges.LeftPanel ? Hand.Left : Hand.Right;
                    // 取り消した直後に、押したままのパネルの切り替えのボタンで機能ページが開かないようにする。
                    IgnoreHeldPanels(effective);
                    EndConversion();
                    _grip.Reset(ApplyPanelIgnore(raw));
                    _haptics.Add(new HapticEvent(hand, HapticKind.Move));
                    break;
                }

                default:
                    throw new InvalidOperationException("未知の結果: " + outcome);
            }
        }

        void EndConversion()
        {
            _mode = KeyboardMode.Input;
            _conversion = null;
            _conversionFailureReason = null;
            _pendingRequestId = 0;
            _viewVersion++;
        }

        void IgnoreHeldPanels(ControllerFrame effective)
        {
            if (effective.Left.Panel) _ignoreLeftPanel = true;
            if (effective.Right.Panel) _ignoreRightPanel = true;
        }

        FrameEdges UpdateEdges(ControllerFrame frame)
        {
            return _edges.Update(frame, _settings.TriggerThreshold);
        }

        ControllerFrame ApplyPanelIgnore(ControllerFrame frame)
        {
            if (!frame.Left.Panel) _ignoreLeftPanel = false;
            if (!frame.Right.Panel) _ignoreRightPanel = false;
            return new ControllerFrame(
                _ignoreLeftPanel ? frame.Left.WithPanel(false) : frame.Left,
                _ignoreRightPanel ? frame.Right.WithPanel(false) : frame.Right);
        }
    }
}
