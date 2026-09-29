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
    /// 自室（<c>Room.unity</c>）の椅子を組む（シナリオ設計 5 節）。潜り手が一日の大半を過ごし、長い潜行に体を預ける専用の椅子。
    ///
    /// <list type="table">
    /// <item><term>形</term><description>厚い座面（前後に 4 本の畝、脇の盛り上がり、前の丸み）、高い背もたれ（横に 8 本の畝、肩の張り出し、首の絞り）、
    /// 腰と頭の当て物、幅の広い肘掛け（外に操作盤の受け皿。右は差込口）、リクライニングの機構とレバー、ガスシリンダー、五本脚とキャスター、
    /// 座の前の下から引き出すフットレスト（腕と張り地のパッド）。
    /// 頭の後ろに潜行の端末（耳の筐体の小さな画面と灯り、裏の箱の画面と接続口）、そこから背もたれの裏を這って座の下と右の肘掛けへ回るケーブル
    /// （<c>BuildChairShape.cs</c>）</description></item>
    /// <item><term>体との取り合い</term><description>座った体の形（SeatedPose）を組み直さずに済むよう、座面の高さの線・肘掛けの上面・差込口・
    /// ジャックの置き場は前の椅子と同じ所。フットレストは座った形の足の裏が乗る所まで引き出した形</description></item>
    /// <item><term>置き場</term><description><c>Room/Chair</c> の下の見た目の子を <c>ChairMesh</c> 一つ（マテリアル 5 つ）に替える。
    /// 働きを持つ子（<c>Blocker</c> とその当たり・<c>JackRest</c>・<c>Cable</c>・<c>PortHole</c>）は名前のまま残し、当たりの大きさだけ新しい形に合わせる。
    /// PortHole はケーブルの始まりの置き場として残し、見た目（前の黒い箱）は外す</description></item>
    /// <item><term>焼き物</term><description>mesh は <c>Assets/Models/generated/chair/Chair.asset</c>、絵は <c>Assets/Textures/Chair/</c>（点で引く粗い絵）、
    /// マテリアルは <c>Assets/Materials/Room/ChairUpholstery.mat</c>・<c>ChairPanel.mat</c>（ほかは部屋の PanelDark・SteelDark・Steel）。
    /// 何度押しても上書きで同じ物になる</description></item>
    /// </list>
    ///
    /// **組み直す前と後で、椅子の外の物の一覧（名前と数）を比べ、増減があれば知らせる。**
    /// 場面 3・5・7（Connect・Rest・Notice）は Room を写して組むので、組み直したら三つとも組み直す
    /// </summary>
    public static partial class BuildChair
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string ChairPath = "Room/Chair";
        public const string MeshName = "ChairMesh";
        public const string MeshPath = "Assets/Models/generated/chair/Chair.asset";
        public const string TextureDir = "Assets/Textures/Chair/";
        public const string UpholsteryTexture = TextureDir + "ChairUpholstery.png";
        public const string PanelTexture = TextureDir + "ChairPanel.png";
        public const string PanelGlowTexture = TextureDir + "ChairPanelGlow.png";
        public const string UpholsteryMaterial = "Assets/Materials/Room/ChairUpholstery.mat";
        public const string PanelMaterial = "Assets/Materials/Room/ChairPanel.mat";

        /// <summary>残す子（働きを持つ物）。ほかの子は見た目なので組み直すたびに捨てる</summary>
        static readonly string[] Keep = { "Blocker", "JackRest", "Cable", "PortHole" };

        [MenuItem("HalfAware/Build the chair", false, 215)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>Room.unity を開いて椅子を組み、保存する。開いている場面に未保存の変更があれば何もしない</summary>
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "再生中は組まない。止めてからもう一度";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var scene = EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            var before = Census(scene);
            var chairBefore = ChairChildren(scene);
            var made = Assemble(scene);
            var after = Census(scene);
            var compare = BuildRoomView.Compare(before, after);
            if (!System.Linq.Enumerable.SequenceEqual(before, after))
                return made + System.Environment.NewLine + compare + System.Environment.NewLine + "椅子の外が変わったので保存しない。開き直して捨てること";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return "Room.unity を保存できなかった";
            AssetDatabase.SaveAssets();
            return made + System.Environment.NewLine + compare.Replace("景色の根", "椅子") + System.Environment.NewLine
                + "椅子の子: 前 " + chairBefore + System.Environment.NewLine + "　　　　　後 " + ChairChildren(scene);
        }

        /// <summary>
        /// 開いている場面へ椅子を組む。絵・マテリアル・mesh を焼き直し、Room/Chair の見た目の子を替え、当たりを合わせる。
        /// **保存はしない。** 保存するのは <see cref="Build"/>。確かめの撮影（<see cref="CheckChair.Preview"/>）は組んで撮ってから捨てる
        /// </summary>
        public static string Assemble(Scene scene)
        {
            var chair = Find(scene, ChairPath);
            if (chair == null) return "Room/Chair が無い";
            var mats = Materials();
            var made = Shape();
            var mesh = SaveMesh(made.Mesh, MeshPath);

            // 見た目の子を捨てる（働きを持つ子と、前に組んだ ChairMesh は残す）
            for (var i = chair.childCount - 1; i >= 0; i--)
            {
                var c = chair.GetChild(i);
                if (System.Array.IndexOf(Keep, c.name) >= 0 || c.name == MeshName) continue;
                Object.DestroyImmediate(c.gameObject);
            }
            var body = chair.Find(MeshName);
            if (body == null)
            {
                body = new GameObject(MeshName).transform;
                body.SetParent(chair, false);
                body.SetSiblingIndex(0);
            }
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one;
            var mf = body.GetComponent<MeshFilter>();
            if (mf == null) mf = body.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = body.GetComponent<MeshRenderer>();
            if (mr == null) mr = body.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            body.gameObject.isStatic = false;

            var port = Port(chair);
            var blocker = Blockers(chair);
            var push = Push(scene);
            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.AppendFormat("椅子を組んだ。三角 {0}・頂点 {1}・部品 {2}・マテリアル {3}（描く回数 {3}）。mesh {4:0.00} MB",
                made.Triangles, made.Vertices, made.Pieces.Count, mats.Length, MeshSize()).AppendLine();
            sb.AppendLine("マテリアルごとの三角: " + SubmeshLine(mesh));
            sb.AppendLine(port);
            sb.AppendLine(blocker);
            sb.Append(push);
            return sb.ToString();
        }

        static string SubmeshLine(Mesh mesh)
        {
            var names = new[] { "張り地", "殻", "金具", "磨いた金属", "操作盤" };
            var parts = new List<string>();
            for (var i = 0; i < mesh.subMeshCount && i < names.Length; i++) parts.Add(names[i] + " " + mesh.GetSubMesh(i).indexCount / 3);
            return string.Join("・", parts.ToArray());
        }

        static float MeshSize()
        {
            return File.Exists(MeshPath) ? new FileInfo(MeshPath).Length / 1048576f : 0f;
        }

        /// <summary>差込口の置き場。ケーブル（Room/Chair/Cable）の始まりなので名前と置き場を残し、見た目は椅子の mesh が持つ</summary>
        static string Port(Transform chair)
        {
            var port = chair.Find("PortHole");
            if (port == null) return "差込口の置き場（PortHole）が無い。ケーブルの始まりが切れる";
            port.localPosition = PortAt;
            port.localRotation = Quaternion.identity;
            port.localScale = Vector3.one;
            var r = port.GetComponent<MeshRenderer>();
            if (r != null) Object.DestroyImmediate(r);
            var f = port.GetComponent<MeshFilter>();
            if (f != null) Object.DestroyImmediate(f);
            return "差込口の置き場（PortHole）: " + PortAt.ToString("F3") + "（見た目は椅子の mesh へ移した）";
        }

        /// <summary>
        /// 立ち上がる時に椅子を後ろへ押し下げる距離（SceneFlow の chairPushBack）。前の椅子は 0.24 m。
        /// フットレストの先（z 0.445）が、机との間の立ち位置（z 1.68、当たりの半径 0.3 m）の当たりの縁 1.38 から 3 cm 手前に来る所まで下げる。
        /// 椅子の後ろ（押し下げた背の後ろの端は z 0.27）は床とラグだけ
        /// </summary>
        public const float PushBack = 0.38f;

        /// <summary>
        /// 立った後の当たり（Room/Chair/Blocker の子）を新しい形に合わせる。名前は前のまま（Seat・Base・Back・ArmR・ArmL）。
        /// 床の近くの当たり（Base）はフットレストの先まで伸ばす（場面 3 で椅子の前を歩いてパッドを踏み抜かない）。
        /// どの当たりも、立ち上がって <see cref="PushBack"/> 下げた時に、机との間の立ち位置の当たりに掛からない
        /// </summary>
        static string Blockers(Transform chair)
        {
            var root = chair.Find("Blocker");
            if (root == null) return "当たり（Blocker）が無い";
            var sb = new StringBuilder("当たり: ");
            // 座面（脇の盛り上がりまで）
            sb.Append(Box(root, "Seat", new Vector3(0f, 0.40f, 0.04f), new Vector3(0.60f, 0.80f, 0.50f)));
            // 五本脚（脚の先のキャスターまで）とフットレストのパッド
            sb.Append(Box(root, "Base", new Vector3(0f, 0.08f, 0.07f), new Vector3(0.64f, 0.16f, 0.76f)));
            // 背もたれと頭の後ろの端末
            sb.Append(Box(root, "Back", new Vector3(0f, 0.985f, -0.39f), new Vector3(0.56f, 1.03f, 0.48f)));
            // 肘掛け（外の受け皿と前の操作盤まで）
            sb.Append(Box(root, "ArmR", new Vector3(0.335f, 0.60f, 0.08f), new Vector3(0.18f, 0.40f, 0.52f)));
            sb.Append(Box(root, "ArmL", new Vector3(-0.335f, 0.60f, 0.08f), new Vector3(0.18f, 0.40f, 0.52f)));
            return sb.ToString();
        }

        /// <summary>場面の SceneFlow の、立ち上がる時に椅子を押し下げる距離を <see cref="PushBack"/> にする（場面 3・5・7 は Room を写して組むので同じ値が行く）</summary>
        static string Push(Scene scene)
        {
            SceneFlow flow = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                flow = r.GetComponentInChildren<SceneFlow>(true);
                if (flow != null) break;
            }
            if (flow == null) return "SceneFlow が無い。椅子の押し下げを書けない";
            var so = new SerializedObject(flow);
            var p = so.FindProperty("chairPushBack");
            var was = p.floatValue;
            p.floatValue = PushBack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return string.Format("立ち上がる時の椅子の押し下げ（SceneFlow の chairPushBack）: {0:0.00} → {1:0.00} m", was, PushBack);
        }

        static string Box(Transform root, string name, Vector3 centre, Vector3 size)
        {
            var t = root.Find(name);
            if (t == null) return name + " が無い。";
            var box = t.GetComponent<BoxCollider>();
            if (box == null) return name + " に BoxCollider が無い。";
            t.localPosition = centre;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            box.center = Vector3.zero;
            box.size = size;
            var b = new Bounds(centre, size);
            return string.Format("{0} x {1:0.00}〜{2:0.00}・y {3:0.00}〜{4:0.00}・z {5:0.00}〜{6:0.00}。", name, b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z);
        }

        // ---- mesh ----------------------------------------------------------------

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

        // ---- マテリアル --------------------------------------------------------------

        /// <summary>張り地・殻・金具・磨いた金属・操作盤の順（mesh の面の組の順）</summary>
        static Material[] Materials()
        {
            var upholstery = Lit(UpholsteryMaterial, "ChairUpholstery");
            upholstery.SetTexture("_BaseMap", UpholsteryPicture());
            upholstery.SetTexture("_MainTex", upholstery.GetTexture("_BaseMap"));
            upholstery.SetColor("_BaseColor", Color.white);
            upholstery.SetFloat("_Metallic", 0f);
            upholstery.SetFloat("_Smoothness", 0.36f);
            upholstery.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(upholstery);

            Texture2D glow;
            var panel = Lit(PanelMaterial, "ChairPanel");
            panel.SetTexture("_BaseMap", PanelPictures(out glow));
            panel.SetTexture("_MainTex", panel.GetTexture("_BaseMap"));
            panel.SetColor("_BaseColor", Color.white);
            panel.SetFloat("_Metallic", 0.1f);
            panel.SetFloat("_Smoothness", 0.4f);
            panel.SetTexture("_EmissionMap", glow);
            panel.SetColor("_EmissionColor", Color.white * 1.1f);
            panel.EnableKeyword("_EMISSION");
            panel.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(panel);

            return new[]
            {
                upholstery,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/PanelDark.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/SteelDark.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Steel.mat"),
                panel,
            };
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

        // ---- 絵 --------------------------------------------------------------------

        /// <summary>張り地の色。黒を基調にした部屋の家具（つや消しの黒の塗り 0.20 ほど）と明るさを揃えた、紫をわずかに残す黒革</summary>
        static readonly Color Leather = new Color(0.180f, 0.165f, 0.190f);
        /// <summary>畝の革（少し暗く青い）と縫い糸（くすんだ藤色）</summary>
        static readonly Color RibLeather = new Color(0.155f, 0.143f, 0.172f);
        static readonly Color Stitch = new Color(0.44f, 0.35f, 0.54f);

        /// <summary>乱れの代わりの決まった値（0〜1）。押すたびに同じ絵にする</summary>
        static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>
        /// 張り地の絵（64×32、点で引く）。左の 32 画素が革の地（1 画素 1 cm）、次の 16 画素が畝の帯（真ん中が膨らんで明るく、両の縫い目の際に藤色の縫い糸）、
        /// 右の 16 画素は同じ帯に小さな穴を並べた物（腰と頭の当て物）。縦は繰り返す
        /// </summary>
        static Texture2D UpholsteryPicture()
        {
            var px = new Color[UpholsteryW * UpholsteryH];
            for (var y = 0; y < UpholsteryH; y++)
                for (var x = 0; x < UpholsteryW; x++)
                {
                    Color c;
                    if (x < 32)
                    {
                        var v = 0.90f + 0.12f * Hash(x, y);
                        if (Hash(x / 3, y + 71) > 0.92f) v *= 0.84f;
                        c = Leather * v;
                    }
                    else
                    {
                        var i = (x - 32) % 16;
                        var perforated = x >= 48;
                        var f = (i + 0.5f) / 16f;
                        var shade = 0.74f + 0.30f * Mathf.Sin(Mathf.PI * f);
                        c = RibLeather * shade * (0.95f + 0.07f * Hash(x, y + 13));
                        if (i == 0 || i == 15) c *= 0.62f;
                        if ((i == 1 || i == 14) && y % 4 != 3) c = Stitch;
                        if (perforated && i >= 4 && i <= 11 && y % 2 == 0 && (i + (y / 2) % 2) % 2 == 0) c *= 0.55f;
                    }
                    c.a = 1f;
                    px[y * UpholsteryW + x] = c;
                }
            return SavePicture(px, UpholsteryW, UpholsteryH, UpholsteryTexture, TextureWrapMode.Repeat);
        }

        static readonly Color Plastic = new Color(0.075f, 0.075f, 0.09f);
        static readonly Color PlasticLight = new Color(0.14f, 0.14f, 0.165f);
        static readonly Color Edge = new Color(0.20f, 0.20f, 0.235f);
        static readonly Color Ink = new Color(0.56f, 0.56f, 0.60f);
        static readonly Color GlassDark = new Color(0.02f, 0.03f, 0.04f);
        static readonly Color Rubber = new Color(0.035f, 0.035f, 0.04f);
        static readonly Color CyanLit = new Color(0.30f, 0.90f, 1.00f);
        static readonly Color VioletLit = new Color(0.66f, 0.40f, 1.00f);
        static readonly Color AmberLit = new Color(1.00f, 0.62f, 0.18f);
        static readonly Color GreenLit = new Color(0.40f, 1.00f, 0.55f);
        static readonly Color RedLit = new Color(1.00f, 0.28f, 0.25f);

        /// <summary>操作盤の絵を描く入れ物。albedo と光る所（glow）を同じ升目で持つ</summary>
        sealed class Canvas
        {
            public readonly Color[] Albedo = new Color[PanelSize * PanelSize];
            public readonly Color[] Glow = new Color[PanelSize * PanelSize];

            public void Put(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= PanelSize || y >= PanelSize) return;
                Albedo[y * PanelSize + x] = c;
                Glow[y * PanelSize + x] = Color.black;
            }

            /// <summary>光る画素。albedo には色を半分に落として置く（灯っていない所から見ても色が分かる）</summary>
            public void Lit(int x, int y, Color c, float level = 1f)
            {
                if (x < 0 || y < 0 || x >= PanelSize || y >= PanelSize) return;
                Albedo[y * PanelSize + x] = c * 0.5f;
                Glow[y * PanelSize + x] = c * level;
            }

            public void Fill(int x, int y, int w, int h, Color c)
            {
                for (var j = y; j < y + h; j++) for (var i = x; i < x + w; i++) Put(i, j, c);
            }

            public void LitFill(int x, int y, int w, int h, Color c, float level = 1f)
            {
                for (var j = y; j < y + h; j++) for (var i = x; i < x + w; i++) Lit(i, j, c, level);
            }

            public void Frame(RectInt r, Color fill, Color edge)
            {
                Fill(r.x, r.y, r.width, r.height, fill);
                for (var i = r.x; i < r.xMax; i++) { Put(i, r.y, edge); Put(i, r.yMax - 1, edge); }
                for (var j = r.y; j < r.yMax; j++) { Put(r.x, j, edge); Put(r.xMax - 1, j, edge); }
            }
        }

        /// <summary>
        /// 操作盤の絵（64×64、点で引く）と、その光る所の絵。下の段に色の升、その上に右と左の肘掛けの前の操作盤と受け皿（前が上）、
        /// 頭の後ろの画面・耳の画面・接続口の板・分岐の箱の面・耳の灯り・灯りの列
        /// </summary>
        static Texture2D PanelPictures(out Texture2D glow)
        {
            var cv = new Canvas();
            cv.Fill(0, 0, PanelSize, PanelSize, Plastic);

            // 色の升
            var swatches = new[] { Rubber, Plastic, new Color(0.33f, 0.34f, 0.36f), new Color(0.28f, 0.15f, 0.42f), new Color(0.07f, 0.33f, 0.36f), new Color(0.60f, 0.28f, 0.07f),
                new Color(0.42f, 0.43f, 0.47f), new Color(0.70f, 0.70f, 0.68f), CyanLit, VioletLit, AmberLit, GreenLit, new Color(0.52f, 0.42f, 0.22f), GlassDark, PlasticLight, RedLit };
            for (var k = 0; k < swatches.Length; k++)
            {
                if (k >= (int)Swatch.Cyan && k <= (int)Swatch.Green || k == (int)Swatch.Red) cv.LitFill(k * 4, 0, 4, 4, swatches[k]);
                else cv.Fill(k * 4, 0, 4, 4, swatches[k]);
            }

            RightDeckPicture(cv, RightDeck);
            LeftDeckPicture(cv, LeftDeck);
            RightPodPicture(cv, RightPod);
            LeftPodPicture(cv, LeftPod);

            // 頭の後ろの箱の画面。紫の見出し、脳波のような波形二本、文字の行
            var s = RearScreen;
            cv.Frame(s, GlassDark, Edge);
            cv.LitFill(s.x + 2, s.yMax - 3, s.width - 4, 1, VioletLit, 0.7f);
            for (var x = s.x + 2; x < s.xMax - 2; x++)
            {
                cv.Lit(x, s.y + 8 + Mathf.RoundToInt(3f * Mathf.Sin(x * 0.75f) * Mathf.Sin(x * 0.21f + 0.4f)), CyanLit);
                cv.Lit(x, s.y + 4 + Mathf.RoundToInt(1.4f * Mathf.Sin(x * 1.3f + 1f)), GreenLit, 0.6f);
            }
            for (var x = s.x + 2; x < s.x + 11; x += 2) cv.Lit(x, s.yMax - 5, CyanLit, 0.5f);
            cv.LitFill(s.x + 14, s.yMax - 5, 6, 1, AmberLit, 0.6f);

            // 耳の小さな画面（縦長）。輪の印と、潜行の深さの棒
            var e = EarScreen;
            cv.Frame(e, GlassDark, Edge);
            for (var y = e.y; y < e.yMax; y++)
                for (var x = e.x; x < e.xMax; x++)
                {
                    var d = new Vector2(x + 0.5f - (e.x + 6f), y + 0.5f - (e.y + 11f)).magnitude;
                    if (d > 2.2f && d < 3.4f) cv.Lit(x, y, CyanLit);
                }
            cv.LitFill(e.x + 2, e.y + 3, 6, 1, GreenLit);
            cv.LitFill(e.x + 2, e.y + 5, 4, 1, GreenLit, 0.8f);
            cv.LitFill(e.x + 2, e.y + 7, 8, 1, VioletLit, 0.6f);

            // 接続口の板。三つの受け口（挿さった端子の後ろ）と、その上の灯り
            var p = PortPlate;
            cv.Frame(p, PlasticLight, Edge);
            var sockets = new[] { p.x + 4, p.x + 10, p.x + 16 };
            var leds = new[] { VioletLit, CyanLit, AmberLit };
            for (var k = 0; k < 3; k++)
            {
                for (var y = p.y + 3; y < p.y + 8; y++)
                    for (var x = sockets[k] - 2; x <= sockets[k] + 2; x++)
                    {
                        var d = new Vector2(x - sockets[k], y - (p.y + 5)).magnitude;
                        if (d <= 1.2f) cv.Put(x, y, Rubber);
                        else if (d <= 2.3f) cv.Put(x, y, new Color(0.42f, 0.43f, 0.47f));
                    }
                cv.Lit(sockets[k], p.y + 9, leds[k]);
                cv.Put(sockets[k] - 1, p.y + 1, Ink);
                cv.Put(sockets[k] + 1, p.y + 1, Ink);
            }

            // 分岐の箱の面。通気の溝、灯りの列、琥珀の縞
            var j2 = JunctionFace;
            cv.Frame(j2, PlasticLight, Edge);
            foreach (var y in new[] { j2.y + 2, j2.y + 4, j2.y + 6 }) cv.Fill(j2.x + 2, y, 12, 1, GlassDark);
            cv.Lit(j2.x + 16, j2.y + 3, GreenLit);
            cv.Lit(j2.x + 16, j2.y + 5, GreenLit);
            cv.Lit(j2.x + 16, j2.y + 7, AmberLit);
            for (var x = j2.x + 2; x < j2.x + 14; x++)
                for (var y = j2.y + 9; y < j2.y + 11; y++)
                    cv.Put(x, y, ((x + y) / 2) % 2 == 0 ? new Color(0.55f, 0.36f, 0.08f) : Plastic);

            // 耳の灯り。紫の輪と、真ん中の水色の点
            var l = EarLamp;
            cv.Fill(l.x, l.y, l.width, l.height, Plastic);
            for (var y = l.y; y < l.yMax; y++)
                for (var x = l.x; x < l.xMax; x++)
                {
                    var d = new Vector2(x + 0.5f - (l.x + 6f), y + 0.5f - (l.y + 6f)).magnitude;
                    if (d > 3.2f && d < 5.0f) cv.Lit(x, y, VioletLit);
                    else if (d >= 5.0f && d < 6f) cv.Put(x, y, Edge);
                    else if (d < 1.5f) cv.Lit(x, y, CyanLit);
                }

            // 灯りの列
            var strip = LedStrip;
            cv.Fill(strip.x, strip.y, strip.width, strip.height, Plastic);
            var cycle = new[] { CyanLit, CyanLit, VioletLit, CyanLit, AmberLit, CyanLit, VioletLit };
            for (var k = 0; k * 3 + 1 < strip.width; k++) cv.LitFill(strip.x + 1 + k * 3, strip.y + 1, 2, 2, cycle[k % cycle.Length]);

            glow = SavePicture(cv.Glow, PanelSize, PanelSize, PanelGlowTexture, TextureWrapMode.Clamp);
            return SavePicture(cv.Albedo, PanelSize, PanelSize, PanelTexture, TextureWrapMode.Clamp);
        }

        /// <summary>
        /// 右の肘掛けの受け皿（x は内から外、y は後ろから前）。後ろに滑り止めの筋、前に差込口の具合を示す灯り三つと紫の帯
        /// </summary>
        static void RightDeckPicture(Canvas cv, RectInt r)
        {
            cv.Frame(r, Plastic, Edge);
            var x0 = r.x;
            var y0 = r.y;
            for (var y = y0 + 2; y < y0 + 13; y += 2) cv.Fill(x0 + 2, y, 8, 1, PlasticLight);
            cv.Fill(x0 + 2, y0 + 17, 8, 1, Ink * 0.6f);
            var leds = new[] { CyanLit, CyanLit, VioletLit };
            for (var k = 0; k < 3; k++) cv.LitFill(x0 + 1 + k * 4, y0 + 20, 2, 2, leds[k]);
            cv.LitFill(x0 + 2, y0 + 24, 8, 1, VioletLit, 0.8f);
        }

        /// <summary>左の肘掛けの受け皿（x は外から内、y は後ろから前）。後ろに滑り止めの筋、真ん中に切り替え二つ、前に緑と琥珀の灯り</summary>
        static void LeftDeckPicture(Canvas cv, RectInt r)
        {
            cv.Frame(r, Plastic, Edge);
            var x0 = r.x;
            var y0 = r.y;
            for (var y = y0 + 2; y < y0 + 11; y += 2) cv.Fill(x0 + 2, y, 8, 1, PlasticLight);
            foreach (var bx in new[] { x0 + 2, x0 + 7 })
            {
                cv.Fill(bx, y0 + 13, 3, 6, PlasticLight);
                cv.Fill(bx + 1, y0 + 16, 1, 3, Ink);
            }
            cv.LitFill(x0 + 2, y0 + 22, 2, 2, GreenLit);
            cv.LitFill(x0 + 6, y0 + 22, 2, 2, GreenLit);
            cv.LitFill(x0 + 9, y0 + 22, 2, 2, AmberLit);
        }

        /// <summary>
        /// 右の肘掛けの前の操作盤（x は内から外、y は手前の低い縁から奥の高い縁）。潜行を扱う盤: 奥に状態の小さな画面、灯りの列、手前に釦二つ（右に紫の灯り）
        /// </summary>
        static void RightPodPicture(Canvas cv, RectInt r)
        {
            cv.Frame(r, Plastic, Edge);
            var x0 = r.x;
            var y0 = r.y;
            cv.Fill(x0 + 2, y0 + 9, 12, 5, GlassDark);
            cv.LitFill(x0 + 3, y0 + 12, 6, 1, CyanLit);
            cv.LitFill(x0 + 3, y0 + 10, 3, 1, VioletLit);
            cv.LitFill(x0 + 7, y0 + 10, 5, 1, GreenLit);
            cv.Lit(x0 + 11, y0 + 12, AmberLit);
            var leds = new[] { CyanLit, CyanLit, VioletLit, AmberLit };
            for (var k = 0; k < 4; k++) cv.LitFill(x0 + 2 + k * 3, y0 + 6, 2, 2, leds[k]);
            cv.Fill(x0 + 2, y0 + 2, 5, 3, PlasticLight);
            cv.Fill(x0 + 9, y0 + 2, 5, 3, PlasticLight);
            cv.Fill(x0 + 2, y0 + 4, 5, 1, Edge);
            cv.Fill(x0 + 9, y0 + 4, 5, 1, Edge);
            cv.LitFill(x0 + 10, y0 + 3, 3, 1, VioletLit);
        }

        /// <summary>
        /// 左の肘掛けの前の操作盤（x は外から内、y は手前の低い縁から奥の高い縁）。椅子を扱う盤: 奥に倒れと張りの目盛りの灯り（緑から琥珀、赤は消えている）、
        /// 手前に紫の灯りの輪の摘みと、切り替え二つ
        /// </summary>
        static void LeftPodPicture(Canvas cv, RectInt r)
        {
            cv.Frame(r, Plastic, Edge);
            var x0 = r.x;
            var y0 = r.y;
            var bars = new[] { GreenLit, GreenLit, GreenLit, AmberLit, AmberLit, RedLit };
            for (var k = 0; k < 6; k++) cv.LitFill(x0 + 2 + k * 2, y0 + 11, 2, 3, bars[k], k == 5 ? 0.2f : 1f);
            for (var y = y0 + 1; y < y0 + 10; y++)
                for (var x = x0 + 1; x < x0 + 10; x++)
                {
                    var v = new Vector2(x + 0.5f - (x0 + 5.5f), y + 0.5f - (y0 + 5.5f));
                    var d = v.magnitude;
                    if (d < 1.8f) cv.Put(x, y, PlasticLight);
                    else if (d < 2.6f) cv.Put(x, y, Edge);
                    else if (d < 3.9f)
                    {
                        var a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                        if (a > -50f && a < 230f) cv.Lit(x, y, VioletLit);
                    }
                }
            foreach (var bx in new[] { x0 + 11, x0 + 13 })
            {
                cv.Fill(bx, y0 + 2, 2, 6, PlasticLight);
                cv.Lit(bx, y0 + 8, CyanLit);
            }
        }

        /// <summary>絵を置き（中身が変わった時だけ書く）、点で引く・mipmap 無し・圧縮無しで取り込む</summary>
        static Texture2D SavePicture(Color[] px, int w, int h, string path, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            tex.SetPixels(px);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var full = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            var same = File.Exists(full) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(full), bytes);
            if (!same) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.mipmapEnabled || imp.filterMode != FilterMode.Point || imp.wrapMode != wrap
                || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = wrap;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- 場面の中 ----------------------------------------------------------------

        /// <summary>根から名前で辿る。切ってある物も辿れる</summary>
        public static Transform Find(Scene scene, string path)
        {
            var cut = path.IndexOf('/');
            var head = cut < 0 ? path : path.Substring(0, cut);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name != head) continue;
                return cut < 0 ? go.transform : go.transform.Find(path.Substring(cut + 1));
            }
            return null;
        }

        /// <summary>椅子の外の物の一覧（道筋）。椅子（Room/Chair）の下は数えない</summary>
        public static List<string> Census(Scene scene)
        {
            var list = new List<string>();
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, r.name, list);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        static void Walk(Transform t, string path, List<string> list)
        {
            if (path == ChairPath) { list.Add(path); return; }
            list.Add(path);
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path + "/" + t.GetChild(i).name, list);
        }

        /// <summary>椅子の子の名前の並び（孫は Blocker の下だけ）</summary>
        public static string ChairChildren(Scene scene)
        {
            var chair = Find(scene, ChairPath);
            if (chair == null) return "無し";
            var names = new List<string>();
            foreach (Transform c in chair)
            {
                var n = c.name + (c.gameObject.activeSelf ? "" : "（切）");
                if (c.name == "Blocker")
                {
                    var kids = new List<string>();
                    foreach (Transform k in c) kids.Add(k.name);
                    n += "［" + string.Join("・", kids.ToArray()) + "］";
                }
                if (c.name == "JackRest" && c.childCount > 0)
                {
                    var kids = new List<string>();
                    foreach (Transform k in c) kids.Add(k.name);
                    n += "［" + string.Join("・", kids.ToArray()) + "］";
                }
                names.Add(n);
            }
            return names.Count + " 個: " + string.Join("、", names.ToArray());
        }
    }
}
