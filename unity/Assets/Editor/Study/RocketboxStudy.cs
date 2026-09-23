using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HalfAware.EditorTools.Rocketbox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// Rocketbox の女大 14 から作った主人公と片割れを、顔の検証（<see cref="FaceStudy"/>）と同じ撮影台で撮って測る。
    /// 灯り・カメラ・測り方は FaceStudy のものをそのまま使い、書き先だけ rocketbox/ に分ける。
    ///
    /// 守ること（FaceStudy と同じ）: 作る物はすべて HideAndDontSave で、一回の呼び出しの中で作って、撮って、壊す。
    /// 場面は変えない。再生モードでもコンパイル中でも撮らない
    /// </summary>
    public static class RocketboxStudy
    {
        public const string OutDir = @"C:\Users\PC_User\AppData\Local\Temp\claude\D--work-kataaware\3eb6fc68-a1f9-4751-bb31-73277ee27f6d\scratchpad\rocketbox";

        /// <summary>撮る一人の指定</summary>
        public sealed class Variant
        {
            public string Tag;
            /// <summary>撮る人（女大 14 か女大 08）</summary>
            public RocketboxPerson Person = RocketboxPerson.Adult14;
            /// <summary>手を入れる前（縮めたテクスチャのまま）</summary>
            public bool Raw;
            public float Beauty = 0.5f;
            public float MoleMm = 5f;
            public bool Twin;
            public BuildRocketboxProtagonist.TwinMode Mode = BuildRocketboxProtagonist.Twin;
            public int HeadSize = 512;
            /// <summary>立ちの動きの初めのこまで立たせる（false なら取り込んだままの A の字）</summary>
            public bool Idle = true;
            /// <summary>見た目に重ねる手（顔の候補など）。null なら人の見た目のまま</summary>
            public System.Action<RocketboxPaint.Look> Tune;

            public RocketboxPaint.Look Look()
            {
                var k = Person.Look();
                if (Tune != null) Tune(k);
                k.beauty = Beauty;
                k.moleDiameter = MoleMm / 1000f;
                return k;
            }
        }

        // ---- 撮る一人を組み立てる ----------------------------------------------

        /// <summary>撮影台に載せる一人。壊すのは呼んだ側（using）</summary>
        public static FaceSubject Subject(Variant v)
        {
            var holder = new GameObject("RocketboxStudy");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = FaceStudy.Origin;
            var who = new FaceSubject { Root = holder };
            try
            {
                var look = v.Look();
                var skin = v.Raw ? BuildRocketboxProtagonist.MakeRawSkin(v.Person) : BuildRocketboxProtagonist.MakeSkin(v.Person, look, v.HeadSize, v.Twin && v.Mode == BuildRocketboxProtagonist.TwinMode.MoleOnly);
                who.Made.AddRange(skin.Made);

                // 印の絵と、黒子を消した頭（黒子で変わる画素を数える）
                var twinHead = v.Twin && v.Mode == BuildRocketboxProtagonist.TwinMode.MoleOnly;
                Texture2D mask;
                RocketboxPaint.HeadResult info;
                var bare = look.Clone();
                bare.moleDiameter = 0f;
                if (v.Raw) bare.beauty = 0f;
                var scratch = new BuildRocketboxProtagonist.Skin(v.Person);
                var bareHead = BuildRocketboxProtagonist.MakeHead(scratch, bare, v.HeadSize, twinHead, out info, out mask);
                who.Made.AddRange(scratch.Made);
                if (!v.Raw) mask = twinHead ? skin.MaskHeadTwin : skin.MaskHead;

                var her = BuildRocketboxProtagonist.Assemble(holder.transform, v.Twin, skin, v.Mode);
                FaceSubject.HideAll(holder);
                var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
                var an = her.GetComponent<Animator>();

                // 顔の中心・目の高さ・正面は、束ねた姿勢で求めて頭の骨に付ける（立たせると頭が少し動く）
                var maps = BuildRocketboxProtagonist.Maps.Get(v.Person, 512);
                var a = maps.Anchors;
                var head = an.GetBoneTransform(HumanBodyBones.Head);
                var root = her.transform;
                var centre = head.InverseTransformPoint(root.TransformPoint(new Vector3(0f, (a.eyeL.y + a.upperLip.y) * 0.5f, a.nose.z - 0.012f)));
                var eyeLine = head.InverseTransformPoint(root.TransformPoint(new Vector3(0f, a.eyeL.y, a.eyeL.z)));
                var forward = head.InverseTransformDirection(root.TransformDirection(Vector3.forward));

                if (v.Idle) PoseIdle(her);

                who.FaceCentreLocal = holder.transform.InverseTransformPoint(head.TransformPoint(centre));
                who.EyeLineLocal = holder.transform.InverseTransformPoint(head.TransformPoint(eyeLine));
                var f = holder.transform.InverseTransformDirection(head.TransformDirection(forward));
                f.y = 0f;
                who.ForwardLocal = f.normalized;
                who.FaceWidth = FaceWidth(maps, v.Person);

                var slots = smr.sharedMaterials;
                int headSlot = -1, hairSlot = -1;
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == skin.Head || slots[i] == skin.HeadTwin) headSlot = i;
                    if (slots[i] == skin.Hair) hairSlot = i;
                    if (skin.Lash != null && slots[i] == skin.Lash) who.MaskSlots.Add(new MaskSlot(smr, i, HairMask(who, v.Person.LashSrc)));
                }
                who.MaskSlots.Add(new MaskSlot(smr, headSlot, mask));
                who.MaskSlots.Add(new MaskSlot(smr, hairSlot, HairMask(who, v.Person.HairSrc)));
                if (!v.Raw && look.moleDiameter > 0f)
                    who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(smr, headSlot, null), bareHead));
                who.Note = string.Format(CultureInfo.InvariantCulture, "{0}{1}", v.Raw ? "手を入れる前" : (twinHead ? info.Note : (skin.HeadInfo != null ? skin.HeadInfo.Note : "")), v.Twin ? "（片割れ " + v.Mode + "）" : "");
                who.Isolate();
                return who;
            }
            catch
            {
                who.Dispose();
                throw;
            }
        }

        /// <summary>髪の房の印。α は髪のマテリアルと同じ閾値で切り、色は地の灰</summary>
        static Texture2D HairMask(FaceSubject who, string opacityPath)
        {
            int n;
            var px = RocketboxTextures.ReadPng(opacityPath, out n, out n);
            var o = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++) o[i] = new Color32(38, 38, 38, px[i].a >= 115 ? (byte)255 : (byte)0);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixels32(o);
            t.Apply(true);
            who.Made.Add(t);
            return t;
        }

        /// <summary>目の高さの肌の幅（m）。頭のテクスチャの明るい（髪でない）画素の、目の高さでの左右の広がり</summary>
        static float FaceWidth(BuildRocketboxProtagonist.Maps maps, RocketboxPerson person)
        {
            int n;
            var px = RocketboxTextures.ReadPng(person.HeadSrc, out n, out n);
            var s = maps.Head;
            var a = maps.Anchors;
            float lo = 0f, hi = 0f;
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                if (Mathf.Abs(p.y - a.eyeL.y) > 0.002f || p.z < a.head.z) continue;
                float h, sat, val;
                Color.RGBToHSV(px[i], out h, out sat, out val);
                if (val < 0.5f) continue;
                lo = Mathf.Min(lo, p.x);
                hi = Mathf.Max(hi, p.x);
            }
            return hi - lo;
        }

        /// <summary>Humanoid に移し替えた立ちの動きの初めのこまで立たせる</summary>
        public static void PoseIdle(GameObject her)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RocketboxRetarget.OutDir + "/Idle.anim");
            if (clip == null) return;
            SampleHuman(her, clip, 0f);
        }

        /// <summary>Humanoid の動きの一こまを、再生せずに骨へ写す（PlayableGraph を一度だけ評価する）</summary>
        public static void SampleHuman(GameObject her, AnimationClip clip, float t)
        {
            var an = her.GetComponent<Animator>();
            var keep = an.runtimeAnimatorController;
            var graph = PlayableGraph.Create("RocketboxStudy");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "pose", an);
                var p = AnimationClipPlayable.Create(graph, clip);
                p.SetApplyFootIK(false);
                output.SetSourcePlayable(p);
                p.SetTime(t);
                p.SetTime(t);
                graph.Evaluate(0f);
            }
            finally
            {
                graph.Destroy();
                an.runtimeAnimatorController = keep;
            }
        }

        // ---- 撮る -------------------------------------------------------------

        /// <summary>
        /// 一人を一つの光で、三つの近さ × 三つの向きで撮って測る（FaceStudy.ShootSet と同じ並び）。
        /// 絵は OutDir へ {tag}_{光}_{d}m_{向き}.png（320×180）と _960.png
        /// </summary>
        public static string ShootSet(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                sb.AppendLine(v.Tag + " " + light + ": " + Describe(who));
                foreach (var d in FaceStudy.Distances)
                    foreach (var yaw in FaceStudy.Yaws)
                    {
                        var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2:0.0}m_{3}", v.Tag, FaceStudy.LightName(light), d, FaceStudy.YawName(yaw));
                        var m = ShootOne(rig, who, d, yaw, name, null);
                        Record(v.Tag, light.ToString(), d, yaw, m);
                        sb.AppendLine(name + " " + m.Short());
                    }
            }
            return sb.ToString();
        }

        /// <summary>端末の黒い画面への映り込み（FaceStudy.ShootMirror と同じ作り）。映った顔は鏡像になる</summary>
        public static string ShootMirror(Variant v)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(FaceStudy.Lighting.Soft))
            using (var who = Subject(v))
            {
                rig.Light(who);
                var s = who.Root.transform.localScale;
                who.Root.transform.localScale = new Vector3(-s.x, s.y, s.z);
                var glass = rig.Quad("RocketboxStudyGlass", rig.Glass(FaceStudy.ScreenOff, 0.72f));
                foreach (var gap in new[] { 0.6f, 0.4f })
                {
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_mirror_{1:0.0}m", v.Tag, gap);
                    var m = ShootOne(rig, who, gap * 2f, 0f, name, glass);
                    Record(v.Tag, "Mirror", gap, 0f, m);
                    sb.AppendLine(name + " " + m.Short());
                }
                // 鏡像だけ（硝子を挟まない）。正面の片割れと並べるため
                glass.SetActive(false);
                foreach (var d in new[] { 0.4f, 1.0f })
                {
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_flipped_{1:0.0}m", v.Tag, d);
                    var m = ShootOne(rig, who, d, 0f, name, null);
                    sb.AppendLine(name + " " + m.Short());
                }
            }
            return sb.ToString();
        }

        /// <summary>全身。正面・横（本人の左 90 度）・斜め（左 45 度）・後ろ。2.2 m から腰の高さを見る。320×180 と 960×540</summary>
        /// <summary>
        /// 上半身（肩の形）。胸の高さを 1.2 m から、正面と横（本人の左 90 度）と背中で。320×180 と 960×540
        /// </summary>
        public static string ShootUpper(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                var an = who.Root.GetComponentInChildren<Animator>();
                var chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest);
                var target = new Vector3(who.Root.transform.position.x, chest.position.y, who.Root.transform.position.z);
                foreach (var yaw in new[] { 0f, -90f, 180f })
                {
                    rig.Place(target + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * 1.2f, target);
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_upper_{2}", v.Tag, FaceStudy.LightName(light), BodyView(yaw));
                    SaveShots(rig, name);
                    sb.AppendLine(name);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 首の継ぎ目の寄り。首の付け根を 0.45 m から、正面・左右 30 度・左右 60 度・背中で、画角 30 度・960×540
        /// （ゲームの見え方ではない）。地の色を緑にして、面の隙間から向こうが見えれば分かるようにする
        /// </summary>
        public static string ShootNeck(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                rig.Camera.backgroundColor = new Color(0.10f, 0.60f, 0.20f);
                var an = who.Root.GetComponentInChildren<Animator>();
                var neck = an.GetBoneTransform(HumanBodyBones.Neck).position + Vector3.down * 0.05f;
                foreach (var yaw in new[] { 0f, -30f, 30f, -60f, 60f, 180f })
                {
                    rig.Place(neck + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * 0.45f + Vector3.up * 0.05f, neck);
                    rig.Camera.fieldOfView = 30f;
                    var shot = rig.Render(FaceStudy.BigW, FaceStudy.BigH);
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_neck_{2}", v.Tag, FaceStudy.LightName(light), FaceStudy.YawName(yaw) == "front" ? "front" : Mathf.Abs(yaw - 180f) < 0.5f ? "back" : FaceStudy.YawName(yaw));
                    try { FaceStudy.Save(shot, Path.Combine(OutDir, name + ".png")); }
                    finally { Object.DestroyImmediate(shot); }
                    sb.AppendLine(name);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 首の継ぎ目をゲームの見え方で撮る。首の付け根を 0.4 m と 1 m から、正面・横（本人の左 90 度）・後ろで。320×180 と 960×540
        /// </summary>
        public static string ShootSeam(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                var an = who.Root.GetComponentInChildren<Animator>();
                var neck = an.GetBoneTransform(HumanBodyBones.Neck).position + Vector3.down * 0.04f;
                foreach (var d in new[] { 0.4f, 1.0f })
                    foreach (var yaw in new[] { 0f, -90f, 180f })
                    {
                        rig.Place(neck + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * d, neck);
                        var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_seam_{2:0.0}m_{3}", v.Tag, FaceStudy.LightName(light), d, BodyView(yaw));
                        SaveShots(rig, name);
                        sb.AppendLine(name);
                    }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 生え際まわりの寄り。額の上を 0.55 m から、正面・左右 30 度・左右の横・後ろ・上で、画角 32 度・960×540
        /// （ゲームの見え方ではない）。地を緑にして、面の隙間から向こうが見えれば分かるようにする
        /// </summary>
        public static string ShootHairline(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                rig.Camera.backgroundColor = new Color(0.10f, 0.60f, 0.20f);
                var c = who.FaceCentre + Vector3.up * 0.05f;
                foreach (var yaw in new[] { 0f, -30f, 30f, -90f, 90f, 180f })
                {
                    rig.Place(c + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * 0.55f + Vector3.up * 0.06f, c);
                    rig.Camera.fieldOfView = 32f;
                    Save(rig, string.Format(CultureInfo.InvariantCulture, "{0}_{1}_hairline_{2}", v.Tag, FaceStudy.LightName(light), Mathf.Abs(yaw) < 0.5f ? "front" : Mathf.Abs(yaw - 180f) < 0.5f ? "back" : FaceStudy.YawName(yaw)), sb);
                }
                rig.Place(c + Vector3.up * 0.5f + who.Forward * 0.25f, c);
                Save(rig, string.Format(CultureInfo.InvariantCulture, "{0}_{1}_hairline_top", v.Tag, FaceStudy.LightName(light)), sb);
            }
            return sb.ToString();
        }

        static void Save(FaceStudy.Rig rig, string name, StringBuilder sb)
        {
            var shot = rig.Render(FaceStudy.BigW, FaceStudy.BigH);
            try { FaceStudy.Save(shot, Path.Combine(OutDir, name + ".png")); }
            finally { Object.DestroyImmediate(shot); }
            sb.AppendLine(name);
        }

        /// <summary>立ちの初めのこまと、歩きの一回りを 6 こまで、髪が服に入り込んでいないかを測る</summary>
        public static string HairCheck(Variant v)
        {
            var sb = new StringBuilder();
            using (var who = Subject(v))
            {
                var her = who.Root.GetComponentInChildren<Animator>().gameObject;
                sb.AppendLine(v.Person + " 立ち: " + RocketboxCompose.HairInside(her, v.Person));
                var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(RocketboxRetarget.OutDir + "/Walk.anim");
                for (var f = 0; f < 6; f++)
                {
                    var t = walk.length * f / 6f;
                    SampleHuman(her, walk, t);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} 歩き {1:0.00} 秒: {2}", v.Person, t, RocketboxCompose.HairInside(her, v.Person)));
                }
            }
            return sb.ToString();
        }

        public static string ShootBody(Variant v, FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = Subject(v))
            {
                rig.Light(who);
                sb.AppendLine(v.Tag + " body: " + Describe(who));
                var o = who.Root.transform.position;
                foreach (var yaw in new[] { 0f, -90f, -45f, 180f })
                {
                    var target = o + new Vector3(0f, 0.88f, 0f);
                    var eye = target + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * 2.2f;
                    rig.Place(eye, target);
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_body_{2}", v.Tag, FaceStudy.LightName(light), BodyView(yaw));
                    SaveShots(rig, name);
                    sb.AppendLine(name);
                }
            }
            return sb.ToString();
        }

        static string BodyView(float yaw)
        {
            if (Mathf.Abs(yaw) < 0.5f) return "front";
            if (Mathf.Abs(yaw - 180f) < 0.5f) return "back";
            if (Mathf.Abs(yaw + 90f) < 0.5f) return "side";
            return FaceStudy.YawName(yaw);
        }

        /// <summary>
        /// 髪の長さ。立ちの動きの初めのこまで、髪の房（透けの面）と、頭のテクスチャで髪と見なした所の頂点のうち、
        /// 一番低い所を後ろ・前・横に分けて測る（まつ毛は目から 3.5 cm 以内なので外す）。
        /// 比べる高さは、肩の関節（上腕の骨の付け根）と、顎の先（顔の真ん中の一番低い肌）
        /// </summary>
        public static string HairLength(Variant v)
        {
            using (var who = Subject(v))
            {
                var her = who.Root.GetComponentInChildren<Animator>();
                var smr = who.Root.GetComponentInChildren<SkinnedMeshRenderer>();
                var baked = new Mesh();
                try
                {
                    smr.BakeMesh(baked, true);
                    var verts = baked.vertices;
                    var uv = smr.sharedMesh.uv;
                    var ground = who.Root.transform.position.y;
                    var head = her.GetBoneTransform(HumanBodyBones.Head).position;
                    var shoulder = (her.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y + her.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y) * 0.5f - ground;
                    var eyes = new List<Vector3>();
                    foreach (var t in who.Root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Bip01 LEye" || t.name == "Bip01 REye") eyes.Add(t.position);
                    var fwd = who.Forward;
                    var right = who.Right;
                    var slots = smr.sharedMaterials;
                    int headSlot = -1, hairSlot = -1, shellSlot = -1;
                    for (var i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] != null && slots[i].name.StartsWith("Head", StringComparison.Ordinal)) headSlot = i;
                        if (slots[i] != null && slots[i].name.StartsWith("Hair", StringComparison.Ordinal)) hairSlot = i;
                        if (slots[i] != null && slots[i].name.StartsWith("Shell", StringComparison.Ordinal)) shellSlot = i;
                    }
                    // 頭のテクスチャで髪と見なした重み
                    var look = v.Look();
                    var scratch = new BuildRocketboxProtagonist.Skin(v.Person);
                    RocketboxPaint.HeadResult info;
                    Texture2D mask;
                    BuildRocketboxProtagonist.MakeHead(scratch, look, 512, false, out info, out mask);
                    who.Made.AddRange(scratch.Made);

                    float low = float.MaxValue, back = float.MaxValue, front = float.MaxValue, side = float.MaxValue, chin = float.MaxValue, top = float.MinValue;
                    var seen = new HashSet<int>();
                    Action<Vector3> hairAt = p =>
                    {
                        var y = p.y - ground;
                        var local = p - head;
                        var f = Vector3.Dot(local, fwd);
                        var r = Vector3.Dot(local, right);
                        low = Mathf.Min(low, y);
                        if (f < -0.03f) back = Mathf.Min(back, y);
                        else if (f > 0.03f) front = Mathf.Min(front, y);
                        if (Mathf.Abs(r) > 0.07f && Mathf.Abs(f) < 0.06f) side = Mathf.Min(side, y);
                    };
                    // 肩の上面: 体の肌と服の、肩の関節の真上あたり（左右 12〜17 cm、前後 6 cm 以内）の一番高い所
                    for (var s = 0; s < smr.sharedMesh.subMeshCount; s++)
                    {
                        if (s == hairSlot || s == shellSlot) continue;
                        foreach (var i in smr.sharedMesh.GetTriangles(s))
                        {
                            var p = smr.transform.TransformPoint(verts[i]);
                            var local = p - head;
                            var r = Mathf.Abs(Vector3.Dot(local, right));
                            if (r > 0.12f && r < 0.17f && Mathf.Abs(Vector3.Dot(local, fwd)) < 0.06f && p.y - ground < shoulder + 0.12f) top = Mathf.Max(top, p.y - ground);
                        }
                    }
                    for (var s = 0; s < smr.sharedMesh.subMeshCount; s++)
                    {
                        if (s != headSlot && s != hairSlot && s != shellSlot) continue;
                        foreach (var i in smr.sharedMesh.GetTriangles(s))
                        {
                            if (!seen.Add(i)) continue;
                            var p = smr.transform.TransformPoint(verts[i]);
                            if (s == shellSlot)
                            {
                                hairAt(p);
                                continue;
                            }
                            if (s == hairSlot)
                            {
                                var lash = false;
                                foreach (var e in eyes) if (Vector3.Distance(p, e) < 0.035f) lash = true;
                                if (!lash) hairAt(p);
                                continue;
                            }
                            var local = p - head;
                            // 顎の先: 顔の真ん中で、唇とほぼ同じだけ前へ出ている所の一番低い所（首の前は出ていないので入らない）
                            if (Mathf.Abs(Vector3.Dot(local, right)) < 0.015f && Vector3.Dot(local, fwd) > 0.09f) chin = Mathf.Min(chin, p.y - ground);
                            // 顔と髪が別の人なら、顔の面の髪（埋め）は殻の内側なので数えない
                            if (shellSlot >= 0) continue;
                            var n = info.N;
                            var x = Mathf.Clamp((int)(uv[i].x * n), 0, n - 1);
                            var yy = Mathf.Clamp((int)(uv[i].y * n), 0, n - 1);
                            if (info.Hair[yy * n + x] > 0.5f) hairAt(p);
                        }
                    }
                    Func<float, string> vsShoulder = y => y == float.MaxValue ? "無し" : string.Format(CultureInfo.InvariantCulture, "{0:0.000} m（肩の上面から {1:+0.0;-0.0} cm、肩の関節から {2:+0.0;-0.0} cm、顎の先から {3:+0.0;-0.0} cm）", y, (y - top) * 100f, (y - shoulder) * 100f, (y - chin) * 100f);
                    return string.Format(CultureInfo.InvariantCulture,
                        "{0}: 床から 肩の上面 {1:0.000} m、肩の関節 {2:0.000} m、顎の先 {3:0.000} m / 髪の一番下 {4}、後ろ {5}、前 {6}、横 {7}",
                        v.Person, top, shoulder, chin, vsShoulder(low), vsShoulder(back), vsShoulder(front), vsShoulder(side));
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }
        }

        static void SaveShots(FaceStudy.Rig rig, string name)
        {
            var small = rig.Render(FaceStudy.GameW, FaceStudy.GameH);
            var big = rig.Render(FaceStudy.BigW, FaceStudy.BigH);
            try
            {
                FaceStudy.Save(small, Path.Combine(OutDir, name + ".png"));
                FaceStudy.Save(big, Path.Combine(OutDir, name + "_960.png"));
            }
            finally
            {
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(big);
            }
        }

        /// <summary>FaceStudy.ShootOne と同じ撮り方と測り方で、書き先だけ OutDir</summary>
        public static FaceStudy.Measure ShootOne(FaceStudy.Rig rig, FaceSubject who, float d, float yaw, string name, GameObject glass)
        {
            var c = who.FaceCentre;
            var eye = c + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * d;
            rig.Place(eye, c);
            if (glass != null)
            {
                glass.transform.position = eye + (c - eye).normalized * 0.12f;
                glass.transform.rotation = Quaternion.LookRotation(c - eye, Vector3.up);
                glass.transform.localScale = Vector3.one * 0.5f;
            }
            var hide = glass != null ? new List<GameObject> { glass } : null;
            var small = rig.Render(FaceStudy.GameW, FaceStudy.GameH);
            var big = rig.Render(FaceStudy.BigW, FaceStudy.BigH);
            var markSmall = rig.RenderMarks(who, FaceStudy.GameW, FaceStudy.GameH, hide);
            var markBig = rig.RenderMarks(who, FaceStudy.BigW, FaceStudy.BigH, hide);
            Texture2D bare = null;
            if (who.MoleOff.Count > 0)
            {
                var keep = who.HideMole();
                try { bare = rig.Render(FaceStudy.GameW, FaceStudy.GameH); }
                finally { FaceSubject.Restore(keep); }
            }
            try
            {
                FaceStudy.Save(small, Path.Combine(OutDir, name + ".png"));
                FaceStudy.Save(big, Path.Combine(OutDir, name + "_960.png"));
                var px = small.GetPixels32();
                var m = FaceStudy.Measure.Take(px, markSmall.GetPixels32(), markBig.GetPixels32());
                m.faceW = ProjectedWidth(rig.Camera, who);
                if (bare != null) m.MoleChange(px, bare.GetPixels32());
                return m;
            }
            finally
            {
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(markSmall);
                Object.DestroyImmediate(markBig);
                if (bare != null) Object.DestroyImmediate(bare);
            }
        }

        static float ProjectedWidth(Camera cam, FaceSubject who)
        {
            cam.aspect = FaceStudy.GameW / (float)FaceStudy.GameH;
            var c = who.EyeLine;
            var r = who.Right * (who.FaceWidth * 0.5f);
            var a = cam.WorldToViewportPoint(c - r);
            var b = cam.WorldToViewportPoint(c + r);
            return Mathf.Abs(b.x - a.x) * FaceStudy.GameW;
        }

        /// <summary>三角の数とテクスチャの大きさと形式</summary>
        public static string Describe(FaceSubject who)
        {
            long tris = 0;
            var texes = new HashSet<Texture>();
            foreach (var r in who.Root.GetComponentsInChildren<Renderer>(true))
            {
                var smr = r as SkinnedMeshRenderer;
                Mesh mesh = smr != null ? smr.sharedMesh : null;
                if (mesh == null)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh != null) for (var s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) texes.Add(m.GetTexture("_BaseMap"));
            }
            var sb = new StringBuilder();
            sb.AppendFormat("三角 {0}、テクスチャ", tris);
            foreach (var t in texes)
            {
                var t2 = t as Texture2D;
                sb.AppendFormat(" {0} {1}×{2} {3}", t.name, t.width, t.height, t2 != null ? t2.format.ToString() : "");
            }
            if (!string.IsNullOrEmpty(who.Note)) sb.Append(" / " + who.Note);
            return sb.ToString();
        }

        // ---- 今の主人公（Quaternius）と並べる -----------------------------------

        /// <summary>今の主人公（段 0 のまま、つまり今のゲームの Face.mat）を同じ条件で撮る</summary>
        public static string ShootCurrent(FaceStudy.Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(light))
            using (var who = FaceStages.Build(0, true))
            {
                rig.Light(who);
                sb.AppendLine("current " + light + ": " + who.Describe());
                foreach (var d in FaceStudy.Distances)
                    foreach (var yaw in FaceStudy.Yaws)
                    {
                        var name = string.Format(CultureInfo.InvariantCulture, "current_{0}_{1:0.0}m_{2}", FaceStudy.LightName(light), d, FaceStudy.YawName(yaw));
                        var m = ShootOne(rig, who, d, yaw, name, null);
                        Record("current", light.ToString(), d, yaw, m);
                        sb.AppendLine(name + " " + m.Short());
                    }
                var o = who.Root.transform.position;
                foreach (var yaw in new[] { 0f, -45f, 180f })
                {
                    var target = o + new Vector3(0f, 0.88f, 0f);
                    rig.Place(target + Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward * 2.2f, target);
                    SaveShots(rig, string.Format("current_{0}_body_{1}", FaceStudy.LightName(light), yaw == 0f ? "front" : yaw == 180f ? "back" : "L45"));
                }
            }
            return sb.ToString();
        }

        // ---- 歩き -------------------------------------------------------------

        /// <summary>
        /// 状態機械（Humanoid の写し）で立ちと歩きを動かし、横から撮る。
        /// 比べるために、今の主人公も今の状態機械で同じこまを撮る。Animator.Update でエディタの中だけで進める
        /// </summary>
        public static string ShootWalk()
        {
            var sb = new StringBuilder();
            using (var rig = new FaceStudy.Rig(FaceStudy.Lighting.Soft))
            using (var rb = Subject(new Variant { Tag = "walk", Idle = false }))
            using (var q = FaceStages.Build(0, true))
            {
                rig.Light(rb);
                rig.Light(q);
                q.Root.transform.position = FaceStudy.Origin + new Vector3(1.2f, 0f, 0f);
                var who = new[] { rb, q };
                var names = new[] { "rocketbox", "current" };
                for (var k = 0; k < 2; k++)
                {
                    var an = who[k].Root.GetComponentInChildren<Animator>();
                    if (an.runtimeAnimatorController == null) throw new InvalidOperationException("状態機械が無い: " + names[k]);
                    an.enabled = true;
                    an.Rebind();
                    an.SetFloat("speed", 0f);
                    an.SetFloat("cycle", 1f);
                    an.Update(0f);
                    an.Update(0.5f);
                }
                Func<int, string> shoot = null;
                shoot = frame =>
                {
                    var line = new StringBuilder();
                    for (var k = 0; k < 2; k++)
                    {
                        var o = who[k].Root.transform.position;
                        var target = o + new Vector3(0f, 0.88f, 0f);
                        // 本人の右（+x）の横から。もう一人は撮る間だけ隠す
                        rig.Place(target + Vector3.right * 2.4f, target);
                        var name = string.Format("walk_{0}_{1:00}", names[k], frame);
                        var others = who[1 - k].Root.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in others) r.enabled = false;
                        try { SaveShots(rig, name); }
                        finally { foreach (var r in others) r.enabled = true; }
                        var an = who[k].Root.GetComponentInChildren<Animator>();
                        var human = an.avatar != null && an.isHuman;
                        var lf = human ? an.GetBoneTransform(HumanBodyBones.LeftFoot) : Find(who[k].Root, "Foot.L");
                        var rf = human ? an.GetBoneTransform(HumanBodyBones.RightFoot) : Find(who[k].Root, "Foot.R");
                        var st = an.GetCurrentAnimatorStateInfo(0);
                        if (lf != null && rf != null)
                            line.AppendFormat(CultureInfo.InvariantCulture, "{0} 足 L({1:0.00},{2:0.00}) R({3:0.00},{4:0.00}) 状態 {5} ", names[k],
                                lf.position.y - o.y, lf.position.z - o.z, rf.position.y - o.y, rf.position.z - o.z, st.IsName("Walk") ? "Walk" : st.IsName("Idle") ? "Idle" : "?");
                        else
                            line.AppendFormat("{0} 状態 {1} ", names[k], st.IsName("Walk") ? "Walk" : st.IsName("Idle") ? "Idle" : "?");
                    }
                    return line.ToString();
                };
                sb.AppendLine("立ち " + shoot(0));
                for (var k = 0; k < 2; k++)
                {
                    var an = who[k].Root.GetComponentInChildren<Animator>();
                    an.SetFloat("speed", 1.2f);
                    an.Update(0.4f);
                }
                // 歩きの一回り（1.667 秒）を 6 こまで
                for (var f = 1; f <= 6; f++)
                {
                    sb.AppendLine("歩き " + shoot(f));
                    for (var k = 0; k < 2; k++) who[k].Root.GetComponentInChildren<Animator>().Update(1.6667f / 6f);
                }
            }
            return sb.ToString();
        }

        static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ---- 測った数と一覧の絵 -----------------------------------------------

        const string CsvHead = "tag,light,distance,yaw,face_px,ipd_px,mole_px,mole_subpixel,mole_luma,ring_luma,delta_luma,iris_px,amber_px,iris_h,iris_s,iris_v,mole_change_px,mole_change_max,eye_centre_h,eye_centre_s,eye_centre_v";

        public static void Record(string tag, string light, float d, float yaw, FaceStudy.Measure m)
        {
            Directory.CreateDirectory(OutDir);
            var path = Path.Combine(OutDir, "measure.csv");
            var lines = new List<string>();
            var key = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.0},{3:0},", tag, light, d, yaw);
            if (File.Exists(path))
                foreach (var l in File.ReadAllLines(path))
                    if (!l.StartsWith(key, StringComparison.Ordinal)) lines.Add(l);
            if (lines.Count == 0 || lines[0] != CsvHead) lines.Insert(0, CsvHead);
            lines.Add(key + string.Format(CultureInfo.InvariantCulture,
                "{0:0.0},{1:0.0},{2},{3},{4:0.0},{5:0.0},{6:0.0},{7},{8},{9:0},{10:0.00},{11:0.00},{12},{13:0},{14:0},{15:0.00},{16:0.00}",
                m.faceW, m.ipd, m.molePx, m.moleSub ? 1 : 0, m.moleLuma, m.ringLuma, m.moleSeen ? m.DeltaL : float.NaN,
                m.irisPx, m.amberPx, m.irisH, m.irisS, m.irisV, m.changePx, m.changeMax, m.eyeH, m.eyeS, m.eyeV));
            File.WriteAllLines(path, lines.ToArray());
        }

        /// <summary>
        /// 並べた一覧の絵。cells[行][列] は OutDir の中の絵の名前（拡張子無し）。
        /// crop は絵の中の矩形（x, y は左下から）、scale は画素のまま拡大する倍率。無い絵は灰で埋める
        /// </summary>
        public static string Sheet(string[][] cells, string outName, RectInt crop, int scale)
        {
            const int gap = 4;
            var rows = cells.Length;
            var cols = 0;
            foreach (var r in cells) cols = Mathf.Max(cols, r.Length);
            int cw = crop.width * scale, ch = crop.height * scale;
            var W = cols * cw + (cols + 1) * gap;
            var H = rows * ch + (rows + 1) * gap;
            var fill = new Color32[W * H];
            for (var i = 0; i < fill.Length; i++) fill[i] = new Color32(40, 40, 44, 255);
            var missing = new List<string>();
            for (var ri = 0; ri < rows; ri++)
                for (var ci = 0; ci < cells[ri].Length; ci++)
                {
                    var nm = cells[ri][ci];
                    if (string.IsNullOrEmpty(nm)) continue;
                    var path = Path.Combine(OutDir, nm + ".png");
                    if (!File.Exists(path)) { missing.Add(nm); continue; }
                    var t = new Texture2D(2, 2);
                    t.hideFlags = HideFlags.HideAndDontSave;
                    try
                    {
                        t.LoadImage(File.ReadAllBytes(path));
                        var px = t.GetPixels32();
                        int ox = gap + ci * (cw + gap);
                        int oy = H - (gap + ri * (ch + gap)) - ch;
                        for (var y = 0; y < ch; y++)
                            for (var x = 0; x < cw; x++)
                            {
                                int sx = crop.x + x / scale, sy = crop.y + y / scale;
                                if (sx < 0 || sy < 0 || sx >= t.width || sy >= t.height) continue;
                                fill[(oy + y) * W + ox + x] = px[sy * t.width + sx];
                            }
                    }
                    finally
                    {
                        Object.DestroyImmediate(t);
                    }
                }
            var sheet = new Texture2D(W, H, TextureFormat.RGBA32, false);
            sheet.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                sheet.SetPixels32(fill);
                sheet.Apply();
                FaceStudy.Save(sheet, Path.Combine(OutDir, outName));
            }
            finally
            {
                Object.DestroyImmediate(sheet);
            }
            return outName + (missing.Count > 0 ? "（無い絵: " + string.Join(", ", missing.ToArray()) + "）" : "");
        }

        /// <summary>320×180 の真ん中 128×72（顔の周り）</summary>
        public static readonly RectInt FaceCrop = new RectInt(96, 54, 128, 72);
    }
}
