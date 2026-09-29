using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 群衆のその場の動き（<see cref="CrowdIdle"/>、オーナー、2026-09-30「モブに動きがなさ過ぎて流石に違和感がある」）のための、焼いた形の骨の組。
    ///
    /// 姿勢に曲げて焼いた形（<see cref="Posed(RocketboxMob, Pose, CrowdIdle.Kind, Rig)"/>）に、胴・首・頭・腕の 14 本の骨（<see cref="CrowdIdle.Slot"/>）の重みを付ける。
    /// Rocketbox の骨（80 本ほど）の重みを、いちばん近い上の骨へまとめる（脚は腰へ、指は手へ、頭の中の骨は頭へ）。一つの頂点は重い方の 2 本まで。
    /// 骨の組の置き場は焼いた姿勢そのもの（形を焼いたときの骨の位置と向き）なので、骨を動かさなければ焼いた形のまま描かれる。
    /// しぐさの形（右の指で左の手首を叩く・煙草を口へ運ぶ）は、同じ模型で腕を狙いへ届かせ（<see cref="BodyPoser.Arm"/>）、骨の向きだけを写して持つ
    /// </summary>
    public static partial class BuildAlleyCrowd
    {
        /// <summary>骨の組の骨。<see cref="CrowdIdle.Slot"/> の順</summary>
        static readonly HumanBodyBones[] SlotBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        /// <summary>焼いた姿勢の骨の組。人の根（足元が原点、+z が前）から見た置き場</summary>
        sealed class Rig
        {
            /// <summary>親の骨の番号。腰は -1（人の根の子）</summary>
            public int[] parents = new int[CrowdIdle.Slots];
            public Vector3[] localPos = new Vector3[CrowdIdle.Slots];
            public Quaternion[] localRot = new Quaternion[CrowdIdle.Slots];
            /// <summary>しぐさの形の骨の向き（親から見た向き）。しぐさが無ければ null</summary>
            public Quaternion[] gesture;
            /// <summary>煙草の置き場。右の手の骨から見た位置と向き（長さの向きが +y）</summary>
            public Vector3 propAt;
            public Quaternion propTurn = Quaternion.identity;
            /// <summary>しぐさの手が胴と頭から離れている一番近い所。m。しぐさが無ければ 0</summary>
            public float gestureGap;
        }

        /// <summary>組み立ての間に焼いた骨の組。形の置き場（<see cref="MeshPath"/>）を鍵にする</summary>
        static Dictionary<string, Rig> rigs;

        /// <summary>しぐさ。手首の端末を見る人は叩く、立っている人の 3 割ほどは煙草を吸う（人の番号から決め、乱数は引かない）</summary>
        static CrowdIdle.Kind KindOf(Pose pose, int index)
        {
            if (pose == Pose.Wrist) return CrowdIdle.Kind.Tap;
            if ((pose == Pose.Stand || pose == Pose.Rest || pose == Pose.Turn) && CrowdIdle.Hash(index, 7) > 0.38f) return CrowdIdle.Kind.Smoke;
            return CrowdIdle.Kind.None;
        }

        /// <summary>形の名の尻。煙草を持つ人は指を曲げて焼くので別の形</summary>
        static string KindTail(CrowdIdle.Kind kind)
        {
            return kind == CrowdIdle.Kind.Smoke ? "_Smoke" : "";
        }

        /// <summary>煙草を指に挟む。人差し指と中指を中指へ寄せて軽く曲げ、ほかの指は深めに曲げる</summary>
        static void Holding(Animator an)
        {
            BodyPoser.Close(an, false, 0.7f);
            BodyPoser.Grip(an, false, 0.45f);
        }

        /// <summary>
        /// 焼いた形 mesh に骨の組の重みと骨の置き場（bindposes）を付け、rig に骨の組を書く。
        /// go は姿勢に曲げた模型（原点で +z を向く）、low は靴の裏の高さ（形は low だけ下げてある）
        /// </summary>
        static void Capture(Animator an, SkinnedMeshRenderer smr, float low, Mesh mesh, Rig rig)
        {
            var t = new Transform[CrowdIdle.Slots];
            for (var i = 0; i < t.Length; i++) t[i] = an.GetBoneTransform(SlotBones[i]);
            var down = new Vector3(0f, low, 0f);
            var pos = new Vector3[t.Length];
            var rot = new Quaternion[t.Length];
            for (var i = 0; i < t.Length; i++)
            {
                // 無い骨（上の胸を持たない骨組み）は、親の骨と同じ所に置く。重みは付かない
                var have = t[i] != null ? t[i] : t[Mathf.Max(0, i - 1)];
                pos[i] = have.position - down;
                rot[i] = have.rotation;
            }
            for (var i = 0; i < t.Length; i++)
            {
                rig.parents[i] = i == 0 ? -1 : ParentSlot(t, i);
                if (rig.parents[i] < 0)
                {
                    rig.localPos[i] = pos[i];
                    rig.localRot[i] = rot[i];
                    continue;
                }
                var p = rig.parents[i];
                var inv = Quaternion.Inverse(rot[p]);
                rig.localPos[i] = inv * (pos[i] - pos[p]);
                rig.localRot[i] = inv * rot[i];
            }
            var bind = new Matrix4x4[t.Length];
            for (var i = 0; i < t.Length; i++) bind[i] = Matrix4x4.TRS(pos[i], rot[i], Vector3.one).inverse;

            // 重みをまとめる
            var bones = smr.bones;
            var map = new int[bones.Length];
            var legs = new[] { an.GetBoneTransform(HumanBodyBones.LeftUpperLeg), an.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            for (var b = 0; b < bones.Length; b++) map[b] = SlotOf(bones[b], t, legs);
            var src = smr.sharedMesh.boneWeights;
            var dst = new BoneWeight[src.Length];
            var w = new float[t.Length];
            for (var v = 0; v < src.Length; v++)
            {
                System.Array.Clear(w, 0, w.Length);
                var s = src[v];
                w[map[s.boneIndex0]] += s.weight0;
                w[map[s.boneIndex1]] += s.weight1;
                w[map[s.boneIndex2]] += s.weight2;
                w[map[s.boneIndex3]] += s.weight3;
                int a = 0, b2 = -1;
                for (var i = 1; i < w.Length; i++) if (w[i] > w[a]) a = i;
                for (var i = 0; i < w.Length; i++) if (i != a && (b2 < 0 || w[i] > w[b2])) b2 = i;
                var sum = w[a] + (b2 >= 0 ? w[b2] : 0f);
                if (sum <= 1e-6f) { dst[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1f }; continue; }
                dst[v] = new BoneWeight
                {
                    boneIndex0 = a, weight0 = w[a] / sum,
                    boneIndex1 = b2 >= 0 ? b2 : 0, weight1 = b2 >= 0 ? w[b2] / sum : 0f,
                };
            }
            mesh.boneWeights = dst;
            mesh.bindposes = bind;

            // 煙草の置き場。人差し指と中指の中の節の間に挟み、手のひらの側から甲の側へ抜ける向きに通す（火は甲の側）
            var hand = t[(int)CrowdIdle.Slot.RightHand];
            var index = an.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
            var middle = an.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
            if (hand != null && index != null && middle != null)
            {
                var at = (index.position + middle.position) * 0.5f;
                rig.propAt = Quaternion.Inverse(hand.rotation) * (at - hand.position);
                rig.propTurn = Quaternion.Inverse(hand.rotation) * Quaternion.FromToRotation(Vector3.up, -BodyPoser.PalmDir(an, false));
            }
        }

        /// <summary>骨の組の i 番目の骨の親。いちばん近い上の骨の組の骨。無ければ腰（0）</summary>
        static int ParentSlot(Transform[] t, int i)
        {
            if (t[i] == null) return i == (int)CrowdIdle.Slot.UpperChest ? (int)CrowdIdle.Slot.Chest : 0;
            for (var x = t[i].parent; x != null; x = x.parent)
                for (var j = 0; j < t.Length; j++)
                    if (j != i && t[j] == x) return j;
            return 0;
        }

        /// <summary>Rocketbox の骨 b の重みをまとめる先。脚は腰へ、ほかはいちばん近い上の骨の組の骨へ</summary>
        static int SlotOf(Transform b, Transform[] t, Transform[] legs)
        {
            if (b == null) return 0;
            foreach (var leg in legs)
                if (leg != null && (b == leg || b.IsChildOf(leg))) return 0;
            for (var x = b; x != null; x = x.parent)
                for (var j = 0; j < t.Length; j++)
                    if (t[j] == x) return j;
            return 0;
        }

        /// <summary>
        /// しぐさの形へ腕を曲げ、骨の組の骨の向き（親から見た向き）を写す。焼いた姿勢から続けて呼ぶ（模型はその形のまま）。
        /// 右の手が胴と頭の皮から離れている一番近い所を rig.gestureGap に書く
        /// </summary>
        static void Gesture(Animator an, SkinnedMeshRenderer smr, CrowdIdle.Kind kind, Rig rig)
        {
            if (kind == CrowdIdle.Kind.None) return;
            System.Func<HumanBodyBones, Transform> B = an.GetBoneTransform;
            var right = Vector3.right;
            if (kind == CrowdIdle.Kind.Tap)
            {
                // 右の指先で左の手首の甲を叩く。右の手首は左の手首の 4 cm 上・11 cm 右・3 cm 手前、指は左の手首へ向ける
                var wrist = B(HumanBodyBones.LeftHand).position;
                var elbowY = B(HumanBodyBones.RightLowerArm).position.y;
                BodyPoser.Arm(an, false, wrist + new Vector3(0.11f, 0.04f, -0.03f), new Vector3(0.45f, elbowY - 0.25f, -0.05f),
                    new Vector3(-0.95f, -0.25f, 0.15f), new Vector3(0f, -1f, 0f));
            }
            else
            {
                // 煙草を口へ。手のひらを顔へ向け、指を左へ寝かせて口の前を横切らせ、指に挟んだ煙草（手のひらの側の端）を唇に当てる。
                // 煙草の真ん中（人差し指と中指の中の節の間）を唇の 4.5 cm 前へ運ぶよう、手首の狙いを三度詰め直す
                var eyes = BodyPoser.Eyes(an);
                var mouth = eyes + new Vector3(0f, -0.075f, 0.035f);
                var want = mouth + new Vector3(0f, 0f, 0.045f);
                var fingers = new Vector3(-0.9f, 0.35f, 0.1f).normalized;
                var palm = new Vector3(0f, 0.15f, -1f);
                var elbowY = B(HumanBodyBones.RightLowerArm).position.y;
                var pole = new Vector3(0.40f, elbowY - 0.15f, -0.05f);
                var index = B(HumanBodyBones.RightIndexIntermediate);
                var middle = B(HumanBodyBones.RightMiddleIntermediate);
                var wrist = want - fingers * 0.07f;
                for (var it = 0; it < 3; it++)
                {
                    BodyPoser.Arm(an, false, wrist, pole, fingers, palm);
                    if (index == null || middle == null) break;
                    wrist += want - (index.position + middle.position) * 0.5f;
                }
            }
            var t = new Transform[CrowdIdle.Slots];
            for (var i = 0; i < t.Length; i++) t[i] = B(SlotBones[i]);
            rig.gesture = new Quaternion[CrowdIdle.Slots];
            for (var i = 0; i < t.Length; i++)
            {
                if (t[i] == null) { rig.gesture[i] = rig.localRot[i]; continue; }
                var p = rig.parents[i];
                var parentRot = p < 0 ? Quaternion.identity : (t[p] != null ? t[p].rotation : Quaternion.identity);
                rig.gesture[i] = p < 0 ? t[i].rotation : Quaternion.Inverse(parentRot) * t[i].rotation;
            }
            rig.gestureGap = HandGap(an, smr);
        }

        /// <summary>右の手と前腕の皮が、胴と頭の皮から離れている一番近い所。m</summary>
        static float HandGap(Animator an, SkinnedMeshRenderer smr)
        {
            var mesh = new Mesh();
            try
            {
                smr.BakeMesh(mesh, true);
                var v = mesh.vertices;
                var bw = smr.sharedMesh.boneWeights;
                var bones = smr.bones;
                var hand = an.GetBoneTransform(HumanBodyBones.RightHand);
                var fore = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var body = new[]
                {
                    an.GetBoneTransform(HumanBodyBones.Spine), an.GetBoneTransform(HumanBodyBones.Chest), an.GetBoneTransform(HumanBodyBones.UpperChest),
                    an.GetBoneTransform(HumanBodyBones.Neck), an.GetBoneTransform(HumanBodyBones.Head),
                };
                var handSet = new HashSet<int>();
                var bodySet = new HashSet<int>();
                for (var b = 0; b < bones.Length; b++)
                {
                    var x = bones[b];
                    if (x == null) continue;
                    if (x == fore || x == hand || x.IsChildOf(hand)) handSet.Add(b);
                    foreach (var y in body) if (y != null && x == y) bodySet.Add(b);
                }
                var hv = new List<Vector3>();
                var tv = new List<Vector3>();
                for (var k = 0; k < v.Length; k++)
                {
                    var p = smr.transform.TransformPoint(v[k]);
                    if (bw[k].weight0 < 0.6f) continue;
                    if (handSet.Contains(bw[k].boneIndex0)) hv.Add(p);
                    else if (bodySet.Contains(bw[k].boneIndex0)) tv.Add(p);
                }
                var best = float.MaxValue;
                foreach (var a in hv)
                    foreach (var c in tv)
                        best = Mathf.Min(best, (a - c).sqrMagnitude);
                return best == float.MaxValue ? 0f : Mathf.Sqrt(best);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// 一人に、その場の動きの骨の組・形を焼き直す元（描かない）・煙草を足し、<see cref="CrowdIdle"/> を繋ぐ。
        /// near は近さの段の近い方の形（骨の重み付き）、view はそれを描く所。煙草の描く物は近さの段の近い方に足すので、返す
        /// </summary>
        static Renderer[] Idle(Transform who, MeshFilter view, Mesh near, Material[] mats, Rig rig, CrowdIdle.Kind kind, Place place, int index)
        {
            var root = new GameObject("Rig").transform;
            root.SetParent(who, false);
            var bones = new Transform[CrowdIdle.Slots];
            for (var i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(((CrowdIdle.Slot)i).ToString()).transform;
            }
            for (var i = 0; i < bones.Length; i++)
            {
                bones[i].SetParent(rig.parents[i] < 0 ? root : bones[rig.parents[i]], false);
                bones[i].localPosition = rig.localPos[i];
                bones[i].localRotation = rig.localRot[i];
            }
            var skinGo = new GameObject("Skin");
            skinGo.transform.SetParent(who, false);
            var skin = skinGo.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = near;
            skin.bones = bones;
            skin.rootBone = bones[0];
            skin.sharedMaterials = mats;
            skin.quality = SkinQuality.Bone2;
            skin.updateWhenOffscreen = false;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 描くのは近さの段の近い方（焼き直した形）。こちらは焼き直す元で、描かない
            skin.enabled = false;

            var props = new List<Renderer>();
            if (kind == CrowdIdle.Kind.Smoke)
            {
                var hand = bones[(int)CrowdIdle.Slot.RightHand];
                var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stick.name = "Cigarette";
                Object.DestroyImmediate(stick.GetComponent<Collider>());
                stick.transform.SetParent(hand, false);
                stick.transform.localPosition = rig.propAt;
                stick.transform.localRotation = rig.propTurn;
                // 人の縮尺で伸び縮みしないよう、手の骨の縮尺（1）のまま。長さ 7 cm、太さ 8 mm
                stick.transform.localScale = new Vector3(0.008f, 0.035f, 0.008f);
                var sr = stick.GetComponent<MeshRenderer>();
                sr.sharedMaterial = PropMat("CrowdCigarette", new Color(0.86f, 0.84f, 0.78f), Color.black);
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                props.Add(sr);
                var ember = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ember.name = "Ember";
                Object.DestroyImmediate(ember.GetComponent<Collider>());
                ember.transform.SetParent(stick.transform, false);
                ember.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                ember.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
                var er = ember.GetComponent<MeshRenderer>();
                er.sharedMaterial = PropMat("CrowdEmber", new Color(0.35f, 0.08f, 0.02f), new Color(2.4f, 0.55f, 0.12f));
                er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                props.Add(er);
            }

            var idle = who.gameObject.AddComponent<CrowdIdle>();
            var so = new SerializedObject(idle);
            so.FindProperty("skin").objectReferenceValue = skin;
            so.FindProperty("view").objectReferenceValue = view;
            var pb = so.FindProperty("bones");
            pb.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++) pb.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
            var pr = so.FindProperty("rest");
            pr.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++) pr.GetArrayElementAtIndex(i).quaternionValue = rig.localRot[i];
            var pg = so.FindProperty("gesture");
            pg.arraySize = kind != CrowdIdle.Kind.None && rig.gesture != null ? bones.Length : 0;
            for (var i = 0; i < pg.arraySize; i++) pg.GetArrayElementAtIndex(i).quaternionValue = rig.gesture[i];
            so.FindProperty("kind").enumValueIndex = (int)kind;
            var seated = place.pose == Pose.Sit || place.pose == Pose.SitChair;
            so.FindProperty("seated").boolValue = seated;
            // 人ごとに周期と始まりをずらす（人の番号から決める。乱数は引かない）
            System.Func<int, float> u = k => CrowdIdle.Hash(index, k) * 0.5f + 0.5f;
            so.FindProperty("breathPeriod").floatValue = Mathf.Lerp(3.6f, 4.9f, u(11));
            so.FindProperty("swayPeriod").floatValue = Mathf.Lerp(7f, 12f, u(12));
            so.FindProperty("lookPeriod").floatValue = Mathf.Lerp(5f, 9.5f, u(13));
            so.FindProperty("lookDegrees").floatValue = seated ? 28f : 22f;
            so.FindProperty("gesturePeriod").floatValue = kind == CrowdIdle.Kind.Tap ? Mathf.Lerp(5f, 8f, u(14)) : Mathf.Lerp(9f, 15f, u(14));
            so.FindProperty("gestureHold").floatValue = kind == CrowdIdle.Kind.Tap ? 0.9f : 1.5f;
            so.FindProperty("phase").floatValue = u(15) * 20f;
            so.FindProperty("seed").intValue = index * 31 + 7;
            so.ApplyModifiedPropertiesWithoutUndo();
            return props.ToArray();
        }

        /// <summary>煙草と火のマテリアル。群衆の置き場（リポジトリに入れない）に作る</summary>
        static Material PropMat(string name, Color colour, Color glow)
        {
            var path = Folder + name + ".mat";
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", 0.2f);
            if (glow.maxColorComponent > 0f)
            {
                m.SetColor("_EmissionColor", glow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return Save(m, path);
        }
    }
}
