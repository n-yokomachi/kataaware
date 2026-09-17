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
            CablePath.Sag(from.position, to.position, length, path);
            Build();
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
