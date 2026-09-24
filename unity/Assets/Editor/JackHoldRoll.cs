using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 左手がジャックを掴む角（JackPull の holdRoll・showRoll・placeRoll、JackPlug の holdRoll・seatRoll）を選ぶ。
    ///
    /// ジャックは丸いので、軸まわりのどの角で掴んでも見た目は同じ。ところが掴む手の向きはこの角で決まり、
    /// 悪い角では指が肩の方を向き、手が前腕に対して裏返る（手のひらのひねりが 180 度近く）。
    /// ひねりの骨の無いこの体では、そのとき手首の肌が絞られて腕が極端に細く見えた。
    /// 運ぶ間に角を移すのは、指の間でジャックを転がすのと同じで、これも見た目は変わらない。
    ///
    /// 流れの段ごとに、角を 15 度ごとに試し、流れを 0.05 秒ごとに止めて左腕の形を測る。
    /// 人の腕の範囲の目安に対して、その段でいちばんはみ出しの小さい角から順に（いちばん悪い一こまで比べる）、
    /// 左手が右の前腕と手に潜らず、ケーブルが体に潜らない最初の角を選ぶ:
    /// - 手のひらのひねり（前腕と手首の和、腕を下ろして手のひらが腿を向く形から）: <see cref="TwistRange"/> 度まで
    /// - 手首の曲げ（手が前腕に対して倒れる角）: <see cref="BendRange"/> 度まで
    /// </summary>
    public static class JackHoldRoll
    {
        public const float Step = 15f;
        public const float TwistRange = 85f;
        public const float BendRange = 65f;
        const float Tick = 0.05f;
        /// <summary>潜りを数えるこまの刻み（秒）。0.025 秒では見落とした潜りがあった</summary>
        const float FineTick = 0.0125f;

        /// <summary>抜く流れ。掴む・見せる・置くの角を、段の順に選んで書く</summary>
        public static void ForPull(JackPull pull, StringBuilder note)
        {
            var so = new SerializedObject(pull);
            var pose = (SeatedPose)so.FindProperty("pose").objectReferenceValue;
            Action reset = () => { pull.ResetForStudy(); pull.Bind(); };
            Action<float> step = t => { pull.StepForStudy(t); pull.Apply(t); };
            Func<float, bool> busy = t => PullTimeline.Reach(t) > 0f;
            Pick(so, "holdRoll", pose, PullTimeline.Total, reset, step,
                t => busy(t) && PullTimeline.Show(t) <= 0f && PullTimeline.Place(t) <= 0f, "抜く 掴む", note);
            Pick(so, "showRoll", pose, PullTimeline.Total, reset, step,
                t => busy(t) && PullTimeline.Show(t) > 0f && PullTimeline.Place(t) <= 0f, "抜く 見せる", note);
            Pick(so, "placeRoll", pose, PullTimeline.Total, reset, step,
                t => busy(t) && PullTimeline.Place(t) > 0f, "抜く 置く", note);
            Worst(pose, PullTimeline.Total, reset, step, busy, "抜く 流れ全体", note);
        }

        /// <summary>挿す流れ。取る・挿すの角を、段の順に選んで書く</summary>
        public static void ForPlug(JackPlug plug, StringBuilder note)
        {
            var so = new SerializedObject(plug);
            var pose = (SeatedPose)so.FindProperty("pose").objectReferenceValue;
            Action reset = () => { plug.ResetForStudy(); plug.Bind(); };
            Action<float> step = t => { plug.Step(t); plug.Apply(t); };
            Func<float, bool> busy = t => PlugTimeline.Reach(t) > 0f;
            Pick(so, "holdRoll", pose, PlugTimeline.Total, reset, step,
                t => busy(t) && PlugTimeline.Carry(t) <= 0f, "挿す 取る", note);
            Pick(so, "seatRoll", pose, PlugTimeline.Total, reset, step,
                t => busy(t) && PlugTimeline.Carry(t) > 0f, "挿す 運んで挿す", note);
            Worst(pose, PlugTimeline.Total, reset, step, busy, "挿す 流れ全体", note);
        }

        /// <summary>左腕の、人の腕の範囲からのはみ出し（1 で目安の境）。ひねりと曲げの大きい方</summary>
        public static float Score(Animator an, ArmReach.Rest rest, out float twist, out float bend)
        {
            var lower = an.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand = an.GetBoneTransform(HumanBodyBones.LeftHand);
            float fa, wr;
            ArmReach.Twists(lower, hand, rest, out fa, out wr);
            twist = Mathf.Abs(Mathf.DeltaAngle(0f, fa + wr));
            bend = ArmReach.WristBend(lower, hand, rest);
            return Mathf.Max(twist / TwistRange, bend / BendRange);
        }

        static ArmReach.Rest RestOfLeft(Animator an)
        {
            return ArmReach.RestOf(an.GetComponentInChildren<SkinnedMeshRenderer>(),
                an.GetBoneTransform(HumanBodyBones.LeftUpperArm), an.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                an.GetBoneTransform(HumanBodyBones.LeftHand));
        }

        /// <summary>
        /// frames に当たるこまで、角 prop を 15 度ごとに試し、いちばん悪いこまがいちばんましな角から順に並べる。
        /// 並べた順に、細かいこま（<see cref="FineTick"/> 秒ごと）で左手が右の前腕と手に潜らないか、ケーブルが体に潜らないかを数え、
        /// どちらも 0 の最初の角を書く（どの角も潜るなら、いちばんましな角）
        /// </summary>
        static void Pick(SerializedObject so, string prop, SeatedPose pose, float total, Action reset, Action<float> step,
            Func<float, bool> frames, string what, StringBuilder note)
        {
            var roll = so.FindProperty(prop);
            var an = pose.Animator;
            var rest = RestOfLeft(an);
            var leftHand = Study.BodyStudy.Hand(an, true, false);
            var rightArm = Study.BodyStudy.Hand(an, false, true);
            var cable = UnityEngine.Object.FindFirstObjectByType<Cable>();
            var ranked = new List<Candidate>();
            pose.Seated = true;
            pose.Bind();
            Candidate chosen = null;
            try
            {
                for (var r = 0f; r < 360f; r += Step)
                {
                    roll.floatValue = r;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    reset();
                    var c = new Candidate { roll = r };
                    float sum = 0f;
                    var n = 0;
                    for (var t = 0f; t <= total + 1e-4f; t += Tick)
                    {
                        pose.Apply();
                        step(t);
                        if (!frames(t)) continue;
                        float twist, bend;
                        var score = Score(an, rest, out twist, out bend);
                        c.worst = Mathf.Max(c.worst, score);
                        c.twist = Mathf.Max(c.twist, twist);
                        c.bend = Mathf.Max(c.bend, bend);
                        sum += score;
                        n++;
                    }
                    c.mean = n > 0 ? sum / n : 0f;
                    ranked.Add(c);
                }
                ranked.Sort((a, b) => Mathf.Abs(a.worst - b.worst) > 1e-3f ? a.worst.CompareTo(b.worst) : a.mean.CompareTo(b.mean));
                // 上から順に、細かいこまで潜りを数える。重いので、形の良い方から 10 まで
                for (var i = 0; i < ranked.Count && i < 10; i++)
                {
                    var c = ranked[i];
                    roll.floatValue = c.roll;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    reset();
                    for (var t = 0f; t <= total + 1e-4f; t += FineTick)
                    {
                        pose.Apply();
                        step(t);
                        if (!frames(t)) continue;
                        c.clash += Study.BodyStudy.PartInPart(an.gameObject, leftHand, rightArm);
                        if (cable != null)
                        {
                            var points = new List<Vector3>();
                            var normals = new List<Vector3>();
                            Study.BodyStudy.Surface(an.gameObject, points, normals);
                            c.cable += Study.BodyStudy.Inside(Study.BodyStudy.CablePoints(cable), points, normals, 0f);
                        }
                    }
                    if (chosen == null || c.clash + c.cable < chosen.clash + chosen.cable) chosen = c;
                    if (c.clash + c.cable == 0) break;
                }
            }
            finally
            {
                if (chosen != null) roll.floatValue = chosen.roll;
                so.ApplyModifiedPropertiesWithoutUndo();
                reset();
                pose.Apply();
            }
            if (chosen == null) return;
            note.AppendFormat("{0}: 角 {1:0} 度（左の手のひらのひねり いちばん大きいこまで {2:0} 度、手首の曲げ {3:0} 度。{4} 秒ごとに数えて、左手が右の前腕と手に入った頂点 {5}、ケーブルが体に入った頂点 {6}）",
                what, chosen.roll, chosen.twist, chosen.bend, FineTick, chosen.clash, chosen.cable).AppendLine();
        }

        /// <summary>試した角ひとつの成績</summary>
        sealed class Candidate
        {
            public float roll, worst, mean, twist, bend;
            public int clash, cable;
        }

        /// <summary>今の角のまま、流れ全体でいちばん悪いこまを書く</summary>
        static void Worst(SeatedPose pose, float total, Action reset, Action<float> step, Func<float, bool> frames, string what, StringBuilder note)
        {
            var an = pose.Animator;
            var rest = RestOfLeft(an);
            float twistMax = 0f, bendMax = 0f, twistAt = 0f, bendAt = 0f;
            pose.Seated = true;
            pose.Bind();
            reset();
            try
            {
                for (var t = 0f; t <= total + 1e-4f; t += Tick)
                {
                    pose.Apply();
                    step(t);
                    if (!frames(t)) continue;
                    float twist, bend;
                    Score(an, rest, out twist, out bend);
                    if (twist > twistMax) { twistMax = twist; twistAt = t; }
                    if (bend > bendMax) { bendMax = bend; bendAt = t; }
                }
            }
            finally
            {
                reset();
                pose.Apply();
            }
            note.AppendFormat("{0}: 左の手のひらのひねり いちばん大きいこま {1:0} 度（{2:0.00} 秒）、手首の曲げ {3:0} 度（{4:0.00} 秒）。目安はひねり {5:0}・曲げ {6:0} 度まで",
                what, twistMax, twistAt, bendMax, bendAt, TwistRange, BendRange).AppendLine();
        }
    }
}
