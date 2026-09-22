using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 画面の角へ向かって白く溶ける膜。記憶の中にいるあいだ出しておく。
    ///
    /// **URP の Vignette では作れない。** あれは絵に色を掛ける作りなので、
    /// 白を渡すと何も起きない（1 を掛けているのと同じ）。暗くはできても明るくはできない。
    ///
    /// **テクスチャも持たない。** 一枚の絵にすると、書き出す大きさを決めた時点で
    /// 境目に段が出る。ここは頂点の色だけで濃さを作るので、どの解像度でも滑らかに出る。
    ///
    /// 濃さは画面の真ん中からの隔たりで決める。縦横それぞれの半分で割ってから測るので、
    /// 隔たりは辺の真ん中で 1、角で 1.41 になる。<see cref="from"/> から <see cref="upto"/>
    /// までを滑らかに繋ぐと、辺より角がはっきり濃くなる
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScreenHaze : MaskableGraphic
    {
        /// <summary>格子の目の数。縦横とも。滑らかさとポリゴン数の釣り合いで決めた</summary>
        const int Mesh = 24;

        [Tooltip("濃さが立ち上がる隔たり。0 が画面の真ん中、1 が辺の真ん中、1.41 が角")]
        [SerializeField] float from = 0.52f;
        [Tooltip("濃さが上限に届く隔たり")]
        [SerializeField] float upto = 1.38f;
        [Tooltip("角での濃さの上限。0 で何も出ない")]
        [SerializeField, Range(0f, 1f)] float depth = 0.34f;
        [Tooltip("全体の強さ。DiveDirector が場面ごとに書き換える")]
        [SerializeField, Range(0f, 1f)] float amount = 1f;

        /// <summary>膜の強さ。0 で消える</summary>
        public float Amount
        {
            get { return amount; }
            set
            {
                var next = Mathf.Clamp01(value);
                if (Mathf.Approximately(next, amount)) return;
                amount = next;
                SetVerticesDirty();
            }
        }

        /// <summary>角での濃さ。組み立てから一度だけ決める</summary>
        public float Depth
        {
            get { return depth; }
            set { depth = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        /// <summary>膜は絵でしかないので、鍵や指を受け取らない</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        /// <summary>
        /// 真ん中からの隔たりに対する濃さ。
        ///
        /// 端を滑らかに繋ぐのは、線で区切ると帯が一本見えてしまうため
        /// </summary>
        public float Veil(float reach)
        {
            if (upto <= from) return reach >= upto ? depth * amount : 0f;
            var k = Mathf.Clamp01((reach - from) / (upto - from));
            return Mathf.SmoothStep(0f, 1f, k) * depth * amount;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            var halfW = rect.width * 0.5f;
            var halfH = rect.height * 0.5f;
            var midX = rect.x + halfW;
            var midY = rect.y + halfH;

            var vert = UIVertex.simpleVert;
            for (var row = 0; row <= Mesh; row++)
            {
                // -1 から 1 へ。真ん中が 0
                var v = row / (float)Mesh * 2f - 1f;
                for (var col = 0; col <= Mesh; col++)
                {
                    var u = col / (float)Mesh * 2f - 1f;
                    var reach = Mathf.Sqrt(u * u + v * v);
                    vert.position = new Vector3(midX + u * halfW, midY + v * halfH, 0f);
                    vert.color = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(Veil(reach)) * 255f));
                    vh.AddVert(vert);
                }
            }

            var stride = Mesh + 1;
            for (var row = 0; row < Mesh; row++)
                for (var col = 0; col < Mesh; col++)
                {
                    var a = row * stride + col;
                    vh.AddTriangle(a, a + stride, a + stride + 1);
                    vh.AddTriangle(a, a + stride + 1, a + 1);
                }
        }
    }
}
