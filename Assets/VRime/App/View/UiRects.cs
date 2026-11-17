using UnityEngine;

namespace VRime.App.View
{
    /// <summary>uGUI と OnGUI の配置の補助。座標は基準の左上を原点とし、下向きを正とする（テクスチャのピクセルと同じ向き）。</summary>
    public static class UiRects
    {
        /// <summary>縦横比 width : height を保って area に収まる最大の、中央に寄せた矩形。</summary>
        public static Rect FitInto(Rect area, float width, float height)
        {
            float scale = Mathf.Min(area.width / width, area.height / height);
            float w = width * scale;
            float h = height * scale;
            return new Rect(area.x + (area.width - w) / 2f, area.y + (area.height - h) / 2f, w, h);
        }

        /// <summary>rect を親の左上を基準に置く（アンカーとピボットを左上にする）。</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>target の矩形を、reference の矩形の左上を原点とする座標で表す。回転していないことを前提とする。</summary>
        public static Rect RectIn(RectTransform target, RectTransform reference)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Rect bounds = reference.rect;
            // GetWorldCorners の並びは 左下・左上・右上・右下。
            Vector3 bottomLeft = reference.InverseTransformPoint(corners[0]);
            Vector3 topRight = reference.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(bottomLeft.x - bounds.xMin, bounds.yMax - topRight.y, topRight.x - bounds.xMin, bounds.yMax - bottomLeft.y);
        }
    }
}
