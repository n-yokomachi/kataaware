using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 脱いだジャケット。着ているジャケットのメッシュ（<see cref="RocketboxJacket"/>）をそのまま形を変えて作る、動かないメッシュ（テクスチャとマテリアルも同じ）。
    /// - 卓に置いた形（場面 1。椅子の右の卓の、明かりを置いていた所。前を上にして寝かせ、天板の手前の縁から下の半分と両の袖を垂らす）: <see cref="MakeFolded"/>
    /// - コートハンガーに襟で吊った形（場面 3 で掛け、場面 5 も掛けたまま）: <see cref="MakeHung"/>
    ///
    /// どちらも、立った形の主人公に着せたジャケットを焼き付けた形（袖は脇に下りる。主人公の根から見た位置）から作る
    /// </summary>
    public static class RocketboxJacketOff
    {
        // ---- 卓に置いた形 ----
        /// <summary>寝かせた服の厚みにする、前後の縮め（1 が着た形）。身頃の前後の二枚で 9 mm ほど（革と裏地の厚みと、ふくらみ）</summary>
        public const float FoldedFlatten = 0.035f;
        /// <summary>脇の下から下の袖を、身頃の前へ浮かせる量（m）。脇の下から 4〜14 cm で浮かせきる。袖と脇の身頃が重なるので</summary>
        public const float SleeveLift = 0.006f;
        /// <summary>脇の下から下の袖を、身頃の真ん中へ寄せる縮め（首の骨からの左右の離れに掛ける）。袖が脇の身頃に少しかぶり、幅が卓に収まる</summary>
        public const float SleeveIn = 0.9f;
        /// <summary>天板の手前の縁の角を回る、いちばん内の重なりの曲がりの半径（m）</summary>
        public const float EdgeRadius = 0.008f;
        /// <summary>卓の面との隙間（m）</summary>
        public const float Gap = 0.0015f;
        /// <summary>重なりの下の面を探す升目の大きさ（m）</summary>
        const float Cell = 0.004f;

        // ---- コートハンガーに掛けた形 ----
        /// <summary>吊った服の前後の縮め。体が抜けて胸と背がしぼむ</summary>
        public const float HungFlatten = 0.45f;
        /// <summary>吊った服の、肩から下の幅の縮め（肩は少し張ったまま、身頃が寄る）</summary>
        public const float HungNarrow = 0.9f;

        public static string FoldedPath(RocketboxPerson who) { return RocketboxJacket.Folder(who) + "JacketFolded_mesh.asset"; }
        public static string HungPath(RocketboxPerson who) { return RocketboxJacket.Folder(who) + "JacketHung_mesh.asset"; }

        /// <summary>立った形に着せて焼き付けたジャケット（主人公の根から見た位置と法線）と、骨が腕か</summary>
        sealed class Baked
        {
            public Vector3[] v, n;
            public Mesh source;
            public Vector3 neck;
            public float[] arm;
        }

        static Baked Bake(RocketboxPerson who)
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
                baked = new Mesh();
                js.BakeMesh(baked, true);
                var b = new Baked { v = baked.vertices, n = baked.normals, source = js.sharedMesh };
                for (var i = 0; i < b.v.Length; i++)
                {
                    b.v[i] = her.transform.InverseTransformPoint(js.transform.TransformPoint(b.v[i]));
                    b.n[i] = her.transform.InverseTransformDirection(js.transform.TransformDirection(b.n[i])).normalized;
                }
                b.neck = her.transform.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.Neck).position);
                var bones = js.bones;
                var isArm = new bool[bones.Length];
                for (var k = 0; k < bones.Length; k++)
                {
                    var name = bones[k].name;
                    isArm[k] = name.Contains("UpperArm") || name.Contains("Forearm") || name.Contains("Hand") || name.Contains("Finger");
                }
                var w = b.source.boneWeights;
                b.arm = new float[b.v.Length];
                for (var i = 0; i < b.v.Length; i++)
                    b.arm[i] = (isArm[w[i].boneIndex0] ? w[i].weight0 : 0f) + (isArm[w[i].boneIndex1] ? w[i].weight1 : 0f)
                        + (isArm[w[i].boneIndex2] ? w[i].weight2 : 0f) + (isArm[w[i].boneIndex3] ? w[i].weight3 : 0f);
                return b;
            }
            finally
            {
                if (baked != null) Object.DestroyImmediate(baked);
                Object.DestroyImmediate(her);
            }
        }

        /// <summary>
        /// 卓に置いた形。前を上にして寝かせ、身頃の上の半分を天板に載せて、天板の手前の縁から下の半分と両の袖を垂らす。
        /// 1. 前後に平たく潰す（袖は脇の下から下を身頃の前へ少し浮かせ、真ん中へ少し寄せる。袖と脇の身頃が重なるので）
        /// 2. 重なりの下の面を平らにならす（袖の所と身頃だけの所の段を天板に寝かせる）
        /// 3. 襟を卓の奥へ向けて、襟から onTop だけを天板に載せ、そこから先を天板の手前の縁に沿って下へ垂らす（<see cref="EdgeRadius"/> で回る）
        ///
        /// 座って右の卓を見下ろすと、天板に襟と襟返し・胸のジッパー・肩、手前の縁に垂れた身頃の下の半分（斜めのジッパー・腰のポケット・裾）と、
        /// その両脇に垂れた袖と袖口が見える。垂れた所は椅子の方を向くので、座った目にまっすぐ映る。
        /// 天板の上に畳んで載せきる形（袖を背中へ回し、丈の真ん中で裾を下へ折り込む）も作ったが、座った目から天板を浅い角（20 度ほど）で見るので
        /// 薄い帯にしか映らず、上着に読めなかった。袖を背中へ回して縁から垂らした形も、垂れた所が黒い板に見えた
        ///
        /// メッシュは縁の枠（原点は天板の手前の縁の上の、幅の真ん中。上 +y、幅 x、縁から外（垂らす側）が +z、襟は -z）で書く。
        /// size に、幅・天板から垂れた袖口までの深さ・縁から襟までの長さを返す
        /// </summary>
        public static Mesh MakeFolded(RocketboxPerson who, float onTop, out Vector3 size, out string note)
        {
            var b = Bake(who);
            var v = b.v;
            float zLo = float.MaxValue, zHi = float.MinValue, yTop = float.MinValue, yLo = float.MaxValue, armTop = float.MinValue;
            for (var i = 0; i < v.Length; i++)
            {
                zLo = Mathf.Min(zLo, v[i].z);
                zHi = Mathf.Max(zHi, v[i].z);
                yTop = Mathf.Max(yTop, v[i].y);
                yLo = Mathf.Min(yLo, v[i].y);
                if (b.arm[i] > 0.5f) armTop = Mathf.Max(armTop, v[i].y);
            }
            var zMid = (zLo + zHi) * 0.5f;

            // 1. 前後に潰す。法線は縮めの逆で傾ける
            var p = new Vector3[v.Length];
            var q = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                var down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, armTop - v[i].y));
                var lift = SleeveLift * down * Mathf.Clamp01((b.arm[i] - 0.3f) / 0.4f);
                var inward = Mathf.Lerp(1f, SleeveIn, down * Mathf.Clamp01((b.arm[i] - 0.3f) / 0.4f));
                p[i] = new Vector3(b.neck.x + (v[i].x - b.neck.x) * inward, v[i].y, zMid + (v[i].z - zMid) * FoldedFlatten + lift);
                q[i] = new Vector3(b.n[i].x / inward, b.n[i].y, b.n[i].z / FoldedFlatten).normalized;
            }

            // 2. 下の面を平らにならす。升目ごとのいちばん下を 0 に
            float xLo = float.MaxValue, xHi = float.MinValue;
            foreach (var x in p)
            {
                xLo = Mathf.Min(xLo, x.x);
                xHi = Mathf.Max(xHi, x.x);
            }
            var floor = Under(p, xLo, xHi, yLo, yTop);
            for (var i = 0; i < p.Length; i++) p[i].z = Mathf.Max(0f, p[i].z - floor.At(p[i].x, p[i].y)) + Gap;

            // 3. 天板の手前の縁から先を垂らす。軸は縁の角の、EdgeRadius だけ下（いちばん内の重なりが角を EdgeRadius で回る）
            var edge = yTop - onTop;
            float cx = (xLo + xHi) * 0.5f, depth = 0f;
            var outV = new Vector3[p.Length];
            var outN = new Vector3[p.Length];
            for (var i = 0; i < p.Length; i++)
            {
                // 縁の枠: 縁から外が +z、上が +y。天板の上は z = -(襟の側へ離れた量)
                float z = edge - p[i].y, y = p[i].z;
                float nz = -q[i].y, ny = q[i].z;
                if (z > 0f)
                {
                    var h = y + EdgeRadius;
                    var phi = Mathf.Min(z / h, Mathf.PI * 0.5f);
                    float s0 = Mathf.Sin(phi), c0 = Mathf.Cos(phi);
                    // 角を回る間は円、回りきった先はまっすぐ下へ
                    var down = Mathf.Max(0f, z - h * Mathf.PI * 0.5f);
                    var zz = h * s0;
                    var yy = -EdgeRadius + h * c0 - down;
                    var nz2 = nz * c0 + ny * s0;
                    var ny2 = -nz * s0 + ny * c0;
                    z = zz;
                    y = yy;
                    nz = nz2;
                    ny = ny2;
                }
                outV[i] = new Vector3(p[i].x - cx, y, z);
                outN[i] = new Vector3(q[i].x, ny, nz).normalized;
                depth = Mathf.Max(depth, -y);
            }
            size = new Vector3(xHi - xLo, depth, onTop);
            var mesh = Write(outV, outN, b.source, "JacketFolded_mesh", false);
            RocketboxJacket.SaveMesh(mesh, FoldedPath(who));
            note = string.Format(CultureInfo.InvariantCulture, "卓に置いたジャケット: 幅 {0:0.000} m、天板に載せた丈 {1:0.000} m、縁から垂れた深さ {2:0.000} m", size.x, size.z, size.y);
            return AssetDatabase.LoadAssetAtPath<Mesh>(FoldedPath(who));
        }

        /// <summary>
        /// コートハンガーのフックに襟で吊った形。体が抜けた分、前後にしぼませ（<see cref="HungFlatten"/>）、肩から下の身頃と袖を少し寄せる（<see cref="HungNarrow"/>）。
        /// 襟の後ろの上の縁（フックが通る所）を原点にした、主人公の根と同じ向きの枠で書く（上 +y、前 +z）
        /// </summary>
        public static Mesh MakeHung(RocketboxPerson who, out string note)
        {
            var b = Bake(who);
            var v = b.v;
            float zLo = float.MaxValue, zHi = float.MinValue, yTop = float.MinValue, yLo = float.MaxValue;
            foreach (var x in v)
            {
                zLo = Mathf.Min(zLo, x.z);
                zHi = Mathf.Max(zHi, x.z);
                yTop = Mathf.Max(yTop, x.y);
                yLo = Mathf.Min(yLo, x.y);
            }
            var zMid = (zLo + zHi) * 0.5f;
            var shoulder = b.neck.y - 0.05f;
            var p = new Vector3[v.Length];
            var q = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                // 肩から下ほど寄せる（首まわりはそのまま）
                var k = Mathf.Lerp(1f, HungNarrow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shoulder, shoulder - 0.15f, v[i].y)));
                p[i] = new Vector3(b.neck.x + (v[i].x - b.neck.x) * k, v[i].y, zMid + (v[i].z - zMid) * HungFlatten);
                q[i] = new Vector3(b.n[i].x / k, b.n[i].y, b.n[i].z / HungFlatten).normalized;
            }
            // フックが通る所: 襟の後ろの上の縁
            var hook = new Vector3(b.neck.x, float.MinValue, float.MaxValue);
            foreach (var x in p)
                if (Mathf.Abs(x.x - b.neck.x) < 0.02f && x.z < b.neck.z) hook.y = Mathf.Max(hook.y, x.y);
            foreach (var x in p)
                if (Mathf.Abs(x.x - b.neck.x) < 0.02f && x.y > hook.y - 0.01f) hook.z = Mathf.Min(hook.z, x.z);
            var outV = new Vector3[p.Length];
            for (var i = 0; i < p.Length; i++) outV[i] = p[i] - hook;
            var mesh = Write(outV, q, b.source, "JacketHung_mesh", false);
            RocketboxJacket.SaveMesh(mesh, HungPath(who));
            var bounds = AssetDatabase.LoadAssetAtPath<Mesh>(HungPath(who)).bounds;
            note = string.Format(CultureInfo.InvariantCulture, "コートハンガーに掛けたジャケット: フックから裾まで {0:0.000} m、幅 {1:0.000} m、厚み {2:0.000} m",
                -bounds.min.y, bounds.size.x, bounds.size.z);
            return AssetDatabase.LoadAssetAtPath<Mesh>(HungPath(who));
        }

        /// <summary>頂点と法線と、元のメッシュの UV と三角でメッシュにする。flip なら三角の巡りを逆にする</summary>
        static Mesh Write(Vector3[] v, Vector3[] n, Mesh source, string name, bool flip)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = v;
            mesh.normals = n;
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
            return mesh;
        }

        /// <summary>重なりのいちばん下の面の高さを、幅と丈の升目で持つ</summary>
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
        /// ならした値は削った値を越えさせない（下の面が卓へ潜らない）。ならすのは、となりの升目との段差で面が裂けないように
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
    }
}
