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
    /// 自室（<c>Room.unity</c>）の椅子を組む（シナリオ設計 5 節）。潜り手が一日の大半を過ごし、長い潜行に体を預ける椅子。
    /// 上等な黒革の重役の椅子に、仕事の道具として後から電子機器を金具で留め、ケーブルを這わせた形。
    ///
    /// <list type="table">
    /// <item><term>形</term><description>椅子: 縁に縫い目を回した厚く柔らかい革の座面、前も裏も革で包んだ厚く幅の広い背もたれ
    /// （脇の太い巻きと上の頭の当ての巻きを縫い目で区切り、内は大きな菱形のボタン留め）、黒い漆の台に載せた厚く丸い革の肘掛けと、
    /// それを支える磨いた金属の腕、磨いた金属の五本脚とガスシリンダー、リクライニングの機構とレバー、座の前の下から引き出す革のフットレスト。後から付けた物: 頭の後ろの黒い金属の箱（背もたれの上の縁に掛けた留め具で留める）と耳の筐体、
    /// 左右の肘掛けの前の操作盤（当て物の下から回り込む留め具で留める）、右の肘掛けの差込口（螺子で留めた金属の板）、
    /// 背もたれの裏と金属の腕に沿って這わせたケーブル（<c>BuildChairShape.cs</c>）</description></item>
    /// <item><term>体との取り合い</term><description>座った体の形（SeatedPose）を組み直さずに済むよう、座面の高さの線・肘掛けの上面・差込口・
    /// ジャックの置き場は前の椅子と同じ所。フットレストは座った形の足の裏が乗る所まで引き出した形</description></item>
    /// <item><term>軽さ</term><description>輪郭を作る起伏（背もたれの厚みと縁の巻き、菱形の膨らみ、肘掛けと座面の厚み、脚の形）は mesh に残し、
    /// 面の上の細かい起伏（ボタンの窪み、折り目の鋭さ、縫い目、皺、螺子）は絵（地の色に焼いた窪みの陰と、法線の絵）へ移す（オーナー、2026-09-30
    /// 「椅子も含めて重くなりそうなところはテクスチャを張ることで軽くして」「テクスチャに変えても立体感は失われないように」）。
    /// マテリアルは一つ（描く回数 1）</description></item>
    /// <item><term>置き場</term><description><c>Room/Chair</c> の下の見た目の子を <c>ChairMesh</c> 一つ（マテリアル 1 つ）に替える。
    /// 働きを持つ子（<c>Blocker</c> とその当たり・<c>JackRest</c>・<c>Cable</c>・<c>PortHole</c>）は名前のまま残し、当たりの大きさだけ新しい形に合わせる。
    /// PortHole はケーブルの始まりの置き場として残し、見た目（前の黒い箱）は外す</description></item>
    /// <item><term>焼き物</term><description>mesh は <c>Assets/Models/generated/chair/Chair.asset</c>、絵は <c>Assets/Textures/Chair/</c>（点で引く粗い絵）、
    /// マテリアルは <c>Assets/Materials/Room/Chair.mat</c>（URP Lit。絵は 128×128 の地の色と艶・法線・光る所の三枚）。
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
        /// <summary>椅子の絵（地の色と艶・法線・光る所）とマテリアル（一つ）</summary>
        public const string AtlasTexture = TextureDir + "Chair.png";
        public const string NormalTexture = TextureDir + "ChairNormal.png";
        public const string GlowTexture = TextureDir + "ChairGlow.png";
        public const string ChairMaterial = "Assets/Materials/Room/Chair.mat";

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
            sb.AppendFormat("椅子を組んだ。三角 {0}・頂点 {1}・部品 {2}・マテリアル {3}（描く回数 {3}）。mesh {4:0.00} MB。絵 {5}×{5} を 3 枚（地の色と艶・法線・光る所）",
                made.Triangles, made.Vertices, made.Pieces.Count, mats.Length, MeshSize(), AtlasSize).AppendLine();
            sb.AppendLine(port);
            sb.AppendLine(blocker);
            sb.Append(push);
            return sb.ToString();
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
        /// 立ち上がる時に椅子を後ろへ押し下げる距離（SceneFlow の chairPushBack）。始めの椅子は 0.24 m。
        /// フットレスト（当たりは持たない）の先（z 0.445）が、机との間の立ち位置（z 1.68、当たりの半径 0.3 m）の当たりの縁 1.38 から 3 cm 手前に来る所まで下げる。
        /// 椅子の後ろ（押し下げた背の後ろの端は z 0.27）は床とラグだけ
        /// </summary>
        public const float PushBack = 0.38f;

        /// <summary>
        /// 立った後の当たり（Room/Chair/Blocker の子）を新しい形に合わせる。名前は前のまま（Seat・Base・Back・ArmR・ArmL）。
        /// **どの当たりも床から立てる。** 床の近くに低い段（五本脚だけの低い箱や、足元の空いた背・肘掛けの箱）があると、
        /// 体の当たり（段を 0.3 m まで乗り越える）が乗り上げて、椅子の脇や前を歩くと目線が上下にかくついた（オーナー、2026-09-30）。
        /// フットレストには当たりを持たせない（パッドは床から 3〜17 cm の低い物で、乗り上げの元になる）。
        /// どの当たりも、立ち上がって <see cref="PushBack"/> 下げた時に、机との間の立ち位置の当たりに掛からない
        /// </summary>
        static string Blockers(Transform chair)
        {
            var root = chair.Find("Blocker");
            if (root == null) return "当たり（Blocker）が無い";
            var sb = new StringBuilder("当たり: ");
            // 座面
            sb.Append(Box(root, "Seat", new Vector3(0f, 0.40f, 0.04f), new Vector3(0.60f, 0.80f, 0.50f)));
            // 五本脚（脚の先のキャスターまで）
            sb.Append(Box(root, "Base", new Vector3(0f, 0.40f, 0f), new Vector3(0.64f, 0.80f, 0.62f)));
            // 背もたれと頭の後ろの端末・耳の筐体
            sb.Append(Box(root, "Back", new Vector3(0f, 0.75f, -0.39f), new Vector3(0.62f, 1.50f, 0.48f)));
            // 肘掛け（前の操作盤まで）
            sb.Append(Box(root, "ArmR", new Vector3(0.345f, 0.40f, 0.07f), new Vector3(0.20f, 0.80f, 0.54f)));
            sb.Append(Box(root, "ArmL", new Vector3(-0.345f, 0.40f, 0.07f), new Vector3(0.20f, 0.80f, 0.54f)));
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
            existing.SetTangents(mesh.tangents);
            existing.subMeshCount = mesh.subMeshCount;
            for (var i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i, false);
            existing.RecalculateBounds();
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---- マテリアル --------------------------------------------------------------

        /// <summary>
        /// 椅子のマテリアル一つ（URP Lit）。地の色の絵（α は艶）・法線の絵・光る所の絵を一枚ずつ。金属も革も同じ一つで描く（描く回数 1）
        /// </summary>
        static Material[] Materials()
        {
            Texture2D normal, glow;
            var atlas = Pictures(out normal, out glow);
            var m = AssetDatabase.LoadAssetAtPath<Material>(ChairMaterial);
            if (m == null)
            {
                m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Steel.mat")) { name = "Chair" };
                AssetDatabase.CreateAsset(m, ChairMaterial);
            }
            m.SetTexture("_BaseMap", atlas);
            m.SetTexture("_MainTex", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_EmissionMap", glow);
            m.SetColor("_EmissionColor", Color.white * 1.1f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return new[] { m };
        }

        // ---- 絵 --------------------------------------------------------------------

        /// <summary>
        /// 張り地の色と艶。部屋の黒い家具（ソファの黒い革 0.075・艶 0.42）に揃えた黒革。艶を強くすると、ゲームの明かりで座面の平らな面が
        /// 白く返って明るい灰色の板に見えるので、艶は控えめにし、照り返しは縁の丸みとボタン留めの膨らみに細く乗る程度にする
        /// </summary>
        static readonly Color Leather = new Color(0.066f, 0.061f, 0.066f);
        const float LeatherGloss = 0.30f;
        /// <summary>
        /// 金属と漆の色と艶（前の部屋のマテリアル Steel・SteelDark・PanelDark の見え方に寄せる）。一つのマテリアルで描くので金属の度合いは持たせず、
        /// 金属は地の色を暗めにして艶を強くし、照り返しで金属らしく見せる
        /// </summary>
        static readonly Color ChromeColour = new Color(0.28f, 0.29f, 0.32f);
        static readonly Color SteelColour = new Color(0.09f, 0.095f, 0.11f);
        static readonly Color LacquerColour = new Color(0.06f, 0.065f, 0.08f);

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

        /// <summary>椅子の絵を描く入れ物（128×128）。地の色・艶・光る所・法線を同じ升目で持つ</summary>
        sealed class Canvas
        {
            public readonly Color[] Albedo = new Color[AtlasSize * AtlasSize];
            public readonly float[] Gloss = new float[AtlasSize * AtlasSize];
            public readonly Color[] Glow = new Color[AtlasSize * AtlasSize];
            public readonly Color[] Normal = new Color[AtlasSize * AtlasSize];
            /// <summary>この後に置く画素の艶</summary>
            public float Shine = 0.4f;

            public Canvas()
            {
                for (var i = 0; i < Normal.Length; i++) Normal[i] = new Color(0.5f, 0.5f, 1f);
            }

            public void Put(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= AtlasSize || y >= AtlasSize) return;
                Albedo[y * AtlasSize + x] = c;
                Gloss[y * AtlasSize + x] = Shine;
                Glow[y * AtlasSize + x] = Color.black;
            }

            /// <summary>光る画素。albedo には色を半分に落として置く（灯っていない所から見ても色が分かる）</summary>
            public void Lit(int x, int y, Color c, float level = 1f)
            {
                if (x < 0 || y < 0 || x >= AtlasSize || y >= AtlasSize) return;
                Albedo[y * AtlasSize + x] = c * 0.5f;
                Gloss[y * AtlasSize + x] = 0.6f;
                Glow[y * AtlasSize + x] = c * level;
            }

            /// <summary>法線（接線の座標。x は絵の右、y は絵の上）</summary>
            public void Bump(int x, int y, Vector3 n)
            {
                if (x < 0 || y < 0 || x >= AtlasSize || y >= AtlasSize) return;
                n.Normalize();
                Normal[y * AtlasSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
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

            /// <summary>地の色に艶を α として重ねた絵</summary>
            public Color[] AlbedoWithGloss()
            {
                var o = new Color[Albedo.Length];
                for (var i = 0; i < o.Length; i++) { o[i] = Albedo[i]; o[i].a = Gloss[i]; }
                return o;
            }
        }

        /// <summary>
        /// 椅子の絵（128×128、点で引く）を描いて置く。地の色（α は艶）・法線・光る所の三枚。
        /// 左下の 64×64 は操作盤・画面・色の升、ほかは革の面ごとの絵（背もたれの前と裏、肘掛け、座面、フットレスト）
        /// </summary>
        static Texture2D Pictures(out Texture2D normal, out Texture2D glow)
        {
            var cv = new Canvas();
            PanelPictures(cv);
            LeatherPictures(cv);
            normal = SavePicture(cv.Normal, AtlasSize, AtlasSize, NormalTexture, PictureKind.Normal);
            glow = SavePicture(cv.Glow, AtlasSize, AtlasSize, GlowTexture, PictureKind.Colour);
            return SavePicture(cv.AlbedoWithGloss(), AtlasSize, AtlasSize, AtlasTexture, PictureKind.ColourWithGloss);
        }

        /// <summary>
        /// 操作盤の絵。下の段に色の升、その上に右と左の肘掛けの前の操作盤（前が上）、耳の画面・分岐の箱の面・耳の灯り・灯りの列、
        /// 頭の後ろの箱の裏の面（画面・灯りの列・接続口・四隅の螺子）、操作盤の外の面（螺子二つ）。左上の段に金属と漆と革の色の升
        /// </summary>
        static void PanelPictures(Canvas cv)
        {
            cv.Shine = 0.35f;
            cv.Fill(0, 0, 64, 64, Plastic);

            // 色の升
            var swatches = new[] { Rubber, Plastic, new Color(0.33f, 0.34f, 0.36f), new Color(0.28f, 0.15f, 0.42f), new Color(0.07f, 0.33f, 0.36f), new Color(0.60f, 0.28f, 0.07f),
                new Color(0.42f, 0.43f, 0.47f), new Color(0.70f, 0.70f, 0.68f), CyanLit, VioletLit, AmberLit, GreenLit, new Color(0.52f, 0.42f, 0.22f), GlassDark, PlasticLight, RedLit };
            var gloss = new[] { 0.15f, 0.35f, 0.4f, 0.4f, 0.4f, 0.4f, 0.7f, 0.3f, 0.6f, 0.6f, 0.6f, 0.6f, 0.6f, 0.7f, 0.35f, 0.6f };
            for (var k = 0; k < swatches.Length; k++)
            {
                cv.Shine = gloss[k];
                if (k >= (int)Swatch.Cyan && k <= (int)Swatch.Green || k == (int)Swatch.Red) cv.LitFill(k * 4, 0, 4, 4, swatches[k]);
                else cv.Fill(k * 4, 0, 4, 4, swatches[k]);
            }
            // 左上の段: 磨いた金属・黒い金属・黒い漆・黒革
            var metals = new[] { ChromeColour, SteelColour, LacquerColour, Leather };
            var metalGloss = new[] { 0.85f, 0.35f, 0.3f, LeatherGloss };
            for (var k = 0; k < metals.Length; k++)
            {
                cv.Shine = metalGloss[k];
                cv.Fill(k * 4, 60, 4, 4, metals[k]);
            }

            cv.Shine = 0.35f;
            RightPodPicture(cv, RightPod);
            LeftPodPicture(cv, LeftPod);

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

            CrownFacePicture(cv, CrownFace);
            PodSidePicture(cv, PodSide);
        }

        /// <summary>留めた螺子（2×2 画素。明るい頭と、暗い溝）</summary>
        static void Screw(Canvas cv, int x, int y)
        {
            cv.Shine = 0.7f;
            cv.Put(x, y, new Color(0.46f, 0.47f, 0.50f));
            cv.Put(x + 1, y, new Color(0.30f, 0.31f, 0.34f));
            cv.Put(x, y + 1, new Color(0.56f, 0.57f, 0.60f));
            cv.Put(x + 1, y + 1, new Color(0.46f, 0.47f, 0.50f));
            // 頭の丸み（左上が明るく、右下が暗い。法線で明かりに応じて返す）
            cv.Bump(x, y + 1, new Vector3(-0.5f, 0.5f, 1f));
            cv.Bump(x + 1, y + 1, new Vector3(0.5f, 0.5f, 1f));
            cv.Bump(x, y, new Vector3(-0.5f, -0.5f, 1f));
            cv.Bump(x + 1, y, new Vector3(0.5f, -0.5f, 1f));
        }

        /// <summary>
        /// 頭の後ろの箱の裏の面（26×28。x は椅子の右、y は背もたれに沿って上）。黒い金属の地に、上に灯りの列、その下に画面（紫の見出し、脳波のような波形二本）、
        /// 下に接続口の板（三つの受け口と灯り。挿さった端子の後ろ）、四隅の螺子
        /// </summary>
        static void CrownFacePicture(Canvas cv, RectInt r)
        {
            cv.Shine = 0.5f;
            cv.Frame(r, SteelColour, new Color(0.26f, 0.27f, 0.31f));
            var x0 = r.x;
            var y0 = r.y;
            // 画面
            var s = new RectInt(x0 + 5, y0 + 13, 16, 10);
            cv.Shine = 0.6f;
            cv.Frame(s, GlassDark, Edge);
            cv.LitFill(s.x + 2, s.yMax - 2, s.width - 4, 1, VioletLit, 0.7f);
            for (var x = s.x + 1; x < s.xMax - 1; x++)
            {
                cv.Lit(x, s.y + 5 + Mathf.RoundToInt(1.6f * Mathf.Sin(x * 0.9f) * Mathf.Sin(x * 0.27f + 0.4f)), CyanLit);
                cv.Lit(x, s.y + 2 + Mathf.RoundToInt(0.8f * Mathf.Sin(x * 1.3f + 1f)), GreenLit, 0.6f);
            }
            // 灯りの列
            var cycle = new[] { CyanLit, VioletLit, CyanLit, AmberLit, CyanLit, VioletLit };
            for (var k = 0; k < 6; k++) cv.Lit(x0 + 6 + k * 3, y0 + 25, cycle[k]);
            // 接続口の板
            cv.Shine = 0.35f;
            var p = new RectInt(x0 + 4, y0 + 2, 18, 9);
            cv.Frame(p, PlasticLight, Edge);
            var sockets = new[] { x0 + 8, x0 + 13, x0 + 18 };
            var leds = new[] { VioletLit, CyanLit, AmberLit };
            for (var k = 0; k < 3; k++)
            {
                cv.Fill(sockets[k] - 1, p.y + 3, 3, 3, new Color(0.42f, 0.43f, 0.47f));
                cv.Put(sockets[k], p.y + 4, Rubber);
                cv.Lit(sockets[k], p.y + 7, leds[k]);
            }
            // 四隅の螺子
            foreach (var x in new[] { x0 + 1, x0 + r.width - 3 })
                foreach (var y in new[] { y0 + 1, y0 + r.height - 3 })
                    Screw(cv, x, y);
        }

        /// <summary>操作盤の外の面（16×6。x は後ろから前、y は下から上）。黒い金属の地に、留めた螺子二つ</summary>
        static void PodSidePicture(Canvas cv, RectInt r)
        {
            cv.Shine = 0.5f;
            cv.Fill(r.x, r.y, r.width, r.height, SteelColour);
            Screw(cv, r.x + 3, r.y + 2);
            Screw(cv, r.x + 11, r.y + 2);
        }

        /// <summary>革の地の明るさ（1 の前後）。細かい粒と、ところどころの浅い皺の筋</summary>
        static float Grain(int x, int y)
        {
            var v = 0.93f + 0.10f * Hash(x, y) + 0.05f * Hash(x / 3, y / 3 + 50);
            // 皺（明るい筋のすぐ下に暗い筋）。ところどころ
            var k = Hash(x / 5 + 7, y / 3 + 13);
            if (k > 0.93f && (x + y) % 3 == 0) v += 0.18f;
            return v;
        }

        /// <summary>
        /// 革の面の絵。置き場の画素ごとに、椅子の座標へ戻して細かい起伏（depth、m、正で窪む）を測り、
        /// 地の色には窪みの陰（明かりの向きに依らない、窪みほど暗い陰）を焼き込み、法線の絵には窪みの斜面を描く（明かりに応じた陰影が出る）。
        /// normal を渡した面（背もたれの前）は、法線をそれで決める
        /// </summary>
        static void PaintLeather(Canvas cv, RectInt r, float x0, float x1, float y0, float y1, System.Func<float, float, float> depth, System.Func<float, float, float> shade,
            System.Func<float, float, float, float, Vector3> normal = null, System.Func<float, float, float> matte = null)
        {
            var dx = (x1 - x0) / r.width;
            var dy = (y1 - y0) / r.height;
            for (var j = 0; j < r.height; j++)
                for (var i = 0; i < r.width; i++)
                {
                    var x = x0 + (i + 0.5f) * dx;
                    var y = y0 + (j + 0.5f) * dy;
                    var d = depth(x, y);
                    var dark = Mathf.Clamp01(d / 0.012f);
                    var c = Leather * (Grain(r.x + i, r.y + j) * (1f - 0.5f * dark) * shade(x, y));
                    c.a = 1f;
                    cv.Shine = Mathf.Lerp(LeatherGloss, 0.12f, Mathf.Max(dark, matte != null ? matte(x, y) : 0f));
                    cv.Put(r.x + i, r.y + j, c);
                    if (normal != null)
                    {
                        cv.Bump(r.x + i, r.y + j, normal(x, y, dx, dy));
                        continue;
                    }
                    // 高さ（-depth）の傾き
                    var gx = -(depth(x + dx, y) - depth(x - dx, y)) / (2f * dx);
                    var gy = -(depth(x, y + dy) - depth(x, y - dy)) / (2f * dy);
                    cv.Bump(r.x + i, r.y + j, new Vector3(-gx, -gy, 1f));
                }
        }

        /// <summary>
        /// 革の面の絵を描く。背もたれの前（菱形のボタン留めの窪み・折り目の鋭い溝・脇と上の縫い目）、裏（無地）、
        /// 肘掛け（上の面の縁の縫い目）、座面とフットレスト（縁の縫い目）
        /// </summary>
        static void LeatherPictures(Canvas cv)
        {
            System.Func<float, float, float> none = (x, y) => 0f;
            System.Func<float, float, float> plain = (x, y) => 1f;
            // 背もたれの前。ボタン留めの菱形は、膨らみの頂を少し明るく、折り目に寄るほど暗くして、形の読みを助ける（明かりの向きに依らない陰）
            PaintLeather(cv, BackFrontArea, -BackHalfW, BackHalfW, 0f, BackLength,
                (x, s) => Mathf.Abs(x) > BackW(s) ? 0f : BackFine(x, s),
                (x, s) =>
                {
                    var inside = Mathf.Abs(x) > BackW(s) ? 0f : TuftInside(x, s);
                    // 折り目の際の急な斜面は窪みの陰で暗くする（明かりを受けた側が一画素の明るい筋にならないように）
                    return 1f + inside * (0.18f * TuftDome(x, s) - 0.10f - 0.5f * TuftCrease(x, s));
                },
                (x, s, ex, es) => Mathf.Abs(x) > BackW(s) - 0.002f ? Vector3.forward : BackFrontNormal(x, s, 1.5f * ex, 1.5f * es),
                // 折り目の際（急な斜面）は艶を落とす（照り返しが一画素の明るい筋にならないように）
                (x, s) => Mathf.Abs(x) > BackW(s) ? 0f : TuftInside(x, s) * TuftCrease(x, s));
            // 背もたれの裏（無地）
            PaintLeather(cv, BackRearArea, -BackHalfW, BackHalfW, 0f, BackLength, none, plain);
            // 肘掛けの上の面（上から写す）。上の面と脇の面の境の縫い目
            PaintLeather(cv, ArmArea, ArmAreaX0, ArmAreaX1, ArmAreaZ0, ArmAreaZ1,
                (x, z) =>
                {
                    if (z < PadBack + 0.02f || z > PadFront - 0.02f) return 0f;
                    var r = PadRound(z);
                    var seam = Mathf.Max(Mathf.Exp(-Mathf.Pow((x - (PadIn(z) + 0.3f * r)) / 0.004f, 2f)), Mathf.Exp(-Mathf.Pow((x - (PadOuter - 0.3f * r)) / 0.004f, 2f)));
                    return 0.004f * seam;
                }, plain);
            // 座面とフットレスト（上から写す）
            PaintLeather(cv, SeatArea, -SeatHalf, SeatHalf, SeatAreaZ0, SeatAreaZ1, (x, z) => 0.005f * Welt(x, z, SeatWeltX, SeatWeltZ), plain);
            PaintLeather(cv, FootArea, -FootHalf, FootHalf, FootAreaZ0, FootAreaZ1, (x, z) => 0.004f * Welt(x, z, FootWeltX, FootWeltZ), plain);
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
            // 釦の出っ張り（上の縁が明るく返る）
            for (var x = x0 + 2; x < x0 + 14; x++) if (x < x0 + 7 || x >= x0 + 9) cv.Bump(x, y0 + 4, new Vector3(0f, 0.6f, 1f));
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
                    // 摘みの丸み
                    if (d < 2.6f && d > 0.5f) cv.Bump(x, y, new Vector3(v.x / d * 0.5f, v.y / d * 0.5f, 1f));
                }
            foreach (var bx in new[] { x0 + 11, x0 + 13 })
            {
                cv.Fill(bx, y0 + 2, 2, 6, PlasticLight);
                cv.Lit(bx, y0 + 8, CyanLit);
            }
        }

        /// <summary>絵の種類。地の色（α 無し）、地の色と艶（α）、法線</summary>
        enum PictureKind { Colour, ColourWithGloss, Normal }

        /// <summary>絵を置き（中身が変わった時だけ書く）、点で引く・mipmap 無し・圧縮無しで取り込む</summary>
        static Texture2D SavePicture(Color[] px, int w, int h, string path, PictureKind kind)
        {
            var tex = new Texture2D(w, h, kind == PictureKind.ColourWithGloss ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, kind == PictureKind.Normal);
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
            var type = kind == PictureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var alpha = kind == PictureKind.ColourWithGloss ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (imp != null && (imp.textureType != type || imp.alphaSource != alpha || imp.mipmapEnabled || imp.filterMode != FilterMode.Point
                || imp.wrapMode != TextureWrapMode.Clamp || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.textureType = type;
                imp.sRGBTexture = kind != PictureKind.Normal;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = alpha;
                imp.alphaIsTransparency = false;
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
