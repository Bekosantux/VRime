using System;
using UnityEngine;
using VRKeyboard.App.Output;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// <see cref="KeyboardSession"/> の状態と送信先を uGUI に反映する。表示の内容はこれらの状態だけで決まり、入力の判断は持たない。
    /// 毎フレーム <see cref="RefreshIfChanged"/> を呼ぶと、<see cref="KeyboardSession.ViewVersion"/> か
    /// <see cref="ForegroundWindowMonitor.Version"/> が変わったときだけ uGUI を更新する。
    /// キーボードのキャンバスに置く。部品の配置はシーンで決め、盤面（<see cref="_board"/>）の外側は透明にする。
    /// </summary>
    public sealed class KeyboardView : MonoBehaviour
    {
        [Tooltip("キーボード本体（盤面）。オーバーレイの位置はこの中心を基準にする。")]
        [SerializeField] RectTransform _board;

        [SerializeField] InputAreaView _input;
        [SerializeField] GripPanelView _panels;
        [SerializeField] CandidateAreaView _candidates;

        KeyboardSession _session;
        ForegroundWindowMonitor _monitor;
        int _renderedVersion = -1;
        int _renderedTargetVersion = -1;

        /// <summary>
        /// テクスチャの中心から盤面の中心までの下向きの距離を、テクスチャの幅で割った値。
        /// オーバーレイの位置を盤面の中心に合わせるのに使う（<see cref="SteamVR.OverlayPlacement"/>）。
        /// </summary>
        public float BoardCenterOffset
        {
            get
            {
                var canvas = (RectTransform)transform;
                Vector2 size = canvas.rect.size;
                Rect board = UiRects.RectIn(_board, canvas);
                return (board.center.y - size.y / 2f) / size.x;
            }
        }

        public void Validate()
        {
            SceneChecks.Require(_board, this, nameof(_board));
            SceneChecks.Require(_input, this, nameof(_input));
            SceneChecks.Require(_panels, this, nameof(_panels));
            SceneChecks.Require(_candidates, this, nameof(_candidates));
            SceneChecks.RequireUnder(_board, this, this, nameof(_board));
            SceneChecks.RequireUnder(_input, this, this, nameof(_input));
            SceneChecks.RequireUnder(_panels, this, this, nameof(_panels));
            SceneChecks.RequireUnder(_candidates, this, this, nameof(_candidates));
            _input.Validate();
            _panels.Validate();
            _candidates.Validate();
        }

        public void Initialize(KeyboardSession session, ForegroundWindowMonitor monitor, Hand consonantHand, KeyboardTheme theme)
        {
            Validate();
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            _input.Initialize(theme);
            _panels.Initialize(consonantHand, theme);
            _candidates.Initialize(theme);
            _renderedVersion = -1;
            _renderedTargetVersion = -1;
        }

        /// <summary>表示に関わる状態が変わっていれば uGUI を更新して true を返す。</summary>
        public bool RefreshIfChanged()
        {
            int version = _session.ViewVersion;
            int targetVersion = _monitor.Version;
            if (version == _renderedVersion && targetVersion == _renderedTargetVersion) return false;
            _renderedVersion = version;
            _renderedTargetVersion = targetVersion;

            _candidates.Refresh(_session);
            _input.Refresh(_session, _monitor.Target);
            _panels.Refresh(_session.GripInput, _session.Mode != KeyboardMode.Input);
            return true;
        }
    }
}
