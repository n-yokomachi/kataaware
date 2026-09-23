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
    /// - ただし顔の人の髪の殻（頭のテクスチャで髪と見なした頂点が三つのうち二つ以上の三角。目・眉・鼻・口のまわりは除く）の頂点で、
    ///   顔の三角に使われていない物と、耳のまわりから後ろの顔の頂点（耳とこめかみ）が、髪の人の髪の殻の外や 3 mm 内側までにあれば、
    ///   殻の 3 mm 内側へ沈める（顔の人のボブや耳が突き抜けないように）
    /// - 髪の人の頭の面から、髪の三角を全部、髪の殻にする（顔の人の顔に近いこめかみやもみあげの髪も外さない。
    ///   外すと顔の人のこめかみの肌が見えた）。耳のまわりから後ろの肌の三角も殻に入れ、殻の絵ではそこを黒く塗る
    ///   （髪の人の耳の所で殻に穴が開き、顔の人の耳とこめかみが覗いたため）
    /// - 髪の殻と髪の房が顔（耳・頬）の内側に入っていれば、顔の 2 mm 外へ押し出す。服の内側に入っていれば、服の 4 mm 外へ押し出す
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
            var hairPaint = Paint(who.HairFrom, hairMaps);
            var a = faceMaps.Anchors;

            // 顔の人の頭の面を、顔の三角と髪の殻の三角に分ける（面は全部使う）
            var faceHead = fm.GetTriangles(Slot(faceSmr, who.FaceFrom.HeadSlot));
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
                if (!IsHair(tri, hw, huv, hairPaint, hairMaps.Anchors) && !IsSide(tri, hw, hairMaps.Anchors)) continue;
                // 顔に近い髪（こめかみ・もみあげ）も外さない。顔の 2 mm 外へ押し出すので重ならない
                var c = (hw[tri[0]] + hw[tri[1]] + hw[tri[2]]) / 3f;
                Vector3 q, n;
                if (faceSurf.Closest(c, ShellClear, out q, out n) <= ShellClear) shellDropped++;
                shellTris.AddRange(tri);
            }
            var shellSurf = new RocketboxCompose.Surface(hw, shellTris.ToArray());
            var shellEdges = EdgeSegments(shellTris.ToArray(), hw);

            // 顔の人の髪の殻を、髪の人の殻の内側へ沈める（顔の三角の頂点は動かさない）
            var faceVerts = new HashSet<int>(faceTris);
            var sunkPos = (Vector3[])fw.Clone();
            int sunk = 0;
            float maxSink = 0f;
            // 耳のまわりから後ろの顔の頂点（耳とこめかみの肌）も沈める。髪の人の殻は耳まわりも覆う（IsSide）ので、
            // 顔の人の耳が殻を突き抜けないようにする
            var sinkable = new HashSet<int>(faceHairTris);
            foreach (var i in faceVerts) if (IsSidePoint(fw[i], a)) sinkable.Add(i);
            foreach (var i in sinkable)
            {
                if (faceVerts.Contains(i) && !IsSidePoint(fw[i], a)) continue;
                Vector3 q, n;
                var s = shellSurf.Signed(fw[i], 0.06f, out q, out n);
                if (float.IsNaN(s) || s < -Sink || EdgeDistance(q, shellEdges) <= 0.0003f) continue;
                // 顔の縁（生え際）の近くは沈めない量を減らし、縁の三角が裏返って穴に見えないようにする
                var w = faceVerts.Contains(i) ? 1f : RocketboxPaint.Smooth(0.005f, 0.02f, EdgeDistance(fw[i], faceEdges));
                if (w <= 0f) continue;
                sunkPos[i] = fw[i] - n * ((s + Sink) * w);
                sunk++;
                maxSink = Mathf.Max(maxSink, (s + Sink) * w);
            }

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
            foreach (var i in hairVerts)
            {
                var p = moved[i];
                Vector3 q, n;
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
            var bodyTris = Pack(bm.GetTriangles(Slot(bodySmr, who.BodyFrom.BodySlot)), i => toLocal.MultiplyPoint3x4(bw[i]), i => bn[i], i => bu[i], i => Re(bwts[i], bodyRemap), verts, norms, uvs, weights);
            var headTris = Pack(faceHead, i => toLocal.MultiplyPoint3x4(sunkPos[i]), i => fn[i], i => fuv[i], i => Re(fwts[i], faceRemap), verts, norms, uvs, weights);
            var shellOut = Pack(shellTris.ToArray(), i => toLocal.MultiplyPoint3x4(moved[i]), i => hn[i], i => huv[i], i => Re(hwts[i], hairRemap), verts, norms, uvs, weights);
            var cardOut = Pack(hairCards.ToArray(), i => toLocal.MultiplyPoint3x4(moved[i]), i => hn[i], i => huv[i], i => Re(hwts[i], hairRemap), verts, norms, uvs, weights);
            var lashOut = Pack(lashes.ToArray(), i => toLocal.MultiplyPoint3x4(fw[i]), i => fn[i], i => fuv[i], i => Re(fwts[i], faceRemap), verts, norms, uvs, weights);

            var mesh = new Mesh { name = who.Name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bm.bindposes;
            mesh.subMeshCount = 5;
            mesh.SetTriangles(bodyTris, 0);
            mesh.SetTriangles(headTris, 1);
            mesh.SetTriangles(shellOut, 2);
            mesh.SetTriangles(cardOut, 3);
            mesh.SetTriangles(lashOut, 4);
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
                "顔の人の髪の殻を髪の人の殻の内側へ沈めた頂点 {12}（最大 {13:0.0} mm）\n" +
                "顔（耳・頬）の内側から押し出した髪の頂点 {14}（最大 {15:0.0} mm）、服から押し出した髪の頂点 {16}（服の内側にあった {17}、一番深い所 {18:0.0} mm）\n" +
                "生え際（顔の人の生え際の辺 {19}（目より 2 cm 上） → 髪の人の殻）: 平均 {20:0.0} mm、中央 {21:0.0} mm、5 mm 超 {22}、15 mm 超 {23}\n" +
                "顔の人の塗った髪で、髪の人の殻から 5 mm より離れた所 {24:0.0} cm²\n書いた所: {25}",
                who, verts.Count, (bodyTris.Length + headTris.Length + shellOut.Length + cardOut.Length + lashOut.Length) / 3,
                bodyTris.Length / 3, headTris.Length / 3, shellOut.Length / 3, cardOut.Length / 3, lashOut.Length / 3,
                faceHead.Length / 3, faceTris.Count / 3, faceHairTris.Count / 3, shellDropped,
                sunk, maxSink * 1000f,
                outOfFace, maxFace * 1000f, outOfClothes, clothesInside, worstClothes * 1000f,
                gaps.Count, gaps.Count > 0 ? gsum / gaps.Count * 1000f : 0f, gaps.Count > 0 ? gaps[gaps.Count / 2] * 1000f : 0f, g5, g15,
                bare * 10000f, who.CompositeMesh);
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
            var c = (w[tri[0]] + w[tri[1]] + w[tri[2]]) / 3f;
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y)) + 0.012f;
            var front = c.z > a.eyeL.z - 0.035f;
            if (front && c.y < browTop) return false;
            return true;
        }

        /// <summary>
        /// 耳のまわりから後ろ（目の玉の中心より 4.5 cm 奥で、目の高さから 10 cm 下より上）。髪の人の耳とこめかみの肌の所で、
        /// 髪の殻に穴が開いていて、そこから顔の人の耳とこめかみが覗いたので、ここも殻に入れて黒く塗る（<see cref="IsSidePoint"/>）
        /// </summary>
        static bool IsSide(int[] tri, Vector3[] w, RocketboxPaint.Anchors a)
        {
            var c = (w[tri[0]] + w[tri[1]] + w[tri[2]]) / 3f;
            return IsSidePoint(c, a);
        }

        public static bool IsSidePoint(Vector3 p, RocketboxPaint.Anchors a)
        {
            return p.z < a.eyeL.z - 0.045f && p.y > a.eyeL.y - 0.10f;
        }

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

        /// <summary>面の縁の辺を、世界の位置の線分の組で</summary>
        static List<Vector3[]> EdgeSegments(int[] tris, Vector3[] w)
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
