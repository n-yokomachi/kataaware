using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 顔の人の顔に、髪の人の髪を載せる（<see cref="RocketboxPerson.ComposeHair"/>）。
    ///
    /// Rocketbox の頭の面は、顔・耳・首・胸の上と、頭皮を兼ねた髪の殻が一つの面になっている。髪の殻を外すと頭の中は空で、
    /// 顔の面だけが仮面のように残る。二人の頭の面は頂点の数も UV の割り付けも違う（女大 14 は 1783 頂点、女大 08 は 1934 頂点）ので、
    /// 頂点を入れ替えることはできない。また、顔の人の髪の殻を外して髪の人の殻とつなぐと、二人の生え際（分け目の位置、
    /// 前髪の下の額の広さ）が違うため、額とこめかみに穴が開いた（試した結果。報告を参照）。そこで次のように組む。
    /// - 顔の人の頭の面は全部残す（顔も、頭皮を兼ねた髪の殻も。穴は開かない）。顔の人の髪の殻は黒く塗ってあり、下地になる
    /// - 髪の人の頭の面から、殻の絵で不透明な所に掛かる三角を全部、髪の殻にする（顔の人の顔に近いこめかみやもみあげの髪も外さない。
    ///   外すと顔の人のこめかみの肌が見えた）。耳のまわりから後ろと、こめかみの肌の三角も殻に入れ、殻の絵ではそこを黒く塗る
    ///   （髪の人の耳の所とこめかみで殻に穴が開き、顔の人の耳とこめかみの肌が覗いたため。<see cref="SideDepth"/>）
    /// - 殻の縁は三角の辺ではなく、殻の絵の透けで切り抜く（髪の所と、耳まわり・こめかみだけ不透明）。三角で切ると、髪の人の頭の面の
    ///   大きな三角の辺がそのまま髪の輪郭になり、頬の横でぎざぎざになった
    /// - 髪の殻と髪の房が顔（耳・頬）の内側に入っていれば、顔の 2 mm 外へ押し出す。服の内側に入っていれば、服の 4 mm 外へ押し出す。
    ///   押し出した量はとなりの頂点へならす（押し出した頂点とそうでない頂点が交互に並ぶと、殻が波打つため）
    /// - 顔の人の頭の面は、押し出した後の髪の殻の外や 3 mm 内側までにあれば、殻の 3 mm 内側へ沈める。沈めるのは、顔の人の髪の殻
    ///   （頭のテクスチャで髪と見なした頂点が三つのうち二つ以上の三角。目・眉・鼻・口のまわりは除く）の頂点と、顔の頂点のうち
    ///   耳のまわりから後ろの物（耳とこめかみ）と殻の不透明な所の下にある物（顔の人のボブや耳、こめかみの肌が殻を突き抜けないように）
    /// - まつ毛は顔の人の物を使う（まぶたの形に合わせてあるため）。顔の人の前髪の房は外す。髪の房は髪の人の物（まつ毛を除く）。
    ///   まつ毛は目の玉の中心から 2.5 cm 以内で、目の中心より 1.2 cm 上までの房
    /// 面の組は 体・顔（顔の人の頭の面）・髪の殻・髪の房・まつ毛 の五つ
    /// </summary>
    public static class RocketboxHairSwap
    {
        const float ShellClear = 0.003f, Sink = 0.003f, OverFace = 0.002f;

        /// <summary>まつ毛の房: 目の玉の中心から 2.5 cm 以内で、目の中心より 1.2 cm 上までの物（眉の上へ垂れる前髪を入れない）</summary>
        static bool IsLash(Vector3 c, RocketboxPaint.Anchors a)
        {
            foreach (var e in new[] { a.eyeL, a.eyeR })
                if (Vector3.Distance(c, e) < 0.025f && c.y < e.y + 0.012f) return true;
            return false;
        }

        public static string BuildMesh(RocketboxPerson who)
        {
            if (!who.IsHairSwap) throw new ArgumentException("顔と髪が同じ人: " + who);
            var bodySmr = Smr(who.BodyFrom.Model);
            var faceSmr = Smr(who.FaceFrom.Model);
            var hairSmr = Smr(who.HairFrom.Model);
            var bm = bodySmr.sharedMesh;
            var fm = faceSmr.sharedMesh;
            var hm = hairSmr.sharedMesh;

            var toWorld = bodySmr.transform.localToWorldMatrix;
            var toLocal = bodySmr.transform.worldToLocalMatrix;
            var bw = World(bm.vertices, toWorld);
            var fw = World(fm.vertices, faceSmr.transform.localToWorldMatrix);
            var hw = World(hm.vertices, hairSmr.transform.localToWorldMatrix);
            var fuv = fm.uv;
            var huv = hm.uv;

            var faceMaps = BuildRocketboxProtagonist.Maps.Get(who.FaceFrom, 512);
            var hairMaps = BuildRocketboxProtagonist.Maps.Get(who.HairFrom, 512);
            var facePaint = Paint(who.FaceFrom, faceMaps);
            // 殻の絵（透けで殻の縁を切る）。殻に入れる三角もこの透けで選ぶ
            var shellPaint = BuildRocketboxProtagonist.PaintShell(who);
            var a = faceMaps.Anchors;

            // 顔の人の頭の面を、顔の三角と髪の殻の三角に分ける（面は全部使う）
            var faceHead = fm.GetTriangles(Slot(faceSmr, who.FaceFrom.HeadSlot));
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            // 体が別の人なら、首元の肌を体の人の肌と服に合わせる（頭の載せ替えと同じ直し）
            string neckNote = null;
            if (who.BodyFrom != who.FaceFrom)
            {
                var skinVerts = new List<int>();
                foreach (var i in new HashSet<int>(faceHead)) if (HairAt(facePaint, fuv[i]) <= 0.5f) skinVerts.Add(i);
                fw = RocketboxCompose.FitNeck(who.BodyFrom, fw, skinVerts, eyeY, out neckNote);
            }
            var faceTris = new List<int>();
            var faceHairTris = new List<int>();
            for (var t = 0; t < faceHead.Length; t += 3)
            {
                var tri = new[] { faceHead[t], faceHead[t + 1], faceHead[t + 2] };
                if (IsHair(tri, fw, fuv, facePaint, a)) faceHairTris.AddRange(tri);
                else faceTris.AddRange(tri);
            }
            var faceSurf = new RocketboxCompose.Surface(fw, faceTris.ToArray());
            var faceEdges = EdgeSegments(faceTris.ToArray(), fw);

            // 髪の人の髪の殻
            var hairHead = hm.GetTriangles(Slot(hairSmr, who.HairFrom.HeadSlot));
            var shellTris = new List<int>();
            int shellDropped = 0;
            for (var t = 0; t < hairHead.Length; t += 3)
            {
                var tri = new[] { hairHead[t], hairHead[t + 1], hairHead[t + 2] };
                // 殻の絵で不透明の所に少しでも掛かる三角は入れる（頂点の髪の多数決で選ぶと、殻の縁が三角の辺でぎざぎざになった）。
                // 縁は殻の絵の透け（髪の所と耳まわり・こめかみだけ不透明、眉・目・鼻の穴は透明）で切る
                if (!IsSide(tri, hw, hairMaps.Anchors, who.HairFrom) && !Opaque(tri, huv, shellPaint.Px, shellPaint.N)) continue;
                // 顔に近い髪（こめかみ・もみあげ）も外さない。顔の 2 mm 外へ押し出すので重ならない
                var c = (hw[tri[0]] + hw[tri[1]] + hw[tri[2]]) / 3f;
                Vector3 q, n;
                if (faceSurf.Closest(c, ShellClear, out q, out n) <= ShellClear) shellDropped++;
                shellTris.AddRange(tri);
            }
            var shellArr = shellTris.ToArray();

            // 髪の人の房（まつ毛を除く）
            var hairCards = new List<int>();
            var hairOpacity = hm.GetTriangles(Slot(hairSmr, who.HairFrom.HairSlot));
            for (var t = 0; t < hairOpacity.Length; t += 3)
            {
                var c = (hw[hairOpacity[t]] + hw[hairOpacity[t + 1]] + hw[hairOpacity[t + 2]]) / 3f;
                if (IsLash(c, hairMaps.Anchors)) continue;
                hairCards.Add(hairOpacity[t]);
                hairCards.Add(hairOpacity[t + 1]);
                hairCards.Add(hairOpacity[t + 2]);
            }

            // 髪の殻と房が顔（耳・頬）や服の内側に入っていれば外へ
            var moved = (Vector3[])hw.Clone();
            var clothes = new RocketboxCompose.Surface(bw, bm.GetTriangles(Slot(bodySmr, who.BodyFrom.BodySlot)));
            int outOfFace = 0, outOfClothes = 0, clothesInside = 0;
            float maxFace = 0f, maxClothes = 0f, worstClothes = 0f;
            var hairVerts = new HashSet<int>(shellTris);
            hairVerts.UnionWith(hairCards);
            // 前髪: 額の上（眉より上で、目の玉の中心より 3 cm 奥より前）では、顔の人の頭の面の全部（頭皮を兼ねた髪の殻も）から外へ出す。
            // 顔の三角だけから出していたので、髪の人の前髪が顔の人の頭皮の中に埋まり、頭皮の明るい毛筋が分け目から覗いた
            var headSurf = new RocketboxCompose.Surface(fw, faceHead);
            var headEdges = EdgeSegments(faceHead, fw);
            int outOfFront = 0;
            foreach (var i in hairVerts)
            {
                var p = moved[i];
                Vector3 q, n;
                if (p.z > a.eyeL.z - 0.03f && p.y > a.eyeL.y + 0.02f)
                {
                    var dh = headSurf.Closest(p, 0.04f, out q, out n);
                    if (!float.IsInfinity(dh) && EdgeDistance(q, headEdges) > 0.0003f)
                    {
                        var sh = Vector3.Dot(p - q, n) >= 0f ? dh : -dh;
                        if (sh < OverFace)
                        {
                            p = p + n * (OverFace - sh);
                            outOfFront++;
                        }
                    }
                }
                var d = faceSurf.Closest(p, 0.04f, out q, out n);
                if (!float.IsInfinity(d) && EdgeDistance(q, faceEdges) > 0.0003f)
                {
                    var s = Vector3.Dot(p - q, n) >= 0f ? d : -d;
                    if (s < OverFace)
                    {
                        p = p + n * (OverFace - s);
                        outOfFace++;
                        maxFace = Mathf.Max(maxFace, OverFace - s);
                    }
                }
                var sc = clothes.Signed(p, 0.05f, out q, out n);
                if (!float.IsNaN(sc) && sc < 0.004f)
                {
                    if (sc < 0f) { clothesInside++; worstClothes = Mathf.Max(worstClothes, -sc); }
                    p = p + n * (0.004f - sc);
                    outOfClothes++;
                    maxClothes = Mathf.Max(maxClothes, 0.004f - sc);
                }
                moved[i] = p;
            }
            // 押し出した量をとなりの頂点へならす。押し出した頂点と押し出さなかった頂点が交互に並ぶと、
            // 殻が波打つため。押し出した量より引っ込めることはしない
            var smoothed = Smooth(hairVerts, shellTris, hairCards, hw, moved, 6);

            // 顔の人の頭の面を、髪の人の殻（押し出した後）の内側へ沈める。殻の外か 3 mm 内側までにある頂点を、殻の 3 mm 内側へ。
            // - 顔の人の髪の殻の頂点（顔の人のボブが突き抜けないように）。殻の透明な所の下で顔の縁（生え際）に近い物は、
            //   沈める量を減らす（縁の三角が裏返って穴に見えないように）
            // - 顔の頂点は、耳のまわりから後ろ（耳とこめかみの肌。髪の人の殻は耳まわりも覆うので、顔の人の耳が突き抜けないように）と、
            //   殻の不透明な所の下にある物（殻の大きな三角の中ほどを顔の人のこめかみや頬が突き抜けて、肌が細く覗いたため）
            var faceVerts = new HashSet<int>(faceTris);
            var sunkPos = (Vector3[])fw.Clone();
            int sunk = 0, faceUnder = 0;
            float maxSink = 0f;
            var finalSurf = new RocketboxCompose.Surface(smoothed, shellArr);
            var finalEdges = EdgeSegments(shellArr, hw, smoothed);
            // 頭の真ん中（両目の間から 8 cm 奥、目の 1 cm 下）
            var headCentre = new Vector3(0f, (a.eyeL.y + a.eyeR.y) * 0.5f - 0.01f, a.eyeL.z - 0.08f);
            foreach (var i in new HashSet<int>(faceHead))
            {
                var isFace = faceVerts.Contains(i);
                var side = isFace && IsSidePoint(fw[i], a, who.HairFrom);
                Vector3 q, n;
                int k;
                // 殻の三角は頭の外を向いている物だけから探す（頭の真ん中から頂点への向きで見る）。内を向く三角（こめかみで重なる殻の内側の面、
                // 首の横へ垂れる髪の内側の面）の裏へ下げると、頂点が頭の外へ動いて外側の殻を突き抜けた
                var outward = (fw[i] - headCentre).normalized;
                Func<Vector3, bool> facesOut = fn2 => Vector3.Dot(fn2, outward) >= 0.2f;
                var d = finalSurf.Closest(fw[i], isFace && !side ? 0.01f : 0.06f, out q, out n, out k, facesOut);
                if (float.IsInfinity(d)) continue;
                var s = Vector3.Dot(fw[i] - q, n) >= 0f ? d : -d;
                if (s < -Sink || EdgeDistance(q, finalEdges) <= 0.0003f) continue;
                var opaque = AlphaAt(shellPaint, UvAt(q, k, shellArr, smoothed, huv)) >= 0.5f;
                float w;
                if (isFace)
                {
                    if (!side && !opaque) continue;
                    if (!side) faceUnder++;
                    w = 1f;
                }
                else w = opaque ? 1f : RocketboxPaint.Smooth(0.005f, 0.02f, EdgeDistance(fw[i], faceEdges));
                if (w <= 0f) continue;
                var p = fw[i] - n * ((s + Sink) * w);
                // 殻の谷（となりの三角が折れて向き合う所）では、一つの三角の内へ下げても、となりの三角の外に残る。
                // 深く沈めるときは、一番近い三角を引き直して三回まで下げる
                for (var it = 0; it < 3 && w >= 1f; it++)
                {
                    var d2 = finalSurf.Closest(p, 0.06f, out q, out n, out k, facesOut);
                    if (float.IsInfinity(d2)) break;
                    var s2 = Vector3.Dot(p - q, n) >= 0f ? d2 : -d2;
                    if (s2 <= -Sink + 0.0002f || EdgeDistance(q, finalEdges) <= 0.0003f) break;
                    p -= n * (s2 + Sink);
                }
                sunkPos[i] = p;
                sunk++;
                maxSink = Mathf.Max(maxSink, (p - fw[i]).magnitude);
            }

            // まつ毛（顔の人の透けの面のうち、まつ毛の房だけ。顔の人の前髪の房は外す）
            var faceOpacity = fm.GetTriangles(Slot(faceSmr, who.FaceFrom.HairSlot));
            var lashes = new List<int>();
            for (var t = 0; t < faceOpacity.Length; t += 3)
            {
                var c = (fw[faceOpacity[t]] + fw[faceOpacity[t + 1]] + fw[faceOpacity[t + 2]]) / 3f;
                if (!IsLash(c, a)) continue;
                lashes.Add(faceOpacity[t]);
                lashes.Add(faceOpacity[t + 1]);
                lashes.Add(faceOpacity[t + 2]);
            }

            // メッシュを組む。骨の番号は体の人の並びへ
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var bodyRemap = Remap(bodySmr, bodySmr);
            var faceRemap = Remap(faceSmr, bodySmr);
            var hairRemap = Remap(hairSmr, bodySmr);
            var bn = bm.normals; var fn = fm.normals; var hn = hm.normals;
            var bwts = bm.boneWeights; var fwts = fm.boneWeights; var hwts = hm.boneWeights;
            var bu = bm.uv;
            var bodyAll = bm.GetTriangles(Slot(bodySmr, who.BodyFrom.BodySlot));
            int[] legsOut = null;
            string legsNote = null;
            if (who.LegsFrom != null)
            {
                // 膝から下を別の人から借りる。体の人の三角は、重心が継ぐ高さより上の物だけ。借りる人の三角は、重心が継ぐ高さ + 3 cm より下の物。
                // 重なる所（継ぐ高さの 1 cm 下から上）では、借りる人の肌を体の人の肌の 1.5 mm 内側へ入れる（体の人の肌が勝つ）。
                // その下 5 cm で入れる量を 0 へ減らし、脚の形の違いを段にしない
                var cut = who.LegsCut;
                var kept = new List<int>();
                for (var t = 0; t < bodyAll.Length; t += 3)
                    if ((bw[bodyAll[t]].y + bw[bodyAll[t + 1]].y + bw[bodyAll[t + 2]].y) / 3f >= cut) { kept.Add(bodyAll[t]); kept.Add(bodyAll[t + 1]); kept.Add(bodyAll[t + 2]); }
                bodyAll = kept.ToArray();
                var legsSmr = Smr(who.LegsFrom.Model);
                var lm = legsSmr.sharedMesh;
                var lw = World(lm.vertices, legsSmr.transform.localToWorldMatrix);
                var legAll = lm.GetTriangles(Slot(legsSmr, who.LegsFrom.BodySlot));
                var legTris = new List<int>();
                for (var t = 0; t < legAll.Length; t += 3)
                    if ((lw[legAll[t]].y + lw[legAll[t + 1]].y + lw[legAll[t + 2]].y) / 3f < cut + 0.03f) { legTris.Add(legAll[t]); legTris.Add(legAll[t + 1]); legTris.Add(legAll[t + 2]); }
                var upper = new RocketboxCompose.Surface(bw, bodyAll);
                var lmoved = (Vector3[])lw.Clone();
                int tucked = 0;
                float maxTuck = 0f;
                foreach (var i in new HashSet<int>(legTris))
                {
                    var p = lw[i];
                    var w = RocketboxPaint.Smooth(cut - 0.06f, cut - 0.01f, p.y);
                    if (w <= 0f) continue;
                    Vector3 q, n;
                    var s = upper.Signed(p, 0.04f, out q, out n);
                    if (float.IsNaN(s)) continue;
                    var d = (s + 0.0015f) * w;
                    lmoved[i] = p - n * d;
                    tucked++;
                    maxTuck = Mathf.Max(maxTuck, Mathf.Abs(d));
                }
                var ln = lm.normals;
                var luv = lm.uv;
                var lwts = lm.boneWeights;
                var legsRemap = Remap(legsSmr, bodySmr);
                legsOut = Pack(legTris.ToArray(), i => toLocal.MultiplyPoint3x4(lmoved[i]), i => ln[i], i => luv[i], i => Re(lwts[i], legsRemap), verts, norms, uvs, weights);
                legsNote = string.Format(CultureInfo.InvariantCulture, "膝から下: {0} の三角 {1}（継ぐ高さ {2:0.00} m）、体の人の肌の内側へ合わせた頂点 {3}（最大 {4:0.0} mm）",
                    who.LegsFrom, legTris.Count / 3, cut, tucked, maxTuck * 1000f);
            }
            var bodyTris = Pack(bodyAll, i => toLocal.MultiplyPoint3x4(bw[i]), i => bn[i], i => bu[i], i => Re(bwts[i], bodyRemap), verts, norms, uvs, weights);
            var headTris = Pack(faceHead, i => toLocal.MultiplyPoint3x4(sunkPos[i]), i => fn[i], i => fuv[i], i => Re(fwts[i], faceRemap), verts, norms, uvs, weights);
            var patchCount = 0;
            if (who.BodyFrom != who.FaceFrom)
            {
                // 胸元の埋め: 体の人の肌の三角で、顔の人の肌が届かない所。0.7 mm 内側へ下げる。
                // UV は埋めの真ん中に一番近い顔の人の肌の頂点の物を全部に使う（頂点ごとに一番近い物を使うと、UV の島をまたいで暗い点が並んだ）
                var patch = RocketboxCompose.ChestPatch(who.BodyFrom, new RocketboxCompose.Surface(sunkPos, faceHead), eyeY, a.head.z, EdgeSegments(faceHead, fw, sunkPos));
                var patchUvAll = Vector2.zero;
                if (patch.Count > 0)
                {
                    // 首の前の付け根（頭の骨から 6 cm 下の、首の前の面）に一番近い顔の人の肌の頂点。胸元の塗りに左右されない肌の色
                    var mid = new Vector3(0f, a.head.y - 0.06f, a.head.z + 0.08f);
                    var best = float.MaxValue;
                    foreach (var h in new HashSet<int>(faceTris))
                    {
                        var d = (sunkPos[h] - mid).sqrMagnitude;
                        if (d < best) { best = d; patchUvAll = fuv[h]; }
                    }
                }
                Func<int, Vector2> patchUv = i => patchUvAll;
                var bodyToWorld = bodySmr.transform.localToWorldMatrix;
                var patchTris = Pack(patch.ToArray(), i => toLocal.MultiplyPoint3x4(bw[i] - bodyToWorld.MultiplyVector(bn[i]).normalized * 0.0007f), i => bn[i], patchUv, i => Re(bwts[i], bodyRemap), verts, norms, uvs, weights);
                var all = new int[headTris.Length + patchTris.Length];
                headTris.CopyTo(all, 0);
                patchTris.CopyTo(all, headTris.Length);
                headTris = all;
                patchCount = patch.Count / 3;
            }
            moved = smoothed;
            var shellOut = Pack(shellTris.ToArray(), i => toLocal.MultiplyPoint3x4(moved[i]), i => hn[i], i => huv[i], i => Re(hwts[i], hairRemap), verts, norms, uvs, weights);
            var cardOut = Pack(hairCards.ToArray(), i => toLocal.MultiplyPoint3x4(moved[i]), i => hn[i], i => huv[i], i => Re(hwts[i], hairRemap), verts, norms, uvs, weights);
            var lashOut = Pack(lashes.ToArray(), i => toLocal.MultiplyPoint3x4(fw[i]), i => fn[i], i => fuv[i], i => Re(fwts[i], faceRemap), verts, norms, uvs, weights);

            var mesh = new Mesh { name = who.Name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bm.bindposes;
            mesh.subMeshCount = legsOut != null ? 6 : 5;
            mesh.SetTriangles(bodyTris, 0);
            mesh.SetTriangles(headTris, 1);
            mesh.SetTriangles(shellOut, 2);
            mesh.SetTriangles(cardOut, 3);
            mesh.SetTriangles(lashOut, 4);
            if (legsOut != null) mesh.SetTriangles(legsOut, 5);
            mesh.RecalculateBounds();
            RocketboxCompose.Save(mesh, who.CompositeMesh);

            // 生え際の測り: 顔の三角の縁（顔の人の生え際）から髪の人の殻まで。0 に近いほど、顔の人の塗った髪が殻の外に見えない
            var finalShell = new RocketboxCompose.Surface(moved, shellTris.ToArray());
            var gaps = new List<float>();
            foreach (var e in faceEdges)
            {
                var p = (e[0] + e[1]) * 0.5f;
                if (p.y < a.eyeL.y - 0.02f) continue;
                Vector3 q, n;
                var d = finalShell.Closest(p, 0.05f, out q, out n);
                gaps.Add(float.IsInfinity(d) ? 0.05f : d);
            }
            gaps.Sort();
            float gsum = 0f; int g5 = 0, g15 = 0;
            foreach (var g in gaps) { gsum += g; if (g > 0.005f) g5++; if (g > 0.015f) g15++; }
            // 顔の人の塗った髪で、髪の人の殻に覆われていない所の広さ（殻から 5 mm より離れた三角の面積）
            var bare = 0f;
            for (var t = 0; t < faceHairTris.Count; t += 3)
            {
                Vector3 v0 = sunkPos[faceHairTris[t]], v1 = sunkPos[faceHairTris[t + 1]], v2 = sunkPos[faceHairTris[t + 2]];
                Vector3 q, n;
                if (finalShell.Closest((v0 + v1 + v2) / 3f, 0.005f, out q, out n) <= 0.005f) continue;
                bare += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}: 頂点 {1}・三角 {2}（体 {3}・顔の人の頭 {4}・髪の殻 {5}・髪の房 {6}・まつ毛 {7}）\n" +
                "顔の人の頭の面 {8} 三角のうち顔 {9}、髪の殻 {10}。髪の人の髪の三角のうち顔から 3 mm 以内の物 {11}（外へ押し出す）\n" +
                "顔の人の髪の殻を髪の人の殻の内側へ沈めた頂点 {12}（最大 {13:0.0} mm。殻の不透明な所の下にあった顔の頂点 {26} を含む）\n" +
                "額の上で顔の人の頭の面の内側から押し出した前髪の頂点 {27}\n" +
                "顔（耳・頬）の内側から押し出した髪の頂点 {14}（最大 {15:0.0} mm）、服から押し出した髪の頂点 {16}（服の内側にあった {17}、一番深い所 {18:0.0} mm）\n" +
                "生え際（顔の人の生え際の辺 {19}（目より 2 cm 上） → 髪の人の殻）: 平均 {20:0.0} mm、中央 {21:0.0} mm、5 mm 超 {22}、15 mm 超 {23}\n" +
                "顔の人の塗った髪で、髪の人の殻から 5 mm より離れた所 {24:0.0} cm²\n書いた所: {25}",
                who, verts.Count, (bodyTris.Length + headTris.Length + shellOut.Length + cardOut.Length + lashOut.Length) / 3,
                bodyTris.Length / 3, headTris.Length / 3, shellOut.Length / 3, cardOut.Length / 3, lashOut.Length / 3,
                faceHead.Length / 3, faceTris.Count / 3, faceHairTris.Count / 3, shellDropped,
                sunk, maxSink * 1000f,
                outOfFace, maxFace * 1000f, outOfClothes, clothesInside, worstClothes * 1000f,
                gaps.Count, gaps.Count > 0 ? gsum / gaps.Count * 1000f : 0f, gaps.Count > 0 ? gaps[gaps.Count / 2] * 1000f : 0f, g5, g15,
                bare * 10000f, who.CompositeMesh, faceUnder, outOfFront)
                + (neckNote != null ? "\n" + neckNote + "、胸元を体の人の肌の三角で埋めた数 " + patchCount : "")
                + (legsNote != null ? "\n" + legsNote : "");
        }

        // ---- 見分け -----------------------------------------------------------

        /// <summary>
        /// 髪の三角か。三つの頂点のうち二つ以上が、頭のテクスチャで髪と見なした所にある。
        /// ただし顔の前（目の高さより 3.5 cm 奥より前）で眉の上端より下は、暗くても顔（眉・目・鼻の穴）とする
        /// </summary>
        static bool IsHair(int[] tri, Vector3[] w, Vector2[] uv, RocketboxPaint.HeadResult paint, RocketboxPaint.Anchors a)
        {
            var votes = 0;
            foreach (var i in tri) if (HairAt(paint, uv[i]) > 0.5f) votes++;
            if (votes < 2) return false;
            return !IsFrontFace(tri, w, a);
        }

        /// <summary>顔の前（目の高さより 3.5 cm 奥より前）で、眉の上端より 1.2 cm 上より下の三角（眉・目・鼻の穴）</summary>
        static bool IsFrontFace(int[] tri, Vector3[] w, RocketboxPaint.Anchors a)
        {
            var c = (w[tri[0]] + w[tri[1]] + w[tri[2]]) / 3f;
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y)) + 0.012f;
            return c.z > a.eyeL.z - 0.035f && c.y < browTop;
        }

        /// <summary>
        /// 殻の絵で透明にする顔の真ん中（眉・目・鼻の穴・口。暗くて髪と見なされる所）の重み。顔の前（目の高さより 3.5 cm 奥より前）で、
        /// 眉の上端より 1.2 cm 上より下、眉尻より 5 mm 内。縁は 2 mm でぼかす。
        /// 顔の前を横幅いっぱい外すと、頬の横へ垂れる髪の人の髪が縦の線で切れた
        /// </summary>
        public static float FaceFeature(Vector3 p, RocketboxPaint.Anchors a)
        {
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y)) + 0.012f;
            var wide = Mathf.Max(Mathf.Abs(a.browOutL.x), Mathf.Abs(a.browOutR.x)) + 0.005f;
            const float f = 0.002f;
            return RocketboxPaint.Smooth(a.eyeL.z - 0.035f - f, a.eyeL.z - 0.035f + f, p.z)
                * RocketboxPaint.Smooth(browTop + f, browTop - f, p.y)
                * RocketboxPaint.Smooth(wide + f, wide - f, Mathf.Abs(p.x));
        }

        /// <summary>
        /// 三角が絵（n×n、下の行から）の不透明（透け 0.5 以上）の所に掛かるか。UV で三角から 1 画素以内の画素を見る
        /// </summary>
        static bool Opaque(int[] tri, Vector2[] uv, Color[] px, int n)
        {
            Vector2 p0 = uv[tri[0]] * n, p1 = uv[tri[1]] * n, p2 = uv[tri[2]] * n;
            var x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))) - 1, 0, n - 1);
            var x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))) + 1, 0, n - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))) - 1, 0, n - 1);
            var y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))) + 1, 0, n - 1);
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if (px[y * n + x].a < 0.5f) continue;
                    if (UvDistance(new Vector2(x + 0.5f, y + 0.5f), p0, p1, p2) <= 1f) return true;
                }
            return false;
        }

        /// <summary>三角（tris の k 番目から三つ）の上の点 q の UV</summary>
        static Vector2 UvAt(Vector3 q, int k, int[] tris, Vector3[] w, Vector2[] uv)
        {
            Vector3 a = w[tris[k]], b = w[tris[k + 1]], c = w[tris[k + 2]];
            Vector3 v0 = b - a, v1 = c - a, v2 = q - a;
            float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
            var den = d00 * d11 - d01 * d01;
            if (Mathf.Abs(den) < 1e-14f) return uv[tris[k]];
            var y = (d11 * d20 - d01 * d21) / den;
            var z = (d00 * d21 - d01 * d20) / den;
            return uv[tris[k]] * (1f - y - z) + uv[tris[k + 1]] * y + uv[tris[k + 2]] * z;
        }

        /// <summary>絵の透け（UV で線形に拾う）</summary>
        static float AlphaAt(RocketboxPaint.HeadResult r, Vector2 uv)
        {
            var n = r.N;
            var fx = Mathf.Clamp(uv.x * n - 0.5f, 0f, n - 1.001f);
            var fy = Mathf.Clamp(uv.y * n - 0.5f, 0f, n - 1.001f);
            int x = (int)fx, y = (int)fy;
            float tx = fx - x, ty = fy - y;
            var a0 = Mathf.Lerp(r.Px[y * n + x].a, r.Px[y * n + x + 1].a, tx);
            var a1 = Mathf.Lerp(r.Px[(y + 1) * n + x].a, r.Px[(y + 1) * n + x + 1].a, tx);
            return Mathf.Lerp(a0, a1, ty);
        }

        /// <summary>UV の平面で、点から三角までの距離（中なら 0）</summary>
        static float UvDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }
            var s = Mathf.Sign(Cross(b - a, c - a));
            if (s != 0f && Cross(b - a, p - a) * s >= 0f && Cross(c - b, p - b) * s >= 0f && Cross(a - c, p - c) * s >= 0f) return 0f;
            return Mathf.Min(SegDistance(p, a, b), Mathf.Min(SegDistance(p, b, c), SegDistance(p, c, a)));
        }

        static float SegDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// 耳のまわりとこめかみの三角か（<see cref="SideDepth"/>）。頂点が一つでも内にあれば殻に入れる。
        /// 三角のまま切ると縁が三角の辺でぎざぎざになるので、殻の絵の透けで縁を切る（殻の絵は <see cref="BuildRocketboxProtagonist.PaintShell"/>）
        /// </summary>
        static bool IsSide(int[] tri, Vector3[] w, RocketboxPaint.Anchors a, RocketboxPerson hair)
        {
            foreach (var i in tri) if (SideDepth(w[i], a, hair) > -SideFeather) return true;
            return false;
        }

        /// <summary>殻の絵で耳のまわりとこめかみを塗る所の縁のぼかし（m）。三角もこの幅だけ外まで殻に入れる</summary>
        public const float SideFeather = 0.002f;

        public static bool IsSidePoint(Vector3 p, RocketboxPaint.Anchors a, RocketboxPerson hair)
        {
            return SideDepth(p, a, hair) > 0f;
        }

        /// <summary>
        /// 耳のまわりとこめかみの内側への深さ（m、外は負）。どちらも髪の人の頭の面では肌で、髪の殻に穴が開いていて、
        /// そこから顔の人の耳とこめかみが覗いたので、殻に入れて黒く塗る。
        /// - 耳のまわりから後ろ: 目の玉の中心より 4.5 cm 奥で、目の高さから 10 cm 下より上
        /// - こめかみ: 頬骨の上（目の高さから 3.5 cm 下）から額の横（8 cm 上）まで。窓の広さは髪の人と左右で違うので、
        ///   目の玉の中心から外と奥へどこまでを入れるかは髪の人の定義（<see cref="RocketboxPerson.TempleLeft"/>）に持たせる
        /// </summary>
        public static float SideDepth(Vector3 p, RocketboxPaint.Anchors a, RocketboxPerson hair)
        {
            var ear = Mathf.Min(a.eyeL.z - 0.045f - p.z, p.y - (a.eyeL.y - 0.10f));
            var t = p.x < 0f ? hair.TempleLeft : hair.TempleRight;
            // 目の 1 cm 上より下は、前の縁を下るほど外と奥へ下げ、もみあげのように細らせる（四角いままだと頬骨の上で髪が直角の角になった）
            var taper = TempleTaper * Mathf.Max(0f, a.eyeL.y + 0.01f - p.y);
            var temple = Mathf.Min(
                Mathf.Min(Mathf.Abs(p.x) - (Mathf.Abs(a.eyeL.x) + t.x + taper), a.eyeL.z - t.y - taper - p.z),
                Mathf.Min(p.y - (a.eyeL.y - 0.035f), a.eyeL.y + 0.08f - p.y));
            return Mathf.Max(ear, temple);
        }

        /// <summary>こめかみの前の縁を、目の 1 cm 上から下へ 1 cm 下るごとに外と奥へ下げる量（cm）</summary>
        const float TempleTaper = 0.6f;

        static RocketboxPaint.HeadResult Paint(RocketboxPerson who, BuildRocketboxProtagonist.Maps maps)
        {
            int n;
            var tex = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out n, out n));
            return RocketboxPaint.Head(tex, maps.Head, maps.Anchors, who.Look(), false, who.IrisUv, who.IrisRadius);
        }

        static float HairAt(RocketboxPaint.HeadResult r, Vector2 uv)
        {
            var n = r.N;
            var x = Mathf.Clamp((int)(uv.x * n), 0, n - 1);
            var y = Mathf.Clamp((int)(uv.y * n), 0, n - 1);
            return r.Hair[y * n + x];
        }

        /// <summary>
        /// 押し出しの量（moved - rest）を、面のとなりの頂点の平均へ半分ずつ寄せることを iterations 回。
        /// 各頂点の押し出した向きには、押し出した量より引っ込めない。
        /// 同じ位置の頂点（UV の継ぎ目で分かれた物）は一つとして動かす（別々に動かすと継ぎ目が開く）
        /// </summary>
        static Vector3[] Smooth(HashSet<int> verts, List<int> shell, List<int> cards, Vector3[] rest, Vector3[] moved, int iterations)
        {
            var rep = Weld(verts, rest);
            var nb = new Dictionary<int, HashSet<int>>();
            foreach (var tris in new[] { shell, cards })
                for (var t = 0; t < tris.Count; t += 3)
                    for (var e = 0; e < 3; e++)
                    {
                        int a = rep[tris[t + e]], b = rep[tris[t + (e + 1) % 3]];
                        if (a == b) continue;
                        HashSet<int> l;
                        if (!nb.TryGetValue(a, out l)) nb[a] = l = new HashSet<int>();
                        l.Add(b);
                        if (!nb.TryGetValue(b, out l)) nb[b] = l = new HashSet<int>();
                        l.Add(a);
                    }
            var need = new Dictionary<int, Vector3>();
            foreach (var i in verts)
            {
                // 継ぎ目で分かれた頂点のうち、一番多く押し出した物に揃える
                var d = moved[i] - rest[i];
                Vector3 old;
                if (!need.TryGetValue(rep[i], out old) || d.sqrMagnitude > old.sqrMagnitude) need[rep[i]] = d;
            }
            var off = new Dictionary<int, Vector3>(need);
            for (var it = 0; it < iterations; it++)
            {
                var next = new Dictionary<int, Vector3>();
                foreach (var kv in need)
                {
                    var i = kv.Key;
                    HashSet<int> l;
                    var o = off[i];
                    if (nb.TryGetValue(i, out l) && l.Count > 0)
                    {
                        var sum = Vector3.zero;
                        var cnt = 0;
                        foreach (var j in l)
                        {
                            Vector3 oj;
                            if (!off.TryGetValue(j, out oj)) continue;
                            sum += oj;
                            cnt++;
                        }
                        if (cnt > 0) o = Vector3.Lerp(o, sum / cnt, 0.5f);
                    }
                    var nd = kv.Value;
                    var len = nd.magnitude;
                    if (len > 1e-6f)
                    {
                        var dir = nd / len;
                        var along = Vector3.Dot(o, dir);
                        if (along < len) o += dir * (len - along);
                    }
                    next[i] = o;
                }
                off = next;
            }
            var result = (Vector3[])moved.Clone();
            foreach (var i in verts) result[i] = rest[i] + off[rep[i]];
            return result;
        }

        /// <summary>同じ位置（0.01 mm まで）の頂点を一つの番号へ</summary>
        static Dictionary<int, int> Weld(IEnumerable<int> verts, Vector3[] pos)
        {
            var weld = new Dictionary<Vector3Int, int>();
            var id = new Dictionary<int, int>();
            foreach (var i in verts)
            {
                if (id.ContainsKey(i)) continue;
                var q = Vector3Int.RoundToInt(pos[i] * 1e5f);
                int j;
                if (!weld.TryGetValue(q, out j)) weld[q] = j = i;
                id[i] = j;
            }
            return id;
        }

        // ---- 道具 -------------------------------------------------------------

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

        static int[] Remap(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            var index = new Dictionary<string, int>();
            for (var i = 0; i < to.bones.Length; i++) index[to.bones[i].name] = i;
            var o = new int[from.bones.Length];
            for (var i = 0; i < o.Length; i++)
            {
                int j;
                if (!index.TryGetValue(from.bones[i].name, out j)) throw new InvalidOperationException("体の人に無い骨: " + from.bones[i].name);
                o[i] = j;
            }
            return o;
        }

        static BoneWeight Re(BoneWeight w, int[] remap)
        {
            w.boneIndex0 = remap[w.boneIndex0];
            w.boneIndex1 = remap[w.boneIndex1];
            w.boneIndex2 = remap[w.boneIndex2];
            w.boneIndex3 = remap[w.boneIndex3];
            return w;
        }

        static Vector3[] World(Vector3[] v, Matrix4x4 m)
        {
            var o = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++) o[i] = m.MultiplyPoint3x4(v[i]);
            return o;
        }

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

        /// <summary>
        /// 面の縁の辺を、世界の位置の線分の組で。同じ位置の頂点は一つと見る（UV の継ぎ目で頂点が分かれていても縁にしない。
        /// 縁と見ると、髪の人の殻の後ろの継ぎ目の近くで顔の人の髪を沈めず、殻の外に筋になって出た）
        /// </summary>
        static List<Vector3[]> EdgeSegments(int[] tris, Vector3[] w)
        {
            return EdgeSegments(tris, w, w);
        }

        /// <summary>頂点を weldBy の位置で一つにまとめて縁を探し、線分は w の位置で返す</summary>
        static List<Vector3[]> EdgeSegments(int[] tris, Vector3[] weldBy, Vector3[] w)
        {
            var id = Weld(tris, weldBy);
            var cnt = new Dictionary<long, int>();
            for (var t = 0; t < tris.Length; t += 3)
                for (var e = 0; e < 3; e++)
                {
                    int a = id[tris[t + e]], b = id[tris[t + (e + 1) % 3]];
                    if (a == b) continue;
                    var k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int c;
                    cnt.TryGetValue(k, out c);
                    cnt[k] = c + 1;
                }
            var o = new List<Vector3[]>();
            foreach (var kv in cnt) if (kv.Value == 1) o.Add(new[] { w[(int)(kv.Key >> 32)], w[(int)(kv.Key & 0xffffffff)] });
            return o;
        }

        static float EdgeDistance(Vector3 q, List<Vector3[]> edges)
        {
            return Vector3.Distance(q, ClosestOnSegments(q, edges));
        }

        static Vector3 ClosestOnSegments(Vector3 q, List<Vector3[]> edges)
        {
            var best = float.MaxValue;
            var o = q;
            foreach (var e in edges)
            {
                var ab = e[1] - e[0];
                var t = Mathf.Clamp01(Vector3.Dot(q - e[0], ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                var p = e[0] + ab * t;
                var d = (p - q).sqrMagnitude;
                if (d < best) { best = d; o = p; }
            }
            return o;
        }
    }
}
