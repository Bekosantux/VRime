using System;

namespace VRime.Core.Grip
{
    public enum GripCellKind
    {
        /// <summary>何も割り当てない。</summary>
        Empty,

        /// <summary>母音パネルの方向で文字を選ぶ。</summary>
        Characters,

        /// <summary>トリガーだけで実行する動作。</summary>
        Action,
    }

    /// <summary>パネルの1マス。種類によって使える情報が異なる。</summary>
    public sealed class GripCell
    {
        public const int DirectionCount = 5;

        readonly string[] _characters;
        readonly GripAction _action;

        GripCell(GripCellKind kind, string label, string[] characters, GripAction action)
        {
            Kind = kind;
            Label = label;
            _characters = characters;
            _action = action;
        }

        public static GripCell Empty { get; } = new GripCell(GripCellKind.Empty, "・", null, default);

        /// <summary>
        /// 文字グループ。characters は 中央・左・上・右・下 の5つで、未割り当ての方向は null。
        /// label を省略すると中央の文字が表示名になる。
        /// </summary>
        public static GripCell ForCharacters(string[] characters, string label = null)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (characters.Length != DirectionCount)
                throw new ArgumentException("文字は5方向分(中央・左・上・右・下)が必要です。", nameof(characters));
            if (characters[0] == null)
                throw new ArgumentException("中央の文字は必須です。", nameof(characters));
            return new GripCell(GripCellKind.Characters, label, (string[])characters.Clone(), default);
        }

        public static GripCell ForAction(GripAction action, string label)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("表示名が必要です。", nameof(label));
            return new GripCell(GripCellKind.Action, label, null, action);
        }

        public GripCellKind Kind { get; }

        /// <summary>
        /// ビュー用の表示名。文字グループで表示名を指定していない場合は null（中央の文字を表示する）。
        /// </summary>
        public string Label { get; }

        public GripAction Action
        {
            get
            {
                if (Kind != GripCellKind.Action)
                    throw new InvalidOperationException("動作のセルではありません: " + Kind);
                return _action;
            }
        }

        /// <summary>指定方向の文字。未割り当てなら null。</summary>
        public string CharacterAt(StickDirection direction)
        {
            if (Kind != GripCellKind.Characters)
                throw new InvalidOperationException("文字グループのセルではありません: " + Kind);
            int index = (int)direction;
            if (index < 0 || index >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            return _characters[index];
        }
    }
}
