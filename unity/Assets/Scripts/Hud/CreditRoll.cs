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
    /// <item>題（HALF AWARE）と読み（かたあはれ）だけは、タイトルの画面と同じく粗くしない。画面の解像度で描く Canvas（<see cref="crispColumn"/>）</item>
    /// </list>
    /// **二つの Canvas は一画素の大きさが違う。** 粗い画面の Canvas は描く先（画面の 0.75 倍）の大きさから拡縮が決まるので、
    /// 16:9 では 960×540 の画素になり、くっきりの Canvas は 1280×720 になる。並べる深さは粗い画面の画素で持ち、
    /// くっきりの列へは二つの Canvas の高さの比（<see cref="ratio"/>）を掛けて渡す。これで二つの中身が画面の上で同じだけ動く。
    ///
    /// **行は、いま見えている列の幅で折り返す。** 画面の縦横比が変わると列の幅（Canvas の画素）が変わるので、
    /// 流すたびに二つの Canvas の大きさを見て、変わっていれば並べ直す（<see cref="Layout"/>）。
    /// 列の左右には余白（<see cref="margin"/>）を取り、どの行も画面の内に収める。題が一行に収まらない細い画面（4:3 など）では、
    /// 列を左へ広げる（<see cref="Fit"/>）。
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
        [Tooltip("最後の行の真ん中の、中身の頭からの深さ（粗い画面の画素）。並べるたびに書く")]
        [SerializeField] float lastCentre;
        [Tooltip("列の左右の余白。それぞれの Canvas の画素")]
        [SerializeField] float margin = 26f;
        [Tooltip("くっきりの列が要る幅（いちばん長い題の行と左右の余白。くっきりの Canvas の画素）。列はこれより細くしない")]
        [SerializeField] float crispNeed;
        [Tooltip("くっきりの Canvas の画素 ÷ 粗い画面の Canvas の画素（高さの比）")]
        [SerializeField] float ratio = 1f;

        [Header("並べた行（Compose が書く）")]
        [Tooltip("上から順の行。粗い列とくっきりの列の行が混じる")]
        [SerializeField] List<TMP_Text> lines = new List<TMP_Text>();
        [Tooltip("行の前の間（粗い画面の画素）")]
        [SerializeField] List<float> before = new List<float>();

        /// <summary>最後に並べた時の二つの Canvas の大きさ。変わったら並べ直す</summary>
        Vector2 laidBody = new Vector2(-1f, -1f), laidCrisp = new Vector2(-1f, -1f);

        /// <summary>流す所の左右（画面の横の割合）</summary>
        public Vector2 Column { get { return new Vector2(columnFrom, columnTo); } }

        /// <summary>中身の長さ（見出しと名前の行の、頭から最後の行の尻まで）</summary>
        public float Length { get { return content != null ? content.sizeDelta.y : 0f; } }

        /// <summary>最後の行の真ん中の深さ</summary>
        public float LastCentre { get { return lastCentre; } }

        void Awake()
        {
            Layout();
        }

        /// <summary>流す所の左右を、列の錨へ書く。いまのくっきりの Canvas の幅で、題が収まる幅を確かめる</summary>
        public void Fit()
        {
            Fit(CanvasSize(crispColumn).x);
        }

        /// <summary>
        /// 流す所の左右を、列の錨へ書く。crispCanvas はくっきりの Canvas の幅（画素）。
        /// 列の幅がくっきりの列の要る幅（<see cref="crispNeed"/>）に足りなければ、右の縁はそのままで左へ広げる
        /// </summary>
        void Fit(float crispCanvas)
        {
            var from = columnFrom;
            if (crispCanvas > 1f && crispNeed > 0f) from = Mathf.Min(from, columnTo - crispNeed / crispCanvas);
            from = Mathf.Clamp(from, 0f, columnTo - 0.05f);
            foreach (var c in new[] { column, crispColumn })
            {
                if (c == null) continue;
                c.anchorMin = new Vector2(from, 0f);
                c.anchorMax = new Vector2(columnTo, 1f);
                c.offsetMin = Vector2.zero;
                c.offsetMax = Vector2.zero;
            }
        }

        /// <summary>二つの Canvas の大きさが前に並べた時と違えば、列を合わせて並べ直す</summary>
        public void Layout()
        {
            if (column == null || crispColumn == null) return;
            var body = CanvasSize(column);
            var crisp = CanvasSize(crispColumn);
            if (body.x <= 1f || crisp.x <= 1f) return;
            if (Mathf.Abs(body.x - laidBody.x) < 0.5f && Mathf.Abs(body.y - laidBody.y) < 0.5f
                && Mathf.Abs(crisp.x - laidCrisp.x) < 0.5f && Mathf.Abs(crisp.y - laidCrisp.y) < 0.5f) return;
            Fit(crisp.x);
            var span = column.anchorMax.x - column.anchorMin.x;
            Relayout(new Vector2(body.x * span, body.y), new Vector2(crisp.x * span, crisp.y));
            laidBody = body;
            laidCrisp = crisp;
        }

        static Vector2 CanvasSize(RectTransform t)
        {
            var c = t != null ? t.GetComponentInParent<Canvas>() : null;
            if (c == null) return Vector2.zero;
            return ((RectTransform)c.rootCanvas.transform).rect.size;
        }

        /// <summary>
        /// 行を列の幅で折り返して、上から並べ直す。body と crisp はそれぞれの Canvas での列の幅と高さ（画素）。
        /// 深さは粗い画面の画素で積み、くっきりの行は高さの比を掛けて置く
        /// </summary>
        void Relayout(Vector2 body, Vector2 crisp)
        {
            ratio = body.y > 0f ? crisp.y / body.y : 1f;
            var wb = Mathf.Max(40f, body.x - margin * 2f);
            var wc = Mathf.Max(40f, crisp.x - margin * 2f);
            var y = 0f;
            var last = 0f;
            var lastHigh = 0f;
            for (var i = 0; i < lines.Count; i++)
            {
                var t = lines[i];
                if (t == null) continue;
                y += i < before.Count ? before[i] : 0f;
                var sharp = crispContent != null && t.transform.parent == crispContent;
                var w = sharp ? wc : wb;
                var high = t.text.Length > 0 ? t.GetPreferredValues(t.text, w, 0f).y : t.fontSize;
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(w, high);
                rt.anchoredPosition = new Vector2(0f, -(sharp ? y * ratio : y));
                var inBody = sharp ? high / ratio : high;
                last = y;
                lastHigh = inBody;
                y += inBody;
            }
            lastCentre = last + lastHigh * 0.5f;
            if (content != null) Size(content, y);
            if (crispContent != null) Size(crispContent, y * ratio);
        }

        static void Size(RectTransform c, float high)
        {
            c.anchorMin = new Vector2(0f, 1f);
            c.anchorMax = new Vector2(1f, 1f);
            c.pivot = new Vector2(0.5f, 1f);
            c.sizeDelta = new Vector2(0f, high);
        }

        /// <summary>
        /// 流れの割合 progress（0 は最初の行が列の下の縁、1 は最後の行が列の真ん中）と、暗がりの濃さの割合 dim（0〜1）。
        /// visible が偽なら字も暗がりも伏せる
        /// </summary>
        public void Set(float progress, float dim, bool visible)
        {
            Layout();
            Place(progress, dim, visible);
        }

        void Place(float progress, float dim, bool visible)
        {
            var high = column != null ? column.rect.height : 540f;
            var y = Offset(progress, high, lastCentre);
            if (content != null)
            {
                content.gameObject.SetActive(visible);
                content.anchoredPosition = new Vector2(0f, y);
            }
            if (crispContent != null)
            {
                crispContent.gameObject.SetActive(visible);
                crispContent.anchoredPosition = new Vector2(0f, y * ratio);
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

        /// <summary>字の大きさと間合い。字の大きさはそれぞれの Canvas の画素、間合いと余白は粗い画面の画素</summary>
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
        /// 行は列の幅で折り返す（長い素材の名は二行、三行になる）。ここでは 16:9 の決まりの大きさ
        /// （粗い画面の Canvas は 1280×720 に粗さを掛けた大きさ、くっきりは 1280×720）で並べ、場面にはその並びを残す。
        /// 流す時に画面の大きさで並べ直す（<see cref="Layout"/>）
        /// </summary>
        public void Compose(List<CreditsText.Row> rows, Look look)
        {
            Clear(content);
            Clear(crispContent);
            lines.Clear();
            before.Clear();
            margin = look.margin;
            var gap = 0f;
            var widest = 0f;
            CreditsText.Kind prev = CreditsText.Kind.Gap;
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.kind == CreditsText.Kind.Gap)
                {
                    gap += look.gap;
                    prev = r.kind;
                    continue;
                }
                if (lines.Count > 0 && prev != CreditsText.Kind.Gap)
                {
                    if (prev == CreditsText.Kind.Heading) gap += look.afterHeading;
                    else if (prev == CreditsText.Kind.Title) gap += look.afterTitle;
                    else gap += look.lineGap;
                }
                var sharp = r.kind == CreditsText.Kind.Title || r.kind == CreditsText.Kind.Reading;
                var t = Line(sharp ? crispContent : content, r, look);
                if (sharp && r.text.Length > 0) widest = Mathf.Max(widest, t.GetPreferredValues(r.text).x);
                lines.Add(t);
                before.Add(gap);
                gap = 0f;
                prev = r.kind;
            }
            crispNeed = widest + margin * 2f + 2f;
            var scaler = column != null ? column.GetComponentInParent<CanvasScaler>() : null;
            var reference = scaler != null ? scaler.referenceResolution : new Vector2(1280f, 720f);
            var bodyCanvas = reference * UiLens.Scale;
            Fit(reference.x);
            var span = column != null ? column.anchorMax.x - column.anchorMin.x : columnTo - columnFrom;
            Relayout(new Vector2(bodyCanvas.x * span, bodyCanvas.y), new Vector2(reference.x * span, reference.y));
            // 並べ直しは流す時（Set）に画面の大きさで行う。ここでは決まりの大きさの並びのまま伏せる
            laidBody = laidCrisp = new Vector2(-1f, -1f);
            Place(0f, 0f, false);
        }

        static TMP_Text Line(RectTransform parent, CreditsText.Row r, Look look)
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
