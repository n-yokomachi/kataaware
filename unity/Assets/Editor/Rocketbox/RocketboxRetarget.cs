using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 今の主人公の立ちと歩き（Quaternius の W_Suit の骨向けの Generic の動き、Assets/Animation/Idle.anim・Walk.anim）を、
    /// Rocketbox の Humanoid の動きへ移し替える。
    ///
    /// Quaternius の骨は足が脛の子ではなく根の子（足の IK の骨）なので、Unity の Humanoid にそのまま載らない。
    /// そこで一こまずつ、骨ごとの向きを「体の形から決めた向きの枠」同士で合わせて Rocketbox の骨へ写し、
    /// その姿を Rocketbox の Avatar で筋肉の値（HumanPose）に読み直して Humanoid の動きとして書く。
    /// 書いた動きは Humanoid なので、Rocketbox の他の人にもそのまま載る。
    ///
    /// 枠は、骨の向き（子の骨へ向かう向き）を主の軸に、左右（腿・肩・手のひら）の向きを従の軸にして作る。
    /// 両方の骨の、元の姿勢（Quaternius は歩きの途中、Rocketbox は腕を下げた A の字）での枠を T の字の枠に対応させる。
    /// 足だけは、元の姿勢で床に平らに置かれていると見なして、足先の水平の向きと真上で枠を作る
    /// </summary>
    public static class RocketboxRetarget
    {
        public const string OutDir = "Assets/Animation/Humanoid";
        public const string ControllerPath = BuildRocketboxProtagonist.Controller;
        public const string SourceModel = "Assets/Models/quaternius/W_Suit.fbx";
        public const string SourceIdle = "Assets/Animation/Idle.anim";
        public const string SourceWalk = "Assets/Animation/Walk.anim";

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
                if (!AssetDatabase.CopyAsset(BuildProtagonist.Controller, ControllerPath))
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

            public Job()
            {
                src = Spawn(SourceModel, new Vector3(0f, -600f, 0f));
                // HumanPoseHandler は体の位置を世界の原点からの値で返すので、Rocketbox は原点に置く
                dst = Spawn(BuildRocketboxProtagonist.Model, Vector3.zero);
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
                Func<Vector3> sFwd = () => Vector3.Cross(sSho(), S("Neck").position - S("Chest").position);
                Func<Vector3> dFwd = () => Vector3.Cross(dSho(), D("Bip01 Neck").position - D("Bip01 Spine2").position);
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
                    Add("Clavicle." + side, S("Shoulder." + side), D("Bip01 " + side + " Clavicle"),
                        Rule(dir(S("Shoulder." + side), S("UpperArm." + side)), sFwd, outward, fwd),
                        Rule(dir(D("Bip01 " + side + " Clavicle"), D("Bip01 " + side + " UpperArm")), dFwd, outward, fwd));
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
                var dstPelvis = D("Bip01 Pelvis");
                var dstL = D("Bip01 L Thigh");
                var dstR = D("Bip01 R Thigh");
                var srcL = S("UpperLeg.L");
                var srcR = S("UpperLeg.R");
                var lowFoot = float.MaxValue;
                try
                {
                    for (var f = 0; f < frames; f++)
                    {
                        var t = Mathf.Min(clip.length, f / fps);
                        ResetSource();
                        clip.SampleAnimation(src, t);
                        foreach (var j in joints) j.Dst.rotation = j.Src.rotation * j.Offset;
                        // 腰の前後左右: 腿の付け根の中点を、脚の長さの比で縮めた Quaternius の中点へ
                        var srcMid = Mid(srcL, srcR) - src.transform.position;
                        var want = dst.transform.position + srcMid * legScale;
                        var move = want - Mid(dstL, dstR);
                        move.y = 0f;
                        dstPelvis.position += move;
                        // 高さ: 低い方の足が床から浮く量を揃える（着いている足は床に着いたまま）
                        var srcLift = Mathf.Min(S("Foot.L").position.y, S("Foot.R").position.y) - src.transform.position.y - srcFootRest;
                        var dstAnkle = Mathf.Min(D("Bip01 L Foot").position.y, D("Bip01 R Foot").position.y) - dst.transform.position.y;
                        dstPelvis.position += Vector3.up * (dstAnkleRest + srcLift * legScale - dstAnkle);
                        lowFoot = Mathf.Min(lowFoot, Mathf.Min(D("Bip01 L Toe0").position.y, D("Bip01 R Toe0").position.y) - dst.transform.position.y);
                        handler.GetHumanPose(ref pose);
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
