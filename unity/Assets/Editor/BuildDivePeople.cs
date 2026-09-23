using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 記憶の中の人を組む（設計書 9.5 節）。
    ///
    /// **人は一人ずつの登場人物として持つ。** 誰かは一覧（<see cref="DiveRoster"/>）の Seen の飛び先で決まり、
    /// 見た目は <see cref="DiveCast"/> の十六人から写す。同じ人はどの記憶に出ても同じ模型・同じ色・同じ背。
    /// 記憶 0 の母と記憶 8 の隣の母親は、どちらもハンナ（34）。
    ///
    /// **模型は Quaternius の FBX をプレハブのまま置き、骨で動かす**（<see cref="PersonMotion"/>）。
    /// 焼いた形を置いていた頃は、全員が暗い一色（買い手のマテリアル）で、姿勢は一つに固まり、
    /// 子どもは大人を縮めただけだった。
    ///
    /// ここで決めるのは次のとおり。どれも人ごとに一度だけ測り、同じ人には同じ値を使う。
    /// - 色: 部位ごとのマテリアルを一枚の <c>Person.mat</c> に揃え、色は <see cref="PersonTint"/> が持つ
    /// - 骨の縮尺: 年齢の区分（<see cref="DiveCast.ProportionOf"/>）と体の太さ
    /// - 背: 立った形で測って、一覧の背に合うよう模型ごと縮める
    /// - 座り方・腕の組み方: 路地裏の姿勢（<c>BuildAlley.Pose</c>）を写した <see cref="FigurePosture"/> で、
    ///   脚と腕の向きを模型から見た向きとして据える
    /// - 床: 靴の裏が床に来る高さ
    /// - 歩幅: 歩きの動きを置いて、着いている足が後ろへ流れる速さを測る
    /// </summary>
    public static partial class BuildDive
    {
        const string ModelsAt = "Assets/Models/quaternius/";
        public const string PersonMatPath = Materials + "Person.mat";
        /// <summary>立ちの動き。Idle は構えた広い足幅で、日常の立ち姿に見えない</summary>
        const string StandClip = "CharacterArmature|Idle_Neutral";
        const string WalkClip = "CharacterArmature|Walk";
        const string RunClip = "CharacterArmature|Run";

        /// <summary>組んでいる記憶の一覧の行。<see cref="Cast"/> は板の相手の名前から人を引く</summary>
        static DiveEntry casting;

        /// <summary>人ごとに測った値。同じ人は一度だけ測る</summary>
        sealed class FigureFit
        {
            public float scale;
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
        /// pose は立ち方: 0 立つ／1 片脚に預ける／2 腕組み／3 手を後ろ／4 振り向く／
        /// 5 椅子に座る／6 卓に肘をつく／7 横を向いて座る。0・1・4 はどれも立ちの動きのまま
        /// </summary>
        static Transform Cast(Transform take, string name, Vector3 at, float yaw, int pose)
        {
            var root = new GameObject(name).transform;
            root.SetParent(take, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Person person;
            if (!CastOf(name, out person))
            {
                Debug.LogWarning("一覧の板の相手に無い人: " + take.name + "/" + name);
                return root;
            }
            DressFigure(root, person, pose, take.name);
            return root;
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
            return pose >= 5 && pose <= 7;
        }

        static void DressFigure(Transform root, Person person, int pose, string takeName)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsAt + person.model + ".fbx");
            if (src == null) { Debug.LogWarning("模型が無い: " + person.model); return; }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
            model.name = "Figure";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            PaintFigure(root, model, person);

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var seated = IsSeated(pose);
            var clips = FigureClips(person.model);
            var motion = root.gameObject.AddComponent<PersonMotion>();

            // ---- 骨の組み ----
            var prop = DiveCast.ProportionOf(person.Band);
            var g = person.girth;
            var sized = new List<Transform>();
            var sizes = new List<Vector3>();
            System.Action<string, Vector3> size = (bone, k) =>
            {
                Transform b;
                if (!bones.TryGetValue(bone, out b)) return;
                sized.Add(b);
                sizes.Add(Vector3.Scale(b.localScale, k));
            };
            // 太さは胴の根（Body）の横と前後だけ。Body の Y は上を向いているので、
            // 体をひねっても横と前後の比は崩れない。腕と脚は Body の子なので、太さはそのまま付いてくる。
            // 頭は太らせない。首から上で打ち消す
            size("Body", new Vector3(g, 1f, g));
            size("Head", new Vector3(prop.head / g, prop.head, prop.head / g));
            size("UpperArm.L", Vector3.one * prop.arm);
            size("UpperArm.R", Vector3.one * prop.arm);
            size("UpperLeg.L", Vector3.one * prop.leg);
            size("UpperLeg.R", Vector3.one * prop.leg);
            // 足は脛の子ではないので、脚を縮めた分は足にも掛ける
            size("Foot.L", Vector3.one * prop.leg);
            size("Foot.R", Vector3.one * prop.leg);

            // 背の丸み（年寄り）と、座った背の傾き。親から順に並べる
            var spine = new[] { "Abdomen", "Torso", "Chest", "Neck", "Head" };
            var curl = new[] { 0.30f, 0.40f, 0.30f, -0.45f, -0.30f };
            var lean = new[] { 0.45f, 0.35f, 0.20f, -0.35f, -0.25f };
            var sit = pose == 5 ? 4f : pose == 6 ? 8f : pose == 7 ? 10f : 0f;
            var bentStand = new List<Transform>();
            var bendsStand = new List<float>();
            var bent = new List<Transform>();
            var bends = new List<float>();
            for (var i = 0; i < spine.Length; i++)
            {
                Transform b;
                if (!bones.TryGetValue(spine[i], out b)) continue;
                var c = person.curl * curl[i];
                if (Mathf.Abs(c) > 1e-3f) { bentStand.Add(b); bendsStand.Add(c); }
                var all = c + sit * lean[i];
                if (Mathf.Abs(all) > 1e-3f) { bent.Add(b); bends.Add(all); }
            }

            var heldNames = HeldBones(pose);
            var held = new List<Transform>();
            var holds = FigureHolds(person.model, pose, heldNames);
            for (var i = 0; i < heldNames.Length; i++)
            {
                Transform b;
                bones.TryGetValue(heldNames[i], out b);
                held.Add(b);
            }

            var feet = new[] { Bone(bones, "Foot.L"), Bone(bones, "Foot.R") };
            var ankles = new[] { Bone(bones, "LowerLeg.L_end"), Bone(bones, "LowerLeg.R_end") };

            // ---- 背を合わせる ----
            // 立った形（腕も脚も据えない、背の丸みだけ）で測る。座る人も立った背で合わせる
            FigureFit fit;
            if (!figureFits.TryGetValue(person.id, out fit))
            {
                fit = new FigureFit();
                RigFigure(motion, animator, model.transform, clips, false, Vector3.zero, null, 0f, 0f,
                    sized, sizes, bentStand, bendsStand, new List<Transform>(), new Quaternion[0], feet, ankles);
                motion.Sample(clips[0], 0f);
                var tall = FigureExtent(root, model).size.y;
                fit.scale = tall > 0.1f ? person.height / tall : 1f;
                figureFits[person.id] = fit;
            }
            model.transform.localScale = Vector3.one * fit.scale;

            // ---- 歩幅を測る（座る人は歩かない） ----
            if (!seated && fit.strides == null)
            {
                RigFigure(motion, animator, model.transform, clips, false, Vector3.zero, null, 0f, 0f,
                    sized, sizes, bent, bends, held, holds, feet, ankles);
                fit.strides = new float[PersonMotion.Reaches.Length];
                for (var i = 0; i < fit.strides.Length; i++)
                    fit.strides[i] = FigureStride(root, model, motion, clips[0], clips[1], PersonMotion.Reaches[i], feet);
                fit.runStride = FigureStride(root, model, motion, clips[0], clips[2], 1f, feet, true);
            }

            // ---- 床に下ろす ----
            var seatKey = person.id + "/" + (seated ? pose : pose == 2 || pose == 3 ? pose : 0);
            float drop;
            if (!figureSeats.TryGetValue(seatKey, out drop))
            {
                RigFigure(motion, animator, model.transform, clips, seated, Vector3.zero, null, 0f, 0f,
                    sized, sizes, bent, bends, held, holds, feet, ankles);
                motion.Sample(clips[0], 0f);
                drop = -FigureExtent(root, model).min.y;
                figureSeats[seatKey] = drop;
            }
            var seat = new Vector3(0f, drop, 0f);
            model.transform.localPosition = seat;

            RigFigure(motion, animator, model.transform, clips, seated, seat,
                seated ? null : fit.strides, fit.runStride, FigureLag(takeName + "/" + root.name),
                sized, sizes, bent, bends, held, holds, feet, ankles);
            // 立ちの動きの頭の一こまを置いたまま返す。持ち物はこの形で骨に付ける
            motion.Still();
            FigureBounds(root, model);

            // プレハブの上書きとして Unity に知らせる。知らせないまま同じプレハブの別の上書きを戻すと
            // （UnposeFigures）、模型の縮尺と置き場まで素へ戻ってしまう
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        }

        /// <summary>
        /// 骨の上書きを消す。測りと持ち物のために置いた形を模型の素へ戻してから保存する。
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

        static Transform Bone(Dictionary<string, Transform> bones, string name)
        {
            Transform b;
            return bones.TryGetValue(name, out b) ? b : null;
        }

        /// <summary>
        /// 部位ごとのマテリアルを一枚に揃え、色を <see cref="PersonTint"/> に並べる。
        /// 知らない部位があれば言う（肌の色で塗っておく）
        /// </summary>
        static void PaintFigure(Transform root, GameObject model, Person person)
        {
            var mat = PersonMat();
            var rs = new List<Renderer>();
            var ss = new List<int>();
            var cs = new List<Color>();
            var gs = new List<float>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var n = smr.sharedMaterials.Length;
                var mats = new Material[n];
                for (var i = 0; i < n; i++)
                {
                    mats[i] = mat;
                    Part part;
                    if (!DiveCast.TryPartOf(person.model, smr.name, i, out part))
                    {
                        Debug.LogWarning("部位が分からない: " + person.model + "/" + smr.name + "[" + i + "]");
                        part = Part.Skin;
                    }
                    rs.Add(smr);
                    ss.Add(i);
                    cs.Add(person[part]);
                    gs.Add(DiveCast.GlossOf(part));
                }
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = false;
            }
            var tint = root.gameObject.AddComponent<PersonTint>();
            tint.Set(person.id, rs.ToArray(), ss.ToArray(), cs.ToArray(), gs.ToArray());
        }

        /// <summary>
        /// 人のマテリアル。全員で一枚。色と艶は <see cref="PersonTint"/> が部位ごとに上書きする。
        /// 映り込みを切るのは、暗い場所で服が空の色を拾って濡れたように光らないようにするため
        /// </summary>
        static Material PersonMat()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(PersonMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Person";
                AssetDatabase.CreateAsset(m, PersonMatPath);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.1f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>立ち・歩き・走りの動き。FBX の中に入っているものをそのまま使う</summary>
        static AnimationClip[] FigureClips(string model)
        {
            var clips = new AnimationClip[3];
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(ModelsAt + model + ".fbx"))
            {
                var c = o as AnimationClip;
                if (c == null) continue;
                if (c.name == StandClip) clips[0] = c;
                else if (c.name == WalkClip) clips[1] = c;
                else if (c.name == RunClip) clips[2] = c;
            }
            if (clips[0] == null) Debug.LogWarning("立ちの動きが無い: " + model);
            return clips;
        }

        /// <summary>PersonMotion の中身を書く。private な [SerializeField] なので SerializedObject 越し</summary>
        static void RigFigure(PersonMotion motion, Animator animator, Transform body, AnimationClip[] clips,
            bool seated, Vector3 seat, float[] strides, float runStride, float lag,
            List<Transform> sized, List<Vector3> sizes, List<Transform> bent, List<float> bends,
            List<Transform> held, Quaternion[] holds, Transform[] feet, Transform[] ankles)
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
            Fill(so.FindProperty("feet"), feet);
            Fill(so.FindProperty("ankles"), ankles);
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
        /// 模型の素の広がりのままだと、胴のレンダラーは肩までしか無く、頭のつもりで肩を狙うことになる
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
        /// 着いている足（いちばん低い所から 1.5 cm 以内の足）が人の根から見て後ろへ流れる速さを測る。
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

        /// <summary>姿勢ごとに向きを据える骨。立ちの動きのままの姿勢は無い</summary>
        static string[] HeldBones(int pose)
        {
            var arms = new[] { "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R" };
            switch (pose)
            {
                case 2:
                case 3:
                    return arms;
                case 5:
                case 6:
                case 7:
                    return new[]
                    {
                        "UpperLeg.L", "UpperLeg.R", "LowerLeg.L", "LowerLeg.R",
                        "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R",
                    };
                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// 据える骨の向き。素の模型を <see cref="FigurePosture"/> で曲げ、模型の根から見た向きとして取る。
        /// 骨の親（Body）は動きの中でひねられるので、親から見た向きで据えると脚がよそを向く
        /// </summary>
        static Quaternion[] FigureHolds(string model, int pose, string[] names)
        {
            var key = model + "/" + pose;
            Quaternion[] had;
            if (figureHolds.TryGetValue(key, out had)) return had;
            var all = new Quaternion[names.Length];
            if (names.Length > 0)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsAt + model + ".fbx");
                var scratch = (GameObject)Object.Instantiate(src);
                scratch.hideFlags = HideFlags.HideAndDontSave;
                scratch.transform.position = new Vector3(-7000f, 0f, -7000f);
                scratch.transform.rotation = Quaternion.identity;
                try
                {
                    FigurePosture(scratch.transform, pose);
                    var inverse = Quaternion.Inverse(scratch.transform.rotation);
                    for (var i = 0; i < names.Length; i++)
                    {
                        var b = FigureBoneOf(scratch.transform, names[i]);
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

        static Transform FigureBoneOf(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// 骨を曲げて姿勢を作る。<c>BuildAlley.Pose</c> の写し（路地裏の側は触らない決まりなので、
        /// 要る分をここへ写した）。素の模型（取り込んだままの姿勢）の上に曲げを重ねる。
        /// 模型は +z を向いている。角度はどれも「正なら前」
        /// </summary>
        static void FigurePosture(Transform root, int pose)
        {
            float thighL = 2f, thighR = -2f, kneeL = 0f, kneeR = 0f;
            float armL = 6f, armR = -6f, elbowL = 12f, elbowR = 12f;
            float outL = 4f, outR = -4f, spine = 2f, lean = 0f;

            switch (pose)
            {
                case 1:
                    thighL = 5f; thighR = -7f; kneeL = -4f; kneeR = 3f;
                    armL = 4f; armR = -9f; elbowL = 18f; elbowR = 10f;
                    outL = 7f; outR = -3f; spine = 1f;
                    break;
                case 2:  // 腕を組んで立つ
                    armL = 44f; armR = -44f; elbowL = 76f; elbowR = 76f;
                    outL = 12f; outR = -12f;
                    break;
                case 3:  // 手を後ろで組むように、腕を少し引いた立ち姿
                    armL = -12f; armR = -16f; elbowL = 26f; elbowR = 24f;
                    outL = 2f; outR = -6f; thighR = -4f; spine = 1f;
                    break;
                case 4:
                    armL = 10f; armR = -4f; elbowL = 14f; elbowR = 20f;
                    outL = 9f; outR = -8f; spine = 3f; thighL = -3f; thighR = 4f;
                    break;
                case 5:  // 椅子に座る
                    thighL = 85f; thighR = 84f; kneeL = -70f; kneeR = -72f;
                    outL = 12f; outR = -12f; armL = 26f; armR = 22f; elbowL = 48f; elbowR = 44f;
                    spine = 4f;
                    break;
                case 6:  // 卓に肘をついて座る
                    thighL = 84f; thighR = 86f; kneeL = -72f; kneeR = -68f;
                    outL = 16f; outR = -9f; armL = 14f; armR = 44f; elbowL = 26f; elbowR = 64f;
                    spine = 8f;
                    break;
                case 7:  // 膝を寄せて横を向いて座る
                    thighL = 86f; thighR = 83f; kneeL = -68f; kneeR = -74f;
                    outL = 3f; outR = -22f; armL = 38f; armR = 20f; elbowL = 56f; elbowR = 36f;
                    spine = 10f;
                    break;
            }

            FigureTurn(root, "Hips", lean, 0f);
            FigureTurn(root, "Abdomen", spine * 0.45f, 0f);
            FigureTurn(root, "Torso", spine * 0.35f, 0f);
            FigureTurn(root, "Chest", spine * 0.20f, 0f);
            FigureTurn(root, "Neck", -spine * 0.35f, 0f);
            FigureTurn(root, "Head", -spine * 0.25f, 0f);

            // 立ち姿は歩幅を詰める（素の模型は歩いている途中の姿勢で入っている）
            var close = IsSeated(pose) ? 0f : 9.5f;
            FigureTurn(root, "UpperLeg.L", -(thighL + close), outL * 0.25f);
            FigureTurn(root, "UpperLeg.R", -(thighR - close), outR * 0.25f);
            FigureTurn(root, "LowerLeg.L", -kneeL, 0f);
            FigureTurn(root, "LowerLeg.R", -kneeR, 0f);

            FigureTurn(root, "UpperArm.L", -armL, outL);
            FigureTurn(root, "UpperArm.R", -armR, outR);
            FigureTurn(root, "LowerArm.L", -elbowL, 0f);
            FigureTurn(root, "LowerArm.R", -elbowR, 0f);
        }

        /// <summary>骨ひとつを、体から見た軸で曲げる。左右で符号が揃う</summary>
        static void FigureTurn(Transform root, string bone, float pitch, float roll)
        {
            if (Mathf.Approximately(pitch, 0f) && Mathf.Approximately(roll, 0f)) return;
            var b = FigureBoneOf(root, bone);
            if (b == null) return;
            b.rotation = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward) * b.rotation;
        }

        // ---- 持ち物 ----------------------------------------------------------------
        //
        // 骨に付いて動かす。形は立った形を置いた人の根の座で作り、骨から見た置き場を覚えさせる

        /// <summary>
        /// 人の根の座で形を作り、骨に付ける。形は人の根の子に置いたまま、
        /// <see cref="PersonMotion.Carry"/> でこまの頭ごとに骨の所へ据え直させる
        /// </summary>
        static Transform FigureAttach(Transform who, string bone, string name, Mesh mesh, Material mat)
        {
            var go = Piece(who, name, mesh, mat);
            go.localPosition = Vector3.zero;
            go.localRotation = Quaternion.identity;
            var b = FigureBoneOf(who, bone);
            var motion = who.GetComponent<PersonMotion>();
            if (b != null && motion != null)
            {
                motion.Carry(go, b);
                EditorUtility.SetDirty(motion);
            }
            return go;
        }

        /// <summary>人の根の座での骨の位置</summary>
        static Vector3 FigureAt(Transform who, string bone)
        {
            var b = FigureBoneOf(who, bone);
            return b != null ? who.InverseTransformPoint(b.position) : Vector3.zero;
        }

        /// <summary>頭の広がり（頭のレンダラーの皮）を人の根の座で</summary>
        static Bounds FigureHead(Transform who)
        {
            var box = new Bounds();
            var first = true;
            var tmp = new Mesh();
            foreach (var smr in who.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.name.EndsWith("_Head")) continue;
                smr.BakeMesh(tmp, true);
                var at = who.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                foreach (var v in tmp.vertices)
                {
                    var p = at.MultiplyPoint3x4(v);
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
            }
            Object.DestroyImmediate(tmp);
            return box;
        }
    }
}
