using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 小さな物の描く回数を減らす（<c>docs/superpowers/plans/2026-10-12-mesh-weight-survey.md</c> の「先に行うもの」）。
    /// 入れ物の下の MeshRenderer を、マテリアルと描き方（影・層・プローブ）が同じ物ごとに 1 つの mesh へまとめ、入れ物の直下に置く。
    ///
    /// 静的バッチ（BatchingStatic の印）は使わない。WebGL は Player Settings で静的バッチを切ってあり、印を付けても束ねられない。
    /// SRP Batcher はマテリアルの切り替えを軽くするだけで、描く回数そのものは減らさない。なので組み立ての時に mesh を結ぶ。
    ///
    /// まとめない物:
    /// - 有効でない物と、その子（後から出し入れする物かもしれない）
    /// - LODGroup・Animator・Animation・MonoBehaviour（URP が灯りに付ける追加の値は除く）を持つ物と、その子。
    ///   スクリプトの欄が指している物と、その子（動かす・出し入れする物かもしれない）
    /// - 不透明でないマテリアル（透ける物は物ごとの距離で描く順が決まるので、まとめると前後が入れ替わりうる）
    /// - マテリアルが 2 つ以上の物、mesh が無い物
    /// - <c>skip</c> が true を返す物（見直しが一つずつ読む物など）
    /// - 同じ組に 1 つしか無い物（まとめても描く回数が減らない）
    ///
    /// 元の物からは MeshRenderer と MeshFilter だけを外す。当たり判定・灯り・子が残らない物は消す（<c>keep</c> なら形だけ残す）。
    /// <c>hide</c> なら元の物は何も外さず、MeshRenderer を切るだけにする（名前で物を引いて形や広がりを読む道具が多い自室で使う。
    /// 切った MeshRenderer の bounds はそのまま読める）。切った物は次のまとめでは数えないので、組み直す道具は先にまとめを解く。
    /// 頂点は入れ物の座へ写す。鏡に映した置き方（拡縮が負）の物は面の向きを返す
    /// </summary>
    public static class MeshMerge
    {
        /// <summary>まとめた物の名の頭。見直しが、まとめた物を一つずつの物と取り違えないように使う</summary>
        public const string Prefix = "Merged";

        /// <summary>まとめた物か</summary>
        public static bool IsMerged(Transform t)
        {
            return t != null && t.name.StartsWith(Prefix + ".");
        }

        sealed class Group
        {
            public Material material;
            public MeshRenderer first;
            public readonly List<MeshRenderer> parts = new List<MeshRenderer>();
        }

        /// <summary>
        /// holder の下をまとめる。mesh は assetDir に「Merged_{name}_{マテリアル}.asset」で置く（組み直すたびに中身を入れ替え、GUID は保つ）。
        /// まとめた物の数（元のレンダラーの数）を返し、log に組ごとの内訳を書く
        /// </summary>
        public static int Merge(Transform holder, string assetDir, string name, System.Func<MeshRenderer, bool> skip = null,
            bool keep = false, System.Text.StringBuilder log = null, bool hide = false)
        {
            if (holder == null) return 0;
            var held = Held(holder);
            var groups = new List<Group>();
            var byKey = new Dictionary<string, Group>();
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!Eligible(r, holder, held)) continue;
                if (skip != null && skip(r)) continue;
                var key = Key(r);
                Group g;
                if (!byKey.TryGetValue(key, out g))
                {
                    g = new Group { material = r.sharedMaterial, first = r };
                    byKey[key] = g;
                    groups.Add(g);
                }
                g.parts.Add(r);
            }

            var merged = 0;
            var names = new HashSet<string>();
            foreach (var g in groups)
            {
                if (g.parts.Count < 2) continue;
                var label = g.material.name;
                var n = 1;
                while (!names.Add(label)) label = g.material.name + "_" + ++n;
                var mesh = Combine(holder, g.parts, Prefix + "_" + name + "_" + label);
                var saved = Save(mesh, assetDir + Prefix + "_" + name + "_" + label + ".asset");
                Place(holder, g, saved, Prefix + "." + label);
                if (log != null)
                    log.AppendFormat("  {0}: {1} 個 → 1（頂点 {2}、ポリゴン数 {3}）", label, g.parts.Count, saved.vertexCount, saved.triangles.Length / 3).AppendLine();
                foreach (var r in g.parts)
                {
                    if (hide) r.enabled = false;
                    else Strip(r, holder, keep);
                }
                merged += g.parts.Count;
            }
            AssetDatabase.SaveAssets();
            return merged;
        }

        /// <summary>
        /// hide でまとめた物を解く。holder の直下のまとめた物を消し、owned が true を返す MeshRenderer を点け直す。
        /// 消した数を返す。組み直す道具が、置き直す前に呼ぶ
        /// </summary>
        public static int Unmerge(Transform holder, System.Func<MeshRenderer, bool> owned)
        {
            if (holder == null) return 0;
            var gone = 0;
            for (var i = holder.childCount - 1; i >= 0; i--)
            {
                var c = holder.GetChild(i);
                if (!IsMerged(c)) continue;
                Object.DestroyImmediate(c.gameObject);
                gone++;
            }
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
                if (!r.enabled && owned(r)) r.enabled = true;
            return gone;
        }

        /// <summary>まとめてよい物か</summary>
        static bool Eligible(MeshRenderer r, Transform holder, HashSet<Transform> held)
        {
            if (!r.enabled) return false;
            if (r.sharedMaterials.Length != 1 || r.sharedMaterial == null) return false;
            // URP は 2500 までを不透明として前から描き、その先を透ける物として距離の順に描く
            if (r.sharedMaterial.renderQueue > (int)RenderQueue.GeometryLast) return false;
            var f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null || f.sharedMesh.subMeshCount != 1) return false;
            if (f.sharedMesh.GetTopology(0) != MeshTopology.Triangles) return false;
            for (var t = r.transform; t != null && t != holder; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (held.Contains(t)) return false;
                if (t.GetComponent<LODGroup>() != null || t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null) return false;
                if (Scripted(t)) return false;
            }
            return true;
        }

        /// <summary>スクリプトを持つか。灯りとカメラに URP が付ける追加の値（UniversalAdditionalLightData など）は数えない</summary>
        static bool Scripted(Transform t)
        {
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null) return true;
                var ns = mb.GetType().Namespace ?? "";
                if (!ns.StartsWith("UnityEngine.Rendering")) return true;
            }
            return false;
        }

        /// <summary>同じ組か。マテリアルと、描き方の設定がすべて同じ物</summary>
        static string Key(MeshRenderer r)
        {
            return string.Join("|", new[]
            {
                r.sharedMaterial.GetInstanceID().ToString(),
                ((int)r.shadowCastingMode).ToString(),
                r.receiveShadows.ToString(),
                r.gameObject.layer.ToString(),
                r.renderingLayerMask.ToString(),
                ((int)r.lightProbeUsage).ToString(),
                ((int)r.reflectionProbeUsage).ToString(),
                ((int)r.motionVectorGenerationMode).ToString(),
                r.staticShadowCaster.ToString(),
                r.probeAnchor != null ? r.probeAnchor.GetInstanceID().ToString() : "-",
                ((int)GameObjectUtility.GetStaticEditorFlags(r.gameObject)).ToString(),
            });
        }

        /// <summary>
        /// 場面の MonoBehaviour の欄が指している物。指されている物とその子はまとめない（スクリプトが動かす・出し入れする・形を読むかもしれない）。
        /// 部品そのものの欄（m_GameObject・m_Script など m_ で始まる物）は数えない
        /// </summary>
        static HashSet<Transform> Held(Transform holder)
        {
            var held = new HashSet<Transform>();
            var scene = holder.gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    var enter = true;
                    while (it.Next(enter))
                    {
                        enter = true;
                        if (it.depth == 0 && it.name.StartsWith("m_")) { enter = false; continue; }
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var o = it.objectReferenceValue;
                        Transform t = null;
                        if (o is GameObject) t = ((GameObject)o).transform;
                        else if (o is Component) t = ((Component)o).transform;
                        if (t != null) held.Add(t);
                    }
                }
            return held;
        }

        /// <summary>組の物を入れ物の座で 1 つの mesh にする。uv1 と頂点色は全部が持つときだけ写す</summary>
        static Mesh Combine(Transform holder, List<MeshRenderer> parts, string name)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tans = new List<Vector4>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var cols = new List<Color>();
            var tris = new List<int>();
            bool hasN = true, hasT = true, hasU0 = true, hasU1 = true, hasC = true;
            foreach (var r in parts)
            {
                var m = r.GetComponent<MeshFilter>().sharedMesh;
                var n = m.vertexCount;
                hasN &= m.normals.Length == n;
                hasT &= m.tangents.Length == n;
                hasU0 &= m.uv.Length == n;
                hasU1 &= m.uv2.Length == n;
                hasC &= m.colors.Length == n;
            }
            var into = holder.worldToLocalMatrix;
            foreach (var r in parts)
            {
                var m = r.GetComponent<MeshFilter>().sharedMesh;
                var at = into * r.transform.localToWorldMatrix;
                var normal = at.inverse.transpose;
                var mirror = at.determinant < 0f;
                var start = verts.Count;
                var v = m.vertices;
                for (var i = 0; i < v.Length; i++) verts.Add(at.MultiplyPoint3x4(v[i]));
                if (hasN) foreach (var x in m.normals) norms.Add(normal.MultiplyVector(x).normalized);
                if (hasT)
                    foreach (var x in m.tangents)
                    {
                        var d = at.MultiplyVector(new Vector3(x.x, x.y, x.z)).normalized;
                        tans.Add(new Vector4(d.x, d.y, d.z, mirror ? -x.w : x.w));
                    }
                if (hasU0) uv0.AddRange(m.uv);
                if (hasU1) uv1.AddRange(m.uv2);
                if (hasC) cols.AddRange(m.colors);
                var t = m.GetTriangles(0);
                for (var i = 0; i < t.Length; i += 3)
                {
                    tris.Add(start + t[i]);
                    // 鏡に映した置き方は、面の向きを返さないと裏返って見える
                    tris.Add(start + t[mirror ? i + 2 : i + 1]);
                    tris.Add(start + t[mirror ? i + 1 : i + 2]);
                }
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (hasN) mesh.SetNormals(norms);
            if (hasT) mesh.SetTangents(tans);
            if (hasU0) mesh.SetUVs(0, uv0);
            if (hasU1) mesh.SetUVs(1, uv1);
            if (hasC) mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            if (!hasN) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>mesh をアセットに置く。前のアセットがあれば中身だけ入れ替えて GUID を保つ（場面の参照が切れない）</summary>
        static Mesh Save(Mesh mesh, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
            }
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, old);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(old);
            return old;
        }

        /// <summary>まとめた物を入れ物の直下に置く。描き方は組の最初の物から写す</summary>
        static void Place(Transform holder, Group g, Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            go.layer = g.first.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(g.first.gameObject));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = g.material;
            r.shadowCastingMode = g.first.shadowCastingMode;
            r.receiveShadows = g.first.receiveShadows;
            r.renderingLayerMask = g.first.renderingLayerMask;
            r.lightProbeUsage = g.first.lightProbeUsage;
            r.reflectionProbeUsage = g.first.reflectionProbeUsage;
            r.motionVectorGenerationMode = g.first.motionVectorGenerationMode;
            r.staticShadowCaster = g.first.staticShadowCaster;
            r.allowOcclusionWhenDynamic = g.first.allowOcclusionWhenDynamic;
            r.probeAnchor = g.first.probeAnchor;
        }

        /// <summary>元の物から描く部品を外す。何も残らない物は消し、空になった親も入れ物の手前まで消す（keep なら残す）</summary>
        static void Strip(MeshRenderer r, Transform holder, bool keep)
        {
            var go = r.gameObject;
            var f = go.GetComponent<MeshFilter>();
            Object.DestroyImmediate(r);
            if (f != null) Object.DestroyImmediate(f);
            if (keep) return;
            var t = go.transform;
            while (t != null && t != holder && Empty(t))
            {
                var up = t.parent;
                Object.DestroyImmediate(t.gameObject);
                t = up;
            }
        }

        static bool Empty(Transform t)
        {
            return t.childCount == 0 && t.GetComponents<Component>().Length == 1;
        }
    }
}
