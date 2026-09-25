using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 のモブを Rocketbox に替える前の、色とインプラントの試し（<c>docs/superpowers/specs/2026-09-25-alley-mob-design.md</c> の 4・5 節）。
    ///
    /// 試しの 6 人（<see cref="RocketboxMob.Trial"/>）を、今の群衆と同じく姿勢を曲げて焼いた静止した形で置く。
    /// 通りの 5 人は通りの入口の西の歩道（ネオンの看板の下）に、売り手はヤードの空いた出店の奥に座らせる。
    /// 服の色は元のまま（オーナーが決めた。暗く・彩度を落とした色と、今の群衆の灰色の半透明は、見比べて外した）。
    /// 肌にはインプラント（<see cref="RocketboxMobPaint"/>）を描く。メニューで試しを出し入れできる（<see cref="Show"/>）。
    ///
    /// 試しの物は場面の根の MobTrial の下に置く。路地裏の根（Alley）の下に置くと、<c>HalfAware/Build the alley</c> が知らない子として落とす。
    /// 今の群衆（Alley/Crowd）には触らない。焼いた mesh とテクスチャとマテリアルは <see cref="Folder"/>（リポジトリに入れない）に置く
    /// </summary>
    public static class BuildAlleyMobTrial
    {
        public const string RootName = "MobTrial";
        public const string Folder = BuildAlley.Generated + "MobTrial/";
        /// <summary>人を並べる組の名前（MobTrial の子）</summary>
        public const string GroupName = "People";

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
        }

        /// <summary>通りの 5 人の並び。at は並びの中心から（x は壁の側へ、z は通りの奥へ、m）、yaw は通りの奥を 0 とした向き（度、壁の側へ回るのが正）</summary>
        struct Slot
        {
            public int who;
            public Vector2 at;
            public float yaw;
            public Pose pose;

            public Slot(int who, float x, float z, float yaw, Pose pose)
            {
                this.who = who;
                at = new Vector2(x, z);
                this.yaw = yaw;
                this.pose = pose;
            }
        }

        /// <summary>
        /// 通りの並び。手前に女大 04（こちらを向いて立つ）、真ん中に男大 04 と 17 の二人組（肩を寄せて斜めに向かい合う。17 は腕を組む）、
        /// 奥に女大 01（片脚に預ける）と男大 20（手を後ろで組む）
        /// </summary>
        static readonly Slot[] Street =
        {
            new Slot(0, 0.05f, -1.45f, 195f, Pose.Stand),
            new Slot(1, -0.25f, -0.25f, 45f, Pose.Turn),
            new Slot(2, 0.30f, 0.30f, 225f, Pose.Crossed),
            new Slot(3, -0.15f, 1.35f, 160f, Pose.Rest),
            new Slot(4, 0.25f, 2.35f, 200f, Pose.Behind),
        };

        /// <summary>保存する並びの中心（通りの入口の西の歩道、看板 Sign8 のネオンの下。プレイヤーの目から 6 m ほど）と、壁の側（西は -1）</summary>
        public static readonly Vector3 StreetAt = new Vector3(-3.85f, BuildAlley.KerbRise, 3.1f);
        public const float StreetWall = -1f;
        /// <summary>売り手を座らせる出店（腰掛けの無い店）</summary>
        public const string SellerStall = "Stall3";
        /// <summary>売り手の腰掛けの天面の高さ（BuildAlley の腰掛けと同じ）</summary>
        const float StoolTop = 0.425f;

        // ---- 組み立て ------------------------------------------------------------

        [MenuItem("HalfAware/Alley mob trial/Build", false, 300)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (GameObject.Find("Alley") == null) { Debug.LogError("路地裏の場面を開いてから組む"); return; }
            var sb = new StringBuilder("路地裏のモブの色の試しを組んだ\n");
            Build(sb);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        [MenuItem("HalfAware/Alley mob trial/Show the trial", false, 310)]
        public static void ShowMenu() { Show(true, true); }
        [MenuItem("HalfAware/Alley mob trial/Hide the trial", false, 311)]
        public static void HideMenu() { Show(false, true); }

        /// <summary>試しの人を出すか伏せるか。mark なら場面を汚れた印にする（保存はしない）</summary>
        public static void Show(bool show, bool mark)
        {
            var root = FindRoot();
            if (root == null) { Debug.LogWarning("試しが組まれていない（HalfAware/Alley mob trial/Build）"); return; }
            var t = root.Find(GroupName);
            if (t != null) t.gameObject.SetActive(show);
            if (mark) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        public static Transform FindRoot()
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == RootName) return go.transform;
            return null;
        }

        /// <summary>テクスチャ・マテリアル・焼いた形を作り、場面の MobTrial を組み直す</summary>
        public static void Build(StringBuilder sb)
        {
            EnsureFolder();
            fresh = new HashSet<string>();
            try
            {
                BuildScene(sb);
            }
            finally
            {
                fresh = null;
            }
        }

        /// <summary>組み立ての間に焼き直した形（人と姿勢）。組み立てでは、使う形を一度ずつ焼き直す（姿勢の値を直したら組み直すだけで反映されるように）</summary>
        static HashSet<string> fresh;

        static void BuildScene(StringBuilder sb)
        {
            var people = RocketboxMob.Trial;
            var mats = new Dictionary<string, Material[]>();
            foreach (var who in people) mats[who.Name] = Materials(who, sb);

            var root = FindRoot();
            if (root == null) root = new GameObject(RootName).transform;
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            for (var i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);

            // 売り手の置き場: 腰掛けの無い出店の奥。BuildAlley の売り手と同じく、座ると膝が出るぶん奥へ下げる
            var stall = GameObject.Find("Alley/Market/" + SellerStall);
            Vector3 sit = Vector3.zero;
            var sitYaw = 0f;
            if (stall == null) sb.AppendLine("出店が無い: " + SellerStall);
            else
            {
                var yaw = stall.transform.eulerAngles.y;
                sit = stall.transform.position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.95f);
                sit.y = 0.02f;
                sitYaw = yaw + 180f;
                Stool(root, sit, yaw);
            }

            var group = new GameObject(GroupName).transform;
            group.SetParent(root, false);
            Lineup(group, mats, StreetAt, StreetWall);
            if (stall != null)
            {
                var seller = people[people.Length - 1];
                Person(group, "Seller_" + seller.Name, seller, Pose.Sit, sit, sitYaw, mats[seller.Name]);
            }
            sb.AppendFormat("通りの 5 人: 西の歩道 {0}、売り手: {1} の奥 {2}（向き {3:F0} 度）", StreetAt.ToString("F2"), SellerStall, sit.ToString("F2"), sitYaw).AppendLine();
        }

        /// <summary>
        /// 通りの 5 人を並べる。center は並びの中心（歩道の上）、wall は壁の側（東は +1、西は -1）。
        /// 撮り比べで距離と明るさを変えて並べ直すのにも使う
        /// </summary>
        public static void Lineup(Transform parent, Dictionary<string, Material[]> mats, Vector3 center, float wall)
        {
            var people = RocketboxMob.Trial;
            foreach (var s in Street)
            {
                var who = people[s.who];
                var at = center + new Vector3(s.at.x * wall, 0f, s.at.y);
                var yaw = wall > 0f ? s.yaw : -s.yaw;
                Material[] m;
                if (mats == null || !mats.TryGetValue(who.Name, out m)) m = LoadMaterials(who);
                Person(parent, who.Name, who, s.pose, at, yaw, m);
            }
        }

        static GameObject Person(Transform parent, string name, RocketboxMob who, Pose pose, Vector3 at, float yaw, Material[] mats)
        {
            var mesh = fresh != null && fresh.Add(MeshPath(who, pose)) ? null : AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(who, pose));
            if (mesh == null) mesh = Bake(who, pose);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            // 今の群衆と同じく影は落とさない（灯りは影を持たない）
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>売り手の腰掛け（BuildAlley の腰掛けと同じ形）</summary>
        static void Stool(Transform root, Vector3 at, float yaw)
        {
            var g = new GameObject("Stool").transform;
            g.SetParent(root, false);
            g.position = at;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cube(g, "Seat", new Vector3(0f, 0.40f, 0f), new Vector3(0.36f, 0.05f, 0.34f), "Timber");
            for (var i = 0; i < 4; i++)
                Cube(g, "Leg" + i, new Vector3((i % 2 == 0 ? -1 : 1) * 0.14f, 0.19f, (i < 2 ? -1 : 1) * 0.13f), new Vector3(0.04f, 0.38f, 0.04f), "Pole");
        }

        static void Cube(Transform parent, string name, Vector3 centre, Vector3 size, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BuildAlley.Materials + material + ".mat");
        }

        // ---- 焼いた形 --------------------------------------------------------------

        public static string MeshPath(RocketboxMob who, Pose pose) { return Folder + who.Name + "_" + pose + ".asset"; }

        /// <summary>
        /// 一人を姿勢に曲げて焼き、骨を持たない mesh にする（面の組は体・頭・髪の房のまま）。
        /// 原点は足元で、+z を向く。靴の裏（いちばん低い頂点）を 0 に揃える
        /// </summary>
        public static Mesh Bake(RocketboxMob who, Pose pose)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Alley mob trial/Import the six）: " + who.Model);
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
                RocketboxJacket.SaveMesh(mesh, MeshPath(who, pose));
                return AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(who, pose));
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
                    Sit(an);
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

        /// <summary>腰掛けに座る。腰は天面の 7.5 cm 上、足首は床に、両手は腿の上（場面 1 の座り方の値を腰掛けの高さへ移した物）</summary>
        static void Sit(Animator an)
        {
            System.Func<HumanBodyBones, Vector3> P = b => an.GetBoneTransform(b).position;
            var ankle = P(HumanBodyBones.LeftFoot).y;
            var hipsY = StoolTop + 0.075f;
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
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(BuildAlley.Generated.TrimEnd('/'), "MobTrial");
            // 前の試しの残り（暗くした色と今の群衆の色の組、平均で縮めた自己発光の PNG）
            foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.Contains("_Dim") || name.Contains("_Grey") || name.Contains("_Original") || path.EndsWith("_Glow.png")) AssetDatabase.DeleteAsset(path);
            }
        }

        static string MatPath(RocketboxMob who, string part) { return Folder + who.Name + "_" + part + ".mat"; }

        /// <summary>面の組の順（体・頭・髪の房）に並べたマテリアル</summary>
        static Material[] LoadMaterials(RocketboxMob who)
        {
            return who.HasOpacity
                ? new[] { Load(MatPath(who, "Body")), Load(MatPath(who, "Head")), Load(MatPath(who, "Hair")) }
                : new[] { Load(MatPath(who, "Body")), Load(MatPath(who, "Head")) };
        }

        static Material Load(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) throw new System.InvalidOperationException("マテリアルが無い: " + path);
            return m;
        }

        /// <summary>
        /// 光の強さ（自己発光のテクスチャに掛ける）。最初の試しの 1.2（うっすら）では、ゲームの見え方の 2〜3 m で 1〜2 画素にしかならなかった。
        /// ブルーム（しきい 0.85）に掛かって光の点や線として拾える強さ
        /// </summary>
        public const float GlowStrength = 3.0f;

        /// <summary>
        /// 一人のテクスチャとマテリアルを作る（服の色は元のまま、肌にインプラントを描く）。
        /// 面の組の順は FBX のマテリアルの順（体・頭・髪の房）
        /// </summary>
        static Material[] Materials(RocketboxMob who, StringBuilder sb)
        {
            const int n = RocketboxMob.TextureSize;
            int w, h;
            var head0 = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out w, out h));
            var body0 = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.BodySrc, out w, out h));
            RocketboxPaint.Surface headS, bodyS;
            RocketboxMobPaint.Frame frame;
            Surfaces(who, n, out headS, out bodyS, out frame);
            var skin = RocketboxMobPaint.SkinOf(headS, head0, frame);
            var strokes = new List<RocketboxMobPaint.Stroke>();
            foreach (var im in who.Implants) strokes.AddRange(RocketboxMobPaint.Strokes(im, frame));
            var skinCloud = new RocketboxMobPaint.Skin();
            skinCloud.Add(headS);
            skinCloud.Add(bodyS);
            RocketboxMobPaint.Prepare(strokes, skinCloud);
            var head = (Color[])head0.Clone();
            var body = (Color[])body0.Clone();
            int ph, pb;
            var headGlow = RocketboxMobPaint.Paint(strokes, headS, head, head0, skin, out ph);
            var bodyGlow = RocketboxMobPaint.Paint(strokes, bodyS, body, body0, skin, out pb);
            var headTex = Write(head, n, n, Folder + who.Name + "_Head.png", false);
            var bodyTex = Write(body, n, n, Folder + who.Name + "_Body.png", false);
            var headGlowTex = ph > 0 ? WriteGlow(headGlow, n, Folder + who.Name + "_Head_Glow.asset") : null;
            var bodyGlowTex = pb > 0 ? WriteGlow(bodyGlow, n, Folder + who.Name + "_Body_Glow.asset") : null;
            var list = new List<Material>
            {
                Save(Mat(who.Name + "_Body", bodyTex, bodyGlowTex, 0.12f, false), MatPath(who, "Body")),
                Save(Mat(who.Name + "_Head", headTex, headGlowTex, 0.22f, false), MatPath(who, "Head")),
            };
            if (who.HasOpacity)
                list.Add(Save(Mat(who.Name + "_Hair", AssetDatabase.LoadAssetAtPath<Texture2D>(who.HairSrc), null, 0.34f, true), MatPath(who, "Hair")));
            var names = new List<string>();
            foreach (var im in who.Implants) names.Add(im.ToString());
            sb.AppendFormat("{0}: 肌の見本 {1}、インプラント {2}（頭のテクスチャ {3} 画素、体のテクスチャ {4} 画素）", who.Label, ColorUtility.ToHtmlStringRGB(skin),
                names.Count > 0 ? string.Join("・", names.ToArray()) : "無し", ph, pb).AppendLine();
            return list.ToArray();
        }

        /// <summary>模型の束ねた姿勢の、頭と体の面の位置の地図と、インプラントを決める骨の位置</summary>
        static void Surfaces(RocketboxMob who, int n, out RocketboxPaint.Surface head, out RocketboxPaint.Surface body, out RocketboxMobPaint.Frame frame)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
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
                int hi = -1, bi = -1;
                var slots = smr.sharedMaterials;
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].name == who.HeadSlot) hi = i;
                    if (slots[i] != null && slots[i].name == who.BodySlot) bi = i;
                }
                if (hi < 0 || bi < 0) throw new System.InvalidOperationException("頭か体の面の組が無い: " + who.Name);
                head = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(hi), n);
                body = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(bi), n);
                frame = RocketboxMobPaint.Frame.Of(go.GetComponent<Animator>());
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 自己発光のテクスチャを書く。ミップマップは平均ではなく、2×2 のいちばん明るい画素を残して縮める。
        /// 平均で縮めると、細い光る線は遠くで地の黒に薄まって消える（ゲームの見え方の 2.5 m で、頭のテクスチャは 1/16 まで縮んで使われる）。
        /// 明るい画素を残すと、遠くでも線が画面の一画素の光として残り、ブルームでにじむ
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
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(t, old);
                Object.DestroyImmediate(t);
                EditorUtility.SetDirty(old);
                return old;
            }
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        static Texture2D Write(Color[] px, int w, int h, string path, bool alpha)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                c32[i] = px[i];
                if (!alpha) c32[i].a = 255;
            }
            RocketboxTextures.WritePng(c32, w, h, path, alpha);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = alpha;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 512;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, Texture2D tex, Texture2D glow, float smooth, bool clip)
        {
            var m = BuildRocketboxProtagonist.Lit(name, tex, smooth, clip);
            if (glow != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", glow);
                m.SetColor("_EmissionColor", Color.white * GlowStrength);
                // None にすると、URP のマテリアルの見直し（取り込みのたびに走る）が _EMISSION を落とし、光らなくなった
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
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
    }
}
