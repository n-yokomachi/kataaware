using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 麦わら帽子（参考画像の、つばの広い帽子）。コードでメッシュと UV と絵を作り、頭の骨に固く付ける（曲がらない）。
    /// - 山: 低めの丸みのある筒（頭の形に合わせた楕円）。天辺はわずかに丸く盛る
    /// - つば: 平らで、縁がわずかに下がる。幅は <see cref="BrimWidth"/>
    /// - リボン: 山の付け根に白い帯を一巻き
    /// - 絵: 麦わらの編み目（同心の筋と放射の目の市松）
    /// 被り方は <see cref="RocketboxPaint.Look"/> の hatTilt（前後の傾き、正で前が下がる）・hatRoll（左右の傾き）・hatDepth（上下、正で上）・hatShift（前後、正で前）で直す。
    /// 被るときは、帽子の中に入る髪の頂点（頭の骨に付いた頂点で、山の付け根より上）を山の内側へ寄せる
    /// </summary>
    public static class RocketboxHat
    {
        /// <summary>山の高さ・天辺の盛り・山と髪のゆとり・つばの幅・つばの縁の下がり・リボンの高さ</summary>
        public const float CrownHeight = 0.080f, CrownDome = 0.014f, CrownMargin = 0.010f, BrimWidth = 0.100f, BrimDroop = 0.018f, RibbonHeight = 0.028f;
        /// <summary>山の天辺の細り（付け根の半径に対する割合）</summary>
        public const float CrownTaper = 0.90f;
        /// <summary>山の付け根（つば）の高さ（目の高さから上へ）の基準。Look.hatDepth（既定 −3.4 cm）を足した所に被る。既定では後ろへ 22° 傾け（hatTilt）、1.1 cm 後ろへずらす（hatShift）</summary>
        public const float BandAboveEyes = 0.068f;
        const int Segs = 64, SideRings = 6, TopRings = 6, BrimRings = 6;
        /// <summary>麦わらの色（sRGB）</summary>
        public static readonly Color Straw = new Color(0.90f, 0.78f, 0.57f);

        /// <summary>帽子の形の基準（束ねた姿勢の、模型の根の中）: 頭の真ん中・つばの高さ・山の楕円の半径</summary>
        sealed class Fit
        {
            public Vector3 centre;
            public float rx, rz;
            public Quaternion rot = Quaternion.identity;
        }

        static Fit Measure(SkinnedMeshRenderer smr, Transform root, Transform head, RocketboxPaint.Anchors a, RocketboxPaint.Look k)
        {
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            var bandY = eyeY + BandAboveEyes + k.hatDepth;
            // 山の大きさは深さによらず、浅めの被り方の高さで測る（深い所で測ると、ボブの広がった髪で山が大きくなった）
            var measureY = eyeY + BandAboveEyes;
            var mesh = smr.sharedMesh;
            var v = mesh.vertices;
            var bw = mesh.boneWeights;
            var headIdx = Array.IndexOf(smr.bones, head);
            var pts = new List<Vector3>();
            for (var i = 0; i < v.Length; i++)
            {
                if (bw[i].boneIndex0 != headIdx || bw[i].weight0 < 0.5f) continue;
                var p = root.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                if (Mathf.Abs(p.y - measureY) > 0.012f) continue;
                pts.Add(p);
            }
            var f = new Fit();
            if (pts.Count < 8)
            {
                f.centre = new Vector3(0f, bandY, a.eyeL.z - 0.075f);
                f.rx = 0.095f;
                f.rz = 0.11f;
            }
            else
            {
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); }
                f.centre = new Vector3((x0 + x1) * 0.5f, bandY, (z0 + z1) * 0.5f);
                // 深く被ると、測った高さより下の頭は少し広いので、下げた分だけゆとりを足す
                var extra = Mathf.Max(0f, -k.hatDepth) * 0.25f;
                x1 += extra; x0 -= extra; z1 += extra; z0 -= extra;
                f.rx = (x1 - x0) * 0.5f + CrownMargin;
                f.rz = (z1 - z0) * 0.5f + CrownMargin;
            }
            f.centre.z += k.hatShift;
            f.rot = Quaternion.Euler(k.hatTilt, 0f, k.hatRoll);
            return f;
        }

        /// <summary>帽子のメッシュ（模型の根の中の位置）。UV: u は向き、v は天辺の真ん中からの面に沿った距離（リボンは 0.94〜1）</summary>
        static Mesh Build(Fit f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const float sTotal = 0.30f;
            Func<float, float, float, Vector3> at = (ang, rho, h) =>
            {
                var local = new Vector3(Mathf.Sin(ang) * f.rx * rho, h, Mathf.Cos(ang) * f.rz * rho);
                return f.centre + f.rot * local;
            };
            Action<Func<int, float, Vector3>, Func<int, float>, int, bool> grid = (pos, vOf, rings, flip) =>
            {
                var start = verts.Count;
                for (var j = 0; j <= rings; j++)
                    for (var k = 0; k <= Segs; k++)
                    {
                        var ang = k / (float)Segs * 2f * Mathf.PI;
                        verts.Add(pos(j, ang));
                        uvs.Add(new Vector2(k / (float)Segs, vOf(j)));
                    }
                for (var j = 0; j < rings; j++)
                    for (var k = 0; k < Segs; k++)
                    {
                        int a0 = start + j * (Segs + 1) + k, a1 = a0 + 1, b0 = a0 + Segs + 1, b1 = b0 + 1;
                        if (!flip) { tris.Add(a0); tris.Add(b0); tris.Add(b1); tris.Add(a0); tris.Add(b1); tris.Add(a1); }
                        else { tris.Add(a0); tris.Add(b1); tris.Add(b0); tris.Add(a0); tris.Add(a1); tris.Add(b1); }
                    }
            };
            var rTop = (f.rx + f.rz) * 0.5f;
            // 天辺（真ん中から縁へ。わずかに盛る）
            grid((j, ang) =>
            {
                var rho = j / (float)TopRings;
                return at(ang, Mathf.Max(rho, 0.001f) * CrownTaper * (1f - 0.08f * rho * rho * rho), CrownHeight + CrownDome * (1f - rho * rho));
            }, j => rTop * (j / (float)TopRings) / sTotal * 0.94f, TopRings, false);
            // 山の横（天辺の縁からリボンの上まで）。縁は少し丸める
            grid((j, ang) =>
            {
                // 天辺の縁（細って丸い）から付け根へ広がる
                var t = j / (float)SideRings;
                var h = Mathf.Lerp(CrownHeight, RibbonHeight, t);
                var taper = Mathf.Lerp(CrownTaper * 0.92f, 1f, 1f - Mathf.Pow(1f - t, 1.6f));
                return at(ang, taper, h);
            }, j => (rTop + (CrownHeight - RibbonHeight) * (j / (float)SideRings)) / sTotal * 0.94f, SideRings, true);
            // リボン（山より 1.5 mm 外）。下の縁はつばの内の縁と同じ輪（同じ頂点の位置）にして継ぎ目を閉じる
            // （つばの内の縁をリボンの下へ重ねていたら、後ろへ傾けたとき重なりがぎざぎざに見え、隙間を空けると帽子の内側が覗いた）
            var ribbonGrow = 1f + 0.0015f / rTop;
            grid((j, ang) => at(ang, ribbonGrow, Mathf.Lerp(RibbonHeight, 0f, j / 2f)), j => 0.95f + 0.05f * (j / 2f), 2, true);
            // つば（リボンの下の縁から外へ。縁がわずかに下がる）
            grid((j, ang) =>
            {
                var t = j / (float)BrimRings;
                var rho = ribbonGrow + BrimWidth / rTop * t;
                return at(ang, rho, -BrimDroop * t * t);
            }, j => (rTop + CrownHeight - RibbonHeight + BrimWidth * (j / (float)BrimRings)) / sTotal * 0.94f, BrimRings, true);
            // 三角の巻きを外向きに揃える（天辺とつばは上、山の横とリボンは軸から外）。巻きが内向きだと、両面で描いても内向きの法線で照らされて暗く見えた
            var up = f.rot * Vector3.up;
            for (var t = 0; t < tris.Count; t += 3)
            {
                Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                var fn = Vector3.Cross(p1 - p0, p2 - p0);
                var c = (p0 + p1 + p2) / 3f;
                var rel = c - f.centre;
                var h = Vector3.Dot(rel, up);
                var radial = rel - up * h;
                // 向きで分ける（高さで分けると、リボンの下の段の三角が上向き扱いになって裏返り、裏の麦わらの色が歯のように覗いた）
                var side = Mathf.Abs(Vector3.Dot(fn.normalized, up)) < 0.7f;
                var want = side ? radial : up;
                if (Vector3.Dot(fn, want) < 0f) { var tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
            }
            // 内側の面: 頂点を写して三角の向きを逆にする（法線は内向きになり、帽子の内側とつばの裏が麦わらの色で陰る）。
            // 両面を一枚で描くと内側が外向きの法線で照らされて暗い灰色の帯に見えた。リボンの内側は麦わらの色にする（こめかみで白く覗かないように）
            var outer = verts.Count;
            var outerTris = tris.Count;
            for (var i = 0; i < outer; i++)
            {
                verts.Add(verts[i]);
                var uv = uvs[i];
                if (uv.y > 0.945f) uv.y = 0.90f;
                uvs.Add(uv);
            }
            for (var t = 0; t < outerTris; t += 3)
            {
                tris.Add(tris[t] + outer);
                tris.Add(tris[t + 2] + outer);
                tris.Add(tris[t + 1] + outer);
            }
            var m = new Mesh { name = "Hat" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>麦わらの絵（n×n）。v がリボンの所（0.94 より上）は白い帯、ほかは同心の筋と放射の目の市松</summary>
        public static Color[] Paint(int n)
        {
            var px = new Color[n * n];
            var lin = new Vector3(Mathf.GammaToLinearSpace(Straw.r), Mathf.GammaToLinearSpace(Straw.g), Mathf.GammaToLinearSpace(Straw.b));
            var white = new Vector3(Mathf.GammaToLinearSpace(0.99f), Mathf.GammaToLinearSpace(0.99f), Mathf.GammaToLinearSpace(0.97f));
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var u = (x + 0.5f) / n;
                    var v = (y + 0.5f) / n;
                    Vector3 c;
                    if (v > 0.945f)
                    {
                        var sh = 1f - 0.08f * RocketboxPaint.Smooth(0.006f, 0f, Mathf.Min(v - 0.945f, 1f - v));
                        c = white * sh;
                    }
                    else
                    {
                        // 同心の筋（面に沿って 6 mm おき）と、放射の目（一周 96）の市松
                        var s = v / 0.94f * 0.30f;
                        var ring = s / 0.006f;
                        var ringF = Mathf.Repeat(ring, 1f);
                        var spoke = u * 96f;
                        var cell = ((int)Mathf.Floor(ring) + (int)Mathf.Floor(spoke)) & 1;
                        var sh = 1f + (cell == 0 ? 0.07f : -0.07f);
                        sh *= 1f - 0.22f * RocketboxPaint.Smooth(0.16f, 0f, Mathf.Min(ringF, 1f - ringF));
                        sh *= 1f + (Hash(x, y) - 0.5f) * 0.10f;
                        c = lin * sh;
                    }
                    px[y * n + x] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(c.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.z)), 1f);
                }
            return px;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>
        /// 帽子を被せる（束ねた姿勢のうちに呼ぶ）。帽子のメッシュを頭の骨の子に置き、帽子の中に入る髪の頂点を山の内側へ寄せる。
        /// persistDir が null でなければ、帽子のメッシュ・絵・マテリアルと髪を寄せたメッシュをアセットとして書く（場面に置くとき）。
        /// made には、アセットにしないで作った物を入れる
        /// </summary>
        public static string Put(GameObject her, SkinnedMeshRenderer smr, RocketboxPaint.Anchors a, RocketboxPaint.Look k, string persistDir, List<Object> made)
        {
            Transform head = null;
            foreach (var t in her.GetComponentsInChildren<Transform>(true)) if (t.name == "Bip01 Head") head = t;
            if (head == null) return "帽子: 頭の骨が無い";
            var root = her.transform;
            var f = Measure(smr, root, head, a, k);
            var mesh = Build(f);
            // 模型の根の中の位置 → 頭の骨の中へ
            var v = mesh.vertices;
            for (var i = 0; i < v.Length; i++) v[i] = head.InverseTransformPoint(root.TransformPoint(v[i]));
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Texture2D tex;
            Material mat;
            if (persistDir != null)
            {
                RocketboxCompose.Save(mesh, persistDir + "Hat_mesh.asset");
                Object.DestroyImmediate(mesh);
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(persistDir + "Hat_mesh.asset");
                var c32 = Array.ConvertAll(Paint(256), c => (Color32)c);
                RocketboxTextures.WritePng(c32, 256, 256, persistDir + "Painted/Hat.png", false);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(persistDir + "Painted/Hat.png");
                mat = BuildRocketboxProtagonist.Lit("Hat", tex, 0.05f, false);
                var mp = persistDir + "Painted/Hat.mat";
                var old = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (old != null) { old.CopyPropertiesFromMaterial(mat); old.shaderKeywords = mat.shaderKeywords; EditorUtility.SetDirty(old); Object.DestroyImmediate(mat); mat = old; }
                else AssetDatabase.CreateAsset(mat, mp);
            }
            else
            {
                made.Add(mesh);
                tex = BuildRocketboxProtagonist.Tex(Paint(256), 256, false, 256);
                made.Add(tex);
                mat = BuildRocketboxProtagonist.Lit("Hat", tex, 0.05f, false);
                made.Add(mat);
            }
            var go = new GameObject("Hat");
            go.transform.SetParent(head, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.hideFlags = her.hideFlags;

            // 帽子の中に入る髪: 頭の骨に付いた頂点で、つばの面より上。その高さの山の内側の 92 % より外なら 92 % へ、天辺より上なら天辺の 8 mm 下へ
            var hm = Object.Instantiate(smr.sharedMesh);
            hm.name = smr.sharedMesh.name + "_hat";
            var hv = hm.vertices;
            var bwts = hm.boneWeights;
            var headIdx = Array.IndexOf(smr.bones, head);
            var inv = Quaternion.Inverse(f.rot);
            int pushed = 0;
            for (var i = 0; i < hv.Length; i++)
            {
                if (bwts[i].boneIndex0 != headIdx || bwts[i].weight0 < 0.5f) continue;
                var p = root.InverseTransformPoint(smr.transform.TransformPoint(hv[i]));
                var q = inv * (p - f.centre);
                // つばの面より上（2 mm 上から）は山の内側へ全部寄せる。つばの面の近くで山より外にある髪は、つばの下 5 mm へ下げる
                // （後ろへ傾けると、山の後ろの付け根で、リボンとつばの間から髪が細い線になって覗いた）
                if (q.y < -0.02f) continue;
                var ex = q.x / f.rx;
                var ez = q.z / f.rz;
                var rho = Mathf.Sqrt(ex * ex + ez * ez);
                var moved = false;
                var ts = Mathf.Clamp01((CrownHeight - q.y) / (CrownHeight - RibbonHeight));
                var allowed = 0.92f * Mathf.Lerp(CrownTaper * 0.92f, 1f, 1f - Mathf.Pow(1f - ts, 1.6f));
                if (rho > allowed)
                {
                    if (q.y >= 0.002f)
                    {
                        q.x *= allowed / rho;
                        q.z *= allowed / rho;
                        moved = true;
                    }
                    else
                    {
                        var rTop = (f.rx + f.rz) * 0.5f;
                        var tb = Mathf.Clamp01((rho - 1f) / (BrimWidth / rTop));
                        var under = -BrimDroop * tb * tb - 0.005f;
                        if (q.y > under) { q.y = under; moved = true; }
                    }
                }
                var top = CrownHeight + CrownDome * (1f - Mathf.Min(1f, rho * rho)) - 0.008f;
                if (q.y > top) { q.y = top; moved = true; }
                if (!moved) continue;
                hv[i] = smr.transform.InverseTransformPoint(root.TransformPoint(f.centre + f.rot * q));
                pushed++;
            }
            hm.vertices = hv;
            hm.RecalculateBounds();
            if (persistDir != null)
            {
                var path = persistDir + her.name + "_hat_hair_mesh.asset";
                RocketboxCompose.Save(hm, path);
                Object.DestroyImmediate(hm);
                hm = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            else made.Add(hm);
            smr.sharedMesh = hm;
            return string.Format(CultureInfo.InvariantCulture,
                "帽子: 山の楕円 {0:0.0}×{1:0.0} cm（髪との間 {2:0} mm）、山の高さ {3:0.0} cm、つばの幅 {4:0.0} cm、つばの高さ 目の {5:0.0} cm 上、傾き 前後 {6:0.0}°・左右 {7:0.0}°、前後のずれ {8:0.0} cm、帽子の中へ寄せた髪の頂点 {9}",
                f.rx * 200f, f.rz * 200f, CrownMargin * 1000f, CrownHeight * 100f, BrimWidth * 100f, (BandAboveEyes + k.hatDepth) * 100f, k.hatTilt, k.hatRoll, k.hatShift * 100f, pushed);
        }
    }
}
