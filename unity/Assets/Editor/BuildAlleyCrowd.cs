using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の群衆・ヤードの売り手・買い手を Rocketbox の人で組む（<c>docs/superpowers/specs/2026-09-25-alley-mob-design.md</c>）。
    ///
    /// 置き場所・向き・二人組は <see cref="BuildAlley"/> の群衆の決め方のまま受け取り、人と姿勢だけをここで決める。
    /// - 人: 通りの人 27 人（<see cref="RocketboxMob.Crowd"/>）と売り手 5 人（<see cref="RocketboxMob.Sellers"/>）を、同じ人が近くに並ばないよう選ぶ
    /// - 姿勢: 立つ・片脚に預ける・手を後ろで組む・振り向く・腕を組む・座る。骨を曲げた形を焼き、骨を持たない mesh にする
    /// - 服の色は元のまま。肌にはネオンのようなインプラント（<see cref="RocketboxMobPaint"/>）を、人ごと・置くたびに場所と色を変えて描く
    /// - 描く重さ: 一人ずつの物に分け（画面の外の人は描かれない）、近さの段（LODGroup）を二つ持たせる。
    ///   近く（10 m まで）は 1 人 4,500 三角ほど、その先は 1,000 三角ほど。三角を減らすのは UnityMeshSimplifier（組み立てのときだけ使う）
    ///
    /// 焼いた形・テクスチャ・マテリアルは <see cref="Folder"/>（リポジトリに入れない）へ組み立てのたびに作り直す
    /// </summary>
    public static class BuildAlleyCrowd
    {
        public const string Folder = BuildAlley.Generated + "Crowd/";

        /// <summary>姿勢。立ちは Humanoid の立ちの動き（<see cref="BodyPoser.Stand"/>）の初めのこまから曲げる</summary>
        public enum Pose
        {
            /// <summary>立つ</summary>
            Stand,
            /// <summary>重心を片脚に預ける</summary>
            Rest,
            /// <summary>手を後ろで組む</summary>
            Behind,
            /// <summary>少し振り向く</summary>
            Turn,
            /// <summary>腕を組む</summary>
            Crossed,
            /// <summary>腰掛けに座る（売り手）</summary>
            Sit,
            /// <summary>椅子に座る（露天席）。腰掛けより座面が 3.5 cm 高い</summary>
            SitChair,
        }

        /// <summary>人を一人立たせる所。at は足元、yaw は向き（度）、scale は背の伸び縮み</summary>
        public struct Place
        {
            public Vector3 at;
            public float yaw;
            public Pose pose;
            /// <summary>売り手の席（出店の奥の腰掛け）</summary>
            public bool seller;
            public float scale;
        }

        /// <summary>近くの段の三角の数（1 人）</summary>
        public const int NearTriangles = 4500;
        /// <summary>中ほどの段の三角の数（1 人）</summary>
        public const int MidTriangles = 1000;
        /// <summary>近くの段から中ほどの段へ替わる距離（m）。WebGL の画質（Mobile、LOD の寄せ 1）での値。エディタの画質（PC、寄せ 2）では倍の距離で替わる</summary>
        public const float NearUntil = 10f;
        /// <summary>同じ人を置く間合い（m）。通りでは、これより近くに同じ人を置かない</summary>
        public const float SameApart = 22f;
        /// <summary>ヤードでの同じ人の間合い（m）。ヤードは 16 × 18 m に 30 人ほどが入り、通りの人 27 人では足りないので、何人かは二度出る。そのときの間合い</summary>
        public const float YardApart = 12f;
        /// <summary>売り手どうしの間合い（ヤードは狭い）</summary>
        public const float SellerApart = 8f;
        /// <summary>光の色を被らせない、となりの人の間合い（m）</summary>
        public const float ColourApart = 5f;

        /// <summary>腰掛けの天面の高さ（BuildAlley の腰掛け）</summary>
        public const float StoolTop = 0.425f;
        /// <summary>椅子の座面の天面の高さ</summary>
        public const float ChairTop = BuildAlley.SeatHigh + 0.025f;

        /// <summary>光の強さ（自己発光のテクスチャに掛ける）。ブルーム（しきい 0.85）に掛かって、2〜3 m で光の点や線として拾える強さ</summary>
        public const float GlowStrength = 3.0f;

        /// <summary>
        /// 買い手の色の濃さ（体・頭・髪の房の _BaseColor に掛ける）。
        /// 買い手は自分の露店の卓の真上の豆電球（Bulb4、強さ 17）から 1.2〜1.5 m の所に立つ。群衆の人は豆電球から 3 m より遠いので、
        /// 買い手の顔はその 5〜10 倍の光を受け、Rocketbox の肌や服（テクスチャの明るさ 0.4〜0.7）のままだと顔も服も白く飛んだ。
        /// 前の Quaternius の買い手は 0.07〜0.09 の暗い色で、この明かりでちょうど見えていた。
        /// 0.35 で、目鼻が読めて服の色も残り、群衆より少しはっきり見える（0.22 では沈み気味、0.14 では暗い灰色の人になる）。
        /// 自己発光（インプラント）には掛からない
        /// </summary>
        public const float BuyerTone = 0.35f;

        /// <summary>光の色の並び（通りの看板のネオンの色）</summary>
        static readonly Color[] Palette =
        {
            RocketboxMobPaint.Pink, RocketboxMobPaint.Cyan, RocketboxMobPaint.Purple, RocketboxMobPaint.Red,
            RocketboxMobPaint.Orange, RocketboxMobPaint.Teal, RocketboxMobPaint.Yellow, RocketboxMobPaint.Green,
            RocketboxMobPaint.Rose, RocketboxMobPaint.Blue, RocketboxMobPaint.Blush, RocketboxMobPaint.Amber,
        };

        static readonly string[] PaletteNames = { "桃", "水色", "紫", "赤", "橙", "青緑", "黄", "緑", "薔薇", "青", "淡い桃", "山吹" };

        // ---- 組み立て ------------------------------------------------------------

        /// <summary>一人を置いた一回分</summary>
        sealed class Appearance
        {
            public RocketboxMob who;
            public Place place;
            /// <summary>同じ人の何回目か（0 から）</summary>
            public int nth;
            public readonly List<RocketboxMobPaint.Implant> implants = new List<RocketboxMobPaint.Implant>();
            public string Key { get { return who.Name + "_" + nth; } }
        }

        /// <summary>
        /// 群衆を組む。parent（Alley/Crowd）の下に People を作り、一人ずつ LODGroup を持つ物を置く。
        /// places は BuildAlley の群衆の決め方で決めた所（二人組もそのまま）
        /// </summary>
        public static void Build(Transform parent, List<Place> places, int seed, StringBuilder sb)
        {
            EnsureFolder();
            Begin();
            try
            {
                var rng = new System.Random(seed);
                var apps = Assign(places, rng);
                ChooseImplants(apps, rng);
                ChooseColours(apps, rng);

                var old = parent.Find("People");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var group = new GameObject("People").transform;
                group.SetParent(parent, false);
                var tris = 0;
                for (var i = 0; i < apps.Count; i++)
                {
                    int n0, n1;
                    Make(group, i, apps[i], out n0, out n1);
                    tris += n0;
                }
                Report(apps, sb, tris);
            }
            finally
            {
                End();
            }
        }

        /// <summary>
        /// 買い手を一人作る（群衆と同じ人の作りで、段を持たない一つの形）。
        /// ふだんは群衆と同じ透かさないマテリアルで描く。自分の番に AlleyDirector が出して濃さを上げるあいだだけ、
        /// 透かせるマテリアルへ差し替える（<see cref="BuyerFade"/>）。
        /// 透かせる側のまま置くと深さを書かないので、口の中や後ろ頭の髪の塗りが顔の上に描かれ、顔が崩れた（女大 15 は顔が上下逆に見えた）
        /// </summary>
        public static GameObject Buyer(Transform parent, string name, RocketboxMob who, Pose pose, Vector3 at, float yaw, int seed, StringBuilder sb)
        {
            EnsureFolder();
            Begin();
            try
            {
                var rng = new System.Random(seed);
                var a = new Appearance { who = who, place = new Place { at = at, yaw = yaw, pose = pose, scale = 1f }, nth = 0 };
                PickKinds(a, Prep(who), rng, new List<Appearance>());
                var used = new List<Color>();
                for (var i = 0; i < a.implants.Count; i++)
                {
                    var im = a.implants[i];
                    im.color = Pick(rng, used, used);
                    used.Add(im.color);
                    a.implants[i] = im;
                }
                var mats = Materials(a, 512, true, "Buyer");
                // 浮かび上がるあいだの透かせる組。髪の房は切り抜きのまま（濃さの α で切り抜かれて現れる）
                var fade = new Material[mats.Length];
                for (var i = 0; i < mats.Length; i++)
                {
                    var path = AssetDatabase.GetAssetPath(mats[i]);
                    fade[i] = mats[i].IsKeywordEnabled("_ALPHATEST_ON") ? mats[i]
                        : Save(Faded(mats[i]), path.Substring(0, path.Length - ".mat".Length) + "_Fade.mat");
                }
                var mesh = Full(who, pose);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<BuyerFade>().Bind(mats, fade);
                if (sb != null) sb.AppendFormat("{0}: {1}（{2}、{3}）、{4} 三角", name, who.Label, pose, Describe(a), mesh.triangles.Length / 3).AppendLine();
                return go;
            }
            finally
            {
                End();
            }
        }

        // ---- 人の選び ---------------------------------------------------------

        /// <summary>
        /// 置き場所ごとに人を決める。通りとヤードは壁で隔てられていて一度に見えないので、分けて数える（小路は両方から覗けるので、どちらとも同じ側に数える。<see cref="SameSide"/>）。
        /// 置く順をばらし、一度に見えうる側でまだ出ていない人、次に全体で使った回数の少ない人から、
        /// 一度に見えうる側で同じ人がすでに置かれた所から十分離れている人を選ぶ。離れた人がいなければ、いちばん離れた人
        /// </summary>
        static List<Appearance> Assign(List<Place> places, System.Random rng)
        {
            var order = new List<int>();
            for (var i = 0; i < places.Count; i++) order.Add(i);
            Shuffle(order, rng);
            var at = new Dictionary<RocketboxMob, List<Vector3>>();
            var made = new Appearance[places.Count];
            foreach (var i in order)
            {
                var pl = places[i];
                var yard = pl.at.x < BuildAlley.LaneWest + 0.5f;
                var src = pl.seller ? RocketboxMob.Sellers : RocketboxMob.Crowd;
                var pool = new List<KeyValuePair<RocketboxMob, int>>();
                foreach (var w in src)
                {
                    var side = 0;
                    List<Vector3> l;
                    if (at.TryGetValue(w, out l)) foreach (var q in l) if (SameSide(q, pl.at)) side++;
                    // 同じ側で出た回数を先に、全体の回数を次に、あとはばらす
                    pool.Add(new KeyValuePair<RocketboxMob, int>(w, side * 10000 + Uses(at, w) * 100 + rng.Next(100)));
                }
                pool.Sort((a, b) => a.Value.CompareTo(b.Value));
                var apart = pl.seller ? SellerApart : yard ? YardApart : SameApart;
                RocketboxMob pick = null, far = null;
                var farD = -1f;
                foreach (var kv in pool)
                {
                    var w = kv.Key;
                    // 買い手 C（女大 15）は通りにだけ出す。露店の卓から買い手を見ると、その先に小路の口とヤードが見えるので、同じ人が背後に並んで見える
                    if (w == RocketboxMob.Adult15F && Side(pl.at) != 0) continue;
                    var d = Nearest(at, w, pl.at);
                    if (d >= apart) { pick = w; break; }
                    if (d > farD) { farD = d; far = w; }
                }
                if (pick == null) pick = far;
                List<Vector3> list;
                if (!at.TryGetValue(pick, out list)) at[pick] = list = new List<Vector3>();
                made[i] = new Appearance { who = pick, place = pl, nth = list.Count };
                list.Add(pl.at);
            }
            // 何回目かは、通りの南から数える（組み立てのたびに同じ名前になるように、置いた順ではなく場所で）
            var byWho = new Dictionary<RocketboxMob, List<Appearance>>();
            foreach (var a in made)
            {
                List<Appearance> l;
                if (!byWho.TryGetValue(a.who, out l)) byWho[a.who] = l = new List<Appearance>();
                l.Add(a);
            }
            foreach (var l in byWho.Values)
            {
                l.Sort((x, y) => (x.place.at.z * 1000f + x.place.at.x).CompareTo(y.place.at.z * 1000f + y.place.at.x));
                for (var k = 0; k < l.Count; k++) l[k].nth = k;
            }
            return new List<Appearance>(made);
        }

        /// <summary>通り（0）・小路（1）・ヤード（2）のどこか</summary>
        static int Side(Vector3 p) { return p.x > -BuildAlley.StreetHalf - 0.5f ? 0 : p.x < BuildAlley.LaneWest + 0.5f ? 2 : 1; }

        /// <summary>
        /// 一度に見えうる側どうしか。通りとヤードは壁で隔てられていて一度に見えない。
        /// 小路は両方から覗けるので、どちらとも同じ側に数える
        /// </summary>
        static bool SameSide(Vector3 a, Vector3 b)
        {
            int sa = Side(a), sb = Side(b);
            return sa == sb || sa == 1 || sb == 1;
        }

        static int Uses(Dictionary<RocketboxMob, List<Vector3>> at, RocketboxMob w)
        {
            List<Vector3> l;
            return at.TryGetValue(w, out l) ? l.Count : 0;
        }

        /// <summary>一度に見えうる側で、同じ人がすでに置かれたいちばん近い所までの距離</summary>
        static float Nearest(Dictionary<RocketboxMob, List<Vector3>> at, RocketboxMob w, Vector3 p)
        {
            List<Vector3> l;
            if (!at.TryGetValue(w, out l)) return float.MaxValue;
            var best = float.MaxValue;
            foreach (var q in l) if (SameSide(q, p)) best = Mathf.Min(best, Flat(q - p));
            return best;
        }

        static float Flat(Vector3 v) { return new Vector2(v.x, v.z).magnitude; }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        // ---- インプラントの選び -----------------------------------------------------

        /// <summary>インプラントの置き所の組（顔・首・腕）。一人に顔の物を一つと、首か腕の物を一つか二つ</summary>
        enum Spot { Face, Neck, Arm }

        static Spot SpotOf(RocketboxMobPaint.Kind k)
        {
            switch (k)
            {
                case RocketboxMobPaint.Kind.TempleLines:
                case RocketboxMobPaint.Kind.EyeRing: return Spot.Face;
                case RocketboxMobPaint.Kind.NapePort:
                case RocketboxMobPaint.Kind.NeckRing: return Spot.Neck;
                default: return Spot.Arm;
            }
        }

        /// <summary>選べる形の候補（左右のある物は左右それぞれ）</summary>
        static IEnumerable<RocketboxMobPaint.Implant> Candidates(bool bold)
        {
            foreach (RocketboxMobPaint.Kind k in System.Enum.GetValues(typeof(RocketboxMobPaint.Kind)))
            {
                if (k == RocketboxMobPaint.Kind.NapePort || k == RocketboxMobPaint.Kind.NeckRing)
                {
                    yield return new RocketboxMobPaint.Implant(k, false, Color.white, bold);
                    continue;
                }
                // 目のまわりの輪には小さい形が無い
                if (!bold && k == RocketboxMobPaint.Kind.EyeRing) continue;
                yield return new RocketboxMobPaint.Implant(k, true, Color.white, bold);
                yield return new RocketboxMobPaint.Implant(k, false, Color.white, bold);
            }
        }

        /// <summary>肌に載る比がこれより小さい形は、その人には選ばない（襟・袖・髪・手袋に隠れる）</summary>
        const float MinOnSkin = 0.55f;
        /// <summary>売り手の小さいこめかみの線は短く、髪の生え際にかかりやすいので、半分ほど肌に載れば選ぶ（肌でない所には塗らない）</summary>
        const float MinOnSkinSmallTemple = 0.4f;

        static void ChooseImplants(List<Appearance> apps, System.Random rng)
        {
            // 置いた順に。同じ人の二回目は、一回目と違う組み合わせにする
            var done = new List<Appearance>();
            foreach (var a in apps)
            {
                PickKinds(a, Prep(a.who), rng, done);
                done.Add(a);
            }
        }

        /// <summary>その人の肌の出ている所に合う形から選ぶ。通りの人と買い手は大胆な形を二つか三つ、売り手は小さい形を一つ</summary>
        static void PickKinds(Appearance a, Person per, System.Random rng, List<Appearance> before)
        {
            var seller = a.who.Part == RocketboxMob.Role.Seller;
            var fits = seller ? per.small : per.bold;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                a.implants.Clear();
                if (seller)
                {
                    // 売り手は小さく一つ。顔（こめかみ）があれば顔、無ければ首、無ければ手の甲（手の甲の小さい形は点ほどで、遠くでは見えない）
                    foreach (var k in new[] { RocketboxMobPaint.Kind.TempleLines, RocketboxMobPaint.Kind.NeckRing, RocketboxMobPaint.Kind.HandGlow, RocketboxMobPaint.Kind.NapePort, RocketboxMobPaint.Kind.ForearmPlate })
                    {
                        var of = fits.FindAll(im => im.kind == k);
                        if (of.Count == 0) continue;
                        a.implants.Add(of[rng.Next(of.Count)]);
                        break;
                    }
                }
                else
                {
                    var face = fits.FindAll(im => SpotOf(im.kind) == Spot.Face);
                    var neck = fits.FindAll(im => SpotOf(im.kind) == Spot.Neck);
                    var arm = fits.FindAll(im => SpotOf(im.kind) == Spot.Arm);
                    if (face.Count > 0) a.implants.Add(face[rng.Next(face.Count)]);
                    var more = rng.NextDouble() < 0.35 ? 2 : 1;
                    var rest = new List<List<RocketboxMobPaint.Implant>>();
                    if (neck.Count > 0) rest.Add(neck);
                    if (arm.Count > 0) rest.Add(arm);
                    if (face.Count == 0) more = 2;
                    // 首の物を先に（手の甲より遠くから見える）。三割ほどは腕を先にする
                    if (rest.Count == 2 && rng.NextDouble() < 0.3) rest.Reverse();
                    for (var i = 0; i < rest.Count && i < more; i++) a.implants.Add(rest[i][rng.Next(rest[i].Count)]);
                }
                // 同じ人のそれまでの回と、形と左右の組が同じなら選び直す（色も変わるが、場所も変えたい）
                var same = false;
                foreach (var b in before)
                    if (b.who == a.who && SameKinds(a, b)) same = true;
                if (!same) return;
            }
        }

        static bool SameKinds(Appearance a, Appearance b)
        {
            if (a.implants.Count != b.implants.Count) return false;
            foreach (var x in a.implants)
            {
                var hit = false;
                foreach (var y in b.implants)
                    if (x.kind == y.kind && x.left == y.left) hit = true;
                if (!hit) return false;
            }
            return true;
        }

        /// <summary>
        /// 光の色を決める。一人の中では部位ごとに違う色。近く（<see cref="ColourApart"/>）の人とは色を被らせない。
        /// 顔の物の色は特に、となりの人の顔の色と被らせない
        /// </summary>
        static void ChooseColours(List<Appearance> apps, System.Random rng)
        {
            var done = new List<Appearance>();
            foreach (var a in apps)
            {
                var near = new List<Color>();
                var nearFace = new List<Color>();
                foreach (var b in done)
                {
                    if (Flat(b.place.at - a.place.at) > ColourApart) continue;
                    foreach (var im in b.implants)
                    {
                        near.Add(im.color);
                        if (SpotOf(im.kind) == Spot.Face) nearFace.Add(im.color);
                    }
                }
                // 同じ人の前の回の色も避ける（二回目は色も変える）
                foreach (var b in done)
                    if (b.who == a.who)
                        foreach (var im in b.implants) near.Add(im.color);
                var mine = new List<Color>();
                for (var i = 0; i < a.implants.Count; i++)
                {
                    var im = a.implants[i];
                    var avoid = new List<Color>(near);
                    avoid.AddRange(mine);
                    var must = new List<Color>(nearFace);
                    must.AddRange(mine);
                    im.color = Pick(rng, avoid, must);
                    mine.Add(im.color);
                    a.implants[i] = im;
                }
                done.Add(a);
            }
        }

        /// <summary>avoid に無い色から選ぶ。尽きたら must（顔の色と自分の色）だけを避ける</summary>
        static Color Pick(System.Random rng, List<Color> avoid, List<Color> must)
        {
            var free = new List<Color>();
            foreach (var c in Palette) if (!avoid.Contains(c)) free.Add(c);
            if (free.Count == 0) foreach (var c in Palette) if (!must.Contains(c)) free.Add(c);
            if (free.Count == 0) free.AddRange(Palette);
            return free[rng.Next(free.Count)];
        }

        static string ColourName(Color c)
        {
            for (var i = 0; i < Palette.Length; i++) if (Palette[i] == c) return PaletteNames[i];
            return ColorUtility.ToHtmlStringRGB(c);
        }

        static string Describe(Appearance a)
        {
            if (a.implants.Count == 0) return "インプラント無し";
            var parts = new List<string>();
            foreach (var im in a.implants) parts.Add(im + " " + ColourName(im.color));
            return string.Join("・", parts.ToArray());
        }

        static void Report(List<Appearance> apps, StringBuilder sb, int nearTris)
        {
            if (sb == null) return;
            var count = new Dictionary<RocketboxMob, int>();
            var closest = float.MaxValue;
            RocketboxMob closestWho = null;
            for (var i = 0; i < apps.Count; i++)
            {
                int c;
                count.TryGetValue(apps[i].who, out c);
                count[apps[i].who] = c + 1;
                for (var j = i + 1; j < apps.Count; j++)
                {
                    // 通りとヤードは一度に見えないので、同じ側どうしだけを比べる
                    if (apps[j].who != apps[i].who || !SameSide(apps[j].place.at, apps[i].place.at)) continue;
                    var d = Flat(apps[i].place.at - apps[j].place.at);
                    if (d < closest) { closest = d; closestWho = apps[i].who; }
                }
            }
            sb.AppendFormat("群衆 {0} 人（{1} 人を使った）、近くの段で {2} 三角", apps.Count, count.Count, nearTris).AppendLine();
            if (closestWho != null) sb.AppendFormat("一度に見えうる側（通り・小路・ヤード）での同じ人のいちばん近い間合い: {0} の {1:F1} m", closestWho.Label, closest).AppendLine();
            for (var i = 0; i < apps.Count; i++)
            {
                var a = apps[i];
                sb.AppendFormat("P{0:00} {1}（{2} 回目）{3} ({4:F1}, {5:F1}): {6}", i, a.who.Label, a.nth + 1, a.place.pose, a.place.at.x, a.place.at.z, Describe(a)).AppendLine();
            }
        }

        // ---- 一人の下ごしらえ（組み立て一回のあいだ使い回す） ----------------------------

        /// <summary>一人の、面の地図・骨の位置・肌の見本と、その人に合う形</summary>
        sealed class Person
        {
            public RocketboxMob who;
            public RocketboxMobPaint.Frame frame;
            /// <summary>FBX の面の組の順のマテリアルの名前（体・頭・髪の房の順とは限らない）</summary>
            public string[] slots;
            public RocketboxMobPaint.Skin cloud;
            public readonly Dictionary<int, RocketboxPaint.Surface> head = new Dictionary<int, RocketboxPaint.Surface>();
            public readonly Dictionary<int, RocketboxPaint.Surface> body = new Dictionary<int, RocketboxPaint.Surface>();
            public readonly Dictionary<int, Color[]> headPx = new Dictionary<int, Color[]>();
            public readonly Dictionary<int, Color[]> bodyPx = new Dictionary<int, Color[]>();
            public readonly List<RocketboxMobPaint.Implant> bold = new List<RocketboxMobPaint.Implant>();
            public readonly List<RocketboxMobPaint.Implant> small = new List<RocketboxMobPaint.Implant>();
        }

        static Dictionary<RocketboxMob, Person> people;
        /// <summary>組み立ての間に焼き直した形。使う形を一度ずつ焼き直す（姿勢の値を直したら組み直すだけで反映されるように）</summary>
        static HashSet<string> fresh;

        static void Begin()
        {
            people = new Dictionary<RocketboxMob, Person>();
            if (fresh == null) fresh = new HashSet<string>();
        }

        static void End()
        {
            people = null;
        }

        /// <summary>
        /// 群衆を組み始めるとき（BuildAlley の組み立ての頭）に呼ぶ。焼き直した形の覚えを空にする
        /// </summary>
        public static void Reset()
        {
            fresh = new HashSet<string>();
        }

        static Person Prep(RocketboxMob who)
        {
            Person per;
            if (people.TryGetValue(who, out per)) return per;
            per = new Person { who = who };
            // 線を落とす面は細かい地図（512）で持つ（256 では手の甲の画素が 8 mm ほどになり、筋から外れる）
            Surfaces(per, 512);
            per.cloud = new RocketboxMobPaint.Skin();
            per.cloud.Add(per.head[512]);
            per.cloud.Add(per.body[512]);
            // 形ごとに、肌に載る比を 256 の地図で測る
            Surfaces(per, 256);
            var skin = SkinOf(per, 256);
            foreach (var bold in new[] { true, false })
                foreach (var im in Candidates(bold))
                {
                    var strokes = RocketboxMobPaint.Strokes(im, per.frame);
                    RocketboxMobPaint.Prepare(strokes, per.cloud);
                    float on = 0f, total = 0f;
                    RocketboxMobPaint.Coverage(strokes, per.head[256], Pixels(per, 256, true), skin, ref on, ref total);
                    RocketboxMobPaint.Coverage(strokes, per.body[256], Pixels(per, 256, false), skin, ref on, ref total);
                    var need = !bold && im.kind == RocketboxMobPaint.Kind.TempleLines ? MinOnSkinSmallTemple : MinOnSkin;
                    if (total > 0f && on / total >= need) (bold ? per.bold : per.small).Add(im);
                }
            people[who] = per;
            return per;
        }

        static Color SkinOf(Person per, int n)
        {
            return RocketboxMobPaint.SkinOf(per.head[n], Pixels(per, n, true), per.frame);
        }

        /// <summary>頭か体の元の色（n×n）。取り込んだ PNG を n へ縮める</summary>
        static Color[] Pixels(Person per, int n, bool head)
        {
            var cache = head ? per.headPx : per.bodyPx;
            Color[] px;
            if (cache.TryGetValue(n, out px)) return px;
            int w, h;
            var src = RocketboxTextures.ReadPng(head ? per.who.HeadSrc : per.who.BodySrc, out w, out h);
            if (w != n)
            {
                if (w < n || w % n != 0 || h != w) throw new System.InvalidOperationException("テクスチャの大きさが合わない（" + w + " を " + n + " へ）: " + per.who.Name);
                src = RocketboxTextures.DownsampleBy(src, w, h, w / n, false);
            }
            px = BuildRocketboxProtagonist.ToColors(src);
            cache[n] = px;
            return px;
        }

        /// <summary>模型の束ねた姿勢の、頭と体の面の位置の地図（n×n）と、インプラントを決める骨の位置</summary>
        static bool Surfaces(Person per, int n)
        {
            if (per.head.ContainsKey(n)) return true;
            var who = per.who;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Alley crowd/Import the people）: " + who.Model);
            var go = (GameObject)Object.Instantiate(src);
            go.hideFlags = HideFlags.HideAndDontSave;
            var baked = new Mesh();
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                for (var i = 0; i < v.Length; i++) v[i] = go.transform.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                var uv = smr.sharedMesh.uv;
                int hi = Slot(smr, who.HeadSlot), bi = Slot(smr, who.BodySlot);
                if (hi < 0 || bi < 0) throw new System.InvalidOperationException("頭か体の面の組が無い: " + who.Name);
                per.head[n] = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(hi), n);
                per.body[n] = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(bi), n);
                if (per.frame == null) per.frame = RocketboxMobPaint.Frame.Of(go.GetComponent<Animator>());
                if (per.slots == null)
                {
                    var mats = smr.sharedMaterials;
                    per.slots = new string[mats.Length];
                    for (var i = 0; i < mats.Length; i++) per.slots[i] = mats[i] != null ? mats[i].name : "";
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(go);
            }
        }

        static int Slot(SkinnedMeshRenderer smr, string name)
        {
            var slots = smr.sharedMaterials;
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].name == name) return i;
            return -1;
        }

        // ---- 一人を置く ------------------------------------------------------------

        static void Make(Transform parent, int index, Appearance a, out int nearTris, out int midTris)
        {
            var seller = a.who.Part == RocketboxMob.Role.Seller;
            var mats = Materials(a, seller ? 512 : 256, false, a.nth.ToString());
            var near = Lod(a.who, a.place.pose, 0);
            var mid = Lod(a.who, a.place.pose, 1);
            nearTris = near.triangles.Length / 3;
            midTris = mid.triangles.Length / 3;

            var go = new GameObject(string.Format("P{0:00}_{1}", index, a.who.Name));
            go.transform.SetParent(parent, false);
            go.transform.position = a.place.at;
            go.transform.rotation = Quaternion.Euler(0f, a.place.yaw, 0f);
            go.transform.localScale = Vector3.one * a.place.scale;
            var r0 = Part(go.transform, "LOD0", near, mats);
            var r1 = Part(go.transform, "LOD1", mid, mats);
            var lod = go.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.SetLODs(new[] { new LOD(0.5f, new Renderer[] { r0 }), new LOD(0.01f, new Renderer[] { r1 }) });
            lod.RecalculateBounds();
            lod.SetLODs(new[]
            {
                new LOD(Relative(lod.size, NearUntil), new Renderer[] { r0 }),
                // 中ほどの段は通りの先まで消さない（遠くの板は入れない）
                new LOD(Relative(lod.size, 250f), new Renderer[] { r1 }),
            });
        }

        static Renderer Part(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            // 今の群衆と同じく影は落とさない（灯りは影を持たない）
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        /// <summary>
        /// 距離 d で段が替わる、画面の高さに対する比（LODGroup の値）。
        /// Unity は大きさ size の物を、size / 2 ÷（距離 × tan(視野角 / 2)）で測り、画質の LOD の寄せ（WebGL の Mobile は 1）を掛けて比べる
        /// </summary>
        static float Relative(float size, float d)
        {
            var fov = Camera.main != null ? Camera.main.fieldOfView : 70f;
            return size * 0.5f / (d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
        }

        // ---- 焼いた形 --------------------------------------------------------------

        static string MeshPath(RocketboxMob who, Pose pose, string tail) { return Folder + who.Name + "_" + pose + tail + ".asset"; }

        /// <summary>段ごとの形（0 は近く、1 は中ほど）。組み立ての間に一度だけ焼き直す</summary>
        static Mesh Lod(RocketboxMob who, Pose pose, int level)
        {
            var path = MeshPath(who, pose, "_LOD" + level);
            if (!fresh.Contains(path))
            {
                var full = Posed(who, pose);
                var slots = Prep(who).slots;
                try
                {
                    foreach (var l in new[] { 0, 1 })
                    {
                        var m = Simplify(full, slots, who, l == 0 ? NearTriangles : MidTriangles, l == 0, who.Name + "_" + pose + "_LOD" + l);
                        var p = MeshPath(who, pose, "_LOD" + l);
                        RocketboxJacket.SaveMesh(m, p);
                        fresh.Add(p);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(full);
                }
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>減らさない形（買い手）</summary>
        static Mesh Full(RocketboxMob who, Pose pose)
        {
            var path = MeshPath(who, pose, "");
            var m = Posed(who, pose);
            RocketboxJacket.SaveMesh(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>
        /// 三角を target まで減らす（UnityMeshSimplifier）。面の組（体・頭・髪の房）ごとに分けて減らし、一つに戻す。
        /// まとめて減らすと、組ごとの減り方が人によってばらけた（頭が 3 割まで減って顔が崩れる人と、体ばかり減る人がいた）。
        /// 髪の房は、近くの段では板の縁（開いた縁）を残す（縁を崩すと房が縮んで、髪が薄くなって見える）。
        /// 頭と体の境（首）はどちらの組でも開いた縁なので、縁を残して継ぎ目が割れないようにする
        /// </summary>
        static Mesh Simplify(Mesh full, string[] slots, RocketboxMob who, int target, bool near, string name)
        {
            var subs = full.subMeshCount;
            var counts = new int[subs];
            var hair = -1;
            var headI = -1;
            for (var i = 0; i < subs; i++)
            {
                counts[i] = full.GetTriangles(i).Length / 3;
                if (i < slots.Length && slots[i] == who.HairSlot) hair = i;
                if (i < slots.Length && slots[i] == who.HeadSlot) headI = i;
            }
            // 髪の房は、近くで 700、中ほどで 220 まで。残りを頭と体に分ける（頭は顔の細かさのぶん少し多めに）
            var hairBudget = hair >= 0 ? Mathf.Min(counts[hair], near ? 700 : 220) : 0;
            var rest = target - hairBudget;
            var headShare = near ? 0.48f : 0.45f;
            var verts = full.vertices;
            var norms = full.normals;
            var uvs = full.uv;
            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outU = new List<Vector2>();
            var outT = new List<int[]>();
            for (var i = 0; i < subs; i++)
            {
                int budget;
                if (i == hair) budget = hairBudget;
                else if (i == headI) budget = Mathf.RoundToInt(rest * headShare);
                else budget = Mathf.RoundToInt(rest * (1f - headShare));
                var part = new Mesh();
                part.SetVertices(verts);
                part.SetNormals(norms);
                part.SetUVs(0, uvs);
                part.SetTriangles(full.GetTriangles(i), 0);
                Mesh done = part;
                if (budget < counts[i])
                {
                    var ms = new MeshSimplifier();
                    var o = SimplificationOptions.Default;
                    // 中ほどの段は、頭の縁（首の継ぎ目）だけを残す（10 m より先なら、体の縁が少し動いても見えない）
                    o.PreserveBorderEdges = near || i == headI;
                    o.PreserveUVSeamEdges = false;
                    o.PreserveUVFoldoverEdges = true;
                    o.EnableSmartLink = true;
                    o.MaxIterationCount = 200;
                    ms.SimplificationOptions = o;
                    ms.Initialize(part);
                    ms.SimplifyMesh(Mathf.Clamp01(budget / (float)counts[i]));
                    done = ms.ToMesh();
                }
                // 使っている頂点だけを写す
                var map = new Dictionary<int, int>();
                var tri = done.GetTriangles(0);
                // 髪の房の板は一枚ずつ離れているので、縁を崩しても減らない。減りきらなければ、面の大きい三角から残す（細い後れ毛から消える）
                // 近くの段では消さない（房が薄くなるのが見える）
                if (!near && i == hair && tri.Length / 3 > budget * 1.2f) tri = Largest(done.vertices, tri, budget);
                var dv = done.vertices;
                var dn = done.normals;
                var du = done.uv;
                var o2 = new int[tri.Length];
                for (var k = 0; k < tri.Length; k++)
                {
                    int m;
                    if (!map.TryGetValue(tri[k], out m))
                    {
                        m = outV.Count;
                        map[tri[k]] = m;
                        outV.Add(dv[tri[k]]);
                        outN.Add(dn.Length > 0 ? dn[tri[k]] : Vector3.up);
                        outU.Add(du.Length > 0 ? du[tri[k]] : Vector2.zero);
                    }
                    o2[k] = m;
                }
                outT.Add(o2);
                if (done != part) Object.DestroyImmediate(done);
                Object.DestroyImmediate(part);
            }
            var mesh = new Mesh { name = name };
            if (outV.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(outV);
            mesh.SetNormals(outN);
            mesh.SetUVs(0, outU);
            mesh.subMeshCount = subs;
            for (var i = 0; i < subs; i++) mesh.SetTriangles(outT[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>三角の並び tris から、面の大きい順に count 個を残す（元の並びの順のまま）</summary>
        static int[] Largest(Vector3[] v, int[] tris, int count)
        {
            var n = tris.Length / 3;
            var area = new float[n];
            var order = new int[n];
            for (var t = 0; t < n; t++)
            {
                area[t] = Vector3.Cross(v[tris[3 * t + 1]] - v[tris[3 * t]], v[tris[3 * t + 2]] - v[tris[3 * t]]).sqrMagnitude;
                order[t] = t;
            }
            System.Array.Sort(order, (a, b) => area[b].CompareTo(area[a]));
            var keep = new bool[n];
            for (var k = 0; k < count && k < n; k++) keep[order[k]] = true;
            var o = new List<int>();
            for (var t = 0; t < n; t++)
                if (keep[t]) { o.Add(tris[3 * t]); o.Add(tris[3 * t + 1]); o.Add(tris[3 * t + 2]); }
            return o.ToArray();
        }

        /// <summary>
        /// 一人を姿勢に曲げて焼き、骨を持たない mesh にする（面の組は体・頭・髪の房のまま）。
        /// 原点は足元で、+z を向く。靴の裏（いちばん低い頂点）を 0 に揃える
        /// </summary>
        static Mesh Posed(RocketboxMob who, Pose pose)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Alley crowd/Import the people）: " + who.Model);
            var go = (GameObject)Object.Instantiate(src);
            go.hideFlags = HideFlags.HideAndDontSave;
            var baked = new Mesh();
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                var an = go.GetComponent<Animator>();
                // 画面に無い物は、既定の間引き（CullUpdateTransforms）だと骨が動かない
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                an.applyRootMotion = false;
                BodyPoser.Stand(an);
                Apply(an, pose);
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                var low = float.MaxValue;
                for (var i = 0; i < v.Length; i++)
                {
                    v[i] = go.transform.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                    n[i] = go.transform.InverseTransformDirection(smr.transform.TransformDirection(n[i])).normalized;
                    low = Mathf.Min(low, v[i].y);
                }
                for (var i = 0; i < v.Length; i++) v[i].y -= low;
                var mesh = new Mesh { name = who.Name + "_" + pose };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, smr.sharedMesh.uv);
                mesh.subMeshCount = smr.sharedMesh.subMeshCount;
                for (var s = 0; s < mesh.subMeshCount; s++) mesh.SetTriangles(smr.sharedMesh.GetTriangles(s), s);
                mesh.RecalculateBounds();
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>立った形から姿勢へ曲げる。模型は原点で +z を向き、本人の右が +x</summary>
        public static void Apply(Animator an, Pose pose)
        {
            var right = Vector3.right;
            var fwd = Vector3.forward;
            var up = Vector3.up;
            System.Func<HumanBodyBones, Transform> B = an.GetBoneTransform;
            switch (pose)
            {
                case Pose.Rest:
                    // 右脚に預け、左の膝を緩める。腰から上を少し右へ傾け、頭で戻す
                    Turn(B(HumanBodyBones.LeftUpperLeg), right, -6f);
                    Turn(B(HumanBodyBones.LeftLowerLeg), right, 10f);
                    Turn(B(HumanBodyBones.LeftUpperLeg), fwd, -3f);
                    Turn(B(HumanBodyBones.Spine), fwd, 2.5f);
                    Turn(B(HumanBodyBones.Chest), fwd, 1.5f);
                    Turn(B(HumanBodyBones.Head), fwd, -4f);
                    Turn(B(HumanBodyBones.LeftUpperArm), right, -6f);
                    Turn(B(HumanBodyBones.LeftLowerArm), right, -14f);
                    break;
                case Pose.Behind:
                    Behind(an);
                    break;
                case Pose.Turn:
                    // 上体と頭を右へ振り向く
                    Turn(B(HumanBodyBones.Spine), up, 7f);
                    Turn(B(HumanBodyBones.Chest), up, 6f);
                    Turn(B(HumanBodyBones.Neck), up, 6f);
                    Turn(B(HumanBodyBones.Head), up, 10f);
                    Turn(B(HumanBodyBones.RightUpperArm), right, -8f);
                    Turn(B(HumanBodyBones.RightLowerArm), right, -18f);
                    Turn(B(HumanBodyBones.LeftUpperArm), right, 4f);
                    break;
                case Pose.Crossed:
                    Crossed(an);
                    break;
                case Pose.Sit:
                    Sit(an, StoolTop);
                    break;
                case Pose.SitChair:
                    Sit(an, ChairTop);
                    break;
            }
        }

        static void Turn(Transform t, Vector3 axis, float degrees)
        {
            if (t == null || Mathf.Approximately(degrees, 0f)) return;
            t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
        }

        /// <summary>
        /// 腕を組む。前腕を胸の下で重ね、左手は右の二の腕の下、右手は左の二の腕の上へ届かせる（<see cref="BodyPoser.Arm"/>）。
        /// 手の置き場は肩の幅と高さから決める（男女で体の大きさが違う）
        /// </summary>
        static void Crossed(Animator an)
        {
            System.Func<HumanBodyBones, Vector3> P = b => an.GetBoneTransform(b).position;
            var sl = P(HumanBodyBones.LeftUpperArm);
            var sr = P(HumanBodyBones.RightUpperArm);
            var half = (sr.x - sl.x) * 0.5f;
            var y = (sl.y + sr.y) * 0.5f;
            var z = P(HumanBodyBones.Chest).z;
            // 左の前腕が下、右の前腕が上。手首は相手の肘の手前まで、指は相手の二の腕に沿って後ろへ回す
            BodyPoser.Arm(an, true, new Vector3(half * 0.50f, y - 0.26f, z + 0.15f), new Vector3(-0.6f, y - 0.40f, z + 0.20f),
                new Vector3(0.45f, 0.1f, -1f), new Vector3(0.9f, 0.2f, 0.2f));
            BodyPoser.Arm(an, false, new Vector3(-half * 0.48f, y - 0.21f, z + 0.18f), new Vector3(0.6f, y - 0.40f, z + 0.20f),
                new Vector3(-0.45f, 0.1f, -1f), new Vector3(-0.9f, -0.2f, 0.2f));
        }

        /// <summary>手を後ろで組む。両の手首を腰の後ろ（腰の骨の 16 cm 後ろ、2 cm 上）へ届かせ、肘は外の後ろへ逃がす</summary>
        static void Behind(Animator an)
        {
            var hips = an.GetBoneTransform(HumanBodyBones.Hips).position;
            var elbow = an.GetBoneTransform(HumanBodyBones.LeftLowerArm).position.y;
            BodyPoser.Arm(an, true, new Vector3(-0.05f, hips.y + 0.02f, hips.z - 0.17f), new Vector3(-0.5f, elbow, hips.z - 0.45f),
                new Vector3(0.6f, -0.6f, -0.2f), new Vector3(0f, 0f, 1f));
            BodyPoser.Arm(an, false, new Vector3(0.05f, hips.y + 0.03f, hips.z - 0.18f), new Vector3(0.5f, elbow, hips.z - 0.45f),
                new Vector3(-0.6f, -0.6f, -0.2f), new Vector3(0f, 0f, 1f));
        }

        /// <summary>座る。腰は座面の天面（top）の 7.5 cm 上、足首は床に、両手は腿の上（場面 1 の座り方の値を座面の高さへ移した物）</summary>
        static void Sit(Animator an, float top)
        {
            System.Func<HumanBodyBones, Vector3> P = b => an.GetBoneTransform(b).position;
            var ankle = P(HumanBodyBones.LeftFoot).y;
            var hipsY = top + 0.075f;
            BodyPoser.Pose(an, new BodyPoser.Sit
            {
                hips = new Vector3(0f, hipsY, 0.0f),
                pelvis = 0f,
                lean = 12f,
                headKeep = 0.8f,
                ankleL = new Vector3(-0.12f, ankle, 0.40f),
                ankleR = new Vector3(0.12f, ankle, 0.42f),
                kneePoleL = new Vector3(-0.12f, 0.9f, 1.4f),
                kneePoleR = new Vector3(0.12f, 0.9f, 1.4f),
                footPoint = 0f,
                wristL = new Vector3(-0.15f, hipsY + 0.13f, 0.30f),
                wristR = new Vector3(0.15f, hipsY + 0.13f, 0.32f),
                elbowPoleL = new Vector3(-0.45f, hipsY + 0.05f, -0.28f),
                elbowPoleR = new Vector3(0.45f, hipsY + 0.05f, -0.28f),
                fingersL = new Vector3(0.1f, -0.3f, 1f),
                palmL = new Vector3(0f, -1f, 0f),
                fingersR = new Vector3(-0.1f, -0.3f, 1f),
                palmR = new Vector3(0f, -1f, 0f),
            });
        }

        // ---- テクスチャとマテリアル ------------------------------------------------------

        static void EnsureFolder()
        {
            var dir = Folder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(BuildAlley.Generated.TrimEnd('/'), "Crowd");
        }

        /// <summary>
        /// 一回分のテクスチャとマテリアルを作る（服の色は元のまま、肌にインプラントを描く）。面の組の順は FBX のマテリアルの順（体・頭・髪の房）。
        /// n は描くテクスチャの大きさ（群衆は 256、売り手と買い手は 512）。buyer なら色を暗くする（<see cref="BuyerTone"/>）
        /// </summary>
        static Material[] Materials(Appearance a, int n, bool buyer, string tag)
        {
            var who = a.who;
            var per = Prep(who);
            Surfaces(per, n);
            var head0 = Pixels(per, n, true);
            var body0 = Pixels(per, n, false);
            var skin = SkinOf(per, n);
            var strokes = new List<RocketboxMobPaint.Stroke>();
            foreach (var im in a.implants) strokes.AddRange(RocketboxMobPaint.Strokes(im, per.frame));
            RocketboxMobPaint.Prepare(strokes, per.cloud);
            var head = (Color[])head0.Clone();
            var body = (Color[])body0.Clone();
            int ph, pb;
            var headGlow = RocketboxMobPaint.Paint(strokes, per.head[n], head, head0, skin, out ph);
            var bodyGlow = RocketboxMobPaint.Paint(strokes, per.body[n], body, body0, skin, out pb);
            var key = who.Name + "_" + tag;
            var headTex = Write(head, n, Folder + key + "_Head.png");
            var bodyTex = Write(body, n, Folder + key + "_Body.png");
            var headGlowTex = ph > 0 ? WriteGlow(headGlow, n, Folder + key + "_Head_Glow.asset") : null;
            var bodyGlowTex = pb > 0 ? WriteGlow(bodyGlow, n, Folder + key + "_Body_Glow.asset") : null;
            var bodyMat = Save(Mat(key + "_Body", bodyTex, bodyGlowTex, 0.12f, buyer), Folder + key + "_Body.mat");
            var headMat = Save(Mat(key + "_Head", headTex, headGlowTex, 0.22f, buyer), Folder + key + "_Head.mat");
            Material hair = null;
            if (who.HasOpacity)
            {
                // 髪の房はインプラントに関わらないので、群衆では人ごとに一つ。買い手は濃さを揃えた別の物（女大 15 は群衆にも出る）
                var hairName = who.Name + (buyer ? "_Buyer_Hair" : "_Hair");
                var hairPath = Folder + hairName + ".mat";
                hair = AssetDatabase.LoadAssetAtPath<Material>(hairPath);
                if (hair == null || !fresh.Contains(hairPath))
                {
                    var h = BuildRocketboxProtagonist.Lit(hairName, AssetDatabase.LoadAssetAtPath<Texture2D>(who.HairSrc), 0.34f, true);
                    if (buyer) h.SetColor("_BaseColor", new Color(BuyerTone, BuyerTone, BuyerTone, 1f));
                    hair = Save(h, hairPath);
                    fresh.Add(hairPath);
                }
            }
            // FBX の面の組の順に並べる（頭が先の人もいる）
            var list = new Material[per.slots.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var slot = per.slots[i];
                list[i] = slot == who.BodySlot ? bodyMat : slot == who.HeadSlot ? headMat : slot == who.HairSlot ? hair : null;
                if (list[i] == null) throw new System.InvalidOperationException("面の組のマテリアルが決まらない: " + who.Name + " の " + slot);
            }
            return list;
        }

        static Material Mat(string name, Texture2D tex, Texture2D glow, float smooth, bool buyer)
        {
            var m = BuildRocketboxProtagonist.Lit(name, tex, smooth, false);
            if (glow != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", glow);
                m.SetColor("_EmissionColor", Color.white * GlowStrength);
                // None にすると、URP のマテリアルの見直し（取り込みのたびに走る）が _EMISSION を落とし、光らなくなった
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            // 卓の上の豆電球で白く飛ばないよう、買い手は色を暗くする（<see cref="BuyerTone"/>）。
            // 浮かび上がるあいだは _BaseColor が全部の面の組に上書きされるので、どの組も同じ色にしておく
            if (buyer) m.SetColor("_BaseColor", new Color(BuyerTone, BuyerTone, BuyerTone, 1f));
            return m;
        }

        /// <summary>
        /// 買い手が浮かび上がるあいだの、透かせる写し。深さは書かない（URP の見直しが透かせる側では必ず切る）ので、
        /// 濃さを上げきったら透かさない組へ戻す（<see cref="BuyerFade"/>）
        /// </summary>
        static Material Faded(Material solid)
        {
            var m = new Material(solid) { name = solid.name + "_Fade" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static Material Save(Material m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            old.shader = m.shader;
            old.CopyPropertiesFromMaterial(m);
            old.shaderKeywords = m.shaderKeywords;
            old.globalIlluminationFlags = m.globalIlluminationFlags;
            old.renderQueue = m.rawRenderQueue;
            old.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
            EditorUtility.SetDirty(old);
            Object.DestroyImmediate(m);
            return old;
        }

        static Texture2D Write(Color[] px, int n, string path)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                c32[i] = px[i];
                c32[i].a = 255;
            }
            RocketboxTextures.WritePng(c32, n, n, path, false);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = n;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.isReadable = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// 自己発光のテクスチャを書く。ミップマップは平均ではなく、2×2 のいちばん明るい画素を残して縮める。
        /// 平均で縮めると、細い光る線は遠くで地の黒に薄まって消える（ゲームの見え方の 2.5 m で、頭のテクスチャは 1/16 まで縮んで使われる）。
        /// 書いたあと DXT1 に縮める（群衆の数十人ぶんを RGBA のまま持つと重い）
        /// </summary>
        static Texture2D WriteGlow(Color[] px, int n, string path)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var level = px;
            var size = n;
            for (var mip = 0; mip < t.mipmapCount; mip++)
            {
                t.SetPixels(level, mip);
                if (size == 1) break;
                var half = size / 2;
                var next = new Color[half * half];
                for (var y = 0; y < half; y++)
                    for (var x = 0; x < half; x++)
                    {
                        Color a = level[(2 * y) * size + 2 * x], b = level[(2 * y) * size + 2 * x + 1];
                        Color c = level[(2 * y + 1) * size + 2 * x], d = level[(2 * y + 1) * size + 2 * x + 1];
                        next[y * half + x] = new Color(Mathf.Max(Mathf.Max(a.r, b.r), Mathf.Max(c.r, d.r)), Mathf.Max(Mathf.Max(a.g, b.g), Mathf.Max(c.g, d.g)),
                            Mathf.Max(Mathf.Max(a.b, b.b), Mathf.Max(c.b, d.b)), 1f);
                    }
                level = next;
                size = half;
            }
            t.Apply(false, false);
            EditorUtility.CompressTexture(t, TextureFormat.DXT1, TextureCompressionQuality.Normal);
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(t, old);
                Object.DestroyImmediate(t);
                t = old;
            }
            else AssetDatabase.CreateAsset(t, path);
            // 読めるままだと、書き出した先で画素の写しをメモリに残す（WebGL では重い）
            var so = new SerializedObject(t);
            so.FindProperty("m_IsReadable").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
