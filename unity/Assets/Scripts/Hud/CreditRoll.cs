using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// エンディングの縦に流れるクレジット（シナリオ設計 13 節）。
    ///
    /// 流す所は画面の縦の一列（<see cref="columnFrom"/>〜<see cref="columnTo"/>、画面の横の割合）。
    /// 仮に右 1/3（運転席の窓の外）の上に置き、読めるよう薄い暗がり（<see cref="shade"/>）を敷く。
    /// 左右はこの二つの値だけで替わる（13.2 節で後で決める）。
    ///
    /// 字は二つの Canvas に分けて置く。
    /// <list type="bullet">
    /// <item>見出しと名前の行は、ほかの UI と同じ粗い画面（<see cref="UiLens"/>）の Canvas</item>
    /// <item>題（HALF AWARE）と読み（かたあはれ）だけは、タイトルの画面と同じく粗くしない。画面の解像度で描く Canvas（<see cref="crisp"/>）</item>
    /// </list>
    /// 二つの中身を同じだけ動かすので、並びはずれない。
    ///
    /// 並べるのは組み立て（BuildEnding）で、<see cref="Compose"/> が本文（<see cref="CreditsText"/>）から字を置く。
    /// 流すのは EndingDirector で、曲の秒から出した割合を <see cref="Set"/> へ渡す
    /// </summary>
    public sealed class CreditRoll : MonoBehaviour
    {
        [Header("流す所（画面の横の割合）。左右はここだけで替わる")]
        [SerializeField, Range(0f, 1f)] float columnFrom = 2f / 3f;
        [SerializeField, Range(0f, 1f)] float columnTo = 1f;

        [Header("組み立てが繋ぐ")]
        [Tooltip("粗い画面の列。暗がりと、見出しと名前の行を持つ")]
        [SerializeField] RectTransform column;
        [Tooltip("粗い画面の中身。これを縦に動かす")]
        [SerializeField] RectTransform content;
        [Tooltip("くっきり描く列（題と読み）")]
        [SerializeField] RectTransform crispColumn;
        [SerializeField] RectTransform crispContent;
        [Tooltip("薄い暗がり")]
        [SerializeField] Graphic shade;
        [Tooltip("暗がりの濃さの上限（α）")]
        [SerializeField, Range(0f, 1f)] float shadeAlpha = 0.5f;
        [Tooltip("最後の行の真ん中の、中身の頭からの深さ。Compose が書く")]
        [SerializeField] float lastCentre;

        /// <summary>流す所の左右（画面の横の割合）</summary>
        public Vector2 Column { get { return new Vector2(columnFrom, columnTo); } }

        /// <summary>中身の長さ（見出しと名前の行の、頭から最後の行の尻まで）</summary>
        public float Length { get { return content != null ? content.sizeDelta.y : 0f; } }

        /// <summary>最後の行の真ん中の深さ</summary>
        public float LastCentre { get { return lastCentre; } }

        void Awake()
        {
            Fit();
        }

        /// <summary>流す所の左右を、列の錨へ書く</summary>
        public void Fit()
        {
            foreach (var c in new[] { column, crispColumn })
            {
                if (c == null) continue;
                c.anchorMin = new Vector2(columnFrom, 0f);
                c.anchorMax = new Vector2(columnTo, 1f);
                c.offsetMin = Vector2.zero;
                c.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// 流れの割合 progress（0 は最初の行が列の下の縁、1 は最後の行が列の真ん中）と、暗がりの濃さの割合 dim（0〜1）。
        /// visible が偽なら字も暗がりも伏せる
        /// </summary>
        public void Set(float progress, float dim, bool visible)
        {
            var high = column != null ? column.rect.height : 720f;
            var y = Offset(progress, high, lastCentre);
            if (content != null)
            {
                content.gameObject.SetActive(visible);
                content.anchoredPosition = new Vector2(0f, y);
            }
            if (crispContent != null)
            {
                crispContent.gameObject.SetActive(visible);
                crispContent.anchoredPosition = new Vector2(0f, y);
            }
            if (shade != null)
            {
                shade.gameObject.SetActive(visible && dim > 0f);
                var c = shade.color;
                c.a = shadeAlpha * Mathf.Clamp01(dim);
                shade.color = c;
            }
        }

        /// <summary>
        /// 中身の頭の、列の頭からの高さ（上が正）。progress 0 で中身の頭が列の下の縁（-high）、
        /// 1 で最後の行の真ん中（中身の頭から centre の深さ）が列の真ん中（-high/2）に来る
        /// </summary>
        public static float Offset(float progress, float high, float centre)
        {
            var from = -high;
            var to = centre - high * 0.5f;
            return Mathf.Lerp(from, to, Mathf.Clamp01(progress));
        }

        // ---- 並べる（組み立てから） ------------------------------------------------

        /// <summary>字の大きさと間合い。1280×720 の Canvas の画素</summary>
        public struct Look
        {
            public TMP_FontAsset heading;
            public TMP_FontAsset body;
            public float titleSize, readingSize, headingSize, lineSize;
            public float titleSpacing, readingSpacing, headingSpacing;
            public float gap, afterHeading, afterTitle, lineGap;
            public float margin;
            public Color titleColor, headingColor, lineColor;
        }

        /// <summary>
        /// rows を並べて置く。前に置いた字は捨てる。題と読みはくっきりの列へ、ほかは粗い列へ。
        /// 行は列の幅で折り返す（長い素材の名は二行、三行になる）
        /// </summary>
        public void Compose(List<CreditsText.Row> rows, Look look)
        {
            Fit();
            Clear(content);
            Clear(crispContent);
            var width = Width() - look.margin * 2f;
            var y = 0f;
            var last = 0f;
            var lastHigh = 0f;
            CreditsText.Kind prev = CreditsText.Kind.Gap;
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.kind == CreditsText.Kind.Gap)
                {
                    y += look.gap;
                    prev = r.kind;
                    continue;
                }
                if (i > 0 && prev != CreditsText.Kind.Gap)
                {
                    if (prev == CreditsText.Kind.Heading) y += look.afterHeading;
                    else if (prev == CreditsText.Kind.Title) y += look.afterTitle;
                    else y += look.lineGap;
                }
                var crisp = r.kind == CreditsText.Kind.Title || r.kind == CreditsText.Kind.Reading;
                var parent = crisp ? crispContent : content;
                var t = Line(parent, r, look, width);
                var high = r.text.Length > 0 ? t.GetPreferredValues(r.text, width, 0f).y : t.fontSize;
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(width, high);
                rt.anchoredPosition = new Vector2(0f, -y);
                last = y;
                lastHigh = high;
                y += high;
                prev = r.kind;
            }
            lastCentre = last + lastHigh * 0.5f;
            foreach (var c in new[] { content, crispContent })
            {
                if (c == null) continue;
                c.anchorMin = new Vector2(0f, 1f);
                c.anchorMax = new Vector2(1f, 1f);
                c.pivot = new Vector2(0.5f, 1f);
                c.sizeDelta = new Vector2(0f, y);
            }
            Set(0f, 0f, false);
        }

        /// <summary>列の幅。Canvas の画素（組み立ての時の 1280×720 で）</summary>
        float Width()
        {
            var scaler = column != null ? column.GetComponentInParent<CanvasScaler>() : null;
            var reference = scaler != null ? scaler.referenceResolution.x : 1280f;
            return reference * (columnTo - columnFrom);
        }

        static TMP_Text Line(RectTransform parent, CreditsText.Row r, Look look, float width)
        {
            var go = new GameObject(r.kind + (r.text.Length > 0 ? " " + Short(r.text) : ""), typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.Top;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            switch (r.kind)
            {
                case CreditsText.Kind.Title:
                    t.font = look.heading; t.fontSize = look.titleSize; t.characterSpacing = look.titleSpacing; t.color = look.titleColor; break;
                case CreditsText.Kind.Reading:
                    t.font = look.heading; t.fontSize = look.readingSize; t.characterSpacing = look.readingSpacing; t.color = look.titleColor; break;
                case CreditsText.Kind.Heading:
                    t.font = look.heading; t.fontSize = look.headingSize; t.characterSpacing = look.headingSpacing; t.color = look.headingColor; break;
                default:
                    t.font = look.body; t.fontSize = look.lineSize; t.color = look.lineColor; break;
            }
            t.text = r.text;
            return t;
        }

        static string Short(string s)
        {
            return s.Length > 24 ? s.Substring(0, 24) : s;
        }

        static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var go = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }
    }
}
