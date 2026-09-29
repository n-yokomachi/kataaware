using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室（<c>Room.unity</c>）の家具を組む（シナリオ設計 5 節）。Kenney の単純な形だった家具を手で組んだ形に替え、足りない物を足す。
    ///
    /// <list type="table">
    /// <item><term>作り込む物</term><description>ソファ（沈むクッション・丸い肘・背のクッション、枕と丸めた毛布。ソファで寝ている）、冷蔵庫（上下の扉・取っ手・表示・磁石のメモとメニュー）、
    /// 台所（扉と取っ手の戸棚・流しと蛇口と水切りかご・IH・換気扇のフード・吊り戸棚・調味料の棚・電気ケトル・コーヒーメーカー・電子レンジ）、ごみ箱、
    /// 本棚（ばらばらの背表紙・寝かせた本・空のチップケース）、鉢植え、ラグ（柄と房の無い毛足）</description></item>
    /// <item><term>足す物</term><description>ローテーブル（マグ。メモを載せる）、フロアランプ、靴置き、傘立て、チップの在庫棚、サーバーラック（点滅する灯りとケーブルの束）、
    /// エアコン、作業台と丸椅子、額入りの絵（ミレー・ターナー・グリス・ハマスホイ）</description></item>
    /// <item><term>外す物</term><description>段ボール（BoxA〜C）と、置き換える前の Kenney の家具（枕・毛布・戸棚・流し・電子レンジ・コーヒーメーカー・本・鉢）</description></item>
    /// <item><term>焼き物</term><description>mesh は家具ごとに <c>Assets/Models/generated/furniture/</c>、絵は <c>Assets/Textures/Furniture/</c>（点で引く一枚のアトラスと光る所、点滅の絵）、
    /// マテリアルは <c>Assets/Materials/Room/Furniture.mat</c>（全部の家具で一つ）と <c>FurnitureBlink.mat</c>（ラックの点滅だけ）。何度押しても同じ物になる</description></item>
    /// <item><term>置き場</term><description>家具ごとの位置と向きは <see cref="Layout"/> の表一か所に持つ。間取りが変わったら表だけ直す</description></item>
    /// </list>
    ///
    /// ラグ（<c>Room/Rug</c>）は物を作り直さず、mesh と置き場だけ替える。付いている当たり（BoxCollider）と足音の地面（<see cref="StepGround"/>）は残し、
    /// 当たりの広さだけ新しい形に合わせる（高さは前の世界の高さのまま）。メモ（<c>Room/Binder</c> と調べる対象の clipboard）はローテーブルの上へ移す
    /// </summary>
    public static partial class BuildFurniture
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string MeshDir = "Assets/Models/generated/furniture/";
        public const string TextureDir = "Assets/Textures/Furniture/";
        public const string AtlasTexture = TextureDir + "FurnitureAtlas.png";
        public const string GlowTexture = TextureDir + "FurnitureGlow.png";
        public const string BlinkTexture = TextureDir + "RackBlink.png";
        public const string AtlasMaterial = "Assets/Materials/Room/Furniture.mat";
        public const string BlinkMaterial = "Assets/Materials/Room/FurnitureBlink.mat";

        /// <summary>
        /// 間取りが決まったか。決まるまでは Room に組み込まず、組み立ての道具は絵・マテリアル・mesh を焼くだけにする
        /// （仮の置き場で撮るのは <see cref="CheckFurniture.Preview"/>。場面は保存しない）
        /// </summary>
        public static readonly bool LayoutSettled = true;

        /// <summary>
        /// 置き場ひとつ。床の物は足元の真ん中、壁の物は壁に付く背の真ん中。Yaw は 0 で前が +z（北）。
        /// Length・Wide は大きさを変えられる物だけ（台所・作業台は長さ、ラグは長い辺と短い辺）。0 なら既定
        /// </summary>
        public struct Place
        {
            public string Name;
            public Vector3 At;
            public float Yaw;
            public float Length;
            public float Wide;

            public Place(string name, float x, float y, float z, float yaw, float length = 0f, float wide = 0f)
            {
                Name = name;
                At = new Vector3(x, y, z);
                Yaw = yaw;
                Length = length;
                Wide = wide;
            }
        }

        /// <summary>
        /// 置き場の表。部屋の形（<see cref="RoomPlan"/>。LDK x −5〜3・z −1.5〜3、南の廊下と玄関）の壁の面に合わせて、
        /// 区画（RoomPlan の Kitchen・Counter・Living・Work、東の窓の前、玄関の脇）へ置く。間取りが変わったらこの表（<see cref="Places"/> と <see cref="SofaWall"/>）だけ直す。
        /// 玄関の脇の物は、場面 3 の始まりの立ち位置（<see cref="RoomPlan.EntranceStand"/>、体の当たりの半径 0.3 m）に掛からない所へ寄せる
        /// </summary>
        public static Place[] Layout
        {
            get
            {
                if (layout != null) return layout;
                var all = new List<Place>(Places);
                all.AddRange(SofaWall());
                return layout = all.ToArray();
            }
        }

        static Place[] layout;

        static readonly Place[] Places =
        {
            // 居間（x −5〜−0.5・z 0.5〜3）。ソファは西の壁に背を付けて東を向き、前にラグとローテーブル、南の端にフロアランプ、上の壁にエアコンと三枚の絵（SofaWall）
            new Place("Sofa", -4.40f, 0f, 1.70f, 90f),
            new Place("Rug", -3.25f, 0f, 1.55f, 0f, 2.10f, 1.70f),
            new Place("LowTable", -3.30f, 0f, 1.62f, 90f),
            new Place("FloorLamp", -4.60f, 0f, 0.42f, 0f),
            new Place("AirCon", -4.90f, 2.42f, 1.70f, 90f),
            // 本棚は北の二つの窓の間の壁（x −3.32〜−1.78）
            new Place("Bookcase", -2.60f, 0f, 2.75f, 180f),
            // 台所（南の壁沿い x −5〜−0.6）。南西の角に冷蔵庫、その東に流しとコンロの並び、上の天井に細長い灯り（台所と作業台の間の上）
            new Place("Fridge", -4.58f, 0f, -1.06f, 0f),
            new Place("Kitchen", -2.44f, 0f, -1.10f, 0f, 3.64f),
            new Place("KitchenLight", -2.44f, RoomPlan.Ceiling, -0.45f, 0f),
            // 作業台（x −4〜−1.2・z 0〜0.6。区画より 10 cm 北へ寄せ、台所との間を 0.79 m 空けて体が通れるようにする）。居間の側に丸椅子。ごみ箱は冷蔵庫の前と作業台の西の端の間（廊下の口の動線から外す）
            new Place("WorkCounter", -2.60f, 0f, 0.30f, 0f, 2.80f),
            new Place("Stool", -1.90f, 0f, 0.88f, 20f),
            new Place("Bin", -4.45f, 0f, -0.45f, 0f),
            // 仕事の区画（x 0〜3・z 0.3〜3）。机・右の卓・PC は前の物と同じ所と広さ。机の西の脇にサーバーラック、右の卓と PC の間の東の壁に在庫棚
            new Place("Desk", 1.50f, 0f, 2.44f, 180f),
            new Place("SideTable", 2.648f, 0f, 1.100f, -90f),
            new Place("Tower", 2.55f, 0f, 2.55f, 180f),
            new Place("ServerRack", -0.05f, 0f, 2.57f, 180f),
            new Place("ChipShelf", 2.75f, 0f, 1.95f, -90f),
            // 東の窓の前
            new Place("Plant", 2.30f, 0f, -0.50f, 0f),
            // 廊下の口の LDK 側のコート掛け（場面 3 でジャケットを西の腕に掛ける。柱の位置は前の物のまま）
            new Place("CoatRack", 0.95f, 0f, -1.05f, 0f),
            // 玄関の脇（廊下 x −0.6〜0.6・z −5.1〜−1.5）。東の壁に靴置き（寝室のドアの手前まで、長さ 0.66）と、その上にハマスホイ、
            // ドアの東の角の玄関マットの上に傘立て。どちらも場面 3 の始まり（0, −4.6）の体の当たりから 3 cm 以上空ける
            new Place("ShoeRack", 0.36f, 0f, -3.99f, -90f, 0.66f),
            new Place("Picture3", 0.50f, 1.50f, -3.99f, -90f),
            new Place("UmbrellaStand", 0.37f, 0.02f, -4.87f, 0f),
        };

        /// <summary>
        /// ソファの上の三枚の組（オーナー「絵画は横並びではなくちょっとデザイン考えて配置」）。一列に並べず、二列の組にする:
        /// 前から見て左の列に縦長のグリスを大きく、右の列にターナー（上）とミレー（下、右の列の外の縁に揃える）を縦に重ねる。
        /// 二つの列の上の縁と下の縁を揃えて、組の外形を一つの長方形にし（グリスの高さ = ターナー + 隙間 + ミレー）、隙間はどこも同じ 9 cm。
        /// 組の真ん中はソファの真ん中、下の縁はソファの背（クッションの頭 0.98 m）から 27 cm 上、上の縁はエアコンの 17 cm 下。
        /// 大きいグリスはフロアランプの側（左）に置いて暖かい灯りを受けさせ、小さい二枚を窓の側（右）へ寄せて、窓の明るさと釣り合わせる
        /// </summary>
        static Place[] SofaWall()
        {
            const float wallX = -4.90f, centreZ = 1.70f, bottomY = 1.25f, gap = 0.09f;
            var tall = FrameSize(2);
            var upper = FrameSize(1);
            var lower = FrameSize(0);
            // 絵は +x を向くので、前から見て左は −z
            var left = centreZ - (tall.x + gap + upper.x) * 0.5f;
            var topY = bottomY + tall.y;
            var column = left + tall.x + gap;
            return new[]
            {
                new Place("Picture2", wallX, bottomY + tall.y * 0.5f, left + tall.x * 0.5f, 90f),
                new Place("Picture1", wallX, topY - upper.y * 0.5f, column + upper.x * 0.5f, 90f),
                new Place("Picture0", wallX, bottomY + lower.y * 0.5f, column + upper.x - lower.x * 0.5f, 90f),
            };
        }

        /// <summary>置き換える前の家具（Kenney）のうち、同じ名前の新しい物が無い物。組むと外す</summary>
        static readonly string[] Retired =
        {
            "Pillow", "Blanket", "KitchenCabinet", "KitchenSink", "Microwave", "CoffeeMachine", "PlantTall",
            "Books1", "Books2", "Books3", "Books4", "Books5", "Books6", "BoxA", "BoxB", "BoxC", "Keyboard", "Mouse",
        };

        /// <summary>当たりを付けない物（壁の物と、形の中に歩いて入らない物）</summary>
        static readonly string[] NoBlocker = { "AirCon", "Picture0", "Picture1", "Picture2", "Picture3", "Rug", "KitchenLight" };

        /// <summary>ローテーブルの上のメモの置き場（テーブルから見た位置と向き）</summary>
        static readonly Vector3 MemoOnTable = new Vector3(-0.20f, 0.401f, 0.02f);
        const float MemoYaw = 22f;
        /// <summary>メモの調べる対象の、メモの置き場からの高さ（前のソファの上と同じ 3.8 cm）</summary>
        const float MemoItemLift = 0.038f;

        /// <summary>メモの調べる対象の置き場（ローテーブルの上）。場面 3 の同じメモ（<see cref="BuildConnect"/> の note）もここ</summary>
        public static Vector3 MemoItemAt
        {
            get
            {
                foreach (var p in Layout)
                    if (p.Name == "LowTable") return p.At + Quaternion.Euler(0f, p.Yaw, 0f) * MemoOnTable + Vector3.up * MemoItemLift;
                return Vector3.zero;
            }
        }

        /// <summary>台所の天井の灯り。台所と作業台の上だけを少し起こす、控えめな白い灯り</summary>
        const float KitchenIntensity = 1.2f;
        const float KitchenRange = 3.0f;
        static readonly Color KitchenColour = new Color(1.0f, 0.94f, 0.86f);

        /// <summary>フロアランプの灯り。部屋の明かりを変えすぎない、弱い暖かい灯り</summary>
        const float LampIntensity = 0.7f;
        const float LampRange = 2.4f;
        static readonly Color LampColour = new Color(1.0f, 0.80f, 0.58f);

        [MenuItem("HalfAware/Build the room furniture", false, 216)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>
        /// 絵・マテリアル・mesh を焼く。間取りが決まっていれば（<see cref="LayoutSettled"/>）Room.unity を開いて組み込み、保存する
        /// </summary>
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "再生中は組まない。止めてからもう一度";
            var notes = new List<string>();
            if (!LayoutSettled)
            {
                var made = Bake(notes);
                AssetDatabase.SaveAssets();
                return "間取りが決まるまで Room には組み込まない（焼くだけ）。" + System.Environment.NewLine + Report(made, notes);
            }
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var scene = EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            var before = Census(scene);
            var text = Assemble(scene);
            var after = Census(scene);
            var diff = Diff(before, after, out var unexpected);
            if (unexpected > 0)
                return text + System.Environment.NewLine + diff + System.Environment.NewLine + "家具の外が変わったので保存しない。開き直して捨てること";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return "Room.unity を保存できなかった";
            AssetDatabase.SaveAssets();
            return text + System.Environment.NewLine + diff;
        }

        /// <summary>焼いた物ひとつ</summary>
        public sealed class Made
        {
            public string Name;
            public Mesh Mesh;
            public Mesh Leds;
            public int Triangles;
            public int Vertices;
            public Dictionary<string, int> Pieces;
        }

        /// <summary>重さの内訳（家具ごとに、三角の多い部品から top 個）</summary>
        public static string Weights(int top)
        {
            var notes = new List<string>();
            var sb = new StringBuilder();
            foreach (var m in Bake(notes))
            {
                var list = new List<KeyValuePair<string, int>>(m.Pieces);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.Append(m.Name).Append(" ").Append(m.Triangles).Append(": ");
                for (var i = 0; i < list.Count && i < top; i++) sb.Append(list[i].Key).Append(" ").Append(list[i].Value).Append("、");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>絵・マテリアル・家具ごとの mesh を焼く（表の物すべて）。場面には触らない</summary>
        public static List<Made> Bake(List<string> notes)
        {
            Texture2D glow;
            var atlas = PaintAtlas(out glow, notes);
            var blink = PaintBlink();
            Materials(atlas, glow, blink);
            var made = new List<Made>();
            foreach (var p in Layout)
            {
                var kit = new FurnitureKit();
                FurnitureKit leds = null;
                if (!Shape(p, kit, ref leds)) { notes.Add("形の無い物: " + p.Name); continue; }
                var m = new Made { Name = p.Name, Triangles = kit.TriangleCount, Vertices = kit.VertexCount, Pieces = kit.PieceTriangles };
                m.Mesh = SaveMesh(kit.Bake(p.Name), MeshDir + p.Name + ".asset");
                if (leds != null)
                {
                    m.Leds = SaveMesh(leds.Bake(p.Name + "Leds"), MeshDir + p.Name + "Leds.asset");
                    m.Triangles += leds.TriangleCount;
                    m.Vertices += leds.VertexCount;
                }
                made.Add(m);
            }
            return made;
        }

        /// <summary>名前から形を組む。組めない名前なら false</summary>
        static bool Shape(Place p, FurnitureKit kit, ref FurnitureKit leds)
        {
            switch (p.Name)
            {
                case "Sofa": Sofa(kit); return true;
                case "Rug": Rug(kit, p.Wide > 0f ? p.Wide : RugWide, p.Length > 0f ? p.Length : RugLong); return true;
                case "LowTable": LowTable(kit); return true;
                case "FloorLamp": FloorLamp(kit); return true;
                case "AirCon": AirCon(kit, p.At.y); return true;
                case "Bookcase": Bookcase(kit); return true;
                case "Fridge": Fridge(kit); return true;
                case "Kitchen": Kitchen(kit, p.Length > 0f ? p.Length : 2.5f); return true;
                case "Bin": Bin(kit); return true;
                case "WorkCounter": WorkCounter(kit, p.Length > 0f ? p.Length : 1.2f); return true;
                case "Stool": Stool(kit); return true;
                case "ServerRack": leds = new FurnitureKit(); ServerRack(kit, leds); return true;
                case "ChipShelf": ChipShelf(kit); return true;
                case "Plant": Plant(kit); return true;
                case "ShoeRack": ShoeRack(kit, p.Length > 0f ? p.Length : 0.8f); return true;
                case "UmbrellaStand": UmbrellaStand(kit); return true;
                case "Desk": Desk(kit); return true;
                case "SideTable": SideTable(kit); return true;
                case "Tower": Tower(kit); return true;
                case "CoatRack": CoatRack(kit); return true;
                case "KitchenLight": KitchenLight(kit); return true;
            }
            if (p.Name.StartsWith("Picture"))
            {
                int k;
                if (int.TryParse(p.Name.Substring("Picture".Length), out k) && k >= 0 && k < Pictures.Length) { Frame(kit, k); return true; }
            }
            return false;
        }

        /// <summary>
        /// 開いている場面（Room）へ家具を組む。**保存はしない。** 保存するのは <see cref="Build"/>。確かめの撮影（<see cref="CheckFurniture.Preview"/>）は組んで撮ってから捨てる
        /// </summary>
        public static string Assemble(Scene scene)
        {
            var notes = new List<string>();
            var room = BuildChair.Find(scene, "Room");
            if (room == null) return "Room が無い";
            var made = Bake(notes);
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterial);
            var blinkMat = AssetDatabase.LoadAssetAtPath<Material>(BlinkMaterial);

            foreach (var name in Retired)
            {
                var old = room.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            // 前の机の当たり（椅子を押し下げて立った所との隙間はこの箱で測ってある）は、世界の同じ箱のまま残す
            var keepDesk = WorldBox(room.Find("Desk"));
            foreach (var p in Layout)
            {
                var m = made.Find(x => x.Name == p.Name);
                if (m == null) continue;
                if (p.Name == "Rug") { notes.Add(PutRug(room, p, m.Mesh, atlas)); continue; }
                var t = Ensure(room, p.Name);
                t.localPosition = p.At;
                t.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                t.localScale = Vector3.one;
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = m.Mesh;
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { atlas };
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                t.gameObject.isStatic = false;
                if (System.Array.IndexOf(NoBlocker, p.Name) < 0) Blocker(t, p.Name, m.Mesh);
                if (p.Name == "Desk" && keepDesk.HasValue) Keep(t, keepDesk.Value);
                if (m.Leds != null) Leds(t, m.Leds, blinkMat);
                if (p.Name == "FloorLamp") Glow(t, new Vector3(0f, LampBulbY, 0f), LampColour, LampIntensity, LampRange);
                if (p.Name == "KitchenLight") Glow(t, new Vector3(0f, -KitchenGlowDrop, 0f), KitchenColour, KitchenIntensity, KitchenRange);
            }
            notes.Add(Memo(scene, room));
            AssetDatabase.SaveAssets();
            return Report(made, notes);
        }

        /// <summary>Room の子を名前で出す。前の Kenney の物（mesh が家具の置き場の外）は捨てて作り直す</summary>
        static Transform Ensure(Transform room, string name)
        {
            var t = room.Find(name);
            if (t != null)
            {
                var mf = t.GetComponent<MeshFilter>();
                var mine = mf != null && mf.sharedMesh != null && AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith(MeshDir);
                if (mine) return t;
                Object.DestroyImmediate(t.gameObject);
            }
            var go = new GameObject(name);
            go.transform.SetParent(room, false);
            return go.transform;
        }

        /// <summary>当たり。形の広さの箱（鉢植えは鉢、フロアランプは台と柱）</summary>
        static void Blocker(Transform t, string name, Mesh mesh)
        {
            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
            var b = mesh.bounds;
            if (name == "Plant") b = new Bounds(new Vector3(0f, 0.19f, 0f), new Vector3(0.38f, 0.38f, 0.38f));
            if (name == "FloorLamp") b = new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(0.30f, 1.5f, 0.30f));
            if (name == "CoatRack") b = new Bounds(new Vector3(0f, 0.77f, 0f), new Vector3(0.40f, 1.54f, 0.40f));
            if (name == "Kitchen" || name == "WorkCounter") b = new Bounds(new Vector3(b.center.x, 0.47f, b.center.z), new Vector3(b.size.x, 0.94f, b.size.z));
            box.center = b.center;
            box.size = b.size;
            box.isTrigger = false;
        }

        /// <summary>ラックの点滅する灯り。子の Leds に点滅のマテリアルで置き、ラックに <see cref="RackLights"/> を付ける</summary>
        static void Leds(Transform t, Mesh mesh, Material mat)
        {
            var child = t.Find("Leds");
            if (child == null)
            {
                child = new GameObject("Leds").transform;
                child.SetParent(t, false);
            }
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            var mf = child.GetComponent<MeshFilter>();
            if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = child.GetComponent<MeshRenderer>();
            if (mr == null) mr = child.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var lights = t.GetComponent<RackLights>();
            if (lights == null) lights = t.gameObject.AddComponent<RackLights>();
            var so = new SerializedObject(lights);
            so.FindProperty("lights").objectReferenceValue = mr;
            so.FindProperty("rows").intValue = BlinkSize;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>灯り（子の Glow に、影を落とさない点光源）。フロアランプの傘の中と、台所の天井の灯具の下</summary>
        static void Glow(Transform t, Vector3 at, Color colour, float intensity, float range)
        {
            var child = t.Find("Glow");
            if (child == null)
            {
                child = new GameObject("Glow").transform;
                child.SetParent(t, false);
            }
            child.localPosition = at;
            child.localRotation = Quaternion.identity;
            var l = child.GetComponent<Light>();
            if (l == null) l = child.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        /// <summary>物の当たり（BoxCollider）の世界の箱。無ければ null</summary>
        static Bounds? WorldBox(Transform t)
        {
            var box = t != null ? t.GetComponent<BoxCollider>() : null;
            if (box == null) return null;
            return box.bounds;
        }

        /// <summary>当たりを世界の箱 world に合わせる（物は y まわりにしか回っていない）</summary>
        static void Keep(Transform t, Bounds world)
        {
            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
            box.center = t.InverseTransformPoint(world.center);
            var size = t.InverseTransformVector(world.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        /// <summary>ラグ。物は作り直さず、mesh・マテリアル・置き場を替え、当たりの広さを新しい形に合わせる（当たりの世界の高さと足音の地面はそのまま）</summary>
        static string PutRug(Transform room, Place p, Mesh mesh, Material mat)
        {
            var rug = room.Find("Rug");
            if (rug == null)
            {
                rug = new GameObject("Rug").transform;
                rug.SetParent(room, false);
            }
            var box = rug.GetComponent<BoxCollider>();
            float bottom = 0f, top = 0f;
            if (box != null)
            {
                bottom = rug.TransformPoint(box.center - Vector3.up * box.size.y * 0.5f).y;
                top = rug.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f).y;
            }
            rug.localPosition = p.At;
            rug.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
            rug.localScale = Vector3.one;
            var mf = rug.GetComponent<MeshFilter>();
            if (mf == null) mf = rug.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = rug.GetComponent<MeshRenderer>();
            if (mr == null) mr = rug.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            var ground = rug.GetComponent<StepGround>();
            if (box == null) return "ラグ: mesh と置き場を替えた（当たりと足音の地面はまだ付いていない。PlaceRoomFoley が付ける）";
            var b = mesh.bounds;
            var lo = rug.InverseTransformPoint(new Vector3(rug.position.x, bottom, rug.position.z)).y;
            var hi = rug.InverseTransformPoint(new Vector3(rug.position.x, top, rug.position.z)).y;
            box.center = new Vector3(b.center.x, (lo + hi) * 0.5f, b.center.z);
            box.size = new Vector3(b.size.x, hi - lo, b.size.z);
            var w = box.bounds;
            return string.Format("ラグ: mesh と置き場を替え、当たりを広さ x {0:0.00}〜{1:0.00}・z {2:0.00}〜{3:0.00}、高さ {4:0.000}〜{5:0.000} にした（足音の地面 {6}）",
                w.min.x, w.max.x, w.min.z, w.max.z, w.min.y, w.max.y, ground != null ? "は付いたまま" : "は無い");
        }

        /// <summary>メモ（Room/Binder と調べる対象 clipboard）をローテーブルの上へ移す</summary>
        static string Memo(Scene scene, Transform room)
        {
            var table = room.Find("LowTable");
            var binder = room.Find("Binder");
            if (table == null || binder == null) return "メモかローテーブルが無い";
            binder.position = table.TransformPoint(MemoOnTable);
            binder.rotation = Quaternion.Euler(0f, table.eulerAngles.y + MemoYaw, 0f);
            if ((binder.position + Vector3.up * MemoItemLift - MemoItemAt).sqrMagnitude > 1e-6f) return "メモの置き場が表から出した MemoItemAt と合わない（Room が原点に無い？）";
            Interactable item = null;
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (it.gameObject.scene == scene && it.Id == RoomIds.Clipboard) item = it;
            if (item == null) return "メモを移した（調べる対象 clipboard が無い）: " + binder.position.ToString("F3");
            item.transform.position = binder.position + Vector3.up * MemoItemLift;
            return string.Format("メモをローテーブルの上へ: {0}、調べる対象 {1}", binder.position.ToString("F3"), item.transform.position.ToString("F3"));
        }

        // ---- マテリアル --------------------------------------------------------------

        static void Materials(Texture2D atlas, Texture2D glow, Texture2D blink)
        {
            var m = Lit(AtlasMaterial, "Furniture");
            m.SetTexture("_BaseMap", atlas);
            m.SetTexture("_MainTex", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            // 艶は albedo の α（家具ごとに布・木・鋼・硝子で変わる）
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetTexture("_EmissionMap", glow);
            m.SetColor("_EmissionColor", Color.white);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);

            var b = Lit(BlinkMaterial, "FurnitureBlink");
            b.SetTexture("_BaseMap", blink);
            b.SetTexture("_MainTex", blink);
            b.SetTextureScale("_BaseMap", Vector2.one);
            b.SetTextureOffset("_BaseMap", Vector2.zero);
            b.SetColor("_BaseColor", new Color(0.4f, 0.4f, 0.4f));
            b.SetFloat("_Metallic", 0f);
            b.SetFloat("_Smoothness", 0.5f);
            b.SetFloat("_SmoothnessTextureChannel", 0f);
            b.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            b.SetTexture("_EmissionMap", blink);
            b.SetColor("_EmissionColor", Color.white * 1.3f);
            b.EnableKeyword("_EMISSION");
            b.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(b);
        }

        /// <summary>部屋の Steel.mat を写した Lit のマテリアル。在れば使う</summary>
        static Material Lit(string path, string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Steel.mat")) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---- mesh ----------------------------------------------------------------------

        /// <summary>mesh をアセットとして上書きする。前の物があれば中身を写して guid を保つ（場面 3・5・7 の参照が切れない）</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir);
            }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, mesh.uv);
            existing.subMeshCount = mesh.subMeshCount;
            for (var i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i, false);
            existing.RecalculateBounds();
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---- 報告 ----------------------------------------------------------------------

        static string Report(List<Made> made, List<string> notes)
        {
            var sb = new StringBuilder();
            var tris = 0;
            var verts = 0;
            var draws = 0;
            var bytes = 0L;
            foreach (var m in made)
            {
                tris += m.Triangles;
                verts += m.Vertices;
                draws += m.Leds != null ? 2 : 1;
                var size = FileSize(MeshDir + m.Name + ".asset") + (m.Leds != null ? FileSize(MeshDir + m.Name + "Leds.asset") : 0L);
                bytes += size;
                sb.AppendFormat("  {0}: 三角 {1}・頂点 {2}・mesh {3:0.00} MB", m.Name, m.Triangles, m.Vertices, size / 1048576f).AppendLine();
            }
            var head = string.Format("家具 {0} 点を焼いた。三角 {1}・頂点 {2}・描く回数 {3}（マテリアル 2: Furniture と、ラックの点滅の FurnitureBlink）・mesh 計 {4:0.00} MB",
                made.Count, tris, verts, draws, bytes / 1048576f);
            foreach (var n in notes) sb.AppendLine("  " + n);
            return head + System.Environment.NewLine + sb.ToString().TrimEnd();
        }

        static long FileSize(string path)
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }

        // ---- 場面の中 ----------------------------------------------------------------

        /// <summary>場面の物の一覧（道筋）。家具（表の名前・外す物・ラグ）の下は数えない</summary>
        public static List<string> Census(Scene scene)
        {
            var list = new List<string>();
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, r.name, list);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        static void Walk(Transform t, string path, List<string> list)
        {
            list.Add(path);
            if (Ours(path)) return;
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path + "/" + t.GetChild(i).name, list);
        }

        /// <summary>家具の組み立てが触ってよい道筋（Room の直下の、表の名前と外す物）</summary>
        static bool Ours(string path)
        {
            if (!path.StartsWith("Room/")) return false;
            var name = path.Substring("Room/".Length);
            if (name.Contains("/")) return false;
            if (System.Array.IndexOf(Retired, name) >= 0) return true;
            foreach (var p in Layout) if (p.Name == name) return true;
            return false;
        }

        /// <summary>二つの一覧の差。家具の物の出入りは書くが、思いがけない差には数えない</summary>
        static string Diff(List<string> a, List<string> b, out int unexpected)
        {
            unexpected = 0;
            var sb = new StringBuilder("場面の物の差:").AppendLine();
            var sa = new HashSet<string>(a);
            var sbSet = new HashSet<string>(b);
            foreach (var k in a)
                if (!sbSet.Contains(k)) { var ok = Ours(k); if (!ok) unexpected++; sb.AppendLine("  " + (ok ? "" : "（思いがけない）") + "無くなった " + k); }
            foreach (var k in b)
                if (!sa.Contains(k)) { var ok = Ours(k); if (!ok) unexpected++; sb.AppendLine("  " + (ok ? "" : "（思いがけない）") + "増えた " + k); }
            return sb.ToString().TrimEnd();
        }
    }
}
