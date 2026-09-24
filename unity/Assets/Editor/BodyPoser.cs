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
            ArmReach.Untwist(lower, hand, ArmReach.RestOf(an.GetComponentInChildren<SkinnedMeshRenderer>(), upper, lower, hand), ArmReach.TwistShare);
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

        /// <summary>指の骨一本を、手のひらの側へ曲げる（正で握る向き）</summary>
        static void Flex(Animator an, bool left, HumanBodyBones bone, HumanBodyBones next, float degrees)
        {
            var b = an.GetBoneTransform(bone);
            var n = next < HumanBodyBones.LastBone ? an.GetBoneTransform(next) : null;
            if (b == null) return;
            // 末節は次の骨が無いので、中節から末節への向きを使う
            var dir = n != null ? (n.position - b.position).normalized
                : b.parent != null ? (b.position - b.parent.position).normalized : FingerDir(an, left);
            var palm = PalmDir(an, left);
            var axis = Vector3.Cross(dir, palm);
            if (axis.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * b.rotation;
        }

        /// <summary>指先の長さ（末節の骨の付け根から指先まで）。Rocketbox の女性の手で測った値</summary>
        public const float TipLength = 0.020f;

        /// <summary>つまむ所の、ジャックの根元からの高さ（m）。胴の尻寄り（根元から 5〜18.5 mm の胴と、細る尻の境）。根元寄りをつまむと、握り込んだ指が手首の皮膚に届いた</summary>
        public const float GripAlong = 0.020f;

        /// <summary>つまむ隙間（親指の腹と人差し指の腹の間）。ジャックの胴の太さ 17.6 mm に、腹の厚みを加えた</summary>
        public const float PinchGap = 0.036f;

        /// <summary>
        /// 左手（left）または右手を、親指と人差し指でつまむ形にする。中指・薬指・小指は手のひらへ握り込む。
        /// 親指は人差し指の先へ向けて寄せ、二つの指先の隙間が <see cref="PinchGap"/> になるところで止める。
        /// つまんだ物の置き所（二つの指先の間）を、手の骨の向きの枠で見た位置と向きで返す。
        /// 物の向きは、前（+Z）が手のひらへ向かう向き、上（+Y）が指の向き
        /// </summary>
        public static void Pinch(Animator an, bool left, out Vector3 holdLocal, out Quaternion holdRotation)
        {
            Pinch(an, left, PinchGap, 1f, out holdLocal, out holdRotation);
        }

        /// <summary>
        /// つまむ形を、隙間 gap と、人差し指・中指の曲げの割合 curlScale を変えて作る。
        /// 掴む前に開いておく形（隙間を広く、指を伸ばし気味に）にも使う
        /// </summary>
        public static void Pinch(Animator an, bool left, float gap, float curlScale, out Vector3 holdLocal, out Quaternion holdRotation)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            // 中指・薬指・小指は手のひらへ握り込む。つまむ指の下（ジャックと、刺さっている手首の皮膚）へ出さない
            var curl = new[]
            {
                new { a = F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), b = F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), c = F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal), d = new[] { 40f, 45f, 25f } },
                new { a = F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), b = F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), c = F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal), d = new[] { 72f, 80f, 45f } },
                new { a = F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), b = F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), c = F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal), d = new[] { 82f, 90f, 50f } },
                new { a = F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), b = F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), c = F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal), d = new[] { 82f, 90f, 50f } },
            };
            for (var i = 0; i < curl.Length; i++)
            {
                var f = curl[i];
                // 人差し指だけ割合を掛ける。中指・薬指・小指はいつも握り込み、つまむ指の下を空けておく
                var k = i < 1 ? curlScale : 1f;
                Flex(an, left, f.a, f.b, f.d[0] * k);
                Flex(an, left, f.b, f.c, f.d[1] * k);
                Flex(an, left, f.c, HumanBodyBones.LastBone, f.d[2] * k);
            }
            var tp = an.GetBoneTransform(F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal));
            var indexTip = Tip(an, F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal), TipLength);
            // 親指の付け根から見て、親指の先が人差し指の先へ向かうよう寄せる。寄せる割合を二分で探す
            var keep = tp.localRotation;
            var from = Tip(an, F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal), TipLength) - tp.position;
            var toward = Quaternion.FromToRotation(from, indexTip - tp.position);
            float lo = 0f, hi = 1f;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * 0.5f;
                tp.localRotation = keep;
                tp.rotation = Quaternion.Slerp(Quaternion.identity, toward, mid) * tp.rotation;
                var thumbTip = Tip(an, F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal), TipLength);
                if (Vector3.Distance(thumbTip, indexTip) > gap) lo = mid; else hi = mid;
            }
            var tTip = Tip(an, F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal), TipLength);
            var hand = an.GetBoneTransform(F(HumanBodyBones.LeftHand, HumanBodyBones.RightHand));
            var at = (tTip + indexTip) * 0.5f;
            // ジャックの尻（ケーブルの出る側）は手のひらへ、根元は手のひらから外へ向ける。
            // 上から摘まんで手の甲の側へ動かせば、ジャックの向きに沿って抜ける
            var palm = PalmDir(an, left);
            var forward = -palm;
            var up = Vector3.ProjectOnPlane(FingerDir(an, left), forward).normalized;
            var world = Quaternion.LookRotation(forward, up);
            holdRotation = Quaternion.Inverse(hand.rotation) * world;
            // 指先でつまむのはジャックの胴（根元の座金ではない）。置き所（ジャックの根元）を胴の真ん中の分だけ根元へ下げる
            holdLocal = Quaternion.Inverse(hand.rotation) * (at - forward * GripAlong - hand.position);
        }

        /// <summary>
        /// つまむ形（<see cref="Pinch(Animator, bool, out Vector3, out Quaternion)"/> の後に呼ぶ）から、親指と人差し指を
        /// つまむ所から離す向きへ開く。親指は人差し指の先から遠ざける向きへ thumbAway 度、人差し指は曲げを indexOpen 度戻す。
        /// 開いた指の先は、つまむ所をはさんで両側に離れるので、手をジャックへ寄せても指先がジャックを横切らない
        /// </summary>
        public static void Open(Animator an, bool left, float thumbAway, float indexOpen)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            var tp = an.GetBoneTransform(F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal));
            var tipI = Tip(an, F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal), TipLength);
            var tipT = Tip(an, F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal), TipLength);
            // 親指の付け根から見て、親指の先を人差し指の先から遠ざける回し
            var toThumb = tipT - tp.position;
            var toIndex = tipI - tp.position;
            var axis = Vector3.Cross(toIndex, toThumb);
            if (axis.sqrMagnitude > 1e-10f) tp.rotation = Quaternion.AngleAxis(thumbAway, axis.normalized) * tp.rotation;
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
                Vector3.ProjectOnPlane(an.GetBoneTransform(b).position - an.GetBoneTransform(a).position, palm).normalized;
            var mid = dir(F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate));
            var others = new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate) },
            };
            foreach (var o in others)
            {
                var angle = Vector3.SignedAngle(dir(o[0], o[1]), mid, palm);
                var bone = an.GetBoneTransform(o[0]);
                bone.rotation = Quaternion.AngleAxis(angle * amount, palm) * bone.rotation;
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
        public static void Wrap(Animator an, SkinnedMeshRenderer skin, bool left, System.Func<Vector3, float> clearance, Vector3 most, float thumbMost, System.Text.StringBuilder log = null)
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
