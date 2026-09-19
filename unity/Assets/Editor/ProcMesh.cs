using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 断面を並べて皮を張り、mesh を作る。箱を積むだけでは出せない、
    /// 先細りや丸みのある形を組むために使う。エディタ専用で、実行時には入らない。
    /// </summary>
    public static class ProcMesh
    {
        /// <summary>
        /// 輪 1 枚。centre を中心に、幅 rx と厚み ry の楕円を、rot の向きで置く。
        /// roll は輪の中での回転で、断面をひねりたいときに使う
        /// </summary>
        public struct Ring
        {
            public Vector3 centre;
            public float rx;
            public float ry;
            public Quaternion rot;

            public Ring(Vector3 centre, float rx, float ry)
            {
                this.centre = centre;
                this.rx = rx;
                this.ry = ry;
                this.rot = Quaternion.identity;
            }

            public Ring(Vector3 centre, float rx, float ry, Quaternion rot)
            {
                this.centre = centre;
                this.rx = rx;
                this.ry = ry;
                this.rot = rot;
            }
        }

        /// <summary>
        /// 輪を順に繋いで筒を張る。segments は 1 周の分割数。
        /// 端は平らな蓋で閉じる。半径 0 の輪を端に置けば、尖った先になる
        /// </summary>
        public static Mesh Loft(IList<Ring> rings, int segments, bool capStart = true, bool capEnd = true)
        {
            if (rings == null || rings.Count < 2) return null;
            if (segments < 3) segments = 3;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (var r = 0; r < rings.Count; r++)
            {
                var ring = rings[r];
                var v = rings.Count > 1 ? (float)r / (rings.Count - 1) : 0f;
                for (var s = 0; s < segments; s++)
                {
                    var a = (float)s / segments * Mathf.PI * 2f;
                    var local = new Vector3(Mathf.Cos(a) * ring.rx, Mathf.Sin(a) * ring.ry, 0f);
                    verts.Add(ring.centre + ring.rot * local);
                    uvs.Add(new Vector2((float)s / segments, v));
                }
            }

            for (var r = 0; r < rings.Count - 1; r++)
            {
                var a0 = r * segments;
                var b0 = (r + 1) * segments;
                for (var s = 0; s < segments; s++)
                {
                    var s1 = (s + 1) % segments;
                    tris.Add(a0 + s); tris.Add(b0 + s); tris.Add(b0 + s1);
                    tris.Add(a0 + s); tris.Add(b0 + s1); tris.Add(a0 + s1);
                }
            }

            if (capStart) AddCap(verts, uvs, tris, rings[0], segments, 0, true);
            if (capEnd) AddCap(verts, uvs, tris, rings[rings.Count - 1], segments, (rings.Count - 1) * segments, false);

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddCap(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Ring ring, int segments, int ringStart, bool flip)
        {
            var centreIndex = verts.Count;
            verts.Add(ring.centre);
            uvs.Add(new Vector2(0.5f, flip ? 0f : 1f));
            for (var s = 0; s < segments; s++)
            {
                var s1 = (s + 1) % segments;
                if (flip)
                {
                    tris.Add(centreIndex); tris.Add(ringStart + s1); tris.Add(ringStart + s);
                }
                else
                {
                    tris.Add(centreIndex); tris.Add(ringStart + s); tris.Add(ringStart + s1);
                }
            }
        }

        /// <summary>幾つかの mesh を 1 つにまとめる。部位ごとに張った皮を 1 つの物にするのに使う</summary>
        public static Mesh Combine(IList<Mesh> parts, IList<Matrix4x4> places)
        {
            var combine = new CombineInstance[parts.Count];
            for (var i = 0; i < parts.Count; i++)
            {
                combine[i].mesh = parts[i];
                combine[i].transform = places != null && i < places.Count ? places[i] : Matrix4x4.identity;
            }
            var mesh = new Mesh();
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>作った mesh をアセットとして保存する。場面から参照できるようにするため</summary>
        public static Mesh Save(Mesh mesh, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir)) System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + dir);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                // 頂点が 65535 を超える形は 32 bit の索引が要る。
                // 以前の資産が 16 bit のままだと、ここで黙って切り捨てられる
                existing.indexFormat = mesh.indexFormat;
                existing.SetVertices(new List<Vector3>(mesh.vertices));
                existing.SetUVs(0, new List<Vector2>(mesh.uv));
                existing.SetTriangles(mesh.triangles, 0);
                var normals = mesh.normals;
                if (normals != null && normals.Length == mesh.vertexCount) existing.SetNormals(normals);
                else existing.RecalculateNormals();
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }
    }
}
