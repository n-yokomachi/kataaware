using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の歩く人（オーナー、2026-09-30「モブに動きがなさ過ぎて流石に違和感がある」）。
    ///
    /// 群衆の置き場のうち、道筋を持つ人（<see cref="Walk"/>）だけは、焼いた形ではなく Rocketbox の模型をそのまま置いて骨で動かす
    /// （<see cref="PersonMotion"/>。場面 4 の記憶の人と同じ作りで、立ち・歩きの動きを一秒 12 こまで置く）。道筋を歩かせるのは <see cref="CrowdWalk"/>。
    /// 人と服の色・インプラントは、その置き場に焼いた人を立てた時と同じ（乱数は同じ順に引く）。三角は近さの段の近い方と同じ 4,500 ほどに減らす（骨の重みは残す）
    /// </summary>
    public static partial class BuildAlleyCrowd
    {
        /// <summary>歩く人の道筋。place は群衆の置き場の番号（BuildAlley の置き場の順）</summary>
        public struct Walk
        {
            public int place;
            /// <summary>道筋の点。世界の座（Alley は原点に置いてある）</summary>
            public Vector3[] points;
            /// <summary>点ごとに立ち止まる秒</summary>
            public float[] waits;
            /// <summary>立ち止まって向く向き。度。NaN なら歩いてきた向き</summary>
            public float[] faces;
            public float speed;
            /// <summary>始まりの位置（道筋の頭から尻を 0〜1、戻りを 1〜2）</summary>
            public float start;
            /// <summary>両端では写っていない時まで待ってから向きを変える</summary>
            public bool hidden;
        }

        /// <summary>一人の模型を歩く人にする。道筋の頭に置く</summary>
        static GameObject MakeWalker(Transform parent, int index, Appearance a, Walk w, out int tris)
        {
            var who = a.who;
            var mats = Materials(a, 256, false, a.nth.ToString());
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Alley crowd/Import the people）: " + who.Model);
            var root = new GameObject(string.Format("P{0:00}_{1}", index, who.Name)).transform;
            root.SetParent(parent, false);
            root.position = w.points[0];
            root.rotation = Quaternion.LookRotation(Heading(w.points[1] - w.points[0]), Vector3.up);
            root.localScale = Vector3.one * a.place.scale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
            model.name = "Figure";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var shapes = new Mesh[skins.Length];
            tris = 0;
            for (var i = 0; i < skins.Length; i++)
            {
                shapes[i] = WalkerMesh(who, skins[i].sharedMesh);
                Dress(skins[i], shapes[i], mats);
                tris += shapes[i].triangles.Length / 3;
            }

            var female = who.Name.Contains("Female");
            var clips = new[] { BuildDiveCast.Clip(female, "Idle"), BuildDiveCast.Clip(female, "Walk"), BuildDiveCast.Clip(female, "Run") };
            var motion = root.gameObject.AddComponent<PersonMotion>();
            RigWalker(motion, animator, model.transform, clips, Vector3.zero, new float[0], 2.4f, 0f);
            var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            var strides = new float[PersonMotion.Reaches.Length];
            for (var i = 0; i < strides.Length; i++)
                strides[i] = BuildDive.FigureStride(root, model, motion, clips[0], clips[1], PersonMotion.Reaches[i], feet);
            // 床に下ろす。立ちの頭の一こまの靴の裏を根の高さへ
            motion.Sample(clips[0], 0f);
            var drop = -Lowest(root, skins);
            var seat = new Vector3(0f, drop, 0f);
            model.transform.localPosition = seat;
            RigWalker(motion, animator, model.transform, clips, seat, strides, 2.4f, CrowdIdle.Hash(index, 21) * 0.5f + 0.5f);

            var walk = root.gameObject.AddComponent<CrowdWalk>();
            var so = new SerializedObject(walk);
            var pts = so.FindProperty("points");
            pts.arraySize = w.points.Length;
            for (var i = 0; i < w.points.Length; i++) pts.GetArrayElementAtIndex(i).vector3Value = parent.InverseTransformPoint(w.points[i]);
            var pw = so.FindProperty("waits");
            pw.arraySize = w.points.Length;
            for (var i = 0; i < w.points.Length; i++) pw.GetArrayElementAtIndex(i).floatValue = w.waits != null && i < w.waits.Length ? w.waits[i] : 0f;
            var pf = so.FindProperty("faces");
            pf.arraySize = w.points.Length;
            for (var i = 0; i < w.points.Length; i++) pf.GetArrayElementAtIndex(i).floatValue = w.faces != null && i < w.faces.Length ? w.faces[i] : float.NaN;
            so.FindProperty("speed").floatValue = w.speed;
            so.FindProperty("start").floatValue = w.start;
            so.FindProperty("turnHidden").boolValue = w.hidden;
            var player = Object.FindFirstObjectByType<PlayerController>();
            so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
            var seen = so.FindProperty("seen");
            seen.arraySize = skins.Length;
            for (var i = 0; i < skins.Length; i++) seen.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // 測りのために置いた骨の形を模型の素へ戻す（置いた形のまま保存すると、骨の数だけ上書きが残ってシーンが太る）。
            // 形は実行時に PersonMotion が毎こま置く。戻すと模型の中の、まだ上書きとして残していない変更も素に戻るので、形とマテリアルはそのあとでもう一度付ける
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(t) == null) continue;
                PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
            }
            for (var i = 0; i < skins.Length; i++) Dress(skins[i], shapes[i], mats);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            return root.gameObject;
        }

        /// <summary>模型の SkinnedMeshRenderer に減らした形と群衆のマテリアルを付け、プレハブの上書きとして残す</summary>
        static void Dress(SkinnedMeshRenderer smr, Mesh shape, Material[] mats)
        {
            smr.sharedMesh = shape;
            smr.sharedMaterials = mats;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
        }

        /// <summary>水平の向き。0 なら前</summary>
        static Vector3 Heading(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v;
        }

        /// <summary>模型の靴の裏の高さ。根の座で</summary>
        static float Lowest(Transform root, SkinnedMeshRenderer[] skins)
        {
            var low = float.MaxValue;
            var m = new Mesh();
            try
            {
                foreach (var smr in skins)
                {
                    smr.BakeMesh(m, true);
                    foreach (var v in m.vertices) low = Mathf.Min(low, root.InverseTransformPoint(smr.transform.TransformPoint(v)).y);
                }
            }
            finally
            {
                Object.DestroyImmediate(m);
            }
            return low == float.MaxValue ? 0f : low;
        }

        /// <summary>歩く人の形。模型の形を骨の重みごと 4,500 三角ほどに減らす。組み立ての間に一度だけ</summary>
        static Mesh WalkerMesh(RocketboxMob who, Mesh full)
        {
            var path = Folder + who.Name + "_Walk_LOD0.asset";
            if (!fresh.Contains(path))
            {
                var m = Simplify(full, Prep(who).slots, who, NearTriangles, true, who.Name + "_Walk_LOD0");
                RocketboxJacket.SaveMesh(m, path);
                fresh.Add(path);
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>PersonMotion の中身を書く（場面 4 の記憶の人と同じ。縮尺・背の曲げ・据える骨は持たない）</summary>
        static void RigWalker(PersonMotion motion, Animator animator, Transform body, AnimationClip[] clips, Vector3 seat, float[] strides, float runStride, float lag)
        {
            var so = new SerializedObject(motion);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("idle").objectReferenceValue = clips[0];
            so.FindProperty("walk").objectReferenceValue = clips[1];
            // 走らせない
            so.FindProperty("run").objectReferenceValue = null;
            so.FindProperty("seated").boolValue = false;
            so.FindProperty("seat").vector3Value = seat;
            var ps = so.FindProperty("strides");
            ps.arraySize = strides.Length;
            for (var i = 0; i < strides.Length; i++) ps.GetArrayElementAtIndex(i).floatValue = strides[i];
            so.FindProperty("runStride").floatValue = runStride;
            so.FindProperty("lag").floatValue = lag;
            so.FindProperty("attends").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 道筋が、ほかの人（焼いた形の人の足元）にどれだけ寄るか。いちばん近い所（m）と、その所を返す。
        /// 道筋は 0.2 m おきに見る。自分の元の置き場（converted）は見ない
        /// </summary>
        public static float Clearance(Walk w, List<Vector3> others, out Vector3 at)
        {
            var best = float.MaxValue;
            at = w.points[0];
            for (var i = 1; i < w.points.Length; i++)
            {
                var a = w.points[i - 1];
                var b = w.points[i];
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.2f));
                for (var k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n);
                    foreach (var o in others)
                    {
                        var d = new Vector2(p.x - o.x, p.z - o.z).magnitude;
                        if (d < best) { best = d; at = p; }
                    }
                }
            }
            return best;
        }
    }
}
