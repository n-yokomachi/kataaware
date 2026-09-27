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
    /// - 主はテラスの白いパラソルの卓の、北の椅子（卓から 80 度、<see cref="HostChair"/>）に座る。この椅子は場面 6 の間だけ卓に背を向け、
    ///   西北西の夕日の方へ回る（<see cref="TurnedChairYaw"/>。卓の組み立て <c>Parasol</c> が形を分け、演出が回す）。朝の村では卓へ向いたまま
    /// - 女性は、テラスの西の脇から北へ抜ける煉瓦の小路の西の縁（<see cref="WomanAt"/>）で、西の塀の下の花の縁に水を撒いている。主の目から顔まで 3.5 m、
    ///   方位 305 度（主の体の向きの真正面）。日（方位 290 度、仰角 11 度）はその左上にあり、女性の後ろに低い夕日と明るい空が来る。夕日を背にするので、顔の側は影
    /// - 歩く道: 小路を南へテラスの西の脇（<see cref="WalkBend"/>）まで下り、テラスへ上がって主の方（<see cref="WalkEnd"/>）へ。
    ///   女性はずっと夕日を背にしている。あいだに卓と椅子は入らない（どれも主の後ろ）
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 並び -----------------------------------------------------------------------

        /// <summary>主の座る椅子の、卓から見た向き（度）。夕日の方へ向けた北の一脚（<see cref="TurnedChairDeg"/>）</summary>
        const float HostChairDeg = TurnedChairDeg;

        /// <summary>主の体の向き。度（+z が 0 で東回り）。椅子の向きのまま、夕日の方</summary>
        public const float HostYaw = TurnedChairYaw;

        /// <summary>
        /// 女性が水を撒く立ち位置。テラスの西の脇から北へ抜ける煉瓦の小路の西の縁（z 19.75 で芯は x -4.1 ほど、幅 1.1 m）。西の花の縁の前。
        /// 主の目から 3.5〜4 m に置く（z 19.3 では 3.4 m で近すぎた）
        /// </summary>
        public static readonly Vector3 WomanAt = new Vector3(-4.62f, 0f, 19.75f);

        /// <summary>水を撒くときに向く向き。西の塀の下の花の縁（夕日の方）</summary>
        public const float FlowerYaw = 262f;

        /// <summary>歩く道の曲がり角。小路の、テラスの北西の角の脇</summary>
        public static readonly Vector3 WalkBend = new Vector3(-4.15f, 0f, 18.35f);

        /// <summary>歩く道の終わり。テラスの上、主の前。ここへ着く前に途切れる</summary>
        public static readonly Vector3 WalkEnd = new Vector3(-2.45f, TerraceTopY, 17.95f);

        /// <summary>テラスの上面の高さ（<c>BuildVillageGarden.TerraceTop</c> と同じ）</summary>
        const float TerraceTopY = 0.1f;

        /// <summary>歩く速さ。m/s。ゆっくり歩いてくる</summary>
        public const float WalkSpeed = 0.4f;

        /// <summary>歩き出す、送ってからの秒（庭の時計）。ホースを止めて、こちらを向いてから</summary>
        public const float WalkAt = 52f;

        /// <summary>ホースの出どころ。西の花の縁の奥の地面（花に隠れる。塀の際の水栓から来ている見立て）</summary>
        static readonly Vector3 HoseSource = new Vector3(-5.7f, 0.02f, 18.6f);
        /// <summary>ホースが花の縁の前と小路を這う途中の点</summary>
        static readonly Vector3[] HoseRun =
        {
            new Vector3(-5.1f, 0.02f, 19.1f),
            new Vector3(-4.8f, 0.02f, 19.4f),
        };

        /// <summary>主の体の置き場の名</summary>
        public const string MemoryName = "GardenMemory";
        public const string DirectorName = "GardenMemoryDirector";

        /// <summary>途切れた後に読む場面。場面 7（自室・気づき）</summary>
        public const string NextScene = "Notice";

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
            // 主の椅子。朝の向き（卓へ）で保存し、場面 6 の間だけ演出が夕日の方へ回す
            var turned = root.Find("Garden/" + TurnedChairName);
            if (turned == null) note.AppendLine("回す椅子が無い: Garden/" + TurnedChairName);
            so.FindProperty("turnedChair").objectReferenceValue = turned;
            so.FindProperty("turnedChairYaw").floatValue = TurnedChairYaw;
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
            // 途切れた次のフレームに場面 7（自室・気づき）へ
            so.FindProperty("nextScene").stringValue = NextScene;
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
            LiftToes(an, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, ToeLift);
            RestHands(an, BuildRocketboxProtagonist.Twin == BuildRocketboxProtagonist.TwinMode.MirrorWhole);
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
            SeatedDrape(her, note);

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
        /// 斜め前へ伸ばした足（右）の爪先を上げる角。度。踵をテラスに置いて爪先を少し起こすと、上から見下ろす目に足の甲とサンダルが長く見える
        /// （爪先を下げたままだと、足が目の向きと並んで縮んで見えた）
        /// </summary>
        const float ToeLift = 20f;

        /// <summary>手首を置く所（腿の付け根から膝への割合と、腿の骨の芯からの高さ m。布の載る丸の半径に手の厚みの半分を足した値）と、指を腿の向きから下げる割合</summary>
        const float HandAlong = 0.33f, HandLift = 0.11f, HandDip = 0.05f;

        /// <summary>
        /// 両手を腿の上に楽に置く。手首を腿の上の面に下ろし、指を腿に沿って前へ少し下げ、手の甲を上へ向け、指を揃えて軽く曲げる。
        /// 座らせる形（<see cref="HostSit"/>）の手首は腿より 8 cm ほど浮いていて、指が下へ垂れて開き、見下ろすと鉤爪のように見えた。
        /// mirror なら、BodyPoser が指の並びから出す手のひらの向きが上下逆になる（片割れの模型は根の x を裏返してある）
        /// </summary>
        public static void RestHands(Animator an, bool mirror)
        {
            var hips = an.GetBoneTransform(HumanBodyBones.Hips).position;
            foreach (var left in new[] { true, false })
            {
                var hip = an.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position;
                var knee = an.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
                var along = (knee - hip).normalized;
                var wrist = Vector3.Lerp(hip, knee, HandAlong) + Vector3.up * HandLift;
                var outward = hip - hips;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.zero;
                var back = new Vector3(-along.x, 0f, -along.z).normalized;
                var pole = hip + outward * 0.45f + back * 0.2f + Vector3.up * 0.15f;
                var fingers = (along + Vector3.down * HandDip).normalized;
                var palm = mirror ? Vector3.up : Vector3.down;
                BodyPoser.Arm(an, left, wrist, pole, fingers, palm);
            }
            RelaxFingers(an);
        }

        /// <summary>指の曲げ（Stretched の筋肉の値。1 で伸び切り、負で握る）と開き（Spread。負で閉じる）。四本の指と親指。中指の開きは 0 のまま</summary>
        const float FingerStretch = 0.1f, FingerSpread = -1f, ThumbStretch = 0f, ThumbSpread = -1f;

        /// <summary>
        /// 両手の指を、揃えて軽く曲げた形にする。Humanoid の指の筋肉の値で決め（関節の限りと曲げの面は骨組みに任せる）、指の骨の向きだけを書き換える。
        /// 体のほかの骨は触らない（筋肉の値を通すと、座らせた脚や腕の形が限りへ寄せられて崩れるので、指の骨の外は元へ戻す）
        /// </summary>
        public static void RelaxFingers(Animator an)
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
                    if (name.EndsWith("Stretched")) pose.muscles[m] = thumb ? ThumbStretch : FingerStretch;
                    else if (name.EndsWith("Spread")) pose.muscles[m] = thumb ? ThumbSpread : name.Contains(" Middle ") ? 0f : FingerSpread;
                }
                handler.SetHumanPose(ref pose);
            }
            finally
            {
                handler.Dispose();
                foreach (var k in keep) { k.Item1.localPosition = k.Item2; k.Item1.localRotation = k.Item3; }
            }
        }

        /// <summary>足の骨を、足首を中心に爪先が上がる向きへ degrees 度回す（足の向きに直交する水平の軸で）</summary>
        static void LiftToes(Animator an, HumanBodyBones foot, HumanBodyBones toes, float degrees)
        {
            var f = an.GetBoneTransform(foot);
            var t = an.GetBoneTransform(toes);
            if (f == null || t == null) return;
            var along = t.position - f.position;
            along.y = 0f;
            if (along.sqrMagnitude < 1e-6f) return;
            var axis = Vector3.Cross(Vector3.up, along.normalized);
            f.rotation = Quaternion.AngleAxis(-degrees, axis) * f.rotation;
        }

        /// <summary>座った体をベイクして置くメッシュ</summary>
        const string HostSeatedPath = Generated + "HostSeated.asset";

        /// <summary>
        /// 座った時のワンピースの裾の上げ方。裾の段の切り替え（膝の少し下）から下の布を、この割合の長さへ詰める。
        /// 裾は膝の先で脛へ垂れ、脛の中ほどで終わる。その先の脛と足首とサンダルは裾の外に出る
        /// </summary>
        const float SeatedHem = 0.3f;

        /// <summary>
        /// 座った体のワンピースを、座った形の布に作り直してベイクし（<see cref="SeatedMesh"/>）、骨で動く体の代わりに置く。
        /// 主は歩かないので、座った形の体を一枚のメッシュにして置く（<see cref="HostSeatedPath"/>）。骨で動く元の体は切り、頭の影（HeadShadow）はそのまま残す
        /// </summary>
        static void SeatedDrape(GameObject her, StringBuilder note)
        {
            var body = SeatedBody(her);
            if (body == null) { note.AppendLine("座った体の mesh が見つからない。裾を上げられない"); return; }
            var mesh = SeatedMesh(her, body, note);
            RocketboxCompose.Save(mesh, HostSeatedPath);
            Object.DestroyImmediate(mesh);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(HostSeatedPath);

            // 骨で動く体と同じ所に、ベイクした体を置く
            var go = new GameObject("Seated");
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var r = go.AddComponent<MeshRenderer>();
            var mats = body.sharedMaterials;
            // ワンピースは座った主だけの複製にして、陰を少し持ち上げる（SeatedDressFill）
            mats[mats.Length - 1] = SeatedDressMat(mats[mats.Length - 1]);
            r.sharedMaterials = mats;
            r.shadowCastingMode = body.shadowCastingMode;
            r.receiveShadows = body.receiveShadows;
            body.enabled = false;
        }

        const string SeatedDressPath = "Assets/Materials/Village/HostSeatedDress.mat";

        /// <summary>
        /// 座った主のワンピースに足す、陰を持ち上げる明るさ（放射の強さ。布の絵の色に掛ける）。
        /// 夕方の庭で見下ろすと、空の光だけを受ける膝の上の布が青みの灰色に沈み、日の当たる胸元だけが黄色く浮いて、
        /// 白いワンピースに見えなかった。テラスから返る光の見立てで、陰の側を少しだけ明るくする
        /// </summary>
        public const float SeatedDressFill = 0.2f;

        /// <summary>座った主のワンピースのマテリアル。元のワンピース（片割れのもの）を写し、放射で陰を持ち上げる</summary>
        public static Material SeatedDressMat(Material dress)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(SeatedDressPath);
            if (m == null)
            {
                m = new Material(dress) { name = "HostSeatedDress" };
                AssetDatabase.CreateAsset(m, SeatedDressPath);
            }
            else m.CopyPropertiesFromMaterial(dress);
            FillDress(m, dress.GetTexture("_BaseMap"));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>ワンピースのマテリアル m に、布の絵の色で陰を持ち上げる放射を掛ける</summary>
        public static void FillDress(Material m, Texture map)
        {
            m.SetTexture("_EmissionMap", map);
            m.SetColor("_EmissionColor", new Color(1f, 0.98f, 0.95f) * SeatedDressFill);
            m.EnableKeyword("_EMISSION");
            // None にすると、URP のマテリアルの見直し（取り込みのたびや、場面を撮る前に走る）が _EMISSION を落とし、光らなくなった（BuildAlleyCrowd と同じ）
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        /// <summary>座った体の、服を着た体の皮（頭の影でない、面の組が五つ以上のもの）</summary>
        public static SkinnedMeshRenderer SeatedBody(GameObject her)
        {
            SkinnedMeshRenderer body = null;
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.name != "HeadShadow" && smr.sharedMesh != null && smr.sharedMesh.subMeshCount >= 5) body = smr;
            return body;
        }

        /// <summary>
        /// 座った形の体を一枚のメッシュにベイクし、ワンピースのスカートを座った形の布に作り直して返す（保存しない。確かめの道具からも呼ぶ）。
        ///
        /// くるぶし丈のスカートは、骨に付いたままだと脛のまわりに輪で立ち、座ると上から足を隠す。
        /// 1. 裾の下の段（膝の少し下から裾まで）の布を、同じ縦の列の上の方の布の所へ寄せて短くする（<see cref="SeatedHem"/>）。
        ///    列と段は布の絵の置き方（UV）で分かる。表と裏の布は別々に寄せる
        /// 2. 腰より前の布を、腿の上に沿わせて下ろし、膝の先では脛へ垂らし、脚の脇では回り込ませ、二本の脚のあいだでは少し沈める（<see cref="LayOnLegs"/>）
        /// 3. 動かした布の法線を、面の形から出し直す（陰の側が沈んで、布の起伏が見える）
        /// </summary>
        public static Mesh SeatedMesh(GameObject her, SkinnedMeshRenderer body, StringBuilder note)
        {
            var mesh = new Mesh { name = "HostSeated" };
            // 体の Transform のローカル空間でベイクする（useScale）。片割れの模型は根の x を裏返してあり、scale を外してベイクすると、
            // 同じ所に置いた時にもう一度裏返り、脚の左右が骨と入れ違った（伸ばした足が反対の側に出た）
            body.BakeMesh(mesh, true);
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            var dress = mesh.GetTriangles(mesh.subMeshCount - 1);
            var front = new HashSet<int>();
            var lining = new HashSet<int>();
            for (var t = 0; t < dress.Length; t++) (t < dress.Length / 2 ? front : lining).Add(dress[t]);

            // 裾の段の切り替え（膝の少し下）の UV の高さ。ワンピースの UV は、腰で 0.5、裾で 0（RocketboxDress.Skirt）
            var twin = BuildRocketboxProtagonist.Chosen.TwinPerson;
            var bodySmr = AssetDatabase.LoadAssetAtPath<GameObject>(twin.BodyFrom.Model).GetComponentInChildren<SkinnedMeshRenderer>();
            var frame = RocketboxDress.Frame.Of(bodySmr);
            var yTop = frame.waistY + 0.012f;
            var vTier = 0.5f * (1f - (yTop - RocketboxDress.TierY) / (yTop - RocketboxDress.HemY));

            var moved = 0;
            foreach (var set in new[] { front, lining })
            {
                // 縦の列ごとに、下の段の頂点を UV の高さで並べる
                var columns = new Dictionary<int, List<int>>();
                foreach (var i in set)
                {
                    if (uv[i].y >= 0.5f || uv[i].y > vTier + 0.02f) continue;
                    var key = Mathf.RoundToInt(uv[i].x * 1000f);
                    List<int> col;
                    if (!columns.TryGetValue(key, out col)) columns[key] = col = new List<int>();
                    col.Add(i);
                }
                var nv = (Vector3[])v.Clone();
                var nn = (Vector3[])n.Clone();
                foreach (var col in columns.Values)
                {
                    col.Sort((a, b) => uv[a].y.CompareTo(uv[b].y));
                    var top = uv[col[col.Count - 1]].y;
                    foreach (var i in col)
                    {
                        // 段の切り替えの所はそのまま、裾は切り替えから SeatedHem の所へ
                        var want = top - (top - uv[i].y) * SeatedHem;
                        var k = 0;
                        while (k < col.Count - 2 && uv[col[k + 1]].y < want) k++;
                        var a = col[k];
                        var b = col[k + 1];
                        var span = uv[b].y - uv[a].y;
                        var s = span > 1e-6f ? Mathf.Clamp01((want - uv[a].y) / span) : 0f;
                        nv[i] = Vector3.Lerp(v[a], v[b], s);
                        nn[i] = Vector3.Slerp(n[a], n[b], s).normalized;
                        moved++;
                    }
                }
                v = nv;
                n = nn;
            }
            var laid = LayOnLegs(her, body.transform, uv, v, n, dress);
            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            if (note != null)
                note.AppendFormat("座った体をベイクした: 裾の下の段を {0:0.00} の長さへ詰めた頂点 {1}（段の切り替えは UV {2:0.000}）、脚の上へ下ろした頂点 {3}", SeatedHem, moved, vTier, laid).AppendLine();
            return mesh;
        }

        /// <summary>腿・膝・脛の、骨の芯から布の載る面までの半径（m）。脚の太さに布の厚みを足した値</summary>
        const float ThighTop = 0.085f, KneeTop = 0.085f, ShinTop = 0.06f;

        /// <summary>二本の脚のあいだで、布が脚の上の面を結んだ線より沈む深さ（m、あいだの真ん中で）。脚が寄った腿の所では、この 4 割</summary>
        const float LapSag = 0.045f;

        /// <summary>膝より先の二本の脚のあいだで、前へ出るほど布が深く沈む割合（前へ 1 m ごとに沈む m）。膝と膝のあいだの布は脛の方へ垂れる</summary>
        const float FrontSag = 1.2f;

        /// <summary>脚の芯から、布を出しておく隙（m）。布の載る丸の半径に足す。脚が布を突き抜けないように押し出す所まで</summary>
        const float LegClear = 0.008f;

        /// <summary>脚の脇の、回り込んだ先で布が垂れる傾き（横へ 1 m 出るごとに下がる m）</summary>
        const float SideFall = 3f;

        /// <summary>
        /// 座った体のスカートの布を、脚の上に載った形へ下ろす。腰より前の布の頂点を、その所での布の載る面より上へ出さない。
        ///
        /// 布の載る面は、腿から膝、膝から足首を結んだ骨の芯のまわりの丸（半径は <see cref="ThighTop"/> など）の上の縁。
        /// - 脚の上では丸の上の縁に沿う。脚の脇では丸に沿って回り込み、その先は下へ垂れる（<see cref="SideFall"/>）。角の立った板にしない
        /// - 二本の脚のあいだでは、二本の上の縁を結んだ線から少し沈む（<see cref="LapSag"/>）
        /// - 膝の先では、脛の上の縁に沿って下がる。布は膝から脛へ垂れる
        ///
        /// 脚より低い所の布（脇に垂れた布や腿の下の布）は動かさない。動かした布の法線は、面の形から出し直す（上へ向け揃えると、白い板に見えた）。
        /// v と n は体のメッシュの中の位置と法線で、書き換える。tris はワンピースの面（前半が表、後半が裏）。下ろした頂点の数を返す
        /// </summary>
        static int LayOnLegs(GameObject her, Transform body, Vector2[] uv, Vector3[] v, Vector3[] n, int[] tris)
        {
            var an = her.GetComponent<Animator>();
            System.Func<HumanBodyBones, Vector3> B = b => an.GetBoneTransform(b).position;
            var ahead = her.transform.forward;
            ahead.y = 0f;
            ahead.Normalize();
            var side = Vector3.Cross(Vector3.up, ahead);
            var legs = new[]
            {
                new[] { B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot) },
                new[] { B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot) },
            };
            var hipAhead = Vector3.Dot(B(HumanBodyBones.Hips), ahead);
            // 二本の膝の前後の位置の、近い方（手前の膝）
            var kneeNear = Mathf.Min(Vector3.Dot(legs[0][1], ahead), Vector3.Dot(legs[1][1], ahead));
            // 前後の位置 f での、一本の脚の骨の芯の高さ（x）・横の位置（y）・布の載る丸の半径（z）
            System.Func<Vector3[], float, Vector3> legAt = (leg, f) =>
            {
                float fh = Vector3.Dot(leg[0], ahead), fk = Vector3.Dot(leg[1], ahead), fa = Vector3.Dot(leg[2], ahead);
                Vector3 p;
                float r;
                if (f <= fk)
                {
                    var t = Mathf.InverseLerp(fh, fk, f);
                    p = Vector3.Lerp(leg[0], leg[1], t);
                    r = Mathf.Lerp(ThighTop, KneeTop, t);
                }
                else
                {
                    var t = Mathf.InverseLerp(fk, fa, f);
                    p = Vector3.Lerp(leg[1], leg[2], t);
                    r = Mathf.Lerp(KneeTop, ShinTop, t);
                }
                return new Vector3(p.y, Vector3.Dot(p, side), r);
            };
            // 一本の脚の丸から、横へ e 離れた所で布が載る高さ
            System.Func<Vector3, float, float> over = (leg, e) =>
                e <= leg.z ? leg.x + Mathf.Sqrt(leg.z * leg.z - e * e) : leg.x - (e - leg.z) * SideFall;
            System.Func<Vector3, float> top = w =>
            {
                var f = Vector3.Dot(w, ahead);
                var a = legAt(legs[0], f);
                var b = legAt(legs[1], f);
                if (a.y > b.y) { var c = a; a = b; b = c; }
                var s = Vector3.Dot(w, side);
                if (s <= a.y) return over(a, a.y - s);
                if (s >= b.y) return over(b, s - b.y);
                // あいだ。二本の上の縁を結ぶ線から沈める。脚が寄った所では浅く、膝より先では前へ出るほど深く
                var t = (s - a.y) / Mathf.Max(b.y - a.y, 1e-4f);
                var gap = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01((b.y - a.y - a.z - b.z) / 0.06f));
                var dip = (LapSag + Mathf.Max(0f, f - kneeNear) * FrontSag) * gap * Mathf.Sin(Mathf.PI * t);
                var bridge = Mathf.Lerp(a.x + a.z, b.x + b.z, t) - dip;
                return Mathf.Max(bridge, over(a, s - a.y), over(b, b.y - s));
            };
            var toWorld = body.localToWorldMatrix;
            var toLocal = body.worldToLocalMatrix;
            var weight = new float[v.Length];
            var laid = 0;
            for (var i = 0; i < v.Length; i++)
            {
                if (uv[i].y >= 0.5f) continue;
                var w = toWorld.MultiplyPoint3x4(v[i]);
                var f = Vector3.Dot(w, ahead);
                if (f < hipAhead + 0.08f) continue;
                // 腰の近くは少しずつ効かせる（腰の布との継ぎ目に段を作らない）
                var k = Mathf.Clamp01((f - hipAhead - 0.08f) / 0.12f);
                weight[i] = k;
                var cap = top(w);
                if (w.y <= cap) continue;
                w.y = Mathf.Lerp(w.y, cap, k);
                v[i] = toLocal.MultiplyPoint3x4(w);
                laid++;
            }

            // 膝の先で垂れた布の中から脛が前へ出ないよう、脛の前の面より奥にある布を前へ出す。前へだけ動かす
            // （脚の丸の外へ四方へ押し出すと、布の面が裏返って穴が開き、脚の肌が覗いた）
            for (var i = 0; i < v.Length; i++)
            {
                if (weight[i] <= 0f) continue;
                var w = toWorld.MultiplyPoint3x4(v[i]);
                var moved = false;
                foreach (var leg in legs)
                {
                    var knee = leg[1];
                    var ankle = leg[2];
                    // 立った脛（曲げた脚）だけ。前へ伸ばした脚の脛には、布が上から載っている
                    if ((knee.y - ankle.y) < 0.9f * Vector3.Distance(knee, ankle)) continue;
                    if (w.y > knee.y || w.y < ankle.y) continue;
                    // その高さでの脛の芯
                    var t = Mathf.InverseLerp(knee.y, ankle.y, w.y);
                    var core = Vector3.Lerp(knee, ankle, t);
                    var r = Mathf.Lerp(KneeTop, ShinTop, t) + LegClear;
                    var off = w - core;
                    var across = Vector3.Dot(off, side);
                    var fore = Vector3.Dot(off, ahead);
                    if (Mathf.Abs(across) >= r || fore >= r || fore < -r * 0.5f) continue;
                    // 脛の丸の前の縁まで出す（横へずれている所は丸の縁の前後の深さまで）
                    var want = Mathf.Sqrt(r * r - across * across);
                    if (fore >= want) continue;
                    w += ahead * ((want - fore) * weight[i]);
                    moved = true;
                }
                if (!moved) continue;
                v[i] = toLocal.MultiplyPoint3x4(w);
            }

            // 法線を面の形から出し直す。表と裏の組ごとに、同じ所にある頂点（布の絵の継ぎ目で分かれた頂点）はまとめて滑らかにする
            var half = tris.Length / 2;
            System.Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
            foreach (var range in new[] { new Vector2Int(0, half), new Vector2Int(half, tris.Length) })
            {
                var sum = new Dictionary<Vector3Int, Vector3>();
                var used = new HashSet<int>();
                for (var t = range.x; t + 2 < range.y; t += 3)
                {
                    int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                    var face = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
                    foreach (var i in new[] { i0, i1, i2 })
                    {
                        if (weight[i] <= 0f) continue;
                        used.Add(i);
                        var kk = key(v[i]);
                        Vector3 acc;
                        sum.TryGetValue(kk, out acc);
                        sum[kk] = acc + face;
                    }
                }
                // 面の巻きの向きと法線の向きの取り決めは、元の法線と比べて決める（組ごとに一つ）
                var agree = 0f;
                foreach (var i in used)
                {
                    Vector3 acc;
                    if (sum.TryGetValue(key(v[i]), out acc) && acc.sqrMagnitude > 1e-12f) agree += Vector3.Dot(acc.normalized, n[i]);
                }
                var sign = agree >= 0f ? 1f : -1f;
                foreach (var i in used)
                {
                    Vector3 acc;
                    if (!sum.TryGetValue(key(v[i]), out acc) || acc.sqrMagnitude < 1e-12f) continue;
                    n[i] = Vector3.Slerp(n[i], acc.normalized * sign, weight[i]).normalized;
                }
            }
            return laid;
        }

        /// <summary>
        /// 白い鉄のビストロの椅子（座面 0.45 m、丸く肘掛けは無い）に座った形。値は椅子の足元から見た位置で、+z が体の前。
        /// 腿は座面に乗せ、片脚を斜め前へ楽に伸ばし、もう片脚は膝を曲げて足を引き、サンダルをテラスに置く。手は腿の上。
        /// 座った目から見ると、足首が自分の膝の線を越えて見えるのは目から 0.65 m ほどより先。膝の真下や少し前の足は、膝と裾の向こうに隠れる。
        /// 裾は座った形で膝のすぐ下まで上げる（<see cref="SeatedDrape"/>）。場面 6 では下を 60 度まで向けられるので（演出の pitchDown）、
        /// 下を向くと、膝の上の白いワンピースと両手、その先に脛と足首とサンダルが見える
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
                // 背は少し前へ倒す（膝の上へ目が出て、膝の向こうの足が見える。頭は立てたまま）
                lean = 14f,
                headKeep = 1f,
                // 右の脚（模型の +x。裏返した片割れでは体の左、卓の脇の空いたテラスの側）を斜め前へ楽に伸ばし、左は膝を曲げて足を膝より奥へ引く（膝から垂れた裾の中に脛が収まる。膝の前へ出すと、脛が裾の前から覗いた）。
                // 両足とも膝の少し前（足首 0.62・0.66 m）に置くと、目から見て膝と裾の向こうに隠れ、爪先しか見えなかった。
                // 右を真っすぐ前（0.71 m）へ伸ばしても、膝の上の裾の向こうに足の甲が細く覗くだけだった。斜めに開くと、裾の脇に脛と足首とサンダルが出る
                ankleL = P(-0.10f, 0.085f, 0.32f),
                ankleR = P(0.38f, 0.085f, 0.62f),
                kneePoleL = P(-0.10f, 0.9f, 1.2f),
                kneePoleR = P(0.34f, 0.9f, 1.2f),
                footPoint = 0f,
                // 手の初めの形。この後で RestHands が腿の上に置き直す
                wristL = P(-0.14f, 0.61f, 0.13f),
                wristR = P(0.14f, 0.61f, 0.13f),
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.66f, 1f), new Color(1f, 0.97f, 0.9f, 0.7f));
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 300f;
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
