using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 2 点を結ぶケーブル。毎フレーム筒を張り直すので、
    /// 端が動いても繋がったまま垂れる。椅子の差込口と手首のジャックを繋ぐのに使う
    /// </summary>
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class Cable : MonoBehaviour
    {
        /// <summary>ケーブルが避ける体の一部。骨から骨への線を芯にした円柱（両端は丸い）</summary>
        [System.Serializable]
        public struct Avoid
        {
            public Transform from;
            public Transform to;
            [Tooltip("芯から皮膚までの太さ（m）。いちばん太い所で測る")]
            public float radius;
        }

        [Tooltip("片方の端。ふつうは椅子の差込口")]
        [SerializeField] Transform from;
        [Tooltip("もう片方の端。ふつうは手首のジャック")]
        [SerializeField] Transform to;
        [Tooltip("ケーブルの長さ。端の間がこれより詰まるとたるむ")]
        [SerializeField] float length = 0.62f;
        [Tooltip("道筋を刻む点の数")]
        [SerializeField] int points = 12;
        [Tooltip("太さ。半径")]
        [SerializeField] float radius = 0.008f;
        [Tooltip("断面の分割数")]
        [SerializeField] int sides = 6;
        [Tooltip("両端から軸の向きへまっすぐ出す長さ（m）。0 なら両端を結ぶ垂れだけ")]
        [SerializeField] float stiffness = 0f;
        [Tooltip("始まりの端（差込口）からケーブルが出る向き。その端から見た軸")]
        [SerializeField] Vector3 fromAxis = Vector3.up;
        [Tooltip("終わりの端（ジャックの尻）からケーブルが出る向き。その端から見た軸")]
        [SerializeField] Vector3 toAxis = Vector3.forward;
        [Tooltip("巻き取り式。長さを端の間に合わせて出し入れし、いつも reelSlack だけ余らせる。切ると length の長さのまま垂れる")]
        [SerializeField] bool reel = false;
        [Tooltip("巻き取り式のときに余らせる長さ（m）")]
        [SerializeField] float reelSlack = 0.03f;
        [Tooltip("避ける体の一部（腕・手・腿・胴）。道筋の点がこの中に入ったら外へ押し出す")]
        [SerializeField] Avoid[] avoid = new Avoid[0];
        [Tooltip("押し出すときに皮膚から取るゆとり（m）。ケーブルの太さに加える")]
        [SerializeField] float clearance = 0.004f;

        Vector3[] path;
        Mesh mesh;
        Vector3[] verts;
        Vector3[] normals;
        int[] tris;

        void Awake()
        {
            points = Mathf.Max(2, points);
            sides = Mathf.Max(3, sides);
            path = new Vector3[points];
            verts = new Vector3[points * sides];
            normals = new Vector3[verts.Length];
            tris = new int[(points - 1) * sides * 6];
            var k = 0;
            for (var r = 0; r < points - 1; r++)
            {
                var a = r * sides;
                var b = (r + 1) * sides;
                for (var s = 0; s < sides; s++)
                {
                    var s1 = (s + 1) % sides;
                    tris[k++] = a + s; tris[k++] = b + s; tris[k++] = b + s1;
                    tris[k++] = a + s; tris[k++] = b + s1; tris[k++] = a + s1;
                }
            }
            mesh = new Mesh();
            mesh.name = "Cable";
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void LateUpdate()
        {
            if (from == null || to == null) return;
            if (reel)
                CablePath.Reel(from.position, from.TransformDirection(fromAxis), to.position, to.TransformDirection(toAxis),
                    reelSlack, stiffness, path);
            else
                CablePath.Curve(from.position, from.TransformDirection(fromAxis), to.position, to.TransformDirection(toAxis),
                    length, stiffness, path);
            Guard();
            Build();
        }

        /// <summary>
        /// 体の中に入った道筋の点を外へ押し出す。押しては隣の点と均すのを繰り返し、最後に押し出しだけをもう一度掛ける
        /// （均した後に中へ戻った点を残さない）。両端は動かさない
        /// </summary>
        void Guard()
        {
            if (avoid == null || avoid.Length == 0) return;
            for (var pass = 0; pass < 4; pass++)
            {
                Push();
                for (var i = 1; i < path.Length - 1; i++)
                    path[i] = Vector3.Lerp(path[i], (path[i - 1] + path[i + 1]) * 0.5f, 0.35f);
            }
            Push();
            Push();
        }

        void Push()
        {
            for (var i = 1; i < path.Length - 1; i++)
            {
                foreach (var a in avoid)
                {
                    if (a.from == null || a.to == null) continue;
                    var c = Closest(a.from.position, a.to.position, path[i]);
                    var d = path[i] - c;
                    var keep = a.radius + radius + clearance;
                    var m = d.magnitude;
                    if (m >= keep) continue;
                    // 芯の上に乗った点は、芯に直交するどちらかへ出す
                    var dir = m > 1e-5f ? d / m : Vector3.Cross(a.to.position - a.from.position, Vector3.up).normalized;
                    path[i] = c + dir * keep;
                }
            }
        }

        /// <summary>線分 ab の上で p にいちばん近い点</summary>
        public static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            var len = ab.sqrMagnitude;
            if (len < 1e-10f) return a;
            var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return a + ab * t;
        }

        /// <summary>刻んだ点に沿って筒を張る。節ごとに前後の向きから断面を立てる</summary>
        void Build()
        {
            for (var i = 0; i < points; i++)
            {
                var ahead = path[Mathf.Min(i + 1, points - 1)];
                var behind = path[Mathf.Max(i - 1, 0)];
                var dir = ahead - behind;
                if (dir.sqrMagnitude < 1e-10f) dir = Vector3.up;
                var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                for (var s = 0; s < sides; s++)
                {
                    var a = (float)s / sides * Mathf.PI * 2f;
                    var off = rot * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                    var index = i * sides + s;
                    verts[index] = transform.InverseTransformPoint(path[i] + off);
                    normals[index] = transform.InverseTransformDirection(off.normalized);
                }
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
