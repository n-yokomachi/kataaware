using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 片割れのワンピースを一から作る（既存の服の形は使わない）。生成り〜クリームの、綿か麻の一枚もの。
    /// - 胴: 体の人（女大 18）の肌と服の面を型にして、数 mm 外へ出した面。襟ぐり（首の付け根より少し下の丸首）・腰の切り替え・袖の付け根で、
    ///   三角を値の面で切る（三角の辺でぎざぎざにならない）。骨の付き方は型の頂点のまま
    /// - 袖: 上腕のまわりの筒を、肩から袖口へ緩く広げる（体の側は狭く、外の側を大きく）。長さは上腕の <see cref="SleeveT1"/>。上腕の骨に付く
    /// - スカート: 腰の切り替えから、ふくらはぎの半ばの裾へたっぷり広がるフレアの筒。縦にゆるい波のひだを寄せる。
    ///   腰の骨を中心に付け、脚の骨は弱く混ぜる（向きで左右の腿、膝より下は脛を少し）。歩いても脚について二本に割れない
    /// 形はどれも束ねた姿勢（体の人の骨のまま）で作る
    /// </summary>
    public static class RocketboxDress
    {
        // ---- 形の値（m、割合） ----
        /// <summary>襟ぐり: 首の骨（首の付け根）から下へ。前・横・後ろ</summary>
        public const float NeckDropFront = 0.04f, NeckDropSide = 0.015f, NeckDropBack = 0.025f;
        /// <summary>胴の面を型から外へ出す量</summary>
        public const float BodiceOffset = 0.005f;
        /// <summary>胴の面をならすとき、型の面から離しておく最小の量（型の段をならすため、出す量より小さい）</summary>
        public const float BodiceMin = 0.0025f;
        /// <summary>腰の切り替えの高さ（腰の骨から上へ）</summary>
        public const float WaistAbovePelvis = 0.09f;
        /// <summary>胴の面が上腕を覆う長さ（上腕の長さの割合。袖の付け根の下に隠れる）</summary>
        public const float CapT = 0.16f;
        /// <summary>袖の付け根と袖口（上腕の長さの割合）</summary>
        public const float SleeveT0 = 0.05f, SleeveT1 = 0.72f;
        /// <summary>袖と腕の間の隙間と、袖口での広がり（外の側）</summary>
        public const float SleeveBase = 0.010f, SleeveFlare = 0.045f;
        /// <summary>裾の高さと、裾の半径（腰の真ん中から）、ひだの波の深さと数</summary>
        public const float HemY = 0.33f, HemR = 0.38f, WaveAmp = 0.028f;
        public const int Waves = 9;
        /// <summary>スカートの裾で脚の骨に付ける割合（残りは腰の骨）</summary>
        public const float LegShare = 0.65f;
        const int SkirtRings = 16, SkirtSegs = 72, SleeveRings = 8, SleeveSegs = 24;

        /// <summary>骨の位置（束ねた姿勢）と、形の基準の高さ</summary>
        public sealed class Frame
        {
            public Vector3 neck, pelvis;
            public float neckY, neckZ, waistY, kneeY, zc;
            public Vector3[] shoulder = new Vector3[2], elbow = new Vector3[2];
            public Dictionary<string, int> bone = new Dictionary<string, int>();

            public static Frame Of(SkinnedMeshRenderer smr)
            {
                var f = new Frame();
                for (var i = 0; i < smr.bones.Length; i++) f.bone[smr.bones[i].name] = i;
                Func<string, Vector3> at = n => smr.bones[f.bone[n]].position;
                f.neck = at("Bip01 Neck");
                f.pelvis = at("Bip01 Pelvis");
                f.neckY = f.neck.y;
                f.neckZ = f.neck.z;
                f.waistY = f.pelvis.y + WaistAbovePelvis;
                f.kneeY = (at("Bip01 L Calf").y + at("Bip01 R Calf").y) * 0.5f;
                f.zc = (at("Bip01 L Thigh").z + at("Bip01 R Thigh").z) * 0.5f;
                f.shoulder[0] = at("Bip01 L UpperArm");
                f.elbow[0] = at("Bip01 L Forearm");
                f.shoulder[1] = at("Bip01 R UpperArm");
                f.elbow[1] = at("Bip01 R Forearm");
                return f;
            }

            /// <summary>上腕の軸の上の位置（上腕の長さの割合）と、軸からの距離。腕の側にない点は false</summary>
            public bool Arm(Vector3 p, int side, out float t, out float perp)
            {
                var s = shoulder[side];
                var a = elbow[side] - s;
                var len = a.magnitude;
                a /= len;
                var v = p - s;
                var along = Vector3.Dot(v, a);
                t = along / len;
                perp = (v - a * along).magnitude;
                var outward = (p.x - s.x) * Mathf.Sign(s.x);
                return t > -0.05f && perp < 0.085f && outward > -0.02f;
            }

            public float UpperArmLength(int side) { return (elbow[side] - shoulder[side]).magnitude; }
        }

        /// <summary>胴の面を残す所の値（正で残す）: 襟ぐりより下、腰の切り替えより上、上腕の付け根より体の側</summary>
        /// <summary>
        /// 襟ぐりの内側（服が覆う側）が正の値（m）。首の付け根からの下がり（前・横・後ろ）より下か、首の軸から横へ（首の太さ + 向きごとのゆとり）より外なら覆う。
        /// 高さだけで決めると、横で肩の上の首の付け根（僧帽筋）が襟ぐりより上に出て、肌が服の外に残った
        /// </summary>
        public static float Neck(Frame f, Vector3 p)
        {
            var dz = p.z - f.neckZ;
            var th = Mathf.Atan2(p.x, dz);
            var c = Mathf.Cos(th);
            var drop = c >= 0f ? Mathf.Lerp(NeckDropSide, NeckDropFront, c * c) : Mathf.Lerp(NeckDropSide, NeckDropBack, c * c);
            var margin = c >= 0f ? Mathf.Lerp(NeckMarginSide, NeckMarginFront, c * c) : Mathf.Lerp(NeckMarginSide, NeckMarginBack, c * c);
            var radial = new Vector2(p.x, dz).magnitude - (NeckRadius + margin);
            return Mathf.Max((f.neckY - drop) - p.y, p.y < f.neckY + 0.03f ? radial : -1f);
        }

        /// <summary>首の太さ（首の骨の軸から肌まで）と、襟ぐりの横へのゆとり（前・横・後ろ）</summary>
        public const float NeckRadius = 0.058f, NeckMarginFront = 0.12f, NeckMarginSide = 0.02f, NeckMarginBack = 0.03f;

        static float Keep(Frame f, Vector3 p, int armSide)
        {
            var g = Neck(f, p);
            g = Mathf.Min(g, p.y - (f.waistY - 0.012f));
            for (var side = 0; side < 2; side++)
            {
                float t, perp;
                // 腕の骨に付いた頂点は、軸からの距離によらず上腕の付け根から先を除く（前腕と手が胴に残らないように）
                if (f.Arm(p, side, out t, out perp) || armSide == side) g = Mathf.Min(g, (CapT - t) * f.UpperArmLength(side));
            }
            return g;
        }

        /// <summary>頭の人の面で、服の下に隠れる所（襟ぐりより 5 mm 内）</summary>
        public static bool UnderDress(Frame f, Vector3 p)
        {
            return Neck(f, p) > 0.005f;
        }

        /// <summary>体の人の面のうち、服の外に出る所（袖口より先の腕と手）だけを残す。袖の中へ 3 cm 重ねる</summary>
        public static int[] KeepArms(Frame f, int[] tris, Vector3[] w)
        {
            var kept = new List<int>();
            for (var t = 0; t < tris.Length; t += 3)
            {
                var c = (w[tris[t]] + w[tris[t + 1]] + w[tris[t + 2]]) / 3f;
                var keep = false;
                for (var side = 0; side < 2; side++)
                {
                    float at, perp;
                    var s = f.shoulder[side];
                    var a = (f.elbow[side] - s).normalized;
                    at = Vector3.Dot(c - s, a) / f.UpperArmLength(side);
                    perp = ((c - s) - a * Vector3.Dot(c - s, a)).magnitude;
                    var outward = (c.x - s.x) * Mathf.Sign(s.x);
                    if (outward > 0.02f && at > SleeveT1 - 0.03f / f.UpperArmLength(side) && (perp < 0.10f || at > 1f)) keep = true;
                }
                if (keep) { kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]); }
            }
            return kept.ToArray();
        }

        /// <summary>
        /// ワンピースのメッシュを作り、verts などへ足して三角の並びを返す（体の人のメッシュの中の位置で）。
        /// templ は型にする体の人の三角（体の面と頭の面）、bw・bn・bwts は体の人の頂点の束ねた姿勢の世界の位置・法線と骨の重み
        /// </summary>
        public static int[] Build(SkinnedMeshRenderer bodySmr, int[] templ, Vector3[] bw, Vector3[] bn, BoneWeight[] bwts,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, out string note)
        {
            var f = Frame.Of(bodySmr);
            var toLocal = bodySmr.transform.worldToLocalMatrix;
            var outTris = new List<int>();

            // ---- 胴: 型の三角を値の面で切り、外へ出す ----
            // 腕の骨（上腕・前腕・手・指）に付いた頂点の左右
            var armBones = new Dictionary<int, int>();
            foreach (var kv in f.bone)
            {
                var n = kv.Key;
                if (!(n.Contains("UpperArm") || n.Contains("Forearm") || n.Contains("Hand") || n.Contains("Finger"))) continue;
                armBones[kv.Value] = n.Contains(" L ") ? 0 : 1;
            }
            int headBone;
            f.bone.TryGetValue("Bip01 Head", out headBone);
            var g = new Dictionary<int, float>();
            foreach (var i in new HashSet<int>(templ))
            {
                int side;
                g[i] = Keep(f, bw[i], armBones.TryGetValue(bwts[i].boneIndex0, out side) ? side : -1);
                // 頭の骨に付いた面（体の人の髪の殻。女大 18 は肩までの髪で、首の後ろへ垂れている）は型にしない
                if (bwts[i].boneIndex0 == headBone && bwts[i].weight0 > 0.5f) g[i] = Mathf.Min(g[i], -0.001f);
            }
            // 同じ位置の頂点（UV の継ぎ目で分かれた物）は同じ法線で出す
            var nsum = new Dictionary<long, Vector3>();
            foreach (var i in g.Keys)
            {
                var k = Key(bw[i]);
                Vector3 s;
                nsum.TryGetValue(k, out s);
                nsum[k] = s + bn[i];
            }
            Func<Vector3, Vector3> welded = p => { Vector3 s; return nsum.TryGetValue(Key(p), out s) ? s.normalized : Vector3.up; };
            var bodiceStart = verts.Count;
            var map = new Dictionary<int, int>();
            var edgeMap = new Dictionary<long, int>();
            var bodPos = new List<Vector3>();
            var bodW = new List<BoneWeight>();
            Func<int, int> orig = i =>
            {
                int o;
                if (map.TryGetValue(i, out o)) return o;
                var n = welded(bw[i]);
                o = Add(verts, norms, uvs, weights, toLocal, bw[i] + n * BodiceOffset, n, BodiceUv(f, bw[i]), bwts[i]);
                bodPos.Add(bw[i] + n * BodiceOffset);
                bodW.Add(bwts[i]);
                map[i] = o;
                return o;
            };
            Func<int, int, int> cut = (i, j) =>
            {
                var key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
                int o;
                if (edgeMap.TryGetValue(key, out o)) return o;
                var t = g[i] / (g[i] - g[j]);
                var p = Vector3.Lerp(bw[i], bw[j], t);
                var n = Vector3.Lerp(welded(bw[i]), welded(bw[j]), t).normalized;
                var w = Mix(bwts[i], bwts[j], t);
                o = Add(verts, norms, uvs, weights, toLocal, p + n * BodiceOffset, n, BodiceUv(f, p), w);
                bodPos.Add(p + n * BodiceOffset);
                bodW.Add(w);
                edgeMap[key] = o;
                return o;
            };
            int kept = 0, clipped = 0;
            for (var t = 0; t < templ.Length; t += 3)
            {
                var ids = new[] { templ[t], templ[t + 1], templ[t + 2] };
                var inside = 0;
                foreach (var i in ids) if (g[i] >= 0f) inside++;
                if (inside == 0) continue;
                if (inside == 3)
                {
                    outTris.Add(orig(ids[0])); outTris.Add(orig(ids[1])); outTris.Add(orig(ids[2]));
                    kept++;
                    continue;
                }
                // 残す側の多角形（三つか四つの頂点）を扇に割る
                var poly = new List<int>();
                for (var e = 0; e < 3; e++)
                {
                    int a = ids[e], b = ids[(e + 1) % 3];
                    if (g[a] >= 0f) poly.Add(orig(a));
                    if ((g[a] >= 0f) != (g[b] >= 0f)) poly.Add(cut(a, b));
                }
                for (var k = 1; k + 1 < poly.Count; k++) { outTris.Add(poly[0]); outTris.Add(poly[k]); outTris.Add(poly[k + 1]); }
                clipped++;
            }
            var bodiceVerts = verts.Count - bodiceStart;
            // 型の段（キャミソールの縁の厚みなど）をならす: となりの頂点の平均へ 30 回寄せ、型の面から BodiceMin より内へは入れない
            SmoothBodice(bodiceStart, verts.Count, outTris, bodPos, templ, bw, toLocal, verts, norms);
            // 胴の UV は腰のまわりの向き。背中の真ん中で 0 と 1 がつながるので、またぐ三角の小さい側の頂点を写し、右へ BodiceU だけずらす
            var wrapCopy = new Dictionary<int, int>();
            var wrapped = 0;
            for (var t = 0; t < outTris.Count; t += 3)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (var e = 0; e < 3; e++) { var u = uvs[outTris[t + e]].x; lo = Mathf.Min(lo, u); hi = Mathf.Max(hi, u); }
                if (hi - lo < BodiceU * 0.5f) continue;
                for (var e = 0; e < 3; e++)
                {
                    var i = outTris[t + e];
                    if (uvs[i].x >= BodiceU * 0.5f) continue;
                    int c;
                    if (!wrapCopy.TryGetValue(i, out c))
                    {
                        verts.Add(verts[i]);
                        norms.Add(norms[i]);
                        uvs.Add(new Vector2(uvs[i].x + BodiceU, uvs[i].y));
                        weights.Add(weights[i]);
                        c = verts.Count - 1;
                        wrapCopy[i] = c;
                    }
                    outTris[t + e] = c;
                }
                wrapped++;
            }

            // ---- 袖 ----
            var sleeveTris = 0;
            for (var side = 0; side < 2; side++) sleeveTris += Sleeve(f, side, bw, templ, bodPos, bodW, toLocal, verts, norms, uvs, weights, outTris);

            // ---- スカート ----
            var skirtTris = Skirt(f, bw, templ, bwts, bodPos, toLocal, verts, norms, uvs, weights, outTris);

            note = string.Format(CultureInfo.InvariantCulture,
                "ワンピース: 胴 {0} 頂点（型の三角をそのまま {1}、切った {2}。襟ぐりは首の付け根の {3:0.0}/{4:0.0}/{5:0.0} cm 下、腰の切り替え {6:0.000} m）、" +
                "袖 {7} 三角（上腕の {8:0.00}〜{9:0.00}、広がり {10:0.0} cm）、スカート {11} 三角（裾 {12:0.00} m、半径 {13:0.00} m、ひだ {14} 本）",
                bodiceVerts, kept, clipped, NeckDropFront * 100f, NeckDropSide * 100f, NeckDropBack * 100f, f.waistY,
                sleeveTris, SleeveT0, SleeveT1, SleeveFlare * 100f, skirtTris, HemY, HemR, Waves);
            return outTris.ToArray();
        }

        static void SmoothBodice(int start, int end, List<int> tris, List<Vector3> bodPos, int[] templ, Vector3[] bw, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms)
        {
            var count = end - start;
            // 同じ位置の頂点をまとめる
            var rep = new int[count];
            var byKey = new Dictionary<long, int>();
            for (var i = 0; i < count; i++)
            {
                var k = Key(bodPos[i]);
                int r;
                if (!byKey.TryGetValue(k, out r)) { r = i; byKey[k] = i; }
                rep[i] = r;
            }
            var nb = new Dictionary<int, HashSet<int>>();
            for (var t = 0; t < tris.Count; t += 3)
            {
                var ids = new[] { tris[t] - start, tris[t + 1] - start, tris[t + 2] - start };
                if (ids[0] < 0 || ids[0] >= count || ids[1] < 0 || ids[1] >= count || ids[2] < 0 || ids[2] >= count) continue;
                for (var e = 0; e < 3; e++)
                {
                    int a = rep[ids[e]], b = rep[ids[(e + 1) % 3]];
                    if (a == b) continue;
                    HashSet<int> s;
                    if (!nb.TryGetValue(a, out s)) nb[a] = s = new HashSet<int>();
                    s.Add(b);
                    if (!nb.TryGetValue(b, out s)) nb[b] = s = new HashSet<int>();
                    s.Add(a);
                }
            }
            // 縁の頂点（襟ぐり・腰・袖の付け根）は動かさない
            var edgeCount = new Dictionary<long, int>();
            for (var t = 0; t < tris.Count; t += 3)
            {
                var ids = new[] { tris[t] - start, tris[t + 1] - start, tris[t + 2] - start };
                if (ids[0] < 0 || ids[0] >= count || ids[1] < 0 || ids[1] >= count || ids[2] < 0 || ids[2] >= count) continue;
                for (var e = 0; e < 3; e++)
                {
                    int a = rep[ids[e]], b = rep[ids[(e + 1) % 3]];
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int c;
                    edgeCount.TryGetValue(key, out c);
                    edgeCount[key] = c + 1;
                }
            }
            var border = new HashSet<int>();
            foreach (var kv in edgeCount)
                if (kv.Value == 1) { border.Add((int)(kv.Key >> 32)); border.Add((int)(kv.Key & 0xffffffff)); }
            var pos = new Vector3[count];
            for (var i = 0; i < count; i++) pos[i] = bodPos[rep[i]];
            var surf = new RocketboxCompose.Surface(bw, templ);
            for (var it = 0; it < 30; it++)
            {
                var next = (Vector3[])pos.Clone();
                foreach (var kv in nb)
                {
                    if (border.Contains(kv.Key)) continue;
                    var avg = Vector3.zero;
                    foreach (var j in kv.Value) avg += pos[j];
                    avg /= kv.Value.Count;
                    var p = Vector3.Lerp(pos[kv.Key], avg, 0.5f);
                    Vector3 q, n;
                    var d = surf.Signed(p, 0.03f, out q, out n);
                    if (!float.IsNaN(d) && d < BodiceMin) p += n * (BodiceMin - d);
                    next[kv.Key] = p;
                }
                pos = next;
            }
            for (var i = 0; i < count; i++)
            {
                var p = pos[rep[i]];
                bodPos[i] = p;
                verts[start + i] = toLocal.MultiplyPoint3x4(p);
            }
            // 法線をならした形から付け直す（型の段の陰が残らないように）。向きは元の法線の側
            var acc = new Vector3[count];
            for (var t = 0; t < tris.Count; t += 3)
            {
                var ids = new[] { tris[t] - start, tris[t + 1] - start, tris[t + 2] - start };
                if (ids[0] < 0 || ids[0] >= count || ids[1] < 0 || ids[1] >= count || ids[2] < 0 || ids[2] >= count) continue;
                var fn = Vector3.Cross(pos[rep[ids[1]]] - pos[rep[ids[0]]], pos[rep[ids[2]]] - pos[rep[ids[0]]]);
                var old = toLocal.inverse.MultiplyVector(norms[start + ids[0]]);
                if (Vector3.Dot(fn, old) < 0f) fn = -fn;
                foreach (var id in ids) acc[rep[id]] += fn;
            }
            var toW = toLocal.inverse;
            for (var i = 0; i < count; i++)
            {
                var n = acc[rep[i]];
                if (n.sqrMagnitude < 1e-12f) continue;
                norms[start + i] = toLocal.MultiplyVector(n.normalized).normalized;
            }
        }

        static int Sleeve(Frame f, int side, Vector3[] bw, int[] templ, List<Vector3> bodPos, List<BoneWeight> bodW, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, List<int> outTris)
        {
            var s = f.shoulder[side];
            var len = f.UpperArmLength(side);
            var a = (f.elbow[side] - s) / len;
            var chest = new Vector3(0f, s.y - 0.05f, s.z);
            var inner = Vector3.ProjectOnPlane(chest - s, a).normalized;
            var side2 = Vector3.Cross(a, inner).normalized;
            // 腕の太さ（輪ごと・向きごと。輪の上下 4 % の間の一番太い所）。付け根では肩の丸みに沿い、そこから下は太い所を下へ持ち越す
            var rRing = new float[SleeveRings + 1, SleeveSegs];
            foreach (var i in new HashSet<int>(templ))
            {
                float t, perp;
                if (!f.Arm(bw[i], side, out t, out perp) || t < SleeveT0 - 0.04f || t > SleeveT1 + 0.05f) continue;
                var v = Vector3.ProjectOnPlane(bw[i] - s, a);
                var ang = Mathf.Atan2(Vector3.Dot(v, side2), Vector3.Dot(v, inner));
                var k = ((int)Mathf.Round((ang + Mathf.PI) / (2f * Mathf.PI) * SleeveSegs)) % SleeveSegs;
                for (var j = 0; j <= SleeveRings; j++)
                    if (Mathf.Abs(t - Mathf.Lerp(SleeveT0, SleeveT1, j / (float)SleeveRings)) < 0.06f) rRing[j, k] = Mathf.Max(rRing[j, k], perp);
            }
            for (var j = 0; j <= SleeveRings; j++)
            {
                for (var pass = 0; pass < SleeveSegs; pass++)
                    for (var k = 0; k < SleeveSegs; k++)
                        if (rRing[j, k] <= 0f) rRing[j, k] = Mathf.Max(rRing[j, (k + SleeveSegs - 1) % SleeveSegs], rRing[j, (k + 1) % SleeveSegs]);
                var sm = new float[SleeveSegs];
                for (var k = 0; k < SleeveSegs; k++) sm[k] = Mathf.Max(rRing[j, k], (rRing[j, (k + SleeveSegs - 1) % SleeveSegs] + rRing[j, k] + rRing[j, (k + 1) % SleeveSegs]) / 3f);
                for (var k = 0; k < SleeveSegs; k++) rRing[j, k] = j > 0 ? Mathf.Max(sm[k], rRing[j - 1, k] - 0.004f) : sm[k];
            }
            int upper, clav;
            f.bone.TryGetValue(side == 0 ? "Bip01 L UpperArm" : "Bip01 R UpperArm", out upper);
            f.bone.TryGetValue(side == 0 ? "Bip01 L Clavicle" : "Bip01 R Clavicle", out clav);
            var grid = new int[SleeveRings + 1, SleeveSegs + 1];
            var pos = new Vector3[SleeveRings + 1, SleeveSegs + 1];
            for (var j = 0; j <= SleeveRings; j++)
            {
                var u = j / (float)SleeveRings;
                var t = Mathf.Lerp(SleeveT0, SleeveT1, u);
                for (var k = 0; k <= SleeveSegs; k++)
                {
                    var kk = k % SleeveSegs;
                    var ang = kk / (float)SleeveSegs * 2f * Mathf.PI - Mathf.PI;
                    var dir = Mathf.Cos(ang) * inner + Mathf.Sin(ang) * side2;
                    // 体の側（ang 0）は狭く、外の側（ang ±π）ほど広げる
                    var outer = 0.5f - 0.5f * Mathf.Cos(ang);
                    var r = rRing[j, kk] + SleeveBase * (0.8f + 0.2f * u) + SleeveFlare * Mathf.Pow(u, 1.3f) * (0.3f + 0.7f * outer);
                    pos[j, k] = s + a * (t * len) + dir * r;
                }
            }
            for (var j = 0; j <= SleeveRings; j++)
                for (var k = 0; k <= SleeveSegs; k++)
                {
                    var p = pos[j, k];
                    var du = pos[Mathf.Min(j + 1, SleeveRings), k] - pos[Mathf.Max(j - 1, 0), k];
                    var dv = pos[j, (k + 1) % (SleeveSegs + 1)] - pos[j, (k + SleeveSegs) % (SleeveSegs + 1)];
                    var n = Vector3.Cross(du, dv).normalized;
                    var radial = Vector3.ProjectOnPlane(p - s, a);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    // 付け根は胴の一番近い頂点の重みから、下は上腕だけへ
                    var bwv = new BoneWeight { boneIndex0 = upper, weight0 = 1f };
                    var u = j / (float)SleeveRings;
                    if (u < 0.3f)
                    {
                        var near = Nearest(bodPos, p);
                        if (near >= 0) bwv = Mix(bodW[near], bwv, Mathf.Clamp01(u / 0.3f));
                    }
                    grid[j, k] = Add(verts, norms, uvs, weights, toLocal, p, n,
                        new Vector2(0.5f + 0.5f * (k / (float)SleeveSegs), 0.5f + 0.5f * (1f - u)), bwv);
                }
            var tris = 0;
            for (var j = 0; j < SleeveRings; j++)
                for (var k = 0; k < SleeveSegs; k++)
                {
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k]); outTris.Add(grid[j + 1, k + 1]);
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k + 1]); outTris.Add(grid[j, k + 1]);
                    tris += 2;
                }
            return tris;
        }

        static int Skirt(Frame f, Vector3[] bw, int[] templ, BoneWeight[] bwts, List<Vector3> bodPos, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, List<int> outTris)
        {
            var yTop = f.waistY + 0.012f;
            Func<Vector3, float> angle = p => Mathf.Atan2(p.x, p.z - f.zc);
            Func<float, int> bin = th => ((int)Mathf.Round((th + Mathf.PI) / (2f * Mathf.PI) * SkirtSegs)) % SkirtSegs;
            // 上の縁: 胴の面の、その高さの向きごとの半径
            var rTop = new float[SkirtSegs];
            foreach (var p in bodPos)
            {
                if (Mathf.Abs(p.y - yTop) > 0.02f) continue;
                var k = bin(angle(p));
                rTop[k] = Mathf.Max(rTop[k], new Vector2(p.x, p.z - f.zc).magnitude);
            }
            for (var pass = 0; pass < SkirtSegs; pass++)
                for (var k = 0; k < SkirtSegs; k++)
                    if (rTop[k] <= 0f) rTop[k] = Mathf.Max(rTop[(k + SkirtSegs - 1) % SkirtSegs], rTop[(k + 1) % SkirtSegs]);
            // 体の太さ（腰と腿。スカートはここから 2 cm 以上離す）
            var ringY = new float[SkirtRings + 1];
            for (var j = 0; j <= SkirtRings; j++) ringY[j] = Mathf.Lerp(yTop, HemY, j / (float)SkirtRings);
            var rBody = new float[SkirtRings + 1, SkirtSegs];
            // 腰と脚の骨に付いた頂点だけ（束ねた姿勢では手が腰の高さにあるので、腕と手は外す）
            var hip = new HashSet<int>();
            foreach (var n in new[] { "Bip01 Pelvis", "Bip01 Spine", "Bip01 L Thigh", "Bip01 R Thigh", "Bip01 L Calf", "Bip01 R Calf" })
            {
                int b;
                if (f.bone.TryGetValue(n, out b)) hip.Add(b);
            }
            foreach (var i in new HashSet<int>(templ))
            {
                if (!hip.Contains(bwts[i].boneIndex0)) continue;
                var p = bw[i];
                if (p.y > yTop || p.y < HemY - 0.03f) continue;
                var r = new Vector2(p.x, p.z - f.zc).magnitude;
                if (r > 0.35f) continue;
                var k = bin(angle(p));
                for (var j = 0; j <= SkirtRings; j++)
                    if (Mathf.Abs(p.y - ringY[j]) < 0.025f) rBody[j, k] = Mathf.Max(rBody[j, k], r);
            }
            int pel, lt, rt, lc, rc;
            f.bone.TryGetValue("Bip01 Pelvis", out pel);
            f.bone.TryGetValue("Bip01 L Thigh", out lt);
            f.bone.TryGetValue("Bip01 R Thigh", out rt);
            f.bone.TryGetValue("Bip01 L Calf", out lc);
            f.bone.TryGetValue("Bip01 R Calf", out rc);
            var pos = new Vector3[SkirtRings + 1, SkirtSegs + 1];
            for (var j = 0; j <= SkirtRings; j++)
            {
                var s = j / (float)SkirtRings;
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var kk = k % SkirtSegs;
                    var th = kk / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    var flare = 1f - Mathf.Pow(1f - s, 2.0f);
                    var r = rTop[kk] - 0.001f + (HemR - rTop[kk]) * flare + WaveAmp * Mathf.Pow(s, 1.3f) * Mathf.Sin(Waves * th);
                    var body = 0f;
                    for (var d = -1; d <= 1; d++) body = Mathf.Max(body, rBody[j, (kk + d + SkirtSegs) % SkirtSegs]);
                    if (j > 0 && body > 0f) r = Mathf.Max(r, body + 0.02f);
                    pos[j, k] = new Vector3(Mathf.Sin(th) * r, ringY[j], f.zc + Mathf.Cos(th) * r);
                }
            }
            var grid = new int[SkirtRings + 1, SkirtSegs + 1];
            for (var j = 0; j <= SkirtRings; j++)
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var p = pos[j, k];
                    var du = pos[Mathf.Min(j + 1, SkirtRings), k] - pos[Mathf.Max(j - 1, 0), k];
                    var dv = pos[j, Mathf.Min(k + 1, SkirtSegs)] - pos[j, Mathf.Max(k - 1, 0)];
                    var n = Vector3.Cross(du, dv).normalized;
                    var radial = new Vector3(p.x, 0f, p.z - f.zc);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    var s = j / (float)SkirtRings;
                    var th = (k % SkirtSegs) / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    // 腰の骨を中心に、脚の骨は裾で LegShare まで。向きで左右の腿を混ぜ（前と後ろの真ん中は半分ずつ）、膝より下は脛を少し。
                    // 脚の割合を 4 割にしたら、大きく踏み出す一歩で前の膝が裾の前から出た
                    var wp = Mathf.Lerp(1f, 1f - LegShare, Mathf.Pow(s, 0.7f));
                    var legs = 1f - wp;
                    var sl = RocketboxPaint.Smooth(0.5f, -0.5f, Mathf.Sin(th));
                    var calf = 0.2f * RocketboxPaint.Smooth(f.kneeY + 0.05f, f.kneeY - 0.10f, p.y);
                    var w = Top4(new[] { pel, lt, rt, lc, rc }, new[] { wp, legs * sl * (1f - calf), legs * (1f - sl) * (1f - calf), legs * sl * calf, legs * (1f - sl) * calf });
                    grid[j, k] = Add(verts, norms, uvs, weights, toLocal, p, n, new Vector2(k / (float)SkirtSegs, 0.5f * (1f - s)), w);
                }
            var tris = 0;
            for (var j = 0; j < SkirtRings; j++)
                for (var k = 0; k < SkirtSegs; k++)
                {
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k]); outTris.Add(grid[j + 1, k + 1]);
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k + 1]); outTris.Add(grid[j, k + 1]);
                    tris += 2;
                }
            return tris;
        }

        /// <summary>胴の UV（絵の左上の四分の一）: 腰の真ん中のまわりの向きと高さ</summary>
        static Vector2 BodiceUv(Frame f, Vector3 p)
        {
            var th = Mathf.Atan2(p.x, p.z - f.zc);
            var v = Mathf.InverseLerp(f.waistY - 0.02f, f.neckY + 0.02f, p.y);
            return new Vector2(BodiceU * (th + Mathf.PI) / (2f * Mathf.PI), 0.5f + 0.5f * v);
        }

        /// <summary>胴の UV の幅（絵の左上。背中の真ん中をまたぐ三角は右へ BodiceU ずらして、0.47 までにはみ出す）</summary>
        const float BodiceU = 0.45f;

        // ---- 布の絵 -------------------------------------------------------------

        /// <summary>生成りの布の色（sRGB）</summary>
        public static readonly Color Cloth = new Color(0.87f, 0.83f, 0.74f);

        /// <summary>
        /// ワンピースの絵を描く（n×n）。組み合わせたメッシュのワンピースの面の組（一番後ろ）を UV に並べ、画素ごとの束ねた姿勢の位置から描く:
        /// - 布の地: 画素ごとの細かい粒（±2 %）と、横と縦の糸のむら（麻の節）、大きなむら（±2 %）
        /// - 縫い目: 脇（胴とスカート）、肩、襟ぐりの見返し（縁の陰と 9 mm 内の縫い線）、腰の切り替え、袖の付け根と袖口、裾の折り返し（1.8 cm 上の縫い線）
        /// - 陰: 腰の切り替えのすぐ下（胴のかぶり）とギャザー、スカートの縦のひだ（形の波に合わせて山を明るく、谷を暗く）。
        ///   胸の下の陰は、胸の真ん中の V の溝に見えたので描かない
        /// </summary>
        public static Color[] Paint(RocketboxPerson who, int n, out string note)
        {
            var bodySmr = AssetDatabase.LoadAssetAtPath<GameObject>(who.BodyFrom.Model).GetComponentInChildren<SkinnedMeshRenderer>();
            var f = Frame.Of(bodySmr);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(who.CompositeMesh);
            if (mesh == null) throw new InvalidOperationException("組み合わせたメッシュが無い: " + who.CompositeMesh);
            var M = bodySmr.transform.localToWorldMatrix;
            var lv = mesh.vertices;
            var wv = new Vector3[lv.Length];
            for (var i = 0; i < lv.Length; i++) wv[i] = M.MultiplyPoint3x4(lv[i]);
            var s = RocketboxPaint.Surface.Of(wv, mesh.uv, mesh.GetTriangles(mesh.subMeshCount - 1), n);
            var px = new Color[n * n];
            var yTop = f.waistY + 0.012f;
            var lin = new Vector3(Mathf.GammaToLinearSpace(Cloth.r), Mathf.GammaToLinearSpace(Cloth.g), Mathf.GammaToLinearSpace(Cloth.b));
            int onCount = 0;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var i = y * n + x;
                    var u = (x + 0.5f) / n;
                    var v = (y + 0.5f) / n;
                    var shade = 1f;
                    // 布の地
                    shade += (Hash(x, y) - 0.5f) * 0.04f;
                    shade += (Noise(x * 0.08f, y * 0.9f) - 0.5f) * 0.035f;
                    shade += (Noise(x * 0.9f + 50f, y * 0.08f) - 0.5f) * 0.03f;
                    shade += (Noise(x * 0.02f + 11f, y * 0.02f + 7f) - 0.5f) * 0.04f;
                    if (s.On[i])
                    {
                        onCount++;
                        var p = s.P[i];
                        var skirt = v < 0.5f;
                        var sleeve = !skirt && u >= 0.5f;
                        var th = Mathf.Atan2(p.x, p.z - f.zc);
                        var r = new Vector2(p.x, p.z - f.zc).magnitude;
                        if (!sleeve)
                        {
                            // 脇の縫い目
                            var side = Mathf.Min(Mathf.Abs(Wrap(th - Mathf.PI * 0.5f)), Mathf.Abs(Wrap(th + Mathf.PI * 0.5f))) * r;
                            shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.0035f, 0.0005f, side);
                        }
                        if (skirt)
                        {
                            var sk = Mathf.Clamp01((yTop - p.y) / (yTop - HemY));
                            // 縦のひだ（形の波に合わせる）
                            shade *= 1f + 0.08f * Mathf.Sin(Waves * th) * Mathf.Pow(sk, 1.3f);
                            // 腰のすぐ下: 胴のかぶりの陰とギャザー
                            var dT = yTop - p.y;
                            shade *= 1f - 0.10f * RocketboxPaint.Smooth(0.015f, 0f, dT);
                            shade *= 1f - 0.07f * RocketboxPaint.Smooth(0.08f, 0f, dT) * (0.5f + 0.5f * Mathf.Sin(th * 48f));
                            // 裾の折り返し: 縁の陰と、1.8 cm 上の縫い線
                            var dH = p.y - HemY;
                            shade *= 1f - 0.10f * RocketboxPaint.Smooth(0.005f, 0f, dH);
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.0025f, 0.0005f, Mathf.Abs(dH - 0.018f)) * Dash(th * r, 0.006f);
                        }
                        else if (!sleeve)
                        {
                            // 襟ぐり: 縁の陰と、9 mm 内の縫い線
                            var tn = Mathf.Atan2(p.x, p.z - f.neckZ);
                            var dN = Neck(f, p);
                            shade *= 1f - 0.10f * RocketboxPaint.Smooth(0.005f, 0f, dN);
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.0025f, 0.0005f, Mathf.Abs(dN - 0.009f)) * Dash(tn * 0.06f, 0.006f);
                            // 肩の縫い目（首の外、肩の一番上）
                            if (Mathf.Abs(p.x) > 0.06f && p.y > f.neckY - 0.10f)
                                shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.004f, 0.001f, Mathf.Abs(p.z - (f.neckZ + 0.012f)));
                            // 腰の切り替え: 縁の陰と 8 mm 上の縫い線
                            var dW = p.y - (f.waistY - 0.012f);
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.006f, 0f, dW);
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.0025f, 0.0005f, Mathf.Abs(dW - 0.010f)) * Dash(th * r, 0.006f);
                        }
                        else
                        {
                            // 袖: 付け根の縁と、袖口の折り返し
                            var sideIdx = p.x < 0f ? 0 : 1;
                            float t, perp;
                            f.Arm(p, sideIdx, out t, out perp);
                            var len = f.UpperArmLength(sideIdx);
                            var dA = (t - SleeveT0) * len;
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.006f, 0f, dA);
                            var dC = (SleeveT1 - t) * len;
                            shade *= 1f - 0.10f * RocketboxPaint.Smooth(0.005f, 0f, dC);
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.0025f, 0.0005f, Mathf.Abs(dC - 0.015f)) * Dash(perp * Mathf.Atan2(p.z, p.y) , 0.006f);
                        }
                    }
                    var col = lin * shade;
                    px[i] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(col.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(col.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(col.z)), 1f);
                }
            note = string.Format(CultureInfo.InvariantCulture, "ワンピースの絵: {0}×{0}、布の画素 {1}、色 {2}", n, onCount, Cloth);
            return px;
        }

        /// <summary>縫い線の点線（長さ period の半分が糸）</summary>
        static float Dash(float along, float period)
        {
            var k = Mathf.Repeat(along / period, 1f);
            return k < 0.55f ? 1f : 0.25f;
        }

        static float Wrap(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
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

        static float Noise(float x, float y)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            var a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), tx);
            var b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>仕上げの段のマテリアル: 布の絵。スカートと袖の内側も見えるので両面を描く</summary>
        public static Material Textured(Texture tex)
        {
            var m = BuildRocketboxProtagonist.Lit("Dress", tex, 0.08f, false);
            m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        static int Add(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, Matrix4x4 toLocal,
            Vector3 p, Vector3 n, Vector2 uv, BoneWeight w)
        {
            verts.Add(toLocal.MultiplyPoint3x4(p));
            norms.Add(toLocal.MultiplyVector(n).normalized);
            uvs.Add(uv);
            weights.Add(w);
            return verts.Count - 1;
        }

        static int Nearest(List<Vector3> ps, Vector3 p)
        {
            var best = -1;
            var bd = float.MaxValue;
            for (var i = 0; i < ps.Count; i++)
            {
                var d = (ps[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static long Key(Vector3 p)
        {
            return ((long)Mathf.RoundToInt(p.x * 5000f) + 100000) * 10000000000L + ((long)Mathf.RoundToInt(p.y * 5000f) + 100000) * 100000L + (Mathf.RoundToInt(p.z * 5000f) + 50000);
        }

        /// <summary>二つの重みを t（0 で a、1 で b）で混ぜ、大きい四つを取る</summary>
        static BoneWeight Mix(BoneWeight a, BoneWeight b, float t)
        {
            var acc = new Dictionary<int, float>();
            Action<int, float> add = (i, v) => { if (v <= 0f) return; float o; acc.TryGetValue(i, out o); acc[i] = o + v; };
            add(a.boneIndex0, a.weight0 * (1f - t)); add(a.boneIndex1, a.weight1 * (1f - t)); add(a.boneIndex2, a.weight2 * (1f - t)); add(a.boneIndex3, a.weight3 * (1f - t));
            add(b.boneIndex0, b.weight0 * t); add(b.boneIndex1, b.weight1 * t); add(b.boneIndex2, b.weight2 * t); add(b.boneIndex3, b.weight3 * t);
            var ids = new List<int>(acc.Keys);
            var ws = new List<float>();
            foreach (var i in ids) ws.Add(acc[i]);
            return Top4(ids.ToArray(), ws.ToArray());
        }

        static BoneWeight Top4(int[] ids, float[] ws)
        {
            var order = new List<int>();
            for (var i = 0; i < ids.Length; i++) if (ws[i] > 0f) order.Add(i);
            order.Sort((x, y) => ws[y].CompareTo(ws[x]));
            var sum = 0f;
            for (var k = 0; k < order.Count && k < 4; k++) sum += ws[order[k]];
            var w = new BoneWeight();
            if (sum <= 0f) return w;
            if (order.Count > 0) { w.boneIndex0 = ids[order[0]]; w.weight0 = ws[order[0]] / sum; }
            if (order.Count > 1) { w.boneIndex1 = ids[order[1]]; w.weight1 = ws[order[1]] / sum; }
            if (order.Count > 2) { w.boneIndex2 = ids[order[2]]; w.weight2 = ws[order[2]] / sum; }
            if (order.Count > 3) { w.boneIndex3 = ids[order[3]]; w.weight3 = ws[order[3]] / sum; }
            return w;
        }

        /// <summary>形の段のマテリアル: 生成りの一色（陰影だけ）。スカートと袖の内側も見えるので両面を描く</summary>
        public static Material Plain()
        {
            var m = BuildRocketboxProtagonist.Lit("Dress", null, 0.08f, false);
            m.SetColor("_BaseColor", new Color(0.86f, 0.82f, 0.74f));
            m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }
    }
}
