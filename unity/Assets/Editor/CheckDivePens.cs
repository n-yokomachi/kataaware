using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 記憶ごとの見えない囲い（<c>BuildDive.Pen</c>）の確かめ。
    ///
    /// 始めの立ち位置（鍵打ちの頭）から、主の体の太さで床を歩いて広げ（0.2 m の升目、段は 0.3 m まで上がる）、
    /// 会話の相手と板の出る人の止まる所に 1.5 m まで寄れるかを見る。歩いた升目が囲いの外へ漏れていないかも数える。
    /// 真上から見た図（囲い・歩けた床・人・始めの立ち位置）を十六枚並べて一枚に描く。
    ///
    /// 人はコライダーを持たないので、歩みを止めるのは場所の壁と床と家具と囲いだけ。
    /// 場所と記憶は <see cref="CheckDiveSky.Stage"/> で起こし、抜けるときに戻す
    /// </summary>
    public static class CheckDivePens
    {
        const float Cell = 0.1f;
        const float Radius = 0.24f;
        const float Height = 1.7f;
        const float Step = 0.3f;
        /// <summary>人の止まる所へこれだけ寄れれば、会話も板も始められる。目を留めるのに距離の上限は無い</summary>
        const float Near = 1.5f;
        const int MostNodes = 400000;

        public struct Result
        {
            public int which;
            public List<Vector3> walked;
            public List<Vector2> hull;
            public Vector3 start;
            public List<KeyValuePair<string, Vector3>> people;
            public List<KeyValuePair<Vector3, Vector3>> lines;
            public HashSet<string> partners;
            public HashSet<string> seen;
            public int leaks;
            public string report;
        }

        /// <summary>十六の記憶を全部見て、図を path に描く</summary>
        public static string All(string path)
        {
            var sb = new StringBuilder();
            var all = new List<Result>();
            for (var i = 0; i < 16; i++)
            {
                var r = Walk(i);
                all.Add(r);
                sb.AppendLine(r.report);
            }
            Sheet(all, path);
            sb.AppendLine("図 → " + path);
            return sb.ToString();
        }

        /// <summary>記憶一本を歩く。位置はどれも場所のローカル</summary>
        public static Result Walk(int which)
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            var entry = roster[which];
            var take = GameObject.Find("Dive").transform.Find("Takes/" + which);
            var tk = take.GetComponent<Take>();
            var r = new Result
            {
                which = which,
                walked = new List<Vector3>(),
                people = new List<KeyValuePair<string, Vector3>>(),
                lines = new List<KeyValuePair<Vector3, Vector3>>(),
                partners = new HashSet<string>(),
                seen = new HashSet<string>(),
            };
            var keys = tk.Keys;
            r.hull = BuildDive.PenHull(BuildDive.PenPoints(take, keys, BuildDive.Pens[which]), BuildDive.Pens[which].margin);
            foreach (var x in DiveEntry.Exchanges(entry.said)) r.partners.Add(x.partner);
            if (entry.seen != null) foreach (var s in entry.seen) r.seen.Add(s.name);

            var sb = new StringBuilder();
            using (var stage = new CheckDiveSky.Stage(entry.place, which))
            {
                var place = stage.Place;
                Physics.SyncTransforms();
                foreach (Transform who in take)
                {
                    if (who.GetComponent<PersonMotion>() == null) continue;
                    var m = who.GetComponent<Mover>();
                    var end = m != null ? m.End : who.localPosition;
                    r.people.Add(new KeyValuePair<string, Vector3>(who.name, end));
                    if (m != null) r.lines.Add(new KeyValuePair<Vector3, Vector3>(m.From, end));
                }

                // 始めの立ち位置の床
                var start = place.TransformPoint(keys[0].position);
                RaycastHit hit;
                if (Physics.Raycast(start + Vector3.up * 1.0f, Vector3.down, out hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    start.y = hit.point.y;
                r.start = place.InverseTransformPoint(start);

                var seen = new HashSet<long>();
                var queue = new Queue<Vector3>();
                queue.Enqueue(start);
                origin = start;
                seen.Add(Key(start));
                var outside = 0;
                while (queue.Count > 0 && r.walked.Count < MostNodes)
                {
                    var at = queue.Dequeue();
                    var local = place.InverseTransformPoint(at);
                    r.walked.Add(local);
                    if (!Inside(r.hull, new Vector2(local.x, local.z))) outside++;
                    for (var d = 0; d < 4; d++)
                    {
                        var nx = at.x + (d == 0 ? Cell : d == 1 ? -Cell : 0f);
                        var nz = at.z + (d == 2 ? Cell : d == 3 ? -Cell : 0f);
                        RaycastHit floor;
                        if (!Physics.Raycast(new Vector3(nx, at.y + Step + 0.2f, nz), Vector3.down, out floor, Step + 0.2f + 0.8f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        if (floor.normal.y < 0.7f) continue;
                        var next = new Vector3(nx, floor.point.y, nz);
                        var k = Key(next);
                        if (seen.Contains(k)) continue;
                        // 体の太さの筒。段を上がれる高さから上で、囲い（Ignore Raycast）も含めて何にも触れないこと。
                        // 下りでは今いる所の高さから測る。座面やベンチから降りる一歩目で、後にする座面に筒が触れて止まらないように
                        var foot = new Vector3(nx, Mathf.Max(at.y, next.y), nz);
                        if (Physics.CheckCapsule(foot + Vector3.up * (Step + Radius), next + Vector3.up * (Height - Radius), Radius,
                                ~0, QueryTriggerInteraction.Ignore)) continue;
                        seen.Add(k);
                        queue.Enqueue(next);
                    }
                }
                r.leaks = outside;

                sb.Append("記憶 " + which + " " + entry.row + " 歩けた升目 " + r.walked.Count + (r.walked.Count >= MostNodes ? "（打ち切り）" : "") + "、囲いの外 " + outside);
                var ok = outside == 0;
                foreach (var p in r.people)
                {
                    var need = r.partners.Contains(p.Key) || r.seen.Contains(p.Key);
                    if (!need) continue;
                    var best = float.MaxValue;
                    foreach (var w in r.walked)
                    {
                        if (Mathf.Abs(w.y - p.Value.y) > 1.2f) continue;
                        var dx = w.x - p.Value.x; var dz = w.z - p.Value.z;
                        best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                    var reach = best <= Near;
                    ok &= reach;
                    sb.Append(" / " + p.Key + (r.partners.Contains(p.Key) ? "（会話）" : "（板）") + " " + (best == float.MaxValue ? "届かない" : best.ToString("F2") + " m") + (reach ? "" : " ×"));
                }
                sb.Append(ok ? " → よし" : " → だめ");
            }
            r.report = sb.ToString();
            return r;
        }

        /// <summary>升目の原点。始めの立ち位置に揃える。升目の境に点が乗って、隣どうしが同じ升目に丸まらないように</summary>
        static Vector3 origin;

        static long Key(Vector3 p)
        {
            p -= origin;
            var x = (long)Mathf.RoundToInt(p.x / Cell) + 100000;
            var z = (long)Mathf.RoundToInt(p.z / Cell) + 100000;
            var y = (long)Mathf.RoundToInt(p.y / 0.25f) + 1000;
            return (x * 200000 + z) * 4000 + y;
        }

        /// <summary>左回りの凸の多角形の内か</summary>
        static bool Inside(List<Vector2> hull, Vector2 p)
        {
            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i]; var b = hull[(i + 1) % hull.Count];
                if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) < -1e-4f) return false;
            }
            return true;
        }

        // ---- 図 -----------------------------------------------------------------

        const int Tile = 320;

        static void Sheet(List<Result> all, string path)
        {
            var cols = 4;
            var rows = (all.Count + cols - 1) / cols;
            var tex = new Texture2D(Tile * cols, Tile * rows, TextureFormat.RGB24, false);
            try
            {
                var px = new Color32[tex.width * tex.height];
                for (var i = 0; i < px.Length; i++) px[i] = new Color32(24, 24, 28, 255);
                for (var n = 0; n < all.Count; n++)
                {
                    var ox = (n % cols) * Tile;
                    var oy = (rows - 1 - n / cols) * Tile;
                    Draw(px, tex.width, ox, oy, all[n]);
                }
                tex.SetPixels32(px);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static void Draw(Color32[] px, int width, int ox, int oy, Result r)
        {
            // 囲いを収める枠。1 m の余白
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var h in r.hull) { min = Vector2.Min(min, h); max = Vector2.Max(max, h); }
            min -= Vector2.one; max += Vector2.one;
            var span = Mathf.Max(max.x - min.x, max.y - min.y);
            var scale = (Tile - 8) / span;
            System.Func<float, float, Vector2Int> at = (x, z) => new Vector2Int(
                ox + 4 + Mathf.RoundToInt((x - min.x) * scale), oy + 4 + Mathf.RoundToInt((z - min.y) * scale));

            // 枠
            for (var i = 0; i < Tile; i++)
            {
                Put(px, width, ox + i, oy, new Color32(60, 60, 66, 255));
                Put(px, width, ox, oy + i, new Color32(60, 60, 66, 255));
            }
            // 歩けた床。高いほど明るい
            var cell = Mathf.Max(1, Mathf.RoundToInt(Cell * scale));
            foreach (var w in r.walked)
            {
                var c = at(w.x, w.z);
                var g = (byte)Mathf.Clamp(90 + w.y * 18f, 90, 200);
                for (var dx = -cell / 2; dx <= cell / 2; dx++)
                    for (var dy = -cell / 2; dy <= cell / 2; dy++)
                        Put(px, width, c.x + dx, c.y + dy, new Color32(g, g, g, 255));
            }
            // 動く人の線
            foreach (var l in r.lines) Line(px, width, at(l.Key.x, l.Key.z), at(l.Value.x, l.Value.z), new Color32(90, 140, 230, 255));
            // 囲い
            for (var i = 0; i < r.hull.Count; i++)
            {
                var a = r.hull[i]; var b = r.hull[(i + 1) % r.hull.Count];
                Line(px, width, at(a.x, a.y), at(b.x, b.y), new Color32(230, 60, 60, 255));
            }
            // 人。会話の相手は橙、板だけの人は青、どちらでもない人は灰
            foreach (var p in r.people)
            {
                var col = r.partners.Contains(p.Key) ? new Color32(255, 160, 40, 255)
                    : r.seen.Contains(p.Key) ? new Color32(70, 140, 255, 255) : new Color32(150, 150, 150, 255);
                Dot(px, width, at(p.Value.x, p.Value.z), 5, col);
                // 1.5 m の輪
                Ring(px, width, at(p.Value.x, p.Value.z), Near * scale, col);
            }
            // 始めの立ち位置
            Dot(px, width, at(r.start.x, r.start.z), 6, new Color32(60, 230, 90, 255));
            // 記憶の番号（一覧の番号 + 1）
            Digits(px, width, ox + 8, oy + Tile - 20, (r.which + 1).ToString(), r.leaks == 0 ? new Color32(255, 255, 255, 255) : new Color32(255, 60, 60, 255));
        }

        static void Put(Color32[] px, int width, int x, int y, Color32 c)
        {
            var height = px.Length / width;
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            px[y * width + x] = c;
        }

        static void Line(Color32[] px, int width, Vector2Int a, Vector2Int b, Color32 c)
        {
            var n = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1);
            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                Put(px, width, Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)), c);
            }
        }

        static void Dot(Color32[] px, int width, Vector2Int c, int r, Color32 col)
        {
            for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                    if (dx * dx + dy * dy <= r * r) Put(px, width, c.x + dx, c.y + dy, col);
        }

        static void Ring(Color32[] px, int width, Vector2Int c, float r, Color32 col)
        {
            var n = Mathf.Max(24, Mathf.RoundToInt(r * 6f));
            for (var i = 0; i < n; i++)
            {
                var a = i * Mathf.PI * 2f / n;
                Put(px, width, c.x + Mathf.RoundToInt(Mathf.Cos(a) * r), c.y + Mathf.RoundToInt(Mathf.Sin(a) * r), col);
            }
        }

        /// <summary>3×5 の升目の数字。3 倍に描く</summary>
        static readonly string[] Glyphs =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        static void Digits(Color32[] px, int width, int x, int y, string text, Color32 col)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var g = Glyphs[text[i] - '0'];
                for (var row = 0; row < 5; row++)
                    for (var c = 0; c < 3; c++)
                    {
                        if (g[row * 3 + c] != '1') continue;
                        for (var sx = 0; sx < 3; sx++)
                            for (var sy = 0; sy < 3; sy++)
                                Put(px, width, x + i * 12 + c * 3 + sx, y + (4 - row) * 3 + sy, col);
                    }
            }
        }
    }
}
