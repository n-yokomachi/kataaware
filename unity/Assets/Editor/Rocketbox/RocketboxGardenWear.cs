using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 場面 6（庭の記憶）で水を撒く女性（過去の主人公）の服を一から作る（既存の服の形は使わない。片割れのワンピース <see cref="RocketboxDress"/> と同じ作り方）。
    /// 原作の「オリーブ色のシャツに、丈の長いスカート」（シナリオ設計書 10.3 節）。
    /// - シャツ（<see cref="Shirt"/>）: 綿の半袖。前は開けずに着る（襟ぐりの前に短い前立てとボタン三つ）。裾はスカートの外に出す
    ///   - 胴: 体の人（女大 18）の肌と服の面を型にして、数 mm 外へ出した面。丸い襟ぐり・腰の切り替え・袖の付け根で、三角を値の面で切る。骨の付き方は型の頂点のまま
    ///   - 裾: 腰の切り替えから腰骨の下まで垂れる筒（<see cref="TailDrop"/>）。スカートの面より常に <see cref="TailGap"/> 外に置き、スカートの上に被さる
    ///   - 袖: 上腕のまわりのまっすぐな筒。長さは上腕の <see cref="SleeveT1"/>。袖口は折り返しの帯
    /// - スカート（<see cref="Skirt"/>）: 腰から、くるぶしの少し上の裾（<see cref="HemY"/>）へゆるく広がる筒。明るい生成り。形は腰から測るが、
    ///   作るのはシャツの裾の内へ <see cref="SkirtTuck"/> 入った所から下だけ（裾に隠れる腰まわりは作らない）。
    ///   縦にゆるい波のひだ。骨はワンピースのスカートと同じく、腰の骨を中心に脚の骨を裾へ向けて混ぜる
    /// 形はどれも束ねた姿勢（体の人の骨のまま）で作る。骨の位置は <see cref="RocketboxDress.Frame"/> を使う
    /// </summary>
    public static class RocketboxGardenWear
    {
        // ---- 形の値（m、割合） ----
        /// <summary>丸い襟ぐり: 首の付け根から下へ。前・横・後ろ（後ろはワンピースと同じく付け根の上まで。うなじと襟の間を抜けさせない）</summary>
        public const float NeckDropFront = 0.050f, NeckDropSide = 0.016f, NeckDropBack = -0.028f;
        /// <summary>胴の面を型から外へ出す量（綿のシャツ。ワンピースよりゆったり）</summary>
        public const float BodiceOffset = 0.007f;
        public const float BodiceMin = 0.003f;
        /// <summary>胴の面が上腕を覆う長さ（上腕の長さの割合。袖の付け根の下に隠れる）</summary>
        public const float CapT = 0.16f;
        /// <summary>袖の付け根と袖口（上腕の長さの割合）。半袖</summary>
        public const float SleeveT0 = 0.05f, SleeveT1 = 0.58f;
        /// <summary>袖と腕の間の隙間、袖口の折り返しの帯（袖の長さの割合）と厚み</summary>
        public const float SleeveBase = 0.011f, SleeveCuff = 0.16f, CuffThick = 0.002f;
        /// <summary>シャツの裾: 腰の切り替えからの下がり。スカートの面から外へ離す量</summary>
        public const float TailDrop = 0.17f, TailGap = 0.010f;
        /// <summary>スカートの上の縁を、シャツの裾の下の縁からどれだけ上（裾の内）に置くか。これより上のスカートは作らない</summary>
        public const float SkirtTuck = 0.035f;
        /// <summary>シャツの裾が腰と尻の丸みから離れるゆとり（m）</summary>
        public const float TailEase = 0.012f;
        /// <summary>スカートの裾の高さと半径（腰の真ん中から）、ひだの波の深さと数</summary>
        public const float HemY = 0.075f, HemR = 0.26f, WaveAmp = 0.010f;
        public const int Waves = 9;
        /// <summary>スカートの裾で脚の骨に付ける割合（残りは腰の骨）</summary>
        public const float LegShare = 0.85f;
        const int SkirtRings = 20, SkirtSegs = 72, TailRings = 8, SleeveRings = 8, SleeveSegs = 24;
        const float NeckRadius = RocketboxDress.NeckRadius, NeckMarginFront = 0.12f, NeckMarginSide = 0.02f, NeckMarginBack = 0.03f;

        /// <summary>シャツの色（sRGB）。オリーブ</summary>
        public static readonly Color Shirt = new Color(0.42f, 0.44f, 0.25f);
        /// <summary>スカートの色（sRGB）。明るい生成り</summary>
        public static readonly Color Skirt = new Color(0.87f, 0.82f, 0.70f);

        /// <summary>襟ぐりの内側（服が覆う側）が正の値（m）。ワンピースの襟ぐり（<see cref="RocketboxDress.Neck"/>）の V を丸くした形</summary>
        public static float Neck(RocketboxDress.Frame f, Vector3 p)
        {
            var dz = p.z - f.neckZ;
            var th = Mathf.Atan2(p.x, dz);
            var c = Mathf.Cos(th);
            var drop = c >= 0f ? Mathf.Lerp(NeckDropSide, NeckDropFront, Mathf.Pow(c, 1.5f)) : Mathf.Lerp(NeckDropSide, NeckDropBack, Mathf.Sqrt(-c));
            var margin = c >= 0f ? Mathf.Lerp(NeckMarginSide, NeckMarginFront, c * c) : Mathf.Lerp(NeckMarginSide, NeckMarginBack, c * c);
            var radial = new Vector2(p.x, dz).magnitude - (NeckRadius + margin);
            return Mathf.Max((f.neckY - drop) - p.y, p.y < f.neckY + 0.03f ? radial : -1f);
        }

        static float Keep(RocketboxDress.Frame f, Vector3 p, int armSide)
        {
            var g = Neck(f, p);
            g = Mathf.Min(g, p.y - (f.waistY - 0.012f));
            for (var side = 0; side < 2; side++)
            {
                float t, perp;
                if (f.Arm(p, side, out t, out perp) || armSide == side) g = Mathf.Min(g, (CapT - t) * f.UpperArmLength(side));
            }
            return g;
        }

        /// <summary>頭の人の面で、服の下に隠れる所（襟ぐりより前は 5 mm、後ろは 3 cm 内）</summary>
        public static bool UnderDress(RocketboxDress.Frame f, Vector3 p)
        {
            var back = RocketboxPaint.Smooth(0.2f, -0.4f, Mathf.Cos(Mathf.Atan2(p.x, p.z - f.neckZ)));
            return Neck(f, p) > Mathf.Lerp(0.005f, 0.03f, back);
        }

        /// <summary>体の人の面のうち、服の外に出る所（袖口より先の腕と手）だけを残す。袖の中へ 3 cm 重ねる</summary>
        public static int[] KeepArms(RocketboxDress.Frame f, int[] tris, Vector3[] w)
        {
            var kept = new List<int>();
            for (var t = 0; t < tris.Length; t += 3)
            {
                var c = (w[tris[t]] + w[tris[t + 1]] + w[tris[t + 2]]) / 3f;
                var keep = false;
                for (var side = 0; side < 2; side++)
                {
                    var s = f.shoulder[side];
                    var a = (f.elbow[side] - s).normalized;
                    var at = Vector3.Dot(c - s, a) / f.UpperArmLength(side);
                    var perp = ((c - s) - a * Vector3.Dot(c - s, a)).magnitude;
                    var outward = (c.x - s.x) * Mathf.Sign(s.x);
                    if (outward > 0.02f && at > SleeveT1 - 0.03f / f.UpperArmLength(side) && (perp < 0.10f || at > 1f)) keep = true;
                }
                if (keep) { kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]); }
            }
            return kept.ToArray();
        }

        /// <summary>
        /// シャツとスカートのメッシュを作り、verts などへ足して三角の並びを返す（体の人のメッシュの中の位置で）。
        /// templ は型にする体の人の三角（体の面と頭の面）、bw・bn・bwts は体の人の頂点の束ねた姿勢の世界の位置・法線と骨の重み
        /// </summary>
        public static int[] Build(SkinnedMeshRenderer bodySmr, int[] templ, Vector3[] bw, Vector3[] bn, BoneWeight[] bwts,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, out string note)
        {
            var f = RocketboxDress.Frame.Of(bodySmr);
            var toLocal = bodySmr.transform.worldToLocalMatrix;
            var outTris = new List<int>();

            // ---- 胴: 型の三角を値の面で切り、外へ出す ----
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
                if (bwts[i].boneIndex0 == headBone && bwts[i].weight0 > 0.5f) g[i] = Mathf.Min(g[i], -0.001f);
            }
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
                o = Add(verts, norms, uvs, weights, toLocal, bw[i] + n * BodiceOffset, n, ShirtUv(f, bw[i]), bwts[i]);
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
                o = Add(verts, norms, uvs, weights, toLocal, p + n * BodiceOffset, n, ShirtUv(f, p), w);
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
            Smooth(bodiceStart, verts.Count, outTris, bodPos, templ, bw, toLocal, verts, norms);
            Wrap(outTris, uvs, verts, norms, weights);

            // ---- 袖 ----
            var sleeveTris = 0;
            for (var side = 0; side < 2; side++) sleeveTris += Sleeve(f, side, bw, templ, bodPos, bodW, toLocal, verts, norms, uvs, weights, outTris);

            // ---- スカートと、その上に被さるシャツの裾 ----
            float[] rTop;
            var skirtTris = SkirtTube(f, bw, templ, bwts, bodPos, bodW, toLocal, verts, norms, uvs, weights, outTris, out rTop);

            // 表の三角の巻きを法線の向きに揃え、裏の布を内へ下げて足す（ワンピースと同じ）
            for (var t = 0; t < outTris.Count; t += 3)
            {
                int i0 = outTris[t], i1 = outTris[t + 1], i2 = outTris[t + 2];
                var fn = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
                if (Vector3.Dot(fn, norms[i0] + norms[i1] + norms[i2]) >= 0f) continue;
                outTris[t + 1] = i2;
                outTris[t + 2] = i1;
            }
            var lining = new Dictionary<int, int>();
            var front = outTris.Count;
            for (var t = 0; t < front; t += 3)
            {
                var ids = new int[3];
                for (var e = 0; e < 3; e++)
                {
                    var i = outTris[t + e];
                    int c;
                    if (!lining.TryGetValue(i, out c))
                    {
                        verts.Add(verts[i] - norms[i] * LiningGap);
                        norms.Add(-norms[i]);
                        uvs.Add(uvs[i]);
                        weights.Add(weights[i]);
                        c = verts.Count - 1;
                        lining[i] = c;
                    }
                    ids[e] = c;
                }
                outTris.Add(ids[0]); outTris.Add(ids[2]); outTris.Add(ids[1]);
            }

            note = string.Format(CultureInfo.InvariantCulture,
                "シャツとスカート: 胴 {0} 頂点（型の三角をそのまま {1}、切った {2}。襟ぐりは首の付け根の {3:0.0}/{4:0.0}/{5:0.0} cm 下、腰の切り替え {6:0.000} m）、" +
                "袖 {7} 三角（上腕の {8:0.00}〜{9:0.00}）、スカートと裾 {10} 三角（シャツの裾 {11:0.000} m、スカートの裾 {12:0.00} m、半径 {13:0.00} m）",
                bodiceVerts, kept, clipped, NeckDropFront * 100f, NeckDropSide * 100f, NeckDropBack * 100f, f.waistY,
                sleeveTris, SleeveT0, SleeveT1, skirtTris, f.waistY - TailDrop, HemY, HemR);
            return outTris.ToArray();
        }

        /// <summary>背中の真ん中をまたぐ胴の三角の、小さい側の頂点を写して右へずらす（UV が 0 と 1 でつながる所）</summary>
        static void Wrap(List<int> outTris, List<Vector2> uvs, List<Vector3> verts, List<Vector3> norms, List<BoneWeight> weights)
        {
            var copy = new Dictionary<int, int>();
            for (var t = 0; t < outTris.Count; t += 3)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (var e = 0; e < 3; e++) { var u = uvs[outTris[t + e]].x; lo = Mathf.Min(lo, u); hi = Mathf.Max(hi, u); }
                if (hi - lo < ShirtU * 0.5f) continue;
                for (var e = 0; e < 3; e++)
                {
                    var i = outTris[t + e];
                    if (uvs[i].x >= ShirtU * 0.5f) continue;
                    int c;
                    if (!copy.TryGetValue(i, out c))
                    {
                        verts.Add(verts[i]);
                        norms.Add(norms[i]);
                        uvs.Add(new Vector2(uvs[i].x + ShirtU, uvs[i].y));
                        weights.Add(weights[i]);
                        c = verts.Count - 1;
                        copy[i] = c;
                    }
                    outTris[t + e] = c;
                }
            }
        }

        /// <summary>型の段をならす（ワンピースの胴と同じ手順。縁は動かさず、型の面から BodiceMin より内へ入れない）</summary>
        static void Smooth(int start, int end, List<int> tris, List<Vector3> bodPos, int[] templ, Vector3[] bw, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms)
        {
            var count = end - start;
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
            var edgeCount = new Dictionary<long, int>();
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
            for (var i = 0; i < count; i++)
            {
                var n = acc[rep[i]];
                if (n.sqrMagnitude < 1e-12f) continue;
                norms[start + i] = toLocal.MultiplyVector(n.normalized).normalized;
            }
        }

        /// <summary>まっすぐな半袖。上腕のまわりの筒で、腕の太さに SleeveBase のゆとりを足す。袖口は折り返しの帯で CuffThick だけ厚い</summary>
        static int Sleeve(RocketboxDress.Frame f, int side, Vector3[] bw, int[] templ, List<Vector3> bodPos, List<BoneWeight> bodW, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, List<int> outTris)
        {
            var s = f.shoulder[side];
            var len = f.UpperArmLength(side);
            var a = (f.elbow[side] - s) / len;
            var chest = new Vector3(0f, s.y - 0.05f, s.z);
            var inner = Vector3.ProjectOnPlane(chest - s, a).normalized;
            var side2 = Vector3.Cross(a, inner).normalized;
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
                // 下へは細らせない（まっすぐな筒）
                for (var k = 0; k < SleeveSegs; k++) rRing[j, k] = j > 0 ? Mathf.Max(sm[k], rRing[j - 1, k] - 0.002f) : sm[k];
            }
            int upper;
            f.bone.TryGetValue(side == 0 ? "Bip01 L UpperArm" : "Bip01 R UpperArm", out upper);
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
                    // 外の側は少し余らせる（綿の袖が腕から離れて垂れる）
                    var outer = 0.5f - 0.5f * Mathf.Cos(ang);
                    var cuff = u > 1f - SleeveCuff ? CuffThick : 0f;
                    var r = rRing[j, kk] + SleeveBase * (0.7f + 0.3f * outer) + cuff;
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

        /// <summary>
        /// スカートの筒と、その上に被さるシャツの裾の筒。スカートは腰の切り替えから裾へ、シャツの裾は腰の切り替えの少し上から TailDrop 下まで。
        /// シャツの裾の半径は、同じ高さのスカートの半径より TailGap 外（スカートは裾の内に隠れる）
        /// </summary>
        static int SkirtTube(RocketboxDress.Frame f, Vector3[] bw, int[] templ, BoneWeight[] bwts, List<Vector3> bodPos, List<BoneWeight> bodW, Matrix4x4 toLocal,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, List<int> outTris, out float[] rTop)
        {
            var yTop = f.waistY + 0.012f;
            Func<Vector3, float> angle = p => Mathf.Atan2(p.x, p.z - f.zc);
            Func<float, int> bin = th => ((int)Mathf.Round((th + Mathf.PI) / (2f * Mathf.PI) * SkirtSegs)) % SkirtSegs;
            // 上の縁: 胴の面の、その高さの向きごとの半径
            rTop = new float[SkirtSegs];
            foreach (var p in bodPos)
            {
                if (Mathf.Abs(p.y - yTop) > 0.02f) continue;
                var k = bin(angle(p));
                rTop[k] = Mathf.Max(rTop[k], new Vector2(p.x, p.z - f.zc).magnitude);
            }
            for (var pass = 0; pass < SkirtSegs; pass++)
                for (var k = 0; k < SkirtSegs; k++)
                    if (rTop[k] <= 0f) rTop[k] = Mathf.Max(rTop[(k + SkirtSegs - 1) % SkirtSegs], rTop[(k + 1) % SkirtSegs]);
            var ringY = new float[SkirtRings + 1];
            for (var j = 0; j <= SkirtRings; j++) ringY[j] = Mathf.Lerp(yTop, HemY, j / (float)SkirtRings);
            // 体の太さ（腰と脚の骨に付いた頂点だけ。スカートはここから 2 cm 以上離す）
            var rBody = new float[SkirtRings + 1, SkirtSegs];
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
            // スカートの半径（輪 × 向き）。腰から裾へゆるく広がり、縦にゆるい波
            var rSkirt = new float[SkirtRings + 1, SkirtSegs];
            for (var j = 0; j <= SkirtRings; j++)
                for (var k = 0; k < SkirtSegs; k++)
                {
                    var u = j / (float)SkirtRings;
                    var r = rTop[k] - 0.003f + (HemR - rTop[k]) * Mathf.Pow(u, 0.85f);
                    var body = 0f;
                    for (var d = -1; d <= 1; d++) body = Mathf.Max(body, rBody[j, (k + d + SkirtSegs) % SkirtSegs]);
                    // 腰まわり（膝より上）は体に近く沿わせ、下は歩く脚の分だけ離す。シャツの裾がその外に被さるので、腰で膨らませない
                    var clear = Mathf.Lerp(0.010f, 0.02f, RocketboxPaint.Smooth(f.kneeY + 0.15f, f.kneeY, ringY[j]));
                    if (j > 0 && body > 0f) r = Mathf.Max(r, body + clear);
                    rSkirt[j, k] = r;
                }
            // 布は尻の谷間へ沈まずに渡る。輪ごとに向きのまわりで膨らませてからならし、谷を埋める。
            // 谷を残すと、谷をまたぐスカートの三角の辺が、上に被さるシャツの裾の面から突き抜けた（立ちの形で 2.5 mm）
            for (var j = 1; j <= SkirtRings; j++)
            {
                var wide = new float[SkirtSegs];
                for (var k = 0; k < SkirtSegs; k++)
                {
                    var m = 0f;
                    for (var d = -3; d <= 3; d++) m = Mathf.Max(m, rSkirt[j, (k + d + SkirtSegs) % SkirtSegs]);
                    wide[k] = m;
                }
                for (var k = 0; k < SkirtSegs; k++)
                    rSkirt[j, k] = (wide[(k + SkirtSegs - 1) % SkirtSegs] + wide[k] * 2f + wide[(k + 1) % SkirtSegs]) * 0.25f;
                // 縦のゆるい波のひだは、谷を埋めた後に外へだけ足す（体からの離れを削らない）
                for (var k = 0; k < SkirtSegs; k++)
                {
                    var th = k / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    rSkirt[j, k] += WaveAmp * Mathf.Pow(j / (float)SkirtRings, 0.7f) * (0.5f + 0.5f * Mathf.Sin(Waves * th + 0.4f));
                }
            }
            int pel, lt, rt, lc, rc, spine;
            f.bone.TryGetValue("Bip01 Pelvis", out pel);
            f.bone.TryGetValue("Bip01 Spine", out spine);
            f.bone.TryGetValue("Bip01 L Thigh", out lt);
            f.bone.TryGetValue("Bip01 R Thigh", out rt);
            f.bone.TryGetValue("Bip01 L Calf", out lc);
            f.bone.TryGetValue("Bip01 R Calf", out rc);
            var total = yTop - HemY;
            Func<float, float, float, BoneWeight> legWeight = (y, th, share) =>
            {
                var s = Mathf.Clamp01((yTop - y) / total);
                var wp = Mathf.Lerp(1f, 1f - share, RocketboxPaint.Smooth(0f, 0.6f, s));
                var legs = 1f - wp;
                var sl = RocketboxPaint.Smooth(0.45f, -0.45f, Mathf.Sin(th));
                var calf = 0.5f * RocketboxPaint.Smooth(f.kneeY + 0.05f, f.kneeY - 0.20f, y);
                return Top4(new[] { pel, lt, rt, lc, rc }, new[] { wp, legs * sl * (1f - calf), legs * (1f - sl) * (1f - calf), legs * sl * calf, legs * (1f - sl) * calf });
            };

            // スカート。腰から垂れる形で測った輪のうち、シャツの裾の下へ SkirtTuck だけ入った輪から下だけを作る。
            // 裾に隠れる腰まわりの布は作らない（作ると、立ちの形で尻の丸みの所の三角がシャツの裾から筋になって突き抜けた）
            var jStart = 0;
            while (jStart < SkirtRings && ringY[jStart] > f.waistY - TailDrop + SkirtTuck) jStart++;
            var tris = 0;
            var grid = new int[SkirtRings + 1, SkirtSegs + 1];
            var pos = new Vector3[SkirtRings + 1, SkirtSegs + 1];
            for (var j = jStart; j <= SkirtRings; j++)
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var kk = k % SkirtSegs;
                    var th = kk / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    var r = rSkirt[j, kk];
                    pos[j, k] = new Vector3(Mathf.Sin(th) * r, ringY[j], f.zc + Mathf.Cos(th) * r);
                }
            for (var j = jStart; j <= SkirtRings; j++)
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var p = pos[j, k];
                    var du = pos[Mathf.Min(j + 1, SkirtRings), k] - pos[Mathf.Max(j - 1, jStart), k];
                    if (du.sqrMagnitude < 1e-10f) du = Vector3.down;
                    var dv = pos[j, Mathf.Min(k + 1, SkirtSegs)] - pos[j, Mathf.Max(k - 1, 0)];
                    var n = Vector3.Cross(du, dv).normalized;
                    var radial = new Vector3(p.x, 0f, p.z - f.zc);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    var th = (k % SkirtSegs) / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    var s = Mathf.Clamp01((yTop - ringY[j]) / total);
                    grid[j, k] = Add(verts, norms, uvs, weights, toLocal, p, n, new Vector2(k / (float)SkirtSegs, 0.5f * (1f - s)), legWeight(p.y, th, LegShare));
                }
            for (var j = jStart; j < SkirtRings; j++)
                for (var k = 0; k < SkirtSegs; k++)
                {
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k]); outTris.Add(grid[j + 1, k + 1]);
                    outTris.Add(grid[j, k]); outTris.Add(grid[j + 1, k + 1]); outTris.Add(grid[j, k + 1]);
                    tris += 2;
                }

            // シャツの裾。上の縁は胴の下の縁（腰の切り替え）より 1.5 cm 上で胴に重ね、腰と尻の丸みに沿って TailEase のゆとりで垂れる。
            // 裾の内にあるのは、下の縁の近くのスカートだけ（体の人の腰の面は残していない）。スカートのある高さでは、その面から TailGap 外に置く
            var tailTop = f.waistY + 0.015f;
            var tailHem = f.waistY - TailDrop;
            var tailY = new float[TailRings + 1];
            for (var j = 0; j <= TailRings; j++) tailY[j] = Mathf.Lerp(tailTop, tailHem, j / (float)TailRings);
            // 裾の輪の高さの体の太さ（腰と脚の骨に付いた型の頂点）。布は尻の谷間へ沈まずに渡るので、スカートと同じく谷を埋める
            var rHip = new float[TailRings + 1, SkirtSegs];
            foreach (var i in new HashSet<int>(templ))
            {
                if (!hip.Contains(bwts[i].boneIndex0)) continue;
                var p = bw[i];
                var r = new Vector2(p.x, p.z - f.zc).magnitude;
                if (r > 0.35f) continue;
                var k = bin(angle(p));
                for (var j = 0; j <= TailRings; j++)
                    if (Mathf.Abs(p.y - tailY[j]) < 0.02f) rHip[j, k] = Mathf.Max(rHip[j, k], r);
            }
            for (var j = 0; j <= TailRings; j++)
            {
                var wide = new float[SkirtSegs];
                for (var k = 0; k < SkirtSegs; k++)
                {
                    var m = 0f;
                    for (var d = -3; d <= 3; d++) m = Mathf.Max(m, rHip[j, (k + d + SkirtSegs) % SkirtSegs]);
                    wide[k] = m;
                }
                for (var k = 0; k < SkirtSegs; k++)
                    rHip[j, k] = (wide[(k + SkirtSegs - 1) % SkirtSegs] + wide[k] * 2f + wide[(k + 1) % SkirtSegs]) * 0.25f;
            }
            var skirtTopY = ringY[jStart];
            var tgrid = new int[TailRings + 1, SkirtSegs + 1];
            var tpos = new Vector3[TailRings + 1, SkirtSegs + 1];
            for (var j = 0; j <= TailRings; j++)
            {
                var y = tailY[j];
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var kk = k % SkirtSegs;
                    var th = kk / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    var hug = rTop[kk] + 0.002f;
                    var r = hug;
                    if (j > 0)
                    {
                        // 腰と尻に沿う
                        if (rHip[j, kk] > 0f) r = Mathf.Max(r, rHip[j, kk] + TailEase);
                        // スカートのある高さ（上の縁の一つ上の輪の所から）は、近い輪のスカートの大きい方より TailGap 外。
                        // スカートの面は輪のあいだを線でつなぐので、裾の輪の間隔と揃わない所で顔を出さないよう三つの輪を見る
                        if (y < skirtTopY + (ringY[0] - ringY[1]))
                        {
                            var sj = Mathf.Clamp((yTop - y) / total * SkirtRings, jStart, SkirtRings);
                            var j0 = Mathf.FloorToInt(sj);
                            var j1 = Mathf.Min(SkirtRings, j0 + 1);
                            var under = Mathf.Max(rSkirt[j0, kk], Mathf.Max(rSkirt[j1, kk], rSkirt[Mathf.Min(SkirtRings, j1 + 1), kk]));
                            r = Mathf.Max(r, under + TailGap);
                        }
                        r += 0.002f * Mathf.Sin(7f * th) * (j / (float)TailRings);
                    }
                    tpos[j, k] = new Vector3(Mathf.Sin(th) * r, y, f.zc + Mathf.Cos(th) * r);
                }
            }
            // 輪の上下でならす（尻の丸みの上と下で段にならないように）。上の縁と下の縁の輪は動かさない
            for (var pass = 0; pass < 2; pass++)
                for (var j = 1; j < TailRings; j++)
                    for (var k = 0; k <= SkirtSegs; k++)
                    {
                        var pa = tpos[j - 1, k];
                        var pb = tpos[j, k];
                        var pc = tpos[j + 1, k];
                        var rA = new Vector2(pa.x, pa.z - f.zc).magnitude;
                        var rB = new Vector2(pb.x, pb.z - f.zc).magnitude;
                        var rC = new Vector2(pc.x, pc.z - f.zc).magnitude;
                        // 外へだけ寄せる（縮めるとスカートや体へ近づく）
                        var want = Mathf.Max(rB, (rA + rB * 2f + rC) * 0.25f);
                        var flat = new Vector2(pb.x, pb.z - f.zc).normalized * want;
                        tpos[j, k] = new Vector3(flat.x, pb.y, f.zc + flat.y);
                    }
            for (var j = 0; j <= TailRings; j++)
                for (var k = 0; k <= SkirtSegs; k++)
                {
                    var p = tpos[j, k];
                    var du = tpos[Mathf.Min(j + 1, TailRings), k] - tpos[Mathf.Max(j - 1, 0), k];
                    if (du.sqrMagnitude < 1e-10f) du = Vector3.down;
                    var dv = tpos[j, Mathf.Min(k + 1, SkirtSegs)] - tpos[j, Mathf.Max(k - 1, 0)];
                    var n = Vector3.Cross(du, dv).normalized;
                    var radial = new Vector3(p.x, 0f, p.z - f.zc);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    var th = (k % SkirtSegs) / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    // UV は輪の閉じ目（k = SkirtSegs）を 1 の側に置く（0 へ戻すと、最後の列の三角が絵を横切る）
                    var thU = k / (float)SkirtSegs * 2f * Mathf.PI - Mathf.PI;
                    // 下のスカートと同じ骨の付き方にする。違えると、立ちの形でも脚につれて動くスカートが、裾の内から筋になって突き抜けた
                    var w = legWeight(p.y, th, LegShare);
                    // 上の縁だけは胴の一番近い頂点の重み（背骨と腰）へ寄せ、胴と一緒に曲がる
                    var v = j / (float)TailRings;
                    if (v < 0.25f)
                    {
                        var near = Nearest(bodPos, p);
                        if (near >= 0) w = Mix(bodW[near], w, Mathf.Clamp01(v / 0.25f));
                    }
                    tgrid[j, k] = Add(verts, norms, uvs, weights, toLocal, p, n, ShirtUv(f, p, thU), w);
                }
            for (var j = 0; j < TailRings; j++)
                for (var k = 0; k < SkirtSegs; k++)
                {
                    outTris.Add(tgrid[j, k]); outTris.Add(tgrid[j + 1, k]); outTris.Add(tgrid[j + 1, k + 1]);
                    outTris.Add(tgrid[j, k]); outTris.Add(tgrid[j + 1, k + 1]); outTris.Add(tgrid[j, k + 1]);
                    tris += 2;
                }
            return tris;
        }

        /// <summary>シャツ（胴と裾）の UV（絵の左上）: 腰の真ん中のまわりの向きと高さ。高さはシャツの裾から首の付け根まで</summary>
        static Vector2 ShirtUv(RocketboxDress.Frame f, Vector3 p)
        {
            return ShirtUv(f, p, Mathf.Atan2(p.x, p.z - f.zc));
        }

        static Vector2 ShirtUv(RocketboxDress.Frame f, Vector3 p, float th)
        {
            var v = Mathf.InverseLerp(f.waistY - TailDrop - 0.02f, f.neckY + 0.02f, p.y);
            // 向きの 0（真後ろ）と 1 のつなぎ目は Wrap が写しで直す。裾の筒の輪の閉じ目（k = SkirtSegs）は 1 の側に置く
            var u = (th + Mathf.PI) / (2f * Mathf.PI);
            return new Vector2(ShirtU * u, 0.5f + 0.5f * v);
        }

        const float ShirtU = 0.45f;
        const float LiningGap = 0.0008f;

        // ---- 布の絵 -------------------------------------------------------------

        /// <summary>
        /// シャツとスカートの絵を描く（n×n）。組み合わせたメッシュの服の面の組（一番後ろ）を UV に並べ、画素ごとの束ねた姿勢の位置から描く。
        /// 320×180 で潰れないよう、線は太め・明暗ははっきりめ:
        /// - 布の地: 画素ごとの細かい粒と、糸のむら、大きなむら（綿の平織り）
        /// - シャツ: 脇と肩の縫い目、襟ぐりの縁の見返し、前の短い前立てと三つのボタン（前は開けない）、裾の縫い線、袖の付け根の縫い目と袖口の折り返し
        /// - スカート: 腰のギャザーの縦の筋と陰（シャツの裾の陰）、縦のひだの陰、脇の縫い目、裾の縫い線
        /// </summary>
        public static Color[] Paint(RocketboxPerson who, int n, out string note)
        {
            var bodySmr = AssetDatabase.LoadAssetAtPath<GameObject>(who.BodyFrom.Model).GetComponentInChildren<SkinnedMeshRenderer>();
            var f = RocketboxDress.Frame.Of(bodySmr);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(who.CompositeMesh);
            if (mesh == null) throw new InvalidOperationException("組み合わせたメッシュが無い: " + who.CompositeMesh);
            var M = bodySmr.transform.localToWorldMatrix;
            var lv = mesh.vertices;
            var wv = new Vector3[lv.Length];
            for (var i = 0; i < lv.Length; i++) wv[i] = M.MultiplyPoint3x4(lv[i]);
            var s = RocketboxPaint.Surface.Of(wv, mesh.uv, mesh.GetTriangles(mesh.subMeshCount - 1), n);
            var px = new Color[n * n];
            var yTop = f.waistY + 0.012f;
            var tailHem = f.waistY - TailDrop;
            Func<Color, Vector3> lin = c => new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
            var shirt = lin(Shirt);
            var skirt = lin(Skirt);
            var k512 = 512f / n;
            var placketEnd = f.neckY - NeckDropFront - 0.11f;
            int onCount = 0;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var i = y * n + x;
                    var u = (x + 0.5f) / n;
                    var v = (y + 0.5f) / n;
                    var isSkirt = v < 0.5f;
                    var isSleeve = !isSkirt && u >= 0.5f;
                    var shade = 1f;
                    // 布の地（綿の平織り: 縦横の糸のむら）
                    shade += (Hash(x, y) - 0.5f) * 0.05f;
                    shade += (Noise(x * 0.5f * k512, y * 0.05f * k512) - 0.5f) * 0.05f;
                    shade += (Noise(x * 0.05f * k512 + 50f, y * 0.5f * k512) - 0.5f) * 0.04f;
                    shade += (Noise(x * 0.02f * k512 + 11f, y * 0.02f * k512 + 7f) - 0.5f) * 0.05f;
                    if (s.On[i])
                    {
                        onCount++;
                        var p = s.P[i];
                        var th = Mathf.Atan2(p.x, p.z - f.zc);
                        var r = new Vector2(p.x, p.z - f.zc).magnitude;
                        var front = Mathf.Cos(th);
                        if (isSkirt)
                        {
                            var sk = Mathf.Clamp01((yTop - p.y) / (yTop - HemY));
                            // 縦のひだの陰（形の波に合わせる）
                            shade *= 1f + 0.08f * Mathf.Sin(Waves * th + 0.4f) * Mathf.Pow(sk, 0.7f);
                            // 腰のギャザー（シャツの裾の下に隠れる所も描いておく）と、シャツの裾の落とす陰
                            var dT = yTop - p.y;
                            shade *= 1f - 0.08f * RocketboxPaint.Smooth(0.12f, 0f, dT) * (0.5f + 0.5f * Mathf.Sin(th * 48f));
                            shade *= 1f - 0.18f * RocketboxPaint.Smooth(0.035f, 0f, Mathf.Abs(p.y - (tailHem - 0.012f)));
                            // 脇の縫い目
                            var sideDist = Mathf.Min(Mathf.Abs(Wrap(th - Mathf.PI * 0.5f)), Mathf.Abs(Wrap(th + Mathf.PI * 0.5f))) * r;
                            shade *= 1f - 0.12f * RocketboxPaint.Smooth(0.004f, 0.001f, sideDist);
                            // 裾: 縁の陰と、2.5 cm 上の縫い線
                            var dH = p.y - HemY;
                            shade *= 1f - 0.16f * RocketboxPaint.Smooth(0.008f, 0f, dH);
                            shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.0022f, 0.0006f, Mathf.Abs(dH - 0.025f)) * Dash(th * r, 0.008f);
                        }
                        else if (!isSleeve)
                        {
                            // 脇の縫い目
                            var sideDist = Mathf.Min(Mathf.Abs(Wrap(th - Mathf.PI * 0.5f)), Mathf.Abs(Wrap(th + Mathf.PI * 0.5f))) * r;
                            shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.004f, 0.001f, sideDist);
                            // 襟ぐりの縁の見返し（縁から 1 cm の帯と縫い線）
                            var dN = Neck(f, p);
                            shade *= 1f - 0.20f * RocketboxPaint.Smooth(0.004f, 0f, dN);
                            shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.0018f, 0.0005f, Mathf.Abs(dN - 0.010f)) * Dash(Mathf.Atan2(p.x, p.z - f.neckZ) * 0.07f, 0.007f);
                            // 前の短い前立て（左右 1.1 cm の縫い線）と三つのボタン。前は開けない
                            if (front > 0.8f && p.y > placketEnd && dN > 0f)
                            {
                                var ax = Mathf.Abs(p.x);
                                shade *= 1f - 0.20f * RocketboxPaint.Smooth(0.0024f, 0.0007f, Mathf.Abs(ax - 0.011f));
                                shade *= 1f - 0.20f * RocketboxPaint.Smooth(0.003f, 0f, Mathf.Abs(p.y - placketEnd));
                                for (var b = 0; b < 3; b++)
                                {
                                    var cy = f.neckY - NeckDropFront - 0.02f - b * 0.035f;
                                    var db = new Vector2(p.x, p.y - cy).magnitude;
                                    shade *= 1f - 0.45f * RocketboxPaint.Smooth(0.0045f, 0.0034f, db) * RocketboxPaint.Smooth(0.0020f, 0.0030f, db);
                                    shade *= 1f + 0.12f * RocketboxPaint.Smooth(0.0030f, 0f, db);
                                }
                            }
                            // 肩の縫い目
                            if (Mathf.Abs(p.x) > 0.06f && p.y > f.neckY - 0.10f)
                                shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.004f, 0.001f, Mathf.Abs(p.z - (f.neckZ + 0.012f)));
                            // シャツの裾: 縁の陰と、2 cm 上の縫い線。腰の切り替えより下は、垂れた布の縦の緩いしわ
                            var dH = p.y - tailHem;
                            shade *= 1f - 0.18f * RocketboxPaint.Smooth(0.007f, 0f, dH);
                            shade *= 1f - 0.14f * RocketboxPaint.Smooth(0.0022f, 0.0006f, Mathf.Abs(dH - 0.02f)) * Dash(th * r, 0.008f);
                            if (p.y < f.waistY) shade *= 1f + 0.06f * Mathf.Sin(7f * th) * RocketboxPaint.Smooth(f.waistY, tailHem, p.y);
                        }
                        else
                        {
                            // 袖: 付け根の縫い目、袖口の折り返しの縁と、その上の縫い線
                            var sideIdx = p.x < 0f ? 0 : 1;
                            float t, perp;
                            f.Arm(p, sideIdx, out t, out perp);
                            var len = f.UpperArmLength(sideIdx);
                            var dA = (t - SleeveT0) * len;
                            var dC = (SleeveT1 - t) * len;
                            var cuffLen = SleeveCuff * (SleeveT1 - SleeveT0) * len;
                            shade *= 1f - 0.16f * RocketboxPaint.Smooth(0.006f, 0f, dA);
                            shade *= 1f - 0.18f * RocketboxPaint.Smooth(0.004f, 0f, Mathf.Abs(dC - cuffLen));
                            shade *= 1f - 0.16f * RocketboxPaint.Smooth(0.004f, 0f, dC);
                            shade *= 1f + 0.05f * RocketboxPaint.Smooth(cuffLen, 0f, dC);
                        }
                    }
                    var col = (isSkirt ? skirt : shirt) * shade;
                    px[i] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(col.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(col.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(col.z)), 1f);
                }
            note = string.Format(CultureInfo.InvariantCulture, "シャツとスカートの絵: {0}×{0}、布の画素 {1}、シャツ {2}、スカート {3}", n, onCount, Shirt, Skirt);
            return px;
        }

        /// <summary>1024 で描いて 512 へ縮めた絵</summary>
        public static Color[] PaintSmall(RocketboxPerson who, out string note)
        {
            var big = Paint(who, 1024, out note);
            var c32 = new Color32[big.Length];
            for (var i = 0; i < big.Length; i++) c32[i] = big[i];
            var small = RocketboxTextures.Downsample(c32, 1024, 1024, 512, false);
            var o = new Color[small.Length];
            for (var i = 0; i < small.Length; i++) o[i] = small[i];
            note += "（1024 で描いて 512 へ縮めた）";
            return o;
        }

        /// <summary>仕上げの段のマテリアル: 布の絵（ワンピースと同じ面の組の名 Dress）</summary>
        public static Material Textured(Texture tex)
        {
            return BuildRocketboxProtagonist.Lit("Dress", tex, 0.06f, false);
        }

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
    }
}
