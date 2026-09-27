using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 9 の終わりと場面 10（対面）の物を村に置く（シナリオ設計書 12 節）。村の組み立て（<c>HalfAware/Build the village</c>）の終わりに呼ぶ。
    ///
    /// - <c>Village/Reunion</c>（伏せて置く）: 片割れ（<c>Twin</c>）、裏口の戸の音（<c>DoorSound</c>）、片割れの顔を起こす灯り（<c>FaceLight</c>、片割れの子）
    /// - <c>Village/ReunionDirector</c>: 進行（<see cref="ReunionDirector"/>）。場面 6 では何もしない。暗転の後はエンディング（<c>Ending</c>、別の場面）を読む
    ///
    /// 並び（世界の値。+x が東、+z が北）:
    /// - 卓の前（場面 10 の頭）は、テラスの西の縁、煉瓦の小路から上がった所（<see cref="ReunionHead"/>）。卓を正面に見て、その奥の右に裏口
    /// - 片割れは裏口の戸口の奥の暗がり（<see cref="TwinInside"/>、戸の板の回る所の外）で待ち、戸が開くと戸口の外（<see cref="TwinOut"/>）へ出てくる
    /// </summary>
    public static partial class BuildVillage
    {
        /// <summary>場面 10 の物の入れ物と、進行の名</summary>
        public const string ReunionName = "Reunion";
        public const string ReunionDirectorName = "ReunionDirector";

        /// <summary>
        /// 卓の前（場面 10 の頭）の足元。卓の東、テラスの上（卓の芯から 2.0 m）。区画に入るとここへ歩かせる。
        /// ここから卓を向くと、パラソルの卓と椅子の奥に西の花の縁。裏口は左の後ろで、戸が開くと目がそちらへ向き、裏口は画面の中ほどに斜めに見える（パラソルの傘は後ろ）。
        /// 戸口を出た所まで 4 m ほどで、片割れは戸口を出てすぐ立ち止まる。テラスの西の縁（前の立ち位置）からは、裏口は真横の奥で、傘が画面の上半分を塞いだ
        /// </summary>
        public static readonly Vector3 ReunionHead = new Vector3(0.15f, TerraceTopY + StandLift, 17.6f);

        /// <summary>
        /// Player の足元を床から浮かせる高さ。組み立ての置き場（<c>Rig</c> の ArriveAt の 0.06 m 上）と同じ。
        /// 当たりの肌の厚みのぶん、立った Player の根は床より少し上に落ち着く。テラスの敷石には当たりがある（<c>Garden/Bounds/TerraceFloor</c>）
        /// </summary>
        const float StandLift = 0.06f;

        /// <summary>頬に触れる時の、片割れの足元（テラスの上面）から見たプレイヤーの目の高さ。二人ともテラスの敷石の上に立つ</summary>
        const float TouchEyeAbove = 1.6f + StandLift;

        /// <summary>そこでの体の向き。卓を向く</summary>
        public static float ReunionHeadYaw
        {
            get { var d = TableAt - ReunionHead; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        }

        /// <summary>卓の前へ歩かせる時に避ける、卓と椅子の広がり（卓の当たり 1.9 m 角に、Player の太さ 0.3 m と余り）。x の小・大、z の小・大</summary>
        static Vector4 TableBox
        {
            get { return new Vector4(TableAt.x - 1.4f, TableAt.x + 1.4f, TableAt.z - 1.4f, TableAt.z + 1.4f); }
        }

        /// <summary>片割れが戸の開く前に立つ所。戸口の奥の暗がりの西の奥（戸の板は東の丁番から内へ回るので、その外）</summary>
        public static readonly Vector3 TwinInside = new Vector3(BackDoorX - 0.30f, 0.01f, HouseRear - 0.95f);

        /// <summary>片割れが出てきて立ち止まる所。踏み石の先のテラスの上</summary>
        public static readonly Vector3 TwinOut = new Vector3(BackDoorX - 0.10f, TerraceTopY, HouseRear + 0.62f);

        /// <summary>卓のまわりの区画。卓の芯からこの内に入ると場面 9 が終わる</summary>
        public const float ReunionZone = 2.2f;

        /// <summary>頬に触れる時の、プレイヤーの目から片割れの足元まで（上から見て）。手の形はこの隔たりで作る</summary>
        public const float TouchApart = 0.42f;

        /// <summary>裏口の戸の音。自室の扉の音（開けると閉めるが一つに入っている）の頭の、開ける所だけを鳴らす（演出の doorSoundSeconds）</summary>
        const string DoorClipPath = "Assets/Audio/DoorShut.wav";
        /// <summary>モンタージュの前もって撮った絵（<see cref="ReunionShots"/>）</summary>
        public const string MirrorShotPath = "Assets/Textures/Reunion/montage_mirror.png";
        public const string GardenShotPath = "Assets/Textures/Reunion/montage_garden.png";

        /// <summary>片割れが歩けるテラスの上。x の小・大、z の小・大（戸口の前の段と、北の縁の手前）</summary>
        static Vector4 TerraceWalk
        {
            get { return new Vector4(SidePathX + PathWide * 0.5f + 0.35f, HouseEast - 0.35f, HouseRear + 0.35f, 18.6f - 0.3f); }
        }

        // ---- 組み立て -------------------------------------------------------------------

        /// <summary>
        /// 開いている村で、場面 10 の物（片割れ・戸の音・進行）だけを組み直す。村の全部を組み直すと 20 秒ほどかかるので、
        /// 片割れの形や値を詰める間はこれで組み、撮って確かめる。save なら保存する（物の一覧を組み直す前と後で比べ、場面 10 の物の外が変わっていれば保存しない）
        /// </summary>
        public static string RebuildReunion(bool save)
        {
            if (EditorApplication.isPlaying) return "再生中は組み直さない";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) return "村が開いていない: " + scene.path;
            if (save && scene.isDirty) return "村に未保存の変更がある。捨ててからもう一度";
            // 根の Village を探す。GameObject.Find は同じ名の子（Player の頭上の環境音の Village）を返すことがある
            GameObject root = null;
            foreach (var g in scene.GetRootGameObjects())
                if (g.name == "Village") root = g;
            if (root == null) return "Village が無い";
            var before = save ? ConsoleShot.Dump("reunion_before") : "";
            var old = root.transform.Find(ReunionName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = root.transform.Find(ReunionDirectorName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var note = new StringBuilder();
            Reunion(root.transform, note);
            if (!save) return "組み直した（保存しない）\n" + note;
            var after = ConsoleShot.Dump("reunion_after");
            var changed = OutsideReunion(System.IO.Path.Combine(ConsoleShot.Scratch, "reunion_before.txt"), System.IO.Path.Combine(ConsoleShot.Scratch, "reunion_after.txt"));
            if (changed.Length > 0) return "場面 10 の物の外が変わった。保存しない\n" + changed + note;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "組み直して保存した。前 " + before + " / 後 " + after + "\n" + note;
        }

        /// <summary>物の一覧の二つを比べ、場面 10 の物（Village/Reunion・Village/ReunionDirector）の外で違う行を返す</summary>
        public static string OutsideReunion(string beforePath, string afterPath)
        {
            System.Func<string, bool> ours = l => l.StartsWith("/Village/" + ReunionName + "/") || l.StartsWith("/Village/" + ReunionName + " ")
                || l.StartsWith("/Village/" + ReunionDirectorName);
            var a = new List<string>(System.IO.File.ReadAllLines(beforePath));
            var b = new List<string>(System.IO.File.ReadAllLines(afterPath));
            a.RemoveAll(l => ours(l));
            b.RemoveAll(l => ours(l));
            var sb = new StringBuilder();
            var sa = new HashSet<string>(a);
            var sbb = new HashSet<string>(b);
            foreach (var l in a) if (!sbb.Contains(l)) sb.AppendLine("消えた: " + l);
            foreach (var l in b) if (!sa.Contains(l)) sb.AppendLine("増えた: " + l);
            return sb.ToString();
        }

        /// <summary>場面 10 の物を置いて繋ぐ。村の根 root の下に置き、入れ物は伏せる</summary>
        static void Reunion(Transform root, StringBuilder note)
        {
            var reunion = Child(root, ReunionName);
            reunion.gameObject.SetActive(true);
            var twin = Twin(reunion, note);

            var door = root.Find("House/BackDoor");
            if (door == null) note.AppendLine("裏口の戸が無い: House/BackDoor");
            var doorSound = new GameObject("DoorSound").AddComponent<AudioSource>();
            doorSound.transform.SetParent(reunion, false);
            doorSound.transform.position = new Vector3(BackDoorX, 1.2f, HouseRear);
            doorSound.playOnAwake = false;
            doorSound.loop = false;
            doorSound.spatialBlend = 1f;
            doorSound.rolloffMode = AudioRolloffMode.Logarithmic;
            doorSound.minDistance = 2.5f;
            doorSound.maxDistance = 30f;
            doorSound.volume = 0.9f;
            doorSound.clip = Clip(DoorClipPath, note);

            var go = new GameObject(ReunionDirectorName);
            go.transform.SetParent(root, false);
            var director = go.AddComponent<ReunionDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            so.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            so.FindProperty("reunion").objectReferenceValue = reunion.gameObject;
            so.FindProperty("twin").objectReferenceValue = twin;
            so.FindProperty("door").objectReferenceValue = door;
            var gates = Object.FindObjectsByType<SwingGate>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            var g = so.FindProperty("gates");
            g.arraySize = gates.Length;
            for (var i = 0; i < gates.Length; i++) g.GetArrayElementAtIndex(i).objectReferenceValue = gates[i];
            so.FindProperty("ambience").objectReferenceValue = Object.FindFirstObjectByType<VillageAmbience>(FindObjectsInactive.Include);
            so.FindProperty("faceLight").objectReferenceValue = twin != null ? twin.GetComponentInChildren<Light>(true) : null;
            so.FindProperty("table").vector3Value = TableAt;
            so.FindProperty("zoneRadius").floatValue = ReunionZone;
            so.FindProperty("headSpot").vector3Value = ReunionHead;
            so.FindProperty("headYaw").floatValue = ReunionHeadYaw;
            so.FindProperty("tableBox").vector4Value = TableBox;
            so.FindProperty("doorSound").objectReferenceValue = doorSound;
            so.FindProperty("twinInside").vector3Value = TwinInside;
            so.FindProperty("twinOut").vector3Value = TwinOut;
            so.FindProperty("walkArea").vector4Value = TerraceWalk;
            so.FindProperty("walkY").floatValue = TerraceTopY;
            so.FindProperty("touchApart").floatValue = TouchApart;
            so.FindProperty("mirrorShot").objectReferenceValue = Picture(MirrorShotPath, note);
            so.FindProperty("gardenShot").objectReferenceValue = Picture(GardenShotPath, note);
            so.FindProperty("nextScene").stringValue = ReunionDirector.EndingScene;
            if (!InBuild(ReunionDirector.EndingScene)) note.AppendLine("エンディング（" + ReunionDirector.EndingScene + "）がまだ組み立ての一覧に無い。暗転の後はタイトルの画面へ戻る");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 場面 9 では伏せたまま。起こすのは場面 10 の演出
            reunion.gameObject.SetActive(false);
        }

        static Texture2D Picture(string path, StringBuilder note)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) note.AppendLine("モンタージュの絵がまだ無い（HalfAware/Shoot the reunion montage で撮る）: " + path);
            return tex;
        }

        // ---- 片割れ ---------------------------------------------------------------------

        /// <summary>片割れの頬へ伸ばす腕と指。模型は根の x を裏返してあるので、模型の左の骨が世界の右（片割れ本人の右）に来る。親から順</summary>
        static readonly HumanBodyBones[] ReachBones =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        /// <summary>
        /// 片割れ。片割れの模型（茶の髪、白いワンピース、素足にサンダル、黒子は口元の右。<see cref="BuildRocketboxProtagonist.Build(Transform, bool)"/> の twin）を立たせ、
        /// 場面 4 の記憶の人と同じく <see cref="PersonMotion"/> が骨で動かす（線は演出が運ぶ）。首と頭はプレイヤーの目へ向ける。
        /// 口元を覆う手と頬へ伸ばす手の形（<see cref="ReunionDirector.MouthPose"/>・<see cref="ReunionDirector.CheekPose"/>）を持たせる
        /// </summary>
        static PersonMotion Twin(Transform reunion, StringBuilder note)
        {
            var root = new GameObject("Twin").transform;
            root.SetParent(reunion, false);
            root.position = TwinInside;
            root.rotation = Quaternion.identity;
            var her = BuildRocketboxProtagonist.Build(root, true);
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

            RigWoman(motion, an, her.transform, idle, walk, run, Vector3.zero, null, new Transform[0], new Quaternion[0]);
            var feet = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
            var strides = new float[PersonMotion.Reaches.Length];
            for (var i = 0; i < strides.Length; i++)
                strides[i] = BuildDive.FigureStride(root, her, motion, idle, walk, PersonMotion.Reaches[i], feet);

            idle.SampleAnimation(her, 0f);
            var drop = -Lowest(root, her);
            RigWoman(motion, an, her.transform, idle, walk, run, new Vector3(0f, drop, 0f), strides, new Transform[0], new Quaternion[0]);

            var bones = new Transform[ReachBones.Length];
            for (var i = 0; i < bones.Length; i++) bones[i] = an.GetBoneTransform(ReachBones[i]);
            var poses = TwinPoses(idle, drop, note);
            var mso = new SerializedObject(motion);
            mso.FindProperty("letsGo").boolValue = false;
            mso.FindProperty("attends").boolValue = true;
            mso.FindProperty("neck").objectReferenceValue = neck;
            mso.FindProperty("head").objectReferenceValue = headBone;
            mso.FindProperty("headAim").vector3Value = headAim.normalized;
            var p = mso.FindProperty("posed");
            p.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
            var q = mso.FindProperty("poses");
            q.arraySize = poses.Length;
            for (var i = 0; i < poses.Length; i++) q.GetArrayElementAtIndex(i).quaternionValue = poses[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            her.transform.localPosition = new Vector3(0f, drop, 0f);
            motion.Still();

            // 顔を起こす灯り。片割れの顔の前（プレイヤーの側）から、近くだけを弱く照らす。朝の日は東北東の低い所にあり、
            // テラスから寄ってくる片割れの顔（西を向く）は日の裏になる
            var lamp = new GameObject("FaceLight").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, FaceLightHeight, FaceLightAhead);
            lamp.type = LightType.Point;
            lamp.range = FaceLightRange;
            lamp.intensity = FaceLightIntensity;
            lamp.color = new Color(1f, 0.96f, 0.9f);
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;

            note.AppendFormat("片割れ: 歩幅 {0}、床へ {1:0.000} m、形 {2} 組", string.Join("/", System.Array.ConvertAll(strides, s => s.ToString("0.00"))), drop, poses.Length / Mathf.Max(1, bones.Length)).AppendLine();
            return motion;
        }

        /// <summary>顔を起こす灯りの置き場（片割れの根から、上と前）と、届く所と強さ</summary>
        const float FaceLightHeight = 1.55f, FaceLightAhead = 0.75f, FaceLightRange = 1.6f, FaceLightIntensity = 0.6f;

        /// <summary>
        /// 口元を覆う手と、頬へ伸ばす手の形。模型の写しを原点に +z 向きで立たせ、立ちの動きの頭の一こまから右手（模型の左の骨）を寄せて、
        /// 骨ごとに模型の根から見た向きとして取る。並びは <see cref="ReachBones"/> の数ずつ、口元・頬の順
        /// </summary>
        static Quaternion[] TwinPoses(AnimationClip idle, float drop, StringBuilder note)
        {
            var scratch = BuildRocketboxProtagonist.Build(null, true);
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                scratch.transform.SetPositionAndRotation(new Vector3(0f, drop, 0f), Quaternion.identity);
                var an = scratch.GetComponent<Animator>();
                an.runtimeAnimatorController = null;
                var n = ReachBones.Length;
                var all = new Quaternion[n * 2];
                var inverse = Quaternion.Inverse(scratch.transform.rotation);

                // 口元を覆う
                BodyPoser.Stand(an, idle);
                var eyes = BodyPoser.Eyes(an);
                var shoulder = an.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var mouth = eyes + MouthBelowEyes;
                var fingers = MouthFingers.normalized;
                var wrist = mouth + MouthAhead - fingers * HandToPalm;
                BodyPoser.Arm(an, true, wrist, shoulder + MouthElbow, fingers, MirrorPalm(MouthPalm));
                RelaxFingers(an);
                for (var i = 0; i < n; i++) all[i] = inverse * an.GetBoneTransform(ReachBones[i]).rotation;
                note.AppendFormat("片割れの目 {0}、口元の手首 {1}", eyes.ToString("F3"), wrist.ToString("F3")).AppendLine();

                // 頬へ伸ばす。プレイヤーの目は片割れの足元から前へ TouchApart、上へ立った目の高さ。触れるのはプレイヤーの左の頬（片割れの右の側）
                BodyPoser.Stand(an, idle);
                shoulder = an.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var eye = new Vector3(0f, TouchEyeAbove, TouchApart);
                var cheek = eye + CheekFromEye;
                fingers = CheekFingers.normalized;
                wrist = cheek - fingers * HandToPalm;
                // 手のひらはプレイヤーの目の方へ（寄せていく手の内が見える）
                BodyPoser.Arm(an, true, wrist, shoulder + CheekElbow, fingers, MirrorPalm((eye - cheek).normalized));
                // 指は揃えてほぼ伸ばし、頬に沿って少しだけ曲げる。親指も人差し指の脇へ寄せて伸ばす（親指だけが前へ突き出ないように）
                ShapeFingers(an, CheekFingerStretch, -1f, CheekThumbStretch, CheekThumbSpread);
                for (var i = 0; i < n; i++) all[n + i] = inverse * an.GetBoneTransform(ReachBones[i]).rotation;
                note.AppendFormat("頬の手首 {0}（プレイヤーの目 {1}）", wrist.ToString("F3"), eye.ToString("F3")).AppendLine();
                return all;
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        /// <summary>手首から手のひらの真ん中まで。m</summary>
        const float HandToPalm = 0.075f;

        /// <summary>口元（両目の真ん中から）と、手のひらを置く所（口元の前）</summary>
        static readonly Vector3 MouthBelowEyes = new Vector3(0f, -0.06f, 0.03f);
        static readonly Vector3 MouthAhead = new Vector3(0.01f, 0f, 0.06f);
        /// <summary>口元を覆う指の向き（上へ、少し本人の左へ）と、手のひらの向き（顔の方）と、肘を寄せる所（肩から）</summary>
        static readonly Vector3 MouthFingers = new Vector3(-0.35f, 0.94f, 0f);
        static readonly Vector3 MouthPalm = new Vector3(0f, 0f, -1f);
        static readonly Vector3 MouthElbow = new Vector3(0.18f, -0.35f, 0.15f);

        /// <summary>
        /// プレイヤーの目から、手のひらの真ん中を寄せる所（プレイヤーの左の頬の前、片割れの右）。目の脇の下、目より 13 cm 手前。
        /// 目のカメラは頭の芯にあるので、頬に当てた手はほとんど画面の外と手前に出る。頬へ寄せていく手のひらと揃えた指を、画面の左の縁に見せる。
        /// 指先は縁の外へ、頬の側へ回り込む。
        /// 頬そのものに当てた形は、手のひらの内の縁の親指が目の前を横切り、顔を隠した。手を下から上げた形は、親指の目立つ肌色の塊に見えた
        /// </summary>
        static readonly Vector3 CheekFromEye = new Vector3(0.12f, -0.05f, -0.13f);
        /// <summary>頬へ寄せる指の向き（上へ、外へ、耳の方へ）と、肘を寄せる所（肩から。外と下）。手のひらはプレイヤーの目の方へ向ける（<see cref="TwinPoses"/>）</summary>
        static readonly Vector3 CheekFingers = new Vector3(0.5f, 0.7f, 0.5f);
        static readonly Vector3 CheekElbow = new Vector3(0.35f, -0.35f, 0f);
        /// <summary>頬の手の指の曲げ（Humanoid の筋肉の Stretched。1 で伸び切り）と、親指の曲げと開き（Stretched を負にして手のひらへ折り、開きを負にして人差し指の脇へ寄せる）</summary>
        const float CheekFingerStretch = 0.6f, CheekThumbStretch = -0.6f, CheekThumbSpread = -1f;

        /// <summary>
        /// 両手の指の形を Humanoid の筋肉の値で決める（<see cref="RelaxFingers"/> と同じ作り。指の骨の外は元へ戻す）。
        /// fingerStretch・fingerSpread は四本の指（中指の開きは 0 のまま）、thumbStretch・thumbSpread は親指
        /// </summary>
        static void ShapeFingers(Animator an, float fingerStretch, float fingerSpread, float thumbStretch, float thumbSpread)
        {
            if (an == null || an.avatar == null || !an.avatar.isHuman) return;
            var keep = new List<(Transform, Vector3, Quaternion)>();
            var fingerBones = new HashSet<Transform>();
            for (var b = HumanBodyBones.LeftThumbProximal; b <= HumanBodyBones.RightLittleDistal; b++)
            {
                var t = an.GetBoneTransform(b);
                if (t != null) fingerBones.Add(t);
            }
            foreach (var t in an.GetComponentsInChildren<Transform>(true))
                if (!fingerBones.Contains(t)) keep.Add((t, t.localPosition, t.localRotation));
            var handler = new HumanPoseHandler(an.avatar, an.transform);
            try
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                var names = HumanTrait.MuscleName;
                for (var m = 0; m < names.Length; m++)
                {
                    var name = names[m];
                    if (!name.StartsWith("Left ") && !name.StartsWith("Right ")) continue;
                    var thumb = name.Contains(" Thumb ");
                    var finger = thumb || name.Contains(" Index ") || name.Contains(" Middle ") || name.Contains(" Ring ") || name.Contains(" Little ");
                    if (!finger) continue;
                    if (name.EndsWith("Stretched")) pose.muscles[m] = thumb ? thumbStretch : fingerStretch;
                    else if (name.EndsWith("Spread")) pose.muscles[m] = thumb ? thumbSpread : name.Contains(" Middle ") ? 0f : fingerSpread;
                }
                handler.SetHumanPose(ref pose);
            }
            finally
            {
                handler.Dispose();
                foreach (var k in keep) { k.Item1.localPosition = k.Item2; k.Item1.localRotation = k.Item3; }
            }
        }

        /// <summary>片割れの模型は根の x を裏返してあり、BodyPoser が指の並びから出す手のひらの向きが逆になる（<see cref="HostSit"/> と同じ）</summary>
        static Vector3 MirrorPalm(Vector3 palm)
        {
            return BuildRocketboxProtagonist.Twin == BuildRocketboxProtagonist.TwinMode.MirrorWhole ? -palm : palm;
        }

        // ---- 行き先 -----------------------------------------------------------------------

        /// <summary>そのシーンが組み立ての一覧に入っているか</summary>
        static bool InBuild(string scene)
        {
            foreach (var s in EditorBuildSettings.scenes)
                if (System.IO.Path.GetFileNameWithoutExtension(s.path) == scene) return true;
            return false;
        }
    }
}
