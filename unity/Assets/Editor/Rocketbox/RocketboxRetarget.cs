using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 前の主人公（Quaternius の女性）の立ちと歩き（Quaternius の W_Suit の骨向けの Generic の動き、Assets/Animation/Idle.anim・Walk.anim）を、
    /// Rocketbox の Humanoid の動きへ移し替える。
    ///
    /// Quaternius の骨は足が脛の子ではなく根の子（足の IK の骨）なので、Unity の Humanoid にそのまま載らない。
    /// そこで一こまずつ、骨ごとの向きを「体の形から決めた向きの枠」同士で合わせて Rocketbox の骨へ写し、
    /// その姿を Rocketbox の Avatar で筋肉の値（HumanPose）に読み直して Humanoid の動きとして書く。
    /// 書いた動きは Humanoid なので、Rocketbox の他の人にもそのまま載る。
    ///
    /// 枠は、骨の向き（子の骨へ向かう向き）を主の軸に、左右（腿・肩・手のひら）の向きを従の軸にして作る。
    /// 両方の骨の、元の姿勢（Quaternius は歩きの途中、Rocketbox は腕を下げた A の字）での枠を T の字の枠に対応させる。
    /// 足だけは、元の姿勢で床に平らに置かれていると見なして、足先の水平の向きと真上で枠を作る。
    ///
    /// 肩と腕は向きを合わせるだけでは不自然になるので、次のように直す。
    /// - 鎖骨は写さず、Rocketbox の束ねた姿勢の向き（体の外へ 13 度下がる）のままにする。
    ///   Quaternius の肩の骨は胸の中ほどから肩の付け根へ 34 度上がって伸びる作りで、これを Rocketbox の鎖骨の向きに合わせると、
    ///   鎖骨が 47 度持ち上がって肩の付け根が 8 cm 上がり、肩が盛り上がって見えた
    /// - 立ちの腕と脚は、Quaternius の立ちを写さず、Rocketbox の骨の上で直立の形を決める（<see cref="Job.CalibrateStand"/>）。
    ///   Quaternius の立ちは腕が体の前に来て肘が曲がっており、骨ごとの直しを重ねても直立に見えなかった。元の立ちからは腕の揺れだけを残す
    /// - 歩きの腕は、上腕・前腕・手を肩の付け根まわりに一緒に回して、振りの真ん中が体の横に来るようにする（振りの幅は変わらない）。
    ///   そのうえで、前への振りだけを縮める（<see cref="WalkArmFrontScale"/>）。後ろへの振りはそのまま。
    ///   肘の曲がる向きも体の真ん前へ向け直す（<see cref="WalkElbowTurn"/>）。元の歩きは上腕が内へねじれていて、手が体の真ん中へ寄っていた
    /// </summary>
    public static class RocketboxRetarget
    {
        public const string OutDir = "Assets/Animation/Humanoid";
        public const string ControllerPath = BuildRocketboxProtagonist.Controller;
        public const string SourceModel = "Assets/Models/quaternius/W_Suit.fbx";
        /// <summary>写す元の状態機械。前の主人公（Quaternius）の立ちと歩きのもので、写してから動きだけ Humanoid のものへ差し替える</summary>
        public const string SourceController = "Assets/Animation/Protagonist.controller";
        public const string SourceIdle = "Assets/Animation/Idle.anim";
        public const string SourceWalk = "Assets/Animation/Walk.anim";

        /// <summary>
        /// 胸（Spine2 から上）を後ろへ起こす角度（度）。Quaternius の立ちは背中が丸まって胸が落ちているので、胸を起こして軽い反りにする
        /// </summary>
        public const float ChestLift = 6f;
        /// <summary>立ちの初めのこまで、首の付け根を腰（腿の付け根の中点）の真上からどれだけ前に置くか（横から見て、m）</summary>
        public const float NeckOverHips = 0f;
        /// <summary>立ちの初めのこまでの顔の向き（度。正は下向き、0 で目の高さの前）</summary>
        public const float HeadPitch = 0f;

        // ---- 立ちの形（腕と脚は、元の立ちを写さず Rocketbox の骨の上で決める） ----
        /// <summary>立ち: 上腕が体の横から外へ開く角度（正面から見て、度）</summary>
        public const float StandArmOpen = 3f;
        /// <summary>立ち: 手首を、腰の関節を通る縦の線からどれだけ前に置くか（横から見て、m）。上腕の前後の傾きで合わせる</summary>
        public const float StandWrist = 0f;
        /// <summary>立ち: 手（手首と中指の付け根の中ほどの骨の線）と太ももの外側の面の隙間（正面から見て、m）。前腕を内へ寄せて合わせる。
        /// 曲げた指先が太ももに刺さらない幅にする</summary>
        public const float StandHandGap = 0.03f;
        /// <summary>立ち: 太ももの外側の面の、腿の骨の線からの距離（m。主人公のジーンズで手の高さを測った値）</summary>
        public const float StandThighRadius = 0.09f;
        /// <summary>立ち: 手のひらの向き。小指の付け根から人差し指の付け根への向きを、真前から体の側へ回す角度（度）。
        /// 0 で手のひらが真横の太ももを向き、正で手のひらが少し後ろを向く（曲げた指先が太ももの横へ刺さらない）</summary>
        public const float StandPalmTurn = 25f;
        /// <summary>立ち: 肘の曲げ（度。前腕が上腕の延長から前へ出る角度）</summary>
        public const float StandElbow = 8f;
        /// <summary>
        /// 立ち・歩き: 指の曲げ（Humanoid の指の Stretched の筋肉の値。人差し指・中指・薬指・小指の順）。
        /// Humanoid の指の筋肉は 0 でも一つの関節で 35° ほど曲がっていて、-0.35 では 52° ずつ曲がって握りこぶしに見えた。
        /// 0.3 で一つの関節 20° ほど。小指ほど少し深く曲げる
        /// </summary>
        public static readonly float[] FingerStretch = { 0.30f, 0.25f, 0.20f, 0.15f };
        /// <summary>立ち・歩き: 親指の曲げ（Stretched）。正では親指が前へ突き出るので、負にして人差し指の横へ下ろす</summary>
        public const float FingerStretchThumb = -0.4f;
        /// <summary>立ち・歩き: 人差し指から小指の開き（Humanoid の指の Spread の筋肉の値）。0 では指の間が空いて見えたので少し寄せる</summary>
        public const float FingerSpread = -0.4f;
        /// <summary>立ち・歩き: 親指の開き（Spread）</summary>
        public const float ThumbSpread = 0f;
        /// <summary>立ち: 膝の曲げ（度。0 でまっすぐ）</summary>
        public const float StandKnee = 2f;
        /// <summary>立ち: 顔の向き。眉間から下唇への線が真下から前へ出る角度（度）。Rocketbox の束ねた姿勢は 8.8° で、顎が上がって見えた</summary>
        public const float FaceTilt = 2f;
        /// <summary>立ち: 元の立ちの揺れ（呼吸と重心の動き）を腕に残す割合（1 で元のまま、0 で止める）</summary>
        public const float StandSway = 1f;
        /// <summary>立ち: 腰（腿の付け根の中点）を足首の中点の真上からどれだけ前に置くか（横から見て、m）</summary>
        public const float HipsOverAnkles = 0.015f;
        /// <summary>歩き: 腕の振りの真ん中で、上腕が体の横から外へ開く角度と、真下から前へ出る角度（度）</summary>
        public const float WalkArmOpen = 5f, WalkArmForward = 0f;
        /// <summary>
        /// 歩き: 腕の前への振りを縮める割合。上腕が真下より前へ出る角（横から見て）を、この割合に縮める。後ろへの振りは縮めない。
        /// 元の歩きは前へ 34°・後ろへ 29° 振り、前では肘も 37° ほど曲がるので、前腕が真下から 63° 前まで上がって、
        /// 手が腰の高さで体の前 40 cm ほどまで出ていた（一人称で見下ろすと、手が画面の真ん中近くまで振れて見えた）。
        /// 0.5 で、上腕の前は 34° から 19°、前腕は真下から 63° から 50° になり、手首のいちばん前は 9 cm 手前に下がる（後ろは 29° のまま）。
        /// 上腕・前腕・手を肩の付け根まわりに一緒に回すので、肘と手首の曲げは変わらない。
        /// 鎖骨は回さないので肩も上がらない
        /// </summary>
        public const float WalkArmFrontScale = 0.5f;
        /// <summary>
        /// 歩き: 前への振りを縮め始める幅（度）。真下からこの角までは縮め方をなだらかに強め、腕の振りの速さが真下で折れないようにする
        /// </summary>
        public const float WalkArmFrontEase = 10f;
        /// <summary>
        /// 歩き: 肘の曲がる向きを体の真ん前へ向け直す割合（1 で真ん前）。
        /// 元の歩きは上腕が内へ 30〜40° ねじれていて、肘の曲げの軸が体の左右の軸から内へ回っていた（立ちは 14° ほど）。
        /// 前で肘が 37° 曲がると前腕が体の内へ 17° 向き、前へ出た手が体の真ん中から 7 cm（立ちは 20 cm）まで寄って、股の前へ入っていた。
        /// 上腕を自分の軸まわりに回して、肘の曲げの軸を体の左右の軸に揃える。上腕の向き（前後の振りと開き）と肘の曲げの大きさは変わらず、
        /// 肘の場所も動かない（肩も上がらず、肘も外へ張り出さない）。回した分、手のひらの向きは前腕を自分の軸まわりに回して戻す（手首の曲げは変わらない）。
        /// 1 で、手の体の真ん中からの離れは一巡を通して 15〜24 cm（立ちと同じくらい）になる。前腕が内でなく前を向くぶん、前へ出た手は 4 cm 前へ出る
        /// </summary>
        public const float WalkElbowTurn = 1f;
        /// <summary>
        /// 立ち・歩き: 鎖骨を束ねた姿勢の向き（体の外へ 13〜14 度下がる）から、さらに肩の先を下げる角度（度）。なで肩に見せる。
        /// 胸の骨に付いて動く（胸の向きの中で回す）。10° で鎖骨は 13.4° から 23.4° 下がり、肩の関節の幅は 34.4 から 33.3 cm、高さは 1.6 cm 下がる。
        /// 歩きでも 23〜24° のまま（肩が上がって見えない）
        /// </summary>
        public const float ClavicleDrop = 10f;

        [MenuItem("HalfAware/Rocketbox/Retarget the idle and walk")]
        public static void Menu()
        {
            Debug.Log(Bake());
        }

        /// <summary>立ちと歩きを移し替えて書き、今の状態機械の写しに差す。今の Protagonist.controller には触らない</summary>
        public static string Bake()
        {
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Animation", "Humanoid");
            var sb = new StringBuilder();
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceIdle);
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceWalk);
            if (idle == null || walk == null) throw new InvalidOperationException("元の動きが無い");
            var made = new Dictionary<AnimationClip, AnimationClip>();
            using (var job = new Job())
            {
                sb.AppendLine(job.CalibrateArms(walk));
                sb.AppendLine(job.CalibrateTrunk(idle));
                sb.AppendLine(job.CalibrateStand(idle));
                foreach (var clip in new[] { idle, walk })
                {
                    string note;
                    var human = job.Convert(clip, out note);
                    var path = OutDir + "/" + clip.name + ".anim";
                    var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (old != null)
                    {
                        EditorUtility.CopySerialized(human, old);
                        Object.DestroyImmediate(human);
                        human = old;
                        EditorUtility.SetDirty(old);
                    }
                    else AssetDatabase.CreateAsset(human, path);
                    made[clip] = human;
                    sb.AppendLine(clip.name + ": " + note + " → " + path);
                }
            }
            MakeController(made);
            AssetDatabase.SaveAssets();
            sb.AppendLine("状態機械: " + ControllerPath);
            return sb.ToString();
        }

        /// <summary>今の状態機械を写し、動きだけ Humanoid のものへ替える</summary>
        static void MakeController(Dictionary<AnimationClip, AnimationClip> made)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceController, ControllerPath))
                    throw new InvalidOperationException("状態機械を写せない");
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            ctrl.name = "ProtagonistHumanoid";
            foreach (var layer in ctrl.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    var clip = st.state.motion as AnimationClip;
                    if (clip == null) continue;
                    // 元の動きか、前に差した Humanoid の動き（名前が同じ）を差し替える
                    foreach (var kv in made)
                        if (kv.Key.name == clip.name) { st.state.motion = kv.Value; break; }
                }
            EditorUtility.SetDirty(ctrl);
        }

        // ---- 骨の対応 ---------------------------------------------------------

        /// <summary>向きの枠の決め方。aim が主の軸（T の字の c へ）、hint が従の軸（h へ）</summary>
        sealed class FrameRule
        {
            public Func<Vector3> Aim, Hint;
            public Vector3 C, H;

            public Quaternion Rest()
            {
                var aim = Aim().normalized;
                var hint = Vector3.ProjectOnPlane(Hint(), aim).normalized;
                return Quaternion.LookRotation(aim, hint) * Quaternion.Inverse(Quaternion.LookRotation(C, H));
            }
        }

        sealed class Joint
        {
            public string Name;
            public Transform Src, Dst;
            /// <summary>dst の世界の向き = src の世界の向き × Offset</summary>
            public Quaternion Offset;
        }

        /// <summary>一回分の移し替え。両方の模型をその場で作り、終わったら壊す</summary>
        sealed class Job : IDisposable
        {
            readonly GameObject src, dst;
            readonly Dictionary<string, Transform> s = new Dictionary<string, Transform>();
            readonly Dictionary<string, Transform> d = new Dictionary<string, Transform>();
            readonly List<Joint> joints = new List<Joint>();
            readonly Avatar avatar;
            readonly float legScale;
            /// <summary>元の姿勢での、Quaternius の足の骨（床の上の IK の骨）の高さと、Rocketbox の足首の高さ</summary>
            readonly float srcFootRest, dstAnkleRest;
            readonly Dictionary<Transform, Quaternion> srcRest = new Dictionary<Transform, Quaternion>();
            readonly Dictionary<Transform, Vector3> srcRestPos = new Dictionary<Transform, Vector3>();
            /// <summary>腕の骨（上腕・前腕・手）に掛ける世界の回し。左右それぞれ</summary>
            readonly Dictionary<string, Quaternion> armFix = new Dictionary<string, Quaternion>();
            /// <summary>背骨から頭の骨に掛ける世界の回し（横から見た起こし。子の骨ほど上の骨の分を足してある）</summary>
            readonly Dictionary<string, Quaternion> trunkFix = new Dictionary<string, Quaternion>();
            /// <summary>束ねた姿勢の頭の骨の世界の向き（顔の向きを測るため）</summary>
            Quaternion headBind;
            /// <summary>束ねた姿勢の Rocketbox の骨の世界の向きと位置</summary>
            readonly Dictionary<Transform, Quaternion> dstBind = new Dictionary<Transform, Quaternion>();
            readonly Dictionary<Transform, Vector3> dstBindPos = new Dictionary<Transform, Vector3>();
            /// <summary>立ちの形の腕の骨の世界の向きと、胴・首・頭・脚の骨の世界の向き（揺れを掛ける前）</summary>
            readonly Dictionary<string, Quaternion> standArm = new Dictionary<string, Quaternion>();
            readonly Dictionary<string, Quaternion> standPose = new Dictionary<string, Quaternion>();
            /// <summary>元の立ちの初めのこまの、腕の骨の世界の向き（揺れを測る基）</summary>
            readonly Dictionary<Transform, Quaternion> srcIdle0 = new Dictionary<Transform, Quaternion>();
            /// <summary>立ちの形を使う動き（立ち）</summary>
            AnimationClip standClip;
            /// <summary>腕の前への振りを縮める動き（歩き。<see cref="CalibrateArms"/> で決める）</summary>
            AnimationClip armClip;

            /// <summary>
            /// 立ちの形を決める。元の立ちを写すと、膝が曲がって腰が引け、顎が上がり、腕が体の前に来ていたので、元の立ちの形は使わず、
            /// Rocketbox の骨の上で一つずつ決める。
            /// - 腰・背骨・首: Rocketbox の束ねた姿勢（まっすぐ立った形）のまま
            /// - 脚: 腿の付け根から足首へまっすぐ（膝は <see cref="StandKnee"/> 度、腰は足首の <see cref="HipsOverAnkles"/> 前）。足は床に平ら
            /// - 頭: 眉間から下唇への線が <see cref="FaceTilt"/> 度になるよう前へ倒す（束ねた姿勢は 8.8° で顎が上がって見えた）
            /// - 腕: 上腕は体の横に <see cref="StandArmOpen"/> 度開いてまっすぐ下ろし、肘は <see cref="StandElbow"/> 度、
            ///   前腕をひねって手のひらを太ももの側へ（人差し指の付け根が小指の付け根より前、<see cref="StandPalmTurn"/> 度だけ後ろ寄り）、手首はまっすぐ。指は軽く曲げる
            /// 元の立ちからは、初めのこまからの胴・首・頭・腕の揺れ（呼吸と重心の動き）だけを <see cref="StandSway"/> の割合で重ねる
            /// </summary>
            public string CalibrateStand(AnimationClip idle)
            {
                standClip = null;
                standArm.Clear();
                standPose.Clear();
                Pose(idle, 0f);
                var before = Stand();
                ResetSource();
                idle.SampleAnimation(src, 0f);
                foreach (var j in joints) srcIdle0[j.Src] = j.Src.rotation;
                standClip = idle;
                // 胴と首: Rocketbox の束ねた姿勢（まっすぐ立った背骨）のまま。元の立ちからは揺れだけを重ねる
                foreach (var name in new[] { "Pelvis", "Spine", "Spine1", "Spine2", "Neck" }) standPose[name] = dstBind[JointDst(name)];
                // 脚: 腿の付け根から足首へまっすぐ（膝は StandKnee だけ前へ）。足は床に平ら（束ねた姿勢の向き）
                foreach (var side in new[] { "L", "R" })
                {
                    var thigh = D("Bip01 " + side + " Thigh");
                    var calf = D("Bip01 " + side + " Calf");
                    var foot = D("Bip01 " + side + " Foot");
                    var dT = dstBindPos[calf] - dstBindPos[thigh];
                    var dC = dstBindPos[foot] - dstBindPos[calf];
                    var len = dT.magnitude + dC.magnitude;
                    var dx = dstBindPos[foot].x - dstBindPos[thigh].x;
                    var dz = -HipsOverAnkles;
                    var line = new Vector3(dx, -Mathf.Sqrt(Mathf.Max(0.01f, len * len - dx * dx - dz * dz)), dz).normalized;
                    // 膝を前へ: 腿は下向きから前へ、脛は後ろへ（x まわり。負の角で前）
                    var thighDir = Quaternion.AngleAxis(-StandKnee * 0.5f, Vector3.right) * line;
                    var calfDir = Quaternion.AngleAxis(StandKnee * 0.5f, Vector3.right) * line;
                    standPose["UpperLeg." + side] = Quaternion.FromToRotation(dT, thighDir) * dstBind[thigh];
                    standPose["LowerLeg." + side] = Quaternion.FromToRotation(dC, calfDir) * dstBind[calf];
                    standPose["Foot." + side] = dstBind[foot];
                }
                // 頭: 眉間から下唇への線を FaceTilt へ
                float head = 0f;
                standPose["Head"] = dstBind[JointDst("Head")];
                for (var it = 0; it < 4; it++)
                {
                    standPose["Head"] = Quaternion.AngleAxis(head, Vector3.right) * dstBind[JointDst("Head")];
                    Pose(idle, 0f);
                    head += FaceLine() - FaceTilt;
                }
                standPose["Head"] = Quaternion.AngleAxis(head, Vector3.right) * dstBind[JointDst("Head")];
                // 腕: 上腕の前後の傾きで手首を腰の関節の縦の線へ、前腕の内への寄せで手を太ももの横へ合わせる
                float fwd = 0f, inward = 0f;
                for (var it = 0; it < 6; it++)
                {
                    SetArms(fwd, inward);
                    Pose(idle, 0f);
                    float wrist, gap, elbow;
                    StandMeasure(out wrist, out gap, out elbow);
                    var armLen = Vector3.Distance(D("Bip01 L UpperArm").position, D("Bip01 L Hand").position);
                    var foreLen = Vector3.Distance(D("Bip01 L Forearm").position, D("Bip01 L Hand").position);
                    fwd -= Mathf.Atan2(wrist - StandWrist, armLen) * Mathf.Rad2Deg;
                    inward += Mathf.Atan2(gap - StandHandGap, foreLen) * Mathf.Rad2Deg;
                }
                SetArms(fwd, inward);
                Pose(idle, 0f);
                var after = Stand();
                ResetSource();
                return string.Format(CultureInfo.InvariantCulture,
                    "立ちの形（Rocketbox の骨の上で決めた）: 胴と首は束ねた姿勢、膝 {0:0}°、頭を {1:0.0}° 下へ、上腕の開き {2:0}°・肘 {3:0}°・手のひらを太ももへ（{7:0}° 後ろ寄り）・中指の筋肉 {4:0.00}。直す前 {5} → 直した後 {6}",
                    StandKnee, head, StandArmOpen, StandElbow, FingerStretch[1], before, after, StandPalmTurn);
            }

            Transform JointDst(string name)
            {
                foreach (var j in joints) if (j.Name == name) return j.Dst;
                throw new InvalidOperationException("骨の対応が無い: " + name);
            }

            /// <summary>眉間から下唇への線が真下から前へ出る角度（度。顎が上がるほど大きい）</summary>
            float FaceLine()
            {
                var brow = D("Bip01 MMiddleEyebrow").position;
                var lip = D("Bip01 MBottomLip").position;
                return Mathf.Atan2(lip.z - brow.z, brow.y - lip.y) * Mathf.Rad2Deg;
            }

            /// <summary>立ちの腕の骨の世界の向き。fwd は上腕を前へ出す角（度）、inward は前腕を体の側へ寄せる角（度）</summary>
            void SetArms(float fwd, float inward)
            {
                foreach (var side in new[] { "L", "R" })
                {
                    var sign = side == "L" ? -1f : 1f;
                    var up = D("Bip01 " + side + " UpperArm");
                    var fo = D("Bip01 " + side + " Forearm");
                    var ha = D("Bip01 " + side + " Hand");
                    var mid = D("Bip01 " + side + " Finger2");
                    var index = D("Bip01 " + side + " Finger1");
                    var pinky = D("Bip01 " + side + " Finger4");
                    var dU = dstBindPos[fo] - dstBindPos[up];
                    var dF = dstBindPos[ha] - dstBindPos[fo];
                    var dH = dstBindPos[mid] - dstBindPos[ha];
                    var palm = dstBindPos[index] - dstBindPos[pinky];
                    var wantU = new Vector3(sign * Mathf.Tan(StandArmOpen * Mathf.Deg2Rad), -1f, Mathf.Tan(fwd * Mathf.Deg2Rad)).normalized;
                    // 肘を前へ曲げ（下向きを x まわりに負へ回すと前へ出る）、前腕を体の側へ寄せる（z まわり。左は +x、右は −x へ）
                    var wantF = Quaternion.AngleAxis(-sign * inward, Vector3.forward) * Quaternion.AngleAxis(-StandElbow, Vector3.right) * wantU;
                    // 手のひらを太ももの側へ: 小指の付け根から人差し指の付け根への向きを前へ（親指が前）。そこから StandPalmTurn だけ体の側へ回す
                    var ahead = Quaternion.AngleAxis(-sign * StandPalmTurn, Vector3.up) * Vector3.forward;
                    standArm["UpperArm." + side] = Quaternion.FromToRotation(dU, wantU) * dstBind[up];
                    standArm["LowerArm." + side] = Frame(wantF, ahead) * Quaternion.Inverse(Frame(dF, palm)) * dstBind[fo];
                    standArm["Hand." + side] = Frame(wantF, ahead) * Quaternion.Inverse(Frame(dH, palm)) * dstBind[ha];
                }
            }

            /// <summary>aim を前、side を上にした向きの枠</summary>
            static Quaternion Frame(Vector3 aim, Vector3 side)
            {
                return Quaternion.LookRotation(aim.normalized, Vector3.ProjectOnPlane(side, aim.normalized).normalized);
            }

            /// <summary>
            /// 立ちの測り（横から見て前が正、cm）: 手首と腰の関節を通る縦の線の前後のずれ（左右の大きい方）、
            /// 正面から見た手と太ももの隙間（左右の大きい方）、肘の曲げ、腰と足首の前後のずれ
            /// </summary>
            string Stand()
            {
                float wrist, gap, elbow;
                StandMeasure(out wrist, out gap, out elbow);
                var hips = Mid(D("Bip01 L Thigh"), D("Bip01 R Thigh"));
                var ankle = Mid(D("Bip01 L Foot"), D("Bip01 R Foot"));
                var knee = Mid(D("Bip01 L Calf"), D("Bip01 R Calf"));
                var kneeAngle = Mathf.Max(Vector3.Angle(D("Bip01 L Calf").position - D("Bip01 L Thigh").position, D("Bip01 L Foot").position - D("Bip01 L Calf").position),
                    Vector3.Angle(D("Bip01 R Calf").position - D("Bip01 R Thigh").position, D("Bip01 R Foot").position - D("Bip01 R Calf").position));
                // 腰・膝・足首の一直線からの膝の前後のずれ
                var t = Mathf.InverseLerp(hips.y, ankle.y, knee.y);
                var kneeOff = knee.z - Mathf.Lerp(hips.z, ankle.z, t);
                var headOverNeck = D("Bip01 Head").position.z - D("Bip01 Neck").position.z;
                return string.Format(CultureInfo.InvariantCulture, "手首と腰の関節の前後のずれ {0:+0.0;-0.0} cm・手と太ももの隙間 {1:0.0} cm・肘の曲げ {2:0}°・腰と足首の前後のずれ {3:+0.0;-0.0} cm・膝の曲げ {4:0.0}°・膝の一直線からの前後 {5:+0.0;-0.0} cm・頭の骨と首の骨の前後 {6:+0.0;-0.0} cm・顔の線 {7:0.0}°",
                    wrist * 100f, gap * 100f, elbow, (hips.z - ankle.z) * 100f, kneeAngle, kneeOff * 100f, headOverNeck * 100f, FaceLine());
            }

            void StandMeasure(out float wrist, out float gap, out float elbow)
            {
                wrist = 0f;
                gap = 0f;
                elbow = 0f;
                foreach (var side in new[] { "L", "R" })
                {
                    var thigh = D("Bip01 " + side + " Thigh").position;
                    var hand = D("Bip01 " + side + " Hand").position;
                    var fingers = D("Bip01 " + side + " Finger2").position;
                    var dz = hand.z - thigh.z;
                    if (Mathf.Abs(dz) > Mathf.Abs(wrist)) wrist = dz;
                    // 手の中ほど（手首と中指の付け根の間）の高さでの、太ももの外側の面までの左右の隙間（面は骨の線から StandThighRadius）
                    var palmMid = (hand + fingers) * 0.5f;
                    var calf = D("Bip01 " + side + " Calf").position;
                    var t = Mathf.InverseLerp(thigh.y, calf.y, palmMid.y);
                    var axis = Vector3.Lerp(thigh, calf, t);
                    var g = Mathf.Abs(palmMid.x - axis.x) - StandThighRadius;
                    gap = Mathf.Max(gap, g);
                    var u = D("Bip01 " + side + " Forearm").position - D("Bip01 " + side + " UpperArm").position;
                    var f = hand - D("Bip01 " + side + " Forearm").position;
                    elbow = Mathf.Max(elbow, Vector3.Angle(u, f));
                }
            }

            /// <summary>
            /// 立ちの初めのこまの背骨を直す量を決める。Quaternius の立ちをそのまま写すと、上体が前に傾いて背中が丸まり、
            /// それを補うように顔が上を向いていた。胸を <see cref="ChestLift"/> 度起こし、背骨の根（Spine）で上体を後ろへ回して
            /// 首の付け根を腰の真上（<see cref="NeckOverHips"/>）へ、首と頭で顔を <see cref="HeadPitch"/> の向きへ戻す。
            /// 同じ回しを歩きにも掛ける（歩きの中での前傾の変化は残る）
            /// </summary>
            public string CalibrateTrunk(AnimationClip idle)
            {
                trunkFix.Clear();
                Pose(idle, 0f);
                var before = Trunk();
                float lean = 0f, head = 0f;
                for (var it = 0; it < 4; it++)
                {
                    SetTrunk(lean, head);
                    Pose(idle, 0f);
                    var hips = Mid(D("Bip01 L Thigh"), D("Bip01 R Thigh"));
                    var neck = D("Bip01 Neck").position;
                    lean -= Mathf.Atan2(neck.z - hips.z - NeckOverHips, neck.y - hips.y) * Mathf.Rad2Deg;
                    SetTrunk(lean, head);
                    Pose(idle, 0f);
                    head -= FacePitch() - HeadPitch;
                }
                SetTrunk(lean, head);
                Pose(idle, 0f);
                var after = Trunk();
                ResetSource();
                return string.Format(CultureInfo.InvariantCulture,
                    "背骨の直し（立ちの初めのこま）: 胸を {0:0.0}° 起こし、上体を {1:0.0}° 後ろへ、顔を {2:0.0}° 回す。直す前 {3} → 直した後 {4}",
                    ChestLift, -lean, head, before, after);
            }

            void SetTrunk(float lean, float head)
            {
                // 正の角は前へ倒す向き（模型は +z を向く）
                Func<float, Quaternion> rx = deg => Quaternion.AngleAxis(deg, Vector3.right);
                trunkFix["Spine"] = rx(lean);
                trunkFix["Spine1"] = rx(lean);
                trunkFix["Spine2"] = rx(lean - ChestLift);
                trunkFix["Neck"] = rx(lean - ChestLift + head * 0.5f);
                trunkFix["Head"] = rx(lean - ChestLift + head);
            }

            /// <summary>横から見た、腰から首の付け根への線の傾き（度、前が正）、背中の折れ、首の付け根と腰の前後のずれ（cm、前が正）、顔の向き（度、下が正）</summary>
            string Trunk()
            {
                var hips = Mid(D("Bip01 L Thigh"), D("Bip01 R Thigh"));
                var neck = D("Bip01 Neck").position;
                var chest = D("Bip01 Spine2").position;
                var tilt = Mathf.Atan2(neck.z - hips.z, neck.y - hips.y) * Mathf.Rad2Deg;
                // 背中の折れ: 腰→胸と胸→首の付け根の向きの差（上が前へ折れるほど正 = 丸まり）
                var lower = Mathf.Atan2(chest.z - hips.z, chest.y - hips.y) * Mathf.Rad2Deg;
                var upper = Mathf.Atan2(neck.z - chest.z, neck.y - chest.y) * Mathf.Rad2Deg;
                return string.Format(CultureInfo.InvariantCulture, "背骨の傾き {0:0.0}°・背中の折れ {1:0.0}°・首の付け根と腰の前後のずれ {2:0.0} cm・顔の向き {3:0.0}°",
                    tilt, upper - lower, (neck.z - hips.z) * 100f, FacePitch());
            }

            /// <summary>顔の向き（度、下向きが正）。束ねた姿勢で顔は +z を向く</summary>
            float FacePitch()
            {
                var f = D("Bip01 Head").rotation * Quaternion.Inverse(headBind) * Vector3.forward;
                return Mathf.Atan2(-f.y, f.z) * Mathf.Rad2Deg;
            }

            /// <summary>元の動きの t 秒の姿を、Rocketbox の骨へ写す（腕と背骨の直しと、腰の位置合わせを含む）</summary>
            void Pose(AnimationClip clip, float t)
            {
                ResetSource();
                clip.SampleAnimation(src, t);
                var stand = clip == standClip;
                // 鎖骨: 束ねた姿勢の向きから ClavicleDrop だけ肩の先を下げる（胸の骨の中の向きで決めるので、胸の動きに付いて動く）
                foreach (var side in new[] { "L", "R" })
                {
                    var cl = D("Bip01 " + side + " Clavicle");
                    var parent = cl.parent;
                    var bindLocal = Quaternion.Inverse(dstBind[parent]) * dstBind[cl];
                    var axis = Quaternion.Inverse(dstBind[parent]) * Vector3.forward;
                    cl.localRotation = Quaternion.AngleAxis(side == "L" ? ClavicleDrop : -ClavicleDrop, axis) * bindLocal;
                }
                foreach (var j in joints)
                {
                    var r = j.Src.rotation * j.Offset;
                    Quaternion c;
                    if (trunkFix.TryGetValue(j.Name, out c)) r = c * r;
                    if (stand && (standArm.TryGetValue(j.Name, out c) || standPose.TryGetValue(j.Name, out c)))
                    {
                        // 立ちの形に、元の立ちの初めのこまからの揺れ（呼吸と重心の動き）を割合で掛ける。脚と足と腰は揺らさない（足が床を滑らないように）
                        Quaternion s0;
                        var legs = j.Name.StartsWith("UpperLeg", StringComparison.Ordinal) || j.Name.StartsWith("LowerLeg", StringComparison.Ordinal) || j.Name.StartsWith("Foot", StringComparison.Ordinal) || j.Name == "Pelvis";
                        var sway = !legs && srcIdle0.TryGetValue(j.Src, out s0) ? j.Src.rotation * Quaternion.Inverse(s0) : Quaternion.identity;
                        r = Quaternion.Slerp(Quaternion.identity, sway, StandSway) * c;
                    }
                    else if (armFix.TryGetValue(j.Name, out c)) r = c * r;
                    j.Dst.rotation = r;
                }
                if (clip == armClip)
                {
                    SquashFront();
                    TurnElbows();
                }
                // 腰の前後左右: 腿の付け根の中点を、脚の長さの比で縮めた Quaternius の中点へ
                var dstPelvis = D("Bip01 Pelvis");
                var srcMid = Mid(S("UpperLeg.L"), S("UpperLeg.R")) - src.transform.position;
                var want = dst.transform.position + srcMid * legScale;
                var move = want - Mid(D("Bip01 L Thigh"), D("Bip01 R Thigh"));
                move.y = 0f;
                dstPelvis.position += move;
                // 高さ: 低い方の足が床から浮く量を揃える（着いている足は床に着いたまま）
                var srcLift = Mathf.Min(S("Foot.L").position.y, S("Foot.R").position.y) - src.transform.position.y - srcFootRest;
                var dstAnkle = Mathf.Min(D("Bip01 L Foot").position.y, D("Bip01 R Foot").position.y) - dst.transform.position.y;
                dstPelvis.position += Vector3.up * (dstAnkleRest + srcLift * legScale - dstAnkle);
            }

            /// <summary>
            /// 歩きの腕: Quaternius の歩きの一回りで上腕の向きを平均し、振りの真ん中が <see cref="WalkArmOpen"/>・<see cref="WalkArmForward"/> へ
            /// 来るように回す量を決める（振りの幅は変わらない）。立ちの腕は <see cref="CalibrateStand"/> で決める
            /// </summary>
            public string CalibrateArms(AnimationClip walk)
            {
                armClip = walk;
                var mean = new Dictionary<string, Vector3>();
                var steps = 24;
                for (var k = 0; k < steps; k++)
                {
                    ResetSource();
                    walk.SampleAnimation(src, walk.length * k / steps);
                    foreach (var side in new[] { "L", "R" })
                    {
                        Vector3 m;
                        mean.TryGetValue(side, out m);
                        mean[side] = m + (S("LowerArm." + side).position - S("UpperArm." + side).position).normalized;
                    }
                }
                var sb = new StringBuilder("歩きの腕の直し（振りの真ん中）: ");
                foreach (var side in new[] { "L", "R" })
                {
                    var sign = side == "L" ? -1f : 1f;
                    var d = mean[side].normalized;
                    var open = Mathf.Atan2(Mathf.Abs(d.x), -d.y) * Mathf.Rad2Deg;
                    var fwd = Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;
                    // 開きと前後の角から向きを作る（下向きを基に、x へ開き、z へ出す）
                    var want = new Vector3(sign * Mathf.Tan(WalkArmOpen * Mathf.Deg2Rad), -1f, Mathf.Tan(WalkArmForward * Mathf.Deg2Rad)).normalized;
                    var fix = Quaternion.FromToRotation(d, want);
                    foreach (var bone in new[] { "UpperArm.", "LowerArm.", "Hand." }) armFix[bone + side] = fix;
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} 開き {1:0}°→{2:0}°、前後 {3:0}°→{4:0}°（{5:0.0}° 回す）/ ", side, open, WalkArmOpen, fwd, WalkArmForward, Quaternion.Angle(Quaternion.identity, fix));
                }
                sb.AppendFormat(CultureInfo.InvariantCulture, "前への振りは {0:0.00} 倍（真下から {1:0}° までなだらかに）", WalkArmFrontScale, WalkArmFrontEase);
                ResetSource();
                return sb.ToString();
            }

            /// <summary>
            /// 歩きの腕の前への振りを縮める（<see cref="WalkArmFrontScale"/>）。横から見て上腕が真下より前へ出た角だけを縮め、後ろへの振りには触らない。
            /// 上腕を肩の付け根まわりに体の左右の軸で回し、前腕と手は子なので一緒に付いてくる（肘と手首の曲げは変わらない）。鎖骨は回さない
            /// </summary>
            void SquashFront()
            {
                foreach (var side in new[] { "L", "R" })
                {
                    var up = D("Bip01 " + side + " UpperArm");
                    var d = D("Bip01 " + side + " Forearm").position - up.position;
                    var fwd = Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;
                    var want = FrontSquash(fwd, WalkArmFrontScale, WalkArmFrontEase);
                    if (Mathf.Abs(want - fwd) < 1e-4f) continue;
                    // x まわりの正の回しで、下向きの腕は後ろへ回る
                    up.rotation = Quaternion.AngleAxis(fwd - want, Vector3.right) * up.rotation;
                }
            }

            /// <summary>
            /// 歩きの肘の曲がる向きを体の真ん前へ向け直す（<see cref="WalkElbowTurn"/>）。
            /// 上腕を自分の軸まわりに回して、肘の曲げの軸（上腕と前腕に直交する軸）を体の左右の軸に揃え、
            /// 手のひらの向きは前腕を自分の軸まわりに回して戻す。上腕の向きと肘の場所、肘と手首の曲げは変わらない
            /// </summary>
            void TurnElbows()
            {
                foreach (var side in new[] { "L", "R" })
                {
                    var up = D("Bip01 " + side + " UpperArm");
                    var fo = D("Bip01 " + side + " Forearm");
                    var ha = D("Bip01 " + side + " Hand");
                    var u = (fo.position - up.position).normalized;
                    var hinge = Vector3.ProjectOnPlane(Vector3.Cross(u, ha.position - fo.position), u);
                    // 肘が伸びきっていると曲げの軸が決まらない
                    if (hinge.sqrMagnitude < 1e-8f) continue;
                    // 前へ曲がる肘の軸は、左右どちらの腕も体の -x（模型は +z を向く）
                    var want = Vector3.ProjectOnPlane(Vector3.left, u);
                    var turn = Vector3.SignedAngle(hinge, want, u) * WalkElbowTurn;
                    var palm = Palm(side);
                    up.rotation = Quaternion.AngleAxis(turn, u) * up.rotation;
                    var f = (ha.position - fo.position).normalized;
                    var back = Vector3.SignedAngle(Vector3.ProjectOnPlane(Palm(side), f), Vector3.ProjectOnPlane(palm, f), f);
                    fo.rotation = Quaternion.AngleAxis(back, f) * fo.rotation;
                }
            }

            /// <summary>手のひらの向き（手の中ほどの向きと、小指の付け根から人差し指の付け根への向きに直交する向き）。回す前と後を比べるだけに使う</summary>
            Vector3 Palm(string side)
            {
                var hand = D("Bip01 " + side + " Hand").position;
                return Vector3.Cross(D("Bip01 " + side + " Finger2").position - hand, D("Bip01 " + side + " Finger1").position - D("Bip01 " + side + " Finger4").position).normalized;
            }

            public Job()
            {
                src = Spawn(SourceModel, new Vector3(0f, -600f, 0f));
                // HumanPoseHandler は体の位置を世界の原点からの値で返すので、Rocketbox は原点に置く
                dst = Spawn(RocketboxPerson.Adult14.Model, Vector3.zero);
                try
                {
                    foreach (var t in src.GetComponentsInChildren<Transform>(true)) s[t.name] = t;
                    foreach (var t in dst.GetComponentsInChildren<Transform>(true)) d[t.name] = t;
                    foreach (var t in src.GetComponentsInChildren<Transform>(true))
                    {
                        srcRest[t] = t.localRotation;
                        srcRestPos[t] = t.localPosition;
                    }
                    avatar = dst.GetComponent<Animator>().avatar;
                    headBind = D("Bip01 Head").rotation;
                    foreach (var t in dst.GetComponentsInChildren<Transform>(true))
                    {
                        dstBind[t] = t.rotation;
                        dstBindPos[t] = t.position;
                    }
                    if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Rocketbox の Avatar が Humanoid でない");
                    Map();
                    var srcLeg = Vector3.Distance(S("UpperLeg.L").position, S("LowerLeg.L").position) + Vector3.Distance(S("LowerLeg.L").position, S("LowerLeg.L_end").position);
                    var dstLeg = Vector3.Distance(D("Bip01 L Thigh").position, D("Bip01 L Calf").position) + Vector3.Distance(D("Bip01 L Calf").position, D("Bip01 L Foot").position);
                    legScale = dstLeg / srcLeg;
                    srcFootRest = Mathf.Min(S("Foot.L").position.y, S("Foot.R").position.y) - src.transform.position.y;
                    dstAnkleRest = Mathf.Min(D("Bip01 L Foot").position.y, D("Bip01 R Foot").position.y) - dst.transform.position.y;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            static GameObject Spawn(string path, Vector3 at)
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (a == null) throw new InvalidOperationException("模型が無い: " + path);
                var go = (GameObject)Object.Instantiate(a);
                go.hideFlags = HideFlags.HideAndDontSave;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                go.transform.position = at;
                var an = go.GetComponent<Animator>();
                if (an != null) an.enabled = false;
                return go;
            }

            Transform S(string n)
            {
                Transform t;
                if (!s.TryGetValue(n, out t)) throw new InvalidOperationException("Quaternius の骨が無い: " + n);
                return t;
            }

            Transform D(string n)
            {
                Transform t;
                if (!d.TryGetValue(n, out t)) throw new InvalidOperationException("Rocketbox の骨が無い: " + n);
                return t;
            }

            static Vector3 Mid(Transform a, Transform b) { return (a.position + b.position) * 0.5f; }

            static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

            /// <summary>骨の対応と、元の姿勢での枠。親から子の順に並べる</summary>
            void Map()
            {
                // 左右の向き（T の字で +x）
                Func<Vector3> sHip = () => S("UpperLeg.R").position - S("UpperLeg.L").position;
                Func<Vector3> dHip = () => D("Bip01 R Thigh").position - D("Bip01 L Thigh").position;
                Func<Vector3> sSho = () => S("Shoulder.R").position - S("Shoulder.L").position;
                Func<Vector3> dSho = () => D("Bip01 R Clavicle").position - D("Bip01 L Clavicle").position;
                Func<string, Func<Vector3>> sPalm = side => () => S("Index1." + side).position - S("Pinky1." + side).position;
                Func<string, Func<Vector3>> dPalm = side => () => D("Bip01 " + side + " Finger1").position - D("Bip01 " + side + " Finger4").position;
                Func<Transform, Transform, Func<Vector3>> dir = (a, b) => () => b.position - a.position;

                var up = Vector3.up;
                var right = Vector3.right;
                var fwd = Vector3.forward;

                Add("Pelvis", S("Body"), D("Bip01 Pelvis"),
                    Rule(() => S("Abdomen").position - Mid(S("UpperLeg.L"), S("UpperLeg.R")), sHip, up, right),
                    Rule(() => D("Bip01 Spine1").position - Mid(D("Bip01 L Thigh"), D("Bip01 R Thigh")), dHip, up, right));
                Add("Spine", S("Hips"), D("Bip01 Spine"),
                    Rule(dir(S("Hips"), S("Abdomen")), sHip, up, right),
                    Rule(dir(D("Bip01 Spine"), D("Bip01 Spine1")), dHip, up, right));
                Add("Spine1", S("Abdomen"), D("Bip01 Spine1"),
                    Rule(dir(S("Abdomen"), S("Chest")), sSho, up, right),
                    Rule(dir(D("Bip01 Spine1"), D("Bip01 Spine2")), dSho, up, right));
                Add("Spine2", S("Chest"), D("Bip01 Spine2"),
                    Rule(dir(S("Chest"), S("Neck")), sSho, up, right),
                    Rule(dir(D("Bip01 Spine2"), D("Bip01 Neck")), dSho, up, right));
                Add("Neck", S("Neck"), D("Bip01 Neck"),
                    Rule(dir(S("Neck"), S("Head")), sSho, up, right),
                    Rule(dir(D("Bip01 Neck"), D("Bip01 Head")), dSho, up, right));
                // 頭: Quaternius は頭の先の骨、Rocketbox は両目の向きと真上（元の姿勢で頭は起きて前を向く）
                Add("Head", S("Head"), D("Bip01 Head"),
                    Rule(dir(S("Head"), S("Head_end")), sSho, up, right),
                    Rule(() => Vector3.up, () => D("Bip01 REye").position - D("Bip01 LEye").position, up, right));

                foreach (var side in new[] { "L", "R" })
                {
                    var sign = side == "L" ? -1f : 1f;
                    var outward = right * sign;
                    // 鎖骨は写さない（束ねた姿勢の向きのまま。クラスの説明を参照）
                    Add("UpperArm." + side, S("UpperArm." + side), D("Bip01 " + side + " UpperArm"),
                        Rule(dir(S("UpperArm." + side), S("LowerArm." + side)), sPalm(side), outward, fwd),
                        Rule(dir(D("Bip01 " + side + " UpperArm"), D("Bip01 " + side + " Forearm")), dPalm(side), outward, fwd));
                    Add("LowerArm." + side, S("LowerArm." + side), D("Bip01 " + side + " Forearm"),
                        Rule(dir(S("LowerArm." + side), S("Wrist." + side)), sPalm(side), outward, fwd),
                        Rule(dir(D("Bip01 " + side + " Forearm"), D("Bip01 " + side + " Hand")), dPalm(side), outward, fwd));
                    Add("Hand." + side, S("Wrist." + side), D("Bip01 " + side + " Hand"),
                        Rule(dir(S("Wrist." + side), S("Middle1." + side)), sPalm(side), outward, fwd),
                        Rule(dir(D("Bip01 " + side + " Hand"), D("Bip01 " + side + " Finger2")), dPalm(side), outward, fwd));
                }
                foreach (var side in new[] { "L", "R" })
                {
                    Add("UpperLeg." + side, S("UpperLeg." + side), D("Bip01 " + side + " Thigh"),
                        Rule(dir(S("UpperLeg." + side), S("LowerLeg." + side)), sHip, -up, right),
                        Rule(dir(D("Bip01 " + side + " Thigh"), D("Bip01 " + side + " Calf")), dHip, -up, right));
                    Add("LowerLeg." + side, S("LowerLeg." + side), D("Bip01 " + side + " Calf"),
                        Rule(dir(S("LowerLeg." + side), S("LowerLeg." + side + "_end")), sHip, -up, right),
                        Rule(dir(D("Bip01 " + side + " Calf"), D("Bip01 " + side + " Foot")), dHip, -up, right));
                    Add("Foot." + side, S("Foot." + side), D("Bip01 " + side + " Foot"),
                        Rule(() => Flat(S("Foot." + side + "_end").position - S("Foot." + side).position), () => Vector3.up, fwd, up),
                        Rule(() => Flat(D("Bip01 " + side + " Toe0").position - D("Bip01 " + side + " Foot").position), () => Vector3.up, fwd, up));
                }
            }

            static FrameRule Rule(Func<Vector3> aim, Func<Vector3> hint, Vector3 c, Vector3 h)
            {
                return new FrameRule { Aim = aim, Hint = hint, C = c, H = h };
            }

            void Add(string name, Transform sb, Transform db, FrameRule sr, FrameRule dr)
            {
                // dst = src × inv(srcRest) × Fsrc × inv(Fdst) × dstRest（どれも世界の向き）
                var off = Quaternion.Inverse(sb.rotation) * sr.Rest() * Quaternion.Inverse(dr.Rest()) * db.rotation;
                joints.Add(new Joint { Name = name, Src = sb, Dst = db, Offset = off });
            }

            /// <summary>一つの動きを移し替える。24 こま毎秒で読み、筋肉の値の曲線で書く</summary>
            public AnimationClip Convert(AnimationClip clip, out string note)
            {
                var fps = Mathf.Max(24f, clip.frameRate);
                var frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);
                var handler = new HumanPoseHandler(avatar, dst.transform);
                var pose = new HumanPose();
                var muscles = new List<float[]>();
                var bodyP = new List<Vector3>();
                var bodyQ = new List<Quaternion>();
                var lowFoot = float.MaxValue;
                try
                {
                    for (var f = 0; f < frames; f++)
                    {
                        var t = Mathf.Min(clip.length, f / fps);
                        Pose(clip, t);
                        lowFoot = Mathf.Min(lowFoot, Mathf.Min(D("Bip01 L Toe0").position.y, D("Bip01 R Toe0").position.y) - dst.transform.position.y);
                        handler.GetHumanPose(ref pose);
                        // 指は軽く曲げた力の抜けた形（Quaternius の指は写していない）
                        for (var m = 0; m < pose.muscles.Length; m++)
                        {
                            var mn = HumanTrait.MuscleName[m];
                            var finger = (mn.StartsWith("Left", StringComparison.Ordinal) || mn.StartsWith("Right", StringComparison.Ordinal)) && (mn.Contains("Thumb") || mn.Contains("Index") || mn.Contains("Middle") || mn.Contains("Ring") || mn.Contains("Little"));
                            if (!finger) continue;
                            if (mn.EndsWith("Stretched", StringComparison.Ordinal))
                                pose.muscles[m] = mn.Contains("Thumb") ? FingerStretchThumb : FingerStretch[mn.Contains("Index") ? 0 : mn.Contains("Middle") ? 1 : mn.Contains("Ring") ? 2 : 3];
                            else if (mn.EndsWith("Spread", StringComparison.Ordinal)) pose.muscles[m] = mn.Contains("Thumb") ? ThumbSpread : FingerSpread;
                        }
                        muscles.Add((float[])pose.muscles.Clone());
                        bodyP.Add(pose.bodyPosition);
                        bodyQ.Add(pose.bodyRotation);
                    }
                }
                finally
                {
                    handler.Dispose();
                }

                var human = new AnimationClip { name = clip.name, frameRate = fps };
                var bindings = new List<EditorCurveBinding>();
                var curves = new List<AnimationCurve>();
                Action<string, Func<int, float>> put = (attr, val) =>
                {
                    var all = new Keyframe[frames];
                    for (var f = 0; f < frames; f++) all[f] = new Keyframe(f / fps, val(f));
                    // 前後のこまを結ぶ直線から外れない中のこまは落とす（筋肉の値で 0.0005 未満。ファイルを軽くするため）
                    var keys = Reduce(all, attr.StartsWith("Root", StringComparison.Ordinal) ? 0.0002f : 0.0005f);
                    var c = new AnimationCurve(keys);
                    for (var f = 0; f < keys.Length; f++) AnimationUtility.SetKeyLeftTangentMode(c, f, AnimationUtility.TangentMode.ClampedAuto);
                    for (var f = 0; f < keys.Length; f++) AnimationUtility.SetKeyRightTangentMode(c, f, AnimationUtility.TangentMode.ClampedAuto);
                    bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), attr));
                    curves.Add(c);
                };
                put("RootT.x", f => bodyP[f].x);
                put("RootT.y", f => bodyP[f].y);
                put("RootT.z", f => bodyP[f].z);
                put("RootQ.x", f => bodyQ[f].x);
                put("RootQ.y", f => bodyQ[f].y);
                put("RootQ.z", f => bodyQ[f].z);
                put("RootQ.w", f => bodyQ[f].w);
                for (var m = 0; m < HumanTrait.MuscleCount; m++)
                {
                    var mm = m;
                    put(ClipAttribute(HumanTrait.MuscleName[m]), f => muscles[f][mm]);
                }
                AnimationUtility.SetEditorCurves(human, bindings.ToArray(), curves.ToArray());

                var st = AnimationUtility.GetAnimationClipSettings(human);
                st.loopTime = true;
                // 体は運ばせない。向き・高さ・前後左右の動きはどれも姿勢に焼き込み、元の位置を基にする
                st.loopBlendOrientation = true;
                st.loopBlendPositionY = true;
                st.loopBlendPositionXZ = true;
                st.keepOriginalOrientation = true;
                st.keepOriginalPositionY = true;
                st.keepOriginalPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(human, st);

                var drift = bodyP[frames - 1] - bodyP[0];
                note = string.Format("{0} こま、humanMotion={1}、体の中心の高さ {2:0.000}〜{3:0.000}、始めと終わりのずれ {4}、つま先の一番低い所 {5:0.000} m",
                    frames, human.humanMotion, Min(bodyP, 1), Max(bodyP, 1), drift.ToString("F3"), lowFoot);
                return human;
            }

            /// <summary>最初と最後のこまは残し、残したこまと次のこまを結ぶ直線から tol 以上外れるこまだけを残す</summary>
            static Keyframe[] Reduce(Keyframe[] k, float tol)
            {
                if (k.Length <= 2) return k;
                var keep = new List<Keyframe> { k[0] };
                var last = 0;
                for (var i = 1; i < k.Length - 1; i++)
                {
                    var next = k[i + 1];
                    var ok = true;
                    for (var j = last + 1; j <= i; j++)
                    {
                        var t = (k[j].time - k[last].time) / (next.time - k[last].time);
                        if (Mathf.Abs(Mathf.Lerp(k[last].value, next.value, t) - k[j].value) > tol) { ok = false; break; }
                    }
                    if (ok) continue;
                    keep.Add(k[i]);
                    last = i;
                }
                keep.Add(k[k.Length - 1]);
                return keep.ToArray();
            }

            /// <summary>Generic の動きは曲線の無い骨を動かさないので、こまごとに元の姿勢へ戻してから読む</summary>
            void ResetSource()
            {
                foreach (var kv in srcRest) kv.Key.localRotation = kv.Value;
                foreach (var kv in srcRestPos) kv.Key.localPosition = kv.Value;
            }

            static float Min(List<Vector3> v, int axis)
            {
                var m = float.MaxValue;
                foreach (var p in v) m = Mathf.Min(m, p[axis]);
                return m;
            }

            static float Max(List<Vector3> v, int axis)
            {
                var m = float.MinValue;
                foreach (var p in v) m = Mathf.Max(m, p[axis]);
                return m;
            }

            public void Dispose()
            {
                if (src != null) Object.DestroyImmediate(src);
                if (dst != null) Object.DestroyImmediate(dst);
            }
        }

        /// <summary>
        /// 真下からの前後の角 angle（度。前が正）の、前の側だけを scale の割合に縮める。後ろ（0 以下）はそのまま。
        /// 0〜ease 度では縮め方をなだらかに強める（角の変わる速さが 0 度で折れない）
        /// </summary>
        public static float FrontSquash(float angle, float scale, float ease)
        {
            if (angle <= 0f) return angle;
            if (ease <= 0f) return angle * scale;
            if (angle <= ease) return angle - (1f - scale) * angle * angle / (2f * ease);
            return ease * (1f + scale) * 0.5f + scale * (angle - ease);
        }

        /// <summary>
        /// HumanTrait の筋肉の名前を、動きの曲線の名前へ。指だけ書き方が違う
        /// （"Left Thumb 1 Stretched" → "LeftHand.Thumb.1 Stretched"、"Left Thumb Spread" → "LeftHand.Thumb.Spread"）
        /// </summary>
        public static string ClipAttribute(string muscle)
        {
            foreach (var side in new[] { "Left", "Right" })
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    var head = side + " " + finger + " ";
                    if (!muscle.StartsWith(head, StringComparison.Ordinal)) continue;
                    var rest = muscle.Substring(head.Length);
                    if (rest == "Spread") return side + "Hand." + finger + ".Spread";
                    var sp = rest.IndexOf(' ');
                    return side + "Hand." + finger + "." + rest.Substring(0, sp) + rest.Substring(sp);
                }
            return muscle;
        }
    }
}
