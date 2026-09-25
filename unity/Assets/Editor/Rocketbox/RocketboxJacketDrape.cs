using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 椅子の左の肘掛けに掛けたジャケット（場面 1 で着る前、場面 3 で脱いだ後、場面 5）。
    /// 着ているジャケットのメッシュ（<see cref="RocketboxJacket"/>）をそのまま折って掛けた形の、動かないメッシュにする（テクスチャとマテリアルも同じ）。
    ///
    /// 作り方（どれも主人公の根から見た位置で）:
    /// 1. 立った形の主人公に着せたジャケットを焼き付ける（袖は脇に下りる）
    /// 2. 前後に平たく潰す（寝かせた服の厚みにする）。袖は脇の身頃と重なるので、脇の下から下は身頃の前へ少し浮かせる
    /// 3. 背中の真ん中より少し本人の右（<see cref="FoldAt"/>）の縦の線で、本人の右の半分を背中の側へ折り返す（背中どうしが合わさり、両面とも前身頃が外を向く）。
    ///    上に来るのは本人の左の身頃（広い身頃。斜めのジッパーと胸のポケット）
    /// 4. 肘掛けの断面（内の面・上の面・外の面、角は丸める）に沿わせて掛ける。襟は内（座面の側）に短く垂れ、裾と袖口は外へ長く垂れる。
    ///    幅（本人の左右）は肘掛けの長さの向きに並べ、折り目を後ろ（背もたれの側）、本人の左の袖を前に置く。
    ///    座った主人公の左の手は、この袖の上に載る（袖は薄いので手首を少し上げるだけで済む）。
    ///    上に来る身頃の面を外へ向けるため、本人の左右は鏡に映した向きに並ぶ（三角の巡りは裏返す）。
    ///    重なりの厚みは、下に何も無い所では肘掛けまで下ろす（袖だけの所が浮かないように）。外に垂れた所にはゆるいしわを付ける
    ///
    /// メッシュは椅子から見た位置で書く（椅子の子に、置き方をそのままにして置く）
    /// </summary>
    public static class RocketboxJacketDrape
    {
        /// <summary>寝かせた服の厚みにする、前後の縮め（1 が着た形）。身頃の前後の二枚で 5 mm ほど</summary>
        public const float Flatten = 0.02f;
        /// <summary>折り返す縦の線。首の骨から本人の右へ（m）。本人の左の身頃（真ん中を越えて右へ出る）と本人の右の身頃の間の、背中だけの所</summary>
        public const float FoldAt = 0.035f;
        /// <summary>折り返しの背中の面の曲がりの半径（m）</summary>
        public const float FoldRadius = 0.004f;
        /// <summary>折った後の幅の縮め（折ると身頃がたるんで幅が詰まる）</summary>
        public const float Narrow = 0.85f;
        /// <summary>脇に下りた袖を、身頃の前へ浮かせる量（m）。脇の下から 4〜14 cm で浮かせきる</summary>
        public const float SleeveLift = 0.006f;
        /// <summary>肘掛けの面との隙間（m）</summary>
        public const float Gap = 0.0015f;
        /// <summary>肘掛けの断面の角を丸める半径（m）</summary>
        public const float Corner = 0.012f;
        /// <summary>内に垂れた襟の先を、座面の上面からどれだけ上で止めるか（m）</summary>
        public const float AboveSeat = 0.013f;
        /// <summary>外に垂れた所が、下へ 40 cm で外へ開く量（m）</summary>
        public const float Flare = 0.02f;
        /// <summary>重なりの下の面を探す升目の大きさ（m）</summary>
        const float Cell = 0.004f;

        /// <summary>肘掛けの形（椅子から見た位置、m）。掛ける先</summary>
        public struct Armrest
        {
            /// <summary>座面の側の面の x と、外の面の x（外の方が小さい。左の肘掛け）</summary>
            public float inner, outer;
            /// <summary>上の面の高さと、座面の上面の高さ</summary>
            public float top, seat;
            /// <summary>折り目（後ろの端）を置く z。ここから前へ幅を並べる</summary>
            public float back;
        }

        public static string MeshPath(RocketboxPerson who) { return RocketboxJacket.Folder(who) + "JacketDraped_mesh.asset"; }

        /// <summary>
        /// 掛けたジャケットのメッシュを作って書く（置き場の GUID は保つ）。pick には調べる対象を立てる所（上の面の座面の側の縁の前寄り、椅子から見た位置）を返す
        /// </summary>
        public static Mesh Make(RocketboxPerson who, Armrest arm, out Vector3 pick, out string note)
        {
            var her = BuildRocketboxProtagonist.Build(null, false);
            Mesh baked = null;
            try
            {
                var an = her.GetComponent<Animator>();
                BodyPoser.Stand(an);
                var garment = her.GetComponentInChildren<Garment>(true);
                if (garment == null) throw new InvalidOperationException("主人公にジャケットが無い");
                var js = garment.GetComponent<SkinnedMeshRenderer>();
                var source = js.sharedMesh;
                baked = new Mesh();
                js.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                for (var i = 0; i < v.Length; i++)
                {
                    v[i] = her.transform.InverseTransformPoint(js.transform.TransformPoint(v[i]));
                    n[i] = her.transform.InverseTransformDirection(js.transform.TransformDirection(n[i])).normalized;
                }
                var neck = her.transform.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.Neck).position);
                var mesh = Drape(v, n, source, js.bones, neck, arm, out pick, out note);
                RocketboxJacket.SaveMesh(mesh, MeshPath(who));
                return AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(who));
            }
            finally
            {
                if (baked != null) Object.DestroyImmediate(baked);
                Object.DestroyImmediate(her);
            }
        }

        /// <summary>腕の骨（上腕・前腕・手・指）に付いた重みの和</summary>
        static float ArmShare(BoneWeight w, bool[] arm)
        {
            return (arm[w.boneIndex0] ? w.weight0 : 0f) + (arm[w.boneIndex1] ? w.weight1 : 0f)
                + (arm[w.boneIndex2] ? w.weight2 : 0f) + (arm[w.boneIndex3] ? w.weight3 : 0f);
        }

        /// <summary>肘掛けの断面に沿う道の、長さ s の所の点と向き（進む向き・外への向き）。s は内の面の下の端から</summary>
        struct Path
        {
            readonly Armrest a;
            readonly float inner, arc, across;

            public Path(Armrest a)
            {
                this.a = a;
                inner = a.top - Corner - (a.seat + AboveSeat);
                arc = Mathf.PI * Corner * 0.5f;
                across = (a.inner - a.outer) - 2f * Corner;
            }

            /// <summary>外の面の頭（ここから下が外に垂れた所）</summary>
            public float OuterStart { get { return inner + 2f * arc + across; } }

            /// <summary>上の面の真ん中</summary>
            public float TopMiddle { get { return inner + arc + across * 0.5f; } }

            public void At(float s, out Vector2 p, out Vector2 along, out Vector2 outward)
            {
                if (s < inner)
                {
                    p = new Vector2(a.inner, a.seat + AboveSeat + s);
                    along = Vector2.up;
                    outward = Vector2.right;
                    return;
                }
                s -= inner;
                if (s < arc)
                {
                    var th = s / Corner;
                    outward = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                    p = new Vector2(a.inner - Corner, a.top - Corner) + outward * Corner;
                    along = new Vector2(-Mathf.Sin(th), Mathf.Cos(th));
                    return;
                }
                s -= arc;
                if (s < across)
                {
                    p = new Vector2(a.inner - Corner - s, a.top);
                    along = Vector2.left;
                    outward = Vector2.up;
                    return;
                }
                s -= across;
                if (s < arc)
                {
                    var th = Mathf.PI * 0.5f + s / Corner;
                    outward = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                    p = new Vector2(a.outer + Corner, a.top - Corner) + outward * Corner;
                    along = new Vector2(-Mathf.Sin(th), Mathf.Cos(th));
                    return;
                }
                s -= arc;
                // 外に垂れた所。下へ行くほど少し外へ開く
                var k = s / 0.4f;
                var d = 2f * Flare * s / (0.4f * 0.4f);
                along = new Vector2(-d, -1f).normalized;
                outward = new Vector2(along.y, -along.x);
                p = new Vector2(a.outer - Flare * k * k, a.top - Corner - s);
            }
        }

        /// <summary>重なりのいちばん下の面の高さ（潰した後の前後の値）を、幅と高さの升目で持つ</summary>
        sealed class Floor
        {
            readonly float[,] low;
            readonly float x0, y0;
            readonly int nx, ny;

            public Floor(float[,] low, float x0, float y0)
            {
                this.low = low;
                this.x0 = x0;
                this.y0 = y0;
                nx = low.GetLength(0);
                ny = low.GetLength(1);
            }

            public float At(float x, float y)
            {
                float fx = Mathf.Clamp((x - x0) / Cell + 1f, 0f, nx - 1.001f), fy = Mathf.Clamp((y - y0) / Cell + 1f, 0f, ny - 1.001f);
                int a = (int)fx, b = (int)fy;
                float tx = fx - a, ty = fy - b;
                return Mathf.Lerp(Mathf.Lerp(low[a, b], low[a + 1, b], tx), Mathf.Lerp(low[a, b + 1], low[a + 1, b + 1], tx), ty);
            }
        }

        /// <summary>
        /// 重なりのいちばん下の面。升目ごとのいちばん低い値を、抜けをとなりで埋め、削ってから（5 升目の中のいちばん低い値）ならす。
        /// ならした値は削った値を越えさせない（下の面が肘掛けへ潜らない）。ならすのは、となりの升目との段差で面が裂けないように
        /// </summary>
        static Floor Under(Vector3[] p, float xLo, float xHi, float yLo, float yHi)
        {
            int nx = Mathf.CeilToInt((xHi - xLo) / Cell) + 3, ny = Mathf.CeilToInt((yHi - yLo) / Cell) + 3;
            var low = new float[nx, ny];
            for (var a = 0; a < nx; a++)
                for (var b = 0; b < ny; b++) low[a, b] = float.MaxValue;
            foreach (var x in p)
            {
                int a = Mathf.Clamp(Mathf.RoundToInt((x.x - xLo) / Cell) + 1, 0, nx - 1), b = Mathf.Clamp(Mathf.RoundToInt((x.y - yLo) / Cell) + 1, 0, ny - 1);
                low[a, b] = Mathf.Min(low[a, b], x.z);
            }
            for (var pass = 0; pass < 30; pass++)
            {
                var was = (float[,])low.Clone();
                var any = false;
                for (var a = 0; a < nx; a++)
                    for (var b = 0; b < ny; b++)
                    {
                        if (was[a, b] < float.MaxValue) continue;
                        var m = Lowest(was, a, b, float.MaxValue);
                        if (m < float.MaxValue) { low[a, b] = m; any = true; }
                    }
                if (!any) break;
            }
            for (var pass = 0; pass < 2; pass++)
            {
                var was = (float[,])low.Clone();
                for (var a = 0; a < nx; a++)
                    for (var b = 0; b < ny; b++) low[a, b] = Lowest(was, a, b, was[a, b]);
            }
            var cut = (float[,])low.Clone();
            for (var pass = 0; pass < 5; pass++)
            {
                var was = (float[,])low.Clone();
                for (var a = 0; a < nx; a++)
                    for (var b = 0; b < ny; b++)
                    {
                        var sum = 0f;
                        var count = 0;
                        for (var da = -1; da <= 1; da++)
                            for (var db = -1; db <= 1; db++)
                            {
                                int a2 = a + da, b2 = b + db;
                                if (a2 < 0 || b2 < 0 || a2 >= nx || b2 >= ny) continue;
                                sum += was[a2, b2];
                                count++;
                            }
                        low[a, b] = Mathf.Min(sum / count, cut[a, b]);
                    }
            }
            return new Floor(low, xLo, yLo);
        }

        /// <summary>升目 (a, b) とそのとなり 8 つの、いちばん低い値（start から始める）</summary>
        static float Lowest(float[,] g, int a, int b, float start)
        {
            var m = start;
            for (var da = -1; da <= 1; da++)
                for (var db = -1; db <= 1; db++)
                {
                    int a2 = a + da, b2 = b + db;
                    if (a2 < 0 || b2 < 0 || a2 >= g.GetLength(0) || b2 >= g.GetLength(1)) continue;
                    m = Mathf.Min(m, g[a2, b2]);
                }
            return m;
        }

        static Mesh Drape(Vector3[] v, Vector3[] n, Mesh source, Transform[] bones, Vector3 neck, Armrest arm, out Vector3 pick, out string note)
        {
            var isArm = new bool[bones.Length];
            for (var b = 0; b < bones.Length; b++)
            {
                var name = bones[b].name;
                isArm[b] = name.Contains("UpperArm") || name.Contains("Forearm") || name.Contains("Hand") || name.Contains("Finger");
            }
            var weights = source.boneWeights;
            float zLo = float.MaxValue, zHi = float.MinValue, yTop = float.MinValue, armTop = float.MinValue;
            for (var i = 0; i < v.Length; i++)
            {
                zLo = Mathf.Min(zLo, v[i].z);
                zHi = Mathf.Max(zHi, v[i].z);
                yTop = Mathf.Max(yTop, v[i].y);
                if (ArmShare(weights[i], isArm) > 0.5f) armTop = Mathf.Max(armTop, v[i].y);
            }
            var zMid = (zLo + zHi) * 0.5f;
            var fold = neck.x + FoldAt;

            // 1. 前後に潰す。法線は縮めの逆で傾ける。袖は脇の下から下を身頃の前へ浮かせる
            var p = new Vector3[v.Length];
            var q = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                var down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, armTop - v[i].y));
                var lift = SleeveLift * down * Mathf.Clamp01((ArmShare(weights[i], isArm) - 0.3f) / 0.4f);
                p[i] = new Vector3(v[i].x, v[i].y, zMid + (v[i].z - zMid) * Flatten + lift);
                q[i] = new Vector3(n[i].x, n[i].y, n[i].z / Flatten).normalized;
            }
            var back = float.MaxValue;
            foreach (var x in p) back = Mathf.Min(back, x.z);

            // 2. 本人の右の半分を背中の側へ折り返す。折り目は背中の面の後ろの軸のまわりに曲げる
            var axis = back - FoldRadius;
            for (var i = 0; i < p.Length; i++)
            {
                var u = p[i].x - fold;
                if (u <= 0f) continue;
                var h = p[i].z - axis;
                var phi = u / FoldRadius;
                float nx = q[i].x, nz = q[i].z;
                if (phi <= Mathf.PI)
                {
                    p[i] = new Vector3(fold + h * Mathf.Sin(phi), p[i].y, axis + h * Mathf.Cos(phi));
                    q[i] = new Vector3(nx * Mathf.Cos(phi) + nz * Mathf.Sin(phi), q[i].y, -nx * Mathf.Sin(phi) + nz * Mathf.Cos(phi));
                }
                else
                {
                    p[i] = new Vector3(fold - (u - Mathf.PI * FoldRadius), p[i].y, axis - h);
                    q[i] = new Vector3(-nx, q[i].y, -nz);
                }
            }

            // 3. 幅を詰める
            float width = 0f, xLo = float.MaxValue, xHi = float.MinValue, yLo = float.MaxValue;
            for (var i = 0; i < p.Length; i++)
            {
                p[i].x = fold + (p[i].x - fold) * Narrow;
                q[i] = new Vector3(q[i].x / Narrow, q[i].y, q[i].z).normalized;
                width = Mathf.Max(width, fold - p[i].x);
                xLo = Mathf.Min(xLo, p[i].x);
                xHi = Mathf.Max(xHi, p[i].x);
                yLo = Mathf.Min(yLo, p[i].y);
            }

            // 4. 重なりのいちばん下の面（幅と高さの升目ごと）。下に何も無い所は肘掛けまで下ろす
            var under = Under(p, xLo, xHi, yLo, yTop);

            // 5. 肘掛けに掛ける。高さ（本人の上下）は断面に沿う道の長さ、厚み（前後）は道から外への離れ、幅（本人の左右）は肘掛けの長さの向き
            var path = new Path(arm);
            var outV = new Vector3[p.Length];
            var outN = new Vector3[p.Length];
            const float WaveLong = 0.085f, WaveShort = 0.037f;
            float k1 = 2f * Mathf.PI / WaveLong, k2 = 2f * Mathf.PI / WaveShort;
            // 本人の左右（折り目から本人の左へ）は、肘掛けの後ろから前へ
            var across = Vector3.back;
            for (var i = 0; i < p.Length; i++)
            {
                var s = yTop - p[i].y;
                Vector2 at, along, outward;
                path.At(s, out at, out along, out outward);
                var z = arm.back + (fold - p[i].x);
                // 外に垂れた所のゆるいしわ（肘掛けの長さの向きに波打つ）
                var hang = Mathf.Max(0f, s - path.OuterStart);
                float g1 = Mathf.Clamp01(hang / 0.25f), g2 = Mathf.Clamp01(hang / 0.35f);
                var wave = 0.007f * Mathf.Sin(z * k1 + 0.8f) * g1 + 0.0025f * Mathf.Sin(z * k2 + 2.1f) * g2;
                var slope = 0.007f * k1 * Mathf.Cos(z * k1 + 0.8f) * g1 + 0.0025f * k2 * Mathf.Cos(z * k2 + 2.1f) * g2;
                var off = Mathf.Max(0f, p[i].z - under.At(p[i].x, p[i].y)) + Gap + wave;
                outV[i] = new Vector3(at.x + outward.x * off, at.y + outward.y * off, z);
                var o3 = new Vector3(outward.x, outward.y, 0f);
                var nrm = (across * q[i].x - new Vector3(along.x, along.y, 0f) * q[i].y + o3 * q[i].z).normalized;
                outN[i] = (nrm - Vector3.forward * slope * Vector3.Dot(nrm, o3)).normalized;
            }

            // 並べ方が鏡に映した向きなら三角の巡りを逆にする（上の面で、幅・高さ・厚みの向きの組を見る）
            Vector2 ta, tl, to;
            path.At(path.TopMiddle, out ta, out tl, out to);
            var flip = Vector3.Dot(Vector3.Cross(across, -new Vector3(tl.x, tl.y, 0f)), new Vector3(to.x, to.y, 0f)) < 0f;

            var mesh = new Mesh { name = "JacketDraped_mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = outV;
            mesh.normals = outN;
            mesh.uv = source.uv;
            mesh.subMeshCount = source.subMeshCount;
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var tri = source.GetTriangles(sub);
                if (flip)
                    for (var k = 0; k < tri.Length; k += 3)
                    {
                        var t = tri[k + 1];
                        tri[k + 1] = tri[k + 2];
                        tri[k + 2] = t;
                    }
                mesh.SetTriangles(tri, sub);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            // 調べる対象は、上の面の座面の側の縁の、前寄り（座った目から肩に隠れず見える所）
            pick = new Vector3(arm.inner - 0.015f, arm.top + 0.01f, arm.back + width * 0.6f);
            var b0 = mesh.bounds;
            note = string.Format(CultureInfo.InvariantCulture,
                "肘掛けのジャケット: 頂点 {0}、幅 {1:0.000} m（肘掛けの z {2:0.000}〜{3:0.000}）、下の端 {4:0.000} m、上の端 {5:0.000} m",
                outV.Length, width, arm.back, arm.back + width, b0.min.y, b0.max.y);
            return mesh;
        }
    }
}
