using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 6 の庭で、女性が持つホース。手元の筒（ノズル）と、そこから地面を這って花の縁の奥へ消える線（<see cref="LineRenderer"/>）。
    ///
    /// **筒は手の骨に付けない。** 持っている間は毎フレーム手の骨の所へ据え、手を離したら（<see cref="Drop"/>）足元の芝に置いたままにする。
    /// <see cref="PersonMotion.Carry"/> で骨に付けると、こまの頭ごとに手の所へ戻されて、離せない。
    ///
    /// **人の動きの後に動かす。** 骨は <see cref="PersonMotion"/> がこまの頭で置き、模型の根はフレームの終わり（LateUpdate）に
    /// こまの頭の置き場へ据わる。その後で筒と線を置き直さないと、一フレーム前の手の所に筒が残る。
    ///
    /// 線は、出どころ（花の縁の奥の地面）から、地面を這う途中の点を通り、筒の真下の少し後ろの地面を経て、筒の尻へ上がる。
    /// 点のあいだは Catmull-Rom で丸める
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GardenHose : MonoBehaviour
    {
        [SerializeField] LineRenderer line;
        [Tooltip("手元の筒。持っている間は手の骨の所へ据える")]
        [SerializeField] Transform nozzle;
        [Tooltip("女性の、筒を持つ手の骨")]
        [SerializeField] Transform hand;
        [Tooltip("手の骨から見た筒の置き場と向き")]
        [SerializeField] Vector3 grip;
        [SerializeField] Quaternion gripTurn = Quaternion.identity;
        [Tooltip("ホースの出どころ。花の縁の奥の地面。世界の位置")]
        [SerializeField] Vector3 source;
        [Tooltip("地面を這う途中の点。世界の位置")]
        [SerializeField] Vector3[] run = new Vector3[0];
        [Tooltip("手を離した後に筒を置く所と向き。世界の値")]
        [SerializeField] Vector3 dropAt;
        [SerializeField] Quaternion dropTurn = Quaternion.identity;
        [Tooltip("筒の尻から、ホースが地面へ垂れ下がる所までの横の隔たり。m")]
        [SerializeField] float slack = 0.35f;
        [Tooltip("地面の高さ。線はこの高さを這う")]
        [SerializeField] float ground = 0.02f;
        [Tooltip("点のあいだを割る数")]
        [SerializeField] int steps = 6;

        bool dropped;
        Vector3[] points = new Vector3[0];

        /// <summary>手を離したか</summary>
        public bool Dropped { get { return dropped; } }

        /// <summary>筒。水の粒と水の音はこの子に付く</summary>
        public Transform Nozzle { get { return nozzle; } }

        /// <summary>手を離す。筒は足元の芝に置いたまま動かさない</summary>
        public void Drop()
        {
            dropped = true;
            if (nozzle != null) nozzle.SetPositionAndRotation(dropAt, dropTurn);
            Refresh();
        }

        /// <summary>手に持つ（場面の頭）</summary>
        public void Hold()
        {
            dropped = false;
            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
        }

        /// <summary>筒を手の所へ据え、線を引き直す。エディタで撮るときにも呼ぶ</summary>
        public void Refresh()
        {
            if (nozzle == null) return;
            if (!dropped && hand != null) nozzle.SetPositionAndRotation(hand.TransformPoint(grip), hand.rotation * gripTurn);
            if (line == null) return;
            var tail = nozzle.position;
            // 筒の尻から垂れ下がって地面へ着く所。出どころへ向かう側へ slack だけ引く
            var toward = (run != null && run.Length > 0 ? run[run.Length - 1] : source) - tail;
            toward.y = 0f;
            var down = new Vector3(tail.x, ground, tail.z) + (toward.sqrMagnitude > 1e-6f ? toward.normalized * slack : Vector3.zero);
            var n = (run != null ? run.Length : 0) + 3;
            var keys = new Vector3[n];
            keys[0] = source;
            for (var i = 0; run != null && i < run.Length; i++) keys[i + 1] = run[i];
            keys[n - 2] = down;
            keys[n - 1] = tail;
            Smooth(keys);
        }

        /// <summary>鍵の点を Catmull-Rom で丸めて線へ渡す。端は端の点を二度使う</summary>
        void Smooth(Vector3[] keys)
        {
            var per = Mathf.Max(1, steps);
            var count = (keys.Length - 1) * per + 1;
            if (points.Length != count) points = new Vector3[count];
            var k = 0;
            for (var i = 0; i < keys.Length - 1; i++)
            {
                var p0 = keys[Mathf.Max(0, i - 1)];
                var p1 = keys[i];
                var p2 = keys[i + 1];
                var p3 = keys[Mathf.Min(keys.Length - 1, i + 2)];
                for (var s = 0; s < per; s++)
                {
                    var t = s / (float)per;
                    var p = CatmullRom(p0, p1, p2, p3, t);
                    // 地面を這う所は地面より下へ潜らせない
                    if (p.y < ground) p.y = ground;
                    points[k++] = p;
                }
            }
            points[k] = keys[keys.Length - 1];
            line.positionCount = count;
            line.SetPositions(points);
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
