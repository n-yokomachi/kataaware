using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 遠景の書き割りの輪。見る位置を中心にした正多角形の板の輪と、
    /// 一枚ずつを撮るときの視錐台（設計書 9.1 節「遠景の書き割り」）。
    ///
    /// **一枚ごとに、板とぴったり同じ視錐台で撮る。** 中心から板の法線の向きに構え、
    /// 近い面の四隅が板の四隅を見込む左右上下の非対称な視錐台にする。こうすると
    /// 撮った絵は板に貼ったときに中心から見て寸分違わず、切れ端を繋いでも歪まない。
    ///
    /// 角度は +z を 0 として東（+x）回りの度。i 番の板は i·360/n の向きを向き、
    /// 左の縁がそこから -180/n、右の縁が +180/n。中心から見て右が角度の増える側。
    ///
    /// 地面の遠い縁も、中心を同じくした同じ向きの正多角形にする。そうすると、
    /// 撮った絵の中で地面の縁が落ちる所は、どの板の上でも同じ高さの一本の横線になる
    /// （<see cref="GroundLine"/>）。
    ///
    /// 実行時には呼ばない。純粋な計算なので、ここに置いて試験から見る
    /// </summary>
    public static class BackdropRing
    {
        /// <summary>一枚の板が中心から見込む角の半分。ラジアン</summary>
        public static float Half(int count)
        {
            return Mathf.PI / count;
        }

        /// <summary>i 番の板の向き（中心から外へ）。度</summary>
        public static float Azimuth(int i, int count)
        {
            return i * 360f / count;
        }

        /// <summary>角度 → 水平の単位ベクトル</summary>
        public static Vector3 Heading(float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>中心から多角形の角までの水平の距離。apothem は中心から辺までの距離</summary>
        public static float Corner(float apothem, int count)
        {
            return apothem / Mathf.Cos(Half(count));
        }

        /// <summary>一枚の板の横幅</summary>
        public static float Width(float apothem, int count)
        {
            return 2f * apothem * Mathf.Tan(Half(count));
        }

        /// <summary>
        /// i 番の板の四隅。左下・右下・右上・左上の順。高さ（bottom, top）は絶対の y
        /// </summary>
        public static Vector3[] Corners(Vector3 centre, float apothem, int count, int i, float bottom, float top)
        {
            var reach = Corner(apothem, count);
            var mid = Azimuth(i, count);
            var step = 180f / count;
            var left = centre + Heading(mid - step) * reach;
            var right = centre + Heading(mid + step) * reach;
            return new[]
            {
                new Vector3(left.x, bottom, left.z),
                new Vector3(right.x, bottom, right.z),
                new Vector3(right.x, top, right.z),
                new Vector3(left.x, top, left.z),
            };
        }

        /// <summary>i 番の板を撮るカメラの向き。水平に、板の法線の向きへ</summary>
        public static Quaternion Facing(int i, int count)
        {
            return Quaternion.Euler(0f, Azimuth(i, count), 0f);
        }

        /// <summary>
        /// 中心の高さ <paramref name="eye"/> から一枚の板を撮る視錐台。
        /// 近い面の四隅が、板の四隅を中心から見込む線の上に来る
        /// </summary>
        public static Matrix4x4 Frustum(float apothem, int count, float bottom, float top, float eye, float near, float far)
        {
            var k = near / apothem;
            var half = apothem * Mathf.Tan(Half(count));
            return Matrix4x4.Frustum(-half * k, half * k, (bottom - eye) * k, (top - eye) * k, near, far);
        }

        /// <summary>
        /// 水平の線が正多角形の縁に届くまでの距離。<paramref name="from"/> は多角形の内側。
        /// 向きは水平の単位ベクトル（y は見ない）
        /// </summary>
        public static float Along(Vector3 from, Vector3 dir, Vector3 centre, float apothem, int count)
        {
            var best = float.MaxValue;
            var d = new Vector2(dir.x, dir.z);
            var rel = new Vector2(from.x - centre.x, from.z - centre.z);
            for (var i = 0; i < count; i++)
            {
                var h = Heading(Azimuth(i, count));
                var n = new Vector2(h.x, h.z);
                var nd = Vector2.Dot(n, d);
                if (nd <= 1e-6f) continue;
                var t = (apothem - Vector2.Dot(n, rel)) / nd;
                if (t < best) best = t;
            }
            return best;
        }

        /// <summary>
        /// 撮った絵の中で、地面の遠い縁（中心から辺まで <paramref name="ground"/> の正多角形）が
        /// 板の上に落ちる高さ。中心の高さ <paramref name="eye"/> から見て、縁の先へ続く地面の始まり
        /// </summary>
        public static float GroundLine(float eye, float ground, float apothem)
        {
            return eye * (1f - apothem / ground);
        }

        /// <summary>
        /// 立ち位置 <paramref name="at"/>（目の高さ込み）から水平に <paramref name="heading"/> 度を向いたとき、
        /// 本物の地面の遠い縁と、書き割りの中の地面の境目が、上下に何画素ずれて見えるか。
        /// 正なら書き割りの境目が上（隙間に書き割りの地面が見える）、負なら下（本物の地面が書き割りに被る）。
        ///
        /// <paramref name="focal"/> は 1 ラジアンが何画素になるか（画面の縦の半分 ÷ tan(fov/2)）。
        /// 中心から撮った目の高さ <paramref name="eye"/> と同じ所に立てば 0 になる
        /// </summary>
        public static float Seam(Vector3 at, float heading, Vector3 centre, float eye,
            float ground, float apothem, int count, float focal)
        {
            var dir = Heading(heading);
            var g = Along(at, dir, centre, ground, count);
            var r = Along(at, dir, centre, apothem, count);
            var line = GroundLine(eye, ground, apothem);
            var drawn = (line - at.y) / r;
            var real = -at.y / g;
            return focal * (drawn - real);
        }

        /// <summary>
        /// 立ち位置から水平に見たとき、書き割りの地平（撮った目の高さの線）が本物の地平から
        /// 上下に何画素ずれて見えるか。正なら上
        /// </summary>
        public static float Horizon(Vector3 at, float heading, Vector3 centre, float eye, float apothem, int count, float focal)
        {
            var r = Along(at, Heading(heading), centre, apothem, count);
            return focal * (eye - at.y) / r;
        }
    }
}
