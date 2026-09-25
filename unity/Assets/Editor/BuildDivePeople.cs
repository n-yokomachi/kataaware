using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 記憶の中の人を組む（設計書 9.5 節、<c>docs/superpowers/specs/2026-09-26-dive-people-design.md</c> 7 節）。
    ///
    /// **人は一人ずつの登場人物として持つ。** 誰かは一覧（<see cref="DiveRoster"/>）の Seen の飛び先で決まり、
    /// 見た目は <see cref="BuildDiveCast"/> が Rocketbox の模型で組んだ一人ずつのプレハブ（<c>generated/dive/People/{id}.prefab</c>）。
    /// 同じ人はどの記憶に出ても同じプレハブ・同じ背。記憶 0 の母と記憶 8 の隣の母親は、どちらもハンナ（34）。
    ///
    /// **プレハブをそのまま置き、骨で動かす**（<see cref="PersonMotion"/>）。動きは Quaternius の立ち・歩き・走りを
    /// Humanoid へ移した物（<see cref="RocketboxRetarget.BakeMemory"/>）で、女の人は W_Suit、男の人は M_Suit から。
    /// 骨は名前ではなく <see cref="Animator.GetBoneTransform"/> で引く。
    ///
    /// ここで決めるのは次のとおり。どれも人ごとに一度だけ測り、同じ人には同じ値を使う。
    /// - 背: プレハブの縮尺のまま（<see cref="BuildDiveCast"/> が一覧の背に合わせてある）。ここでは測り直さない
    /// - 骨の縮尺と背の丸み: <see cref="RocketboxMemory.Proportion"/> と <see cref="DiveCast"/> の curl を、動きを置くたびに掛け直す。
    ///   **掛けるのは一度だけ。** Quaternius の頃の年齢の比（<see cref="DiveCast.ProportionOf"/>）と太さ（girth）は、Rocketbox の模型の上では使わない
    /// - 立ち方: 場面 2 の姿勢（<see cref="BuildAlleyCrowd.Apply"/> の Rest・Crossed・SitChair）と、手を後ろで組む形（<see cref="HandsBehind"/>）・
    ///   机に肘をつく形（<see cref="LeanOnDesk"/>）を写しの上で作り、その骨の向きを模型の根から見た向きとして据える
    /// - 床: 靴の裏が床に来る高さ
    /// - 歩幅: 歩きの動きを置いて、着いている足が後ろへ流れる速さを測る
    ///
    /// 色は人ごとのテクスチャのマテリアル（プレハブのまま）。Quaternius の頃の一枚の <c>Person.mat</c> と部位ごとの色（<see cref="PersonTint"/>）は、場面 4 では使わない
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>組んでいる記憶の一覧の行。<see cref="Cast"/> は板の相手の名前から人を引く</summary>
        static DiveEntry casting;

        /// <summary>人ごとに測った値。同じ人は一度だけ測る</summary>
        sealed class FigureFit
        {
            public float[] strides;
            public float runStride;
        }

        static readonly Dictionary<string, FigureFit> figureFits = new Dictionary<string, FigureFit>();
        static readonly Dictionary<string, float> figureSeats = new Dictionary<string, float>();
        static readonly Dictionary<string, Quaternion[]> figureHolds = new Dictionary<string, Quaternion[]>();

        /// <summary>組み直しの頭で、前の組み立ての測りを捨てる</summary>
        static void ForgetFigures()
        {
            figureFits.Clear();
            figureSeats.Clear();
            figureHolds.Clear();
        }

        /// <summary>
        /// 人をひとり置く。名前は一覧の <see cref="Seen.name"/> と揃える。誰かは Seen の飛び先で決まる。
        /// pose は立ち方: 0 立つ／1 片脚に預ける／2 腕組み／3 手を後ろ／4 振り向く／5 椅子に座る／6 机に肘をつく。
        /// 0 は立ちの動きのまま
        /// </summary>
        static Transform Cast(Transform take, string name, Vector3 at, float yaw, int pose)
        {
            Person person;
            GameObject prefab = null;
            if (!CastOf(name, out person)) Debug.LogWarning("一覧の板の相手に無い人: " + take.name + "/" + name);
            else
            {
                var path = BuildDiveCast.PrefabPath(RocketboxMemory.ById(person.id));
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) Debug.LogWarning("人のプレハブが無い（HalfAware/Dive people/Build the people）: " + path);
            }
            var root = prefab != null ? ((GameObject)PrefabUtility.InstantiatePrefab(prefab, take)).transform : new GameObject(name).transform;
            root.SetParent(take, false);
            root.name = name;
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (prefab != null) DressFigure(root, person, pose, take.name);
            return root;
        }

        /// <summary>
        /// 主の相手をしていない人。首と頭を主の目へ向けない（自分の娘を見ている母親、背を向けて待つ同級生、
        /// 通りすがり、机に伏せて眠っている生徒、新聞を読んでいる乗客）。
        /// 置いた人はどれも相手をしている人として組むので、外す人だけここを呼ぶ
        /// </summary>
        static void Aside(Transform who)
        {
            var motion = who != null ? who.GetComponent<PersonMotion>() : null;
            if (motion == null) return;
            var so = new SerializedObject(motion);
            so.FindProperty("attends").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>板の相手の名前から、飛び先の人を引く</summary>
        static bool CastOf(string name, out Person person)
        {
            person = default(Person);
            var seen = casting.seen ?? new Seen[0];
            for (var i = 0; i < seen.Length; i++)
                if (seen[i].name == name) return DiveCast.TryByEntry(seen[i].target, out person);
            return false;
        }

        static bool IsSeated(int pose)
        {
            return pose >= 5;
        }

        static void DressFigure(Transform root, Person person, int pose, string takeName)
        {
            var m = RocketboxMemory.ById(person.id);
            var model = root.Find("Figure").gameObject;
            // 背はプレハブの縮尺のまま。置き場は測ってから決める
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // 顔の向く向き。プレハブの骨は立ちの頭の一こまで、顔は体の前を向いている
            var neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            var headAim = headBone != null ? Quaternion.Inverse(headBone.rotation) * model.transform.forward : Vector3.forward;
            // 広がりは組み立てで据える（FigureBounds）。毎こま皮から測り直さない
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;

            var seated = IsSeated(pose);
            var clips = FigureClips(person.female);
            var motion = root.gameObject.AddComponent<PersonMotion>();

            // ---- 骨の組み ----
            // 骨の縮尺（頭・腕・脚）。プレハブの骨にも入っているが、動きを置いたあとに毎こま掛け直す
            var sized = new List<Transform>();
            var sizes = new List<Vector3>();
            System.Action<HumanBodyBones, float> size = (bone, k) =>
            {
                var b = animator.GetBoneTransform(bone);
                if (b == null || Mathf.Abs(k - 1f) < 1e-4f) return;
                sized.Add(b);
                sizes.Add(Vector3.one * k);
            };
            size(HumanBodyBones.Head, m.Proportion.head);
            size(HumanBodyBones.LeftUpperArm, m.Proportion.arm);
            size(HumanBodyBones.RightUpperArm, m.Proportion.arm);
            size(HumanBodyBones.LeftUpperLeg, m.Proportion.leg);
            size(HumanBodyBones.RightUpperLeg, m.Proportion.leg);

            // 背の丸み（年寄り）。BuildDiveCast.Curl と同じ骨と比。親から順に並べる
            var bent = new List<Transform>();
            var bends = new List<float>();
            for (var i = 0; i < BuildDiveCast.CurlBones.Length; i++)
            {
                var b = animator.GetBoneTransform(BuildDiveCast.CurlBones[i]);
                var c = person.curl * BuildDiveCast.CurlShare[i];
                if (b == null || Mathf.Abs(c) < 1e-3f) continue;
                bent.Add(b);
                bends.Add(c);
            }

            var heldBones = HeldBones(pose);
            var held = new List<Transform>();
            foreach (var hb in heldBones) held.Add(animator.GetBoneTransform(hb));
            var holds = FigureHolds(person, m, pose, heldBones);

            // Rocketbox（Biped）の足は脛の子なので、Quaternius の頃のように足首へ付け直さない
            var none = new Transform[0];
            var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };

            // ---- 歩幅を測る（座る人は歩かない） ----
            FigureFit fit;
            if (!figureFits.TryGetValue(person.id, out fit))
            {
                fit = new FigureFit();
                figureFits[person.id] = fit;
            }
            if (!seated && fit.strides == null)
            {
                RigFigure(motion, animator, model.transform, clips, false, Vector3.zero, null, 0f, 0f,
                    sized, sizes, bent, bends, new List<Transform>(), new Quaternion[0]);
                fit.strides = new float[PersonMotion.Reaches.Length];
                for (var i = 0; i < fit.strides.Length; i++)
                    fit.strides[i] = FigureStride(root, model, motion, clips[0], clips[1], PersonMotion.Reaches[i], feet);
                fit.runStride = FigureStride(root, model, motion, clips[0], clips[2], 1f, feet, true);
            }

            // ---- 床に下ろす ----
            var seatKey = person.id + "/" + pose;
            float drop;
            if (!figureSeats.TryGetValue(seatKey, out drop))
            {
                RigFigure(motion, animator, model.transform, clips, seated, Vector3.zero, null, 0f, 0f,
                    sized, sizes, bent, bends, held, holds);
                motion.Sample(clips[0], 0f);
                drop = -FigureExtent(root, model).min.y;
                figureSeats[seatKey] = drop;
            }
            var seat = new Vector3(0f, drop, 0f);
            model.transform.localPosition = seat;

            RigFigure(motion, animator, model.transform, clips, seated, seat,
                seated ? null : fit.strides, fit.runStride, FigureLag(takeName + "/" + root.name),
                sized, sizes, bent, bends, held, holds);
            // 目線。相手をしている人は主の目の方へ首と頭を向ける。相手をしていない人は Aside で外す
            var gso = new SerializedObject(motion);
            gso.FindProperty("attends").boolValue = true;
            gso.FindProperty("neck").objectReferenceValue = neckBone;
            gso.FindProperty("head").objectReferenceValue = headBone;
            gso.FindProperty("headAim").vector3Value = headAim.normalized;
            gso.ApplyModifiedPropertiesWithoutUndo();
            // 立ちの動きの頭の一こまを置いたまま返す。持ち物はこの形で骨に付ける
            motion.Still();
            FigureBounds(root, model);

            // プレハブの上書きとして Unity に知らせる。知らせないまま同じプレハブの別の上書きを戻すと
            // （UnposeFigures）、置き場まで素へ戻ってしまう
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
        }

        /// <summary>
        /// 骨の上書きを消す。測りと持ち物のために置いた形をプレハブの素へ戻してから保存する。
        /// 置いた形のまま保存すると、一人あたり骨の数 × 位置・向き・縮尺の上書きが残り、
        /// シーンが 0.8 MB から 4 MB へ太った。形は実行時に <see cref="PersonMotion"/> が毎こま置く
        /// </summary>
        static void UnposeFigures(Transform take)
        {
            foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true))
            {
                var body = motion.Body;
                if (body == null) continue;
                foreach (var t in body.GetComponentsInChildren<Transform>(true))
                {
                    if (t == body) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(t) == null) continue;
                    PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
                }
            }
        }

        /// <summary>立ち・歩き・走りの動き。女の人は W_Suit、男の人は M_Suit から Humanoid へ移した物</summary>
        static AnimationClip[] FigureClips(bool female)
        {
            return new[]
            {
                BuildDiveCast.Clip(female, "Idle"),
                BuildDiveCast.Clip(female, "Walk"),
                BuildDiveCast.Clip(female, "Run"),
            };
        }

        /// <summary>PersonMotion の中身を書く。private な [SerializeField] なので SerializedObject 越し</summary>
        static void RigFigure(PersonMotion motion, Animator animator, Transform body, AnimationClip[] clips,
            bool seated, Vector3 seat, float[] strides, float runStride, float lag,
            List<Transform> sized, List<Vector3> sizes, List<Transform> bent, List<float> bends,
            List<Transform> held, Quaternion[] holds)
        {
            var so = new SerializedObject(motion);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("idle").objectReferenceValue = clips[0];
            so.FindProperty("walk").objectReferenceValue = seated ? null : clips[1];
            so.FindProperty("run").objectReferenceValue = seated ? null : clips[2];
            so.FindProperty("seated").boolValue = seated;
            so.FindProperty("seat").vector3Value = seat;
            FillFloats(so.FindProperty("strides"), strides ?? new float[0]);
            so.FindProperty("runStride").floatValue = runStride > 0f ? runStride : 2.4f;
            so.FindProperty("lag").floatValue = lag;
            Fill(so.FindProperty("sized"), sized.ToArray());
            var ps = so.FindProperty("sizes");
            ps.arraySize = sizes.Count;
            for (var i = 0; i < sizes.Count; i++) ps.GetArrayElementAtIndex(i).vector3Value = sizes[i];
            Fill(so.FindProperty("bent"), bent.ToArray());
            FillFloats(so.FindProperty("bends"), bends.ToArray());
            Fill(so.FindProperty("held"), held.ToArray());
            var ph = so.FindProperty("holds");
            ph.arraySize = holds.Length;
            for (var i = 0; i < holds.Length; i++) ph.GetArrayElementAtIndex(i).quaternionValue = holds[i];
            Fill(so.FindProperty("feet"), new Transform[0]);
            Fill(so.FindProperty("ankles"), new Transform[0]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FillFloats(SerializedProperty row, float[] all)
        {
            row.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) row.GetArrayElementAtIndex(i).floatValue = all[i];
        }

        /// <summary>
        /// 模型の広がりを人の根のローカルで測る。皮を置いた形の頂点から出す。
        /// レンダラーの bounds は骨の素の広がりで、背も床も測れない
        /// </summary>
        static Bounds FigureExtent(Transform root, GameObject model)
        {
            var box = new Bounds();
            var first = true;
            var tmp = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(tmp, true);
                var at = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var v = tmp.vertices;
                for (var i = 0; i < v.Length; i++)
                {
                    var p = at.MultiplyPoint3x4(v[i]);
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
            }
            Object.DestroyImmediate(tmp);
            return box;
        }

        /// <summary>
        /// レンダラーの広がりを、立った（座った）形の皮に合わせて据える。
        ///
        /// **縦は皮の頂点どおり、横と前後だけ歩く脚のぶん広げる。** 目から相手の頭と胸へ撃つ光線
        /// （DiveDirector.Body）は、最初のレンダラーの広がりの上端と高さの比で頭と胸を決める。
        /// 手首の差込口のある人（エミリー・プリヤ）は、差込口の小さな部品が体の皮より先に並ぶので、
        /// 部品にも体と同じ広がりを入れる（どの部品も根の骨は腰）
        /// </summary>
        static void FigureBounds(Transform root, GameObject model)
        {
            var box = FigureExtent(root, model);
            box.Expand(new Vector3(0.6f, 0f, 0.6f));
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bone = smr.rootBone != null ? smr.rootBone : smr.transform;
                var local = new Bounds();
                for (var i = 0; i < 8; i++)
                {
                    var corner = box.center + Vector3.Scale(box.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = bone.InverseTransformPoint(root.TransformPoint(corner));
                    if (i == 0) local = new Bounds(p, Vector3.zero);
                    else local.Encapsulate(p);
                }
                smr.localBounds = local;
            }
        }

        /// <summary>
        /// 一周期で進む m。歩きの動きを立ちの動きへ reach だけ寄せて置き、
        /// 着いている足（いちばん低い所から 1.5 cm 以内の足首）が人の根から見て後ろへ流れる速さを測る。
        /// その速さで根を運べば、着いている足は床に止まって見える
        /// </summary>
        static float FigureStride(Transform root, GameObject model, PersonMotion motion,
            AnimationClip stand, AnimationClip clip, float reach, Transform[] feet, bool running = false)
        {
            if (clip == null) return running ? 2.4f : 1.8f;
            const int n = 48;
            var all = model.GetComponentsInChildren<Transform>(true);
            var pos = new Vector3[all.Length];
            var rot = new Quaternion[all.Length];
            var scl = new Vector3[all.Length];
            stand.SampleAnimation(model, 0f);
            for (var i = 0; i < all.Length; i++)
            {
                pos[i] = all[i].localPosition;
                rot[i] = all[i].localRotation;
                scl[i] = all[i].localScale;
            }
            var track = new Vector3[feet.Length, n];
            for (var s = 0; s < n; s++)
            {
                clip.SampleAnimation(model, clip.length * s / n);
                if (reach < 1f)
                    for (var i = 1; i < all.Length; i++)
                    {
                        all[i].localPosition = Vector3.Lerp(pos[i], all[i].localPosition, reach);
                        all[i].localRotation = Quaternion.Slerp(rot[i], all[i].localRotation, reach);
                        all[i].localScale = Vector3.Lerp(scl[i], all[i].localScale, reach);
                    }
                motion.Settle();
                for (var f = 0; f < feet.Length; f++)
                    track[f, s] = feet[f] != null ? root.InverseTransformPoint(feet[f].position) : Vector3.zero;
            }
            var low = float.MaxValue;
            for (var f = 0; f < feet.Length; f++)
                for (var s = 0; s < n; s++) low = Mathf.Min(low, track[f, s].y);
            var dt = clip.length / n;
            var speeds = new List<float>();
            for (var f = 0; f < feet.Length; f++)
                for (var s = 0; s < n; s++)
                {
                    var a = track[f, s];
                    var b = track[f, (s + 1) % n];
                    if (a.y > low + 0.015f || b.y > low + 0.015f) continue;
                    speeds.Add(-(b.z - a.z) / dt);
                }
            if (speeds.Count == 0) return running ? 2.4f : 1.8f;
            speeds.Sort();
            return Mathf.Max(0.05f, speeds[speeds.Count / 2] * clip.length);
        }

        /// <summary>こまの頭のずらし。記憶と名前から決めるので、組み直しても入り直しても同じ</summary>
        static float FigureLag(string key)
        {
            unchecked
            {
                var h = 2166136261u;
                for (var i = 0; i < key.Length; i++) h = (h ^ key[i]) * 16777619u;
                return (h % 1000u) / 1000f;
            }
        }

        // ---- 据える姿勢 ----------------------------------------------------------

        static readonly HumanBodyBones[] ArmBones =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
        };

        /// <summary>
        /// 姿勢ごとに向きを据える骨。<see cref="BuildAlleyCrowd.Apply"/>（と <see cref="LeanOnDesk"/>）が曲げる骨で、
        /// 親から順に並べる。据えない骨は立ちの動きのまま揺れる（腕を据えた人も胸は息をする）。
        /// 座る形は背骨から頭までと脚と腕を全部据える（腰の高さは床に下ろすときに合う）
        /// </summary>
        static HumanBodyBones[] HeldBones(int pose)
        {
            switch (pose)
            {
                case 1:
                    return new[]
                    {
                        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
                        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    };
                case 2:
                case 3:
                    return ArmBones;
                case 4:
                    return new[]
                    {
                        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
                        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftUpperArm,
                    };
                case 0:
                    return new HumanBodyBones[0];
                default:
                    var seated = new List<HumanBodyBones>
                    {
                        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
                        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                        HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
                        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                    };
                    seated.AddRange(ArmBones);
                    return seated.ToArray();
            }
        }

        /// <summary>
        /// 据える骨の向き。その人のプレハブの写しを原点に置き、立ちの動きの頭の一こまに骨の縮尺と背の丸みを掛けてから、
        /// 姿勢へ曲げて、模型の根から見た向きとして取る。人ごと（背と骨の縮尺が違う）・姿勢ごとに一度だけ。
        /// 骨の親（背骨）は動きの中でひねられるので、親から見た向きで据えると腕や脚がよそを向く
        /// </summary>
        static Quaternion[] FigureHolds(Person person, RocketboxMemory m, int pose, HumanBodyBones[] bones)
        {
            var key = person.id + "/" + pose;
            Quaternion[] had;
            if (figureHolds.TryGetValue(key, out had)) return had;
            var all = new Quaternion[bones.Length];
            if (bones.Length > 0)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(BuildDiveCast.PrefabPath(m));
                var scratch = (GameObject)Object.Instantiate(src);
                scratch.hideFlags = HideFlags.HideAndDontSave;
                // 場面 2 の姿勢は、模型が原点で +z を向き、靴の裏が高さ 0 にある前提で世界の位置を狙う（座面の高さなど）
                scratch.transform.position = Vector3.zero;
                scratch.transform.rotation = Quaternion.identity;
                try
                {
                    var figure = scratch.transform.Find("Figure");
                    var an = figure.GetComponent<Animator>();
                    an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    an.applyRootMotion = false;
                    BodyPoser.Stand(an, BuildDiveCast.Clip(person.female, "Idle"));
                    BuildDiveCast.Size(an, m.Proportion);
                    BuildDiveCast.Curl(an, person.curl);
                    var box = BuildDiveCast.Extent(scratch.transform, BuildDiveCast.Body(figure.gameObject));
                    figure.localPosition -= Vector3.up * box.min.y;
                    FigurePosture(an, pose, BuildDiveCast.Body(figure.gameObject));
                    var inverse = Quaternion.Inverse(figure.rotation);
                    for (var i = 0; i < bones.Length; i++)
                    {
                        var b = an.GetBoneTransform(bones[i]);
                        all[i] = b != null ? inverse * b.rotation : Quaternion.identity;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(scratch);
                }
            }
            figureHolds[key] = all;
            return all;
        }

        /// <summary>
        /// 骨を曲げて姿勢を作る。場面 2 の姿勢（<see cref="BuildAlleyCrowd.Apply"/>）を使い、
        /// 机に肘をつく形（6）と、手を後ろで組む形（3）だけここで作る（<see cref="LeanOnDesk"/>・<see cref="HandsBehind"/>）。
        /// 模型は原点で +z を向いている
        /// </summary>
        static void FigurePosture(Animator an, int pose, SkinnedMeshRenderer skin)
        {
            switch (pose)
            {
                case 1: BuildAlleyCrowd.Apply(an, BuildAlleyCrowd.Pose.Rest); break;
                case 2: BuildAlleyCrowd.Apply(an, BuildAlleyCrowd.Pose.Crossed); break;
                case 3: HandsBehind(an, skin); break;
                case 4: BuildAlleyCrowd.Apply(an, BuildAlleyCrowd.Pose.Turn); break;
                case 6: LeanOnDesk(an); break;
                default:
                    if (IsSeated(pose)) BuildAlleyCrowd.Apply(an, BuildAlleyCrowd.Pose.SitChair);
                    break;
            }
        }

        /// <summary>机に肘をつく形で、上体を椅子に座った形からさらに前へ倒す一段（度）と、その上限</summary>
        const float DeskLeanStep = 2f;
        const float DeskLeanMost = 40f;
        /// <summary>机の天板の高さ（教室の机。<c>BuildDiveClassroom.DeskY</c> の板の上の面）</summary>
        const float DeskTop = DeskY + 0.02f;
        /// <summary>椅子の座面の真ん中から、机の手前の縁まで（教室の机と椅子の並び）</summary>
        const float DeskNear = 0.56f - 0.22f;
        /// <summary>肘を置く所。机の手前の縁からこれだけ奥</summary>
        const float DeskElbowIn = 0.02f;
        /// <summary>前腕の下の面から骨の線まで（天板に食い込まないよう、肘と手首を天板からこれだけ上げる）</summary>
        const float ForearmHalf = 0.035f;

        /// <summary>
        /// 机に肘をつく（pose 6）。椅子に座った形（<see cref="BuildAlleyCrowd.Pose.SitChair"/>。座面の高さは教室の椅子と同じ 0.46 m）から、
        /// 上体を前へ倒し、両の肘を机の手前の縁の少し奥に、前腕を天板の上に置いて、手を前で寄せる（<see cref="BodyPoser.Arm"/>）。
        ///
        /// **倒す角は、肘が天板に届くまで。** 決め打ちの 16° では肩が座面の真上近くに残り、机の縁（座面の真ん中から 0.34 m 先）まで
        /// 上腕（0.23 m ほど）が届かず、腕が伸び切って天板の上に浮いた。肩から上腕の長さだけ離れた天板の上の点が
        /// 机の縁より奥に来るまで、上体を一段ずつ倒す。背丈が違っても肘が天板から浮かない。
        /// 倒したあとで、脚を座った形の足首の置き場へ解き直す（Rocketbox は腿が背骨の一つ目の子で、背骨と一緒に振れる）。
        /// 首と頭は倒した分の半分だけ起こし返し、顔は机の上のノートへ向く
        /// </summary>
        static void LeanOnDesk(Animator an)
        {
            BuildAlleyCrowd.Apply(an, BuildAlleyCrowd.Pose.SitChair);
            System.Func<HumanBodyBones, Transform> B = an.GetBoneTransform;
            var right = Vector3.right;
            var y = DeskTop + ForearmHalf;
            var want = DeskNear + DeskElbowIn;
            var upper = new float[2];
            var fore = new float[2];
            for (var i = 0; i < 2; i++)
            {
                var left = i == 0;
                var s = B(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm).position;
                var e = B(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).position;
                var w = B(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
                upper[i] = Vector3.Distance(s, e);
                fore[i] = Vector3.Distance(e, w);
            }
            // 肘: 肩の真下から少し外、天板の高さで、肩から上腕の長さだけ前
            System.Func<int, Vector3> elbowOf = i =>
            {
                var sign = i == 0 ? -1f : 1f;
                var s = B(i == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm).position;
                var dx = sign * 0.03f;
                var dy = s.y - y;
                var reach = Mathf.Sqrt(Mathf.Max(0f, upper[i] * upper[i] - dy * dy - dx * dx));
                return new Vector3(s.x + dx, y, s.z + reach);
            };
            // 足首の置き場と足の向き。上体を倒したあとで脚を解き直す（Rocketbox（Biped）は腿が背骨の一つ目の子なので、
            // 背骨を曲げると脚ごと前へ振れて、足先が床に沈む）
            var legs = new[]
            {
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
            };
            var ankles = new Vector3[2];
            var feet = new Quaternion[2];
            var knees = new Vector3[2];
            for (var i = 0; i < 2; i++)
            {
                ankles[i] = B(legs[i][2]).position;
                feet[i] = B(legs[i][2]).rotation;
                knees[i] = B(legs[i][1]).position + Vector3.forward * 0.6f;
            }
            for (var lean = 0f; lean < DeskLeanMost; lean += DeskLeanStep)
            {
                if (elbowOf(0).z >= want && elbowOf(1).z >= want) break;
                FigureTurnBone(B(HumanBodyBones.Spine), right, DeskLeanStep * 0.40f);
                FigureTurnBone(B(HumanBodyBones.Chest), right, DeskLeanStep * 0.35f);
                FigureTurnBone(B(HumanBodyBones.UpperChest), right, DeskLeanStep * 0.25f);
                FigureTurnBone(B(HumanBodyBones.Neck), right, -DeskLeanStep * 0.25f);
                FigureTurnBone(B(HumanBodyBones.Head), right, -DeskLeanStep * 0.25f);
            }
            for (var i = 0; i < 2; i++)
            {
                ArmReach.Solve(B(legs[i][0]), B(legs[i][1]), B(legs[i][2]), ankles[i], knees[i], 1f);
                B(legs[i][2]).rotation = feet[i];
            }
            for (var i = 0; i < 2; i++)
            {
                var left = i == 0;
                var sign = left ? -1f : 1f;
                var elbow = elbowOf(i);
                // 手首: 肘から前腕の長さだけ、体の真ん中へ寄せながら前へ
                var toward = new Vector3(-sign * 0.55f, 0f, 1f).normalized;
                var wrist = elbow + toward * fore[i];
                BodyPoser.Arm(an, left, wrist, elbow + new Vector3(sign * 0.05f, -0.02f, 0f), toward, Vector3.down);
            }
        }

        /// <summary>手を後ろで組む形で、手首を背中の皮からどれだけ後ろ・腰の骨からどれだけ上に置くか（m）</summary>
        const float BehindOff = 0.05f;
        const float BehindUp = 0.12f;

        /// <summary>
        /// 手を後ろで組む（pose 3）。両の手首を腰の後ろで重ね、肘を外の後ろへ逃がして曲げる（<see cref="BodyPoser.Arm"/>）。
        ///
        /// **場面 2 の形（<see cref="BuildAlleyCrowd.Pose.Behind"/>）は使わない。** あちらは手首を腰の骨の 17 cm 後ろ・2 cm 上に置くので、
        /// 腕がほとんど伸び切り、肘が肩と手首を結ぶ線の上（胴の中）に来る。背中の厚い人（男大 05 のベスト、背を丸めた年寄り）では、
        /// 肘から先がベストの背中の中に沈んだ。手首を背中の皮の <see cref="BehindOff"/> 後ろ、腰の骨の <see cref="BehindUp"/> 上に置いて、
        /// 腕に曲がる余りを作る
        /// </summary>
        static void HandsBehind(Animator an, SkinnedMeshRenderer skin)
        {
            var hips = an.GetBoneTransform(HumanBodyBones.Hips).position;
            var y = hips.y + BehindUp;
            // 手首の高さの背中の面（背骨の線の近く、横 0.15 m の内でいちばん後ろ）。模型は原点に置いてあるので世界の座がそのまま根の座
            var back = hips.z - 0.15f;
            var tmp = new Mesh();
            try
            {
                skin.BakeMesh(tmp, true);
                var at = skin.transform.localToWorldMatrix;
                var first = true;
                foreach (var v in tmp.vertices)
                {
                    var p = at.MultiplyPoint3x4(v);
                    if (Mathf.Abs(p.y - y) > 0.05f || Mathf.Abs(p.x) > 0.15f) continue;
                    if (first || p.z < back) { back = p.z; first = false; }
                }
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
            var elbow = an.GetBoneTransform(HumanBodyBones.LeftLowerArm).position.y;
            BodyPoser.Arm(an, true, new Vector3(-0.05f, y, back - BehindOff), new Vector3(-0.45f, elbow, back - 0.35f),
                new Vector3(0.6f, -0.6f, -0.2f), new Vector3(0f, 0f, 1f));
            BodyPoser.Arm(an, false, new Vector3(0.05f, y + 0.01f, back - BehindOff - 0.01f), new Vector3(0.45f, elbow, back - 0.35f),
                new Vector3(-0.6f, -0.6f, -0.2f), new Vector3(0f, 0f, 1f));
        }

        static void FigureTurnBone(Transform t, Vector3 axis, float degrees)
        {
            if (t == null || Mathf.Approximately(degrees, 0f)) return;
            t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
        }

        // ---- 持ち物 ----------------------------------------------------------------
        //
        // 骨に付いて動かす。形は立った形を置いた人の根の座で作り、骨から見た置き場を覚えさせる

        /// <summary>人の Humanoid の骨</summary>
        static Transform FigureBone(Transform who, HumanBodyBones bone)
        {
            var an = who.GetComponentInChildren<Animator>(true);
            return an != null ? an.GetBoneTransform(bone) : null;
        }

        /// <summary>
        /// 人の根の座で形を作り、骨に付ける。形は人の根の子に置いたまま、
        /// <see cref="PersonMotion.Carry"/> でこまの頭ごとに骨の所へ据え直させる
        /// </summary>
        static Transform FigureAttach(Transform who, HumanBodyBones bone, string name, Mesh mesh, Material mat)
        {
            var go = Piece(who, name, mesh, mat);
            go.localPosition = Vector3.zero;
            go.localRotation = Quaternion.identity;
            var b = FigureBone(who, bone);
            var motion = who.GetComponent<PersonMotion>();
            if (b != null && motion != null)
            {
                motion.Carry(go, b);
                EditorUtility.SetDirty(motion);
            }
            return go;
        }

        /// <summary>人の根の座での骨の位置</summary>
        static Vector3 FigureAt(Transform who, HumanBodyBones bone)
        {
            var b = FigureBone(who, bone);
            return b != null ? who.InverseTransformPoint(b.position) : Vector3.zero;
        }

        /// <summary>体の皮を置いた形の頂点を、人の根の座で</summary>
        static Vector3[] FigureSkin(Transform who)
        {
            var figure = who.Find("Figure");
            if (figure == null) return new Vector3[0];
            var smr = BuildDiveCast.Body(figure.gameObject);
            var tmp = new Mesh();
            try
            {
                smr.BakeMesh(tmp, true);
                var at = who.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var v = tmp.vertices;
                for (var i = 0; i < v.Length; i++) v[i] = at.MultiplyPoint3x4(v[i]);
                return v;
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }
    }
}
