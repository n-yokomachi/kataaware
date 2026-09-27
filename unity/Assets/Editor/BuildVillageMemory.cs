using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 6（庭の記憶）の物を村に置く（シナリオ設計書 10 節）。村の組み立て（<c>HalfAware/Build the village</c>）の終わりに呼ぶ。
    ///
    /// - <c>Village/GardenMemory</c>（伏せて置く）: 主（片割れ）の座った体、水を撒く女性（過去の主人公）、ホースと筒と水の弧
    /// - <c>Village/GardenMemoryDirector</c>: 場面 6 の進行（<see cref="GardenMemoryDirector"/>）。場面 9 では何もしない
    /// - <c>Village/HoloPanel</c>: 人の脇の板（場面 4 と同じ物、<see cref="BuildDive.Panel"/>）
    /// - Hud に、角の白い膜と右上の見出し（場面 4 と同じ物）
    ///
    /// 並び（世界の値。+x が東、+z が北）:
    /// - 主はテラスの白いパラソルの卓の、北西寄りの椅子（卓から 320 度、<see cref="HostChair"/>）に、芝の方へ少し体を開いて座る（<see cref="HostYaw"/>）
    /// - 女性は芝の西の縁、小路のトンネルの南の口の脇（<see cref="WomanAt"/>）で、西の花の縁に水を撒いている。主から北へ 5 m ほど。
    ///   夕日（方位 290 度）は女性の背の左から当たり、主の目から見た顔の側は影になる
    /// - 歩く道: 芝を南東へ横切ってテラスの段の北（<see cref="WalkBend"/>）へ、段を上がって卓の方（<see cref="WalkEnd"/>）へ。
    ///   テラスと芝の境はラベンダーの縁で塞がっていて、抜けられるのは東の段だけ
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 並び -----------------------------------------------------------------------

        /// <summary>主の座る椅子の、卓から見た向き（度。卓の組み立て <c>Parasol</c> の椅子の並び 200・320・80 のうちの一つ）</summary>
        const float HostChairDeg = 320f;

        /// <summary>主の体の向き。度（+z が 0 で東回り）。椅子は卓（310 度）を向くが、芝の方へ少し体を開いて座る</summary>
        public const float HostYaw = 345f;

        /// <summary>女性が水を撒く立ち位置。芝の西の縁（x -2.0）の少し内、トンネルの南の口の脇</summary>
        public static readonly Vector3 WomanAt = new Vector3(-1.6f, 0f, 21.6f);

        /// <summary>水を撒くときに向く向き。西の花の縁</summary>
        public const float FlowerYaw = 268f;

        /// <summary>歩く道の曲がり角。テラスの段（x 0.3〜1.7）の北の芝</summary>
        public static readonly Vector3 WalkBend = new Vector3(0.95f, 0f, 19.95f);

        /// <summary>歩く道の終わり。段を上がったテラスの上、卓の方。ここへ着く前に途切れる</summary>
        public static readonly Vector3 WalkEnd = new Vector3(0.55f, TerraceTopY, 17.75f);

        /// <summary>テラスの上面の高さ（<c>BuildVillageGarden.TerraceTop</c> と同じ）</summary>
        const float TerraceTopY = 0.1f;

        /// <summary>歩く速さ。m/s。ゆっくり歩いてくる</summary>
        public const float WalkSpeed = 0.5f;

        /// <summary>歩き出す、送ってからの秒（庭の時計）。ホースを止めて、こちらを向いてから</summary>
        public const float WalkAt = 33f;

        /// <summary>ホースの出どころ。西の花の縁の奥の地面（花に隠れる）</summary>
        static readonly Vector3 HoseSource = new Vector3(-2.65f, 0.02f, 20.35f);
        /// <summary>ホースが芝を這う途中の点</summary>
        static readonly Vector3[] HoseRun =
        {
            new Vector3(-2.02f, 0.02f, 20.55f),
            new Vector3(-1.72f, 0.02f, 20.95f),
        };

        /// <summary>主の体の置き場の名</summary>
        public const string MemoryName = "GardenMemory";
        public const string DirectorName = "GardenMemoryDirector";

        const string HoseClipPath = "Assets/Audio/GardenHose.wav";
        const string HoseStopClipPath = "Assets/Audio/GardenHoseStop.wav";
        const string GlitchClipPath = "Assets/Audio/SignalGlitch.wav";
        const string CutClipPath = "Assets/Audio/SignalCut.wav";
        const string SprayMatPath = "Assets/Materials/Village/GardenSpray.mat";
        const string PuffPath = "Assets/Textures/SmokePuff.png";

        /// <summary>主の椅子の足元（卓の組み立てと同じ式）</summary>
        public static Vector3 HostChair
        {
            get
            {
                var a = HostChairDeg * Mathf.Deg2Rad;
                return TableAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.82f;
            }
        }


        // ---- 組み立て -------------------------------------------------------------------

        /// <summary>場面 6 の物を置いて繋ぐ。村の根 root の下に置き、入れ物は伏せる</summary>
        static void Memory(Transform root, Transform hours, StringBuilder note)
        {
            var memory = Child(root, MemoryName);
            memory.gameObject.SetActive(true);
            float eyeHeight;
            var seat = Host(memory, out eyeHeight, note);
            Transform hand, jaw;
            var woman = Woman(memory, out hand, out jaw, note);
            AudioSource water, stop;
            ParticleSystem spray;
            var hose = Hose(memory, hand, woman, out spray, out water, out stop);
            var caption = Screen6();
            BuildDive.Panel(root);
            var panel = root.Find("HoloPanel");

            var go = new GameObject(DirectorName);
            go.transform.SetParent(root, false);
            var noise = go.AddComponent<AudioSource>();
            noise.playOnAwake = false;
            noise.loop = false;
            noise.spatialBlend = 0f;
            noise.volume = 0.8f;
            var director = go.AddComponent<GardenMemoryDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            so.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            so.FindProperty("caption").objectReferenceValue = caption;
            so.FindProperty("panel").objectReferenceValue = panel != null ? panel.GetComponent<HoloPanel>() : null;
            so.FindProperty("hour").objectReferenceValue = hours.GetComponent<VillageHour>();
            so.FindProperty("memory").objectReferenceValue = memory.gameObject;
            var gates = Object.FindObjectsByType<SwingGate>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            var off = so.FindProperty("morningOnly");
            off.arraySize = gates.Length;
            for (var i = 0; i < gates.Length; i++) off.GetArrayElementAtIndex(i).objectReferenceValue = gates[i];
            so.FindProperty("seat").objectReferenceValue = seat;
            so.FindProperty("seatEyeHeight").floatValue = eyeHeight;
            so.FindProperty("woman").objectReferenceValue = woman.GetComponent<Mover>();
            so.FindProperty("womanMotion").objectReferenceValue = woman.GetComponent<PersonMotion>();
            so.FindProperty("jaw").objectReferenceValue = jaw;
            so.FindProperty("flowerYaw").floatValue = FlowerYaw;
            so.FindProperty("hose").objectReferenceValue = hose;
            so.FindProperty("spray").objectReferenceValue = spray;
            so.FindProperty("water").objectReferenceValue = water;
            so.FindProperty("stop").objectReferenceValue = stop;
            so.FindProperty("noise").objectReferenceValue = noise;
            so.FindProperty("glitch").objectReferenceValue = Clip(GlitchClipPath, note);
            so.FindProperty("cut").objectReferenceValue = Clip(CutClipPath, note);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 場面 9 では伏せたまま。起こすのは場面 6 の演出
            memory.gameObject.SetActive(false);
        }

        static AudioClip Clip(string path, StringBuilder note)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) note.AppendLine("音の素材がまだ無い（仮に鳴らさない）: " + path);
            return clip;
        }

        // ---- 主の体 ---------------------------------------------------------------------

        /// <summary>
        /// 主（片割れ）の座った体。片割れの模型（白いワンピースとサンダル、<see cref="BuildRocketboxProtagonist.Build(Transform, bool)"/> の twin）を
        /// 椅子に座らせて据える。頭は一人称のカメラに映さない（影だけ落とす、<see cref="PlaceProtagonist.SplitHead"/>）。
        ///
        /// **主は歩かないので、体は Player の子にしない。** 椅子の上に据え、動き（Animator）を止めて座った形のまま置く。
        /// 首を振ると目（カメラ）だけが回る。
        ///
        /// **目の置き場は体から決める。** 座らせてから両目の真ん中を測り、Player の足元（<paramref name="eyeHeight"/> と eyeLead で目がそこへ来る所）を返す。
        /// 目の置き場へ体を合わせると、腰が座面から浮くか沈む
        /// </summary>
        static Transform Host(Transform memory, out float eyeHeight, StringBuilder note)
        {
            var chair = new GameObject("HostChair").transform;
            chair.SetParent(memory, false);
            chair.position = HostChair;
            chair.rotation = Quaternion.Euler(0f, HostYaw, 0f);

            var her = BuildRocketboxProtagonist.Build(memory, true);
            her.name = "Host";
            PlaceProtagonist.SplitHead(her);
            var an = her.GetComponent<Animator>();
            her.transform.position = chair.position;
            her.transform.rotation = chair.rotation;
            BodyPoser.Stand(an);
            BodyPoser.Pose(an, HostSit(chair, BuildRocketboxProtagonist.Twin == BuildRocketboxProtagonist.TwinMode.MirrorWhole));
            // **動きを止めると骨が素の形へ戻る**（controller を外す・Animator を切ると、Unity が骨を既定の値へ書き戻す）。
            // 座った形の骨の値を持っておき、止めてから書き戻す
            var bones = her.GetComponentsInChildren<Transform>(true);
            var at = new Vector3[bones.Length];
            var turn = new Quaternion[bones.Length];
            for (var i = 0; i < bones.Length; i++) { at[i] = bones[i].localPosition; turn[i] = bones[i].localRotation; }
            an.runtimeAnimatorController = null;
            an.enabled = false;
            for (var i = 0; i < bones.Length; i++) { bones[i].localPosition = at[i]; bones[i].localRotation = turn[i]; }
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

            var eyes = BodyPoser.Eyes(an);
            var ahead = chair.forward;
            var foot = eyes - ahead * EyeLead;
            foot.y = HostChair.y;
            eyeHeight = eyes.y - foot.y;
            chair.name = "Seat";
            chair.position = foot;
            note.AppendFormat("主の目: {0}（足元から {1:0.000} m）。腰 {2}", eyes.ToString("F3"), eyeHeight,
                an.GetBoneTransform(HumanBodyBones.Hips).position.ToString("F3")).AppendLine();
            return chair;
        }

        /// <summary>
        /// 白い鉄のビストロの椅子（座面 0.45 m、丸く肘掛けは無い）に座った形。値は椅子の足元から見た位置で、+z が体の前。
        /// 腿は座面に乗せ、脚は前へ下ろしてサンダルをテラスに置く。手は膝の上。下を向くと、膝の上の白いワンピースと両手が見える。
        /// サンダルは、くるぶしまでの長い裾の輪（脚の骨に付いて脛のまわりに立つ）に上から隠れる（脚を前へ投げ出しても、裾の輪が足を囲んで隠れた）
        /// mirror なら左右を入れ替える（片割れの模型は根の x を裏返してあり、左の骨が世界の右に来る）
        /// </summary>
        static BodyPoser.Sit HostSit(Transform chair, bool mirror)
        {
            var sx = mirror ? -1f : 1f;
            // 裏返した模型は手の左右の巻きも逆になるので、手のひらの向き（BodyPoser が指の並びから出す）も上下が入れ替わる
            var palmY = mirror ? 1f : -1f;
            System.Func<float, float, float, Vector3> P = (x, y, z) => chair.TransformPoint(new Vector3(x * sx, y, z));
            System.Func<float, float, float, Vector3> D = (x, y, z) => chair.TransformDirection(new Vector3(x * sx, y, z));
            return new BodyPoser.Sit
            {
                hips = P(0f, 0.53f, -0.03f),
                pelvis = 0f,
                lean = 6f,
                headKeep = 1f,
                ankleL = P(-0.12f, 0.085f, 0.55f),
                ankleR = P(0.08f, 0.085f, 0.62f),
                kneePoleL = P(-0.12f, 0.9f, 1.2f),
                kneePoleR = P(0.12f, 0.9f, 1.2f),
                footPoint = 18f,
                wristL = P(-0.13f, 0.63f, 0.25f),
                wristR = P(0.13f, 0.63f, 0.25f),
                elbowPoleL = P(-0.45f, 0.62f, -0.2f),
                elbowPoleR = P(0.45f, 0.62f, -0.2f),
                fingersL = D(0.1f, -0.35f, 1f),
                palmL = D(0f, palmY, 0f),
                fingersR = D(-0.1f, -0.35f, 1f),
                palmR = D(0f, palmY, 0f),
            };
        }

        // ---- 女性 -----------------------------------------------------------------------

        /// <summary>筒を持つ左腕と指。親から順。主の目から見える側（女性は西を向き、主は南東にいる）の手に持たせ、水の弧が体の陰に隠れないようにする</summary>
        static readonly HumanBodyBones[] GripBones =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        /// <summary>水を撒く女性（過去の主人公）の人。黒い髪、黒子は口元の左、オリーブのシャツと生成りの長いスカート（<see cref="RocketboxGardenWear"/>）</summary>
        public static readonly RocketboxPerson WomanPerson = RocketboxPerson.Face14Hair14GardenWear;

        /// <summary>
        /// 女性の模型。服のメッシュがまだ無ければ先に組む（服の絵はメッシュの UV から描くので、描く前に要る）。
        /// 上着は着ない（シナリオ設計書 1 節「片割れの記憶の中の主人公（過去）は、上着を着る人ではない」）
        /// </summary>
        static GameObject WomanModel(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(WomanPerson.CompositeMesh) == null) Debug.Log(RocketboxCompose.BuildMesh(WomanPerson));
            var her = BuildRocketboxProtagonist.Build(parent, false, WomanPerson);
            var jacket = her.transform.Find("Jacket");
            if (jacket != null) Object.DestroyImmediate(jacket.gameObject);
            return her;
        }

        /// <summary>
        /// 水を撒く女性（過去の主人公）。場面 4 の記憶の人と同じ作り（<see cref="PersonMotion"/> が骨で動かし、<see cref="Mover"/> が線を運ぶ）。
        /// 左手に筒を持った形（<see cref="GripBones"/>）を据え、歩き出したら解く（letsGo）。主を見るときは首と頭を主の目へ向ける
        /// </summary>
        static Transform Woman(Transform memory, out Transform hand, out Transform jaw, StringBuilder note)
        {
            var root = new GameObject("Woman").transform;
            root.SetParent(memory, false);
            root.position = WomanAt;
            root.rotation = Quaternion.Euler(0f, FlowerYaw, 0f);
            var her = WomanModel(root);
            her.name = "Figure";
            her.transform.localPosition = Vector3.zero;
            her.transform.localRotation = Quaternion.identity;
            var an = her.GetComponent<Animator>();
            an.runtimeAnimatorController = null;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

            var idle = BuildDiveCast.Clip(true, "Idle");
            var walk = BuildDiveCast.Clip(true, "Walk");
            var run = BuildDiveCast.Clip(true, "Run");
            var neck = an.GetBoneTransform(HumanBodyBones.Neck);
            var headBone = an.GetBoneTransform(HumanBodyBones.Head);
            var headAim = headBone != null ? Quaternion.Inverse(headBone.rotation) * her.transform.forward : Vector3.forward;
            var motion = root.gameObject.AddComponent<PersonMotion>();

            // 歩幅を測る（筒を持つ手の形は入れずに）
            RigWoman(motion, an, her.transform, idle, walk, run, Vector3.zero, null, new Transform[0], new Quaternion[0]);
            var feet = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
            var strides = new float[PersonMotion.Reaches.Length];
            for (var i = 0; i < strides.Length; i++)
                strides[i] = BuildDive.FigureStride(root, her, motion, idle, walk, PersonMotion.Reaches[i], feet);

            // 床に下ろす。立ちの動きの頭の一こまで、皮のいちばん低い所が根の高さに来るように
            idle.SampleAnimation(her, 0f);
            var drop = -Lowest(root, her);
            var held = new List<Transform>();
            foreach (var b in GripBones) held.Add(an.GetBoneTransform(b));
            var holds = GripHolds(idle);
            RigWoman(motion, an, her.transform, idle, walk, run, new Vector3(0f, drop, 0f), strides, held.ToArray(), holds);
            var mso = new SerializedObject(motion);
            mso.FindProperty("letsGo").boolValue = true;
            mso.FindProperty("attends").boolValue = true;
            mso.FindProperty("neck").objectReferenceValue = neck;
            mso.FindProperty("head").objectReferenceValue = headBone;
            mso.FindProperty("headAim").vector3Value = headAim.normalized;
            mso.ApplyModifiedPropertiesWithoutUndo();
            her.transform.localPosition = new Vector3(0f, drop, 0f);
            motion.Still();

            // 歩く線。水を撒く所から芝を横切ってテラスの段の北へ、段を上がって卓の方へ。向きは人の動きが歩く向きへ回す
            var mover = root.gameObject.AddComponent<Mover>();
            var first = Vector3.Distance(Flat(WomanAt), Flat(WalkBend)) / WalkSpeed;
            var second = Vector3.Distance(Flat(WalkBend), Flat(WalkEnd)) / WalkSpeed;
            var vso = new SerializedObject(mover);
            vso.FindProperty("from").vector3Value = WomanAt;
            vso.FindProperty("to").vector3Value = WalkBend;
            vso.FindProperty("at").floatValue = WalkAt;
            vso.FindProperty("span").floatValue = first;
            vso.FindProperty("ease").boolValue = true;
            vso.FindProperty("ground").boolValue = true;
            vso.FindProperty("cue").intValue = -1;
            vso.FindProperty("next").vector3Value = WalkEnd;
            vso.FindProperty("nextAt").floatValue = WalkAt + first;
            vso.FindProperty("nextSpan").floatValue = second;
            vso.FindProperty("nextCue").intValue = -1;
            vso.FindProperty("turns").boolValue = false;
            vso.ApplyModifiedPropertiesWithoutUndo();

            hand = an.GetBoneTransform(HumanBodyBones.LeftHand);
            jaw = FindDeep(her.transform, "Bip01 MJaw");
            note.AppendFormat("女性: 歩幅 {0}、床へ {1:0.000} m、歩く {2:0.0}+{3:0.0} 秒、口の骨 {4}", string.Join("/", System.Array.ConvertAll(strides, s => s.ToString("0.00"))),
                drop, first, second, jaw != null ? jaw.name : "無し").AppendLine();
            return root;
        }

        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>模型の皮のいちばん低い所の、根から見た高さ</summary>
        static float Lowest(Transform root, GameObject model)
        {
            var low = float.MaxValue;
            var mesh = new Mesh();
            try
            {
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.BakeMesh(mesh, true);
                    var m = smr.transform.localToWorldMatrix;
                    foreach (var v in mesh.vertices) low = Mathf.Min(low, root.InverseTransformPoint(m.MultiplyPoint3x4(v)).y);
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
            return low == float.MaxValue ? 0f : low;
        }

        /// <summary>PersonMotion の中身を書く（<c>BuildDive.RigFigure</c> と同じ並び）</summary>
        static void RigWoman(PersonMotion motion, Animator an, Transform body, AnimationClip idle, AnimationClip walk, AnimationClip run,
            Vector3 seat, float[] strides, Transform[] held, Quaternion[] holds)
        {
            var so = new SerializedObject(motion);
            so.FindProperty("animator").objectReferenceValue = an;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("idle").objectReferenceValue = idle;
            so.FindProperty("walk").objectReferenceValue = walk;
            so.FindProperty("run").objectReferenceValue = run;
            so.FindProperty("seated").boolValue = false;
            so.FindProperty("seat").vector3Value = seat;
            var st = so.FindProperty("strides");
            var all = strides ?? new float[0];
            st.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) st.GetArrayElementAtIndex(i).floatValue = all[i];
            so.FindProperty("runStride").floatValue = 2.4f;
            so.FindProperty("lag").floatValue = 0.35f;
            so.FindProperty("sized").arraySize = 0;
            so.FindProperty("sizes").arraySize = 0;
            so.FindProperty("bent").arraySize = 0;
            so.FindProperty("bends").arraySize = 0;
            var h = so.FindProperty("held");
            h.arraySize = held.Length;
            for (var i = 0; i < held.Length; i++) h.GetArrayElementAtIndex(i).objectReferenceValue = held[i];
            var q = so.FindProperty("holds");
            q.arraySize = holds.Length;
            for (var i = 0; i < holds.Length; i++) q.GetArrayElementAtIndex(i).quaternionValue = holds[i];
            so.FindProperty("feet").arraySize = 0;
            so.FindProperty("ankles").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 筒を持つ左腕の形。模型の写しを原点に +z 向きで立たせ、立ちの動きの頭の一こまから、左の手首を腰の前へ出して
        /// 指を前の少し下へ、手のひらを内へ向け、指を握る。骨ごとに、模型の根から見た向きとして取る
        /// </summary>
        static Quaternion[] GripHolds(AnimationClip idle)
        {
            var scratch = WomanModel(null);
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                scratch.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var an = scratch.GetComponent<Animator>();
                an.runtimeAnimatorController = null;
                BodyPoser.Stand(an, idle);
                BodyPoser.Arm(an, true, new Vector3(-0.15f, 1.07f, 0.34f), new Vector3(-0.45f, 1.05f, -0.25f),
                    new Vector3(-0.05f, -0.35f, 1f), new Vector3(1f, 0.1f, 0f));
                BodyPoser.Grip(an, true, 0.85f);
                var inverse = Quaternion.Inverse(scratch.transform.rotation);
                var all = new Quaternion[GripBones.Length];
                for (var i = 0; i < GripBones.Length; i++)
                {
                    var b = an.GetBoneTransform(GripBones[i]);
                    all[i] = b != null ? inverse * b.rotation : Quaternion.identity;
                }
                return all;
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        // ---- ホース ---------------------------------------------------------------------

        /// <summary>
        /// ホースと筒と水の弧。筒は左手の骨の所へ据え（<see cref="GardenHose"/>）、水の弧の粒と水の音と止める音を筒の子に置く。
        /// 線は西の花の縁の奥から芝を這って足元へ来て、手元へ上がる
        /// </summary>
        static GardenHose Hose(Transform memory, Transform hand, Transform woman, out ParticleSystem spray, out AudioSource water, out AudioSource stop)
        {
            var go = new GameObject("Hose");
            go.transform.SetParent(memory, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.034f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.generateLightingData = true;
            line.shadowCastingMode = ShadowCastingMode.On;
            line.sharedMaterial = Paint("GardenHose", new Color(0.12f, 0.27f, 0.13f), 0.45f);

            // 筒（引き金のある散水ノズル）。+z が水の出る向き
            var nozzle = new GameObject("Nozzle").transform;
            nozzle.SetParent(memory, false);
            var body = Paint("GardenNozzle", new Color(0.20f, 0.46f, 0.22f), 0.35f);
            var metal = Paint("GardenNozzleMetal", new Color(0.62f, 0.60f, 0.55f), 0.55f);
            Part(nozzle, "Barrel", new Vector3(0f, 0f, 0.035f), new Vector3(0.030f, 0.034f, 0.13f), Quaternion.identity, body);
            Part(nozzle, "Head", new Vector3(0f, 0f, 0.108f), new Vector3(0.038f, 0.038f, 0.024f), Quaternion.identity, metal);
            Part(nozzle, "Grip", new Vector3(0f, -0.055f, -0.005f), new Vector3(0.026f, 0.085f, 0.03f), Quaternion.Euler(-14f, 0f, 0f), body);

            // 手の中に置く。持った形（人の動きの据えた形）の手の、指の側の少し先
            var motion = woman.GetComponent<PersonMotion>();
            if (motion != null) motion.Still();
            var an = woman.GetComponentInChildren<Animator>();
            var fingers = an != null ? BodyPoser.FingerDir(an, true) : woman.forward;
            var palm = an != null ? BodyPoser.PalmDir(an, true) : woman.right;
            var holdAt = hand.position + fingers * 0.055f + palm * 0.025f;
            var aim = woman.forward;
            aim.y = -0.25f;
            nozzle.SetPositionAndRotation(holdAt, Quaternion.LookRotation(aim.normalized, Vector3.up));

            var hose = go.AddComponent<GardenHose>();
            var so = new SerializedObject(hose);
            so.FindProperty("line").objectReferenceValue = line;
            so.FindProperty("nozzle").objectReferenceValue = nozzle;
            so.FindProperty("hand").objectReferenceValue = hand;
            so.FindProperty("grip").vector3Value = hand.InverseTransformPoint(nozzle.position);
            so.FindProperty("gripTurn").quaternionValue = Quaternion.Inverse(hand.rotation) * nozzle.rotation;
            so.FindProperty("source").vector3Value = HoseSource;
            var run = so.FindProperty("run");
            run.arraySize = HoseRun.Length;
            for (var i = 0; i < HoseRun.Length; i++) run.GetArrayElementAtIndex(i).vector3Value = HoseRun[i];
            // 手を離したら、足元の南東の芝に横倒しで置く
            so.FindProperty("dropAt").vector3Value = WomanAt + new Vector3(0.28f, 0.03f, -0.30f);
            so.FindProperty("dropTurn").quaternionValue = Quaternion.Euler(0f, 205f, 84f);
            so.ApplyModifiedPropertiesWithoutUndo();
            hose.Refresh();

            spray = Spray(nozzle);
            water = nozzle.gameObject.AddComponent<AudioSource>();
            water.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HoseClipPath);
            water.playOnAwake = false;
            water.loop = true;
            water.volume = 0f;
            water.spatialBlend = 1f;
            water.rolloffMode = AudioRolloffMode.Logarithmic;
            water.minDistance = 1.5f;
            water.maxDistance = 25f;
            water.dopplerLevel = 0f;
            var stopGo = new GameObject("Stop");
            stopGo.transform.SetParent(nozzle, false);
            stop = stopGo.AddComponent<AudioSource>();
            stop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HoseStopClipPath);
            stop.playOnAwake = false;
            stop.loop = false;
            stop.volume = 0.8f;
            stop.spatialBlend = 1f;
            stop.rolloffMode = AudioRolloffMode.Logarithmic;
            stop.minDistance = 1.5f;
            stop.maxDistance = 25f;
            stop.dopplerLevel = 0f;
            return hose;
        }

        static void Part(Transform parent, string name, Vector3 at, Vector3 size, Quaternion turn, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = turn;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// 水の弧。筒の先から少し上向きに出た粒が落ちて弧を描く。夕日に光る程度の、暖かい白の足し算の粒。
        /// 粗い画面（中 320×180）で消えないよう、粒は細かくしすぎない
        /// </summary>
        static ParticleSystem Spray(Transform nozzle)
        {
            var go = new GameObject("Spray");
            go.transform.SetParent(nozzle, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            go.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 2.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.90f, 0.72f, 0.85f), new Color(0.95f, 0.97f, 1f, 0.55f));
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 260f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.006f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SprayMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Material SprayMat()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(SprayMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.name = "GardenSpray";
                AssetDatabase.CreateAsset(m, SprayMatPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PuffPath);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.2f, 1.1f, 0.95f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 画面 -----------------------------------------------------------------------

        /// <summary>
        /// Hud に、記憶の中の見え方を足す。角の白い膜（場面 4 と同じ形と濃さ）と、右上の見出し（端末と同じ緑）。
        /// 膜はいちばん先に並べて字の下に敷く。場面 9 では膜は伏せ、見出しは空
        /// </summary>
        static TMP_Text Screen6()
        {
            var hud = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) { Debug.LogWarning("Hud が無い。見出しと膜を足せない"); return null; }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BuildDive.FontPath);

            var haze = new GameObject("Haze", typeof(RectTransform), typeof(CanvasRenderer), typeof(ScreenHaze));
            haze.transform.SetParent(hud.transform, false);
            haze.transform.SetSiblingIndex(0);
            var rect = haze.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var view = haze.GetComponent<ScreenHaze>();
            var hso = new SerializedObject(view);
            hso.FindProperty("from").floatValue = BuildDive.HazeFrom;
            hso.FindProperty("upto").floatValue = BuildDive.HazeUpto;
            hso.FindProperty("corner").floatValue = BuildDive.HazeDepth;
            hso.FindProperty("amount").floatValue = 0f;
            hso.ApplyModifiedPropertiesWithoutUndo();
            haze.SetActive(false);

            var go = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(hud.transform, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 22f;
            text.color = BuildDive.Terminal;
            text.alignment = TextAlignmentOptions.TopRight;
            text.text = "";
            var cr = text.rectTransform;
            cr.anchorMin = Vector2.one;
            cr.anchorMax = Vector2.one;
            cr.pivot = Vector2.one;
            cr.anchoredPosition = new Vector2(-24f, -24f);
            cr.sizeDelta = new Vector2(640f, 40f);

            var so = new SerializedObject(hud);
            so.FindProperty("hazeLayer").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }
    }
}
