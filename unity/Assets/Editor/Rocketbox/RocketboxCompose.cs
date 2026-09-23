using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 頭の人の頭・髪・まつ毛・目の玉と、体の人の体（服・手・靴）を一つのメッシュにする（<see cref="RocketboxPerson.Compose"/>）。
    /// Rocketbox の女性は骨の並びと束ねた姿勢が同じなので、頭の人の頂点の骨の重みを、骨の名前で体の人の骨の番号へ付け替えるだけで載る。
    ///
    /// 頭の面（頭の人の面の組 1）は顔・頭皮・首・胸の上までを含み、服の襟ぐりと胸の形は人によって違う。そこで束ねた姿勢で次を直す。
    /// 直すのは首元から下（目の高さから 11 cm 下で 0、17 cm 下で全部）と、服の襟ぐりの縁から 5 cm 以内（2 cm 以内で全部）。
    /// - 体の人が肌を見せていた所（一番近い体の人の肌の点が、肌の面の縁でなく中にある）では、頭の人の肌を体の人の肌の面へ寄せる。
    ///   襟ぐりの縁が肌から浮いたり（隙間から服の内側が見える）、肌に埋もれたりしないようにする
    /// - 体の人では服の下になる所（一番近い体の人の肌の点が面の縁にある）では、頭の人の肌を服の面の 3 mm 内側へ置く
    /// - 髪（透けの面の房と、頭の面で髪と見なした所）が服の面の 4 mm 外に出ていなければ、服の面の外へ押し出す
    /// - 頭の人の肌が届かない胸元（体の人の襟ぐりの方が深い所）は、体の人の肌の三角で埋める。
    ///   その三角は 0.7 mm 内側へ下げ（頭の人の肌と重なる所で頭の人の肌が勝つように）、UV は一番近い頭の人の頂点のものを使う
    /// 骨の重みは変えない。首元の肌も長い髪も背骨（Spine1・Spine2）に付いているので、立ちと歩きでも服との間は大きく変わらない
    /// </summary>
    public static class RocketboxCompose
    {
        /// <summary>頭の人の首元の肌を、体の人の肌へ寄せ始める所と寄せ切る所（目の高さから下へ m）</summary>
        const float SnapFrom = 0.11f, SnapTo = 0.17f;
        /// <summary>体の人の肌の面を探す範囲（m）</summary>
        const float SkinReach = 0.04f;
        const float SkinUnder = 0.003f, HairOver = 0.004f;

        [MenuItem("HalfAware/Rocketbox/Build the composite meshes")]
        public static void Menu()
        {
            foreach (var who in RocketboxPerson.All) if (who.IsComposite) Debug.Log(BuildMesh(who));
        }

        /// <summary>組み合わせたメッシュを作って who.CompositeMesh に書く。直した量を返す</summary>
        public static string BuildMesh(RocketboxPerson who)
        {
            if (!who.IsComposite) throw new ArgumentException("頭と体が同じ人: " + who);
            if (who.IsHairSwap) return RocketboxHairSwap.BuildMesh(who);
            var bodySmr = Smr(who.BodyFrom.Model);
            var headSmr = Smr(who.HeadFrom.Model);
            var bm = bodySmr.sharedMesh;
            var hm = headSmr.sharedMesh;
            var bodySub = Slot(bodySmr, who.BodyFrom.BodySlot);
            var bodySkinSub = Slot(bodySmr, who.BodyFrom.HeadSlot);
            var headSub = Slot(headSmr, who.HeadFrom.HeadSlot);
            var hairSub = Slot(headSmr, who.HeadFrom.HairSlot);

            // 骨の番号を名前で付け替える
            var index = new Dictionary<string, int>();
            for (var i = 0; i < bodySmr.bones.Length; i++) index[bodySmr.bones[i].name] = i;
            var remap = new int[headSmr.bones.Length];
            for (var i = 0; i < remap.Length; i++)
            {
                int j;
                if (!index.TryGetValue(headSmr.bones[i].name, out j)) throw new InvalidOperationException("体の人に無い骨: " + headSmr.bones[i].name);
                remap[i] = j;
            }

            var bodyToWorld = bodySmr.transform.localToWorldMatrix;
            var headToWorld = headSmr.transform.localToWorldMatrix;
            var worldToBody = bodySmr.transform.worldToLocalMatrix;
            var bv = bm.vertices;
            var hv = hm.vertices;
            var bw = World(bv, bodyToWorld);
            var hw = World(hv, headToWorld);

            var bodyClothes = new Surface(bw, bm.GetTriangles(bodySub));
            var bodySkin = new Surface(bw, bm.GetTriangles(bodySkinSub));
            // 体の人の肌の面の縁（服との継ぎ目と、面の他の縁）と、服の襟ぐりの縁（肌に接していた服の頂点）
            var skinEdges = new List<Vector3[]>();
            foreach (var e in BoundaryEdges(bm.GetTriangles(bodySkinSub))) skinEdges.Add(new[] { bw[e[0]], bw[e[1]] });
            var neckline = new List<Vector3>();
            foreach (var i in Boundary(bm.GetTriangles(bodySub)))
            {
                Vector3 q0, n0;
                if (bodySkin.Closest(bw[i], 0.01f, out q0, out n0) <= 0.003f) neckline.Add(bw[i]);
            }

            // 頭の人の髪と見なす頂点（頭の面の髪の所と、透けの面のまつ毛以外）
            var maps = BuildRocketboxProtagonist.Maps.Get(who.HeadFrom, 512);
            int n;
            var headTex = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadFrom.HeadSrc, out n, out n));
            var paint = RocketboxPaint.Head(headTex, maps.Head, maps.Anchors, who.HeadFrom.Look(), false, who.IrisUv, who.IrisRadius);
            var huv = hm.uv;
            var a = maps.Anchors;
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;

            var moved = (Vector3[])hw.Clone();
            var headVerts = Used(hm.GetTriangles(headSub));
            var hairVerts = Used(hm.GetTriangles(hairSub));
            int snapped = 0, pushedIn = 0, pushedOut = 0, hairInside = 0;
            float maxSnap = 0f, maxIn = 0f, maxOut = 0f, worstHair = 0f;

            foreach (var i in headVerts)
            {
                var p = hw[i];
                var hair = HairAt(paint, huv[i]) > 0.5f;
                if (hair)
                {
                    PushOut(ref moved[i], bodyClothes, ref pushedOut, ref maxOut, ref hairInside, ref worstHair);
                    continue;
                }
                if (p.y > eyeY - 0.08f) continue;
                var wh = RocketboxPaint.Smooth(eyeY - SnapFrom, eyeY - SnapTo, p.y);
                var near = float.MaxValue;
                foreach (var e in neckline) near = Mathf.Min(near, Vector3.Distance(e, p));
                var w = Mathf.Max(wh, RocketboxPaint.Smooth(0.05f, 0.02f, near));
                if (w <= 0f) continue;
                Vector3 q, nrm;
                var d = bodySkin.Closest(p, SkinReach, out q, out nrm);
                if (!float.IsInfinity(d) && EdgeDistance(q, skinEdges) > 0.0003f)
                {
                    // 体の人が肌を見せていた所: 体の人の肌の面へ
                    moved[i] = Vector3.Lerp(p, q, w);
                    snapped++;
                    maxSnap = Mathf.Max(maxSnap, d * w);
                    continue;
                }
                // 体の人では服の下になる所: 服の面の 3 mm 内側へ
                var s = bodyClothes.Signed(p, 0.08f, out q, out nrm);
                if (float.IsNaN(s)) continue;
                moved[i] = Vector3.Lerp(p, p - nrm * (s + SkinUnder), w);
                pushedIn++;
                maxIn = Mathf.Max(maxIn, Mathf.Abs(s + SkinUnder) * w);
            }
            foreach (var i in hairVerts)
            {
                var p = hw[i];
                if (Vector3.Distance(p, a.eyeL) < 0.035f || Vector3.Distance(p, a.eyeR) < 0.035f) continue;
                PushOut(ref moved[i], bodyClothes, ref pushedOut, ref maxOut, ref hairInside, ref worstHair);
            }

            // メッシュを組む
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var bNorm = bm.normals;
            var hNorm = hm.normals;
            var bUv = bm.uv;
            var bWeights = bm.boneWeights;
            var hWeights = hm.boneWeights;
            var bodyTris = Pack(bm.GetTriangles(bodySub), i => bv[i], i => bNorm[i], i => bUv[i], i => bWeights[i], verts, norms, uvs, weights);
            Func<int, BoneWeight> hwOf = i =>
            {
                var w = hWeights[i];
                w.boneIndex0 = remap[w.boneIndex0];
                w.boneIndex1 = remap[w.boneIndex1];
                w.boneIndex2 = remap[w.boneIndex2];
                w.boneIndex3 = remap[w.boneIndex3];
                return w;
            };
            // 頭の人の頂点は、直した世界の位置を体の人のメッシュの中へ戻す（二つの SkinnedMeshRenderer は同じ置き方）
            var headLocal = new Vector3[hv.Length];
            for (var i = 0; i < hv.Length; i++) headLocal[i] = worldToBody.MultiplyPoint3x4(moved[i]);
            var headTris = Pack(hm.GetTriangles(headSub), i => headLocal[i], i => worldToBody.MultiplyVector(headToWorld.MultiplyVector(hNorm[i])).normalized, i => huv[i], hwOf, verts, norms, uvs, weights);

            // 胸元の埋め: 体の人の肌の三角のうち、頭の人の肌から離れている前側の所
            var headSurface = new Surface(moved, hm.GetTriangles(headSub));
            var skinTris = bm.GetTriangles(bodySkinSub);
            var neckZ = maps.Anchors.head.z;
            var patch = new List<int>();
            for (var t = 0; t < skinTris.Length; t += 3)
            {
                Vector3 v0 = bw[skinTris[t]], v1 = bw[skinTris[t + 1]], v2 = bw[skinTris[t + 2]];
                var c = (v0 + v1 + v2) / 3f;
                if (c.y > eyeY - 0.12f || c.z < neckZ + 0.03f) continue;
                // 三角の頂点・辺の中点・重心のどこかが頭の人の肌から 1.2 mm より離れていれば、覆われていない所がある
                var open = false;
                foreach (var pt in new[] { v0, v1, v2, (v0 + v1) * 0.5f, (v1 + v2) * 0.5f, (v2 + v0) * 0.5f, c })
                {
                    Vector3 q1, n1;
                    if (headSurface.Closest(pt, 0.02f, out q1, out n1) > 0.0012f) { open = true; break; }
                }
                if (!open) continue;
                patch.Add(skinTris[t]);
                patch.Add(skinTris[t + 1]);
                patch.Add(skinTris[t + 2]);
            }
            var headIdx = new List<int>(headVerts);
            Func<int, Vector2> patchUv = i =>
            {
                var best = float.MaxValue;
                var uv = Vector2.zero;
                foreach (var h in headIdx)
                {
                    var d = (moved[h] - bw[i]).sqrMagnitude;
                    if (d < best && HairAt(paint, huv[h]) <= 0.5f) { best = d; uv = huv[h]; }
                }
                return uv;
            };
            var patchTris = Pack(patch.ToArray(), i => worldToBody.MultiplyPoint3x4(bw[i] - bNormWorld(bodyToWorld, bNorm[i]) * 0.0007f), i => bNorm[i], patchUv, i => bWeights[i], verts, norms, uvs, weights);
            var headAll = new int[headTris.Length + patchTris.Length];
            headTris.CopyTo(headAll, 0);
            patchTris.CopyTo(headAll, headTris.Length);
            headTris = headAll;
            var hairTris = Pack(hm.GetTriangles(hairSub), i => headLocal[i], i => worldToBody.MultiplyVector(headToWorld.MultiplyVector(hNorm[i])).normalized, i => huv[i], hwOf, verts, norms, uvs, weights);

            var mesh = new Mesh { name = who.Name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bm.bindposes;
            mesh.subMeshCount = 3;
            mesh.SetTriangles(bodyTris, 0);
            mesh.SetTriangles(headTris, 1);
            mesh.SetTriangles(hairTris, 2);
            mesh.RecalculateBounds();
            Save(mesh, who.CompositeMesh);

            var finalWorld = World(verts.ToArray(), bodyToWorld);
            var seam = SeamGap(bw, bm.GetTriangles(bodySub), bodySkin, new Surface(finalWorld, headTris));
            return string.Format(CultureInfo.InvariantCulture,
                "{0}: 頂点 {1}・三角 {2}（体 {3}・頭 {4}・髪 {5}）\n" +
                "首元の肌を体の人の肌へ寄せた頂点 {6}（最大 {7:0.0} mm）、服の 3 mm 内側へ置いた頂点 {8}（最大 {9:0.0} mm 動かした）\n" +
                "髪が服の面の {13:0} mm 内に入っていた頂点 {10}（一番深い所 {11:0.0} mm）→ 押し出した頂点 {12}（最大 {14:0.0} mm）\n" +
                "胸元を体の人の肌の三角で埋めた数 {17}\n{15}\n書いた所: {16}",
                who, verts.Count, (bodyTris.Length + headTris.Length + hairTris.Length) / 3, bodyTris.Length / 3, headTris.Length / 3, hairTris.Length / 3,
                snapped, maxSnap * 1000f, pushedIn, maxIn * 1000f, hairInside, worstHair * 1000f, pushedOut, HairOver * 1000f, maxOut * 1000f,
                seam, who.CompositeMesh, patch.Count / 3);
        }

        /// <summary>
        /// 顔の人の首元の肌を、体の人（顔の人と別の人）に合わせる（<see cref="BuildMesh"/> の頭の載せ替えと同じ直し。顔と髪も別の人から取るときに使う）。
        /// skinVerts は顔の人の頭の面のうち髪でない頂点。直した世界の位置を返す
        /// </summary>
        public static Vector3[] FitNeck(RocketboxPerson body, Vector3[] headWorld, IEnumerable<int> skinVerts, float eyeY, out string note)
        {
            var bodySmr = Smr(body.Model);
            var bm = bodySmr.sharedMesh;
            var bw = World(bm.vertices, bodySmr.transform.localToWorldMatrix);
            var bodyClothes = new Surface(bw, bm.GetTriangles(Slot(bodySmr, body.BodySlot)));
            var bodySkinTris = bm.GetTriangles(Slot(bodySmr, body.HeadSlot));
            var bodySkin = new Surface(bw, bodySkinTris);
            var skinEdges = new List<Vector3[]>();
            foreach (var e in BoundaryEdges(bodySkinTris)) skinEdges.Add(new[] { bw[e[0]], bw[e[1]] });
            var neckline = new List<Vector3>();
            foreach (var i in Boundary(bm.GetTriangles(Slot(bodySmr, body.BodySlot))))
            {
                Vector3 q0, n0;
                if (bodySkin.Closest(bw[i], 0.01f, out q0, out n0) <= 0.003f) neckline.Add(bw[i]);
            }
            var moved = (Vector3[])headWorld.Clone();
            int snapped = 0, pushedIn = 0;
            float maxSnap = 0f, maxIn = 0f;
            foreach (var i in skinVerts)
            {
                var p = headWorld[i];
                if (p.y > eyeY - 0.08f) continue;
                var wh = RocketboxPaint.Smooth(eyeY - SnapFrom, eyeY - SnapTo, p.y);
                var near = float.MaxValue;
                foreach (var e in neckline) near = Mathf.Min(near, Vector3.Distance(e, p));
                var w = Mathf.Max(wh, RocketboxPaint.Smooth(0.05f, 0.02f, near));
                if (w <= 0f) continue;
                Vector3 q, nrm;
                var d = bodySkin.Closest(p, SkinReach, out q, out nrm);
                if (!float.IsInfinity(d) && EdgeDistance(q, skinEdges) > 0.0003f)
                {
                    moved[i] = Vector3.Lerp(p, q, w);
                    snapped++;
                    maxSnap = Mathf.Max(maxSnap, d * w);
                    continue;
                }
                var s = bodyClothes.Signed(p, 0.08f, out q, out nrm);
                if (float.IsNaN(s)) continue;
                moved[i] = Vector3.Lerp(p, p - nrm * (s + SkinUnder), w);
                pushedIn++;
                maxIn = Mathf.Max(maxIn, Mathf.Abs(s + SkinUnder) * w);
            }
            note = string.Format(CultureInfo.InvariantCulture, "首元の肌を体の人の肌へ寄せた頂点 {0}（最大 {1:0.0} mm）、服の 3 mm 内側へ置いた頂点 {2}（最大 {3:0.0} mm 動かした）",
                snapped, maxSnap * 1000f, pushedIn, maxIn * 1000f);
            return moved;
        }

        /// <summary>
        /// 胸元の埋め: 体の人の肌の三角のうち、載せた顔の人の肌（headSurface）から離れている前側の物（体の人のメッシュの三角の番号）。
        /// 体の人の襟ぐりの方が深く、顔の人の肌が届かない所
        /// </summary>
        public static List<int> ChestPatch(RocketboxPerson body, Surface headSurface, float eyeY, float neckZ, List<Vector3[]> headEdges = null, bool backing = false)
        {
            var bodySmr = Smr(body.Model);
            var bm = bodySmr.sharedMesh;
            var bw = World(bm.vertices, bodySmr.transform.localToWorldMatrix);
            var skinTris = bm.GetTriangles(Slot(bodySmr, body.HeadSlot));
            var patch = new List<int>();
            for (var t = 0; t < skinTris.Length; t += 3)
            {
                Vector3 v0 = bw[skinTris[t]], v1 = bw[skinTris[t + 1]], v2 = bw[skinTris[t + 2]];
                var c = (v0 + v1 + v2) / 3f;
                if (c.y > eyeY - 0.12f || c.z < neckZ + 0.03f) continue;
                // backing: 覆われている所も全部入れて、顔の人の肌の縁の細い隙間の裏打ちにする（隙間から体の中の暗がりが点になって見えたため）
                var open = backing;
                foreach (var pt in new[] { v0, v1, v2, (v0 + v1) * 0.5f, (v1 + v2) * 0.5f, (v2 + v0) * 0.5f, c })
                {
                    Vector3 q1, n1;
                    // 顔の人の肌の縁の上に一番近い点があるなら、そこは縁の外（覆われていない）。縁の近くの三角も埋めに入れて、縁の隙間を塞ぐ
                    if (headSurface.Closest(pt, 0.02f, out q1, out n1) > 0.0012f || (headEdges != null && EdgeDistance(q1, headEdges) <= 0.0003f)) { open = true; break; }
                }
                if (!open) continue;
                patch.Add(skinTris[t]);
                patch.Add(skinTris[t + 1]);
                patch.Add(skinTris[t + 2]);
            }
            return patch;
        }

        static Vector3 bNormWorld(Matrix4x4 toWorld, Vector3 n)
        {
            return toWorld.MultiplyVector(n).normalized;
        }

        static void PushOut(ref Vector3 p, Surface clothes, ref int pushed, ref float maxOut, ref int inside, ref float worst)
        {
            Vector3 q, nrm;
            var s = clothes.Signed(p, 0.05f, out q, out nrm);
            if (float.IsNaN(s) || s >= HairOver) return;
            if (s < 0f)
            {
                inside++;
                worst = Mathf.Max(worst, -s);
            }
            p = p + nrm * (HairOver - s);
            pushed++;
            maxOut = Mathf.Max(maxOut, HairOver - s);
        }

        /// <summary>
        /// 首の継ぎ目の測り。体の人の服の縁のうち、もともと体の人の肌に接していた頂点（肌から 3 mm 以内）について、
        /// 載せた頭の人の肌の面までの距離を測る。正は縁が肌から浮く（段差）、負は縁が肌の下に埋もれる（肌が突き抜ける）
        /// </summary>
        public static string SeamGap(Vector3[] bodyWorld, int[] clothesTris, Surface ownSkin, Surface newSkin)
        {
            var edge = Boundary(clothesTris);
            var ds = new List<float>();
            foreach (var i in edge)
            {
                var p = bodyWorld[i];
                Vector3 q, nrm;
                if (ownSkin.Closest(p, 0.01f, out q, out nrm) > 0.003f) continue;
                var s = newSkin.Signed(p, 0.03f, out q, out nrm);
                if (float.IsNaN(s)) continue;
                ds.Add(s);
            }
            if (ds.Count == 0) return "首の継ぎ目: 測れる縁が無い";
            ds.Sort();
            float sum = 0f, abs = 0f;
            int over2 = 0, under1 = 0;
            foreach (var d in ds)
            {
                sum += d;
                abs += Mathf.Abs(d);
                if (d > 0.002f) over2++;
                if (d < -0.001f) under1++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "首の継ぎ目（服の縁 {0} 頂点 → 頭の人の肌）: 平均 {1:+0.0;-0.0} mm、絶対値の平均 {2:0.0} mm、範囲 {3:+0.0;-0.0}〜{4:+0.0;-0.0} mm、2 mm より浮く {5}、1 mm より埋もれる {6}",
                ds.Count, sum / ds.Count * 1000f, abs / ds.Count * 1000f, ds[0] * 1000f, ds[ds.Count - 1] * 1000f, over2, under1);
        }

        /// <summary>
        /// 立ちや歩きの一こまで、髪が服に入り込んでいないかを測る（her は組み立てた人、骨を動かした後）。
        /// 髪の頂点（面の組 2 と、面の組 1 のうち髪の所）のうち服の面の内側にある数と、一番深い所
        /// </summary>
        public static string HairInside(GameObject her, RocketboxPerson who)
        {
            var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            try
            {
                smr.BakeMesh(baked, true);
                var w = World(baked.vertices, smr.transform.localToWorldMatrix);
                var clothes = new Surface(w, smr.sharedMesh.GetTriangles(0));
                var maps = BuildRocketboxProtagonist.Maps.Get(who.HeadFrom, 512);
                int n;
                var headTex = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out n, out n));
                var paint = RocketboxPaint.Head(headTex, maps.Head, maps.Anchors, who.Look(), false, who.IrisUv, who.IrisRadius);
                // 顔と髪が別の人なら、髪は面の組 2（殻）と 3（房）で、どちらも全部が髪
                var hairSubs = who.IsHairSwap ? new[] { 2, 3 } : new[] { 1, 2 };
                var uv = smr.sharedMesh.uv;
                var eyes = new List<Vector3>();
                foreach (var t in her.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Bip01 LEye" || t.name == "Bip01 REye") eyes.Add(t.position);
                int count = 0, inside = 0;
                float worst = 0f;
                var seen = new HashSet<int>();
                foreach (var sub in hairSubs)
                    foreach (var i in smr.sharedMesh.GetTriangles(sub))
                    {
                        if (!seen.Add(i)) continue;
                        if (!who.IsHairSwap && sub == 1 && HairAt(paint, uv[i]) <= 0.5f) continue;
                        var lash = false;
                        foreach (var e in eyes) if (Vector3.Distance(w[i], e) < 0.035f) lash = true;
                        if (lash) continue;
                        count++;
                        Vector3 q, nrm;
                        var s = clothes.Signed(w[i], 0.05f, out q, out nrm);
                        if (float.IsNaN(s) || s >= 0f) continue;
                        inside++;
                        worst = Mathf.Max(worst, -s);
                    }
                return string.Format(CultureInfo.InvariantCulture, "髪の頂点 {0} のうち服の内側 {1}（一番深い所 {2:0.0} mm）", count, inside, worst * 1000f);
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        // ---- 道具 -------------------------------------------------------------

        static float HairAt(RocketboxPaint.HeadResult r, Vector2 uv)
        {
            var n = r.N;
            var x = Mathf.Clamp((int)(uv.x * n), 0, n - 1);
            var y = Mathf.Clamp((int)(uv.y * n), 0, n - 1);
            return r.Hair[y * n + x];
        }

        static SkinnedMeshRenderer Smr(string model)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(model);
            if (go == null) throw new InvalidOperationException("模型が無い: " + model);
            return go.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        static int Slot(SkinnedMeshRenderer smr, string name)
        {
            var ms = smr.sharedMaterials;
            for (var i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name == name) return i;
            throw new InvalidOperationException("面の組が無い: " + name);
        }

        static Vector3[] World(Vector3[] v, Matrix4x4 m)
        {
            var o = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++) o[i] = m.MultiplyPoint3x4(v[i]);
            return o;
        }

        static HashSet<int> Used(int[] tris) { return new HashSet<int>(tris); }

        static int[] Pack(int[] tris, Func<int, Vector3> pos, Func<int, Vector3> nrm, Func<int, Vector2> uv, Func<int, BoneWeight> bw,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights)
        {
            var map = new Dictionary<int, int>();
            var o = new int[tris.Length];
            for (var k = 0; k < tris.Length; k++)
            {
                var i = tris[k];
                int j;
                if (!map.TryGetValue(i, out j))
                {
                    j = verts.Count;
                    map[i] = j;
                    verts.Add(pos(i));
                    norms.Add(nrm(i));
                    uvs.Add(uv(i));
                    weights.Add(bw(i));
                }
                o[k] = j;
            }
            return o;
        }

        /// <summary>面の縁の辺（一つの三角にしか使われない辺）の両端の組</summary>
        static List<int[]> BoundaryEdges(int[] tris)
        {
            var cnt = new Dictionary<long, int>();
            for (var t = 0; t < tris.Length; t += 3)
                for (var e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3];
                    var k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int c;
                    cnt.TryGetValue(k, out c);
                    cnt[k] = c + 1;
                }
            var o = new List<int[]>();
            foreach (var kv in cnt) if (kv.Value == 1) o.Add(new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) });
            return o;
        }

        static float EdgeDistance(Vector3 q, List<Vector3[]> edges)
        {
            var best = float.MaxValue;
            foreach (var e in edges)
            {
                var ab = e[1] - e[0];
                var t = Mathf.Clamp01(Vector3.Dot(q - e[0], ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(q, e[0] + ab * t));
            }
            return best;
        }

        static List<int> Boundary(int[] tris)
        {
            var cnt = new Dictionary<long, int>();
            for (var t = 0; t < tris.Length; t += 3)
                for (var e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3];
                    var k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int c;
                    cnt.TryGetValue(k, out c);
                    cnt[k] = c + 1;
                }
            var s = new HashSet<int>();
            foreach (var kv in cnt)
                if (kv.Value == 1)
                {
                    s.Add((int)(kv.Key >> 32));
                    s.Add((int)(kv.Key & 0xffffffff));
                }
            return new List<int>(s);
        }

        public static void Save(Mesh mesh, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
            }
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                // 置き場の GUID を保つため、今のアセットへ中身を移す（CopySerialized では描かれなくなった）
                old.Clear();
                old.SetVertices(mesh.vertices);
                old.SetNormals(mesh.normals);
                old.SetUVs(0, mesh.uv);
                old.boneWeights = mesh.boneWeights;
                old.bindposes = mesh.bindposes;
                old.subMeshCount = mesh.subMeshCount;
                for (var i = 0; i < mesh.subMeshCount; i++) old.SetTriangles(mesh.GetTriangles(i), i);
                old.RecalculateBounds();
                old.name = mesh.name;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(old);
            }
            else AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }

        /// <summary>三角の面。近い三角を粗い升目で引き、一番近い点と、その三角の表の向きを返す</summary>
        public sealed class Surface
        {
            const float Cell = 0.03f;
            readonly Vector3[] v;
            readonly int[] t;
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

            public Surface(Vector3[] verts, int[] tris)
            {
                v = verts;
                t = tris;
                for (var k = 0; k < tris.Length; k += 3)
                {
                    Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                    var lo = Vector3.Min(a, Vector3.Min(b, c));
                    var hi = Vector3.Max(a, Vector3.Max(b, c));
                    for (var x = Mathf.FloorToInt(lo.x / Cell); x <= Mathf.FloorToInt(hi.x / Cell); x++)
                        for (var y = Mathf.FloorToInt(lo.y / Cell); y <= Mathf.FloorToInt(hi.y / Cell); y++)
                            for (var z = Mathf.FloorToInt(lo.z / Cell); z <= Mathf.FloorToInt(hi.z / Cell); z++)
                            {
                                var key = Key(x, y, z);
                                List<int> l;
                                if (!grid.TryGetValue(key, out l)) grid[key] = l = new List<int>();
                                l.Add(k);
                            }
                }
            }

            static long Key(int x, int y, int z) { return ((long)(x + 100000) << 40) ^ ((long)(y + 100000) << 20) ^ (long)(z + 100000); }

            /// <summary>reach より近くに三角が無ければ正の無限大</summary>
            public float Closest(Vector3 p, float reach, out Vector3 q, out Vector3 nrm)
            {
                int tri;
                return Closest(p, reach, out q, out nrm, out tri);
            }

            /// <summary>tri は一番近い三角の、三角の並びの中の先頭の番号（無ければ -1）</summary>
            public float Closest(Vector3 p, float reach, out Vector3 q, out Vector3 nrm, out int tri)
            {
                return Closest(p, reach, out q, out nrm, out tri, null);
            }

            /// <summary>accept が表の向きを受け入れる三角だけから探す（null なら全部）</summary>
            public float Closest(Vector3 p, float reach, out Vector3 q, out Vector3 nrm, out int tri, Func<Vector3, bool> accept)
            {
                q = p;
                nrm = Vector3.up;
                tri = -1;
                var best = float.PositiveInfinity;
                var seen = new HashSet<int>();
                int x0 = Mathf.FloorToInt((p.x - reach) / Cell), x1 = Mathf.FloorToInt((p.x + reach) / Cell);
                int y0 = Mathf.FloorToInt((p.y - reach) / Cell), y1 = Mathf.FloorToInt((p.y + reach) / Cell);
                int z0 = Mathf.FloorToInt((p.z - reach) / Cell), z1 = Mathf.FloorToInt((p.z + reach) / Cell);
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                        for (var z = z0; z <= z1; z++)
                        {
                            List<int> l;
                            if (!grid.TryGetValue(Key(x, y, z), out l)) continue;
                            foreach (var k in l)
                            {
                                if (!seen.Add(k)) continue;
                                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                                var cp = ClosestOnTriangle(p, a, b, c);
                                var d = (cp - p).magnitude;
                                if (d >= best || d > reach) continue;
                                var fn = Vector3.Cross(b - a, c - a).normalized;
                                if (accept != null && !accept(fn)) continue;
                                best = d;
                                q = cp;
                                nrm = fn;
                                tri = k;
                            }
                        }
                return best;
            }

            /// <summary>表の向きで符号を付けた距離（外が正）。reach より近くに三角が無ければ NaN</summary>
            public float Signed(Vector3 p, float reach, out Vector3 q, out Vector3 nrm)
            {
                var d = Closest(p, reach, out q, out nrm);
                if (float.IsInfinity(d)) return float.NaN;
                return Vector3.Dot(p - q, nrm) >= 0f ? d : -d;
            }
        }

        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            var den = 1f / (va + vb + vc);
            return a + ab * (vb * den) + ac * (vc * den);
        }
    }
}
