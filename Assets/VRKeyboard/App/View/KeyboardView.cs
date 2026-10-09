using System;
using UnityEngine;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// <see cref="KeyboardSession"/> の状態を uGUI に反映する。表示の内容は Core の状態だけで決まり、入力の判断は持たない。
    /// 毎フレーム <see cref="RefreshIfChanged"/> を呼ぶと、<see cref="KeyboardSession.ViewVersion"/> が変わったときだけ uGUI を更新する。
    /// 上から候補欄・入力欄・状態行・ページ行・グリップのパネルを置く。盤面の外側は完全に透明にする。
    /// </summary>
    public sealed class KeyboardView
    {
        readonly KeyboardSession _session;
        readonly CandidateAreaView _candidates;
        readonly InputAreaView _input;
        readonly GripPanelView _panels;
        int _renderedVersion = -1;

        public KeyboardView(KeyboardSession session, KeyboardSettings settings, KeyboardFonts fonts, Transform canvasRoot)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (fonts == null) throw new ArgumentNullException(nameof(fonts));
            if (canvasRoot == null) throw new ArgumentNullException(nameof(canvasRoot));

            // 盤面は候補欄の下から最下部まで。候補欄は盤面の外（透明）に重ならない位置にある。
            RectTransform root = UiBuilder.Rect(canvasRoot, "Keyboard", 0f, 0f, KeyboardTheme.Width, KeyboardTheme.Height);
            UiBuilder.Image(root, "Board", 0f, KeyboardTheme.BoardTop, KeyboardTheme.Width, KeyboardTheme.Height - KeyboardTheme.BoardTop, KeyboardTheme.Board);

            _input = new InputAreaView(root, fonts);
            _panels = new GripPanelView(root, fonts, settings.ConsonantHand);
            _candidates = new CandidateAreaView(root, fonts);
        }

        /// <summary>表示に関わる状態が変わっていれば uGUI を更新して true を返す。</summary>
        public bool RefreshIfChanged()
        {
            int version = _session.ViewVersion;
            if (version == _renderedVersion) return false;
            _renderedVersion = version;

            _candidates.Refresh(_session);
            _input.Refresh(_session);
            _panels.Refresh(_session.GripInput, _session.Mode != KeyboardMode.Input);
            return true;
        }
    }
}
