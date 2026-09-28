using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 助手席の片割れ。片割れの模型（<see cref="BuildRocketboxProtagonist.Build(Transform, bool)"/>。茶の髪、白いワンピース、素足にサンダル）を
    /// 下ろした助手席（<see cref="BuildEnding.PassengerDrop"/>）に座らせ、座った形のまま一枚のメッシュに焼いて置く。
    /// 焼き方は場面 6 の主と同じ（<see cref="BuildVillage.SeatedMesh"/>。裾を座った形の布に作り直す）。
    ///
    /// 座り方は運転席の主人公と同じ低い座面の形（腰 0.87、脚を前へ投げ出して踵を床に）で、背は少し起こし、
    /// 両手を腿の上に楽に置く（<see cref="BuildVillage.RestHands"/> と同じ置き方で、膝の方へ寄せる）。顔は自分の窓の外へ向ける（仮。13.2 節「片割れの体の姿勢」）。
    /// 目が向いても顔は画面に入らない（画面の上の縁が顎より下。<see cref="BuildEnding.TwinLook"/>）が、顔を窓へ向けておけば、
    /// 縁に掛かっても見えるのは髪と耳の後ろ
    /// </summary>
    public static partial class BuildEnding
    {
        /// <summary>助手席の真ん中の x（車の座標）</summary>
        const float PassengerX = -0.42f;

        /// <summary>荷室と客室の床の上面（BuildDriveCar.HoldFloorY）</summary>
        const float CabinFloorY = 0.685f;

        /// <summary>背を前へ倒す角。度。運転席の主人公（腕を組んで考え込む 20 度）より起こす</summary>
        const float TwinLean = 6f;

        /// <summary>
        /// 腰を座面の真ん中より前へ出す量。m。目が向いた時、腿の上の手が変速の把手（z 0.11〜0.19、高さ 1.26 まで）の陰に入らないように、
        /// 手の置き場（腿の付け根から 3 割）を把手より前へ出す
        /// </summary>
        const float TwinSeatForward = 0.08f;

        /// <summary>顔を自分の窓（-x）の方へ回す角。度。首と頭で分ける</summary>
        public const float TwinHeadTurn = 40f;

        /// <summary>座った体を焼いて置くメッシュ</summary>
        const string TwinSeatedPath = Generated + "TwinSeated.asset";

        static void Twin(Transform holder, Transform car, StringBuilder note)
        {
            Clear(holder);
            var mirror = BuildRocketboxProtagonist.Twin == BuildRocketboxProtagonist.TwinMode.MirrorWhole;
            var her = BuildRocketboxProtagonist.Build(holder, true);
            her.name = "Twin";
            var an = her.GetComponent<Animator>();
            her.transform.SetPositionAndRotation(car.TransformPoint(new Vector3(PassengerX, CabinFloorY, 0f)), car.rotation);
            BodyPoser.Stand(an);
            BodyPoser.Pose(an, TwinSit(car, mirror));
            RestHands(an, mirror);
            Turn(an, mirror);
            // 動きを止めると骨が素の形へ戻るので、座った形の骨の値を持っておき、止めてから書き戻す（場面 6 の主と同じ）
            var bones = her.GetComponentsInChildren<Transform>(true);
            var at = new Vector3[bones.Length];
            var turn = new Quaternion[bones.Length];
            for (var i = 0; i < bones.Length; i++) { at[i] = bones[i].localPosition; turn[i] = bones[i].localRotation; }
            an.runtimeAnimatorController = null;
            an.enabled = false;
            for (var i = 0; i < bones.Length; i++) { bones[i].localPosition = at[i]; bones[i].localRotation = turn[i]; }

            var body = BuildVillage.SeatedBody(her);
            if (body == null) { note.AppendLine("片割れの服を着た体の mesh が見つからない"); return; }
            var mesh = BuildVillage.SeatedMesh(her, body, note);
            RocketboxCompose.Save(mesh, TwinSeatedPath);
            Object.DestroyImmediate(mesh);
            var go = new GameObject("Seated");
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TwinSeatedPath);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = body.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.enabled = false;

            var eyes = BodyPoser.Eyes(an);
            var hips = an.GetBoneTransform(HumanBodyBones.Hips).position;
            note.AppendFormat("片割れ: 腰 {0}、目 {1}（車の座標）、頭の上の縁 {2:0.000}（天井の板 1.88）", car.InverseTransformPoint(hips).ToString("F3"),
                car.InverseTransformPoint(eyes).ToString("F3"), Top(go, car)).AppendLine();
        }

        /// <summary>
        /// 助手席に座った形の狙い（車の座標）。運転席の主人公の形（BuildDriveBody.Sit）を助手席へ写し、背を起こして、手は腿の上（後で RestHands が置き直す）。
        /// mirror なら模型の左右の骨を入れ替える（片割れの模型は根の x を裏返してあり、左の骨が世界の右に来る）
        /// </summary>
        static BodyPoser.Sit TwinSit(Transform car, bool mirror)
        {
            var sx = mirror ? -1f : 1f;
            var palmY = mirror ? 1f : -1f;
            System.Func<float, float, float, Vector3> P = (x, y, z) => car.TransformPoint(new Vector3(PassengerX + x * sx, y, z));
            System.Func<float, float, float, Vector3> D = (x, y, z) => car.TransformDirection(new Vector3(x * sx, y, z));
            return new BodyPoser.Sit
            {
                hips = P(0f, 0.87f, TwinSeatForward),
                pelvis = 0f,
                lean = TwinLean,
                headKeep = 1f,
                ankleL = P(-0.11f, CabinFloorY + 0.115f, 0.88f),
                ankleR = P(0.11f, CabinFloorY + 0.115f, 0.90f),
                kneePoleL = P(-0.14f, 1.4f, 1.2f),
                kneePoleR = P(0.14f, 1.4f, 1.2f),
                footPoint = -10f,
                wristL = P(-0.14f, 1.0f, 0.28f),
                wristR = P(0.14f, 1.0f, 0.28f),
                elbowPoleL = P(-0.45f, 0.95f, -0.15f),
                elbowPoleR = P(0.45f, 0.95f, -0.15f),
                fingersL = D(0.1f, -0.35f, 1f),
                palmL = D(0f, palmY, 0f),
                fingersR = D(-0.1f, -0.35f, 1f),
                palmR = D(0f, palmY, 0f),
            };
        }

        /// <summary>手首を置く所（腿の付け根から膝への割合と、腿の骨の芯からの高さ m）と、指を腿の向きから下げる割合</summary>
        const float HandAlong = 0.58f, HandLift = 0.10f, HandDip = 0.08f;

        /// <summary>
        /// 両手を腿の上に楽に置く。場面 6 の主（BuildVillage.RestHands）と同じ置き方で、手首を膝の方へ寄せる（腿の付け根から 6 割）。
        /// 付け根から 3 割の所では、目が向いた時に手が変速の把手の頭（運転席と助手席のあいだ）の陰に入った
        /// </summary>
        static void RestHands(Animator an, bool mirror)
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
            BuildVillage.RelaxFingers(an);
        }

        /// <summary>
        /// 顔を自分の窓（世界の -x）へ回す。首に 4 割、頭に 6 割。世界の上の軸で回す（裏返した模型でも、絵の上で同じ向きに回る）
        /// </summary>
        static void Turn(Animator an, bool mirror)
        {
            var angle = -TwinHeadTurn;
            var neck = an.GetBoneTransform(HumanBodyBones.Neck);
            var head = an.GetBoneTransform(HumanBodyBones.Head);
            if (neck != null) neck.rotation = Quaternion.AngleAxis(angle * 0.4f, Vector3.up) * neck.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(angle * 0.6f, Vector3.up) * head.rotation;
        }

        /// <summary>焼いた体のいちばん高い所（車の座標の y）</summary>
        static float Top(GameObject seated, Transform car)
        {
            var mf = seated.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0f;
            var top = float.MinValue;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var y = car.InverseTransformPoint(seated.transform.TransformPoint(v)).y;
                if (y > top) top = y;
            }
            return top;
        }
    }
}
