using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 売り買いの買い手（オーナー、2026-09-30「場面2の買い手も歩いて近づいてくるようにして。ただ、画面奥から登場させると時間かかるから、画面の左右から出てくる感じ」）。
    ///
    /// 前は姿勢に曲げて焼いた一つの形を、卓の向こうに浮かび上がらせていた。今は歩く人（<see cref="MakeWalker"/>）と同じく、
    /// Rocketbox の模型をそのまま置いて <see cref="PersonMotion"/> で立ち・歩きの動きを置き、<see cref="BuyerWalk"/> が視界の外から歩かせる。
    /// 人と服・インプラントは前と同じ（同じ乱数の種で選び、同じ 512 のテクスチャと暗めの色）。寄りで見るので三角は減らさない。
    ///
    /// **立ち姿**（腕を組む・片脚に預ける）は、止まってから <see cref="PersonMotion.Pose"/> で掛ける形として持たせる。
    /// 立ちの動きの頭の一こまで立たせて姿勢に曲げ（<see cref="Apply"/>）、胸の骨から上（胸・首・頭・鎖骨・腕・指）の向きを模型の根から見た向きで写す。
    /// 脚と腰は立ちの動きのまま。Rocketbox は太腿が背の骨の子なので、背の骨を据えると脚まで傾く。胸から上だけにする
    /// </summary>
    public static partial class BuildAlleyCrowd
    {
        /// <summary>
        /// 買い手を一人作る。route は視界の外の入り口から止まる所までの道筋（世界の座、尻が止まる所の足元）、
        /// face は止まって向く所（主人公の立つ所）。止まる所に置き、伏せずに返す（伏せるのは呼ぶ側）
        /// </summary>
        public static GameObject Buyer(Transform parent, string name, RocketboxMob who, Pose pose, Vector3[] route, Vector3 face, int seed, BuyerSteps steps, StringBuilder sb)
        {
            var at = route[route.Length - 1];
            EnsureFolder();
            Begin();
            try
            {
                var toward = face - at;
                toward.y = 0f;
                var yaw = toward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toward).eulerAngles.y : 0f;
                var rng = new System.Random(seed);
                var a = new Appearance { who = who, place = new Place { at = at, yaw = yaw, pose = pose, scale = 1f }, nth = 0 };
                PickKinds(a, Prep(who), rng, new List<Appearance>());
                var used = new List<Color>();
                for (var i = 0; i < a.implants.Count; i++)
                {
                    var im = a.implants[i];
                    im.color = Pick(rng, used, used);
                    used.Add(im.color);
                    a.implants[i] = im;
                }
                var mats = Materials(a, 512, true, "Buyer");

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
                if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Alley crowd/Import the people）: " + who.Model);
                var root = new GameObject(name).transform;
                root.SetParent(parent, false);
                root.position = at;
                root.rotation = Quaternion.Euler(0f, yaw, 0f);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
                model.name = "Figure";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var tris = 0;
                foreach (var smr in skins)
                {
                    Dress(smr, smr.sharedMesh, mats);
                    tris += smr.sharedMesh.triangles.Length / 3;
                }

                var female = who.Name.Contains("Female");
                var clips = new[] { BuildDiveCast.Clip(female, "Idle"), BuildDiveCast.Clip(female, "Walk"), BuildDiveCast.Clip(female, "Run") };
                var motion = root.gameObject.AddComponent<PersonMotion>();
                RigWalker(motion, animator, model.transform, clips, Vector3.zero, new float[0], 2.4f, 0f);
                var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                var strides = new float[PersonMotion.Reaches.Length];
                for (var i = 0; i < strides.Length; i++)
                    strides[i] = BuildDive.FigureStride(root, model, motion, clips[0], clips[1], PersonMotion.Reaches[i], feet);
                motion.Sample(clips[0], 0f);
                var seat = new Vector3(0f, -Lowest(root, skins), 0f);
                model.transform.localPosition = seat;
                RigWalker(motion, animator, model.transform, clips, seat, strides, 2.4f, 0f);
                var contacts = Contacts(root, model, clips[1], feet);

                // 立ち姿。立ちの動きの頭の一こまで立たせてから姿勢に曲げ、胸から上の骨の向きを写す。
                // 姿勢の曲げ（Apply）は模型が原点で +z を向いている前提なので、その間だけ原点へ動かす
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localPosition = Vector3.zero;
                BodyPoser.Stand(animator, clips[0]);
                Apply(animator, pose);
                var stance = Stance(animator);
                var mso = new SerializedObject(motion);
                var posed = mso.FindProperty("posed");
                var poses = mso.FindProperty("poses");
                posed.arraySize = stance.Count;
                poses.arraySize = stance.Count;
                var frame = Quaternion.Inverse(model.transform.rotation);
                for (var i = 0; i < stance.Count; i++)
                {
                    posed.GetArrayElementAtIndex(i).objectReferenceValue = stance[i];
                    poses.GetArrayElementAtIndex(i).quaternionValue = frame * stance[i].rotation;
                }
                mso.ApplyModifiedPropertiesWithoutUndo();
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                model.transform.localPosition = seat;

                var walk = root.gameObject.AddComponent<BuyerWalk>();
                var wso = new SerializedObject(walk);
                wso.FindProperty("motion").objectReferenceValue = motion;
                wso.FindProperty("stance").intValue = 0;
                // 腕を組む人は組んだまま歩く（下ろした腕から組む途中で、手と前腕が腹に沈む）
                wso.FindProperty("keepStance").boolValue = pose == Pose.Crossed;
                wso.FindProperty("steps").objectReferenceValue = steps;
                wso.FindProperty("heels").boolValue = female;
                wso.FindProperty("leftFoot").objectReferenceValue = feet[0];
                wso.FindProperty("rightFoot").objectReferenceValue = feet[1];
                var pc = wso.FindProperty("contacts");
                pc.arraySize = 2;
                pc.GetArrayElementAtIndex(0).floatValue = contacts[0];
                pc.GetArrayElementAtIndex(1).floatValue = contacts[1];
                // 男は自然な歩き（1.2 m/s ほど）より少し速く、ヒールの女は歩数を詰めて同じくらいの速さで
                wso.FindProperty("speed").floatValue = female ? 1.2f : 1.3f;
                var path = wso.FindProperty("path");
                path.arraySize = route.Length;
                for (var i = 0; i < route.Length; i++) path.GetArrayElementAtIndex(i).vector3Value = parent.InverseTransformPoint(route[i]);
                wso.ApplyModifiedPropertiesWithoutUndo();

                // 測りと写しのために置いた骨の形を模型の素へ戻す（歩く人と同じ。形とマテリアルは戻したあとで付け直す）
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t == model.transform) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(t) == null) continue;
                    PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
                }
                foreach (var smr in skins) Dress(smr, smr.sharedMesh, mats);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

                if (sb != null)
                    sb.AppendFormat("{0}: {1}（{2}、{3}）、{4} 三角、骨 {5} 本、立ち姿の骨 {6} 本、着地 {7:0.00}・{8:0.00}", name, who.Label, pose, Describe(a), tris,
                        skins.Length > 0 ? skins[0].bones.Length : 0, stance.Count, contacts[0], contacts[1]).AppendLine();
                return root.gameObject;
            }
            finally
            {
                End();
            }
        }

        /// <summary>
        /// 立ち姿で据える骨。胸の骨から下の枝（胸・首・頭・鎖骨・腕・指と、その間の骨）を、親から順に。
        /// 腰と背の一本目は立ちの動きのまま（Rocketbox は太腿が背の骨の子）
        /// </summary>
        static List<Transform> Stance(Animator an)
        {
            var top = an.GetBoneTransform(HumanBodyBones.Chest);
            if (top == null) top = an.GetBoneTransform(HumanBodyBones.Spine);
            var list = new List<Transform>();
            if (top == null) return list;
            var legs = new[] { an.GetBoneTransform(HumanBodyBones.LeftUpperLeg), an.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            var todo = new Queue<Transform>();
            todo.Enqueue(top);
            // 幅から（親から順に並ぶ）
            while (todo.Count > 0)
            {
                var t = todo.Dequeue();
                if (t == legs[0] || t == legs[1]) continue;
                if (t.GetComponent<Renderer>() != null) continue;
                list.Add(t);
                foreach (Transform c in t) todo.Enqueue(c);
            }
            return list;
        }

        /// <summary>
        /// 歩きの動きの周期のうち、左と右の足が着地する所（0〜1）。足の骨の高さが、いちばん低い所の 1.5 cm 上まで下りてきた所。
        /// 足音をそこで鳴らす（<see cref="BuyerWalk"/>）
        /// </summary>
        static float[] Contacts(Transform root, GameObject model, AnimationClip walk, Transform[] feet)
        {
            var result = new[] { 0f, 0.5f };
            if (walk == null) return result;
            const int n = 60;
            var h = new float[feet.Length, n];
            for (var s = 0; s < n; s++)
            {
                walk.SampleAnimation(model, walk.length * s / n);
                for (var f = 0; f < feet.Length; f++)
                    h[f, s] = feet[f] != null ? root.InverseTransformPoint(feet[f].position).y : 0f;
            }
            for (var f = 0; f < feet.Length && f < 2; f++)
            {
                var low = float.MaxValue;
                for (var s = 0; s < n; s++) low = Mathf.Min(low, h[f, s]);
                var near = low + 0.015f;
                for (var s = 0; s < n; s++)
                {
                    var was = h[f, (s + n - 1) % n];
                    if (h[f, s] <= near && was > near)
                    {
                        result[f] = s / (float)n;
                        break;
                    }
                }
            }
            return result;
        }
    }
}
