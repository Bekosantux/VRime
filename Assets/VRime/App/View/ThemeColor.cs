using System;
using UnityEngine;
using UnityEngine.UI;

namespace VRime.App.View
{
    /// <summary>
    /// 同じオブジェクトの uGUI（<see cref="Graphic"/>）を、テーマの役割（<see cref="ThemeColorRole"/>）の色を設定する。
    /// 状態によらない色の部品に付ける。状態によって色を変える部品（ビューが色を設定するもの）には付けない（テーマを変えたときにビューの色を上書きするため）。
    /// 色を設定するのはビューが再描画するとき（<see cref="ApplyAll"/>）で、シーンに保存した色は編集用の見本になる。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class ThemeColor : MonoBehaviour
    {
        [SerializeField] ThemeColorRole _role;

        public ThemeColorRole Role => _role;

        public void Apply(KeyboardTheme theme)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            GetComponent<Graphic>().color = theme.ColorOf(_role);
        }

        /// <summary>root の下（非表示のものと、複製のテンプレートを含む）の <see cref="ThemeColor"/> をすべて適用する。</summary>
        public static void ApplyAll(Transform root, KeyboardTheme theme)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            foreach (ThemeColor color in root.GetComponentsInChildren<ThemeColor>(true)) color.Apply(theme);
        }
    }
}
