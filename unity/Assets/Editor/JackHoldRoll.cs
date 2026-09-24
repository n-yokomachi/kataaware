using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 左手がジャックを掴む角（JackPull の holdRoll・showRoll・placeRoll、JackPlug の holdRoll・seatRoll）と、
    /// 指の間のジャックの傾き（JackPull の holdTilt・showTilt・placeTilt、JackPlug の holdTilt・seatTilt）を、流れの段ごとに選ぶ。
    ///
    /// ジャックは丸いので、軸まわりのどの角で掴んでも見た目は同じ。ところが掴む手の向きはこの角で決まり、
    /// 悪い角では指が肩の方を向き、手が前腕に対して裏返る（手のひらのひねりが 180 度近く）。
    /// ひねりの骨の無いこの体では、そのとき手首の肌が絞られて腕が極端に細く見えた。
    /// 運ぶ間に角を移すのは、指の間でジャックを転がすのと同じで、これも見た目は変わらない。
    ///
    /// 傾きは、親指の腹と人差し指の腹を結ぶ線（JackHold の X）まわりにジャックを倒す角。二つの腹はジャックに接したまま、
    /// 手がジャックに対して起きたり伏せたりする。運ぶ間に傾きを移すのは、指の間でジャックを起こすのと同じ。
    /// 手首のジャックを掴むには指が右の手首に潜らない傾きが要り、置き場に置くには左手首を曲げすぎない傾きが要って、
    /// 一つの傾きでは両方を満たせなかったので、段ごとに選ぶ。
    /// 角と傾きは、掴むのに使う所（手の骨・親指・人差し指）が右の前腕と手に潜らないように選ぶ。
    /// 中指・薬指・小指は、選んだ後で潜らないところまで曲げを深める（<see cref="Tuck"/>）。
    ///
    /// 流れの段ごとに、角を 15 度ごとに試し、流れを 0.05 秒ごとに止めて左腕の形を測る。
    /// 人の腕の範囲の目安に対して、その段でいちばんはみ出しの小さい角から順に（いちばん悪い一こまで比べる）、
    /// 掴むのに使う所が右の前腕と手に潜らず、ケーブルが体に潜らない最初の角を選ぶ:
    /// - 手のひらのひねり（前腕と手首の和、腕を下ろして手のひらが腿を向く形から）: <see cref="TwistRange"/> 度まで
    /// - 手首の曲げ（手が前腕に対して倒れる角）: <see cref="BendRange"/> 度まで
    /// 段ごとに <see cref="Tilts"/> の傾きをすべて試し、潜りが無く目安に収まる中で、中指・薬指・小指を深める曲げが浅く済む傾きを選ぶ
    /// </summary>
    public static class JackHoldRoll
    {
        public const float Step = 15f;
        public const float TwistRange = 85f;
        public const float BendRange = 65f;
        const float Tick = 0.05f;
        /// <summary>潜りを数えるこまの刻み（秒）。0.025 秒では見落とした潜りがあった</summary>
        const float FineTick = 0.0125f;

        /// <summary>
        /// 角を選ぶとき、ケーブルを体の肌から離しておく隙間（m）。これより近いケーブルの点は、入ったとみなす。
        /// 肌をかすめるだけの潜りは 0.0125 秒のこまの間に収まることがあり、こまの上では 0 でも、こまの間で 1.3 mm 入っていた
        /// </summary>
        const float CableClear = 0.001f;

        /// <summary>試す傾き（度）。倒さない所から、小さい順に</summary>
        public static readonly float[] Tilts = { 0f, 15f, -15f, 30f, -30f };

        /// <summary>抜く流れ。傾きと、掴む・見せる・置くの角を選んで書く</summary>
        public static void ForPull(JackPull pull, StringBuilder note)
        {
            var so = new SerializedObject(pull);
            var pose = (SeatedPose)so.FindProperty("pose").objectReferenceValue;
            Action reset = () => { pull.ResetForStudy(); pull.Bind(); };
            Action<float> step = t => { pull.StepForStudy(t); pull.Apply(t); };
            Func<float, bool> busy = t => PullTimeline.Reach(t) > 0f;
            var phases = new[]
            {
                new Phase("holdRoll", "holdTilt", t => busy(t) && PullTimeline.Show(t) <= 0f && PullTimeline.Place(t) <= 0f, "抜く 掴む"),
                new Phase("showRoll", "showTilt", t => busy(t) && PullTimeline.Show(t) > 0f && PullTimeline.Place(t) <= 0f, "抜く 見せる"),
                new Phase("placeRoll", "placeTilt", t => busy(t) && PullTimeline.Place(t) > 0f, "抜く 置く"),
            };
            Choose(so, pose, PullTimeline.Total, reset, step, phases, note);
            Tuck(so, pose, PullTimeline.Total, reset, step, busy, note);
            Worst(pose, PullTimeline.Total, reset, step, busy, "抜く 流れ全体", note);
            Clear(so, pose, PullTimeline.Total, reset, step, busy, "抜く 流れ全体", note);
        }

        /// <summary>挿す流れ。傾きと、取る・挿すの角を選んで書く</summary>
        public static void ForPlug(JackPlug plug, StringBuilder note)
        {
            var so = new SerializedObject(plug);
            var pose = (SeatedPose)so.FindProperty("pose").objectReferenceValue;
            Action reset = () => { plug.ResetForStudy(); plug.Bind(); };
            Action<float> step = t => { plug.Step(t); plug.Apply(t); };
            Func<float, bool> busy = t => PlugTimeline.Reach(t) > 0f;
            var phases = new[]
            {
                new Phase("holdRoll", "holdTilt", t => busy(t) && PlugTimeline.Carry(t) <= 0f, "挿す 取る"),
                new Phase("seatRoll", "seatTilt", t => busy(t) && PlugTimeline.Carry(t) > 0f, "挿す 運んで挿す"),
            };
            Choose(so, pose, PlugTimeline.Total, reset, step, phases, note);
            Tuck(so, pose, PlugTimeline.Total, reset, step, busy, note);
            Worst(pose, PlugTimeline.Total, reset, step, busy, "挿す 流れ全体", note);
            Clear(so, pose, PlugTimeline.Total, reset, step, busy, "挿す 流れ全体", note);
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
            return ArmReach.RestOf(SkinPoint.BodyOf(an),
                an.GetBoneTransform(HumanBodyBones.LeftUpperArm), an.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                an.GetBoneTransform(HumanBodyBones.LeftHand));
        }

        /// <summary>流れの段。角と傾きを書く欄の名と、段に入るこま</summary>
        sealed class Phase
        {
            public readonly string prop;
            public readonly string tilt;
            public readonly Func<float, bool> frames;
            public readonly string what;

            public Phase(string prop, string tilt, Func<float, bool> frames, string what)
            {
                this.prop = prop;
                this.tilt = tilt;
                this.frames = frames;
                this.what = what;
            }
        }

        /// <summary>
        /// 段の順に、傾き（<see cref="Tilts"/>）と角を選ぶ。傾きごとに角を選び（<see cref="Pick"/>）、いちばんよい傾きをその段の傾きにする。
        /// よさの順は、潜りが少ない、腕の範囲の目安に収まる、掴むのに使わない指を深めきれば右の前腕と手から離せる、
        /// そのままの指が入る頂点が少ない（後で深める曲げが浅く済む）、はみ出しが小さい、の順
        /// </summary>
        static void Choose(SerializedObject so, SeatedPose pose, float total, Action reset, Action<float> step, Phase[] phases, StringBuilder note)
        {
            var best = new Candidate[phases.Length];
            var bestTilt = new float[phases.Length];
            for (var i = 0; i < phases.Length; i++)
            {
                var tried = new StringBuilder();
                tried.AppendFormat("  {0}:", phases[i].what);
                foreach (var tilt in Tilts)
                {
                    // 後の段の傾きも同じにしておく（まだ選んでいない段の傾きへ移る間を、この段のこまに混ぜない）
                    for (var j = i; j < phases.Length; j++) so.FindProperty(phases[j].tilt).floatValue = tilt;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var c = Pick(so, phases[i].prop, pose, total, reset, step, phases[i].frames);
                    if (c == null) continue;
                    tried.AppendFormat(" 傾き {0:0} 角 {1:0}・{2:0.00}（{3:0}/{4:0}）・潜り {5}+{6}・指 {7}/{8}", tilt, c.roll, c.worst,
                        c.twist, c.bend, c.clash, c.cable, c.loose, c.stuck);
                    if (best[i] == null || Better(c, best[i])) { best[i] = c; bestTilt[i] = tilt; }
                }
                note.AppendLine(tried.ToString());
                if (best[i] == null) continue;
                so.FindProperty(phases[i].tilt).floatValue = bestTilt[i];
                for (var j = i + 1; j < phases.Length; j++) so.FindProperty(phases[j].tilt).floatValue = bestTilt[i];
                so.FindProperty(phases[i].prop).floatValue = best[i].roll;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            reset();
            pose.Apply();
            for (var i = 0; i < phases.Length; i++)
            {
                var c = best[i];
                if (c == null) continue;
                note.AppendFormat("{0}: 傾き {1:0} 度・角 {2:0} 度（左の手のひらのひねり いちばん大きいこまで {3:0} 度、手首の曲げ {4:0} 度。{5} 秒ごとに数えて、掴むのに使う所が右の前腕と手に入った頂点 {6}、ケーブルが体の肌から {8:0} mm の内に来た頂点 {7}）",
                    phases[i].what, bestTilt[i], c.roll, c.twist, c.bend, FineTick, c.clash, c.cable, CableClear * 1000f).AppendLine();
            }
        }

        /// <summary>角ひとつの成績どうしを比べる。a の方がよいか</summary>
        static bool Better(Candidate a, Candidate b)
        {
            var bad = (a.clash + a.cable).CompareTo(b.clash + b.cable);
            if (bad != 0) return bad < 0;
            var inA = a.worst <= 1f;
            var inB = b.worst <= 1f;
            if (inA != inB) return inA;
            if (!inA) return a.worst < b.worst - 1e-3f;
            if ((a.stuck == 0) != (b.stuck == 0)) return a.stuck == 0;
            if (a.loose != b.loose) return a.loose < b.loose;
            return a.worst < b.worst - 1e-3f;
        }

        /// <summary>
        /// frames のこまで、角 prop を 15 度ごとに試して並べる。腕の範囲の目安に収まる角を先に、その中では
        /// 掴むのに使わない指を深めきれば右の前腕と手から離せる角、そのままの指が入る頂点の少ない角の順。目安を越える角は、いちばん悪いこまがましな順。
        /// 並べた順に、掴むのに使う所が右の前腕と手に潜らないか、ケーブルが体に潜らないかを数え（まず 0.05 秒ごと、潜らなければ細かいこま
        /// <see cref="FineTick"/> 秒ごと）、どちらも 0 の最初の角を書いて返す（どの角も潜るなら、いちばんましな角）
        /// </summary>
        static Candidate Pick(SerializedObject so, string prop, SeatedPose pose, float total, Action reset, Action<float> step,
            Func<float, bool> frames)
        {
            var roll = so.FindProperty(prop);
            var an = pose.Animator;
            var rest = RestOfLeft(an);
            var leftHand = Pinching(an);
            var looseFingers = new HashSet<Transform>();
            foreach (var finger in Loose)
                foreach (var b in finger) looseFingers.Add(an.GetBoneTransform(b));
            var rightArm = Study.BodyStudy.Hand(an, false, true);
            var cable = UnityEngine.Object.FindFirstObjectByType<Cable>();
            var shapes = new[] { so.FindProperty("pinch"), so.FindProperty("open") };
            var start = new[] { Read(shapes[0]), Read(shapes[1]) };
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
                    // 目安に収まる角は、掴むのに使わない指が右の前腕と手に入る頂点も数える（後で深める曲げが浅く済む角を先にする）
                    if (c.worst <= 1f)
                    {
                        c.loose = LooseClash(pose, total, reset, step, frames, looseFingers, rightArm);
                        // 深めきっても潜る角は、深める曲げでは直せない
                        for (var s = 0; s < shapes.Length; s++) Write(shapes[s], Tucked(an, start[s], TuckMost));
                        so.ApplyModifiedPropertiesWithoutUndo();
                        c.stuck = LooseClash(pose, total, reset, step, frames, looseFingers, rightArm);
                        for (var s = 0; s < shapes.Length; s++) Write(shapes[s], start[s]);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    ranked.Add(c);
                }
                ranked.Sort((a, b) =>
                {
                    var inA = a.worst <= 1f;
                    var inB = b.worst <= 1f;
                    if (inA != inB) return inA ? -1 : 1;
                    if (inA && (a.stuck == 0) != (b.stuck == 0)) return a.stuck == 0 ? -1 : 1;
                    if (inA && a.loose != b.loose) return a.loose.CompareTo(b.loose);
                    return Mathf.Abs(a.worst - b.worst) > 1e-3f ? a.worst.CompareTo(b.worst) : a.mean.CompareTo(b.mean);
                });
                // 上から順に潜りを数える。目安に収まる角はすべて、越える角は上から 5 まで（重いので）
                var inLimit = ranked.FindAll(x => x.worst <= 1f).Count;
                // 潜りは、掴むのに使わない指を深めきった形で数える（その指は後で要るだけ深める。ゆるいままの指がケーブルを横切るのは数えない）
                for (var s = 0; s < shapes.Length; s++) Write(shapes[s], Tucked(an, start[s], TuckMost));
                so.ApplyModifiedPropertiesWithoutUndo();
                for (var i = 0; i < ranked.Count && i < inLimit + 5; i++)
                {
                    var c = ranked[i];
                    roll.floatValue = c.roll;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Clashes(c, Tick, total, reset, step, frames, pose, leftHand, rightArm, cable, CableClear);
                    if (c.clash + c.cable == 0) Clashes(c, FineTick, total, reset, step, frames, pose, leftHand, rightArm, cable, CableClear);
                    if (chosen == null || c.clash + c.cable < chosen.clash + chosen.cable) chosen = c;
                    if (c.clash + c.cable == 0) break;
                }
            }
            finally
            {
                for (var s = 0; s < shapes.Length; s++) Write(shapes[s], start[s]);
                if (chosen != null) roll.floatValue = chosen.roll;
                so.ApplyModifiedPropertiesWithoutUndo();
                reset();
                pose.Apply();
            }
            return chosen;
        }

        /// <summary>角 c を、tick 秒ごとのこまで、左手が右の前腕と手に入った頂点と、ケーブルが体に入った頂点を数えて c へ書く</summary>
        static void Clashes(Candidate c, float tick, float total, Action reset, Action<float> step, Func<float, bool> frames,
            SeatedPose pose, HashSet<Transform> leftHand, HashSet<Transform> rightArm, Cable cable, float cableClear)
        {
            var an = pose.Animator;
            c.clash = 0;
            c.cable = 0;
            reset();
            for (var t = 0f; t <= total + 1e-4f; t += tick)
            {
                pose.Apply();
                step(t);
                if (!frames(t)) continue;
                c.clash += Study.BodyStudy.PartInPart(an.gameObject, leftHand, rightArm);
                if (cable == null) continue;
                var points = new List<Vector3>();
                var normals = new List<Vector3>();
                Study.BodyStudy.Surface(an.gameObject, points, normals);
                c.cable += Study.BodyStudy.Inside(Study.BodyStudy.CablePoints(cable), points, normals, cableClear);
            }
        }

        /// <summary>掴むのに使う所（左手の手の骨・親指・人差し指）。角と傾きは、ここが右の前腕と手に潜らないように選ぶ</summary>
        static HashSet<Transform> Pinching(Animator an)
        {
            var set = new HashSet<Transform> { an.GetBoneTransform(HumanBodyBones.LeftHand) };
            foreach (var b in new[]
            {
                HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
                HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            })
            {
                var t = an.GetBoneTransform(b);
                if (t != null) set.Add(t);
            }
            return set;
        }

        /// <summary>掴むのに使わない指（中指・薬指・小指）。付け根・中・先の順</summary>
        static readonly HumanBodyBones[][] Loose =
        {
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
        };

        /// <summary>掴むのに使わない指を、右の前腕と手から離すために深める曲げの刻みと上限（度、付け根と中の関節。先の関節はその半分）</summary>
        public const float TuckStep = 5f;
        public const float TuckMost = 40f;

        /// <summary>
        /// 選んだ傾きと角のまま、中指・薬指・小指を、流れの間（busy のこま）右の前腕と手に潜らないところまで深める。
        /// 指ごとに、つまむ形（pinch）と開いた形（open）の曲げを <see cref="TuckStep"/> 度ずつ深め、潜りが 0 になる最も浅い所で止める。
        /// ゆるく曲げた指は、手首のジャックを掴むと右の親指の付け根に潜ることがあり、角と傾きだけでは避けきれなかった
        /// </summary>
        static void Tuck(SerializedObject so, SeatedPose pose, float total, Action reset, Action<float> step, Func<float, bool> busy, StringBuilder note)
        {
            var an = pose.Animator;
            var rightArm = Study.BodyStudy.Hand(an, false, true);
            var cable = UnityEngine.Object.FindFirstObjectByType<Cable>();
            var shapes = new[] { so.FindProperty("pinch"), so.FindProperty("open") };
            var line = new StringBuilder("掴むのに使わない指を深めた曲げ:");
            pose.Seated = true;
            pose.Bind();
            try
            {
                foreach (var finger in Loose)
                {
                    var bones = new HashSet<Transform>();
                    foreach (var b in finger) bones.Add(an.GetBoneTransform(b));
                    var start = new[] { Read(shapes[0]), Read(shapes[1]) };
                    var extra = 0f;
                    // まず 0.05 秒ごとのこまで浅い方から探し、潜らなくなった所を細かいこまで確かめる（潜れば一刻み深める）
                    for (; extra <= TuckMost; extra += TuckStep)
                    {
                        for (var s = 0; s < shapes.Length; s++) Write(shapes[s], Curl(an, start[s], finger, extra));
                        so.ApplyModifiedPropertiesWithoutUndo();
                        if (Clash(pose, total, Tick, reset, step, busy, bones, rightArm, cable) == 0
                            && Clash(pose, total, FineTick, reset, step, busy, bones, rightArm, cable) == 0) break;
                    }
                    extra = Mathf.Min(extra, TuckMost);
                    for (var s = 0; s < shapes.Length; s++) Write(shapes[s], Curl(an, start[s], finger, extra));
                    so.ApplyModifiedPropertiesWithoutUndo();
                    line.AppendFormat(" {0} {1:0} 度", finger[0].ToString().Replace("Left", "").Replace("Proximal", ""), extra);
                }
            }
            finally
            {
                reset();
                pose.Apply();
            }
            note.AppendLine(line.ToString());
        }

        /// <summary>
        /// tick 秒ごとのこま（busy のこま）で、骨の組 bones に付いた頂点が右の前腕と手 rightArm に入った数と、
        /// ケーブルの点が bones に付いた肌の中に入った数の和。一つ見つけたら止める
        /// </summary>
        static int Clash(SeatedPose pose, float total, float tick, Action reset, Action<float> step, Func<float, bool> busy,
            HashSet<Transform> bones, HashSet<Transform> rightArm, Cable cable)
        {
            var an = pose.Animator;
            var clash = 0;
            reset();
            for (var t = 0f; t <= total + 1e-4f && clash == 0; t += tick)
            {
                pose.Apply();
                step(t);
                if (!busy(t)) continue;
                clash += Study.BodyStudy.PartInPart(an.gameObject, bones, rightArm);
                if (cable != null) clash += CableIn(an.gameObject, cable, bones);
            }
            return clash;
        }

        /// <summary>ケーブルの点のうち、体の骨の組 part に付いた肌の中に入った数</summary>
        static int CableIn(GameObject body, Cable cable, HashSet<Transform> part)
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || SkinPoint.Rides(smr)) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                var w = smr.sharedMesh.boneWeights;
                var bones = smr.bones;
                for (var i = 0; i < v.Length; i++)
                {
                    if (!part.Contains(bones[w[i].boneIndex0])) continue;
                    points.Add(smr.transform.TransformPoint(v[i]));
                    normals.Add(smr.transform.TransformDirection(n[i]).normalized);
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            return Study.BodyStudy.Inside(Study.BodyStudy.CablePoints(cable), points, normals, 0f);
        }

        /// <summary>0.05 秒ごとのこま（frames のこま）で、骨の組 bones に付いた頂点が右の前腕と手 rightArm に入った数の和</summary>
        static int LooseClash(SeatedPose pose, float total, Action reset, Action<float> step, Func<float, bool> frames,
            HashSet<Transform> bones, HashSet<Transform> rightArm)
        {
            var an = pose.Animator;
            var n = 0;
            reset();
            for (var t = 0f; t <= total + 1e-4f; t += Tick)
            {
                pose.Apply();
                step(t);
                if (frames(t)) n += Study.BodyStudy.PartInPart(an.gameObject, bones, rightArm);
            }
            return n;
        }

        /// <summary>形 shape の、掴むのに使わない指を三本とも extra 度深めた形</summary>
        static SeatedPose.Bone[] Tucked(Animator an, SeatedPose.Bone[] shape, float extra)
        {
            foreach (var finger in Loose) shape = Curl(an, shape, finger, extra);
            return shape;
        }

        /// <summary>形 shape のうち、指 finger の骨を、付け根と中の関節は extra 度、先の関節はその半分だけ、握る向きへ深めた形</summary>
        static SeatedPose.Bone[] Curl(Animator an, SeatedPose.Bone[] shape, HumanBodyBones[] finger, float extra)
        {
            var result = (SeatedPose.Bone[])shape.Clone();
            for (var i = 0; i < result.Length; i++)
            {
                var j = Array.IndexOf(finger, result[i].bone);
                if (j < 0) continue;
                var degrees = j < 2 ? extra : extra * 0.5f;
                result[i].rotation = result[i].rotation * Quaternion.AngleAxis(degrees, BodyPoser.FlexAxis(an, result[i].bone));
            }
            return result;
        }

        static SeatedPose.Bone[] Read(SerializedProperty p)
        {
            var bones = new SeatedPose.Bone[p.arraySize];
            for (var i = 0; i < bones.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                bones[i].bone = (HumanBodyBones)e.FindPropertyRelative("bone").intValue;
                bones[i].position = e.FindPropertyRelative("position").vector3Value;
                bones[i].rotation = e.FindPropertyRelative("rotation").quaternionValue;
            }
            return bones;
        }

        static void Write(SerializedProperty p, SeatedPose.Bone[] bones)
        {
            p.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("bone").intValue = (int)bones[i].bone;
                e.FindPropertyRelative("position").vector3Value = bones[i].position;
                e.FindPropertyRelative("rotation").quaternionValue = bones[i].rotation;
            }
        }

        /// <summary>流れ全体を <see cref="FineTick"/> 秒ごとに止めて、左手（指も）が右の前腕と手に入った頂点と、ケーブルが体に入った頂点を数えて書く</summary>
        static void Clear(SerializedObject so, SeatedPose pose, float total, Action reset, Action<float> step, Func<float, bool> busy, string what, StringBuilder note)
        {
            var an = pose.Animator;
            var c = new Candidate();
            pose.Seated = true;
            pose.Bind();
            try
            {
                Clashes(c, FineTick, total, reset, step, busy, pose, Study.BodyStudy.Hand(an, true, false), Study.BodyStudy.Hand(an, false, true),
                    UnityEngine.Object.FindFirstObjectByType<Cable>(), 0f);
            }
            finally
            {
                reset();
                pose.Apply();
            }
            note.AppendFormat("{0}: {1} 秒ごとに数えて、左手が右の前腕と手に入った頂点 {2}、ケーブルが体に入った頂点 {3}", what, FineTick, c.clash, c.cable).AppendLine();
        }

        /// <summary>試した角ひとつの成績</summary>
        sealed class Candidate
        {
            public float roll, worst, mean, twist, bend;
            public int clash, cable;
            /// <summary>掴むのに使わない指が、右の前腕と手に入った頂点（0.05 秒ごとのこまの和）</summary>
            public int loose;
            /// <summary>その指を <see cref="TuckMost"/> 度深めても、まだ入る頂点</summary>
            public int stuck;
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
