using System;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRKeyboard.App.View
{
    /// <summary>シーンに置いた部品の参照の検証。足りない参照は、どのオブジェクトのどの欄かを示す例外にする。</summary>
    public static class SceneChecks
    {
        public static void Require(Object value, Component owner, string field)
        {
            if (value == null) throw new InvalidOperationException(PathOf(owner.transform) + " の " + field + " が設定されていません。");
        }

        /// <summary>配列が count 個の参照を持つことを確かめる。</summary>
        public static void RequireAll<T>(T[] values, int count, Component owner, string field) where T : Object
        {
            if (values == null || values.Length != count)
                throw new InvalidOperationException(PathOf(owner.transform) + " の " + field + " には " + count + " 個の参照が必要です。");
            for (int i = 0; i < values.Length; i++) Require(values[i], owner, field + "[" + i + "]");
        }

        /// <summary>child が parent の子孫（または parent 自身）であることを確かめる。</summary>
        public static void RequireUnder(Component child, Component parent, Component owner, string field)
        {
            if (!child.transform.IsChildOf(parent.transform))
                throw new InvalidOperationException(PathOf(owner.transform) + " の " + field + " は " + PathOf(parent.transform) + " の下にある必要があります。");
        }

        public static string PathOf(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) builder.Insert(0, parent.name + "/");
            return builder.ToString();
        }
    }
}
