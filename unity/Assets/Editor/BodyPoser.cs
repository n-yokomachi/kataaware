using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 主人公の体（Humanoid）に、組み立てのときに姿勢を付ける道具。
    ///
    /// 座った形は、立ちの動きの初めのこまから始めて、腰を座面へ下ろし、背を倒し、
    /// 脚と腕を二関節の IK（<see cref="ArmReach"/>）で狙った所へ届かせて作る。
    /// 狙いはどれも世界の位置で渡すので、椅子や車の座席の寸法から直に書ける。
    /// 解いた形は <see cref="Capture"/> で骨ごとの向きに読み出し、<see cref="SeatedPose"/> へ書く
    /// </summary>
    public static class BodyPoser
    {
        /// <summary>立ちと歩きの Humanoid の動き（RocketboxRetarget が書く）</summary>
        public const string IdleClip = "Assets/Animation/Humanoid/Idle.anim";

        /// <summary>座った形の狙い。位置と向きは世界の値</summary>
        public struct Sit
        {
            /// <summary>腰の骨（Hips）の位置</summary>
            public Vector3 hips;
            /// <summary>骨盤の前後の傾き（度、正で前へ倒れる）</summary>
            public float pelvis;
            /// <summary>背を前へ倒す角（度、正で前。背骨・胸・上の胸へ分けて掛ける）</summary>
            public float lean;
            /// <summary>首と頭で起こし返す割合（0 で背と一緒に倒れたまま、1 で頭は立ったまま）</summary>
            public float headKeep;
            /// <summary>左右の足首の狙い</summary>
            public Vector3 ankleL, ankleR;
            /// <summary>足の甲を下へ向ける角（度、正で爪先が下がる）</summary>
            public float footPoint;
            /// <summary>左右の手首（手の骨）の狙い</summary>
            public Vector3 wristL, wristR;
            /// <summary>左右の手の、指の向きと手のひらの向き（世界の向き。零なら寄せない）</summary>
            public Vector3 fingersL, fingersR, palmL, palmR;
            /// <summary>肘と膝を寄せたい所</summary>
            public Vector3 elbowPoleL, elbowPoleR, kneePoleL, kneePoleR;
        }

        /// <summary>立ちの動きの初めのこまで立たせる（PlayableGraph を一度だけ評価する）</summary>
        public static void Stand(Animator an)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClip);
            if (clip == null) { Debug.LogWarning("立ちの動きが無い: " + IdleClip); return; }
            var graph = PlayableGraph.Create("BodyPoser");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "pose", an);
                var p = AnimationClipPlayable.Create(graph, clip);
                p.SetApplyFootIK(false);
                output.SetSourcePlayable(p);
                p.SetTime(0f);
                p.SetTime(0f);
                graph.Evaluate(0f);
            }
            finally
            {
                graph.Destroy();
            }
        }

        /// <summary>立った形から座った形へ曲げる</summary>
        public static void Pose(Animator an, Sit s)
        {
            var root = an.transform;
            var right = root.right;
            System.Func<HumanBodyBones, Transform> B = an.GetBoneTransform;

            // 腰を座面へ。骨盤を傾ける
            var hips = B(HumanBodyBones.Hips);
            hips.position = s.hips;
            hips.rotation = Quaternion.AngleAxis(s.pelvis, right) * hips.rotation;

            // 背。骨盤で倒したぶんは背骨が受け継ぐので、残りを背骨・胸・上の胸へ分ける
            var rest = s.lean - s.pelvis;
            Turn(B(HumanBodyBones.Spine), right, rest * 0.40f);
            Turn(B(HumanBodyBones.Chest), right, rest * 0.35f);
            Turn(B(HumanBodyBones.UpperChest), right, rest * 0.25f);
            // 首と頭で起こし返す
            Turn(B(HumanBodyBones.Neck), right, -s.lean * s.headKeep * 0.5f);
            Turn(B(HumanBodyBones.Head), right, -s.lean * s.headKeep * 0.5f);

            // 脚。足首を狙いへ。足の向きは立っていたときの世界の向き（床に平ら）に爪先の下げを重ねる
            Leg(an, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, s.ankleL, s.kneePoleL, right, s.footPoint);
            Leg(an, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, s.ankleR, s.kneePoleR, right, s.footPoint);

            // 腕
            Arm(an, true, s.wristL, s.elbowPoleL, s.fingersL, s.palmL);
            Arm(an, false, s.wristR, s.elbowPoleR, s.fingersR, s.palmR);
        }

        static void Turn(Transform t, Vector3 axis, float degrees)
        {
            if (t == null || Mathf.Approximately(degrees, 0f)) return;
            t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
        }

        static void Leg(Animator an, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot, Vector3 ankle, Vector3 pole, Vector3 right, float point)
        {
            var f = an.GetBoneTransform(foot);
            var keep = f.rotation;
            ArmReach.Solve(an.GetBoneTransform(upper), an.GetBoneTransform(lower), f, ankle, pole, 1f);
            f.rotation = Quaternion.AngleAxis(point, right) * keep;
        }

        /// <summary>腕を狙いへ届かせ、手の指と手のひらの向きを寄せる</summary>
        public static void Arm(Animator an, bool left, Vector3 wrist, Vector3 pole, Vector3 fingers, Vector3 palm)
        {
            var upper = an.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = an.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            ArmReach.Solve(upper, lower, hand, wrist, pole, 1f);
            if (fingers.sqrMagnitude > 1e-6f) Aim(an, left, fingers, palm);
            // 手のひらのひねりを前腕と手首に分ける。手の骨だけをひねると手首の肌が絞られて細く潰れる
            ArmReach.Untwist(lower, hand, ArmReach.RestOf(SkinPoint.BodyOf(an), upper, lower, hand), ArmReach.TwistShare);
        }

        /// <summary>
        /// 手の向きを、指（手の骨から中指の付け根へ）が fingers を、手のひらが palm を向くように回す。
        /// 手のひらの向きは、指の向きと、小指の付け根から人差し指の付け根への向きから求める
        /// </summary>
        public static void Aim(Animator an, bool left, Vector3 fingers, Vector3 palm)
        {
            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var f = FingerDir(an, left);
            hand.rotation = Quaternion.FromToRotation(f, fingers.normalized) * hand.rotation;
            if (palm.sqrMagnitude < 1e-6f) return;
            f = FingerDir(an, left);
            var now = PalmDir(an, left);
            var want = Vector3.ProjectOnPlane(palm, f);
            now = Vector3.ProjectOnPlane(now, f);
            if (want.sqrMagnitude < 1e-8f || now.sqrMagnitude < 1e-8f) return;
            var angle = Vector3.SignedAngle(now, want, f);
            hand.rotation = Quaternion.AngleAxis(angle, f) * hand.rotation;
        }

        /// <summary>手の骨から中指の付け根への向き</summary>
        public static Vector3 FingerDir(Animator an, bool left)
        {
            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var mid = an.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            return (mid.position - hand.position).normalized;
        }

        /// <summary>手のひらが向く向き（手のひらの面から外へ）</summary>
        public static Vector3 PalmDir(Animator an, bool left)
        {
            var index = an.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = an.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var across = index.position - little.position;
            var f = FingerDir(an, left);
            return (left ? Vector3.Cross(across, f) : Vector3.Cross(f, across)).normalized;
        }

        // ---- 指 ----------------------------------------------------------------

        /// <summary>指先の点（末節の骨から、中節から末節への向きへ length だけ先）</summary>
        public static Vector3 Tip(Animator an, HumanBodyBones middle, HumanBodyBones distal, float length)
        {
            var m = an.GetBoneTransform(middle).position;
            var d = an.GetBoneTransform(distal).position;
            return d + (d - m).normalized * length;
        }

        /// <summary>
        /// 指の骨一本を、その関節の屈曲の軸（<see cref="FlexAxis"/>）まわりに曲げる（正で握る向き）。
        /// 前は手のひらの向きと骨の向きの外積を軸にしていたが、指ごとの曲げの面とずれていて（人差し指の中の関節で 11°、親指で 30°）、
        /// 曲げるたびにひねりと横倒れが混じって、肌が関節の脇で引き伸ばされた
        /// </summary>
        static void Flex(Animator an, bool left, HumanBodyBones bone, HumanBodyBones next, float degrees)
        {
            var b = an.GetBoneTransform(bone);
            if (b == null) return;
            var axis = FlexAxis(an, bone);
            if (axis.sqrMagnitude > 1e-6f)
            {
                b.localRotation = b.localRotation * Quaternion.AngleAxis(degrees, axis);
                return;
            }
            // 骨組みから軸が読めないとき（Humanoid でない体）だけ、手のひらの向きから決める
            var n = next < HumanBodyBones.LastBone ? an.GetBoneTransform(next) : null;
            var dir = n != null ? (n.position - b.position).normalized
                : b.parent != null ? (b.position - b.parent.position).normalized : FingerDir(an, left);
            var world = Vector3.Cross(dir, PalmDir(an, left));
            if (world.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.AngleAxis(degrees, world.normalized) * b.rotation;
        }

        static readonly Dictionary<Avatar, Dictionary<HumanBodyBones, Vector3>> flexAxes = new Dictionary<Avatar, Dictionary<HumanBodyBones, Vector3>>();
        static readonly Dictionary<Avatar, Dictionary<HumanBodyBones, Vector3>> spreadAxes = new Dictionary<Avatar, Dictionary<HumanBodyBones, Vector3>>();

        /// <summary>
        /// 指の骨の屈曲の軸（骨の枠で見た向き。正に回すと握る）。Humanoid の曲げの筋肉（Stretched）を少し動かして、
        /// 骨がどの軸まわりに回るかを読む。人の手の曲げの面として骨組みが決めている軸なので、ひねりが混じらない。
        /// Rocketbox の指では骨の -z、親指では -y になる
        /// </summary>
        public static Vector3 FlexAxis(Animator an, HumanBodyBones bone)
        {
            ReadAxes(an);
            Dictionary<HumanBodyBones, Vector3> map;
            Vector3 a;
            return an.avatar != null && flexAxes.TryGetValue(an.avatar, out map) && map.TryGetValue(bone, out a) ? a : Vector3.zero;
        }

        /// <summary>指の付け根の骨の、開く・閉じる軸（骨の枠で見た向き。Spread の筋肉を少し動かして読む）</summary>
        public static Vector3 SpreadAxis(Animator an, HumanBodyBones bone)
        {
            ReadAxes(an);
            Dictionary<HumanBodyBones, Vector3> map;
            Vector3 a;
            return an.avatar != null && spreadAxes.TryGetValue(an.avatar, out map) && map.TryGetValue(bone, out a) ? a : Vector3.zero;
        }

        static void ReadAxes(Animator an)
        {
            if (an == null || an.avatar == null || !an.avatar.isHuman || flexAxes.ContainsKey(an.avatar)) return;
            var flex = new Dictionary<HumanBodyBones, Vector3>();
            var spread = new Dictionary<HumanBodyBones, Vector3>();
            var keep = new List<(Transform, Vector3, Quaternion)>();
            foreach (var t in an.GetComponentsInChildren<Transform>(true)) keep.Add((t, t.localPosition, t.localRotation));
            var handler = new HumanPoseHandler(an.avatar, an.transform);
            try
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                for (var b = HumanBodyBones.LeftThumbProximal; b <= HumanBodyBones.RightLittleDistal; b++)
                {
                    var bone = an.GetBoneTransform(b);
                    if (bone == null) continue;
                    foreach (var dof in new[] { 2, 1 })
                    {
                        var muscle = HumanTrait.MuscleFromBone((int)b, dof);
                        if (muscle < 0) continue;
                        var keepValue = pose.muscles[muscle];
                        pose.muscles[muscle] = 0.1f;
                        handler.SetHumanPose(ref pose);
                        var q0 = bone.localRotation;
                        // 曲げの筋肉は値を下げると握る。開く筋肉は値を下げた向きを正にする
                        pose.muscles[muscle] = -0.1f;
                        handler.SetHumanPose(ref pose);
                        var q1 = bone.localRotation;
                        pose.muscles[muscle] = keepValue;
                        handler.SetHumanPose(ref pose);
                        (Quaternion.Inverse(q0) * q1).ToAngleAxis(out var angle, out var axis);
                        if (angle > 180f) axis = -axis;
                        (dof == 2 ? flex : spread)[b] = axis.normalized;
                    }
                }
            }
            finally
            {
                handler.Dispose();
                foreach (var k in keep) { k.Item1.localPosition = k.Item2; k.Item1.localRotation = k.Item3; }
            }
            flexAxes[an.avatar] = flex;
            spreadAxes[an.avatar] = spread;
        }

        /// <summary>ジャックの形（<see cref="BuildProps.BuildJack"/>）。根元からの高さ（m）と、そこでの半径（m）。先のピン・座金・胴・細る尻の順</summary>
        static readonly Vector2[] JackProfile =
        {
            new Vector2(BuildProps.JackPinTip, BuildProps.JackPinRadius * 0.6f), new Vector2(BuildProps.JackPinTip + 0.001f, BuildProps.JackPinRadius),
            new Vector2(-0.002f, BuildProps.JackPinRadius),
            new Vector2(-0.002f, 0.0115f), new Vector2(0.0035f, BuildProps.JackWasherRadius), new Vector2(0.0050f, 0.0092f),
            new Vector2(0.0050f, 0.0088f), new Vector2(0.0165f, 0.0086f), new Vector2(0.0185f, 0.0062f),
            new Vector2(0.0185f, 0.0058f), new Vector2(0.0300f, 0.0040f),
        };

        /// <summary>ジャックの、根元から z（m）の高さでの半径（m）。胴の外は 0</summary>
        public static float JackRadius(float z)
        {
            if (z < JackProfile[0].x || z > JackProfile[JackProfile.Length - 1].x) return 0f;
            var r = 0f;
            for (var i = 0; i + 1 < JackProfile.Length; i++)
            {
                var a = JackProfile[i];
                var b = JackProfile[i + 1];
                if (z < a.x || z > b.x || b.x - a.x < 1e-6f) continue;
                r = Mathf.Max(r, Mathf.Lerp(a.y, b.y, (z - a.x) / (b.x - a.x)));
            }
            return r;
        }

        /// <summary>
        /// 点 p（世界）から、ジャック（jack の枠。前 +Z が根元から尻へ）の面までの距離（m、中なら負）。
        /// 胴の脇は面までの横の距離、両端より先は端の面までの距離で測る
        /// </summary>
        public static float JackSurface(Transform jack, Vector3 p)
        {
            return JackSurface(jack.InverseTransformPoint(p) * jack.lossyScale.x);
        }

        /// <summary>ジャックの枠で見た点 local（m）から、ジャックの面までの距離（m、中なら負）</summary>
        public static float JackSurface(Vector3 local)
        {
            var r = new Vector2(local.x, local.y).magnitude;
            var z0 = JackProfile[0].x;
            var z1 = JackProfile[JackProfile.Length - 1].x;
            var z = Mathf.Clamp(local.z, z0, z1);
            var side = r - JackRadius(z);
            var off = Mathf.Abs(local.z - z);
            if (off <= 0f) return side <= 0f ? Mathf.Max(side, Mathf.Max(z0 - local.z, local.z - z1)) : side;
            return Mathf.Sqrt(Mathf.Max(side, 0f) * Mathf.Max(side, 0f) + off * off);
        }

        /// <summary>指先の長さ（末節の骨の付け根から指先まで）。Rocketbox の女性の手で測った値</summary>
        public const float TipLength = 0.020f;

        /// <summary>
        /// つまむ所の、ジャックの根元からの高さ（m）。胴のまっすぐな所（根元から 5〜16.5 mm）の尻寄り。
        /// 前は 20 mm（胴と細る尻の境）で、指の腹が細る所に掛かって浮いた。12 mm では、手首の肌に刺さったジャックを摘まむと
        /// 人差し指の先が右の手首の肌へ潜った
        /// </summary>
        public const float GripAlong = 0.015f;

        /// <summary>
        /// 指の腹の肌とジャックの胴の面の隙間（m、片側）。0.2 mm の内で合わせるので、肌は面から 0〜0.4 mm に来る。
        /// 画面では触れて見え、肌の頂点はジャックに潜らない
        /// </summary>
        public const float PinchGap = 0.0002f;

        /// <summary>
        /// つまむ形の指の曲げ（度。付け根・中・先、指をまっすぐに伸ばした形から）。人差し指・中指・薬指・小指の順。
        /// 人差し指は腹が親指の側を向くまで曲げる。この体の親指は付け根から指先まで 8 cm 余りと短く、
        /// 人差し指の曲げが浅い（付け根 40° 以下）と、指先が親指の届く所より先へ出て、腹どうしが向き合わない。
        /// 中指は人差し指に添う（人差し指より少し浅く曲げる。深く曲げると、手首のジャックを掴んだとき先が右の手首に潜った）。
        /// 薬指・小指はゆるく曲げる（固い拳にしない）
        /// </summary>
        public static readonly Vector3[] PinchCurl =
        {
            new Vector3(50f, 55f, 20f),
            new Vector3(45f, 50f, 20f),
            new Vector3(45f, 55f, 25f),
            new Vector3(50f, 55f, 25f),
        };

        /// <summary>つまむ形の親指の先の関節の曲げ（度、まっすぐから）。腹を人差し指へ向ける</summary>
        public static float PinchThumbTip = 20f;

        /// <summary>つまむとき、ジャックに接する二つの末節のほかの節（人差し指の付け根と中節、中指、親指の中節）を、ジャックの面から離す距離（m）</summary>
        public const float PinchClear = 0.001f;

        /// <summary>つまむ前に、人差し指・薬指・小指を中指の向きへ寄せる割合（<see cref="Close"/>）。立ちの形の指は開いている</summary>
        public static float PinchClose = 0.6f;

        /// <summary>
        /// 左手（left）または右手を、親指と人差し指の腹でジャックの胴を挟む形にする。中指は人差し指に添い、薬指・小指はゆるく曲げる。
        /// 挟んだジャックの置き所（根元）を、手の骨の向きの枠で見た位置と向きで返す。
        /// ジャックの向きは、前（+Z、根元から尻へ）が手のひらへ向かう向き、上（+Y）が指の向き
        /// </summary>
        public static void Pinch(Animator an, bool left, out Vector3 holdLocal, out Quaternion holdRotation)
        {
            Pinch(an, left, PinchGap, 1f, out holdLocal, out holdRotation);
        }

        /// <summary>
        /// つまむ形を、指の腹の肌とジャックの面の隙間 gap（m）と、人差し指の曲げの割合 curlScale を変えて作る。
        ///
        /// 人差し指・中指・薬指・小指は <see cref="PinchCurl"/> の曲げ（人差し指だけ curlScale を掛ける）。
        /// 親指は付け根の関節の二つの軸（曲げと開き、骨組みから読んだ軸）と、向かい合わせに要る付け根の骨の軸まわりの回し（30° まで）、
        /// 中の関節の曲げで寄せ、親指の腹が人差し指の腹へ向かい、二本の指の肌（中節と末節）がどちらもジャックの胴の面に接する所を探す（<see cref="Seat"/>）。
        /// 前は骨の先の点どうしの隙間で測っていて、肌の厚みが指ごとに違うので、人差し指の腹がジャックに 5 mm 潜り、親指の腹は 5 mm 浮いた
        /// </summary>
        public static void Pinch(Animator an, bool left, float gap, float curlScale, out Vector3 holdLocal, out Quaternion holdRotation)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var fingers = new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal) },
                new[] { F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal) },
            };
            Close(an, left, PinchClose);
            for (var i = 0; i < fingers.Length; i++)
            {
                var f = fingers[i];
                // 曲げは、指をまっすぐに伸ばした形から数える。前は立ちの形の少し曲がった指の上に重ねて曲げていて、
                // 人差し指の先の節が手首の側を向くまで曲がり（曲げの和が 180° 近く）、腹が手の甲の側を向いた
                foreach (var b in f) Unbend(an, b);
                // 人差し指だけ割合を掛ける
                var k = i < 1 ? curlScale : 1f;
                Flex(an, left, f[0], f[1], PinchCurl[i].x * k);
                Flex(an, left, f[1], f[2], PinchCurl[i].y * k);
                Flex(an, left, f[2], HumanBodyBones.LastBone, PinchCurl[i].z * k);
            }
            var tp = F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal);
            var ti = F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate);
            var td = F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal);
            var indexEnd = F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal);
            // 親指は付け根（曲げと開き）と中の関節の曲げで寄せる。中と先の関節は、まっすぐに伸ばした形から数える。
            // 先の関節は軽く曲げる（腹を人差し指へ向ける）
            Unbend(an, ti);
            Unbend(an, td);
            Flex(an, left, td, HumanBodyBones.LastBone, PinchThumbTip);
            var root = an.GetBoneTransform(tp);
            var mid = an.GetBoneTransform(ti);
            var thumbEnd = an.GetBoneTransform(td);
            var indexBone = an.GetBoneTransform(indexEnd);
            // 付け根は骨組みの基の姿勢（T の字）から探す。今の姿勢から探すと、呼ぶ前の手の形で選ぶ親指が変わった
            var reference = Straight(an, tp);
            if (reference.HasValue) root.localRotation = reference.Value;
            var keep = root.localRotation;
            var keepMid = mid.localRotation;
            var flexAxis = FlexAxis(an, tp);
            var spreadAxis = SpreadAxis(an, tp);
            var midAxis = FlexAxis(an, ti);
            var indexPad = Pad(an, indexEnd);
            // 肌の点（骨の枠）。肌は骨と一緒に動くとみなす。
            // ジャックに接するのは親指と人差し指の末節の肌だけ。ほかの節（人差し指の付け根と中節、中指、親指の中節）は離しておく
            var skin = SkinOf(FirstSkin(an), new[] { fingers[0], fingers[1], new[] { ti, td } }, an);
            var indexSkin = new List<Vector3>();
            Gather(skin, indexBone, indexSkin);
            var others = new List<Vector3>();
            foreach (var f in new[] { fingers[0][0], fingers[0][1], fingers[1][0], fingers[1][1], fingers[1][2] })
                Gather(skin, an.GetBoneTransform(f), others);
            var thumbSkin = new List<Vector3>();
            var thumbMid = new List<Vector3>();
            // 二本の指の腹の点。ジャックはこの二点を結ぶ線に直交させる
            var indexPoint = indexBone.localToWorldMatrix.MultiplyPoint3x4(PadPoint(skin[indexBone], indexBone, indexPad));
            var thumbLocal = PadPoint(skin[thumbEnd], thumbEnd, Pad(an, td));
            var palm = PalmDir(an, left);
            var grip = 2f * JackRadius(GripAlong);
            // 親指の付け根の長さの軸。向かい合わせ（対立）では付け根の骨が軸まわりに 20° 前後回るので、30° まで許す
            var along = root.childCount > 0 ? root.GetChild(0).localPosition.normalized : Vector3.right;
            // 曲げ（中の関節）・ひねり・開き・曲げ（付け根）の四つの角を、粗い刻みで探してから、いちばんよい所のまわりを細かく探す
            var pick = new Vector4(20f, 0f, 0f, 10f);
            var span = new Vector4(20f, 30f, 60f, 50f);
            var stepSize = new Vector4(10f, 10f, 8f, 8f);
            var bestCost = float.MaxValue;
            for (var round = 0; round < 4; round++)
            {
                var centre = pick;
                for (var bend = centre.x - span.x; bend <= centre.x + span.x + 1e-3f; bend += stepSize.x)
                {
                    if (bend < 0f || bend > 45f) continue;
                    mid.localRotation = keepMid * Quaternion.AngleAxis(bend, midAxis);
                    for (var tw = centre.y - span.y; tw <= centre.y + span.y + 1e-3f; tw += stepSize.y)
                    {
                        if (Mathf.Abs(tw) > 30f) continue;
                        for (var sp = centre.z - span.z; sp <= centre.z + span.z + 1e-3f; sp += stepSize.z)
                            for (var fl = centre.w - span.w; fl <= centre.w + span.w + 1e-3f; fl += stepSize.w)
                            {
                                if (fl < -40f || fl > 60f || Mathf.Abs(sp) > 60f) continue;
                                root.localRotation = keep * Quaternion.AngleAxis(sp, spreadAxis) * Quaternion.AngleAxis(fl, flexAxis) * Quaternion.AngleAxis(tw, along);
                                var thumbPoint = thumbEnd.localToWorldMatrix.MultiplyPoint3x4(thumbLocal);
                                // 腹どうしがジャックの太さから大きく外れる所は、肌で測るまでもない
                                if (Mathf.Abs(Vector3.Distance(thumbPoint, indexPoint) - grip) > 0.008f) continue;
                                var toIndex = (indexPoint - thumbPoint).normalized;
                                thumbSkin.Clear();
                                Gather(skin, thumbEnd, thumbSkin);
                                thumbMid.Clear();
                                Gather(skin, mid, thumbMid);
                                Vector3 at, axis;
                                float touch;
                                Seat(indexSkin, thumbSkin, thumbPoint, indexPoint, palm, out at, out axis, out touch);
                                var clear = Mathf.Min(Clearance(others, at, axis, toIndex), Clearance(thumbMid, at, axis, toIndex));
                                // 二本の指の末節の肌は、どちらもジャックの胴の面から gap の所で接する（0.2 mm の内。挟む幅なので外せない）。
                                // ほかの節は PinchClear より離す。
                                // 親指の腹が人差し指の腹へ、人差し指の腹が親指の腹へ向き合うほどよい。付け根を無理に回さないよう、回しの大きさも少し数える
                                var miss = Mathf.Abs(touch - gap);
                                var cost = (miss > 0.0002f ? 1f + miss : 0f)
                                    + (clear < PinchClear ? 1f + (PinchClear - clear) : 0f)
                                    + 0.5f * (1f - Vector3.Dot(Pad(an, td), toIndex))
                                    + 0.5f * (1f - Vector3.Dot(indexPad, -toIndex))
                                    + (Mathf.Abs(sp) + Mathf.Abs(fl) + Mathf.Abs(tw) + bend) * 0.0005f;
                                if (cost < bestCost) { bestCost = cost; pick = new Vector4(bend, tw, sp, fl); }
                            }
                    }
                }
                span = stepSize;
                stepSize *= 0.5f;
            }
            mid.localRotation = keepMid * Quaternion.AngleAxis(pick.x, midAxis);
            // 刻みの上では隙間が 0.2 mm の内に入らないことがある（付け根を 1° 回すと指先は 1.4 mm 動く）。
            // 付け根の曲げだけを、選んだ角の前後 4° の内で二分に詰め、隙間をちょうど gap に合わせる
            System.Func<float, float> touchAt = fl =>
            {
                root.localRotation = keep * Quaternion.AngleAxis(pick.z, spreadAxis) * Quaternion.AngleAxis(fl, flexAxis) * Quaternion.AngleAxis(pick.y, along);
                thumbSkin.Clear();
                Gather(skin, thumbEnd, thumbSkin);
                Vector3 at, axis;
                float touch;
                Seat(indexSkin, thumbSkin, thumbEnd.localToWorldMatrix.MultiplyPoint3x4(thumbLocal), indexPoint, palm, out at, out axis, out touch);
                return touch - gap;
            };
            float lo = pick.w - 4f, hi = pick.w + 4f;
            var gLo = touchAt(lo);
            var gHi = touchAt(hi);
            if (gLo * gHi < 0f)
            {
                for (var i = 0; i < 20; i++)
                {
                    var m = (lo + hi) * 0.5f;
                    var g = touchAt(m);
                    if (g * gLo > 0f) { lo = m; gLo = g; } else hi = m;
                }
                pick.w = (lo + hi) * 0.5f;
            }
            root.localRotation = keep * Quaternion.AngleAxis(pick.z, spreadAxis) * Quaternion.AngleAxis(pick.w, flexAxis) * Quaternion.AngleAxis(pick.y, along);
            thumbSkin.Clear();
            Gather(skin, thumbEnd, thumbSkin);
            var thumbAt = thumbEnd.localToWorldMatrix.MultiplyPoint3x4(thumbLocal);
            Vector3 centreAt, forward;
            float contact;
            // ジャックの尻（ケーブルの出る側）は手のひらへ、根元は手のひらから外へ向ける。
            // 上から摘まんで手の甲の側へ動かせば、ジャックの向きに沿って抜ける
            Seat(indexSkin, thumbSkin, thumbAt, indexPoint, palm, out centreAt, out forward, out contact);
            var hand = an.GetBoneTransform(F(HumanBodyBones.LeftHand, HumanBodyBones.RightHand));
            // ジャックの横（X）は親指の腹と人差し指の腹を結ぶ線、上（Y）は指の向きの側。
            // X の軸まわりに傾けても、二つの腹はジャックに接したまま（傾きは JackHoldRoll が選ぶ）
            var across = (indexPoint - thumbAt).normalized;
            var up = Vector3.Cross(forward, across).normalized;
            if (Vector3.Dot(up, FingerDir(an, left)) < 0f) up = -up;
            var world = Quaternion.LookRotation(forward, up);
            holdRotation = Quaternion.Inverse(hand.rotation) * world;
            // 指の腹が挟むのはジャックの胴。置き所（ジャックの根元）を、挟む所の高さの分だけ根元へ下げる
            holdLocal = Quaternion.Inverse(hand.rotation) * (centreAt - forward * GripAlong - hand.position);
        }

        /// <summary>骨 bone に付いた肌の点（骨の枠）を、今の骨の向きで世界の点にして into へ加える</summary>
        static void Gather(Dictionary<Transform, List<Vector3>> skin, Transform bone, List<Vector3> into)
        {
            List<Vector3> list;
            if (bone == null || !skin.TryGetValue(bone, out list)) return;
            var m = bone.localToWorldMatrix;
            foreach (var p in list) into.Add(m.MultiplyPoint3x4(p));
        }

        /// <summary>
        /// 親指の腹 thumbPad と人差し指の腹 indexPad の間に、ジャックを挟ませる。ジャックの軸は二点を結ぶ線に直交させ、
        /// その中で手のひらの逆（palm の逆）へいちばん近い向きにする。芯は二点を結ぶ線の上で、
        /// 二本の指の肌（index・thumb）のジャックの面までのいちばん近い距離が等しくなる所に置く。
        /// 挟む所（<see cref="GripAlong"/>）の芯 at、軸 axis と、そのときの面までの距離 touch（m、中なら負）を返す
        /// </summary>
        static void Seat(List<Vector3> index, List<Vector3> thumb, Vector3 thumbPad, Vector3 indexPad, Vector3 palm,
            out Vector3 at, out Vector3 axis, out float touch)
        {
            var across = (indexPad - thumbPad).normalized;
            axis = Vector3.ProjectOnPlane(-palm, across).normalized;
            var side = Vector3.Cross(axis, across);
            var middle = (thumbPad + indexPad) * 0.5f;
            float lo = -0.012f, hi = 0.012f;
            float toIndex = 0f, toThumb = 0f;
            at = middle;
            for (var i = 0; i < 14; i++)
            {
                var d = (lo + hi) * 0.5f;
                at = middle + across * d;
                var rootAt = at - axis * GripAlong;
                toIndex = Nearest(index, rootAt, across, side, axis);
                toThumb = Nearest(thumb, rootAt, across, side, axis);
                // 芯を人差し指へ寄せるほど、人差し指の肌は面に近づき、親指の肌は離れる
                if (toIndex > toThumb) lo = d; else hi = d;
            }
            touch = Mathf.Min(toIndex, toThumb);
        }

        /// <summary>点の組 points の、挟む所の芯 at・軸 axis のジャック（横 across は軸に直交させて使う）の面までのいちばん近い距離（m、中なら負）</summary>
        static float Clearance(List<Vector3> points, Vector3 at, Vector3 axis, Vector3 across)
        {
            var x = Vector3.ProjectOnPlane(across, axis).normalized;
            return Nearest(points, at - axis * GripAlong, x, Vector3.Cross(axis, x), axis);
        }

        /// <summary>点の組 points の、ジャック（根元 rootAt、枠の三軸 x・y・z）の面までのいちばん近い距離（m、中なら負）</summary>
        static float Nearest(List<Vector3> points, Vector3 rootAt, Vector3 x, Vector3 y, Vector3 z)
        {
            var best = float.MaxValue;
            foreach (var p in points)
            {
                var d = p - rootAt;
                best = Mathf.Min(best, JackSurface(new Vector3(Vector3.Dot(d, x), Vector3.Dot(d, y), Vector3.Dot(d, z))));
            }
            return best;
        }

        /// <summary>体の肌（頭の影だけを落とすものを除く）</summary>
        static SkinnedMeshRenderer FirstSkin(Animator an)
        {
            foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && !SkinPoint.Rides(smr)) return smr;
            return null;
        }

        /// <summary>
        /// 指の末節の腹の点（骨の枠）。末節の肌の点のうち、骨の長さの 3〜9 割の所で、腹の向き pad（世界）へいちばん出た三つの平均
        /// </summary>
        static Vector3 PadPoint(List<Vector3> points, Transform distal, Vector3 pad)
        {
            var along = distal.localPosition.normalized;
            var padLocal = Quaternion.Inverse(distal.rotation) * pad;
            var scale = distal.lossyScale.x;
            var picked = new List<KeyValuePair<float, Vector3>>();
            foreach (var p in points)
            {
                var s = Vector3.Dot(p, along) * scale;
                if (s < TipLength * 0.3f || s > TipLength * 0.9f) continue;
                picked.Add(new KeyValuePair<float, Vector3>(Vector3.Dot(p, padLocal), p));
            }
            if (picked.Count == 0) return Vector3.zero;
            picked.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sum = Vector3.zero;
            var n = Mathf.Min(3, picked.Count);
            for (var i = 0; i < n; i++) sum += picked[i].Value;
            return sum / n;
        }

        /// <summary>指の末節の腹が向く向き（その関節を握る向きへ曲げたときに、指先が動く向き）</summary>
        static Vector3 Pad(Animator an, HumanBodyBones distal)
        {
            var d = an.GetBoneTransform(distal);
            var axis = d.rotation * FlexAxis(an, distal);
            var tip = BoneTip(an, distal);
            return Vector3.Cross(axis, tip - d.position).normalized;
        }

        /// <summary>親指の先の点</summary>
        static Vector3 ThumbTip(Animator an, bool left)
        {
            return BoneTip(an, left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
        }

        /// <summary>
        /// 指の先の点。末節の骨の向き（中節の中で末節が置かれた向きを、末節の向きで回したもの）へ指先の長さだけ先。
        /// <see cref="Tip"/> は中節から末節への向きで伸ばすので、末節だけを曲げても動かない
        /// </summary>
        public static Vector3 BoneTip(Animator an, HumanBodyBones distal)
        {
            var d = an.GetBoneTransform(distal);
            return d.position + d.rotation * d.localPosition.normalized * TipLength;
        }

        /// <summary>
        /// つまむ形（<see cref="Pinch(Animator, bool, out Vector3, out Quaternion)"/> の後に呼ぶ）から、親指と人差し指を
        /// つまむ所から離す向きへ開く。親指は付け根の開く軸まわりに人差し指から遠ざけて thumbAway 度、人差し指は曲げを indexOpen 度戻す。
        /// 開いた指の先は、つまむ所をはさんで両側に離れるので、手をジャックへ寄せても指先がジャックを横切らない
        /// </summary>
        public static void Open(Animator an, bool left, float thumbAway, float indexOpen)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var tp = F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal);
            var root = an.GetBoneTransform(tp);
            var tipI = BoneTip(an, F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal));
            var axis = SpreadAxis(an, tp);
            if (axis.sqrMagnitude > 1e-6f)
            {
                // 開く軸のどちら向きが人差し指から遠ざかるかを試して決める
                var keep = root.localRotation;
                root.localRotation = keep * Quaternion.AngleAxis(thumbAway, axis);
                var plus = Vector3.Distance(ThumbTip(an, left), tipI);
                root.localRotation = keep * Quaternion.AngleAxis(-thumbAway, axis);
                var minus = Vector3.Distance(ThumbTip(an, left), tipI);
                root.localRotation = keep * Quaternion.AngleAxis(plus >= minus ? thumbAway : -thumbAway, axis);
            }
            // 人差し指は、つまむ向きの曲げを戻す
            Flex(an, left, F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), -indexOpen * 0.5f);
            Flex(an, left, F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal), -indexOpen * 0.5f);
        }

        /// <summary>
        /// 四本の指と親指を、手のひらの側へそろえて曲げる。curl は下の角の割合で、0.3 ほどで力の抜けた手、
        /// 2 を越えると四本の指は関節の限りまで曲がり切る。場面 8 の腕組みに使う（物を握る手は <see cref="Wrap"/>）
        /// </summary>
        public static void Grip(Animator an, bool left, float curl)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var fingers = new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal) },
                new[] { F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal) },
                new[] { F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal), F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal) },
            };
            // 付け根・中・先の曲げ（度）。小指の側ほど深く、親指は浅く
            var bend = new[]
            {
                new[] { 45f, 55f, 30f },
                new[] { 50f, 60f, 35f },
                new[] { 55f, 65f, 35f },
                new[] { 60f, 65f, 35f },
                new[] { 10f, 20f, 20f },
            };
            // 人の関節が曲がる所まで（付け根・中・先）。握り込んでもこれより深くは曲げない。
            // 越えると指の肌が関節で潰れて、拳が崩れて見える
            var most = new[] { 85f, 100f, 70f };
            for (var i = 0; i < fingers.Length; i++)
            {
                var f = fingers[i];
                Flex(an, left, f[0], f[1], Mathf.Min(bend[i][0] * curl, most[0]));
                Flex(an, left, f[1], f[2], Mathf.Min(bend[i][1] * curl, most[1]));
                Flex(an, left, f[2], HumanBodyBones.LastBone, Mathf.Min(bend[i][2] * curl, most[2]));
            }
        }

        /// <summary>
        /// 人差し指・薬指・小指を、中指の向きへ寄せてそろえる（付け根の関節を手のひらの面の中で回す）。
        /// amount は寄せる割合で、1 で中指と同じ向き。立ちの形の指は開いているので、握る前にそろえる
        /// </summary>
        public static void Close(Animator an, bool left, float amount)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var palm = PalmDir(an, left);
            System.Func<HumanBodyBones, HumanBodyBones, Vector3> dir = (a, b) =>
                (an.GetBoneTransform(b).position - an.GetBoneTransform(a).position).normalized;
            var mid = dir(F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate));
            var others = new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate) },
            };
            foreach (var o in others)
            {
                var bone = an.GetBoneTransform(o[0]);
                var local = SpreadAxis(an, o[0]);
                // 開く・閉じる軸（骨組みから読んだ軸）まわりに、中指の向きへ寄せる。軸が読めなければ手のひらの向きのまわり
                var axis = local.sqrMagnitude > 1e-6f ? bone.rotation * local : palm;
                var angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(dir(o[0], o[1]), axis), Vector3.ProjectOnPlane(mid, axis), axis);
                bone.rotation = Quaternion.AngleAxis(angle * amount, axis) * bone.rotation;
            }
        }

        /// <summary>指が物に触れたとみなす、肌から物の面までの隙間（m）</summary>
        public const float TouchGap = 0.001f;

        /// <summary>
        /// 四本の指と親指を、物の面に沿わせて曲げる。指ごとに、付け根・中・先の関節を 3 度ずつ順に曲げていき、
        /// 曲げるとその関節から先の節の肌が物の面に触れる（隙間が <see cref="TouchGap"/> を切る）関節はそこで待つ。
        /// ほかの関節が動いて空きができればまた曲げ、どの関節も動けなくなったら止める。手前の節が物に乗り、
        /// 先の節が物の向こう側へ回り込む。物に触れないまま曲がる関節は most（付け根・中・先、度。親指は thumbMost）で止め、
        /// 関節の限りまで曲げた鉤爪にしない。置いただけで肌が物に食い込んでいる節は、先に離れるまで伸ばす。
        /// clearance は点から物の面までの距離（中なら負）。log を渡すと関節ごとの曲げ（度、* は物に触れて止まった）を書く。
        ///
        /// 触れたかは骨の芯ではなく肌の頂点で測る。Rocketbox の指の骨は肌の芯から指先の側へ 1〜2 cm ずれているので、
        /// 骨の芯で測ると肌が物に食い込む。頂点は、いちばん重みの大きい骨にだけ付いているとみなして動かす
        /// </summary>
        public static void Wrap(Animator an, SkinnedMeshRenderer skin, bool left, System.Func<Vector3, float> clearance, Vector3 most, float thumbMost, System.Text.StringBuilder log = null, bool straighten = false)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var fingers = new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal) },
                new[] { F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal) },
                new[] { F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal), F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal) },
            };
            var points = SkinOf(skin, fingers, an);
            const float step = 3f;
            for (var i = 0; i < fingers.Length; i++)
            {
                var f = fingers[i];
                var chain = new Transform[3];
                for (var k = 0; k < 3; k++) chain[k] = an.GetBoneTransform(f[k]);
                var thumb = i == 4;
                var cap = thumb ? new Vector3(thumbMost, thumbMost, thumbMost) : most;
                var turned = new float[3];
                // 伸ばしてから握る: 四本の指を骨組みの基の姿勢（指がまっすぐな T の字）の曲げまで戻す。
                // 立ちの形の指は少し曲がっていて、置いた途端に指先が物の手前に触れ、付け根が曲がれない
                if (straighten && !thumb)
                    for (var j = 0; j < 3; j++) Unbend(an, f[j]);
                // 初めから物に食い込んでいる節は、離れるまで伸ばす（立ちの形の指は少し曲がっているので、置いただけで先が食い込むことがある）
                for (var j = 0; j < 3; j++)
                    for (var opened = 0f; opened < 60f && Touches(points, chain, j, j, clearance, 0f); opened += step)
                    {
                        Flex(an, left, f[j], Next(f, j), -step);
                        turned[j] -= step;
                    }
                // 三つの関節を 3 度ずつ順に曲げる。曲げると先の節が物に触れる関節はそこで待ち、ほかの関節が動いて空きができればまた曲げる。
                // どの関節も動けなくなるまで回すと、手前の節が物に乗り、先の節が物の向こう側へ回り込む
                var moved = true;
                while (moved)
                {
                    moved = false;
                    for (var j = 0; j < 3; j++)
                    {
                        if (turned[j] + step > cap[j]) continue;
                        Flex(an, left, f[j], Next(f, j), step);
                        if (Touches(points, chain, j, 2, clearance, TouchGap)) { Flex(an, left, f[j], Next(f, j), -step); continue; }
                        turned[j] += step;
                        moved = true;
                    }
                }
                if (log == null) continue;
                for (var j = 0; j < 3; j++)
                    log.AppendFormat("{0}:{1}{2} ", f[j].ToString().Replace("Right", "").Replace("Left", ""), turned[j], turned[j] + step > cap[j] ? "" : "*");
            }
        }

        /// <summary>骨組みの基の姿勢（取り込みで決めた T の字。指はまっすぐ）での、骨の親から見た向き。無ければ null</summary>
        public static Quaternion? Straight(Animator an, HumanBodyBones bone)
        {
            var t = an.GetBoneTransform(bone);
            if (t == null || an.avatar == null) return null;
            foreach (var sk in an.avatar.humanDescription.skeleton)
                if (sk.name == t.name) return sk.rotation;
            return null;
        }

        /// <summary>
        /// 指の骨 bone の曲げ（屈曲の軸まわりの回し）を、骨組みの基の姿勢（指がまっすぐな T の字）の曲げまで戻す。開き（横の回し）はそのまま
        /// </summary>
        public static void Unbend(Animator an, HumanBodyBones bone)
        {
            var t = an.GetBoneTransform(bone);
            var straight = Straight(an, bone);
            if (t == null || !straight.HasValue) return;
            var ax = FlexAxis(an, bone);
            var bend = BendAbout(Quaternion.Inverse(straight.Value) * t.localRotation, ax);
            t.localRotation = t.localRotation * Quaternion.AngleAxis(-bend, ax);
        }

        /// <summary>回し d のうち、軸 axis まわりの回しの角（度、符号つき）。振りとひねりに分けたときのひねりの側</summary>
        public static float BendAbout(Quaternion d, Vector3 axis)
        {
            var p = Vector3.Project(new Vector3(d.x, d.y, d.z), axis);
            var twist = new Quaternion(p.x, p.y, p.z, d.w);
            var m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m < 1e-9f) return 0f;
            twist = new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
            twist.ToAngleAxis(out var angle, out var ax);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(ax, axis) >= 0f ? angle : -angle;
        }

        static HumanBodyBones Next(HumanBodyBones[] finger, int j)
        {
            return j < 2 ? finger[j + 1] : HumanBodyBones.LastBone;
        }

        /// <summary>指の骨ごとの、肌の頂点（骨の枠で見た位置）</summary>
        static Dictionary<Transform, List<Vector3>> SkinOf(SkinnedMeshRenderer skin, HumanBodyBones[][] fingers, Animator an)
        {
            var points = new Dictionary<Transform, List<Vector3>>();
            foreach (var f in fingers)
                foreach (var b in f)
                {
                    var t = an.GetBoneTransform(b);
                    if (t != null && !points.ContainsKey(t)) points[t] = new List<Vector3>();
                }
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var bind = mesh.bindposes;
            var verts = mesh.vertices;
            var weights = mesh.boneWeights;
            for (var i = 0; i < verts.Length; i++)
            {
                var bi = weights[i].boneIndex0;
                List<Vector3> list;
                if (bi < bones.Length && bones[bi] != null && points.TryGetValue(bones[bi], out list))
                    list.Add(bind[bi].MultiplyPoint3x4(verts[i]));
            }
            return points;
        }

        /// <summary>指の節 from から to までの肌のどこかが、物の面まで gap を切っているか</summary>
        static bool Touches(Dictionary<Transform, List<Vector3>> skin, Transform[] chain, int from, int to, System.Func<Vector3, float> clearance, float gap)
        {
            for (var k = from; k <= to; k++)
            {
                List<Vector3> list;
                if (chain[k] == null || !skin.TryGetValue(chain[k], out list)) continue;
                var m = chain[k].localToWorldMatrix;
                foreach (var p in list)
                    if (clearance(m.MultiplyPoint3x4(p)) < gap) return true;
            }
            return false;
        }

        /// <summary>手の指の骨の、親から見た向き（つまむ形などを読み出す）</summary>
        public static SeatedPose.Bone[] Fingers(Animator an, bool left)
        {
            var list = new List<SeatedPose.Bone>();
            var first = left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            var last = left ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal;
            for (var b = first; b <= last; b++)
            {
                var t = an.GetBoneTransform(b);
                if (t == null) continue;
                list.Add(new SeatedPose.Bone { bone = b, position = t.localPosition, rotation = t.localRotation });
            }
            return list.ToArray();
        }

        /// <summary>今の姿勢を、骨ごとの親から見た向き（腰は位置も）として読み出す</summary>
        public static SeatedPose.Bone[] Capture(Animator an)
        {
            var list = new List<SeatedPose.Bone>();
            for (var b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
            {
                var t = an.GetBoneTransform(b);
                if (t == null) continue;
                list.Add(new SeatedPose.Bone { bone = b, position = t.localPosition, rotation = t.localRotation });
            }
            return list.ToArray();
        }

        /// <summary>SeatedPose の座った形（seated）か二つ目の形（alternate）へ書く</summary>
        public static void Write(SeatedPose pose, string field, SeatedPose.Bone[] bones)
        {
            var so = new SerializedObject(pose);
            var p = so.FindProperty(field);
            p.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("bone").intValue = (int)bones[i].bone;
                e.FindPropertyRelative("position").vector3Value = bones[i].position;
                e.FindPropertyRelative("rotation").quaternionValue = bones[i].rotation;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>両目の真ん中（目の玉の骨の中点）。Humanoid に目の骨が無い模型は名前で探す</summary>
        public static Vector3 Eyes(Animator an)
        {
            var l = an.GetBoneTransform(HumanBodyBones.LeftEye);
            var r = an.GetBoneTransform(HumanBodyBones.RightEye);
            if (l == null || r == null)
            {
                foreach (var t in an.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Bip01 LEye") l = t;
                    else if (t.name == "Bip01 REye") r = t;
                }
            }
            if (l == null || r == null) return an.GetBoneTransform(HumanBodyBones.Head).position;
            return (l.position + r.position) * 0.5f;
        }
    }
}
