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
    /// 主人公の革のライダースジャケットを一から作る（既存の服の形は使わない）。黒い革のダブルのライダースで、前は斜めのジッパーを首元まで上げて閉じる。
    /// 片割れのワンピース（<see cref="RocketboxDress"/>）と同じく、体の人（スポーツ 02）の肌と服の面を型にする。
    /// 型は組み合わせたメッシュ（<see cref="RocketboxCompose"/>）と同じく、体の人の模型の束ねた姿勢に華奢を掛けた面で、体の面と頭の面が継ぎ目なくつながる。
    /// - 胴: 型の面を、胸の形が出ないよう、横の断面ごとに外の輪郭（凸の包み）へ出し、胸の高さの断面を裾まで下ろした箱に近い形にして、少しゆとりを取る。
    ///   脇より上（肩・胸の上・背中の上）は型の面を法線の向きへ出すだけ
    /// - 袖: 腕の軸のまわりに、断面を丸めて出す。手首まで
    /// - 骨の付き方は型の点のまま（胴と袖は体の面の付き方を写す。裾の前は、下に着たタンクトップの裾と同じく腿の骨にも少し付く）
    /// 形はどれも束ねた姿勢（体の人の骨のまま）で作り、組み合わせたメッシュと同じ骨と束ねた姿勢の、別のメッシュにする。
    /// 着る・脱ぐは体に付けた <see cref="Garment"/> で切り替える
    /// </summary>
    public static class RocketboxJacket
    {
        // ---- 形の値（m、比） ----
        /// <summary>裾の高さ（腰の骨から上へ）。腰骨の上あたり</summary>
        public const float HemAbovePelvis = 0.045f;
        /// <summary>首まわりの切り口: 首の骨（首の付け根）から下への下がり（前・横・後ろ）</summary>
        public const float NeckDropFront = 0.045f, NeckDropSide = 0.010f, NeckDropBack = -0.010f;
        /// <summary>首まわりの切り口: 首の骨の軸から横へ、首の太さと向きごとのゆとり（前・横・後ろ）より外は覆う</summary>
        public const float NeckRadius = 0.052f, NeckGapFront = 0.014f, NeckGapSide = 0.003f, NeckGapBack = 0.005f;
        /// <summary>袖口: 手の骨（手首）の手前</summary>
        public const float CuffBeforeWrist = 0.010f;
        /// <summary>胴の外の輪郭から取るゆとりと、胸の高さの断面を裾へ下ろすときの細め（裾で）</summary>
        public const float EaseBox = 0.013f, BoxTaper = 0.04f;
        /// <summary>断面を角の丸い四角へ寄せる度合いと、四角の角の丸み（超楕円の次数。2 で楕円、大きいほど四角）。体に沿った細身にするので弱く</summary>
        public const float Boxiness = 0.2f, BoxPower = 3.0f;
        /// <summary>胸の高さの断面を裾へ下ろすとき、裾で後ろへ寄せる量</summary>
        public const float BoxLean = 0.02f;
        /// <summary>
        /// 上下にまっすぐにする（半径の上の包みを取る）度合いを、向きごとに（前・横・後ろ）。
        /// 前の身頃は胸から裾へまっすぐ落ち、横と後ろは体の線（腰のくびれ）に沿う
        /// </summary>
        public const float DrapeFront = 1f, DrapeSide = 0.35f, DrapeBack = 0f;
        /// <summary>箱に近い形から法線の向きへ出す形へ移る帯の、上下の幅の半分</summary>
        public const float BlendBand = 0.045f;
        /// <summary>前の胸の上で、胸の高さから箱の上の端（鎖骨の下）までに半径が下がってよい量</summary>
        public const float FrontRecede = 0.03f;
        /// <summary>前の真ん中（胸）のゆとり。横と後ろは EaseBox</summary>
        public const float EaseFront = 0.009f;
        /// <summary>後ろで箱に近い形にする上の端（脇から上へ）</summary>
        public const float BackTopAboveArmpit = 0.02f;
        /// <summary>
        /// 胴の下を升目で作る切り口の高さ: 横は脇の下（脇から下へ）、前は胸の上（箱の上の端から下へ）、後ろは脇の少し下。
        /// ここから裾までは型の面を使わず、箱の面の上の角度と高さの升目で作る
        /// </summary>
        public const float CutBelowArmpit = 0.05f, CutBelowChestTop = 0.045f, CutBelowArmpitBack = 0.03f;
        /// <summary>胴の下の升目の、高さの目安の間隔（m）。まわりは切り口の輪の頂点の数</summary>
        const float GridStep = 0.015f;
        /// <summary>裏地を付ける所（縁から、この距離まで）。縁から覗く所だけに付ける</summary>
        const float LiningReach = 0.07f;
        /// <summary>革と裏地を合わせた厚み（裾・袖口・前の縁・襟の縁に見える）</summary>
        public const float Thick = 0.0025f;
        /// <summary>襟の立ち上がりの上の縁: 首の骨からの高さ（前・横・後ろ）と、首の骨の軸からの距離（前・横・後ろ）</summary>
        public static Vector3 StandTop = new Vector3(-0.010f, 0.022f, 0.045f), StandReach = new Vector3(0.068f, 0.058f, 0.057f);
        /// <summary>立ち上がりの前の端で、上の縁を根まで下げる列の数（首まわりの切り口の輪の頂点の数）</summary>
        public static float StandTaper = 6f;
        /// <summary>立ち上がりの高さ（前の真ん中。目安の値として書き出す）</summary>
        public static float StandHeight = 0.035f;
        /// <summary>襟の折り返しと襟返しが、身頃の面から浮く量</summary>
        public static float FallLift = 0.004f;
        /// <summary>襟の折り返しの幅（首まわりの切り口から）。後ろの真ん中と、肩の上（横）</summary>
        public static float FallBack = 0.050f, FallSide = 0.045f;
        /// <summary>襟の折り返しの帯が前で終わる角度（首のまわり、前の真ん中から。度）。ここより前は襟の前の端（<see cref="CollarPoint"/>）と襟返し</summary>
        public static float FallFrontEnd = 70f;

        // ---- 前の開き（前は開けた形。オーナーの決め） ----
        // 位置はどれも、前から見た（左右の外へ、上へ）の首の骨からの距離（m）。左右で映す（本人の右と左で折れ目の高さだけ違う）
        /// <summary>首元の、襟返しの折り目の上の端（左右とも）</summary>
        public static Vector2 FoldTop = new Vector2(0.040f, -0.035f);
        /// <summary>襟返しの折り目の下の端（折れ目。ここから下が身頃の前の縁）。本人の右と左</summary>
        public static Vector2 BreakRight = new Vector2(0.062f, -0.190f), BreakLeft = new Vector2(0.066f, -0.175f);
        /// <summary>裾での前の縁の左右の外への距離。本人の右と左</summary>
        public static float HemEdgeRight = 0.055f, HemEdgeLeft = 0.072f;
        /// <summary>襟返し: 折れ目から外へ上がった先の角、角から上の縁を戻った所、刻み（ノッチ）の奥</summary>
        public static Vector2 LapelCorner = new Vector2(0.130f, -0.102f), LapelTop = new Vector2(0.121f, -0.084f), Notch = new Vector2(0.098f, -0.070f);
        /// <summary>襟の前の端: 刻みの奥から外へ上がった襟の先、襟の外の縁の肩の方、首の横</summary>
        public static Vector2 CollarPoint = new Vector2(0.137f, -0.047f), CollarOuter = new Vector2(0.120f, 0.005f), CollarNeck = new Vector2(0.075f, 0.035f);

        // ---- UV の置き場（テクスチャの中の四角） ----
        static readonly Rect TorsoUv = new Rect(0f, 0f, 1f, 0.5f);
        static readonly Rect[] SleeveUv = { new Rect(0f, 0.505f, 0.5f, 0.26f), new Rect(0.5f, 0.505f, 0.5f, 0.26f) };
        static readonly Rect FallUv = new Rect(0f, 0.775f, 0.62f, 0.155f);
        static readonly Rect StandUv = new Rect(0.625f, 0.775f, 0.375f, 0.065f);
        /// <summary>帯（ベルト・肩章・ベルト通し）と、ジッパーの布の帯。どちらもテクスチャの幅いっぱいで、長さの向き（u）には繰り返す</summary>
        static readonly Rect StrapBand = new Rect(0f, 0.935f, 1f, 0.04f), TapeBand = new Rect(0f, 0.98f, 1f, 0.02f);

        // ---- 飾りの値（m） ----
        /// <summary>裾のベルトを付けるか（参考画像の形では付けない）</summary>
        public static bool Belt = false;
        /// <summary>裾のベルト: 裾からの高さ（真ん中）、幅、身頃からの浮き。尾錠（バックル）の角度（前の真ん中から、体の右へ正。度）と大きさ</summary>
        public static float BeltAbove = 0.022f, BeltWidth = 0.040f, BeltLift = 0.003f, BuckleAngle = -30f, BuckleHalf = 0.026f;
        /// <summary>肩章: 幅、肩の先から首の方への長さの比（肩の骨から首の骨まで）、身頃からの浮き</summary>
        public static float EpauletWidth = 0.032f, EpauletReach = 0.45f, EpauletLift = 0.003f;
        /// <summary>ジッパーの布の幅と、務歯（歯）の幅と一組の間隔</summary>
        public static float TapeWidth = 0.013f, TeethWidth = 0.0056f, TeethPitch = 0.0028f;
        /// <summary>前の縁のジッパー（開けた形では片側ずつ）: 布の幅、務歯の幅、縁から務歯の真ん中までと布の真ん中まで（身頃の側へ）</summary>
        public static float EdgeTapeWidth = 0.008f, EdgeTeethWidth = 0.0034f, EdgeTeethIn = 0.0022f, EdgeTapeIn = 0.0045f;
        /// <summary>袖口のジッパーの長さと、袖の腕の軸のまわりの向き（外へ、後ろへ）</summary>
        public static float CuffZipLength = 0.09f, CuffZipBack = 0.6f;
        /// <summary>胸のポケットのジッパー（本人の右の胸の斜め）: 前から見た上の端 (x, y) と下の端 (z, w)、首の骨から</summary>
        public static Vector4 ChestZip = new Vector4(0.138f, -0.128f, 0.102f, -0.212f);
        /// <summary>腰の横のポケットのジッパー（左右）: 前から見た左右の外（上の端）、裾からの下の端と上の端、下の端の左右の外</summary>
        public static Vector4 SideZip = new Vector4(0.128f, 0.060f, 0.165f, 0.121f);
        /// <summary>スナップの半径と高さ</summary>
        public static float StudRadius = 0.0055f, StudHeight = 0.0028f;
        /// <summary>脇より上（肩・胸の上・背中の上）で、型の面から法線の向きへ出す量</summary>
        public const float EaseTop = 0.012f;
        /// <summary>袖のゆとり（上腕と袖口）と、袖の断面を丸める度合い</summary>
        public const float EaseUpper = 0.018f, EaseCuff = 0.011f, SleeveRound = 0.5f;
        /// <summary>ならした後も、型の面からこれより内へは入れない</summary>
        public const float MinGap = 0.004f;
        /// <summary>袖付け（脇の前後の折れ目）で、型の面から離す量</summary>
        public const float ArmholeGap = 0.012f;
        /// <summary>脇の高さ（上腕の骨の付け根から下へ）。これより下を箱に近い形にする</summary>
        public const float ArmpitBelowShoulder = 0.095f;
        /// <summary>前と後ろで箱に近い形にする上の端（首の骨から下へ）</summary>
        public const float ChestTopBelowNeck = 0.07f;
        const float SliceStep = 0.01f;
        const int SliceAngles = 72;
        const int SmoothPasses = 12;

        /// <summary>
        /// ジャケットのメッシュ・テクスチャ・マテリアルの置き場。人の置き場（Assets/Models/rocketbox/ の下）には置かない
        /// （そこのテクスチャは取り込みの設定（<see cref="RocketboxImport"/>）で 512 以下・色のテクスチャ・繰り返し無しにされ、法線のテクスチャとジッパーの歯が繰り返しにならない）
        /// </summary>
        public static string Folder(RocketboxPerson who) { return "Assets/Models/generated/" + who.Name + "_Jacket/"; }
        public static string MeshPath(RocketboxPerson who) { return Folder(who) + "Jacket_mesh.asset"; }
        public static string LeatherPath(RocketboxPerson who) { return Folder(who) + "Leather.mat"; }
        public static string MetalPath(RocketboxPerson who) { return Folder(who) + "Metal.mat"; }
        public static string LiningPath(RocketboxPerson who) { return Folder(who) + "Lining.mat"; }
        public static string TeethPath(RocketboxPerson who) { return Folder(who) + "ZipperTeeth.mat"; }

        [MenuItem("HalfAware/Rocketbox/Make the protagonist's jacket")]
        static void Menu()
        {
            Debug.Log(Make(BuildRocketboxProtagonist.Chosen));
        }

        // ---- 骨 ----------------------------------------------------------------

        /// <summary>束ねた姿勢の骨の位置（華奢で動かした骨は動いた先）</summary>
        sealed class Rig
        {
            public readonly Dictionary<string, int> index = new Dictionary<string, int>();
            public readonly Dictionary<string, Vector3> at = new Dictionary<string, Vector3>();
            public readonly HashSet<int>[] arm = { new HashSet<int>(), new HashSet<int>() };
            public readonly HashSet<int> legs = new HashSet<int>();
            public int pelvis, head;

            public Rig(RocketboxPerson who, SkinnedMeshRenderer smr)
            {
                var moves = who.Slim ? RocketboxCompose.SlimBones(who, smr) : new Dictionary<string, Vector3>();
                var bones = smr.bones;
                for (var i = 0; i < bones.Length; i++)
                {
                    var n = bones[i].name;
                    index[n] = i;
                    Vector3 d;
                    at[n] = bones[i].position + (moves.TryGetValue(n, out d) ? d : Vector3.zero);
                    var side = n.Contains(" L ") ? 0 : n.Contains(" R ") ? 1 : -1;
                    if (side >= 0 && (n.EndsWith("UpperArm") || n.EndsWith("Forearm") || n.EndsWith("Hand") || n.Contains("Finger"))) arm[side].Add(i);
                    if (n.EndsWith("Thigh") || n.EndsWith("Calf") || n.EndsWith("Foot") || n.Contains("Toe")) legs.Add(i);
                }
                pelvis = index["Bip01 Pelvis"];
                head = index["Bip01 Head"];
            }

            public Vector3 this[string name] { get { return at[name]; } }
            public Vector3 Neck { get { return at["Bip01 Neck"]; } }
            public Vector3 Shoulder(int side) { return at[side == 0 ? "Bip01 L UpperArm" : "Bip01 R UpperArm"]; }
            public Vector3 Elbow(int side) { return at[side == 0 ? "Bip01 L Forearm" : "Bip01 R Forearm"]; }
            public Vector3 Wrist(int side) { return at[side == 0 ? "Bip01 L Hand" : "Bip01 R Hand"]; }
            public float HemY { get { return at["Bip01 Pelvis"].y + HemAbovePelvis; } }
            public float ArmpitY { get { return (Shoulder(0).y + Shoulder(1).y) * 0.5f - ArmpitBelowShoulder; } }
            /// <summary>前と後ろで箱に近い形にする上の端（首の付け根の下、鎖骨の下）</summary>
            public float ChestTopY { get { return Neck.y - ChestTopBelowNeck; } }

            /// <summary>腕の骨（上腕・前腕・手・指）に付いた重みの和。左右の大きい方と、その側</summary>
            public float Armness(BoneWeight w, out int side)
            {
                var a0 = Share(w, arm[0]);
                var a1 = Share(w, arm[1]);
                side = a0 >= a1 ? 0 : 1;
                return Mathf.Max(a0, a1);
            }

            public float HandShare(BoneWeight w, int side)
            {
                var s = 0f;
                foreach (var i in arm[side])
                {
                    var name = NameOf(i);
                    if (!name.EndsWith("Hand") && !name.Contains("Finger")) continue;
                    s += WeightOf(w, i);
                }
                return s;
            }

            string NameOf(int i)
            {
                foreach (var kv in index) if (kv.Value == i) return kv.Key;
                return "";
            }
        }

        static float WeightOf(BoneWeight w, int bone)
        {
            var s = 0f;
            if (w.boneIndex0 == bone) s += w.weight0;
            if (w.boneIndex1 == bone) s += w.weight1;
            if (w.boneIndex2 == bone) s += w.weight2;
            if (w.boneIndex3 == bone) s += w.weight3;
            return s;
        }

        static float Share(BoneWeight w, HashSet<int> set)
        {
            var s = 0f;
            if (set.Contains(w.boneIndex0)) s += w.weight0;
            if (set.Contains(w.boneIndex1)) s += w.weight1;
            if (set.Contains(w.boneIndex2)) s += w.weight2;
            if (set.Contains(w.boneIndex3)) s += w.weight3;
            return s;
        }

        // ---- 面 ----------------------------------------------------------------

        /// <summary>三角の網。位置（束ねた姿勢の世界）と骨の重み</summary>
        sealed class Net
        {
            public readonly List<Vector3> p = new List<Vector3>();
            public readonly List<BoneWeight> w = new List<BoneWeight>();
            public readonly List<int> t = new List<int>();

            public int Add(Vector3 pos, BoneWeight weight)
            {
                p.Add(pos);
                w.Add(weight);
                return p.Count - 1;
            }

            /// <summary>頂点ごとの法線（三角の面積で重みを付けた和）</summary>
            public Vector3[] Normals(IList<Vector3> at)
            {
                var n = new Vector3[at.Count];
                for (var k = 0; k < t.Count; k += 3)
                {
                    int a = t[k], b = t[k + 1], c = t[k + 2];
                    var f = Vector3.Cross(at[b] - at[a], at[c] - at[a]);
                    n[a] += f;
                    n[b] += f;
                    n[c] += f;
                }
                for (var i = 0; i < n.Length; i++) n[i] = n[i].sqrMagnitude > 1e-20f ? n[i].normalized : Vector3.up;
                return n;
            }

            /// <summary>となりの頂点（辺でつながる頂点）</summary>
            public List<int>[] Neighbours()
            {
                var nb = new List<int>[p.Count];
                for (var i = 0; i < nb.Length; i++) nb[i] = new List<int>();
                for (var k = 0; k < t.Count; k += 3)
                    for (var e = 0; e < 3; e++)
                    {
                        int a = t[k + e], b = t[k + (e + 1) % 3];
                        if (!nb[a].Contains(b)) nb[a].Add(b);
                        if (!nb[b].Contains(a)) nb[b].Add(a);
                    }
                return nb;
            }

            /// <summary>縁の辺（一つの三角にしか使われない辺）に載る頂点と、縁の上のとなり</summary>
            public Dictionary<int, List<int>> Rim()
            {
                var count = new Dictionary<long, int>();
                for (var k = 0; k < t.Count; k += 3)
                    for (var e = 0; e < 3; e++)
                    {
                        var key = EdgeKey(t[k + e], t[k + (e + 1) % 3]);
                        int c;
                        count.TryGetValue(key, out c);
                        count[key] = c + 1;
                    }
                var rim = new Dictionary<int, List<int>>();
                foreach (var kv in count)
                {
                    if (kv.Value != 1) continue;
                    var a = (int)(kv.Key >> 32);
                    var b = (int)(kv.Key & 0xffffffff);
                    List<int> l;
                    if (!rim.TryGetValue(a, out l)) rim[a] = l = new List<int>();
                    l.Add(b);
                    if (!rim.TryGetValue(b, out l)) rim[b] = l = new List<int>();
                    l.Add(a);
                }
                return rim;
            }
        }

        static long EdgeKey(int a, int b)
        {
            return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        }

        /// <summary>
        /// 同じ位置の頂点（UV の継ぎ目で分かれた物と、体の面と頭の面の継ぎ目）を一つにした網。同じ位置かどうかは華奢を掛ける前の位置 raw で見る
        /// （継ぎ目で分かれた頂点は骨の重みがわずかに違うことがあり、華奢を掛けた後では少しずれて、身頃に裂け目ができた）
        /// </summary>
        static Net Weld(Vector3[] raw, Vector3[] pos, BoneWeight[] weights, int[] tris)
        {
            var net = new Net();
            var byKey = new Dictionary<long, int>();
            var map = new Dictionary<int, int>();
            foreach (var i in tris)
            {
                if (map.ContainsKey(i)) continue;
                var key = PosKey(raw[i]);
                int o;
                if (!byKey.TryGetValue(key, out o)) byKey[key] = o = net.Add(pos[i], weights[i]);
                map[i] = o;
            }
            var all = new List<int>();
            for (var k = 0; k < tris.Length; k += 3)
            {
                int a = map[tris[k]], b = map[tris[k + 1]], c = map[tris[k + 2]];
                if (a == b || b == c || c == a) continue;
                all.Add(a);
                all.Add(b);
                all.Add(c);
            }
            // 一番大きいつながりだけを残す（左の手首の腕時計の帯は体と離れた面で、袖の中に縁の輪と重なった面を作った）
            var root = new int[net.p.Count];
            for (var i = 0; i < root.Length; i++) root[i] = i;
            Func<int, int> find = null;
            find = i => { while (root[i] != i) { root[i] = root[root[i]]; i = root[i]; } return i; };
            for (var k = 0; k < all.Count; k += 3)
            {
                int ra = find(all[k]), rb = find(all[k + 1]), rc = find(all[k + 2]);
                root[rb] = ra;
                root[find(rc)] = ra;
            }
            var size = new Dictionary<int, int>();
            for (var i = 0; i < root.Length; i++)
            {
                var ri = find(i);
                int c;
                size.TryGetValue(ri, out c);
                size[ri] = c + 1;
            }
            var biggest = -1;
            foreach (var kv in size) if (biggest < 0 || kv.Value > size[biggest]) biggest = kv.Key;
            for (var k = 0; k < all.Count; k += 3)
            {
                if (find(all[k]) != biggest) continue;
                net.t.Add(all[k]);
                net.t.Add(all[k + 1]);
                net.t.Add(all[k + 2]);
            }
            return net;
        }

        static long PosKey(Vector3 p)
        {
            const long half = 1L << 20;
            return ((Mathf.RoundToInt(p.x * 20000f) + half) << 42) | ((Mathf.RoundToInt(p.y * 20000f) + half) << 21) | (Mathf.RoundToInt(p.z * 20000f) + half);
        }

        /// <summary>三角を四つに分ける（辺の真ん中に頂点を置く。重みは両端の半分ずつ）</summary>
        static Net Subdivide(Net a)
        {
            var b = new Net();
            b.p.AddRange(a.p);
            b.w.AddRange(a.w);
            var mid = new Dictionary<long, int>();
            Func<int, int, int> m = (i, j) =>
            {
                var key = EdgeKey(i, j);
                int o;
                if (mid.TryGetValue(key, out o)) return o;
                o = b.Add((a.p[i] + a.p[j]) * 0.5f, Mix(a.w[i], a.w[j], 0.5f));
                mid[key] = o;
                return o;
            };
            for (var k = 0; k < a.t.Count; k += 3)
            {
                int i0 = a.t[k], i1 = a.t[k + 1], i2 = a.t[k + 2];
                int m01 = m(i0, i1), m12 = m(i1, i2), m20 = m(i2, i0);
                b.t.AddRange(new[] { i0, m01, m20, i1, m12, m01, i2, m20, m12, m01, m12, m20 });
            }
            return b;
        }

        /// <summary>
        /// 値 g（正で残す）の面で三角を切る。切り口の頂点は辺の上に置き、重みは両端を混ぜる。
        /// 返す網の頂点の番号は元と違う。cut には切り口に置いた頂点の番号を入れる
        /// </summary>
        static Net Clip(Net a, float[] g, HashSet<int> cut)
        {
            Dictionary<int, int> map;
            return Clip(a, g, cut, out map);
        }

        /// <summary>map には、残した元の頂点の番号から、返す網の番号への対応を入れる</summary>
        static Net Clip(Net a, float[] g, HashSet<int> cut, out Dictionary<int, int> kept)
        {
            var b = new Net();
            var map = new Dictionary<int, int>();
            kept = map;
            var edge = new Dictionary<long, int>();
            Func<int, int> keep = i =>
            {
                int o;
                if (map.TryGetValue(i, out o)) return o;
                o = b.Add(a.p[i], a.w[i]);
                map[i] = o;
                return o;
            };
            Func<int, int, int> at = (i, j) =>
            {
                var key = EdgeKey(i, j);
                int o;
                if (edge.TryGetValue(key, out o)) return o;
                var s = g[i] / (g[i] - g[j]);
                o = b.Add(Vector3.Lerp(a.p[i], a.p[j], s), Mix(a.w[i], a.w[j], s));
                cut.Add(o);
                edge[key] = o;
                return o;
            };
            for (var k = 0; k < a.t.Count; k += 3)
            {
                var ids = new[] { a.t[k], a.t[k + 1], a.t[k + 2] };
                var inside = 0;
                foreach (var i in ids) if (g[i] >= 0f) inside++;
                if (inside == 0) continue;
                if (inside == 3)
                {
                    b.t.Add(keep(ids[0]));
                    b.t.Add(keep(ids[1]));
                    b.t.Add(keep(ids[2]));
                    continue;
                }
                var poly = new List<int>();
                for (var e = 0; e < 3; e++)
                {
                    int i = ids[e], j = ids[(e + 1) % 3];
                    if (g[i] >= 0f) poly.Add(keep(i));
                    if ((g[i] >= 0f) != (g[j] >= 0f)) poly.Add(at(i, j));
                }
                for (var q = 1; q + 1 < poly.Count; q++)
                {
                    b.t.Add(poly[0]);
                    b.t.Add(poly[q]);
                    b.t.Add(poly[q + 1]);
                }
            }
            return b;
        }

        /// <summary><see cref="Clip(Net, float[], HashSet{int}, out Dictionary{int, int})"/> と同じく切り、頂点ごとの向き attr（法線）も切り口で混ぜて返す</summary>
        static Net ClipWith(Net a, float[] g, Vector3[] attr, HashSet<int> cut, out Dictionary<int, int> kept, out Vector3[] attrOut)
        {
            var b = new Net();
            var map = new Dictionary<int, int>();
            var edge = new Dictionary<long, int>();
            var outAttr = new List<Vector3>();
            kept = map;
            Func<int, int> keep = i =>
            {
                int o;
                if (map.TryGetValue(i, out o)) return o;
                o = b.Add(a.p[i], a.w[i]);
                outAttr.Add(attr[i]);
                map[i] = o;
                return o;
            };
            Func<int, int, int> at = (i, j) =>
            {
                var key = EdgeKey(i, j);
                int o;
                if (edge.TryGetValue(key, out o)) return o;
                var s = g[i] / (g[i] - g[j]);
                o = b.Add(Vector3.Lerp(a.p[i], a.p[j], s), Mix(a.w[i], a.w[j], s));
                outAttr.Add(Vector3.Lerp(attr[i], attr[j], s).normalized);
                cut.Add(o);
                edge[key] = o;
                return o;
            };
            for (var k = 0; k < a.t.Count; k += 3)
            {
                var ids = new[] { a.t[k], a.t[k + 1], a.t[k + 2] };
                var inside = 0;
                foreach (var i in ids) if (g[i] >= 0f) inside++;
                if (inside == 0) continue;
                if (inside == 3)
                {
                    b.t.Add(keep(ids[0]));
                    b.t.Add(keep(ids[1]));
                    b.t.Add(keep(ids[2]));
                    continue;
                }
                var poly = new List<int>();
                for (var e = 0; e < 3; e++)
                {
                    int i = ids[e], j = ids[(e + 1) % 3];
                    if (g[i] >= 0f) poly.Add(keep(i));
                    if ((g[i] >= 0f) != (g[j] >= 0f)) poly.Add(at(i, j));
                }
                for (var q = 1; q + 1 < poly.Count; q++)
                {
                    b.t.Add(poly[0]);
                    b.t.Add(poly[q]);
                    b.t.Add(poly[q + 1]);
                }
            }
            attrOut = outAttr.ToArray();
            return b;
        }

        /// <summary>
        /// 値 g の面のそば（g が -margin より大きい頂点を持つ三角）だけを四つに分ける。元の頂点の番号はそのまま。
        /// 分けた辺の真ん中の頂点は、法線を混ぜ、両端が mark で縁の辺なら mark を付ける（首まわりの切り口の輪を細かくしても輪のまま）
        /// </summary>
        static Net Refine(Net a, Vector3[] nrm, bool[] mark, Func<Vector3, float> g, float margin, out Vector3[] nrmOut, out bool[] markOut)
        {
            var b = new Net();
            b.p.AddRange(a.p);
            b.w.AddRange(a.w);
            var nl = new List<Vector3>(nrm);
            var ml = new List<bool>(mark);
            var gv = new float[a.p.Count];
            for (var i = 0; i < gv.Length; i++) gv[i] = g(a.p[i]);
            var count = new Dictionary<long, int>();
            for (var k = 0; k < a.t.Count; k += 3)
                for (var e = 0; e < 3; e++)
                {
                    var key = EdgeKey(a.t[k + e], a.t[k + (e + 1) % 3]);
                    int c;
                    count.TryGetValue(key, out c);
                    count[key] = c + 1;
                }
            var mid = new Dictionary<long, int>();
            Func<int, int, int> m = (i, j) =>
            {
                var key = EdgeKey(i, j);
                int o;
                if (mid.TryGetValue(key, out o)) return o;
                o = b.Add((a.p[i] + a.p[j]) * 0.5f, Mix(a.w[i], a.w[j], 0.5f));
                nl.Add((nrm[i] + nrm[j]).normalized);
                ml.Add(mark[i] && mark[j] && count[key] == 1);
                mid[key] = o;
                return o;
            };
            for (var k = 0; k < a.t.Count; k += 3)
            {
                int i0 = a.t[k], i1 = a.t[k + 1], i2 = a.t[k + 2];
                if (Mathf.Max(gv[i0], Mathf.Max(gv[i1], gv[i2])) <= -margin)
                {
                    b.t.Add(i0);
                    b.t.Add(i1);
                    b.t.Add(i2);
                    continue;
                }
                int m01 = m(i0, i1), m12 = m(i1, i2), m20 = m(i2, i0);
                b.t.AddRange(new[] { i0, m01, m20, i1, m12, m01, i2, m20, m12, m01, m12, m20 });
            }
            nrmOut = nl.ToArray();
            markOut = ml.ToArray();
            return b;
        }

        // ---- 残す所 --------------------------------------------------------------

        /// <summary>首まわりの切り口の内側（服が覆う側）が正の値（m）</summary>
        static float Neck(Rig r, Vector3 p)
        {
            var neck = r.Neck;
            var dz = p.z - neck.z;
            var c = Mathf.Cos(Mathf.Atan2(p.x, dz));
            var drop = c >= 0f ? Mathf.Lerp(NeckDropSide, NeckDropFront, c * c) : Mathf.Lerp(NeckDropSide, NeckDropBack, c * c);
            var gap = c >= 0f ? Mathf.Lerp(NeckGapSide, NeckGapFront, c * c) : Mathf.Lerp(NeckGapSide, NeckGapBack, c * c);
            var radial = new Vector2(p.x, dz).magnitude - (NeckRadius + gap);
            return Mathf.Max((neck.y - drop) - p.y, p.y < neck.y + 0.03f ? radial : -1f);
        }

        /// <summary>袖口の内側が正の値（m）。腕の側でない点は正の無限大</summary>
        static float Cuff(Rig r, Vector3 p, int side)
        {
            var e = r.Elbow(side);
            var h = r.Wrist(side);
            var axis = h - e;
            var len = axis.magnitude;
            axis /= len;
            var along = Vector3.Dot(p - e, axis);
            var perp = (p - e - axis * along).magnitude;
            if (along < 0f || perp > 0.07f) return float.PositiveInfinity;
            return (len - CuffBeforeWrist) - along;
        }

        static float Region(Rig r, Slices s, Vector3 p, BoneWeight w)
        {
            var g = Neck(r, p);
            // 胴の下は升目で作るので、型の面は切り口より上だけ
            int armSide;
            if (r.Armness(w, out armSide) < 0.5f) g = Mathf.Min(g, p.y - CutY(r, Mathf.Cos(s.Angle(p))));
            g = Mathf.Min(g, p.y - r.HemY);
            for (var side = 0; side < 2; side++)
            {
                g = Mathf.Min(g, Cuff(r, p, side));
                if (r.HandShare(w, side) > 0.5f) g = Mathf.Min(g, -0.001f);
            }
            // 頭の骨に付いた面（体の人の髪の房。スポーツ 02 は首の後ろへ結んだ髪が垂れている）は型にしない
            if (WeightOf(w, r.head) > 0.5f) g = Mathf.Min(g, -0.001f);
            return g;
        }

        // ---- 形 ----------------------------------------------------------------

        /// <summary>胴の横の断面ごとの外の輪郭（凸の包み）。真ん中と、角度ごとの真ん中からの距離</summary>
        sealed class Slices
        {
            public float y0;
            public int count;
            public Vector2[] centre;
            public float[,] radius;

            public Vector2 CentreAt(float y)
            {
                var f = Mathf.Clamp((y - y0) / SliceStep, 0f, count - 1.001f);
                var k = Mathf.FloorToInt(f);
                return Vector2.Lerp(centre[k], centre[k + 1], f - k);
            }

            /// <summary>高さ y・角度 th（前が 0、体の右へ正）の、外の輪郭から ease だけ外の点までの、真ん中からの距離</summary>
            public float Radius(float y, float th, float ease)
            {
                var f = Mathf.Clamp((y - y0) / SliceStep, 0f, count - 1.001f);
                var k = Mathf.FloorToInt(f);
                var s = f - k;
                var a = Mathf.Repeat(th / (2f * Mathf.PI) * SliceAngles, SliceAngles);
                var i = Mathf.FloorToInt(a) % SliceAngles;
                var j = (i + 1) % SliceAngles;
                var t = a - Mathf.Floor(a);
                var r0 = Mathf.Lerp(radius[k, i], radius[k, j], t);
                var r1 = Mathf.Lerp(radius[k + 1, i], radius[k + 1, j], t);
                return Mathf.Lerp(r0, r1, s) + ease;
            }

            /// <summary>高さ y・角度 th の、外の輪郭から ease だけ外の点</summary>
            public Vector3 At(float y, float th, float ease)
            {
                var c = CentreAt(y);
                var rr = Radius(y, th, ease);
                return new Vector3(c.x + Mathf.Sin(th) * rr, y, c.y + Mathf.Cos(th) * rr);
            }

            /// <summary>真ん中から見た点 p の角度</summary>
            public float Angle(Vector3 p)
            {
                var c = CentreAt(p.y);
                return Mathf.Atan2(p.x - c.x, p.z - c.y);
            }

            public Vector3 Box(Vector3 p, float ease)
            {
                var c = CentreAt(p.y);
                var d = new Vector2(p.x - c.x, p.z - c.y);
                var th = Mathf.Atan2(d.x, d.y);
                var rr = Mathf.Max(Radius(p.y, th, ease), d.magnitude + MinGap);
                return new Vector3(c.x + Mathf.Sin(th) * rr, p.y, c.y + Mathf.Cos(th) * rr);
            }
        }

        static Slices Box(Rig r, IList<Vector3> pts, IList<BoneWeight> ws, out string note)
        {
            var s = new Slices();
            s.y0 = r.HemY - 0.04f;
            var top = r.ChestTopY + 0.05f;
            s.count = Mathf.CeilToInt((top - s.y0) / SliceStep) + 1;
            s.centre = new Vector2[s.count];
            s.radius = new float[s.count, SliceAngles];
            var hulls = new List<Vector2>[s.count];
            for (var k = 0; k < s.count; k++)
            {
                var y = s.y0 + k * SliceStep;
                var ring = new List<Vector2>();
                for (var i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    if (Mathf.Abs(p.y - y) > SliceStep * 1.5f) continue;
                    int side;
                    if (r.Armness(ws[i], out side) > 0.4f) continue;
                    if (Share(ws[i], r.legs) > 0.3f) continue;
                    ring.Add(new Vector2(p.x, p.z));
                }
                hulls[k] = Hull(ring);
            }
            // 真ん中: 裾から脇までの段の、包みの点の平均の平均（どの段も同じ縦の軸）。
            // 段ごとの揺れは身頃の横縞に見え、高さの曲線を当てはめると、半径で上下にまっすぐにしても前が腹で膨らんだ
            var raw = new Vector2[s.count];
            var have = new bool[s.count];
            for (var k = 0; k < s.count; k++)
            {
                var c = Vector2.zero;
                foreach (var q in hulls[k]) c += q;
                have[k] = hulls[k].Count > 0 && s.y0 + k * SliceStep >= r.HemY - 0.005f;
                raw[k] = have[k] ? c / hulls[k].Count : Vector2.zero;
            }
            var axis = Vector2.zero;
            var used = 0;
            for (var k = 0; k < s.count; k++)
            {
                if (!have[k] || s.y0 + k * SliceStep > r.ArmpitY) continue;
                axis += raw[k];
                used++;
            }
            if (used > 0) axis /= used;
            for (var k = 0; k < s.count; k++) s.centre[k] = axis;
            for (var k = 0; k < s.count; k++)
                for (var a = 0; a < SliceAngles; a++)
                {
                    var th = a * 2f * Mathf.PI / SliceAngles;
                    s.radius[k, a] = Ray(hulls[k], s.centre[k], new Vector2(Mathf.Sin(th), Mathf.Cos(th)));
                }
            // 胸の高さ: 真ん中の前（左右 10 cm 以内）が一番前へ出た段
            var bust = 0;
            var most = float.MinValue;
            for (var k = 0; k < s.count; k++)
            {
                var y = s.y0 + k * SliceStep;
                if (y < r.ArmpitY - 0.15f || y > r.ArmpitY) continue;
                var front = s.centre[k].y + s.radius[k, 0];
                if (front > most) { most = front; bust = k; }
            }
            // 胸より下は、胸の高さの断面（前後 2 段を合わせた包み）を、胸の真ん中のまわりに裾へ向けて少し細めながら下ろし、その段の包みと合わせる。
            // 真ん中が段ごとに後ろへ下がっても、前は胸から裾へまっすぐ落ちる
            var bustPts = new List<Vector2>();
            for (var d = -2; d <= 2; d++) bustPts.AddRange(hulls[Mathf.Clamp(bust + d, 0, s.count - 1)]);
            var bustHull = Hull(bustPts);
            var bc = s.centre[bust];
            var hemK = Mathf.RoundToInt((r.HemY - s.y0) / SliceStep);
            for (var k = hemK; k < bust; k++)
            {
                var t = Mathf.Clamp01((float)(bust - k) / Mathf.Max(1, bust - hemK));
                var merged = new List<Vector2>(hulls[k]);
                foreach (var q in bustHull) merged.Add(bc + (q - bc) * (1f - BoxTaper * t) + new Vector2(0f, -BoxLean * t));
                hulls[k] = Hull(merged);
                for (var a2 = 0; a2 < SliceAngles; a2++)
                {
                    var th = a2 * 2f * Mathf.PI / SliceAngles;
                    s.radius[k, a2] = Ray(hulls[k], s.centre[k], new Vector2(Mathf.Sin(th), Mathf.Cos(th)));
                }
            }
            // 箱に近く: 断面を、包みの左右・前後の張り出しに合わせた角の丸い四角（超楕円）へ寄せる（胸の丸みを前の平らな面に紛らす）
            for (var k = 0; k < s.count; k++)
            {
                if (hulls[k].Count < 3) continue;
                float xp = 0f, xn = 0f, zp = 0f, zn = 0f;
                foreach (var q in hulls[k])
                {
                    var d = q - s.centre[k];
                    xp = Mathf.Max(xp, d.x);
                    xn = Mathf.Max(xn, -d.x);
                    zp = Mathf.Max(zp, d.y);
                    zn = Mathf.Max(zn, -d.y);
                }
                for (var a = 0; a < SliceAngles; a++)
                {
                    var th = a * 2f * Mathf.PI / SliceAngles;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    var ax = sx >= 0f ? xp : xn;
                    var az = cz >= 0f ? zp : zn;
                    if (ax <= 0f || az <= 0f) continue;
                    var box = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(sx) / ax, BoxPower) + Mathf.Pow(Mathf.Abs(cz) / az, BoxPower), 1f / BoxPower);
                    s.radius[k, a] = Mathf.Lerp(s.radius[k, a], Mathf.Max(s.radius[k, a], box), Boxiness);
                }
            }
            // 前の胸の上: 胸の高さから箱の上の端まで、半径の下がりを FrontRecede までにする（胸の上の丸みを、胸から襟元へのまっすぐな面に紛らす）。
            // 上の端より上も、その傾きのまま続ける（法線の向きへ出す所へ移る帯で窪まないように）
            for (var a = 0; a < SliceAngles; a++)
            {
                var ca = Mathf.Cos(a * 2f * Mathf.PI / SliceAngles);
                if (ca <= 0f) continue;
                var last = Mathf.Clamp(Mathf.RoundToInt((BoxTop(r, ca) + 0.02f - s.y0) / SliceStep), bust + 1, s.count - 1);
                var want = s.radius[bust, a] - FrontRecede;
                s.radius[last, a] = Mathf.Max(s.radius[last, a], Mathf.Lerp(s.radius[last, a], want, ca * ca));
                var slope = (s.radius[bust, a] - s.radius[last, a]) / Mathf.Max(1, last - bust);
                for (var k = last + 1; k < s.count; k++) s.radius[k, a] = Mathf.Max(s.radius[k, a], s.radius[last, a] - slope * (k - last));
            }
            var beforeDrape = (float[,])s.radius.Clone();
            // 上下にも凹ませない: 角度ごとに、高さに沿った半径の上の包み（胸の上の窪みと、胸から裾へのくびれを埋める）
            for (var a = 0; a < SliceAngles; a++)
            {
                // 横は脇まで、前と後ろは胸の上まで（上の段は肩の張り出しを含むので、脇の横へ持ち込まない）
                var ca = Mathf.Cos(a * 2f * Mathf.PI / SliceAngles);
                var last = Mathf.Clamp(Mathf.RoundToInt((BoxTop(r, ca) + 0.02f - s.y0) / SliceStep), 1, s.count - 1);
                var up = new List<int>();
                for (var k = hemK; k <= last; k++)
                {
                    while (up.Count >= 2)
                    {
                        int k0 = up[up.Count - 2], k1 = up[up.Count - 1];
                        // k1 が k0 と k を結ぶ線より下なら除く
                        var line = s.radius[k0, a] + (s.radius[k, a] - s.radius[k0, a]) * (k1 - k0) / (float)(k - k0);
                        if (s.radius[k1, a] <= line) up.RemoveAt(up.Count - 1);
                        else break;
                    }
                    up.Add(k);
                }
                for (var q = 0; q + 1 < up.Count; q++)
                    for (var k = up[q] + 1; k < up[q + 1]; k++)
                        s.radius[k, a] = Mathf.Max(s.radius[k, a], s.radius[up[q], a] + (s.radius[up[q + 1], a] - s.radius[up[q], a]) * (k - up[q]) / (float)(up[q + 1] - up[q]));
            }
            // 上下にまっすぐにする度合いは向きごと（前は胸から裾へまっすぐ、横と後ろは体の線に沿う）
            for (var a = 0; a < SliceAngles; a++)
            {
                var ca = Mathf.Cos(a * 2f * Mathf.PI / SliceAngles);
                var drape = ca >= 0f ? Mathf.Lerp(DrapeSide, DrapeFront, ca * ca) : Mathf.Lerp(DrapeSide, DrapeBack, ca * ca);
                for (var k = 0; k < s.count; k++) s.radius[k, a] = Mathf.Lerp(beforeDrape[k, a], s.radius[k, a], drape);
            }
            // 角度と高さでならす（上下は 5 段の幅で。外の輪郭より内へは入れない）
            for (var k = 0; k < hemK; k++)
                for (var a = 0; a < SliceAngles; a++)
                    s.radius[k, a] = s.radius[hemK, a];
            var floor = (float[,])s.radius.Clone();
            for (var pass = 0; pass < 6; pass++)
            {
                var o = (float[,])s.radius.Clone();
                for (var k = 0; k < s.count; k++)
                    for (var a = 0; a < SliceAngles; a++)
                    {
                        var sum = 0f;
                        for (var d = -1; d <= 1; d++) sum += o[k, (a + d + SliceAngles) % SliceAngles];
                        for (var d = -2; d <= 2; d++) sum += o[Mathf.Clamp(k + d, 0, s.count - 1), a];
                        s.radius[k, a] = sum / 8f;
                    }
            }
            for (var k = 0; k < s.count; k++)
                for (var a = 0; a < SliceAngles; a++)
                    s.radius[k, a] = Mathf.Max(s.radius[k, a], floor[k, a] - 0.002f);
            // 裾より下の段（腿の付け根を含む）は使わない。裾の段のまま
            for (var k = 0; k < hemK; k++)
                for (var a = 0; a < SliceAngles; a++)
                    s.radius[k, a] = s.radius[hemK, a];
            note = string.Format(CultureInfo.InvariantCulture, "胴の断面 {0} 段（{1:0.000}〜{2:0.000} m）、胸の高さ {3:0.000} m、脇 {4:0.000} m、裾 {5:0.000} m",
                s.count, s.y0, s.y0 + (s.count - 1) * SliceStep, s.y0 + bust * SliceStep, r.ArmpitY, r.HemY);
            return s;
        }

        /// <summary>二次元の点の凸の包み（反時計回り）</summary>
        static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (pts.Count < 3) return new List<Vector2>(pts);
            var h = new List<Vector2>();
            Func<Vector2, Vector2, Vector2, float> cross = (o, a, b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            foreach (var p in pts)
            {
                while (h.Count >= 2 && cross(h[h.Count - 2], h[h.Count - 1], p) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            var lower = h.Count + 1;
            for (var i = pts.Count - 2; i >= 0; i--)
            {
                var p = pts[i];
                while (h.Count >= lower && cross(h[h.Count - 2], h[h.Count - 1], p) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            h.RemoveAt(h.Count - 1);
            return h;
        }

        /// <summary>包みの中の点 c から向き d へ、包みの縁までの距離</summary>
        static float Ray(List<Vector2> hull, Vector2 c, Vector2 d)
        {
            var best = 0f;
            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Count];
                var e = b - a;
                var den = d.x * e.y - d.y * e.x;
                if (Mathf.Abs(den) < 1e-9f) continue;
                var ac = a - c;
                var t = (ac.x * e.y - ac.y * e.x) / den;
                var u = (ac.x * d.y - ac.y * d.x) / den;
                if (t > 0f && u >= -1e-4f && u <= 1f + 1e-4f) best = Mathf.Max(best, t);
            }
            return best;
        }

        /// <summary>腕の断面の太さ: 左右の腕の軸に沿って 1 cm ごとの、軸から肌までの一番遠い距離</summary>
        sealed class Arms
        {
            public readonly float[][] most = new float[2][];

            public Arms(Rig r, IList<Vector3> pts, IList<BoneWeight> ws)
            {
                for (var side = 0; side < 2; side++) most[side] = new float[80];
                for (var i = 0; i < pts.Count; i++)
                {
                    int side;
                    if (r.Armness(ws[i], out side) < 0.8f || r.HandShare(ws[i], side) > 0.2f) continue;
                    float along;
                    var q = OnArm(r, side, pts[i], out along);
                    var k = Mathf.Clamp(Mathf.RoundToInt(along / 0.01f), 0, 79);
                    most[side][k] = Mathf.Max(most[side][k], (pts[i] - q).magnitude);
                }
                // 前後 2 cm の大きい方
                for (var side = 0; side < 2; side++)
                {
                    var o = (float[])most[side].Clone();
                    for (var k = 0; k < o.Length; k++)
                        for (var d = -2; d <= 2; d++)
                            most[side][k] = Mathf.Max(most[side][k], o[Mathf.Clamp(k + d, 0, o.Length - 1)]);
                }
            }

            public float Most(int side, float along)
            {
                var f = Mathf.Clamp(along / 0.01f, 0f, 78.999f);
                var k = Mathf.FloorToInt(f);
                return Mathf.Lerp(most[side][k], most[side][k + 1], f - k);
            }
        }

        /// <summary>腕の軸（肩・肘・手首を結ぶ線）の上で一番近い点と、肩からの長さ</summary>
        static Vector3 OnArm(Rig r, int side, Vector3 p, out float along)
        {
            var s = r.Shoulder(side);
            var e = r.Elbow(side);
            var h = r.Wrist(side);
            var l0 = (e - s).magnitude;
            var t0 = Mathf.Clamp01(Vector3.Dot(p - s, e - s) / (l0 * l0));
            var q0 = s + (e - s) * t0;
            var l1 = (h - e).magnitude;
            var t1 = Mathf.Clamp01(Vector3.Dot(p - e, h - e) / (l1 * l1));
            var q1 = e + (h - e) * t1;
            if ((p - q0).sqrMagnitude <= (p - q1).sqrMagnitude)
            {
                along = t0 * l0;
                return q0;
            }
            along = l0 + t1 * l1;
            return q1;
        }

        // ---- 作る ----------------------------------------------------------------

        /// <summary>ジャケットのメッシュとマテリアルを作って書く。測った値を返す</summary>
        public static string Make(RocketboxPerson who)
        {
            var smr = Smr(who.BodyFrom.Model);
            var bm = smr.sharedMesh;
            var bodySub = Slot(smr, who.BodyFrom.BodySlot);
            var skinSub = Slot(smr, who.BodyFrom.HeadSlot);
            var toWorld = smr.transform.localToWorldMatrix;
            var toLocal = smr.transform.worldToLocalMatrix;
            var src = bm.vertices;
            var bw = new Vector3[src.Length];
            for (var i = 0; i < src.Length; i++) bw[i] = toWorld.MultiplyPoint3x4(src[i]);
            var raw = (Vector3[])bw.Clone();
            string slimNote = null;
            if (who.Slim) bw = RocketboxCompose.Slim(who, smr, bw, bm.boneWeights, out slimNote);
            var rig = new Rig(who, smr);

            var templ = new List<int>(bm.GetTriangles(bodySub));
            templ.AddRange(bm.GetTriangles(skinSub));
            var body = new RocketboxCompose.Surface(bw, templ.ToArray());
            var net = Subdivide(Weld(raw, bw, bm.boneWeights, templ.ToArray()));
            string boxNote;
            var slices = Box(rig, net.p, net.w, out boxNote);
            var g = new float[net.p.Count];
            for (var i = 0; i < g.Length; i++) g[i] = Region(rig, slices, net.p[i], net.w[i]);
            var cut = new HashSet<int>();
            var shell = Clip(net, g, cut);

            var n0 = shell.Normals(shell.p);
            var moved = new Vector3[shell.p.Count];
            var arms = new Arms(rig, net.p, net.w);
            for (var i = 0; i < moved.Length; i++) moved[i] = Place(rig, slices, arms, shell.p[i], n0[i], shell.w[i]);

            // ならす: となりの平均へ寄せる（縁の頂点は縁の上のとなりだけ）。型の面から MinGap より内へは入れない。
            // 袖付け（腕の骨と胴の骨の重みが半々の所。脇の前後の折れ目）は ArmholeGap まで離す（腕を下ろすと、脇の折れ目の肌が覗いた）
            var nb = shell.Neighbours();
            var rim = shell.Rim();
            var pushed = 0;
            var gap = new float[shell.p.Count];
            for (var i = 0; i < gap.Length; i++)
            {
                int armSide;
                var a = rig.Armness(shell.w[i], out armSide);
                gap[i] = Mathf.Lerp(MinGap, ArmholeGap, RocketboxPaint.Smooth(0.1f, 0.35f, a) * RocketboxPaint.Smooth(0.9f, 0.65f, a));
            }
            for (var pass = 0; pass < SmoothPasses; pass++)
            {
                var was = (Vector3[])moved.Clone();
                for (var i = 0; i < moved.Length; i++)
                {
                    List<int> along;
                    var list = rim.TryGetValue(i, out along) ? along : nb[i];
                    if (list.Count == 0) continue;
                    var c = Vector3.zero;
                    foreach (var j in list) c += was[j];
                    moved[i] = Vector3.Lerp(was[i], c / list.Count, 0.5f);
                }
                for (var i = 0; i < moved.Length; i++)
                {
                    Vector3 q, fn;
                    var d = body.Signed(moved[i], 0.06f, out q, out fn);
                    if (float.IsNaN(d) || d >= gap[i]) continue;
                    moved[i] = q + fn * gap[i];
                    if (pass == SmoothPasses - 1) pushed++;
                }
            }

            var disp = new Net();
            disp.p.AddRange(moved);
            disp.w.AddRange(shell.w);
            disp.t.AddRange(shell.t);
            // 胴の下: 切り口の輪から裾まで、箱の面の上の升目
            var torsoTris = new List<int>();
            foreach (var k in TorsoTriangles(rig, templ, bm.boneWeights)) torsoTris.Add(k);
            List<int> seam;
            var gridNote = Grid(rig, slices, disp, new RocketboxCompose.Surface(bw, torsoTris.ToArray()), bw, bm.boneWeights, torsoTris, out seam);
            // 型の面と升目の継ぎ目のそば（4 cm）をならす
            if (seam != null)
            {
                var near = new List<int>();
                for (var i = 0; i < disp.p.Count; i++)
                    foreach (var j in seam)
                        if ((disp.p[i] - disp.p[j]).sqrMagnitude < 0.04f * 0.04f) { near.Add(i); break; }
                var dnb = disp.Neighbours();
                var drim = disp.Rim();
                for (var pass = 0; pass < 8; pass++)
                {
                    var was = disp.p.ToArray();
                    foreach (var i in near)
                    {
                        if (drim.ContainsKey(i) || dnb[i].Count == 0) continue;
                        var c = Vector3.zero;
                        foreach (var j in dnb[i]) c += was[j];
                        var q = Vector3.Lerp(was[i], c / dnb[i].Count, 0.5f);
                        Vector3 on, fn;
                        var d = body.Signed(q, 0.06f, out on, out fn);
                        if (!float.IsNaN(d) && d < MinGap) q = on + fn * MinGap;
                        disp.p[i] = q;
                    }
                }
            }
            // 骨の重みは、ずらした後の点に一番近い型の肌の点の重みにする（手と指の面は除く）。
            // 型の点の重みのままだと、脇と肩の前で外へ大きく出した点が、腕を下ろすと腕に引かれて体へ入り、肩の前と脇に肌が覗いた
            {
                var noHands = new List<int>();
                for (var k = 0; k < templ.Count; k += 3)
                {
                    var hand = false;
                    for (var e = 0; e < 3; e++)
                    {
                        var wv = bm.boneWeights[templ[k + e]];
                        if (rig.HandShare(wv, 0) > 0.3f || rig.HandShare(wv, 1) > 0.3f || WeightOf(wv, rig.head) > 0.5f) hand = true;
                    }
                    if (hand) continue;
                    noHands.Add(templ[k]);
                    noHands.Add(templ[k + 1]);
                    noHands.Add(templ[k + 2]);
                }
                var skin = new RocketboxCompose.Surface(bw, noHands.ToArray());
                for (var i = 0; i < disp.p.Count; i++) disp.w[i] = WeightAt(skin, bw, bm.boneWeights, noHands, disp.p[i]);
            }

            // 襟の輪（首まわりの切り口）は、前を開ける前の閉じた身頃で取る
            List<int> neck = null;
            foreach (var l in Loops(disp))
                if (neck == null || Mean(disp, l) > Mean(disp, neck)) neck = l;
            // 襟と襟返しの下の身頃をならす（型の服の縁や鎖骨の細かな凹凸を拾うと、襟返しがしわくちゃの板に見えた）
            if (neck != null)
            {
                var ringPts = new List<Vector3>();
                foreach (var i in neck) ringPts.Add(disp.p[i]);
                var under = new List<int>();
                var drim2 = disp.Rim();
                for (var i = 0; i < disp.p.Count; i++)
                    if (!drim2.ContainsKey(i) && FallG(rig, ringPts, disp.p[i]) > -0.03f) under.Add(i);
                var dnb2 = disp.Neighbours();
                for (var pass = 0; pass < 30; pass++)
                {
                    var was = disp.p.ToArray();
                    foreach (var i in under)
                    {
                        if (dnb2[i].Count == 0) continue;
                        var c = Vector3.zero;
                        foreach (var j in dnb2[i]) c += was[j];
                        var q = Vector3.Lerp(was[i], c / dnb2[i].Count, 0.5f);
                        Vector3 on, fn;
                        var d = body.Signed(q, 0.06f, out on, out fn);
                        if (!float.IsNaN(d) && d < MinGap) q = on + fn * MinGap;
                        disp.p[i] = q;
                    }
                }
            }
            var shellN = disp.Normals(disp.p);
            var look = new Look { r = rig, s = slices, ring = new List<Vector3>() };
            if (neck != null) foreach (var i in neck) look.ring.Add(disp.p[i]);

            // 前を開ける: 前の開きの多角形（首元から裾まで、左右の身頃の前の縁の間）を切り落とす
            var gOpen = new float[disp.p.Count];
            for (var i = 0; i < gOpen.Length; i++) gOpen[i] = OpenG(rig, slices, disp.p[i]);
            var cutOpen = new HashSet<int>();
            Dictionary<int, int> keptOpen;
            Vector3[] frontN;
            var front = ClipWith(disp, gOpen, shellN, cutOpen, out keptOpen, out frontN);
            var openLoops = Loops(front);
            var loopNote = new StringBuilder();
            foreach (var l in openLoops)
            {
                var c = Vector3.zero;
                foreach (var i in l) c += front.p[i];
                loopNote.AppendFormat(CultureInfo.InvariantCulture, " [{0} 頂点 ({1:0.000},{2:0.000},{3:0.000})]", l.Count, c.x / l.Count, c.y / l.Count, c.z / l.Count);
            }

            var o = new Out();
            var charts = Charts(rig, front);
            var outer = Skin(o, rig, slices, front, frontN, charts);
            Lining(o, front, frontN);
            var rims = Rims(o, front, frontN, charts, outer);

            // 襟: 閉じた身頃の首まわりの輪から、立ち上がり（前の開きの間は無し）、折り返しと襟の前の端、襟返し
            string collarNote = "襟の輪が無い";
            if (neck != null) collarNote = Collar(o, rig, disp, shellN, neck);

            // 飾り: 前の縁のジッパー、胸と腰のポケットのジッパー、肩章、袖口のジッパー、スナップ
            var sh = new Surf(front, frontN);
            var detailNote = Details(o, look, sh);
            string paintNote;
            PaintLeather(who, o, look, out paintNote);

            // メッシュ（体の人のメッシュの中の位置）
            var mesh = o.Mesh("Jacket_mesh", toLocal);
            var composite = AssetDatabase.LoadAssetAtPath<Mesh>(who.CompositeMesh);
            if (composite == null) throw new InvalidOperationException("組み合わせたメッシュが無い: " + who.CompositeMesh);
            mesh.bindposes = composite.bindposes;
            SaveMesh(mesh, MeshPath(who));
            SaveMaterials(who);

            return string.Format(CultureInfo.InvariantCulture,
                "ジャケット（前を開けた形）: 頂点 {0}・三角 {1}（身頃と袖 {2}、切り口 {3} 頂点、前の開きの切り口 {13} 頂点、縁の辺 {4}、首の輪 {5} 頂点、縁の輪 {14}（首から前の縁と裾を回る一つと袖口で 3）{15}）。{6}。{10}。{7}。{11}。{12}。ならした後に型の面の外へ出した頂点 {8}\n書いた所: {9}",
                o.v.Count, o.TriangleCount, front.t.Count / 3, cut.Count, rims, neck != null ? neck.Count : 0, boxNote, collarNote, pushed, MeshPath(who), gridNote, detailNote, paintNote,
                cutOpen.Count, openLoops.Count, loopNote);
        }

        /// <summary>前の開きの多角形（前から見た、首の骨からの左右 x・上 y）。本人の右が正</summary>
        static Vector2[] OpenPoly(Rig r)
        {
            var hem = r.HemY - r.Neck.y - 0.05f;
            return new[]
            {
                new Vector2(HemEdgeRight, hem), new Vector2(BreakRight.x, BreakRight.y), new Vector2(FoldTop.x, FoldTop.y), new Vector2(FoldTop.x, 0.08f),
                new Vector2(-FoldTop.x, 0.08f), new Vector2(-FoldTop.x, FoldTop.y), new Vector2(-BreakLeft.x, BreakLeft.y), new Vector2(-HemEdgeLeft, hem),
            };
        }

        /// <summary>前の開きの外（身頃を残す側）が正の値（m）。胴の前の半分だけを見る</summary>
        static float OpenG(Rig r, Slices s, Vector3 p)
        {
            var c = s.CentreAt(p.y);
            if (p.z < c.y) return 1f;
            return -Inside(OpenPoly(r), new Vector2(p.x - r.Neck.x, p.y - r.Neck.y));
        }

        /// <summary>襟の前の端（襟の先まで）の多角形（前から見た、首の骨からの左右の外・上）</summary>
        static Vector2[] CollarFrontPoly()
        {
            return new[] { FoldTop, Notch, CollarPoint, CollarOuter, CollarNeck, new Vector2(FoldTop.x, CollarNeck.y) };
        }

        /// <summary>襟返しの多角形（前から見た、首の骨からの左右の外・上）。right は本人の右</summary>
        static Vector2[] LapelPoly(bool right)
        {
            return new[] { FoldTop, right ? BreakRight : BreakLeft, LapelCorner, LapelTop, Notch };
        }

        static Vector2 Centroid(Vector2[] poly)
        {
            var c = Vector2.zero;
            foreach (var q in poly) c += q;
            return c / poly.Length;
        }

        static float Mean(Net n, List<int> l)
        {
            var y = 0f;
            foreach (var i in l) y += n.p[i].y;
            return y / l.Count;
        }

        /// <summary>型の点 p（法線 n、重み w）のジャケットの面の位置。胴は箱に近い形へ、袖は腕の軸のまわりに丸めて出す</summary>
        static Vector3 Place(Rig r, Slices s, Arms arms, Vector3 p, Vector3 n, BoneWeight w)
        {
            int side;
            var armness = RocketboxPaint.Smooth(0.35f, 0.8f, r.Armness(w, out side));
            // 胴: 前は胸の上（鎖骨の下）まで、後ろは脇の少し上まで、横は脇までを箱に近い形にし、その上は法線の向きへ出す
            var top = p + n * EaseTop;
            var cen = s.CentreAt(p.y);
            var d = new Vector2(p.x - cen.x, p.z - cen.y);
            var cz = d.magnitude > 1e-4f ? d.y / d.magnitude : 0f;
            var boxTop = BoxTop(r, cz);
            var b = RocketboxPaint.Smooth(boxTop + BlendBand, boxTop - BlendBand, p.y);
            var torso = b > 0f ? Vector3.Lerp(top, s.Box(p, EaseAt(cz)), b) : top;
            if (armness <= 0f) return torso;
            // 袖: 腕の軸から外へ、断面を丸めてゆとりを取って出す
            float along;
            var q = OnArm(r, side, p, out along);
            var radial = p - q;
            var rr = radial.magnitude;
            var dir = rr > 1e-6f ? radial / rr : n;
            var upper = (r.Elbow(side) - r.Shoulder(side)).magnitude;
            var full = upper + (r.Wrist(side) - r.Elbow(side)).magnitude;
            var ease = Mathf.Lerp(EaseUpper, EaseCuff, RocketboxPaint.Smooth(upper * 0.9f, full, along));
            var round = Mathf.Lerp(rr, Mathf.Max(rr, arms.Most(side, along)), SleeveRound);
            var sleeve = q + dir * (round + ease);
            return Vector3.Lerp(torso, sleeve, armness);
        }

        /// <summary>胴のゆとり。cz は胴の真ん中から見た向きの前後（前 1、横 0、後ろ -1）</summary>
        static float EaseAt(float cz)
        {
            return cz > 0f ? Mathf.Lerp(EaseBox, EaseFront, cz * cz) : EaseBox;
        }

        /// <summary>胴の下を升目で作る切り口の高さ</summary>
        static float CutY(Rig r, float cz)
        {
            return cz >= 0f
                ? Mathf.Lerp(r.ArmpitY - CutBelowArmpit, r.ChestTopY - CutBelowChestTop, cz * cz)
                : Mathf.Lerp(r.ArmpitY - CutBelowArmpit, r.ArmpitY - CutBelowArmpitBack, cz * cz);
        }

        /// <summary>型の三角のうち、三つの頂点がどれも腕の骨にほとんど付いていない物（胴の下の升目の骨の重みを取る所）の、並びの中の三角の番号の頂点</summary>
        static IEnumerable<int> TorsoTriangles(Rig r, List<int> templ, BoneWeight[] w)
        {
            for (var k = 0; k < templ.Count; k += 3)
            {
                int side;
                if (r.Armness(w[templ[k]], out side) > 0.3f || r.Armness(w[templ[k + 1]], out side) > 0.3f || r.Armness(w[templ[k + 2]], out side) > 0.3f) continue;
                if (Share(w[templ[k]], r.legs) > 0.6f && Share(w[templ[k + 1]], r.legs) > 0.6f && Share(w[templ[k + 2]], r.legs) > 0.6f) continue;
                yield return templ[k];
                yield return templ[k + 1];
                yield return templ[k + 2];
            }
        }

        /// <summary>
        /// 胴の下: 型の面の切り口の輪（CutY の高さ）から裾（HemY）まで、箱の面の上に、輪の頂点ごとの列・高さ GridStep ごとの段の升目を加える。
        /// 骨の重みは、升目の点に一番近い型の胴の面の点の重み
        /// </summary>
        static string Grid(Rig r, Slices s, Net n, RocketboxCompose.Surface torso, Vector3[] bw, BoneWeight[] bwts, List<int> torsoTris, out List<int> seam)
        {
            seam = null;
            List<int> loop = null;
            foreach (var l in Loops(n))
            {
                var y = Mean(n, l);
                if (y < r.HemY || y > r.ArmpitY + 0.05f) continue;
                if (loop == null || l.Count > loop.Count) loop = l;
            }
            if (loop == null) return "胴の切り口の輪が無い";
            // 輪を角度の順に（小さい方から）
            var turn = 0f;
            for (var i = 0; i < loop.Count; i++) turn += Mathf.DeltaAngle(s.Angle(n.p[loop[i]]) * Mathf.Rad2Deg, s.Angle(n.p[loop[(i + 1) % loop.Count]]) * Mathf.Rad2Deg);
            if (turn < 0f) loop.Reverse();
            var first = 0;
            for (var i = 1; i < loop.Count; i++) if (s.Angle(n.p[loop[i]]) < s.Angle(n.p[loop[first]])) first = i;
            var ring = new List<int>();
            for (var i = 0; i < loop.Count; i++) ring.Add(loop[(first + i) % loop.Count]);
            seam = ring;
            if (ring.Count < 8) return "胴の切り口の輪が短い";

            // 輪の頂点ごとに一列: 輪を上の段にし、下の段ほど角度を等しい間隔へ寄せながら裾まで下ろす（輪と升目の間を角度の順で継がない。
            // 胸の内側で輪が角度の上で行きつ戻りつすると、継ぎに穴が開いた）
            var na = ring.Count;
            var theta = new float[na];
            theta[0] = s.Angle(n.p[ring[0]]);
            for (var i = 1; i < na; i++)
                theta[i] = theta[i - 1] + Mathf.DeltaAngle(s.Angle(n.p[ring[i - 1]]) * Mathf.Rad2Deg, s.Angle(n.p[ring[i]]) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            for (var i = 1; i < na; i++) theta[i] = Mathf.Max(theta[i], theta[i - 1] + 1e-4f);
            var span = theta[na - 1] - theta[0];
            var step = 2f * Mathf.PI / na;
            for (var i = 1; i < na; i++) theta[i] = theta[0] + (theta[i] - theta[0]) * (2f * Mathf.PI - step) / Mathf.Max(1e-4f, span);
            var tallest = 0f;
            foreach (var i in ring) tallest = Mathf.Max(tallest, n.p[i].y - r.HemY);
            var rows = Mathf.Max(2, Mathf.CeilToInt(tallest / GridStep));
            var ids = new int[rows + 1, na];
            for (var c = 0; c < na; c++)
            {
                ids[0, c] = ring[c];
                var top = n.p[ring[c]].y;
                var even = theta[0] + step * c;
                for (var j = 1; j <= rows; j++)
                {
                    var f = j / (float)rows;
                    var th = Mathf.Lerp(theta[c], even, RocketboxPaint.Smooth(0f, 0.6f, f));
                    var p = s.At(Mathf.Lerp(top, r.HemY, f), th, EaseAt(Mathf.Cos(th)));
                    ids[j, c] = n.Add(p, WeightAt(torso, bw, bwts, torsoTris, p));
                }
            }
            Action<int, int, int> tri = (a, b, c) =>
            {
                var m = (n.p[a] + n.p[b] + n.p[c]) / 3f;
                var cen = s.CentreAt(m.y);
                var outward = new Vector3(m.x - cen.x, 0f, m.z - cen.y);
                if (Vector3.Dot(Vector3.Cross(n.p[b] - n.p[a], n.p[c] - n.p[a]), outward) < 0f) { var x = b; b = c; c = x; }
                n.t.Add(a);
                n.t.Add(b);
                n.t.Add(c);
            };
            for (var j = 0; j < rows; j++)
                for (var c = 0; c < na; c++)
                {
                    var c1 = (c + 1) % na;
                    tri(ids[j, c], ids[j, c1], ids[j + 1, c1]);
                    tri(ids[j, c], ids[j + 1, c1], ids[j + 1, c]);
                }
            return string.Format(CultureInfo.InvariantCulture, "胴の下の升目: 切り口の輪 {0} 列・高さ {1} 段", na, rows);
        }

        /// <summary>点 p に一番近い型の面の点の、骨の重み（三角の中の位置で三つの頂点を混ぜる）</summary>
        static BoneWeight WeightAt(RocketboxCompose.Surface surface, Vector3[] bw, BoneWeight[] bwts, List<int> tris, Vector3 p)
        {
            Vector3 q, nrm;
            int k;
            if (float.IsInfinity(surface.Closest(p, 0.12f, out q, out nrm, out k)) || k < 0) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            Vector3 a = bw[tris[k]], b = bw[tris[k + 1]], c = bw[tris[k + 2]];
            var v0 = b - a;
            var v1 = c - a;
            var v2 = q - a;
            float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
            var den = d00 * d11 - d01 * d01;
            var wb = den > 1e-20f ? (d11 * d20 - d01 * d21) / den : 0f;
            var wc = den > 1e-20f ? (d00 * d21 - d01 * d20) / den : 0f;
            var wa = 1f - wb - wc;
            var ab = Mix(bwts[tris[k]], bwts[tris[k + 1]], Mathf.Clamp01(wb / Mathf.Max(1e-6f, wa + wb)));
            return Mix(ab, bwts[tris[k + 2]], Mathf.Clamp01(wc));
        }

        /// <summary>箱に近い形にする上の端。cz は胴の真ん中から見た向きの前後（前 1、横 0、後ろ -1）</summary>
        static float BoxTop(Rig r, float cz)
        {
            return cz >= 0f ? Mathf.Lerp(r.ArmpitY, r.ChestTopY, cz * cz) : Mathf.Lerp(r.ArmpitY, r.ArmpitY + BackTopAboveArmpit, cz * cz);
        }

        // ---- 組み立て ----------------------------------------------------------------

        /// <summary>頂点の部分: 身頃・左の袖・右の袖・襟の折り返しと襟返し・襟の立ち上がり・帯・ジッパーの布・縁（描かない）・金具・裏地</summary>
        const byte PartTorso = 0, PartSleeveL = 1, PartSleeveR = 2, PartFall = 3, PartStand = 4, PartStrap = 5, PartTape = 6, PartEdge = 7, PartMetal = 8, PartLining = 9;

        /// <summary>メッシュの組み立て場（束ねた姿勢の世界の位置）。面の組は 0 が革、1 が金具、2 が裏地、3 がジッパーの務歯</summary>
        sealed class Out
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<BoneWeight> w = new List<BoneWeight>();
            /// <summary>頂点ごとの部分（テクスチャを描き分ける）</summary>
            public readonly List<byte> tag = new List<byte>();
            public readonly List<int>[] sub = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            public int Add(Vector3 p, Vector3 nrm, Vector2 u, BoneWeight wt, byte part = PartEdge)
            {
                v.Add(p);
                n.Add(nrm);
                uv.Add(u);
                w.Add(wt);
                tag.Add(part);
                return v.Count - 1;
            }

            /// <summary>三角を加える。巻きは頂点の法線の向きに揃える</summary>
            public void Tri(int s, int a, int b, int c)
            {
                var f = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(f, n[a] + n[b] + n[c]) < 0f) { var x = b; b = c; c = x; }
                sub[s].Add(a);
                sub[s].Add(b);
                sub[s].Add(c);
            }

            public int TriangleCount { get { return (sub[0].Count + sub[1].Count + sub[2].Count + sub[3].Count) / 3; } }

            public Mesh Mesh(string name, Matrix4x4 toLocal)
            {
                var m = new Mesh { name = name };
                var lv = new Vector3[v.Count];
                var ln = new Vector3[v.Count];
                for (var i = 0; i < lv.Length; i++)
                {
                    lv[i] = toLocal.MultiplyPoint3x4(v[i]);
                    ln[i] = toLocal.MultiplyVector(n[i]).normalized;
                }
                m.vertices = lv;
                m.normals = ln;
                m.uv = uv.ToArray();
                m.boneWeights = w.ToArray();
                m.subMeshCount = 4;
                for (var s = 0; s < 4; s++) m.SetTriangles(sub[s], s);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>三角ごとの置き場: 0 が身頃、1 が左の袖、2 が右の袖（三つの頂点の腕の骨の重みの平均で分ける）</summary>
        static int[] Charts(Rig r, Net n)
        {
            var c = new int[n.t.Count / 3];
            for (var k = 0; k < n.t.Count; k += 3)
            {
                float a0 = 0f, a1 = 0f;
                for (var e = 0; e < 3; e++)
                {
                    var w = n.w[n.t[k + e]];
                    a0 += Share(w, r.arm[0]);
                    a1 += Share(w, r.arm[1]);
                }
                c[k / 3] = Mathf.Max(a0, a1) / 3f < 0.5f ? 0 : a0 >= a1 ? 1 : 2;
            }
            return c;
        }

        /// <summary>身頃と袖の表の面。置き場ごとに UV を付け（継ぎ目をまたぐ三角は写した頂点で）、面の組 0 へ。返すのは（頂点, 置き場）ごとの組み立て場の番号</summary>
        static Dictionary<long, int> Skin(Out o, Rig r, Slices s, Net n, Vector3[] nrm, int[] charts)
        {
            var at = new Dictionary<long, int>();
            var lo = r.HemY - 0.03f;
            var hi = r.Neck.y + 0.04f;
            Func<int, int, Vector2> raw = (i, chart) =>
            {
                var p = n.p[i];
                if (chart == 0)
                {
                    var c = s.CentreAt(p.y);
                    var th = Mathf.Atan2(p.x - c.x, p.z - c.y);
                    return new Vector2((th + Mathf.PI) / (2f * Mathf.PI), Mathf.InverseLerp(lo, hi, p.y));
                }
                return SleeveParam(r, chart - 1, p);
            };
            for (var k = 0; k < n.t.Count; k += 3)
            {
                var chart = charts[k / 3];
                var ps = new Vector2[3];
                for (var e = 0; e < 3; e++) ps[e] = raw(n.t[k + e], chart);
                // 継ぎ目（身頃は後ろの真ん中の u、袖は腕の下の v）をまたぐ三角は、小さい側を 1 ずらす
                if (chart == 0)
                {
                    float mn = Mathf.Min(ps[0].x, Mathf.Min(ps[1].x, ps[2].x)), mx = Mathf.Max(ps[0].x, Mathf.Max(ps[1].x, ps[2].x));
                    if (mx - mn > 0.5f) for (var e = 0; e < 3; e++) if (ps[e].x < 0.5f) ps[e].x += 1f;
                }
                else
                {
                    float mn = Mathf.Min(ps[0].y, Mathf.Min(ps[1].y, ps[2].y)), mx = Mathf.Max(ps[0].y, Mathf.Max(ps[1].y, ps[2].y));
                    if (mx - mn > 0.5f) for (var e = 0; e < 3; e++) if (ps[e].y < 0.5f) ps[e].y += 1f;
                }
                var rect = chart == 0 ? TorsoUv : SleeveUv[chart - 1];
                var ids = new int[3];
                for (var e = 0; e < 3; e++)
                {
                    var i = n.t[k + e];
                    var wrapped = chart == 0 ? ps[e].x >= 1f : ps[e].y >= 1f;
                    var key = ((long)i << 3) | ((long)chart << 1) | (wrapped ? 1L : 0L);
                    int id;
                    if (!at.TryGetValue(key, out id))
                    {
                        var uv = new Vector2(rect.x + ps[e].x * rect.width, rect.y + ps[e].y * rect.height);
                        at[key] = id = o.Add(n.p[i], nrm[i], uv, n.w[i], (byte)chart);
                    }
                    ids[e] = id;
                }
                o.sub[0].Add(ids[0]);
                o.sub[0].Add(ids[1]);
                o.sub[0].Add(ids[2]);
            }
            return at;
        }

        /// <summary>袖の UV の元: 肩から手首への長さの比と、腕の軸のまわりの角度の比（腕の上が 0.5、下が 0 と 1）</summary>
        static Vector2 SleeveParam(Rig r, int side, Vector3 p)
        {
            float along;
            var q = OnArm(r, side, p, out along);
            var upper = (r.Elbow(side) - r.Shoulder(side)).magnitude;
            var full = upper + (r.Wrist(side) - r.Elbow(side)).magnitude;
            var axis = along < upper ? (r.Elbow(side) - r.Shoulder(side)).normalized : (r.Wrist(side) - r.Elbow(side)).normalized;
            var up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            var rad = Vector3.ProjectOnPlane(p - q, axis);
            var phi = rad.sqrMagnitude > 1e-12f ? Vector3.SignedAngle(up, rad, axis) * Mathf.Deg2Rad : 0f;
            return new Vector2(Mathf.Clamp01(along / full), (phi + Mathf.PI) / (2f * Mathf.PI));
        }

        /// <summary>裏地: 表の面を厚みだけ内へ下げ、向きを裏返した面（面の組 2）</summary>
        static void Lining(Out o, Net n, Vector3[] nrm)
        {
            var edge = new List<Vector3>();
            foreach (var i in n.Rim().Keys) edge.Add(n.p[i]);
            var near = new bool[n.p.Count];
            for (var i = 0; i < near.Length; i++)
                foreach (var e in edge)
                    if ((e - n.p[i]).sqrMagnitude < LiningReach * LiningReach) { near[i] = true; break; }
            var ids = new int[n.p.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = near[i] ? o.Add(n.p[i] - nrm[i] * Thick, -nrm[i], Vector2.zero, n.w[i], PartLining) : -1;
            for (var k = 0; k < n.t.Count; k += 3)
            {
                int a = ids[n.t[k]], b = ids[n.t[k + 1]], c = ids[n.t[k + 2]];
                if (a < 0 || b < 0 || c < 0) continue;
                o.Tri(2, a, b, c);
            }
        }

        /// <summary>縁（裾・袖口・首まわり）の厚み: 表の縁と裏地の縁をつなぐ細い帯（面の組 0、表の UV のまま）。帯の辺の数を返す</summary>
        static int Rims(Out o, Net n, Vector3[] nrm, int[] charts, Dictionary<long, int> outer)
        {
            var count = new Dictionary<long, int>();
            var owner = new Dictionary<long, int>();
            for (var k = 0; k < n.t.Count; k += 3)
                for (var e = 0; e < 3; e++)
                {
                    var key = EdgeKey(n.t[k + e], n.t[k + (e + 1) % 3]);
                    int c;
                    count.TryGetValue(key, out c);
                    count[key] = c + 1;
                    owner[key] = k;
                }
            var edges = 0;
            foreach (var kv in count)
            {
                if (kv.Value != 1) continue;
                var k = owner[kv.Key];
                var a = (int)(kv.Key >> 32);
                var b = (int)(kv.Key & 0xffffffff);
                var third = n.t[k] != a && n.t[k] != b ? n.t[k] : n.t[k + 1] != a && n.t[k + 1] != b ? n.t[k + 1] : n.t[k + 2];
                var mid = (n.p[a] + n.p[b]) * 0.5f;
                var edge = n.p[b] - n.p[a];
                var away = Vector3.ProjectOnPlane(mid - n.p[third], edge).normalized;
                var chart = charts[k / 3];
                Func<int, Vector2> uvOf = i =>
                {
                    int id;
                    if (outer.TryGetValue(((long)i << 3) | ((long)chart << 1), out id)) return o.uv[id];
                    if (outer.TryGetValue(((long)i << 3) | ((long)chart << 1) | 1L, out id)) return o.uv[id];
                    return Vector2.zero;
                };
                var A = o.Add(n.p[a], away, uvOf(a), n.w[a]);
                var B = o.Add(n.p[b], away, uvOf(b), n.w[b]);
                var A2 = o.Add(n.p[a] - nrm[a] * Thick, away, uvOf(a), n.w[a]);
                var B2 = o.Add(n.p[b] - nrm[b] * Thick, away, uvOf(b), n.w[b]);
                o.Tri(0, A, B, B2);
                o.Tri(0, A, B2, A2);
                edges++;
            }
            return edges;
        }

        /// <summary>縁の輪（縁の辺をたどった頂点の並び）</summary>
        static List<List<int>> Loops(Net n)
        {
            // 縁の辺を三角の巻きの向きのまま集め、辺の終わりから次の辺へたどる（二つの縁が一点で触れていても、向きで分けられる）
            var count = new Dictionary<long, int>();
            for (var k = 0; k < n.t.Count; k += 3)
                for (var e = 0; e < 3; e++)
                {
                    var key = EdgeKey(n.t[k + e], n.t[k + (e + 1) % 3]);
                    int c;
                    count.TryGetValue(key, out c);
                    count[key] = c + 1;
                }
            var next = new Dictionary<int, List<int>>();
            for (var k = 0; k < n.t.Count; k += 3)
                for (var e = 0; e < 3; e++)
                {
                    int a = n.t[k + e], b = n.t[k + (e + 1) % 3];
                    if (count[EdgeKey(a, b)] != 1) continue;
                    List<int> l;
                    if (!next.TryGetValue(a, out l)) next[a] = l = new List<int>();
                    l.Add(b);
                }
            var used = new HashSet<long>();
            var loops = new List<List<int>>();
            foreach (var start in next.Keys)
                foreach (var first in next[start])
                {
                    if (used.Contains(((long)start << 32) | (uint)first)) continue;
                    var loop = new List<int> { start };
                    int from = start, to = first;
                    while (true)
                    {
                        used.Add(((long)from << 32) | (uint)to);
                        if (to == start) break;
                        loop.Add(to);
                        List<int> outs;
                        if (!next.TryGetValue(to, out outs)) break;
                        var go = -1;
                        foreach (var c in outs) if (!used.Contains(((long)to << 32) | (uint)c)) { go = c; break; }
                        if (go < 0) break;
                        from = to;
                        to = go;
                    }
                    if (loop.Count >= 3) loops.Add(loop);
                }
            return loops;
        }

        /// <summary>多角形の中の点までの符号付きの距離（中で正）</summary>
        static float Inside(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            var best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return inside ? best : -best;
        }

        /// <summary>
        /// 襟の折り返しと襟返しの内側が正の値（m。縁からのおおよその距離）。ring は首まわりの切り口の輪の点。
        /// 後ろと横（FallFrontEnd より後ろ）は輪からの幅の帯、前は襟の前の端（<see cref="CollarFrontPoly"/>）と襟返し（<see cref="LapelPoly"/>）の多角形
        /// </summary>
        static float FallG(Rig r, List<Vector3> ring, Vector3 p)
        {
            var nk = r.Neck;
            var dn = float.MaxValue;
            for (var k = 0; k < ring.Count; k++)
            {
                var a = ring[k];
                var b = ring[(k + 1) % ring.Count];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                dn = Mathf.Min(dn, (a + ab * t - p).magnitude);
            }
            var phi = Mathf.Abs(Mathf.Atan2(p.x - nk.x, p.z - nk.z) * Mathf.Rad2Deg);
            var width = phi >= 90f ? Mathf.Lerp(FallSide, FallBack, (phi - 90f) / 90f) : FallSide;
            var band = phi >= FallFrontEnd ? width - dn : -1f;
            var front = -1f;
            if (p.z - nk.z > -0.005f)
            {
                var q = new Vector2(Mathf.Abs(p.x - nk.x), p.y - nk.y);
                front = Mathf.Max(Inside(CollarFrontPoly(), q), Inside(LapelPoly(p.x > nk.x), q));
            }
            return Mathf.Max(band, front);
        }

        /// <summary>
        /// 襟。閉じた身頃 n の首まわりの切り口の輪 neck から:
        /// - 立ち上がり: 輪から上へ立つ帯（上の縁は首のまわりの角度だけで決める）。上で外へ折り返す。前の開きの間（襟返しの折り目の上の端より内）には無い
        /// - 折り返し・襟の前の端・襟返し: 閉じた身頃の面のうち、<see cref="FallG"/> の内を切り出し、FallLift だけ浮かせた面。
        ///   切り出す所のそばは身頃の三角を細かく分け、縁をならしてから面へ戻す（縁がぎざぎざにならず、一本の線で通る）。
        ///   浮かせた面もならす（身頃の面の細かな凹凸を拾わず、平らな布に見える。身頃から FallLift の半分より内へは入れない）。外の縁に厚みの帯を付ける
        /// </summary>
        static string Collar(Out o, Rig r, Net n, Vector3[] nrm, List<int> neck)
        {
            var nk = r.Neck;
            // 輪を首のまわりの角度の順（上から見て）に
            Func<Vector3, float> angle = p => Mathf.Atan2(p.x - nk.x, p.z - nk.z);
            var turn = 0f;
            for (var i = 0; i < neck.Count; i++) turn += Mathf.DeltaAngle(angle(n.p[neck[i]]) * Mathf.Rad2Deg, angle(n.p[neck[(i + 1) % neck.Count]]) * Mathf.Rad2Deg);
            if (turn < 0f) neck.Reverse();
            Func<Vector3, bool> inOpening = p => p.z > nk.z && Mathf.Abs(p.x - nk.x) < FoldTop.x;

            var ringPts = new List<Vector3>();
            foreach (var i in neck) ringPts.Add(n.p[i]);
            Func<Vector3, float> region = p => FallG(r, ringPts, p);
            var mark = new bool[n.p.Count];
            foreach (var i in neck) if (!inOpening(n.p[i])) mark[i] = true;
            Vector3[] nn;
            bool[] mk;
            var net = Refine(n, nrm, mark, region, 0.02f, out nn, out mk);
            var g = new float[net.p.Count];
            for (var i = 0; i < g.Length; i++)
            {
                g[i] = region(net.p[i]);
                if (mk[i]) g[i] = Mathf.Max(g[i], 0.001f);
            }
            var cut = new HashSet<int>();
            Dictionary<int, int> kept;
            Vector3[] fn0;
            var fall = ClipWith(net, g, nn, cut, out kept, out fn0);
            var onRing = new bool[fall.p.Count];
            foreach (var kv in kept) if (mk[kv.Key]) onRing[kv.Value] = true;

            // 縁をならす（輪は立ち上がりへつながるので動かさない）。内の点も少しならし、身頃の面へ戻して浮かせる
            var pos = fall.p.ToArray();
            var fnb = fall.Neighbours();
            var frim = fall.Rim();
            for (var pass = 0; pass < 6; pass++)
            {
                var was = (Vector3[])pos.Clone();
                for (var i = 0; i < pos.Length; i++)
                {
                    if (onRing[i]) continue;
                    List<int> along;
                    var list = frim.TryGetValue(i, out along) ? along : fnb[i];
                    if (list.Count == 0) continue;
                    var c = Vector3.zero;
                    foreach (var j in list) c += was[j];
                    pos[i] = Vector3.Lerp(was[i], c / list.Count, 0.5f);
                }
            }
            var onShell = new Surf(n, nrm);
            var lift = new Vector3[pos.Length];
            var fn = new Vector3[pos.Length];
            var ground = new Vector3[pos.Length];
            for (var i = 0; i < pos.Length; i++)
            {
                var sn = fn0[i];
                BoneWeight sw;
                Vector3 q;
                if (onRing[i]) q = fall.p[i];
                else
                {
                    q = onShell.Snap(pos[i], 0.03f, out sn, out sw);
                    if (Vector3.Dot(sn, fn0[i]) < 0f) sn = fn0[i];
                }
                fn[i] = sn;
                ground[i] = q;
                lift[i] = q + sn * FallLift;
            }

            // 浮かせた面をならす（輪は動かさない）。身頃の面から FallLift の半分より内へは入れない
            for (var pass = 0; pass < 20; pass++)
            {
                var was = (Vector3[])lift.Clone();
                for (var i = 0; i < lift.Length; i++)
                {
                    if (onRing[i]) continue;
                    List<int> along;
                    var list = frim.TryGetValue(i, out along) ? along : fnb[i];
                    if (list.Count == 0) continue;
                    var c = Vector3.zero;
                    foreach (var j in list) c += was[j];
                    var q = Vector3.Lerp(was[i], c / list.Count, 0.5f);
                    var h = Vector3.Dot(q - ground[i], fn[i]);
                    if (h < FallLift * 0.5f) q += fn[i] * (FallLift * 0.5f - h);
                    lift[i] = q;
                }
            }
            // 縁の厚みの足は、ならした面の真下の身頃の面
            for (var i = 0; i < lift.Length; i++)
            {
                if (onRing[i]) continue;
                Vector3 sn;
                BoneWeight sw;
                ground[i] = onShell.Snap(lift[i], 0.03f, out sn, out sw);
            }
            // 法線は、ならした面の形から取り直してならす（身頃の面の凹凸の法線を持ち込まない）
            var ln = fall.Normals(lift);
            for (var i = 0; i < ln.Length; i++) if (Vector3.Dot(ln[i], fn[i]) < 0f) ln[i] = -ln[i];
            for (var pass = 0; pass < 3; pass++)
            {
                var was = (Vector3[])ln.Clone();
                for (var i = 0; i < ln.Length; i++)
                {
                    var c = was[i];
                    foreach (var j in fnb[i]) c += was[j];
                    ln[i] = c.normalized;
                }
            }
            for (var i = 0; i < ln.Length; i++) if (!onRing[i]) fn[i] = ln[i];

            // UV: u は首のまわりの角度（後ろの真ん中で継ぐ。継ぎ目をまたぐ三角は写した頂点で 1 先へ。四角に収めるため 1/1.05 に縮める）、v は輪からの距離
            var rawU = new float[fall.p.Count];
            var fallV = new float[fall.p.Count];
            for (var i = 0; i < rawU.Length; i++)
            {
                rawU[i] = (angle(ground[i]) + Mathf.PI) / (2f * Mathf.PI);
                var dn = float.MaxValue;
                foreach (var q in ringPts) dn = Mathf.Min(dn, (q - ground[i]).magnitude);
                fallV[i] = FallUv.y + Mathf.Clamp01(dn / 0.2f) * FallUv.height;
            }
            var fallIds = new Dictionary<long, int>();
            Func<int, bool, int> fid = (i, wrapped) =>
            {
                var key = ((long)i << 1) | (wrapped ? 1L : 0L);
                int id;
                if (fallIds.TryGetValue(key, out id)) return id;
                var u = (rawU[i] + (wrapped ? 1f : 0f)) / 1.05f;
                id = o.Add(lift[i], fn[i], new Vector2(FallUv.x + u * FallUv.width, fallV[i]), fall.w[i], PartFall);
                fallIds[key] = id;
                return id;
            };
            var ids = new int[fall.p.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = fid(i, false);
            for (var k = 0; k < fall.t.Count; k += 3)
            {
                int a0 = fall.t[k], a1 = fall.t[k + 1], a2 = fall.t[k + 2];
                var lo = Mathf.Min(rawU[a0], Mathf.Min(rawU[a1], rawU[a2]));
                var hi = Mathf.Max(rawU[a0], Mathf.Max(rawU[a1], rawU[a2]));
                var wrap = hi - lo > 0.5f;
                o.Tri(0, fid(a0, wrap && rawU[a0] < 0.5f), fid(a1, wrap && rawU[a1] < 0.5f), fid(a2, wrap && rawU[a2] < 0.5f));
            }
            // 外の縁の厚み（首まわりの縁は立ち上がりの折り返しがつながるので除く）
            var walls = 0;
            var done = new HashSet<long>();
            foreach (var kv in frim)
                foreach (var j in kv.Value)
                {
                    var a = kv.Key;
                    var key = EdgeKey(a, j);
                    if (!done.Add(key)) continue;
                    if (onRing[a] && onRing[j]) continue;
                    var inward = Vector3.zero;
                    foreach (var q in fnb[a]) inward += pos[q] - pos[a];
                    var away = Vector3.ProjectOnPlane(-inward, pos[j] - pos[a]).normalized;
                    var A = o.Add(lift[a], away, o.uv[ids[a]], fall.w[a]);
                    var B = o.Add(lift[j], away, o.uv[ids[j]], fall.w[j]);
                    var A2 = o.Add(ground[a] + fn[a] * 0.0004f, away, o.uv[ids[a]], fall.w[a]);
                    var B2 = o.Add(ground[j] + fn[j] * 0.0004f, away, o.uv[ids[j]], fall.w[j]);
                    o.Tri(0, A, B, B2);
                    o.Tri(0, A, B2, A2);
                    walls++;
                }

            // 立ち上がり: 輪の頂点ごとに、根（裏地の縁）→ 内の上 → 折り目 → 外の上 → 折り返しの首まわりの縁。前の開きの間の列は作らない
            var rows = 5;
            var grid = new int[neck.Count, rows];
            var gridWrap = new int[neck.Count, rows];
            var standU = new float[neck.Count];
            var use = new bool[neck.Count];
            var tangent = new Vector3[neck.Count];
            for (var k = 0; k < neck.Count; k++) use[k] = !inOpening(n.p[neck[k]]);
            // 立ち上がりの端（前の開き）のそばは、上の縁を根の方へ下げる（襟の前の縁が、立ち上がりの上から襟の先の方へ斜めに下りる）
            var toEnd = new int[neck.Count];
            for (var k = 0; k < neck.Count; k++)
            {
                toEnd[k] = int.MaxValue;
                if (!use[k]) continue;
                for (var d = 1; d < neck.Count / 2; d++)
                    if (!use[(k + d) % neck.Count] || !use[(k - d + neck.Count) % neck.Count]) { toEnd[k] = d; break; }
            }
            for (var k = 0; k < neck.Count; k++)
            {
                var i = neck[k];
                var p = n.p[i];
                if (!use[k]) continue;
                var radial = new Vector3(p.x - nk.x, 0f, p.z - nk.z).normalized;
                tangent[k] = Vector3.Cross(Vector3.up, radial);
                // 上の縁は首のまわりの角度だけで決める（切り口の輪の細かな揺れを拾わない）
                var ca = Vector3.Dot(radial, Vector3.forward);
                var topY = nk.y + (ca >= 0f ? Mathf.Lerp(StandTop.y, StandTop.x, ca * ca) : Mathf.Lerp(StandTop.y, StandTop.z, ca * ca));
                var reach = ca >= 0f ? Mathf.Lerp(StandReach.y, StandReach.x, ca * ca) : Mathf.Lerp(StandReach.y, StandReach.z, ca * ca);
                var topIn = new Vector3(nk.x, Mathf.Max(topY, p.y + 0.015f), nk.z) + radial * reach;
                var taper = toEnd[k] == int.MaxValue ? 1f : RocketboxPaint.Smooth(0f, StandTaper, toEnd[k]);
                topIn = Vector3.Lerp(p + Vector3.up * 0.004f + radial * 0.002f, topIn, taper);
                var fold = topIn + radial * (Thick * 0.5f) + Vector3.up * (Thick * 0.5f);
                var topOut = topIn + radial * Thick;
                int j;
                var down = kept.TryGetValue(i, out j) ? lift[j] : p + nrm[i] * FallLift;
                var pts = new[] { p - nrm[i] * Thick, topIn, fold, topOut, down };
                var ns = new[] { -radial, (-radial + Vector3.up).normalized, Vector3.up, (radial + Vector3.up).normalized, radial };
                var u = (angle(p) + Mathf.PI) / (2f * Mathf.PI);
                standU[k] = u;
                for (var q = 0; q < rows; q++)
                {
                    var v = StandUv.y + q / (rows - 1f) * StandUv.height;
                    grid[k, q] = o.Add(pts[q], ns[q], new Vector2(StandUv.x + u / 1.05f * StandUv.width, v), n.w[i], PartStand);
                    gridWrap[k, q] = o.Add(pts[q], ns[q], new Vector2(StandUv.x + (u + 1f) / 1.05f * StandUv.width, v), n.w[i], PartStand);
                }
            }
            var ends = 0;
            for (var k = 0; k < neck.Count; k++)
            {
                var k1 = (k + 1) % neck.Count;
                if (!use[k]) continue;
                if (!use[k1] || !use[(k - 1 + neck.Count) % neck.Count])
                {
                    // 立ち上がりの端: 断面（根・内の上・折り目・外の上・折り返しの縁）を扇でふさぐ
                    var dir = !use[k1] ? tangent[k] : -tangent[k];
                    var cap = new int[rows];
                    for (var q = 0; q < rows; q++) cap[q] = o.Add(o.v[grid[k, q]], dir, o.uv[grid[k, q]], o.w[grid[k, q]], PartEdge);
                    for (var q = 1; q + 1 < rows; q++) o.Tri(0, cap[0], cap[q], cap[q + 1]);
                    ends++;
                }
                if (!use[k1]) continue;
                // 後ろの真ん中の継ぎ目をまたぐ四角は、u の小さい側を 1 先の写しにする
                var wrap = Mathf.Abs(standU[k1] - standU[k]) > 0.5f;
                Func<int, int, int> at = (kk, q) => wrap && standU[kk] < 0.5f ? gridWrap[kk, q] : grid[kk, q];
                for (var q = 0; q + 1 < rows; q++)
                {
                    o.Tri(0, at(k, q), at(k1, q), at(k1, q + 1));
                    o.Tri(0, at(k, q), at(k1, q + 1), at(k, q + 1));
                }
            }
            return string.Format(CultureInfo.InvariantCulture, "襟: 立ち上がり {0} 列（前の開きで {4} 端）、折り返し・襟の前の端・襟返し {1} 三角（細かく分けた身頃 {2} 三角から。縁の厚み {3} 辺）",
                neck.Count, fall.t.Count / 3, net.t.Count / 3, walls, ends);
        }

        // ---- 飾り ----------------------------------------------------------------

        /// <summary>描き分けと飾りの置き場に使う、形の測り</summary>
        sealed class Look
        {
            public Rig r;
            public Slices s;
            /// <summary>首まわりの切り口の輪の点（ずらした後）</summary>
            public List<Vector3> ring;
        }

        /// <summary>ずらした後の身頃と袖の面。近い点と、その点の法線（頂点の法線を混ぜた物）と骨の重み</summary>
        sealed class Surf
        {
            readonly Net n;
            readonly Vector3[] nrm;
            readonly RocketboxCompose.Surface s;

            public Surf(Net n, Vector3[] nrm)
            {
                this.n = n;
                this.nrm = nrm;
                s = new RocketboxCompose.Surface(n.p.ToArray(), n.t.ToArray());
            }

            public Vector3 Snap(Vector3 p, float reach, out Vector3 normal, out BoneWeight w)
            {
                Vector3 q, fn;
                int k;
                s.Closest(p, reach, out q, out fn, out k);
                if (k < 0)
                {
                    normal = Vector3.up;
                    w = new BoneWeight { weight0 = 1f };
                    return p;
                }
                int a = n.t[k], b = n.t[k + 1], c = n.t[k + 2];
                float wa, wb, wc;
                Bary(q, n.p[a], n.p[b], n.p[c], out wa, out wb, out wc);
                normal = (nrm[a] * wa + nrm[b] * wb + nrm[c] * wc).normalized;
                w = Mix(Mix(n.w[a], n.w[b], wb / Mathf.Max(1e-6f, wa + wb)), n.w[c], wc);
                return q;
            }

            /// <summary>前から見た (x, y) の所の、前の面の点</summary>
            public Vector3 Front(float x, float y, out Vector3 normal, out BoneWeight w)
            {
                var q = Snap(new Vector3(x, y, 0.35f), 0.45f, out normal, out w);
                for (var i = 0; i < 2; i++) q = Snap(new Vector3(x, y, q.z + 0.03f), 0.08f, out normal, out w);
                return q;
            }
        }

        static void Bary(Vector3 q, Vector3 a, Vector3 b, Vector3 c, out float wa, out float wb, out float wc)
        {
            var v0 = b - a;
            var v1 = c - a;
            var v2 = q - a;
            float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
            var den = d00 * d11 - d01 * d01;
            wb = den > 1e-20f ? Mathf.Clamp01((d11 * d20 - d01 * d21) / den) : 0f;
            wc = den > 1e-20f ? Mathf.Clamp01((d00 * d21 - d01 * d20) / den) : 0f;
            var sum = wb + wc;
            if (sum > 1f) { wb /= sum; wc /= sum; }
            wa = 1f - wb - wc;
        }

        /// <summary>折れ線を、長さ step ごとの点に取り直す。closed なら最後の点から最初の点へも</summary>
        static List<Vector3> Resample(List<Vector3> path, bool closed, float step)
        {
            var pts = new List<Vector3>(path);
            if (closed) pts.Add(path[0]);
            var total = 0f;
            for (var i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).magnitude;
            var n = Mathf.Max(2, Mathf.CeilToInt(total / step) + 1);
            var o = new List<Vector3>();
            var seg = 0;
            var segStart = 0f;
            for (var k = 0; k < n; k++)
            {
                var d = total * k / (n - 1);
                while (seg < pts.Count - 2 && segStart + (pts[seg + 1] - pts[seg]).magnitude < d)
                {
                    segStart += (pts[seg + 1] - pts[seg]).magnitude;
                    seg++;
                }
                var len = (pts[seg + 1] - pts[seg]).magnitude;
                o.Add(Vector3.Lerp(pts[seg], pts[seg + 1], len > 1e-9f ? Mathf.Clamp01((d - segStart) / len) : 0f));
            }
            // 輪は、最後の点が最初の点と同じ（UV の u を最後まで増やして、継ぎ目で絵が戻らないように）
            return o;
        }

        /// <summary>
        /// 面に沿った帯。path は面の近くの点の並び。幅は width（長さの比 0〜1 から）、面から lift だけ浮かせた上の面と両脇の壁（輪でなければ両端のふた）。
        /// 上の面の UV: u は長さ（m）に uPerMetre を掛けた物、v は幅の向きに vLo〜vHi。返すのは点の数
        /// </summary>
        static int Strap(Out o, Surf sh, List<Vector3> path, bool closed, Func<float, float> width, float lift, int sub, byte part, float vLo, float vHi, float uPerMetre, float step = 0.004f)
        {
            var pts = Resample(path, closed, step);
            var n = pts.Count;
            var c = new Vector3[n];
            var nn = new Vector3[n];
            var ww = new BoneWeight[n];
            for (var i = 0; i < n; i++) c[i] = sh.Snap(pts[i], 0.06f, out nn[i], out ww[i]);
            var s = new float[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + (c[i] - c[i - 1]).magnitude;
            var total = Mathf.Max(1e-6f, s[n - 1]);
            var edgePart = part == PartMetal ? PartMetal : PartEdge;
            var tl = new int[n];
            var tr = new int[n];
            var wl = new int[n];
            var wlb = new int[n];
            var wr = new int[n];
            var wrb = new int[n];
            var tangents = new Vector3[n];
            for (var i = 0; i < n; i++)
            {
                Vector3 prev, next;
                if (closed && (i == 0 || i == n - 1)) { prev = c[n - 2]; next = c[1]; }
                else { prev = c[Mathf.Max(0, i - 1)]; next = c[Mathf.Min(n - 1, i + 1)]; }
                var tan = Vector3.ProjectOnPlane(next - prev, nn[i]).normalized;
                tangents[i] = tan;
                var side = Vector3.Cross(nn[i], tan).normalized;
                var half = width(s[i] / total) * 0.5f;
                Vector3 nL = nn[i], nR = nn[i];
                BoneWeight wL = ww[i], wR = ww[i];
                var L = half > 1e-5f ? sh.Snap(c[i] - side * half, 0.03f, out nL, out wL) : c[i];
                var R = half > 1e-5f ? sh.Snap(c[i] + side * half, 0.03f, out nR, out wR) : c[i];
                var u = s[i] * uPerMetre;
                tl[i] = o.Add(L + nL * lift, nL, new Vector2(u, vLo), wL, part);
                tr[i] = o.Add(R + nR * lift, nR, new Vector2(u, vHi), wR, part);
                wl[i] = o.Add(L + nL * lift, -side, new Vector2(u, vLo), wL, edgePart);
                wlb[i] = o.Add(L + nL * 0.0004f, -side, new Vector2(u, vLo), wL, edgePart);
                wr[i] = o.Add(R + nR * lift, side, new Vector2(u, vHi), wR, edgePart);
                wrb[i] = o.Add(R + nR * 0.0004f, side, new Vector2(u, vHi), wR, edgePart);
            }
            for (var i = 0; i + 1 < n; i++)
            {
                var j = i + 1;
                o.Tri(sub, tl[i], tr[i], tr[j]);
                o.Tri(sub, tl[i], tr[j], tl[j]);
                o.Tri(sub, wl[i], wl[j], wlb[j]);
                o.Tri(sub, wl[i], wlb[j], wlb[i]);
                o.Tri(sub, wr[i], wr[j], wrb[j]);
                o.Tri(sub, wr[i], wrb[j], wrb[i]);
            }
            if (!closed)
                foreach (var end in new[] { 0, n - 1 })
                {
                    var dir = end == 0 ? -tangents[0] : tangents[n - 1];
                    var a = o.Add(o.v[tl[end]], dir, o.uv[tl[end]], o.w[tl[end]], edgePart);
                    var b = o.Add(o.v[tr[end]], dir, o.uv[tr[end]], o.w[tr[end]], edgePart);
                    var bb = o.Add(o.v[wrb[end]], dir, o.uv[tr[end]], o.w[tr[end]], edgePart);
                    var ab = o.Add(o.v[wlb[end]], dir, o.uv[tl[end]], o.w[tl[end]], edgePart);
                    o.Tri(sub, a, b, bb);
                    o.Tri(sub, a, bb, ab);
                }
            return n;
        }

        /// <summary>スナップ（丸い金具）: 面の点 at に、面から lift だけ浮かせて置く</summary>
        static void Stud(Out o, Surf sh, Vector3 at, float lift)
        {
            Vector3 n;
            BoneWeight w;
            var c = sh.Snap(at, 0.06f, out n, out w);
            var b1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var b2 = Vector3.Cross(n, b1);
            const int seg = 14;
            var bottom = c + n * lift;
            var top = bottom + n * StudHeight;
            var centre = o.Add(top + n * (StudHeight * 0.35f), n, Vector2.zero, w, PartMetal);
            var cap = new int[seg];
            var rimTop = new int[seg];
            var rimBottom = new int[seg];
            for (var i = 0; i < seg; i++)
            {
                var a = i * 2f * Mathf.PI / seg;
                var radial = b1 * Mathf.Cos(a) + b2 * Mathf.Sin(a);
                cap[i] = o.Add(top + radial * (StudRadius * 0.8f), (n * 0.75f + radial * 0.25f).normalized, Vector2.zero, w, PartMetal);
                rimTop[i] = o.Add(top - n * 0.0004f + radial * StudRadius, (radial + n * 0.4f).normalized, Vector2.zero, w, PartMetal);
                rimBottom[i] = o.Add(bottom + radial * StudRadius, radial, Vector2.zero, w, PartMetal);
            }
            for (var i = 0; i < seg; i++)
            {
                var j = (i + 1) % seg;
                o.Tri(1, centre, cap[i], cap[j]);
                o.Tri(1, cap[i], rimTop[i], rimTop[j]);
                o.Tri(1, cap[i], rimTop[j], cap[j]);
                o.Tri(1, rimTop[i], rimBottom[i], rimBottom[j]);
                o.Tri(1, rimTop[i], rimBottom[j], rimTop[j]);
            }
        }

        /// <summary>ジッパー: 布の帯と、その上の務歯の帯（面の組 3）。返すのは長さ</summary>
        static float Zipper(Out o, Surf sh, List<Vector3> path, float tapeLift)
        {
            Strap(o, sh, path, false, f => TapeWidth, tapeLift, 0, PartTape, TapeBand.y, TapeBand.yMax, 1f);
            Strap(o, sh, path, false, f => TeethWidth, tapeLift + 0.0011f, 3, PartMetal, 0f, 1f, 1f / TeethPitch, 0.0028f);
            var len = 0f;
            for (var i = 1; i < path.Count; i++) len += (path[i] - path[i - 1]).magnitude;
            return len;
        }

        /// <summary>ジッパーの引き手: 点 at から向き down へ、つまみ（スライダー）と引き手の板</summary>
        static void Puller(Out o, Surf sh, Vector3 at, Vector3 down, float lift, float size)
        {
            var slider = new List<Vector3> { at, at + down * (0.012f * size) };
            Strap(o, sh, slider, false, f => 0.010f * size, lift + 0.0018f, 1, PartMetal, 0f, 0f, 0f, 0.002f);
            var tab = new List<Vector3> { at + down * (0.008f * size), at + down * (0.036f * size) };
            Strap(o, sh, tab, false, f => (0.0085f - 0.003f * Mathf.Max(0f, f - 0.75f) * 4f) * size, lift + 0.0032f, 1, PartMetal, 0f, 0f, 0f, 0.002f);
        }

        /// <summary>
        /// 飾りを付ける: 前の縁のジッパー（左右の身頃の縁に片側ずつ。本人の右の裾の近くに引き手）、本人の右の胸の斜めのポケットのジッパー、
        /// 腰の横のポケットのジッパー（左右）、肩章とスナップ、袖口のジッパー、襟の先と襟返しの角のスナップ。
        /// 裾のベルト（<see cref="Belt"/>）は付けない
        /// </summary>
        static string Details(Out o, Look k, Surf sh)
        {
            var r = k.r;
            var s = k.s;
            var nk = r.Neck;
            var up = Vector3.up;
            Vector3 nrm;
            BoneWeight w;
            var parts = new List<string>();

            if (Belt)
            {
                var by = r.HemY + BeltAbove;
                var belt = new List<Vector3>();
                for (var i = 0; i < 96; i++)
                {
                    var th = -Mathf.PI + 2f * Mathf.PI * i / 96f;
                    belt.Add(s.At(by, th, EaseAt(Mathf.Cos(th))));
                }
                Strap(o, sh, belt, true, f => BeltWidth, BeltLift, 0, PartStrap, StrapBand.y, StrapBand.yMax, 1f);
                parts.Add("ベルト");
            }

            // 前の縁のジッパー（片側ずつ）: 裾から折れ目まで、縁の少し身頃の側に布と務歯
            var hemRel = r.HemY - nk.y;
            foreach (var right in new[] { true, false })
            {
                var sign = right ? 1f : -1f;
                var hx = right ? HemEdgeRight : HemEdgeLeft;
                var b = right ? BreakRight : BreakLeft;
                Func<float, float> edgeX = y => Mathf.LerpUnclamped(hx, b.x, (y - (hemRel - 0.05f)) / (b.y - (hemRel - 0.05f)));
                var teeth = new List<Vector3>();
                var tape = new List<Vector3>();
                for (var i = 0; i <= 30; i++)
                {
                    var y = Mathf.Lerp(hemRel + 0.003f, b.y - 0.003f, i / 30f);
                    var x = edgeX(y);
                    teeth.Add(sh.Front(nk.x + sign * (x + EdgeTeethIn), nk.y + y, out nrm, out w));
                    tape.Add(sh.Front(nk.x + sign * (x + EdgeTapeIn), nk.y + y, out nrm, out w));
                }
                Strap(o, sh, tape, false, f => EdgeTapeWidth, 0.0007f, 0, PartTape, TapeBand.y, TapeBand.yMax, 1f);
                Strap(o, sh, teeth, false, f => EdgeTeethWidth, 0.0016f, 3, PartMetal, 0f, 0.5f, 1f / TeethPitch, 0.0028f);
                if (right) Puller(o, sh, teeth[4], (teeth[0] - teeth[4]).normalized, 0.0012f, 0.8f);
            }
            parts.Add("前の縁のジッパー 2");

            // 胸のポケット（本人の右の胸、斜め）
            {
                var a = new Vector2(ChestZip.x, ChestZip.y);
                var b = new Vector2(ChestZip.z, ChestZip.w);
                var path = new List<Vector3>();
                for (var i = 0; i <= 8; i++)
                {
                    var q = Vector2.Lerp(a, b, i / 8f);
                    path.Add(sh.Front(nk.x + q.x, nk.y + q.y, out nrm, out w));
                }
                Zipper(o, sh, path, 0.0008f);
                Puller(o, sh, path[0], (path[path.Count - 1] - path[0]).normalized, 0.0008f, 0.7f);
            }
            // 腰の横のポケット（左右）
            foreach (var sign in new[] { 1f, -1f })
            {
                var path = new List<Vector3>();
                for (var i = 0; i <= 6; i++)
                {
                    var f = i / 6f;
                    var x = Mathf.Lerp(SideZip.x, SideZip.w, f);
                    var y = r.HemY + Mathf.Lerp(SideZip.z, SideZip.y, f);
                    path.Add(sh.Front(nk.x + sign * x, y, out nrm, out w));
                }
                Zipper(o, sh, path, 0.0008f);
                Puller(o, sh, path[0], (path[path.Count - 1] - path[0]).normalized, 0.0008f, 0.6f);
            }
            parts.Add("ポケットのジッパー 3");

            // 肩章
            for (var side = 0; side < 2; side++)
            {
                var sp = r.Shoulder(side);
                var outward = new Vector3(sp.x - nk.x, 0f, 0f).normalized;
                var path = new List<Vector3>();
                for (var i = 0; i <= 6; i++)
                {
                    var q = Vector3.Lerp(sp + outward * 0.010f, nk, EpauletReach * i / 6f) + up * 0.12f;
                    path.Add(sh.Snap(q, 0.2f, out nrm, out w));
                }
                Strap(o, sh, path, false, f => EpauletWidth * (f < 0.8f ? 1f : 1f - (f - 0.8f) / 0.2f * 0.75f), EpauletLift, 0, PartStrap, StrapBand.y, StrapBand.yMax, 1f);
                Stud(o, sh, Vector3.Lerp(path[path.Count - 1], path[path.Count - 2], 0.55f), EpauletLift);
            }
            parts.Add("肩章 2");

            // 袖口のジッパー（閉じた飾り。腕の外の少し後ろ）
            for (var side = 0; side < 2; side++)
            {
                var e = r.Elbow(side);
                var h = r.Wrist(side);
                var axis = (h - e).normalized;
                var outward = Vector3.ProjectOnPlane(new Vector3(side == 0 ? -1f : 1f, 0f, -CuffZipBack), axis).normalized;
                var len = (h - e).magnitude;
                var path = new List<Vector3>();
                for (var i = 0; i <= 6; i++)
                {
                    var a = len - CuffBeforeWrist - 0.004f - CuffZipLength * i / 6f;
                    path.Add(sh.Snap(e + axis * a + outward * 0.08f, 0.1f, out nrm, out w));
                }
                Zipper(o, sh, path, 0.0008f);
                Puller(o, sh, path[0] - axis * 0.004f, -axis, 0.0008f, 0.6f);
            }
            parts.Add("袖口のジッパー 2");

            // スナップ: 襟の先と襟返しの角（左右）。角から多角形の真ん中へ少し入れた所
            var collar = CollarFrontPoly();
            foreach (var right in new[] { true, false })
            {
                var sign = right ? 1f : -1f;
                var cp = CollarPoint + (Centroid(collar) - CollarPoint).normalized * 0.014f;
                Stud(o, sh, sh.Front(nk.x + sign * cp.x, nk.y + cp.y, out nrm, out w), FallLift);
                var lapel = LapelPoly(right);
                var lc = LapelCorner + (Centroid(lapel) - LapelCorner).normalized * 0.014f;
                Stud(o, sh, sh.Front(nk.x + sign * lc.x, nk.y + lc.y, out nrm, out w), FallLift);
            }
            parts.Add("スナップ 6（肩章 2・襟の先 2・襟返しの角 2）");
            return "飾り: " + string.Join("、", parts.ToArray());
        }

        // ---- テクスチャ ----------------------------------------------------------------

        const int LeatherSize = 2048;
        /// <summary>テクスチャの一画素のおおよその大きさ（m）。高さのテクスチャから法線のテクスチャを作るときに使う</summary>
        const float TexelMetres = 0.0005f;

        /// <summary>
        /// 革のテクスチャを描く（色と滑らかさのテクスチャ、法線のテクスチャ）と、ジッパーの務歯のテクスチャ。
        /// 身頃・袖・襟の画素は、UV の三角の中の位置から束ねた姿勢の面の点を求め、その点で描く（継ぎ目で柄がずれない）。
        /// 帯とジッパーの布の画素は、帯の UV（長さと幅）で描く
        /// </summary>
        static void PaintLeather(RocketboxPerson who, Out o, Look k, out string note)
        {
            const int N = LeatherSize;
            var col = new Color[N * N];
            var height = new float[N * N];
            var have = new bool[N * N];
            var painted = 0;
            var tris = o.sub[0];
            for (var t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                var part = o.tag[a];
                if (part == PartEdge || part == PartLining || part == PartMetal) continue;
                Vector2 ua = o.uv[a] * N, ub = o.uv[b] * N, uc = o.uv[c] * N;
                var area = (ub.x - ua.x) * (uc.y - ua.y) - (ub.y - ua.y) * (uc.x - ua.x);
                if (Mathf.Abs(area) < 1e-3f) continue;
                int x0 = Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x))) - 1, x1 = Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))) + 1;
                int y0 = Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y))) - 1, y1 = Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))) + 1;
                if (x1 - x0 > N || y1 - y0 > N) continue;
                for (var py = Mathf.Max(0, y0); py <= Mathf.Min(N - 1, y1); py++)
                    for (var px = x0; px <= x1; px++)
                    {
                        var q = new Vector2(px + 0.5f, py + 0.5f);
                        var l1 = ((ub.x - q.x) * (uc.y - q.y) - (ub.y - q.y) * (uc.x - q.x)) / area;
                        var l2 = ((uc.x - q.x) * (ua.y - q.y) - (uc.y - q.y) * (ua.x - q.x)) / area;
                        var l3 = 1f - l1 - l2;
                        const float slack = -0.08f;
                        if (l1 < slack || l2 < slack || l3 < slack) continue;
                        var inside = l1 >= 0f && l2 >= 0f && l3 >= 0f;
                        var X = ((px % N) + N) % N;
                        var idx = py * N + X;
                        if (have[idx] && !inside) continue;
                        l1 = Mathf.Clamp01(l1);
                        l2 = Mathf.Clamp01(l2);
                        l3 = Mathf.Clamp01(l3);
                        var sum = l1 + l2 + l3;
                        l1 /= sum;
                        l2 /= sum;
                        l3 /= sum;
                        var p = o.v[a] * l1 + o.v[b] * l2 + o.v[c] * l3;
                        var n = (o.n[a] * l1 + o.n[b] * l2 + o.n[c] * l3).normalized;
                        var uv = o.uv[a] * l1 + o.uv[b] * l2 + o.uv[c] * l3;
                        Color colour;
                        float h;
                        Leather(k, part, p, n, uv, out colour, out h);
                        col[idx] = colour;
                        height[idx] = h;
                        if (!have[idx]) painted++;
                        have[idx] = true;
                    }
            }
            // 描いていない画素は、となりの描いた画素で埋める（継ぎ目で縮小のミップマップが混ざらないように）
            for (var pass = 0; pass < 12; pass++)
            {
                var add = new List<int>();
                for (var y = 0; y < N; y++)
                    for (var x = 0; x < N; x++)
                    {
                        var i = y * N + x;
                        if (have[i]) continue;
                        var c = new Color(0f, 0f, 0f, 0f);
                        var h = 0f;
                        var m = 0;
                        if (x > 0 && have[i - 1]) { c += col[i - 1]; h += height[i - 1]; m++; }
                        if (x < N - 1 && have[i + 1]) { c += col[i + 1]; h += height[i + 1]; m++; }
                        if (y > 0 && have[i - N]) { c += col[i - N]; h += height[i - N]; m++; }
                        if (y < N - 1 && have[i + N]) { c += col[i + N]; h += height[i + N]; m++; }
                        if (m == 0) continue;
                        col[i] = c / m;
                        height[i] = h / m;
                        add.Add(i);
                    }
                foreach (var i in add) have[i] = true;
            }
            var normal = new Color[N * N];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    var i = y * N + x;
                    var hx = height[y * N + (x + 1) % N] - height[y * N + (x + N - 1) % N];
                    var hy = height[Mathf.Min(N - 1, y + 1) * N + x] - height[Mathf.Max(0, y - 1) * N + x];
                    var v = new Vector3(-hx / (2f * TexelMetres), -hy / (2f * TexelMetres), 1f).normalized;
                    normal[i] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
            WritePng(Folder(who) + "Leather.png", col, N, false);
            WritePng(Folder(who) + "LeatherNormal.png", normal, N, true);
            WriteTeeth(Folder(who) + "ZipperTeeth.png");
            note = string.Format(CultureInfo.InvariantCulture, "革のテクスチャ {0}×{0}（描いた画素 {1:0}%）", N, painted * 100f / (N * N));
        }

        /// <summary>革の一点の色（a に滑らかさ）と高さ（m）。part は部分、p と n は束ねた姿勢の面の点と法線、uv は UV</summary>
        static void Leather(Look k, byte part, Vector3 p, Vector3 n, Vector2 uv, out Color colour, out float height)
        {
            var r = k.r;
            var s = k.s;
            // 地: 細かいしぼ（1 mm ほど）と、大きなむら
            Vector3 q = p;
            if (part == PartStrap || part == PartTape)
                q = new Vector3(uv.x, uv.y * 1.2f, part * 0.37f);
            var grain = Soft(q * 1100f) * 0.6f + Soft(q * 2700f + new Vector3(3.1f, 1.7f, 9.2f)) * 0.4f;
            var mottle = Soft(q * 22f + new Vector3(5f, 1f, 2f));
            var baseCol = new Color(0.080f, 0.070f, 0.064f);
            // 艶のある滑らかな革: しぼは浅く、むらも弱く、滑らかさを高く
            var tone = 1f + (mottle - 0.5f) * 0.10f + (grain - 0.5f) * 0.05f;
            var c = baseCol * tone;
            var smooth = 0.62f + (grain - 0.5f) * 0.05f + (mottle - 0.5f) * 0.06f;
            var h = (grain - 0.5f) * 0.00003f;
            var wear = 0.06f;
            var stitch = 0f;
            var groove = 0f;

            if (part == PartTorso)
            {
                var cen = s.CentreAt(p.y);
                var th = Mathf.Atan2(p.x - cen.x, p.z - cen.y);
                var rad = new Vector2(p.x - cen.x, p.z - cen.y).magnitude;
                // 脇の縫い目（縦）
                var ds = (Mathf.Abs(th) - SideSeam) * rad;
                groove = Mathf.Max(groove, Line(ds, 0.0012f));
                stitch = Mathf.Max(stitch, Line(ds - 0.004f, 0.0006f) * Dash(p.y));
                // 背中の切り替え（横）
                if (Mathf.Abs(th) > SideSeam)
                {
                    var dy = p.y - (r.ArmpitY + YokeAboveArmpit);
                    groove = Mathf.Max(groove, Line(dy, 0.0012f));
                    stitch = Mathf.Max(stitch, Line(dy + 0.004f, 0.0006f) * Dash(th * rad));
                }
                // 前の縁の縫い目（身頃の側へ）
                if (p.z > cen.y)
                {
                    var de = -Inside(OpenPoly(r), new Vector2(p.x - r.Neck.x, p.y - r.Neck.y));
                    stitch = Mathf.Max(stitch, Line(de - 0.0085f, 0.0006f) * Dash(p.y));
                    wear += RocketboxPaint.Smooth(0.006f, 0f, de) * 0.5f;
                }
                // 裾の縫い目と擦れ
                var above = p.y - r.HemY;
                stitch = Mathf.Max(stitch, Line(above - 0.012f, 0.0006f) * Dash(th * rad));
                wear += RocketboxPaint.Smooth(0.01f, 0f, above) * 0.6f;
                // 脇のしわ（脇の下から斜め下へ）
                for (var side = 0; side < 2; side++)
                {
                    var sg = side == 0 ? -1f : 1f;
                    var pit = new Vector3(r.Neck.x + sg * 0.115f, r.ArmpitY - 0.01f, cen.y);
                    var dp = (p - pit).magnitude;
                    if (dp > 0.09f) continue;
                    var dir = new Vector3(-sg * 0.45f, -1f, 0f).normalized;
                    h += 0.0007f * Mathf.Sin(Vector3.Dot(p - pit, dir) / 0.016f * 2f * Mathf.PI + Noise(p * 18f) * 3f) * RocketboxPaint.Smooth(0.09f, 0.03f, dp);
                }
            }
            if (part == PartSleeveL || part == PartSleeveR)
            {
                var side = part == PartSleeveL ? 0 : 1;
                var sp = SleeveParam(r, side, p);
                var upper = (r.Elbow(side) - r.Shoulder(side)).magnitude;
                var full = upper + (r.Wrist(side) - r.Elbow(side)).magnitude;
                var al = sp.x * full;
                float alongDummy;
                var axisPt = OnArm(r, side, p, out alongDummy);
                var radial = (p - axisPt).normalized;
                var forward = Vector3.ProjectOnPlane(Vector3.forward, (r.Wrist(side) - r.Elbow(side)).normalized).normalized;
                var front = Vector3.Dot(radial, forward);
                // 肘の内のしわ
                var elbow = RocketboxPaint.Smooth(0.075f, 0.02f, Mathf.Abs(al - upper));
                h += 0.0008f * Mathf.Sin(al / 0.015f * 2f * Mathf.PI + Noise(p * 40f) * 4f) * elbow * RocketboxPaint.Smooth(-0.2f, 0.7f, front);
                // 袖口のたまり
                var toCuff = (full - CuffBeforeWrist) - al;
                h += 0.0003f * Mathf.Sin(al / 0.017f * 2f * Mathf.PI + Noise(p * 30f) * 3f) * RocketboxPaint.Smooth(0.08f, 0.02f, toCuff);
                // 袖口の縫い目と、袖の下の縫い目
                var circ = Mathf.Min(sp.y, 1f - sp.y) * 2f * Mathf.PI * 0.045f;
                stitch = Mathf.Max(stitch, Line(toCuff - 0.006f, 0.0006f) * Dash(sp.y * 0.28f));
                groove = Mathf.Max(groove, Line(circ, 0.0012f));
                stitch = Mathf.Max(stitch, Line(circ - 0.004f, 0.0006f) * Dash(al));
                // 肘の外と袖口の擦れ
                wear += elbow * RocketboxPaint.Smooth(0.2f, -0.7f, front) * 0.6f + RocketboxPaint.Smooth(0.02f, 0f, toCuff) * 0.8f;
            }
            if (part == PartTorso || part == PartSleeveL || part == PartSleeveR)
            {
                // 袖付けの縫い目: 肩の骨を通り、上腕の軸に直交する面のまわり
                for (var side = 0; side < 2; side++)
                {
                    var sp = r.Shoulder(side);
                    if ((p - sp).sqrMagnitude > 0.16f * 0.16f) continue;
                    var axis = (r.Elbow(side) - sp).normalized;
                    var d = Vector3.Dot(p - sp, axis) - ArmholeSeam;
                    var around = Vector3.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(p - sp, axis), axis) * Mathf.Deg2Rad * 0.06f;
                    groove = Mathf.Max(groove, Line(d, 0.0012f));
                    stitch = Mathf.Max(stitch, Line(d - 0.004f, 0.0006f) * Dash(around));
                    wear += RocketboxPaint.Smooth(0.06f, 0f, Mathf.Abs(d)) * RocketboxPaint.Smooth(0f, 0.05f, p.y - sp.y) * 0.4f;
                }
            }
            if (part == PartFall)
            {
                var g = k.ring.Count > 0 ? FallG(r, k.ring, p) : 0.02f;
                stitch = Mathf.Max(stitch, Line(g - 0.0045f, 0.0006f) * Dash((p.x + p.z) * 1.3f + p.y));
                wear += RocketboxPaint.Smooth(0.007f, 0f, g) * 0.8f;
                // 襟の折り目のそばのやわらかいしわ
                h += 0.0003f * Mathf.Sin(g / 0.012f * 2f * Mathf.PI + Noise(p * 20f) * 3f) * RocketboxPaint.Smooth(0.03f, 0.01f, g);
            }
            if (part == PartStand)
            {
                var sv = (uv.y - StandUv.y) / StandUv.height;
                var d = (sv - 0.82f) * 0.14f;
                stitch = Mathf.Max(stitch, Line(d, 0.0006f) * Dash(uv.x * 0.9f));
                wear += RocketboxPaint.Smooth(0.2f, 0.5f, sv) * RocketboxPaint.Smooth(0.8f, 0.5f, sv) * 0.8f;
            }
            if (part == PartStrap)
            {
                var sv = (uv.y - StrapBand.y) / StrapBand.height;
                var edge = Mathf.Min(sv, 1f - sv);
                stitch = Mathf.Max(stitch, Line((edge - 0.14f) * 0.04f, 0.0006f) * Dash(uv.x));
                wear += RocketboxPaint.Smooth(0.08f, 0f, edge) * 0.8f;
                h -= RocketboxPaint.Smooth(0.06f, 0f, edge) * 0.0003f;
            }
            if (part == PartTape)
            {
                var sv = (uv.y - TapeBand.y) / TapeBand.height;
                var weave = (Mathf.Sin(uv.x / 0.0007f * 2f * Mathf.PI) * Mathf.Sin(sv * 40f * Mathf.PI)) * 0.5f + 0.5f;
                c = new Color(0.045f, 0.043f, 0.046f) * (0.85f + weave * 0.3f);
                smooth = 0.15f;
                h = weave * 0.00008f;
                wear = 0f;
                stitch = Mathf.Max(stitch, Line((Mathf.Abs(sv - 0.5f) - 0.40f) * 0.013f, 0.0005f) * Dash(uv.x));
            }

            // 擦れ: 白っぽく褪せ、滑らかさが落ちる
            var scuff = RocketboxPaint.Smooth(0.5f, 0.8f, Soft(q * 55f + new Vector3(2f, 7f, 1f)) * 0.65f + Soft(q * 160f) * 0.35f) * Mathf.Clamp01(wear);
            c = Color.Lerp(c, new Color(0.23f, 0.21f, 0.195f), scuff * 0.35f);
            smooth = Mathf.Lerp(smooth, 0.35f, scuff);
            // 縫い目の溝（少し暗く、へこむ）と、糸（少し明るく、盛り上がる）
            c *= 1f - groove * 0.35f;
            h -= groove * 0.0005f;
            c = Color.Lerp(c, new Color(0.15f, 0.145f, 0.14f), stitch * 0.9f);
            h += stitch * 0.0003f;
            smooth = Mathf.Lerp(smooth, 0.3f, stitch);
            colour = new Color(c.r, c.g, c.b, Mathf.Clamp01(smooth));
            height = h;
        }

        /// <summary>脇の縫い目の角度（前の真ん中から。rad）、背中の切り替えの高さ（脇から上へ）、袖付けの縫い目（肩の骨から上腕の向きへ）</summary>
        const float SideSeam = 1.72f, YokeAboveArmpit = 0.035f, ArmholeSeam = 0.018f;

        /// <summary>距離 d の線の濃さ（半分の幅 half で 1、その外で 0 へ）</summary>
        static float Line(float d, float half)
        {
            return RocketboxPaint.Smooth(half * 1.6f, half * 0.6f, Mathf.Abs(d));
        }

        /// <summary>縫い目の糸の破線（長さの向きの位置 m から）</summary>
        static float Dash(float along)
        {
            var f = Mathf.Repeat(along / 0.0034f, 1f);
            return RocketboxPaint.Smooth(0.0f, 0.12f, f) * RocketboxPaint.Smooth(0.68f, 0.56f, f);
        }

        /// <summary>升目の向きが目立たないよう、向きを変えた二つの <see cref="Noise"/> を混ぜた物</summary>
        static float Soft(Vector3 p)
        {
            var a = Noise(new Vector3(0.80f * p.x + 0.60f * p.z, p.y, -0.60f * p.x + 0.80f * p.z));
            var b = Noise(new Vector3(p.x, 0.70f * p.y + 0.71f * p.z + 17.3f, -0.71f * p.y + 0.70f * p.z));
            return (a + b) * 0.5f;
        }

        static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            var a = Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx);
            var b = Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx);
            var c = Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx);
            var d = Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        /// <summary>ジッパーの務歯の繰り返しのテクスチャ（u が一組、v が幅。左右の歯が互い違いに噛み合う）</summary>
        static void WriteTeeth(string path)
        {
            const int W = 32, H = 32;
            var px = new Color[W * H];
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var u = (x + 0.5f) / W;
                    var v = (y + 0.5f) / H;
                    var left = u < 0.5f && v < 0.64f && Mathf.Abs(u - 0.25f) < 0.19f;
                    var right = u >= 0.5f && v > 0.36f && Mathf.Abs(u - 0.75f) < 0.19f;
                    var tooth = left || right;
                    var shade = tooth ? 0.78f + 0.12f * Mathf.Sin((u * 2f % 1f) * Mathf.PI) : 0.06f;
                    px[y * W + x] = new Color(shade, shade * 1.01f, shade * 1.04f, 1f);
                }
            WritePng(path, px, W, false);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }

        static void WritePng(string path, Color[] px, int n, bool normalMap)
        {
            var dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, normalMap);
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normalMap;
            imp.alphaSource = normalMap ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.maxTextureSize = n;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }

        // ---- 着せる ----------------------------------------------------------------

        /// <summary>
        /// 組み立てた人 her にジャケットを着せる（体と同じ骨で動く SkinnedMeshRenderer を、体の根の子の Jacket に置く）。
        /// worn が偽なら脱いだ形（レンダラーを消す）。メッシュが無ければ作る
        /// </summary>
        public static Garment Put(GameObject her, RocketboxPerson who, bool worn)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(who));
            if (mesh == null)
            {
                Debug.Log(Make(who));
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(who));
            }
            if (mesh == null) throw new InvalidOperationException("ジャケットのメッシュを作れない: " + MeshPath(who));
            var body = SkinPoint.BodyOf(her.transform);
            var old = her.transform.Find("Jacket");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Jacket");
            go.transform.SetParent(her.transform, false);
            go.transform.localPosition = her.transform.InverseTransformPoint(body.transform.position);
            go.transform.localRotation = Quaternion.Inverse(her.transform.rotation) * body.transform.rotation;
            go.transform.localScale = Vector3.one;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = body.bones;
            smr.rootBone = body.rootBone;
            smr.sharedMaterials = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>(LeatherPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(MetalPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(LiningPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(TeethPath(who)),
            };
            smr.updateWhenOffscreen = true;
            var garment = go.AddComponent<Garment>();
            garment.Set(new Renderer[] { smr }, worn);
            return garment;
        }

        // ---- 書く ----------------------------------------------------------------

        static void SaveMesh(Mesh mesh, string path)
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
                // 置き場の GUID を保つため、今のアセットへ中身を移す
                old.Clear();
                old.SetVertices(mesh.vertices);
                old.SetNormals(mesh.normals);
                if (mesh.tangents.Length == mesh.vertexCount) old.SetTangents(mesh.tangents);
                if (mesh.uv.Length == mesh.vertexCount) old.SetUVs(0, mesh.uv);
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

        static void SaveMaterials(RocketboxPerson who)
        {
            // 革: 色のテクスチャ（a に滑らかさ）と法線のテクスチャ
            var leather = Lit(LeatherPath(who), "Leather", Color.white, 0f, 1f);
            leather.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(who) + "Leather.png"));
            leather.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(who) + "LeatherNormal.png"));
            leather.SetFloat("_BumpScale", 1f);
            leather.EnableKeyword("_NORMALMAP");
            leather.SetFloat("_SmoothnessTextureChannel", 1f);
            leather.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            // 金具は差込口の輪と同じ銀
            Lit(MetalPath(who), "Metal", BuildProps.PortColour, 0.85f, 0.55f);
            Lit(LiningPath(who), "Lining", new Color(0.035f, 0.033f, 0.036f), 0f, 0.15f);
            var teeth = Lit(TeethPath(who), "ZipperTeeth", Color.white, 0.85f, 0.55f);
            teeth.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(who) + "ZipperTeeth.png"));
            AssetDatabase.SaveAssets();
        }

        static Material Lit(string path, string name, Color colour, float metallic, float smooth)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = m == null;
            if (fresh) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (fresh) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 道具 ----------------------------------------------------------------

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
    }
}
