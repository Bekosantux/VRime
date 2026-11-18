using System;
using Valve.VR;

namespace VRime.App.SteamVR
{
    /// <summary>OpenVR の関数が返すエラーコードの検査。失敗は例外にし、エラー名を含める。</summary>
    internal static class OpenVRErrors
    {
        public static void Check(EVRInputError error, string operation)
        {
            if (error != EVRInputError.None) throw new InvalidOperationException(operation + " に失敗しました: " + error);
        }

        public static void Check(EVROverlayError error, string operation)
        {
            if (error != EVROverlayError.None) throw new InvalidOperationException(operation + " に失敗しました: " + error);
        }

        public static void Check(EVRApplicationError error, string operation)
        {
            if (error != EVRApplicationError.None) throw new InvalidOperationException(operation + " に失敗しました: " + error);
        }

        /// <summary>
        /// 入力値・振動の取得に対する検査。<see cref="EVRInputError.NoData"/> は、そのアクションに今データがない
        /// （バインドされていない、他のアプリが入力を占有しているなど）ことを表し、故障ではない。
        /// その場合は false を返す。呼び出し側は <c>bActive</c> が false のときと同じに扱う（SPEC 4.3）。
        /// それ以外のエラーは例外にする。
        /// </summary>
        public static bool CheckAllowingNoData(EVRInputError error, string operation)
        {
            if (error == EVRInputError.NoData) return false;
            Check(error, operation);
            return true;
        }
    }
}
