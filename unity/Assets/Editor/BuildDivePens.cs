using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 記憶ごとの見えない囲い。その記憶で歩ける範囲を、見えない壁で絞る。
    ///
    /// **場所の箱だけでは広すぎる。** 公営住宅の敷地も公園の芝生も全部歩けたので、
    /// 話す相手と板の出る人から離れて何も無い所まで出歩けた。「各記憶で歩ける範囲を透明な壁で制限して。
    /// インタラクトできる人以外のところにあまり出歩きすぎないように」と差し戻された。
    ///
    /// 囲う点は、鍵打ちの全部（始めの立ち位置と、出来事で主が向かう階段・玄関・ホワイトボードなど）、
    /// 人の止まる所（動く人は <see cref="Mover.End"/>）、記憶ごとの足し（<see cref="PenShape.extra"/>）。
    /// その点の一つずつを余白（<see cref="PenShape.margin"/>）の八角形に膨らませ、全部を包む凸の多角形を取り、
    /// 辺ごとに薄い箱のコライダーを立てる。凸なので、点どうしを結ぶ道筋は必ず内に入る。
    ///
    /// **Ignore Raycast の層に置く。** 主の体（CharacterController）は止めるが、
    /// 目を留める光線（<c>DiveDirector.Blocking</c>）・板の置き場を探す光線（<see cref="HoloPanel"/>）・
    /// 人の足元を床へ下ろす光線（<see cref="Mover"/>）はどれも既定の層しか見ないので、囲いに当たらない。
    /// 人はコライダーを持たず、Mover が位置を直に置くので、囲いの外から歩いてくる人（二階から降りる息子、
    /// 芝生を横切る少女）も止まらない。
    ///
    /// 囲いは記憶（Take）の子に置く。DiveDirector が起こした記憶の囲いだけが効く。
    /// 高さは地面の下 1 m から、いちばん高い点の 8 m 上まで。公営住宅の階とメゾネットの上の階を一続きに囲う
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>囲いの層。既定の光線はこの層を見ない</summary>
        public const int PenLayer = 2;
        /// <summary>壁の厚み。m</summary>
        const float PenThick = 0.1f;
        /// <summary>地面の下へ伸ばす長さ。m</summary>
        const float PenBelow = 1f;
        /// <summary>
        /// いちばん高い点の上へ伸ばす長さ。m。公営住宅のメゾネットは玄関の階（5.6 m）の上にもう一つ階があり、
        /// 3 m では住戸の中の階段で上の階へ上がると壁の上を越えて囲いの外へ出られた
        /// </summary>
        const float PenAbove = 8f;

        /// <summary>記憶一本の囲いの値</summary>
        public struct PenShape
        {
            /// <summary>点のまわりの余白。m</summary>
            public float margin;
            /// <summary>鍵打ちと人のほかに囲いへ入れる所。場所のローカル</summary>
            public Vector3[] extra;
        }

        static PenShape P(float margin, params Vector3[] extra)
        {
            return new PenShape { margin = margin, extra = extra };
        }

        /// <summary>
        /// 記憶ごとの囲い。並びは一覧の番号。
        ///
        /// 余白はどれも 1.75 m（八角形なので、辺の向きでは 1.62 m まで詰まる）。
        /// 足しは、鍵打ちが途中で止まっていて、出来事の向かう先まで届いていない記憶だけに置く
        /// </summary>
        public static readonly PenShape[] Pens =
        {
            P(1.75f),                                  // 0 メイ。階段の下から三階の A の戸口まで、鍵打ちが全部なぞる
            P(1.75f, new Vector3(-0.7f, 5.6f, -13.1f)), // 1 ハンナ。「階段を降り始める」ので、三階の階段の降り口まで
            P(1.75f),                                  // 2 アルベルト。ベンチから門まで
            P(1.75f),                                  // 3 ソフィア。芝生の向こうで止まる少女まで
            P(1.75f),                                  // 4 エミリー
            P(1.75f),                                  // 5 マーク。流しから玄関まで
            P(1.75f),                                  // 6 リンダ
            P(1.75f),                                  // 7 リー。ホワイトボードから机の列まで
            P(1.75f),                                  // 8 ジョルジョ。廊下から居間まで
            P(1.75f),                                  // 9 ローザ
            P(1.75f),                                  // 10 ルーカス
            P(1.75f),                                  // 11 プリヤ
            P(1.75f),                                  // 12 ダニエル。階段から玄関の外の同級生まで
            P(1.75f),                                  // 13 アイシャ
            P(1.75f),                                  // 14 マテオ
            P(1.75f),                                  // 15 エレナ。居間から玄関の外の老女まで
        };

        /// <summary>八角形の向きの数</summary>
        const int PenSides = 8;

        /// <summary>記憶一本の囲いを立てる。鍵打ちと人を置き終えてから呼ぶ</summary>
        static Transform Pen(Transform take, HostKey[] keys, int which)
        {
            var shape = which >= 0 && which < Pens.Length ? Pens[which] : P(1.75f);
            var points = PenPoints(take, keys, shape);
            var hull = PenHull(points, shape.margin);
            var top = 0f;
            foreach (var p in points) top = Mathf.Max(top, p.y);
            var low = -PenBelow;
            var high = top + PenAbove;

            var pen = new GameObject("Pen").transform;
            pen.SetParent(take, false);
            pen.gameObject.layer = PenLayer;
            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Count];
                var along = b - a;
                var length = along.magnitude;
                if (length < 1e-3f) continue;
                var wall = new GameObject("Wall" + i).transform;
                wall.SetParent(pen, false);
                wall.gameObject.layer = PenLayer;
                var mid = (a + b) * 0.5f;
                // 壁の内側の面を辺に合わせる。凸の多角形は左回りなので、外は辺の右
                var outward = new Vector2(along.y, -along.x) / length;
                mid += outward * (PenThick * 0.5f);
                wall.localPosition = new Vector3(mid.x, (low + high) * 0.5f, mid.y);
                wall.localRotation = Quaternion.Euler(0f, Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg, 0f);
                var box = wall.gameObject.AddComponent<BoxCollider>();
                // 角で隙間が空かないよう、厚みのぶん長く取る
                box.size = new Vector3(PenThick, high - low, length + PenThick * 2f);
            }
            return pen;
        }

        /// <summary>囲う点。鍵打ち・人の止まる所・足し。場所のローカル</summary>
        public static List<Vector3> PenPoints(Transform take, HostKey[] keys, PenShape shape)
        {
            var points = new List<Vector3>();
            if (keys != null) foreach (var k in keys) points.Add(k.position);
            foreach (Transform who in take)
            {
                if (who.GetComponent<PersonMotion>() == null) continue;
                var mover = who.GetComponent<Mover>();
                points.Add(mover != null ? mover.End : who.localPosition);
            }
            if (shape.extra != null) points.AddRange(shape.extra);
            return points;
        }

        /// <summary>
        /// 点を余白の八角形に膨らませて包む凸の多角形。左回り（上から見て +x を右、+z を上に置いたとき）
        /// </summary>
        public static List<Vector2> PenHull(List<Vector3> points, float margin)
        {
            var all = new List<Vector2>();
            foreach (var p in points)
                for (var i = 0; i < PenSides; i++)
                {
                    var a = (i + 0.5f) * Mathf.PI * 2f / PenSides;
                    all.Add(new Vector2(p.x + Mathf.Cos(a) * margin, p.z + Mathf.Sin(a) * margin));
                }
            return Hull(all);
        }

        /// <summary>凸包（Andrew の単調連鎖）。左回り</summary>
        static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var h = new List<Vector2>();
            if (pts.Count < 3) { h.AddRange(pts); return h; }
            System.Func<Vector2, Vector2, Vector2, float> cross = (o, a, b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (var i = 0; i < pts.Count; i++)
            {
                while (h.Count >= 2 && cross(h[h.Count - 2], h[h.Count - 1], pts[i]) <= 1e-6f) h.RemoveAt(h.Count - 1);
                h.Add(pts[i]);
            }
            var lower = h.Count + 1;
            for (var i = pts.Count - 2; i >= 0; i--)
            {
                while (h.Count >= lower && cross(h[h.Count - 2], h[h.Count - 1], pts[i]) <= 1e-6f) h.RemoveAt(h.Count - 1);
                h.Add(pts[i]);
            }
            h.RemoveAt(h.Count - 1);
            return h;
        }
    }
}
