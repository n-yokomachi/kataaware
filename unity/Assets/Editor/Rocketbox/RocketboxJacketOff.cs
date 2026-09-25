using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 脱いだジャケット。着ているジャケットのメッシュ（<see cref="RocketboxJacket"/>）をそのまま形を変えて作る、動かないメッシュ（テクスチャとマテリアルも同じ）。
    /// - 卓に置いた形（場面 1。椅子の右の卓の、明かりを置いていた所。無造作に脱ぎ置いた形で、天板の手前の縁から下の半分と左の袖を垂らす）: <see cref="MakeFolded"/>
    /// - コートハンガーに襟で吊った形（場面 3 で掛け、場面 5 も掛けたまま）: <see cref="MakeHung"/>
    ///
    /// どちらも、立った形の主人公に着せたジャケットを焼き付けた形（袖は脇に下りる。主人公の根から見た位置）から作る
    /// </summary>
    public static class RocketboxJacketOff
    {
        // ---- 卓に置いた形 ----
        /// <summary>寝かせた服の厚みにする、前後の縮め（1 が着た形）。身頃の前後の二枚で 9 mm ほど（革と裏地の厚みと、ふくらみ）</summary>
        public const float FoldedFlatten = 0.035f;
        /// <summary>襟（首の骨の 2〜5 cm 下から上の輪）の前後の縮め。寝かせきらず、襟が 3〜4 cm 立ち上がる</summary>
        public const float CollarKeep = 0.15f;
        /// <summary>襟返し（首の骨の 12〜18 cm 下から首まで、前の打ち合わせの側）の前の面を浮かせる量（m）。寝かせきらず、縁が少し立つ</summary>
        public const float LapelLift = 0.012f;
        /// <summary>脇の下から下の袖を、身頃の前へ浮かせる量（m）。脇の下から 4〜14 cm で浮かせきる。袖と脇の身頃が重なるので</summary>
        public const float SleeveLift = 0.006f;
        /// <summary>脇の下から下の袖を、身頃の真ん中へ寄せる縮め（首の骨からの左右の離れに掛ける）。袖が脇の身頃に少しかぶり、幅が卓に収まる</summary>
        public const float SleeveIn = 0.9f;
        /// <summary>
        /// 身頃の上に折り返す袖（本人の右）の、肘から先を回す角（度）。下を向いた前腕を、胸を斜めに横切って反対の肩の方へ向ける。
        /// もう片方の袖（本人の左）は、肘から先が天板の縁から垂れる
        /// </summary>
        public const float ForearmTurn = 120f;
        /// <summary>肘で曲がりきるまでの長さ（m）。この間で角が 0 から <see cref="ForearmTurn"/> へ移り、内の側が詰まる</summary>
        public const float ElbowBend = 0.07f;
        /// <summary>両の袖の肘の寄り皺: 高さ（m）、肘から上下への広がり（m）、間隔（m）</summary>
        public const float ElbowRipple = 0.005f, ElbowSpan = 0.07f, ElbowWave = 0.025f;
        /// <summary>天板の手前の縁の角を回る、いちばん内の重なりの曲がりの半径（m）</summary>
        public const float EdgeRadius = 0.02f;
        /// <summary>垂れた所の、下へ行くほどの広がり（いちばん下で幅が 1 + HangFlare 倍）と、外への振れ（下がった長さあたり）</summary>
        public const float HangFlare = 0.15f, HangTilt = 0.12f;
        /// <summary>垂れた所の縦の襞の深さ（いちばん下で、m）と間隔（m）</summary>
        public const float FluteDepth = 0.012f, FluteWave = 0.085f;
        /// <summary>卓の面との隙間（m）</summary>
        public const float Gap = 0.0015f;
        /// <summary>重なりの下の面を探す升目の大きさ（m）</summary>
        const float Cell = 0.004f;

        /// <summary>天板に載せた身頃の、盛り上がった折れ目とたるみ（重なりごと持ち上げ、下は浮く）</summary>
        struct Ridge
        {
            /// <summary>両端。首の骨から左右（x、本人の右が +）と、襟の上の縁から下へ（y）、m</summary>
            public Vector2 a, b;
            /// <summary>高さと、山の幅（m）</summary>
            public float height, width;

            public Ridge(float ax, float ay, float bx, float by, float height, float width)
            {
                a = new Vector2(ax, ay);
                b = new Vector2(bx, by);
                this.height = height;
                this.width = width;
            }
        }

        /// <summary>
        /// 天板の上の折れ目とたるみ。
        /// - 左の胸から右の裾へ斜めに下りる大きな折れ目。本人の左の裾の側（卓では椅子の側でメモリハブの側）を向いた斜面が、座った目へ窓の光を返す
        /// - 縁の手前の横のたるみと、胸の中ほどの横の折れ目。椅子の側を向いた斜面が、座った目へ天井の灯りを返す
        /// - 右の脇の短い折れ目と、左の胸の小さな折れ目
        /// </summary>
        static readonly Ridge[] Ridges =
        {
            new Ridge(-0.16f, 0.16f, 0.04f, 0.34f, 0.040f, 0.022f),
            new Ridge(-0.19f, 0.40f, 0.03f, 0.38f, 0.030f, 0.020f),
            new Ridge(-0.02f, 0.25f, 0.15f, 0.29f, 0.025f, 0.018f),
            new Ridge(0.08f, 0.14f, 0.18f, 0.26f, 0.022f, 0.018f),
            new Ridge(-0.12f, 0.10f, -0.02f, 0.15f, 0.015f, 0.012f),
        };

        // ---- コートハンガーに掛けた形 ----
        /// <summary>吊った服の前後の縮め。体が抜けて胸と背がぺたんと平たくなる</summary>
        public const float HungFlatten = 0.2f;
        /// <summary>吊った服の、肩から下の幅の縮め（身頃が寄る）</summary>
        public const float HungNarrow = 0.9f;
        /// <summary>肩を落とす量（m）。首の骨から 4 cm より外ほど下げ、18 cm より外は同じだけ下げる（袖も一緒に下がる）。着た肩の丸みが落ちる</summary>
        public const float ShoulderDrop = 0.035f;
        /// <summary>裾と袖口を内へ寄せる縮め（脇の下から下へ行くほど強め、いちばん下で 1 - HemIn 倍）</summary>
        public const float HemIn = 0.08f;
        /// <summary>身頃と袖の縦の垂れ皺: 深さ（m、肩の 30 cm 下で出きる）と間隔（m）</summary>
        public const float HangCrease = 0.007f, CreaseWave = 0.07f;

        public static string FoldedPath(RocketboxPerson who) { return RocketboxJacket.Folder(who) + "JacketFolded_mesh.asset"; }
        public static string HungPath(RocketboxPerson who) { return RocketboxJacket.Folder(who) + "JacketHung_mesh.asset"; }

        /// <summary>立った形に着せて焼き付けたジャケット（主人公の根から見た位置と法線）と、骨が腕か</summary>
        sealed class Baked
        {
            public Vector3[] v, n;
            public Mesh source;
            public Vector3 neck, elbowR, elbowL;
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
                b.elbowR = her.transform.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
                b.elbowL = her.transform.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.LeftLowerArm).position);
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
        /// 卓に置いた形。無造作に脱ぎ置いた形で、前を上にして寝かせ、身頃の上の半分を天板に載せて、天板の手前の縁から下の半分と左の袖を垂らす。
        /// 1. 前後に平たく潰す。襟は寝かせきらず立ち上がり（<see cref="CollarKeep"/>）、襟返しの縁は少し浮く（<see cref="LapelLift"/>）。
        ///    袖は脇の下から下を身頃の前へ少し浮かせ、真ん中へ少し寄せる
        /// 2. 重なりの下の面を平らにならす（袖の所と身頃だけの所の段を天板に寝かせる）
        /// 3. 両の袖の肘に寄り皺を付ける
        /// 4. 右の袖の肘から先を、胸を斜めに横切るように回して身頃の上へ載せる（下の面の高さに沿わせる）
        /// 5. 天板の上の身頃に、盛り上がった折れ目とたるみ（<see cref="Ridges"/>、高さ 1.5〜4 cm）を付ける
        /// 6. 襟を卓の奥へ向けて、襟から onTop だけを天板に載せ、そこから先を天板の手前の縁で丸く折り曲げて垂らす（<see cref="EdgeRadius"/>）。
        ///    垂れた所は下へ行くほど広がり、少し外へ振れ、縦の襞が付く（板のように真下へ落ちない）
        ///
        /// 座って右の卓を見下ろすと、天板に襟と襟返し・折れ目・身頃に載せた右の袖と袖口、手前の縁に垂れた身頃の下の半分と左の袖が見える。
        /// 天板の上に畳んで載せきる形（袖を背中へ回し、丈の真ん中で裾を下へ折り込む）も作ったが、座った目から天板を浅い角（20 度ほど）で見るので
        /// 薄い帯にしか映らず、上着に読めなかった。平らに寝かせて縁から垂らしただけの形は、卓に黒い布を掛けたように見えた
        ///
        /// メッシュは縁の枠（原点は天板の手前の縁の上の、幅の真ん中。上 +y、幅 x、縁から外（垂らす側）が +z、襟は -z）で書く。
        /// size に、幅・天板から垂れた裾と袖口までの深さ・縁から襟までの長さを返す
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
            var neck = b.neck;
            var sleeve = new float[v.Length];

            // 1. 前後に潰す。法線は縮めの逆で傾ける
            var p = new Vector3[v.Length];
            var q = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                var down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, armTop - v[i].y));
                sleeve[i] = Mathf.Clamp01((b.arm[i] - 0.3f) / 0.4f);
                var lift = SleeveLift * down * sleeve[i];
                var inward = Mathf.Lerp(1f, SleeveIn, down * sleeve[i]);
                var dx = v[i].x - neck.x;
                var collar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(neck.y - 0.05f, neck.y - 0.02f, v[i].y)) * (1f - sleeve[i]);
                var flat = Mathf.Lerp(FoldedFlatten, CollarKeep, collar);
                var lapel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zMid - 0.02f, zMid + 0.04f, v[i].z))
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(neck.y - 0.18f, neck.y - 0.12f, v[i].y))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(neck.y - 0.02f, neck.y + 0.01f, v[i].y)))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.14f, Mathf.Abs(dx)))) * (1f - sleeve[i]);
                p[i] = new Vector3(neck.x + dx * inward, v[i].y, zMid + (v[i].z - zMid) * flat + lift + LapelLift * lapel);
                q[i] = new Vector3(b.n[i].x / inward, b.n[i].y, b.n[i].z / flat).normalized;
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

            // 3. 両の袖の肘の寄り皺
            var sideR = Mathf.Sign(b.elbowR.x - neck.x);
            for (var i = 0; i < p.Length; i++)
            {
                if (sleeve[i] < 0.5f) continue;
                var elbow = Mathf.Sign(p[i].x - neck.x) == sideR ? b.elbowR : b.elbowL;
                var t = p[i].y - elbow.y;
                var env = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(t) / ElbowSpan);
                var wave = 2f * Mathf.PI / ElbowWave;
                p[i].z += ElbowRipple * env * (0.5f - 0.5f * Mathf.Cos(wave * t));
                var gy = ElbowRipple * env * 0.5f * wave * Mathf.Sin(wave * t);
                q[i] = new Vector3(q[i].x, q[i].y - gy * q[i].z, q[i].z).normalized;
            }

            // 4. 右の袖の肘から先を、胸を斜めに横切るように回して身頃の上へ載せる
            var ex = neck.x + (b.elbowR.x - neck.x) * SleeveIn;
            var ey = b.elbowR.y;
            var forearm = new bool[p.Length];
            var rest = new System.Collections.Generic.List<Vector3>();
            for (var i = 0; i < p.Length; i++)
            {
                forearm[i] = sleeve[i] >= 0.5f && Mathf.Sign(p[i].x - neck.x) == sideR && p[i].y < ey + 0.01f;
                if (!forearm[i]) rest.Add(new Vector3(p[i].x, p[i].y, -p[i].z));
            }
            // 下にある物のいちばん上の面（ならした上で、下の面を探すのと同じ升目で、向きを返して探す）
            var over = Under(rest.ToArray(), xLo - 0.1f, xHi + 0.1f, yLo - 0.1f, yTop + 0.1f);
            var turn = -sideR * ForearmTurn;
            for (var i = 0; i < p.Length; i++)
            {
                if (!forearm[i]) continue;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.01f, ElbowBend, ey - p[i].y));
                var r = Quaternion.AngleAxis(turn * k, Vector3.forward);
                var d = r * new Vector3(p[i].x - ex, p[i].y - ey, 0f);
                var x2 = ex + d.x;
                var y2 = ey + d.y;
                var n2 = r * new Vector3(q[i].x, q[i].y, 0f);
                // 下にある物の上へ。袖の中の高さ（ならした後の z）はそのまま重ねる
                var top = -over.At(x2, y2);
                var z2 = Mathf.Lerp(p[i].z, Mathf.Max(p[i].z, top + p[i].z + Gap), k);
                const float e = 0.004f;
                var gx = (-over.At(x2 + e, y2) + over.At(x2 - e, y2)) / (2f * e) * k;
                var gy = (-over.At(x2, y2 + e) + over.At(x2, y2 - e)) / (2f * e) * k;
                p[i] = new Vector3(x2, y2, z2);
                q[i] = new Vector3(n2.x - gx * q[i].z, n2.y - gy * q[i].z, q[i].z).normalized;
            }

            // 5. 天板の上の身頃の折れ目とたるみ。重なりごと持ち上げる
            for (var i = 0; i < p.Length; i++)
            {
                float h0 = RidgeAt(p[i].x - neck.x, yTop - p[i].y);
                if (h0 <= 0f) continue;
                const float e = 0.002f;
                var gx = (RidgeAt(p[i].x - neck.x + e, yTop - p[i].y) - RidgeAt(p[i].x - neck.x - e, yTop - p[i].y)) / (2f * e);
                var gy = (RidgeAt(p[i].x - neck.x, yTop - p[i].y - e) - RidgeAt(p[i].x - neck.x, yTop - p[i].y + e)) / (2f * e);
                p[i].z += h0;
                q[i] = new Vector3(q[i].x - gx * q[i].z, q[i].y - gy * q[i].z, q[i].z).normalized;
            }

            // 6. 天板の手前の縁から先を垂らす。軸は縁の角の、EdgeRadius だけ下（いちばん内の重なりが角を EdgeRadius で回る）
            var edge = yTop - onTop;
            var reach = Mathf.Max(0.01f, edge - yLo);
            float cx = (xLo + xHi) * 0.5f, depth = 0f;
            var outV = new Vector3[p.Length];
            var outN = new Vector3[p.Length];
            for (var i = 0; i < p.Length; i++)
            {
                // 縁の枠: 縁から外が +z、上が +y。天板の上は z = -(襟の側へ離れた量)
                float x = p[i].x - cx, z = edge - p[i].y, y = p[i].z;
                float nx = q[i].x, nz = -q[i].y, ny = q[i].z;
                if (z > 0f)
                {
                    var h = y + EdgeRadius;
                    var phi = Mathf.Min(z / h, Mathf.PI * 0.5f);
                    float s0 = Mathf.Sin(phi), c0 = Mathf.Cos(phi);
                    // 角を回る間は円、回りきった先は下へ
                    var down = Mathf.Max(0f, z - h * Mathf.PI * 0.5f);
                    var nz2 = nz * c0 + ny * s0;
                    var ny2 = -nz * s0 + ny * c0;
                    z = h * s0;
                    y = -EdgeRadius + h * c0 - down;
                    nz = nz2;
                    ny = ny2;
                    // 下へ行くほど広がり、外へ振れ、縦の襞が付く
                    var f = Mathf.Clamp01(down / reach);
                    var spread = 1f + HangFlare * f;
                    var wave = 2f * Mathf.PI / FluteWave;
                    var flute = FluteDepth * f * (0.5f - 0.5f * Mathf.Cos(wave * x));
                    var gx = FluteDepth * f * 0.5f * wave * Mathf.Sin(wave * x);
                    // 外への振れは下がるほど（y が下がるほど）増える
                    var gy = down > 0f ? -HangTilt : 0f;
                    z += HangTilt * down + flute;
                    x *= spread;
                    nx = nx / spread - gx * nz;
                    ny = ny - gy * nz;
                }
                outV[i] = new Vector3(x, y, z);
                outN[i] = new Vector3(nx, ny, nz).normalized;
                depth = Mathf.Max(depth, -y);
            }
            size = new Vector3(xHi - xLo, depth, onTop);
            var mesh = Write(outV, outN, b.source, "JacketFolded_mesh", false);
            RocketboxJacket.SaveMesh(mesh, FoldedPath(who));
            note = string.Format(CultureInfo.InvariantCulture, "卓に置いたジャケット: 幅 {0:0.000} m、天板に載せた丈 {1:0.000} m、縁から垂れた深さ {2:0.000} m、天板からいちばん高い所 {3:0.000} m",
                size.x, size.z, size.y, Highest(outV));
            return AssetDatabase.LoadAssetAtPath<Mesh>(FoldedPath(who));
        }

        /// <summary>天板の上（縁の枠で z が 0 以下）の、いちばん高い所</summary>
        static float Highest(Vector3[] v)
        {
            var top = 0f;
            foreach (var x in v)
                if (x.z <= 0f) top = Mathf.Max(top, x.y);
            return top;
        }

        /// <summary>折れ目とたるみの高さ。x は首の骨から左右、y は襟の上の縁から下へ（m）</summary>
        static float RidgeAt(float x, float y)
        {
            var h = 0f;
            var at = new Vector2(x, y);
            foreach (var r in Ridges)
            {
                var ab = r.b - r.a;
                var t = Mathf.Clamp01(Vector2.Dot(at - r.a, ab) / ab.sqrMagnitude);
                var d = (at - (r.a + ab * t)).magnitude / r.width;
                // 両端へ向かって低くなる
                var taper = Mathf.Sin(Mathf.PI * Mathf.Lerp(0.1f, 0.9f, t));
                h = Mathf.Max(h, r.height * taper * Mathf.Exp(-d * d));
            }
            return h;
        }

        /// <summary>
        /// コートハンガーのフックに襟で吊った形。体が抜けた分、前後にぺたんと平たくし（<see cref="HungFlatten"/>）、肩を落とし（<see cref="ShoulderDrop"/>）、
        /// 肩から下の身頃と袖を寄せ（<see cref="HungNarrow"/>）、裾と袖口を内へ寄せ（<see cref="HemIn"/>）、身頃と袖の中ほどに縦の垂れ皺を付ける（<see cref="HangCrease"/>）。
        /// 襟の後ろの上の縁（フックが通る所）を原点にした、主人公の根と同じ向きの枠で書く（上 +y、前 +z）
        /// </summary>
        public static Mesh MakeHung(RocketboxPerson who, out string note)
        {
            var b = Bake(who);
            var v = b.v;
            float zLo = float.MaxValue, zHi = float.MinValue, yLo = float.MaxValue;
            foreach (var x in v)
            {
                zLo = Mathf.Min(zLo, x.z);
                zHi = Mathf.Max(zHi, x.z);
                yLo = Mathf.Min(yLo, x.y);
            }
            var zMid = (zLo + zHi) * 0.5f;
            var neck = b.neck;
            var shoulder = neck.y - 0.05f;
            var armpit = shoulder - 0.12f;
            var p = new Vector3[v.Length];
            var q = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                var dx = v[i].x - neck.x;
                // 肩から下ほど寄せ（首まわりはそのまま）、裾と袖口ほど内へ
                var k = Mathf.Lerp(1f, HungNarrow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shoulder, shoulder - 0.15f, v[i].y)))
                    * (1f - HemIn * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(armpit, yLo, v[i].y)));
                // 首から外ほど肩を落とす。落とす量の左右の傾きで法線も傾ける
                var a = Mathf.InverseLerp(0.04f, 0.18f, Mathf.Abs(dx));
                var drop = ShoulderDrop * Mathf.SmoothStep(0f, 1f, a);
                var slope = a > 0f && a < 1f ? ShoulderDrop * 6f * a * (1f - a) / 0.14f * Mathf.Sign(dx) : 0f;
                var x = neck.x + dx * k;
                var z = zMid + (v[i].z - zMid) * HungFlatten;
                // 縦の垂れ皺。肩の 5 cm 下から出はじめ、30 cm 下で出きる
                var amp = HangCrease * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shoulder - 0.05f, shoulder - 0.30f, v[i].y));
                var wave = 2f * Mathf.PI / CreaseWave;
                z += amp * Mathf.Sin(wave * (x - neck.x));
                var gx = amp * wave * Mathf.Cos(wave * (x - neck.x));
                p[i] = new Vector3(x, v[i].y - drop, z);
                var n = new Vector3(b.n[i].x / k, b.n[i].y, b.n[i].z / HungFlatten);
                n = new Vector3(n.x + slope * n.y - gx * n.z, n.y, n.z);
                q[i] = n.normalized;
            }
            // フックが通る所: 襟の後ろの上の縁（首の骨より後ろ。首の骨の前後の位置も同じだけしぼませて比べる）
            var neckZ = zMid + (neck.z - zMid) * HungFlatten;
            var hook = new Vector3(neck.x, float.MinValue, float.MaxValue);
            foreach (var x in p)
                if (Mathf.Abs(x.x - neck.x) < 0.02f && x.z < neckZ) hook.y = Mathf.Max(hook.y, x.y);
            if (hook.y == float.MinValue) throw new InvalidOperationException("吊ったジャケットの襟の後ろが見つからない");
            foreach (var x in p)
                if (Mathf.Abs(x.x - neck.x) < 0.02f && x.y > hook.y - 0.01f) hook.z = Mathf.Min(hook.z, x.z);
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
