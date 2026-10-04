using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class PlaceProtagonist
    {
        // ---- 映り込みの中の、煙草を吸う右腕 ------------------------------------
        //
        // モニターに映る主人公を、煙草を吸っている最中の姿にする（MirrorSmoking。オーナー、2026-10-05）。
        // 腕を上げるのは映り込みの写しだけで、場面の主人公の右腕は肘掛けに置いたまま（一人称の視界に腕を入れない）。
        // 写しの右腕のボーン（上腕から指先まで）は、場面の主人公のボーンを写して映り込みの一式の下に置き、根を場面の主人公の右の鎖骨のボーンに重ねて動かす。
        // 形は、場面の主人公を写した体（座った形を当てた物。撮る間だけ作って捨てる）の右腕を口元へ届かせて解き、ボーンごとの向きを読み出す。
        // **主人公を組み立て直す道（BuildRocketboxProtagonist.Build）は通らない。** あちらは差込口の mesh を書き直す

        /// <summary>映り込みの中の煙草の紙・フィルター・火のマテリアル</summary>
        public const string MirrorPaperMaterial = "Assets/Materials/Room/MirrorCigarette.mat";
        public const string MirrorFilterMaterial = "Assets/Materials/Room/MirrorCigaretteFilter.mat";
        public const string MirrorEmberMaterial = "Assets/Materials/Room/MirrorCigaretteEmber.mat";
        public const string MirrorAshMaterial = "Assets/Materials/Room/MirrorCigaretteAsh.mat";
        /// <summary>火のにじみのマテリアル（煙の粒の絵を、足し合わせで重ねる）</summary>
        public const string MirrorGlowMaterial = "Assets/Materials/Room/MirrorCigaretteGlow.mat";

        // 解き方の値。撮り比べで決めた（エディタから変えて撮り直せるよう、定数にしない）。向きはどれも体の右・上・前

        /// <summary>
        /// 吸う時の煙草の向き（唇から火の先へ）。口の右寄りから、体の前の斜め上（少し右）へ出す。手のひらは顔の方を向く。
        /// 前は体の右斜め前へ水平に出していたが、横一文字に真横へ突き出して見えた。上へ向けるほど挟む指が上がり、指先が鼻に掛かる
        /// </summary>
        public static Vector3 SmokeOut = new Vector3(0.35f, 0.18f, 1f);
        /// <summary>脇に持った時の煙草の向き。手首が少し下がり、煙草の先がやや上を向く</summary>
        public static Vector3 AsideOut = new Vector3(0.4f, 0.75f, 0.9f);
        /// <summary>
        /// 指の向きの、真上から体の左への傾き（度）。手は口元の右から、力を抜いた指を口の前へ寝かせ気味に左上へ向ける（手のひらは顔の方）。
        /// 立てるほど指先が上がって鼻に掛かり、寝かせるほど口元の左（ほくろ）へ寄る
        /// </summary>
        public static float FingerTilt = 60f;
        /// <summary>脇に持った形の、指の傾きの足し（度）</summary>
        public static float AsideTilt = 10f;
        /// <summary>煙草を当てる所。唇の真ん中から体の右へ（m。口の端は 2.65 cm）。ほくろ（口元の左）と反対の側</summary>
        public static float LipSide = 0.016f;
        /// <summary>煙草を当てる高さの、上唇と下唇のボーンの中ほどからの下がり（m）。下唇寄りに当てて、手と指を低く保つ（指先が鼻の下を越えない）</summary>
        public static float LipDrop = 0.006f;
        /// <summary>煙草の端を唇に食い込ませる深さ（m）。唇で挟んでいる</summary>
        public static float LipBite = 0.003f;
        /// <summary>指で挟む所から、唇の側の端（フィルターの端）までの長さ（m）</summary>
        public static float HoldFromEnd = 0.03f;
        /// <summary>煙草の長さ（吸いかけ）・フィルターの長さ・灰と火の長さ・太さ（半径）。m。群衆の煙草（BuildAlleyCrowdRig）と同じ太さ</summary>
        public static float CigaretteLength = 0.066f;
        public static float FilterLength = 0.021f;
        public static float EmberLength = 0.004f;
        /// <summary>火の芯の太さ（煙草の太さに対する割合）と、灰の縁から火の芯が出る長さ（m）。灰の色の縁の内に、赤〜橙の芯が見える</summary>
        public static float EmberCore = 0.5f;
        /// <summary>火の縁（芯と灰の縁のあいだの暗い赤）の太さ（煙草の太さに対する割合）</summary>
        public static float EmberEdge = 0.84f;
        public static float EmberProud = 0.0006f;
        public static float CigaretteRadius = 0.004f;
        /// <summary>肘を寄せる所。右の上腕の付け根から（m）。肘は体の脇の下の前へ下ろす</summary>
        public static Vector3 ElbowPole = new Vector3(0.22f, -0.35f, 0.12f);
        /// <summary>唇のすぐ脇に持った形の、煙草の唇の側の端の、唇からの離れ（m）。手首が少し下がる</summary>
        public static Vector3 AsideOffset = new Vector3(0.012f, -0.026f, 0.02f);
        /// <summary>
        /// 指の曲げ（付け根・中・先。度）。どの指も力を抜いて軽く曲げ、人差し指と中指で煙草を挟み、薬指と小指はそれより内へ丸め、親指は軽く添える。
        /// 前は指をまっすぐ伸ばして開いていて、顔の前に手のひらをかざしているように見えた
        /// </summary>
        public static Vector3 IndexBend = new Vector3(25f, 40f, 30f);
        public static Vector3 MiddleBend = new Vector3(25f, 42f, 30f);
        public static Vector3 RingBend = new Vector3(30f, 50f, 30f);
        public static Vector3 LittleBend = new Vector3(35f, 52f, 30f);
        public static Vector3 ThumbBend = new Vector3(35f, 30f, 20f);
        /// <summary>親指を人差し指の方へ寄せる角（度）</summary>
        public static float ThumbIn = 30f;
        /// <summary>
        /// 煙草を挟む所。人差し指と中指の、付け根から数えた節の位置（0〜1 が基節、1〜2 が中節、肌の点で測る）。
        /// 第二関節から第一関節のあいだ（中節）の、第一関節寄り（挟んだ所から先の指が短く、指先が鼻の下を越えない）
        /// </summary>
        public static float HoldAt = 1.85f;
        /// <summary>人差し指と中指の間を開く角（度。中指を人差し指から離す）。煙草の太さぶん空ける</summary>
        public static float Spread = 5f;
        /// <summary>薬指と小指を中指へ寄せる割合</summary>
        public static float CloseOthers = 0.6f;

        /// <summary>解いた右腕の形</summary>
        public sealed class SmokingArm
        {
            /// <summary>写すボーンの名（上腕から指先まで。場面の主人公のボーンの名）</summary>
            public readonly List<string> names = new List<string>();
            public readonly List<Quaternion> lips = new List<Quaternion>();
            public readonly List<Quaternion> aside = new List<Quaternion>();
            /// <summary>煙草の置き場。手のボーンから見た、挟む所と、長さの向き（唇の側から火の先へ）を +y にする向き</summary>
            public Vector3 holdLocal;
            public Quaternion holdTurn = Quaternion.identity;
            /// <summary>唇の間（吐いた煙の出る所）。世界の位置</summary>
            public Vector3 mouth;
        }

        /// <summary>
        /// 場面の主人公 her を写した体で、右腕を口元へ届かせ、煙草を唇に当てた形と唇のすぐ脇に持った形を解く。
        /// 測った値（唇へ届いた狂い、手と顔・胴の食い込み、煙草と指の食い込み、ほくろから手までの離れ、指先の高さ）は note へ書く
        /// </summary>
        public static SmokingArm SolveSmokingArm(GameObject her, StringBuilder note)
        {
            var probe = Object.Instantiate(her);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(her.transform.position, her.transform.rotation);
            try
            {
                var an = probe.GetComponent<Animator>();
                var pose = probe.GetComponent<SeatedPose>();
                if (an == null || pose == null) { note.AppendLine("写した体に Animator か座った形が無い"); return null; }
                pose.Seated = true;
                pose.UseAlternate = false;
                pose.Bind();
                pose.Apply();
                var body = probe.transform;
                var skin = SkinPoint.BodyOf(body);
                var shadow = probe.transform.Find("HeadShadow");
                var headMats = shadow != null ? shadow.GetComponent<SkinnedMeshRenderer>().sharedMaterials : skin.sharedMaterials;
                System.Func<Vector3, Vector3> B = v => body.right * v.x + body.up * v.y + body.forward * v.z;
                System.Func<HumanBodyBones, Transform> T = an.GetBoneTransform;

                // 唇。上唇と下唇のボーンの間の高さ、真ん中から体の右へ LipSide の所の、唇の前の面
                var upper = FindDeep(probe.transform, "Bip01 MUpperLip");
                var lower = FindDeep(probe.transform, "Bip01 MBottomLip");
                var cornerL = FindDeep(probe.transform, "Bip01 LMouthCorner");
                var nose = FindDeep(probe.transform, "Bip01 MNose");
                if (upper == null || lower == null || cornerL == null || nose == null) { note.AppendLine("唇か鼻のボーンが無い"); return null; }
                var lipMid = (upper.position + lower.position) * 0.5f;
                var world = Baked(skin);
                var headSet = SubmeshVerts(skin.sharedMesh, headMats, m => m != null && m.name.StartsWith("Head_"));
                var lipFront = Front(world, headSet, lipMid + body.right * LipSide - body.up * LipDrop, body);
                var target = lipFront - body.forward * LipBite;
                var mouthFront = Front(world, headSet, lipMid, body);
                // ほくろ: 口元の左（下唇の端の少し下の外）
                var mole = cornerL.position - body.right * 0.004f - body.up * 0.007f;
                mole = Front(world, headSet, mole, body);
                // 鼻の下（指先がこれを越えない）。上唇のボーンから鼻のボーンへ 35% の所（顔の肌の横顔で測った鼻の下の窪みの高さ）
                var underNose = Vector3.Dot(Vector3.Lerp(upper.position, nose.position, 0.35f) - body.position, body.up);

                // 指の形。まっすぐ（ボーンの基の姿勢）から曲げる。煙草は人差し指と中指の中節で挟み、指の並びと中節の向きに直交させる
                Fingers(an);
                var hand = T(HumanBodyBones.RightHand);
                Vector3 hold, axis;
                float gap;
                Hold(an, skin, out hold, out axis, out gap);
                var holdLocal = hand.InverseTransformPoint(hold);
                var axisLocal = hand.InverseTransformDirection(axis);
                note.AppendFormat("映り込みの煙草を挟む指の間: {0:0.0} mm（煙草の太さ {1:0.0} mm）", gap * 1000f, CigaretteRadius * 2000f).AppendLine();

                var arm = new SmokingArm { mouth = mouthFront + body.forward * 0.004f };
                var chain = new List<Transform>();
                foreach (var t in T(HumanBodyBones.RightUpperArm).GetComponentsInChildren<Transform>(true))
                    if (Bone(skin, t) || t == T(HumanBodyBones.RightUpperArm)) chain.Add(t);
                foreach (var t in chain) arm.names.Add(t.name);

                var shoulder = T(HumanBodyBones.RightUpperArm).position;
                var pole = shoulder + B(ElbowPole);
                Reach(an, hand, holdLocal, axisLocal, target, B(SmokeOut).normalized, Tilted(body, FingerTilt), pole, note, "唇に当てた形");
                Check(an, skin, headMats, hand, holdLocal, axisLocal, mole, underNose, body, note);
                foreach (var t in chain) arm.lips.Add(t.localRotation);

                var aside = target + B(AsideOffset);
                Reach(an, hand, holdLocal, axisLocal, aside, B(AsideOut).normalized, Tilted(body, FingerTilt + AsideTilt), pole, note, "脇に持った形");
                Check(an, skin, headMats, hand, holdLocal, axisLocal, mole, underNose, body, note);
                foreach (var t in chain) arm.aside.Add(t.localRotation);

                arm.holdLocal = holdLocal;
                arm.holdTurn = Quaternion.FromToRotation(Vector3.up, axisLocal);
                return arm;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>体の上から、体の左へ tilt 度倒した向き（顔の前の面の中）</summary>
        static Vector3 Tilted(Transform body, float tilt)
        {
            return Quaternion.AngleAxis(tilt, body.forward) * body.up;
        }

        /// <summary>
        /// 右腕を、煙草（手のボーンから見て holdLocal で挟み、axisLocal へ伸びる）の唇の側の端が target に来るよう届かせる。
        /// 煙草は outward へ向け、その向きのまわりの手のひねりは、指（手から中指の付け根へ）が fingers に近づくように決める。
        /// 手首の狙いを六度詰め直す
        /// </summary>
        static void Reach(Animator an, Transform hand, Vector3 holdLocal, Vector3 axisLocal, Vector3 target, Vector3 outward, Vector3 fingers,
            Vector3 pole, StringBuilder note, string label)
        {
            // 手の枠で見た、煙草の向きと指の向きと手のひらの向き（指の形を決めた後なので変わらない）
            var inv = Quaternion.Inverse(hand.rotation);
            var fL = inv * BodyPoser.FingerDir(an, false);
            var pL = inv * BodyPoser.PalmDir(an, false);
            var local = Quaternion.LookRotation(axisLocal, Vector3.ProjectOnPlane(fL, axisLocal));
            var want = Quaternion.LookRotation(outward, Vector3.ProjectOnPlane(fingers, outward));
            var turn = want * Quaternion.Inverse(local);
            var f = turn * fL;
            var p = turn * pL;
            var wrist = target + outward * HoldFromEnd - f * 0.08f;
            for (var i = 0; i < 6; i++)
            {
                BodyPoser.Arm(an, false, wrist, pole, f, p);
                var end = hand.TransformPoint(holdLocal) - hand.TransformDirection(axisLocal) * HoldFromEnd;
                wrist += target - end;
            }
            BodyPoser.Arm(an, false, wrist, pole, f, p);
            var a = hand.TransformDirection(axisLocal);
            var e = hand.TransformPoint(holdLocal) - a * HoldFromEnd;
            note.AppendFormat("映り込みの右腕（{0}）: 煙草の端と狙いの狂い {1:0.0} mm、煙草の向きの狂い {2:0.0} 度",
                label, (target - e).magnitude * 1000f, Vector3.Angle(a, outward)).AppendLine();
        }

        /// <summary>
        /// 右手の指を、まっすぐから煙草を挟む形へ曲げる。中指を人差し指から少し離し、薬指と小指は中指へ寄せて内へ丸め、
        /// 親指は人差し指の方へ寄せて軽く曲げる
        /// </summary>
        static void Fingers(Animator an)
        {
            for (var b = HumanBodyBones.RightThumbProximal; b <= HumanBodyBones.RightLittleDistal; b++)
            {
                var t = an.GetBoneTransform(b);
                var straight = BodyPoser.Straight(an, b);
                if (t != null && straight.HasValue) t.localRotation = straight.Value;
            }
            BodyPoser.Close(an, false, CloseOthers);
            // 中指（と、寄せた薬指と小指）を人差し指から離す。中指の開く軸のどちら向きが離れるかを測って決め、三本とも同じ回しで
            var index = an.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
            var middle = an.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var tip = an.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
            var axis = BodyPoser.SpreadAxis(an, HumanBodyBones.RightMiddleProximal);
            if (index != null && middle != null && tip != null && axis.sqrMagnitude > 1e-6f)
            {
                var keep = middle.localRotation;
                middle.localRotation = keep * Quaternion.AngleAxis(Spread, axis);
                var plus = Vector3.Distance(tip.position, index.position);
                middle.localRotation = keep * Quaternion.AngleAxis(-Spread, axis);
                var minus = Vector3.Distance(tip.position, index.position);
                middle.localRotation = keep;
                var turn = Quaternion.AngleAxis(plus >= minus ? Spread : -Spread, middle.rotation * axis);
                foreach (var f in new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal })
                {
                    var t = an.GetBoneTransform(f);
                    if (t != null) t.rotation = turn * t.rotation;
                }
            }
            // 親指を人差し指の方へ寄せる（開く軸のどちら向きが近づくかを測って決める）
            var thumb = an.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            var thumbAxis = BodyPoser.SpreadAxis(an, HumanBodyBones.RightThumbProximal);
            var indexRoot = an.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            if (thumb != null && indexRoot != null && thumbAxis.sqrMagnitude > 1e-6f)
            {
                var keep = thumb.localRotation;
                thumb.localRotation = keep * Quaternion.AngleAxis(ThumbIn, thumbAxis);
                var plus = Vector3.Distance(BodyPoser.BoneTip(an, HumanBodyBones.RightThumbDistal), indexRoot.position);
                thumb.localRotation = keep * Quaternion.AngleAxis(-ThumbIn, thumbAxis);
                var minus = Vector3.Distance(BodyPoser.BoneTip(an, HumanBodyBones.RightThumbDistal), indexRoot.position);
                thumb.localRotation = keep * Quaternion.AngleAxis(plus <= minus ? ThumbIn : -ThumbIn, thumbAxis);
            }
            Bend(an, HumanBodyBones.RightIndexProximal, IndexBend);
            Bend(an, HumanBodyBones.RightMiddleProximal, MiddleBend);
            Bend(an, HumanBodyBones.RightRingProximal, RingBend);
            Bend(an, HumanBodyBones.RightLittleProximal, LittleBend);
            Bend(an, HumanBodyBones.RightThumbProximal, ThumbBend);
        }

        /// <summary>指の付け根のボーン first から三つの節を、屈曲の軸まわりに bend（付け根・中・先。度）だけ曲げる</summary>
        static void Bend(Animator an, HumanBodyBones first, Vector3 bend)
        {
            for (var k = 0; k < 3; k++)
            {
                var b = first + k;
                var t = an.GetBoneTransform(b);
                var axis = BodyPoser.FlexAxis(an, b);
                if (t == null || axis.sqrMagnitude < 1e-6f) continue;
                t.localRotation = t.localRotation * Quaternion.AngleAxis(bend[k], axis);
            }
        }

        /// <summary>
        /// 煙草を挟む所。人差し指と中指の、<see cref="HoldAt"/> の節の位置（前後 0.17 節）の肌の真ん中どうしの中点。
        /// axis は煙草の向き（唇の側から火の先へ）。二本の指の並びと、その節の向きの両方に直交させ、手の甲の側へ向ける。
        /// gap はそこでの二本の指の肌の間（m）
        /// </summary>
        static void Hold(Animator an, SkinnedMeshRenderer skin, out Vector3 at, out Vector3 axis, out float gap)
        {
            var world = Baked(skin);
            var weights = skin.sharedMesh.boneWeights;
            var bones = skin.bones;
            var second = HoldAt >= 1f;
            var along = second ? HoldAt - 1f : HoldAt;
            var dirs = Vector3.zero;
            System.Func<HumanBodyBones, List<Vector3>> band = proximal =>
            {
                var p = an.GetBoneTransform(second ? proximal + 1 : proximal);
                var q = an.GetBoneTransform(second ? proximal + 2 : proximal + 1);
                var dir = (q.position - p.position).normalized;
                dirs += dir;
                var len = Vector3.Distance(p.position, q.position);
                var list = new List<Vector3>();
                for (var i = 0; i < world.Length; i++)
                {
                    var w = weights[i];
                    if (w.weight0 < 0.5f || bones[w.boneIndex0] != p) continue;
                    var s = Vector3.Dot(world[i] - p.position, dir) / len;
                    if (Mathf.Abs(s - along) <= 0.17f) list.Add(world[i]);
                }
                return list;
            };
            var index = band(HumanBodyBones.RightIndexProximal);
            var middle = band(HumanBodyBones.RightMiddleProximal);
            System.Func<List<Vector3>, Vector3> mean = l =>
            {
                var m = Vector3.zero;
                foreach (var v in l) m += v;
                return l.Count > 0 ? m / l.Count : Vector3.zero;
            };
            var ci = mean(index);
            var cm = mean(middle);
            at = (ci + cm) * 0.5f;
            axis = Vector3.Cross(dirs.normalized, (ci - cm).normalized).normalized;
            if (Vector3.Dot(axis, -BodyPoser.PalmDir(an, false)) < 0f) axis = -axis;
            gap = float.MaxValue;
            foreach (var a in index)
                foreach (var b in middle)
                    gap = Mathf.Min(gap, Vector3.Distance(a, b));
            if (gap == float.MaxValue) gap = 0f;
        }

        /// <summary>
        /// 解いた形を測って note へ書く。右の手と前腕の肌が顔・首・胴の肌に食い込んでいる点の数と深さ、
        /// 煙草の筒の中に入る指の肌の点の数、煙草の唇の側の端から先で顔の肌に入る点の数、ほくろから手の肌までの正面から見た離れ、
        /// 指先（四本の指の末節の肌）のいちばん高い所の、鼻の下（underNose。体の根からの高さ）からの高さ
        /// </summary>
        static void Check(Animator an, SkinnedMeshRenderer skin, Material[] headMats, Transform hand, Vector3 holdLocal, Vector3 axisLocal,
            Vector3 mole, float underNose, Transform body, StringBuilder note)
        {
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                var v = mesh.vertices;
                var n = mesh.normals;
                for (var i = 0; i < v.Length; i++)
                {
                    v[i] = skin.transform.TransformPoint(v[i]);
                    n[i] = skin.transform.TransformDirection(n[i]);
                }
                var weights = skin.sharedMesh.boneWeights;
                var bones = skin.bones;
                var lowerArm = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var clavR = an.GetBoneTransform(HumanBodyBones.RightShoulder);
                var clavL = an.GetBoneTransform(HumanBodyBones.LeftShoulder);
                var neck = an.GetBoneTransform(HumanBodyBones.Neck);
                var trunk = new HashSet<Transform> { an.GetBoneTransform(HumanBodyBones.Spine), an.GetBoneTransform(HumanBodyBones.Chest), an.GetBoneTransform(HumanBodyBones.UpperChest) };
                var hairSet = SubmeshVerts(skin.sharedMesh, headMats, m => m != null && m.name == "Hair");
                var handPts = new List<int>();
                var restPts = new List<int>();
                for (var i = 0; i < v.Length; i++)
                {
                    var b = bones[weights[i].boneIndex0];
                    if (b == null || hairSet.Contains(i)) continue;
                    if (b == lowerArm || b == hand || b.IsChildOf(hand)) { if (weights[i].weight0 >= 0.5f) handPts.Add(i); continue; }
                    if (clavR != null && (b == clavR || b.IsChildOf(clavR))) continue;
                    if (clavL != null && (b == clavL || b.IsChildOf(clavL))) continue;
                    if (trunk.Contains(b) || (neck != null && (b == neck || b.IsChildOf(neck)))) restPts.Add(i);
                }
                var deep = 0f;
                var deepAt = "";
                var count = 0;
                foreach (var h in handPts)
                {
                    var best = -1;
                    var bd = 0.03f * 0.03f;
                    foreach (var r in restPts)
                    {
                        var d = (v[h] - v[r]).sqrMagnitude;
                        if (d < bd) { bd = d; best = r; }
                    }
                    if (best < 0) continue;
                    var depth = -Vector3.Dot(v[h] - v[best], n[best]);
                    if (depth > 0.002f) { count++; if (depth > deep) { deep = depth; deepAt = bones[weights[h].boneIndex0].name + "→" + bones[weights[best].boneIndex0].name; } }
                }
                // 煙草の筒
                var a = hand.TransformDirection(axisLocal);
                var c = hand.TransformPoint(holdLocal);
                var from = c - a * HoldFromEnd;
                var inFingers = 0;
                var inFace = 0;
                for (var i = 0; i < v.Length; i++)
                {
                    var s = Vector3.Dot(v[i] - from, a);
                    if (s < 0f || s > CigaretteLength) continue;
                    var r = (v[i] - from - a * s).magnitude;
                    if (r > CigaretteRadius * 0.85f) continue;
                    var b = bones[weights[i].boneIndex0];
                    if (b != null && b.IsChildOf(hand) && b != hand) inFingers++;
                    else if (!handPts.Contains(i) && s > 0.006f && !hairSet.Contains(i)) inFace++;
                }
                // ほくろから、手の肌（顔より前にある点）までの、正面から見た離れ
                var near = float.MaxValue;
                foreach (var h in handPts)
                {
                    var d = v[h] - mole;
                    if (Vector3.Dot(d, body.forward) < 0f) continue;
                    near = Mathf.Min(near, new Vector2(Vector3.Dot(d, body.right), Vector3.Dot(d, body.up)).magnitude);
                }
                // 指先の高さ（四本の指の末節）
                var tips = new HashSet<Transform>();
                foreach (var f in new[] { HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleDistal })
                {
                    var t1 = an.GetBoneTransform(f);
                    if (t1 != null) tips.Add(t1);
                }
                var top = float.MinValue;
                for (var i = 0; i < v.Length; i++)
                    if (tips.Contains(bones[weights[i].boneIndex0])) top = Mathf.Max(top, Vector3.Dot(v[i] - body.position, body.up));
                note.AppendFormat("  手と前腕が顔・首・胴に 2 mm より深く入る点 {0}（いちばん深い {1:0.0} mm、{6}）、煙草の筒に入る指の点 {2}、顔や胴の点 {3}、ほくろから手までの正面の離れ {4:0.0} mm、指先の上の端は鼻の下から {5:+0.0;-0.0} mm",
                    count, deep * 1000f, inFingers, inFace, near == float.MaxValue ? -1f : near * 1000f, (top - underNose) * 1000f, deepAt).AppendLine();
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>体の肌の今の形（世界の位置）</summary>
        static Vector3[] Baked(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                var v = mesh.vertices;
                for (var i = 0; i < v.Length; i++) v[i] = skin.transform.TransformPoint(v[i]);
                return v;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>マテリアルの並び mats で which に当たる面の組の、頂点の番号</summary>
        static HashSet<int> SubmeshVerts(Mesh mesh, Material[] mats, System.Func<Material, bool> which)
        {
            var set = new HashSet<int>();
            for (var s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                if (which(mats[s]))
                    foreach (var i in mesh.GetTriangles(s)) set.Add(i);
            return set;
        }

        /// <summary>点 at を、体の前から見て、頂点の組 set の面（いちばん前の点）まで寄せる。at の周り 3 mm の点で測る。無ければ at のまま</summary>
        static Vector3 Front(Vector3[] world, HashSet<int> set, Vector3 at, Transform body)
        {
            var best = float.MinValue;
            foreach (var i in set)
            {
                var d = world[i] - at;
                if (Mathf.Abs(Vector3.Dot(d, body.right)) > 0.003f || Mathf.Abs(Vector3.Dot(d, body.up)) > 0.003f) continue;
                best = Mathf.Max(best, Vector3.Dot(d, body.forward));
            }
            return best == float.MinValue ? at : at + body.forward * best;
        }

        /// <summary>t が体の肌のボーンか</summary>
        static bool Bone(SkinnedMeshRenderer skin, Transform t)
        {
            foreach (var b in skin.bones) if (b == t) return true;
            return false;
        }

        /// <summary>
        /// 映り込みの一式 root の下に、場面の主人公の右腕のボーン（上腕から指先まで）を写す。根（ArmMirror）は右の鎖骨のボーンに重ねる。
        /// 返すのは、場面の主人公のボーンから写しのボーンへの対応（写した順）
        /// </summary>
        static Dictionary<Transform, Transform> MirrorArm(Transform root, Animator an, SkinnedMeshRenderer skin)
        {
            var map = new Dictionary<Transform, Transform>();
            var clavicle = an.GetBoneTransform(HumanBodyBones.RightShoulder);
            var upper = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var top = new GameObject("ArmMirror").transform;
            top.SetParent(root, false);
            top.SetPositionAndRotation(clavicle.position, clavicle.rotation);
            System.Action<Transform, Transform> copy = null;
            copy = (from, parent) =>
            {
                var t = new GameObject("Mirror " + from.name).transform;
                t.SetParent(parent, false);
                t.localPosition = from.localPosition;
                t.localRotation = from.localRotation;
                t.localScale = from.localScale;
                map[from] = t;
                foreach (Transform c in from)
                    if (Bone(skin, c)) copy(c, t);
            };
            copy(upper, top);
            return map;
        }

        /// <summary>
        /// 映り込みの写しの手 hand に煙草（フィルター・紙・灰・火）を持たせる。at は挟む所、turn は長さの向き（唇の側から火の先へ）を +y にする向き（手のボーンから見た値）。
        /// 先は、灰の色の筒（EmberLength）の内に、赤〜橙の火の芯（細い筒、灰の縁から少し出す）。火のにじみは MirrorSmoking が火の先に足す。
        /// どれも映り込みのカメラだけが撮る層に置く。返すのは、フィルター・紙・灰・火のレンダラーと、火の先
        /// </summary>
        static Renderer[] MirrorCigarette(Transform hand, Vector3 at, Quaternion turn, out Renderer ember, out Renderer edge, out Transform tip)
        {
            var go = new GameObject("Cigarette").transform;
            go.SetParent(hand, false);
            go.localPosition = at;
            go.localRotation = turn;
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var d = CigaretteRadius * 2f;
            var start = -HoldFromEnd;
            var paperLength = CigaretteLength - FilterLength - EmberLength;
            System.Func<string, float, float, float, Material, Renderer> part = (name, from, length, wide, mat) =>
            {
                var t = new GameObject(name) { layer = TerminalReflection.MirrorLayer }.transform;
                t.SetParent(go, false);
                t.localPosition = new Vector3(0f, from + length * 0.5f, 0f);
                t.localScale = new Vector3(wide, length * 0.5f, wide);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = cylinder;
                var r = t.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.enabled = false;
                return r;
            };
            var filter = part("Filter", start, FilterLength, d, CigaretteMat(MirrorFilterMaterial, "MirrorCigaretteFilter", new Color(0.80f, 0.58f, 0.34f)));
            var paper = part("Paper", start + FilterLength, paperLength, d, CigaretteMat(MirrorPaperMaterial, "MirrorCigarette", new Color(0.88f, 0.87f, 0.83f)));
            var end = start + CigaretteLength;
            var ash = part("Ash", end - EmberLength, EmberLength, d * 0.98f, CigaretteMat(MirrorAshMaterial, "MirrorCigaretteAsh", new Color(0.40f, 0.39f, 0.38f)));
            edge = part("EmberEdge", end - EmberLength * 0.5f, EmberLength * 0.5f + EmberProud * 0.4f, d * EmberEdge, EmberMat());
            ember = part("Ember", end - EmberLength * 0.5f, EmberLength * 0.5f + EmberProud, d * EmberCore, EmberMat());
            tip = new GameObject("Tip") { layer = TerminalReflection.MirrorLayer }.transform;
            tip.SetParent(go, false);
            tip.localPosition = new Vector3(0f, end + 0.002f, 0f);
            return new[] { filter, paper, ash, edge, ember };
        }

        /// <summary>
        /// 煙草の火のマテリアル。灯りを受けない URP の Unlit で、色は MirrorSmoking が吸う間合いに合わせて変える（ここでは吸っていない時の色）。
        /// 前は URP の Lit の emission で灯していたが、場面を開き直すと _EMISSION のキーワードが外れて、暗い茶色に映った
        /// </summary>
        static Material EmberMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(MirrorEmberMaterial);
            if (m == null)
            {
                m = new Material(shader) { name = "MirrorCigaretteEmber" };
                AssetDatabase.CreateAsset(m, MirrorEmberMaterial);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", MirrorSmoking.EmberDim);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 火のにじみのマテリアル。煙の粒のマテリアル smoke を写し、足し合わせ（Additive）にする。
        /// 色と濃さは MirrorSmoking が吸う間合いに合わせて変える
        /// </summary>
        static Material GlowMat(Material smoke)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MirrorGlowMaterial);
            if (m == null)
            {
                m = new Material(smoke) { name = "MirrorCigaretteGlow" };
                AssetDatabase.CreateAsset(m, MirrorGlowMaterial);
            }
            m.shader = smoke.shader;
            m.SetTexture("_BaseMap", smoke.GetTexture("_BaseMap"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>煙草の紙とフィルターのマテリアル（URP Lit。艶と環境の映り込みは切る）</summary>
        static Material CigaretteMat(string path, string name, Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
