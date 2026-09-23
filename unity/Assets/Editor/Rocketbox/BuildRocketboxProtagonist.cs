using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 主人公と片割れを Microsoft Rocketbox の一人（<see cref="RocketboxPerson"/>。今は女大 14 と女大 08 を撮り比べている）から組み立てる。
    /// 顔も体もその一人。髪を黒に、目の下に黒子、顔に控えめな手入れ、ニットの服の人はカーディガンを黒に
    /// （<see cref="RocketboxPaint"/>）。目は元の色のまま。場面には置かない（置くのは次の段で、場面の組み立てから <see cref="Build"/> を呼ぶ）。
    ///
    /// 片割れは主人公の鏡像。模型の根の x を裏返して、体ごと左右を入れ替える（<see cref="Twin"/>）。
    /// 黒子の絵は主人公と同じ一枚で、裏返した結果として本人の右目の下に来る。髪の分け目も逆になる
    /// </summary>
    public static class BuildRocketboxProtagonist
    {
        /// <summary>主人公にする人。オーナーが見比べて、女大 14 の顔と体に女大 08 の髪を載せた人に決めた</summary>
        public static readonly RocketboxPerson Chosen = RocketboxPerson.Face14Hair14BodySports02;

        /// <summary>立ちと歩きの状態機械（今の Protagonist.controller の写しに、Humanoid へ移し替えた動きを差した物。<see cref="RocketboxRetarget"/> が作る）</summary>
        public const string Controller = "Assets/Animation/Humanoid/ProtagonistHumanoid.controller";

        /// <summary>片割れの作り方</summary>
        public enum TwinMode
        {
            /// <summary>模型の根の x を裏返す。髪の分け目も黒子も逆になる</summary>
            MirrorWhole,
            /// <summary>同じ模型のまま、黒子だけ右目の下に描いた絵にする</summary>
            MoleOnly,
        }

        public const TwinMode Twin = TwinMode.MirrorWhole;

        /// <summary>頭のテクスチャの大きさ。256 では 0.4 m の黒子が薄れるので 512</summary>
        public const int HeadSize = 512;

        /// <summary>頭の Specular の絵の大きさ（髪か肌かの区別だけなので小さくてよい）</summary>
        public const int SpecSize = 256;

        // ---- 組み立て ---------------------------------------------------------

        /// <summary>
        /// 組み立てて返す。parent が null なら場面の根に置く。twin なら片割れ。
        /// 手を入れたテクスチャが無ければ描いてから使う
        /// </summary>
        public static GameObject Build(Transform parent, bool twin)
        {
            // 片割れは、主人公と同じ顔と髪を別の体に載せた人（TwinPerson）を、模型ごと裏返して組み立てる
            return Build(parent, twin, twin && Chosen.TwinPerson != null ? Chosen.TwinPerson : Chosen);
        }

        public static GameObject Build(Transform parent, bool twin, RocketboxPerson who)
        {
            var skin = LoadPainted(who, twin && Twin == TwinMode.MoleOnly);
            if (skin == null)
            {
                Paint(who, who.Look(), Twin == TwinMode.MoleOnly);
                skin = LoadPainted(who, twin && Twin == TwinMode.MoleOnly);
            }
            if (skin == null) throw new InvalidOperationException("手を入れたマテリアルを作れない: " + who.Painted);
            skin.JawScale = who.Look().JawScale;
            skin.JawClose = who.Look().jawClose;
            skin.Look = who.Look();
            return Assemble(parent, twin, skin, Twin);
        }

        /// <summary>マテリアルの組を模型に着せる。撮り比べでは描いたばかりの（アセットでない）組を渡す</summary>
        public static GameObject Assemble(Transform parent, bool twin, Skin skin, TwinMode mode)
        {
            var who = skin.Person;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new InvalidOperationException("模型が無い: " + who.Model);
            var her = (GameObject)Object.Instantiate(src, parent);
            her.name = twin ? "Twin" : "Protagonist";
            her.transform.localPosition = Vector3.zero;
            her.transform.localRotation = Quaternion.identity;
            her.transform.localScale = twin && mode == TwinMode.MirrorWhole ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            if (who.IsComposite)
            {
                // 体の人の骨に、組み合わせたメッシュ（面の組は体・頭・髪）を載せる
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(who.CompositeMesh);
                if (mesh == null)
                {
                    Debug.Log(RocketboxCompose.BuildMesh(who));
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(who.CompositeMesh);
                }
                if (mesh == null) throw new InvalidOperationException("組み合わせたメッシュを作れない: " + who.CompositeMesh);
                var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.name = who.Name;
            }
            Dress(her, skin, twin && mode == TwinMode.MoleOnly);
            Shape(her, skin.JawScale, skin.JawClose);
            if (skin.Look != null) ShapeFace(her, skin.Look);
            if (skin.Look != null && (skin.Look.noseFlatten > 0f || skin.Look.noseNarrow > 0f)) ShapeNose(her, skin);
            AddAnimator(her);
            return her;
        }

        /// <summary>
        /// 顎の骨を左右に縮めて、顎から頬の下を細くする（手入れの一つ）。close 度だけ顎を閉じる向きへ回して、唇を合わせる
        /// （顎の骨の前向きの軸が模型の左右に沿う。片割れは模型の根ごと裏返すので、同じ回し方で閉じる）。
        /// 顎は Humanoid の骨に当てていないので、動きに上書きされない
        /// </summary>
        static void Shape(GameObject her, float jaw, float close)
        {
            foreach (var t in her.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Bip01 MJaw") continue;
                if (!Mathf.Approximately(close, 0f)) t.localRotation = t.localRotation * Quaternion.AngleAxis(close, Vector3.forward);
                if (Mathf.Approximately(jaw, 1f)) continue;
                // 骨の軸のうち、模型の左右（x）に沿う軸を縮める
                var ax = t.InverseTransformDirection(her.transform.right);
                var a = new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z));
                var s = t.localScale;
                if (a.x >= a.y && a.x >= a.z) s.x *= jaw;
                else if (a.y >= a.z) s.y *= jaw;
                else s.z *= jaw;
                t.localScale = s;
            }
        }

        /// <summary>
        /// 顔の形を寄せる（<see cref="RocketboxPaint.Look"/> の eyeScale・lidOpen・noseScale・chinShort）。
        /// 目の玉の骨を拡げ、瞼の骨を目の玉の中心から外へ、鼻の骨を縮め、顎の骨を縦に縮める。どれも Humanoid に当てていない骨なので動きに上書きされない
        /// </summary>
        static void ShapeFace(GameObject her, RocketboxPaint.Look k)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in her.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            Func<string, Transform> B = name => { Transform t; return bones.TryGetValue(name, out t) ? t : null; };
            foreach (var side in new[] { "L", "R" })
            {
                var eye = B("Bip01 " + side + "Eye");
                if (eye == null) continue;
                if (!Mathf.Approximately(k.lidOpen, 0f))
                    foreach (var lid in new[] { "EyeBlinkTop", "EyeBlinkBottom" })
                    {
                        var t = B("Bip01 " + side + lid);
                        if (t != null) t.position = eye.position + (t.position - eye.position) * (1f + k.lidOpen);
                    }
                if (!Mathf.Approximately(k.eyeScale, 1f)) eye.localScale *= k.eyeScale;
            }
            var nose = B("Bip01 MNose");
            if (nose != null && !Mathf.Approximately(k.noseScale, 1f)) nose.localScale *= k.noseScale;
            var jaw = B("Bip01 MJaw");
            if (jaw != null && k.chinShort > 0f)
            {
                // 顎の骨の軸のうち、模型の上下に沿う軸を縮める
                var ax = jaw.InverseTransformDirection(her.transform.up);
                var a = new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z));
                var s = jaw.localScale;
                if (a.x >= a.y && a.x >= a.z) s.x *= 1f - k.chinShort;
                else if (a.y >= a.z) s.y *= 1f - k.chinShort;
                else s.z *= 1f - k.chinShort;
                jaw.localScale = s;
            }
        }

        /// <summary>
        /// 鼻を低く・細くする（<see cref="RocketboxPaint.Look"/> の noseFlatten・noseNarrow）。頭の面の頂点を動かすので、メッシュを写してから直す
        /// （写しは skin.Made に入れる。どちらも 0 なら写さない）。束ねた姿勢のまま、模型の根の向きで測る。
        /// - 高さ: 鼻筋から鼻の下まで、頬の内側（真ん中から 2.4〜3 cm）の面から前へ出た分を noseFlatten の割合だけ後ろへ。
        ///   鼻の下の面（鼻の穴のまわり）も鼻先と同じだけ下げ、上唇との間（鼻の骨の 1.8〜2.4 cm 下）で弱める
        ///   （鼻の骨の 1.2 cm 下で弱めていたので、鼻の下の面が前を向いて、鼻の穴が暗い点に見えた）
        /// - 小鼻: 鼻の骨の高さから 2 cm 下まで、真ん中へ noseNarrow の割合だけ寄せる
        /// 動かした頂点の法線は、となりの三角から付け直す
        /// </summary>
        static void ShapeNose(GameObject her, Skin skin)
        {
            var k = skin.Look;
            var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
            var ms = smr.sharedMaterials;
            var headSlot = -1;
            for (var i = 0; i < ms.Length; i++) if (ms[i] != null && (ms[i] == skin.Head || ms[i] == skin.HeadTwin)) headSlot = i;
            if (headSlot < 0) return;
            var a = Maps.Get(skin.Person, 512).Anchors;
            // 描いた組（アセット）で組み立てるとき（場面に置く主人公）は、直したメッシュもアセットにする（場面を保存しても消えないように）
            var persist = skin.Head != null && AssetDatabase.Contains(skin.Head);
            var mesh = Object.Instantiate(smr.sharedMesh);
            if (!persist) Keep(skin, mesh);
            mesh.name = smr.sharedMesh.name + "_nose";
            var v = mesh.vertices;
            var nrm = mesh.normals;
            var tris = mesh.GetTriangles(headSlot);
            var root = her.transform;
            Func<Vector3, Vector3> toRoot = q => root.InverseTransformPoint(smr.transform.TransformPoint(q));
            Func<Vector3, Vector3> fromRoot = q => smr.transform.InverseTransformPoint(root.TransformPoint(q));
            var headVerts = new HashSet<int>(tris);
            var rp = new Dictionary<int, Vector3>();
            foreach (var i in headVerts) rp[i] = toRoot(v[i]);
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            // 頬の内側の面の奥行き（2 mm ごと）
            float y0 = a.nose.y - 0.030f, step = 0.002f;
            var bins = Mathf.CeilToInt((eyeY + 0.012f - y0) / step) + 1;
            var zs = new float[bins];
            var zn = new int[bins];
            foreach (var kv in rp)
            {
                var p = kv.Value;
                var ax = Mathf.Abs(p.x);
                if (ax < 0.024f || ax > 0.030f || p.z < a.nose.z - 0.050f) continue;
                var b = Mathf.RoundToInt((p.y - y0) / step);
                for (var d = -1; d <= 1; d++)
                    if (b + d >= 0 && b + d < bins) { zs[b + d] += p.z; zn[b + d]++; }
            }
            // 鼻先の高さ（鼻の骨の 1.6 cm 下から 4 mm 上まで）での、左右 1 mm ごとの一番の出っ張り。
            // 鼻先より下の頂点（鼻の穴のまわりの下の面）は、真上の鼻先と同じだけ下げる（鼻先だけ下げると、下の面が前を向いて鼻の穴が暗い点に見えた）
            var colMax = new Dictionary<int, float>();
            foreach (var kv in rp)
            {
                var p = kv.Value;
                if (Mathf.Abs(p.x) > 0.030f || p.z < a.nose.z - 0.045f || p.y < a.nose.y - 0.016f || p.y > a.nose.y + 0.004f) continue;
                var b = Mathf.Clamp(Mathf.RoundToInt((p.y - y0) / step), 0, bins - 1);
                if (zn[b] == 0) continue;
                var col = Mathf.RoundToInt(p.x * 1000f);
                var h = p.z - zs[b] / zn[b];
                float m;
                if (!colMax.TryGetValue(col, out m) || h > m) colMax[col] = h;
            }
            var moved = new HashSet<int>();
            foreach (var kv in rp)
            {
                var p = kv.Value;
                var ax = Mathf.Abs(p.x);
                if (ax > 0.030f || p.z < a.nose.z - 0.045f) continue;
                var q = p;
                var w = RocketboxPaint.Smooth(0.030f, 0.012f, ax) * RocketboxPaint.Smooth(eyeY + 0.006f, eyeY - 0.006f, p.y) * RocketboxPaint.Smooth(a.nose.y - 0.024f, a.nose.y - 0.018f, p.y);
                var b = Mathf.Clamp(Mathf.RoundToInt((p.y - y0) / step), 0, bins - 1);
                if (w > 0f && k.noseFlatten > 0f && zn[b] > 0)
                {
                    var h = Mathf.Max(0f, p.z - zs[b] / zn[b]);
                    float tip;
                    if (p.y < a.nose.y - 0.004f && colMax.TryGetValue(Mathf.RoundToInt(p.x * 1000f), out tip)) h = Mathf.Max(h, tip);
                    q.z -= h * Mathf.Clamp01(k.noseFlatten) * w;
                }
                var wa = RocketboxPaint.Smooth(a.nose.y + 0.010f, a.nose.y, p.y) * RocketboxPaint.Smooth(a.nose.y - 0.024f, a.nose.y - 0.018f, p.y) * RocketboxPaint.Smooth(0.030f, 0.018f, ax);
                if (wa > 0f && k.noseNarrow > 0f) q.x *= 1f - Mathf.Clamp01(k.noseNarrow) * wa;
                if ((q - p).sqrMagnitude < 1e-12f) continue;
                v[kv.Key] = fromRoot(q);
                moved.Add(kv.Key);
            }
            if (moved.Count == 0) return;
            var acc = new Dictionary<int, Vector3>();
            for (var t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                if (!moved.Contains(i0) && !moved.Contains(i1) && !moved.Contains(i2)) continue;
                var fn = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
                foreach (var i in new[] { i0, i1, i2 })
                {
                    if (!moved.Contains(i)) continue;
                    Vector3 s;
                    acc.TryGetValue(i, out s);
                    acc[i] = s + fn;
                }
            }
            foreach (var kv in acc)
            {
                var n = kv.Value.normalized;
                // 元の法線と逆を向いたら（三角の向きが逆の面）、裏返す
                if (Vector3.Dot(n, nrm[kv.Key]) < 0f) n = -n;
                nrm[kv.Key] = n;
            }
            mesh.vertices = v;
            mesh.normals = nrm;
            mesh.RecalculateBounds();
            if (persist)
            {
                var path = skin.Person.Dir + skin.Person.Name + "_nose_mesh.asset";
                RocketboxCompose.Save(mesh, path);
                Object.DestroyImmediate(mesh);
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            smr.sharedMesh = mesh;
        }

        static void Dress(GameObject her, Skin skin, bool twinHead)
        {
            var head = twinHead && skin.HeadTwin != null ? skin.HeadTwin : skin.Head;
            var body = skin.Body;
            foreach (var r in her.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] ms;
                if (skin.Person.IsComposite)
                {
                    // 組み合わせたメッシュはマテリアルの名前を持たない。面の組の順が体・頭・髪
                    ms = skin.Person.IsHairSwap
                        ? HairSwapSlots(skin, body, head)
                        : ComposeSlots(skin, body, head);
                }
                else
                {
                    ms = r.sharedMaterials;
                    for (var i = 0; i < ms.Length; i++)
                    {
                        var n = ms[i] == null ? "" : ms[i].name;
                        if (n == skin.Person.BodySlot) ms[i] = body;
                        else if (n == skin.Person.HeadSlot) ms[i] = head;
                        else if (n == skin.Person.HairSlot) ms[i] = skin.Hair;
                    }
                }
                r.sharedMaterials = ms;
                // 骨が動いて頭が元の箱から出ても消えないように
                r.updateWhenOffscreen = true;
            }
        }

        /// <summary>頭を載せ替えた人の、組み合わせたメッシュの面の組: 体・頭・髪、体の人の頭の面で作った胸元、借りた腰から下</summary>
        static Material[] ComposeSlots(Skin skin, Material body, Material head)
        {
            var ms = new List<Material> { body, head, skin.Hair };
            if (skin.Chest != null) ms.Add(skin.Chest);
            if (skin.Legs != null) ms.Add(skin.Legs);
            if (skin.Dress != null) ms.Add(skin.Dress);
            return ms.ToArray();
        }

        /// <summary>顔と髪が別の人の、組み合わせたメッシュの面の組: 体・頭・髪の殻・髪の房・まつ毛、借りた膝から下、体の人の頭の面で作った胸元</summary>
        static Material[] HairSwapSlots(Skin skin, Material body, Material head)
        {
            var ms = new List<Material> { body, head, skin.Shell, skin.Hair, skin.Lash };
            if (skin.Legs != null) ms.Add(skin.Legs);
            if (skin.Chest != null) ms.Add(skin.Chest);
            return ms.ToArray();
        }

        /// <summary>
        /// 立ちと歩きの動き（Quaternius の動きを Humanoid で移し替えたもの）。
        /// 進むのは CharacterController の仕事なので、動作そのものに体を運ばせない
        /// </summary>
        static void AddAnimator(GameObject her)
        {
            var an = her.GetComponent<Animator>();
            if (an == null) an = her.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            if (ctrl == null) Debug.LogWarning("Humanoid の状態機械が無い（HalfAware/Rocketbox/Retarget the idle and walk で作る）: " + Controller);
            an.runtimeAnimatorController = ctrl;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.updateMode = AnimatorUpdateMode.Normal;
        }

        // ---- マテリアルの組 ---------------------------------------------------

        public sealed class Skin
        {
            public readonly RocketboxPerson Person;
            public Material Body, Head, HeadTwin, Hair;
            /// <summary>顔と髪が別の人のとき: 髪の殻（髪の人の頭のテクスチャ）と、まつ毛（顔の人の透けの絵）</summary>
            public Material Shell, Lash;
            /// <summary>膝から下を別の人から借りるとき: その人の体のテクスチャ（肌を頭に揃えた物）</summary>
            public Material Legs;
            /// <summary>胸元を体の人の頭の面で作るとき: 体の人の頭のテクスチャ（肌を顔の人の肌に揃えた物）</summary>
            public Material Chest;
            /// <summary>一から作るワンピース（<see cref="RocketboxDress"/>）</summary>
            public Material Dress;
            /// <summary>顎の骨の左右の倍率（<see cref="RocketboxPaint.Look.JawScale"/>）</summary>
            public float JawScale = 1f;
            /// <summary>形の手入れ（目・瞼・鼻・顎の骨）に使う見た目（<see cref="ShapeFace"/>）</summary>
            public RocketboxPaint.Look Look;
            /// <summary>顎を閉じる向きへ回す角度（<see cref="RocketboxPaint.Look.jawClose"/>）</summary>
            public float JawClose;
            /// <summary>撮り比べで測るための、頭の印の絵と黒子の位置</summary>
            public Texture2D MaskHead, MaskHeadTwin;
            public RocketboxPaint.HeadResult HeadInfo, HeadTwinInfo;
            /// <summary>体の手の肌を頭の肌に揃えたときの測り（揃えていなければ null）</summary>
            public string SkinNote;
            /// <summary>その場で作った物（アセットでない）。使い終えたら <see cref="Destroy"/></summary>
            public readonly List<Object> Made = new List<Object>();

            public Skin(RocketboxPerson person) { Person = person; }

            public void Destroy()
            {
                foreach (var o in Made) if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
                Made.Clear();
            }
        }

        [MenuItem("HalfAware/Rocketbox/Paint the protagonist")]
        public static void PaintMenu()
        {
            Debug.Log(Paint(Chosen, Chosen.Look(), Twin == TwinMode.MoleOnly));
        }

        [MenuItem("HalfAware/Rocketbox/Paint every Rocketbox person")]
        public static void PaintAllMenu()
        {
            foreach (var who in RocketboxPerson.All) Debug.Log(Paint(who, who.Look(), Twin == TwinMode.MoleOnly));
        }

        /// <summary>
        /// 縮めたテクスチャに手を入れ、その人の Painted/ に PNG とマテリアルで書く。
        /// withTwinHead なら片割れ用の頭（黒子が右目の下）も書く。服を元のままにする人は、体のマテリアルが縮めた写しをそのまま使う
        /// </summary>
        public static string Paint(RocketboxPerson who, RocketboxPaint.Look look, bool withTwinHead)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(who.ToString());
            EnsureFolder(who);
            // 描くのはいつも 512。頭を 256 にするときは取り込みの設定で縮める
            var maps = Maps.Get(who, 512);
            var dir = who.Painted;
            int n;
            var head = RocketboxTextures.ReadPng(who.HeadSrc, out n, out n);
            var self = RocketboxPaint.Head(ToColors(head), maps.Head, maps.Anchors, look, false, who.IrisUv, who.IrisRadius);
            WritePainted(self.Px, n, dir + "Head_self.png", false, HeadSize);
            WritePainted(ToColors(self.Spec), n, dir + "Head_self_spec.png", true, SpecSize);
            sb.AppendLine("頭（主人公）: " + self.Note + "、UV " + self.MoleUv.ToString("F4"));
            if (withTwinHead)
            {
                var twin = RocketboxPaint.Head(ToColors(head), maps.Head, maps.Anchors, look, true, who.IrisUv, who.IrisRadius);
                WritePainted(twin.Px, n, dir + "Head_twin.png", false, HeadSize);
                sb.AppendLine("頭（片割れ）: " + twin.Note);
            }
            string skinNote;
            var bodyPx = PaintBody(who, look, maps, self, out skinNote);
            if (skinNote != null) sb.AppendLine("肌の色: " + skinNote);
            Texture2D bodyTex;
            if (bodyPx != null)
            {
                WritePainted(bodyPx, 512, dir + "Body.png", false, 512);
                bodyTex = Load(dir + "Body.png");
            }
            else bodyTex = Load(who.BodySrc);
            var hair = RocketboxTextures.ReadPng(who.HairSrc, out n, out n);
            WritePainted(RocketboxPaint.Hair(ToColors(hair), look), n, dir + "Hair.png", true, 512);
            if (who.IsHairSwap)
            {
                var shell = PaintShell(who, look.naturalHair);
                WritePainted(shell.Px, 512, dir + "Shell.png", true, HeadSize);
                WritePainted(ToColors(shell.Spec), 512, dir + "Shell_spec.png", true, SpecSize);
                var lash = RocketboxTextures.ReadPng(who.LashSrc, out n, out n);
                WritePainted(RocketboxPaint.Hair(ToColors(lash), look), n, dir + "Lash.png", true, 512);
                SaveMaterial(LitHead("Shell", Load(dir + "Shell.png"), Load(dir + "Shell_spec.png"), true), dir + "Shell.mat");
                SaveMaterial(Lit("Lash", Load(dir + "Lash.png"), 0.34f, true), dir + "Lash.mat");
            }
            if (who.LegsFrom != null)
            {
                string legsNote;
                WritePainted(PaintLegs(who, look, self, out legsNote), 512, dir + "Legs.png", false, 512);
                SaveMaterial(Lit("Legs", Load(dir + "Legs.png"), 0.12f, false), dir + "Legs.mat");
                sb.AppendLine("膝から下の肌: " + legsNote);
            }
            if (who.MadeDress)
            {
                string dressNote;
                WritePainted(RocketboxDress.Paint(who, 512, out dressNote), 512, dir + "Dress.png", false, 512);
                SaveMaterial(RocketboxDress.Textured(Load(dir + "Dress.png")), dir + "Dress.mat");
                sb.AppendLine(dressNote);
            }
            if (who.ChestFromBody)
            {
                string chestNote;
                WritePainted(PaintChest(who, look, self, out chestNote), 512, dir + "Chest.png", false, 512);
                SaveMaterial(Lit("Chest", Load(dir + "Chest.png"), look.skinSmoothness, false), dir + "Chest.mat");
                sb.AppendLine("胸元の肌: " + chestNote);
            }

            SaveMaterial(Lit("Body", bodyTex, 0.12f, false), dir + "Body.mat");
            var spec = Load(dir + "Head_self_spec.png");
            SaveMaterial(LitHead("Head_self", Load(dir + "Head_self.png"), spec), dir + "Head_self.mat");
            if (withTwinHead) SaveMaterial(LitHead("Head_twin", Load(dir + "Head_twin.png"), spec), dir + "Head_twin.mat");
            SaveMaterial(Lit("Hair", Load(dir + "Hair.png"), 0.34f, true), dir + "Hair.mat");
            AssetDatabase.SaveAssets();
            sb.AppendLine("書いた所: " + dir + (bodyPx != null ? "" : "（体は " + who.BodySrc + " をそのまま）"));
            return sb.ToString();
        }

        /// <summary>
        /// 体のテクスチャに手を入れる（ニットを黒に、頭と体が別の人なら手の肌を頭の肌に揃える）。
        /// どちらもしないなら null（縮めた写しをそのまま使う）
        /// </summary>
        /// <summary>
        /// 髪の殻の絵: 髪の人の頭のテクスチャを、髪の人の値で黒く塗る（使うのは髪の所だけなので、黒子と手入れは描かない）
        /// </summary>
        public static RocketboxPaint.HeadResult PaintShell(RocketboxPerson who, bool natural = false)
        {
            var hair = who.HairFrom;
            var maps = Maps.Get(hair, 512);
            var k = hair.Look();
            k.moleDiameter = 0f;
            k.beauty = 0f;
            // 元の色の髪（片割れ）: 殻の髪は塗らず、耳まわり・こめかみの肌と生え際の明るい所は元の髪の暗い色で塗る
            k.naturalHair = natural;
            if (natural) k.hairShadow = k.naturalHairInk;
            int n;
            var tex = ToColors(RocketboxTextures.ReadPng(hair.HeadSrc, out n, out n));
            var r = RocketboxPaint.Head(tex, maps.Head, maps.Anchors, k, false, hair.IrisUv, hair.IrisRadius);
            // 透け: 髪の所と、耳のまわり・こめかみ（殻に入れた肌）だけ不透明。殻の縁は三角の辺ではなく、この絵の縁で切る。
            // 耳まわりとこめかみの肌は髪の影の色で塗り、照り返しを 0 にする。髪の所の毛筋の濃淡は残す
            n = r.N;
            var alpha = new float[r.Px.Length];
            var shadow = k.hairShadow;
            for (var i = 0; i < r.Px.Length; i++)
            {
                if (!maps.Head.On[i]) { alpha[i] = -1f; continue; }
                var p = maps.Head.P[i];
                var side = RocketboxPaint.Smooth(-RocketboxHairSwap.SideFeather, 0f, RocketboxHairSwap.SideDepth(p, maps.Anchors, hair));
                alpha[i] = Mathf.Max(r.Hair[i] * (1f - RocketboxHairSwap.FaceFeature(p, maps.Anchors)), side);
                var c = shadow;
                c.a = r.Px[i].a;
                r.Px[i] = Color.Lerp(r.Px[i], c, side * (1f - r.Hair[i]));
                if (side > 0f) r.Spec[i] = Color32.Lerp(r.Spec[i], new Color32(0, 0, 0, r.Spec[i].a), side);
            }
            // UV の島の外は、となりの島の中の透けを広げる（縁で絵を拾ったとき、島の外の値が混ざらないように）。届かない所は不透明
            for (var pass = 0; pass < 4; pass++)
            {
                var next = (float[])alpha.Clone();
                for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var i = y * n + x;
                        if (alpha[i] >= 0f) continue;
                        float sum = 0f;
                        var cnt = 0;
                        if (x > 0 && alpha[i - 1] >= 0f) { sum += alpha[i - 1]; cnt++; }
                        if (x < n - 1 && alpha[i + 1] >= 0f) { sum += alpha[i + 1]; cnt++; }
                        if (y > 0 && alpha[i - n] >= 0f) { sum += alpha[i - n]; cnt++; }
                        if (y < n - 1 && alpha[i + n] >= 0f) { sum += alpha[i + n]; cnt++; }
                        if (cnt > 0) next[i] = sum / cnt;
                    }
                alpha = next;
            }
            for (var i = 0; i < alpha.Length; i++) if (alpha[i] < 0f) alpha[i] = 1f;
            // 髪の中の明るい毛筋で透けに開いた小さな穴（幅 4 画素まで）を閉じる（顔の人のこめかみの肌が点になって覗いたため）
            alpha = RocketboxPaint.MinMax(RocketboxPaint.MinMax(alpha, n, 2, true), n, 2, false);
            for (var i = 0; i < r.Px.Length; i++)
            {
                var c = r.Px[i];
                // 不透明に残る所で明るい画素（髪と肌の混ざった分け目や生え際の茶色、島の外の詰め物）は影の色へ寄せる。暗い毛筋の濃淡は残す
                // （分け目の近くで橙の点に見えた）
                if (alpha[i] >= 0.4f)
                {
                    var ink = shadow;
                    ink.a = c.a;
                    c = Color.Lerp(c, ink, RocketboxPaint.Smooth(0.12f, 0.30f, RocketboxPaint.Lum(c)));
                }
                c.a = alpha[i];
                r.Px[i] = c;
            }
            return r;
        }

        /// <summary>膝から下を借りる人の体のテクスチャで、肌を頭の肌に揃える（サンダルの色は元のまま）</summary>
        static Color[] PaintLegs(RocketboxPerson who, RocketboxPaint.Look look, RocketboxPaint.HeadResult head, out string note)
        {
            int n;
            var px = ToColors(RocketboxTextures.ReadPng(who.LegsSrc, out n, out n));
            var k = look.Clone();
            k.matchSkinAll = true;
            var headMaps = Maps.Get(who, 512);
            var legMaps = Maps.Get(who.LegsFrom, 512);
            px = RocketboxPaint.MatchSkin(px, legMaps.Body, head.Px, head.Hair, headMaps.Head, headMaps.Anchors, k, out note);
            // 服を一色の布に（長衣。足とサンダルの高さより上）
            if (look.dress) RocketboxPaint.Dress(px, legMaps.Body, look, -1f, who.LegsCut + 0.05f, true);
            return px;
        }

        /// <summary>胸元（体の人の頭のテクスチャ）の肌を、顔の人の首元の肌に揃える</summary>
        static Color[] PaintChest(RocketboxPerson who, RocketboxPaint.Look look, RocketboxPaint.HeadResult head, out string note)
        {
            int n;
            var px = ToColors(RocketboxTextures.ReadPng(who.ChestSrc, out n, out n));
            var k = look.Clone();
            k.matchSkinAll = true;
            var chestMaps = Maps.Get(who.BodyFrom, 512);
            px = MatchBodyPersonSkin(px, chestMaps.Head, who, head, k, out note);
            // ネックレスは頭の面と同じ位置に描く（二人の骨と束ねた姿勢は同じなので、頭の人の顔の骨の位置で決めてよい）
            RocketboxPaint.Necklace(px, chestMaps.Head, Maps.Get(who, 512).Anchors, look);
            return px;
        }

        /// <summary>
        /// 体の人の絵（体または頭のテクスチャ）の肌を、体の人の首元の肌から顔の人の首元の肌への一つの比で揃える
        /// （<see cref="RocketboxPaint.MatchSkinBy"/>。胸元を体の人の頭の面で作るとき、体と胸元の二枚に同じ比を掛ける）
        /// </summary>
        static Color[] MatchBodyPersonSkin(Color[] px, RocketboxPaint.Surface map, RocketboxPerson who, RocketboxPaint.HeadResult head, RocketboxPaint.Look look, out string note)
        {
            int n;
            var headMaps = Maps.Get(who, 512);
            var own = Maps.Get(who.BodyFrom, 512);
            var ownHead = ToColors(RocketboxTextures.ReadPng(who.BodyFrom.HeadSrc, out n, out n));
            return RocketboxPaint.MatchSkinBy(px, map, head.Px, head.Hair, headMaps.Head, headMaps.Anchors, ownHead, own.Head, own.Anchors, look, out note);
        }

        static Color[] PaintBody(RocketboxPerson who, RocketboxPaint.Look look, Maps maps, RocketboxPaint.HeadResult head, out string skinNote)
        {
            skinNote = null;
            if (!look.blackenKnit && !look.matchSkin && !look.recolourPants && !look.recolourShoes && !look.recolourTop && !look.dress) return null;
            int n;
            var px = ToColors(RocketboxTextures.ReadPng(who.BodySrc, out n, out n));
            if (look.blackenKnit || look.recolourPants || look.recolourShoes || look.recolourTop)
            {
                float[] knit;
                px = RocketboxPaint.Body(px, maps.Body, look, out knit);
            }
            if (look.dress) RocketboxPaint.Dress(px, maps.Body, look, who.LegsFrom != null ? who.LegsCut - 0.05f : 0f, 9f, false);
            if (look.matchSkin)
                px = who.ChestFromBody
                    ? MatchBodyPersonSkin(px, maps.Body, who, head, look, out skinNote)
                    : RocketboxPaint.MatchSkin(px, maps.Body, head.Px, head.Hair, maps.Head, maps.Anchors, look, out skinNote);
            return px;
        }

        static Skin LoadPainted(RocketboxPerson who, bool twinHead)
        {
            var dir = who.Painted;
            var s = new Skin(who)
            {
                Body = AssetDatabase.LoadAssetAtPath<Material>(dir + "Body.mat"),
                Head = AssetDatabase.LoadAssetAtPath<Material>(dir + "Head_self.mat"),
                HeadTwin = AssetDatabase.LoadAssetAtPath<Material>(dir + "Head_twin.mat"),
                Hair = AssetDatabase.LoadAssetAtPath<Material>(dir + "Hair.mat"),
                Shell = AssetDatabase.LoadAssetAtPath<Material>(dir + "Shell.mat"),
                Lash = AssetDatabase.LoadAssetAtPath<Material>(dir + "Lash.mat"),
                Legs = AssetDatabase.LoadAssetAtPath<Material>(dir + "Legs.mat"),
                Chest = AssetDatabase.LoadAssetAtPath<Material>(dir + "Chest.mat"),
                Dress = AssetDatabase.LoadAssetAtPath<Material>(dir + "Dress.mat"),
            };
            if (s.Body == null || s.Head == null || s.Hair == null) return null;
            if (who.IsHairSwap && (s.Shell == null || s.Lash == null)) return null;
            if (who.LegsFrom != null && s.Legs == null) return null;
            if (who.ChestFromBody && s.Chest == null) return null;
            if (who.MadeDress && s.Dress == null) return null;
            if (!who.MadeDress) s.Dress = null;
            if (!who.ChestFromBody) s.Chest = null;
            if (twinHead && s.HeadTwin == null) return null;
            return s;
        }

        /// <summary>
        /// 描いたばかりの組を、アセットにせずに作る（撮り比べ用）。テクスチャは取り込みと同じく DXT に圧縮する。
        /// headSize が 256 なら頭だけ 512 で描いてから縮める
        /// </summary>
        public static Skin MakeSkin(RocketboxPerson who, RocketboxPaint.Look look, int headSize, bool withTwinHead)
        {
            var skin = new Skin(who) { JawScale = look.JawScale, JawClose = look.jawClose, Look = look };
            try
            {
                var maps = Maps.Get(who, 512);
                RocketboxPaint.HeadResult info;
                Texture2D mask;
                skin.Head = MakeHead(skin, look, headSize, false, out info, out mask);
                skin.HeadInfo = info;
                skin.MaskHead = mask;
                if (withTwinHead)
                {
                    skin.HeadTwin = MakeHead(skin, look, headSize, true, out info, out mask);
                    skin.HeadTwinInfo = info;
                    skin.MaskHeadTwin = mask;
                }
                int n;
                string skinNote;
                var body = PaintBody(who, look, maps, skin.HeadInfo, out skinNote);
                skin.SkinNote = skinNote;
                if (body != null) skin.Body = Keep(skin, Lit("Body", Keep(skin, Tex(body, 512, false, 512)), 0.12f, false));
                else skin.Body = Keep(skin, Lit("Body", Load(who.BodySrc), 0.12f, false));
                var hair = RocketboxPaint.Hair(ToColors(RocketboxTextures.ReadPng(who.HairSrc, out n, out n)), look);
                skin.Hair = Keep(skin, Lit("Hair", Keep(skin, Tex(hair, n, true, 512)), 0.34f, true));
                if (who.IsHairSwap)
                {
                    var shell = PaintShell(who, look.naturalHair);
                    var shellSpec = Keep(skin, Tex(ToColors(shell.Spec), 512, true, SpecSize));
                    skin.Shell = Keep(skin, LitHead("Shell", Keep(skin, Tex(shell.Px, 512, true, headSize)), shellSpec, true));
                    var lash = RocketboxPaint.Hair(ToColors(RocketboxTextures.ReadPng(who.LashSrc, out n, out n)), look);
                    skin.Lash = Keep(skin, Lit("Lash", Keep(skin, Tex(lash, n, true, 512)), 0.34f, true));
                }
                if (who.LegsFrom != null)
                {
                    string legsNote;
                    skin.Legs = Keep(skin, Lit("Legs", Keep(skin, Tex(PaintLegs(who, look, skin.HeadInfo, out legsNote), 512, false, 512)), 0.12f, false));
                }
                if (who.MadeDress)
                {
                    string dressNote;
                    skin.Dress = Keep(skin, RocketboxDress.Textured(Keep(skin, Tex(RocketboxDress.Paint(who, 512, out dressNote), 512, false, 512))));
                }
                if (who.ChestFromBody)
                {
                    string chestNote;
                    skin.Chest = Keep(skin, Lit("Chest", Keep(skin, Tex(PaintChest(who, look, skin.HeadInfo, out chestNote), 512, false, 512)), look.skinSmoothness, false));
                }
                return skin;
            }
            catch
            {
                skin.Destroy();
                throw;
            }
        }

        /// <summary>頭のマテリアルだけを描いて作る。作った物は into に入れる（人も into の人）。印の絵（撮り比べで測る）も返す</summary>
        public static Material MakeHead(Skin into, RocketboxPaint.Look look, int headSize, bool twin, out RocketboxPaint.HeadResult info, out Texture2D mask)
        {
            var who = into.Person;
            var maps = Maps.Get(who, 512);
            int n;
            var head = ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out n, out n));
            info = RocketboxPaint.Head(head, maps.Head, maps.Anchors, look, twin, who.IrisUv, who.IrisRadius);
            mask = Keep(into, MaskTex(info.Mask, n, headSize));
            var spec = Keep(into, Tex(ToColors(info.Spec), n, true, SpecSize));
            return Keep(into, LitHead(twin ? "Head_twin" : "Head_self", Keep(into, Tex(info.Px, n, false, headSize)), spec));
        }

        /// <summary>手を入れていない縮めたテクスチャのままの組（撮り比べの「前」）</summary>
        public static Skin MakeRawSkin(RocketboxPerson who)
        {
            var skin = new Skin(who);
            skin.Head = Keep(skin, Lit("Head_raw", Load(who.HeadSrc), 0.22f, false));
            skin.Body = Keep(skin, Lit("Body_raw", Load(who.BodySrc), 0.12f, false));
            skin.Hair = Keep(skin, Lit("Hair_raw", Load(who.HairSrc), 0.34f, true));
            if (who.IsHairSwap)
            {
                skin.Shell = Keep(skin, Lit("Shell_raw", Load(who.ShellSrc), 0.22f, false));
                skin.Lash = Keep(skin, Lit("Lash_raw", Load(who.LashSrc), 0.34f, true));
            }
            return skin;
        }

        public static T Keep<T>(Skin s, T o) where T : Object
        {
            o.hideFlags = HideFlags.HideAndDontSave;
            s.Made.Add(o);
            return o;
        }

        // ---- 位置の地図（UV → 模型の上の位置） ---------------------------------

        public sealed class Maps
        {
            public RocketboxPaint.Surface Head, Body;
            public RocketboxPaint.Anchors Anchors;
            static readonly Dictionary<string, Maps> cache = new Dictionary<string, Maps>();

            /// <summary>模型の束ねた姿勢から作る。同じ人・同じ大きさなら使い回す</summary>
            public static Maps Get(RocketboxPerson who, int n)
            {
                Maps m;
                var key = who.Name + "/" + n;
                if (cache.TryGetValue(key, out m)) return m;
                if (who.IsComposite)
                {
                    // 頭と顔の骨は頭の人、体は体の人の地図（骨の束ねた姿勢は同じ）
                    var h = Get(who.FaceFrom, n);
                    m = new Maps { Head = h.Head, Anchors = h.Anchors, Body = Get(who.BodyFrom, n).Body };
                    cache[key] = m;
                    return m;
                }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
                var go = (GameObject)Object.Instantiate(src);
                go.hideFlags = HideFlags.HideAndDontSave;
                var baked = new Mesh();
                try
                {
                    go.transform.position = Vector3.zero;
                    go.transform.rotation = Quaternion.identity;
                    var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                    smr.BakeMesh(baked, true);
                    var v = baked.vertices;
                    for (var i = 0; i < v.Length; i++) v[i] = go.transform.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                    var uv = smr.sharedMesh.uv;
                    var names = smr.sharedMaterials;
                    int head = -1, body = -1;
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (names[i] != null && names[i].name == who.HeadSlot) head = i;
                        if (names[i] != null && names[i].name == who.BodySlot) body = i;
                    }
                    if (head < 0 || body < 0) throw new InvalidOperationException("頭か体の面の組が無い");
                    m = new Maps
                    {
                        Head = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(head), n),
                        Body = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(body), n),
                        Anchors = RocketboxPaint.Anchors.Of(go.transform),
                    };
                    cache[key] = m;
                    return m;
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(baked);
                }
            }

            public static void Forget() { cache.Clear(); }
        }

        // ---- テクスチャとマテリアル -------------------------------------------

        public static Color[] ToColors(Color32[] px)
        {
            var o = new Color[px.Length];
            for (var i = 0; i < px.Length; i++) o[i] = px[i];
            return o;
        }

        /// <summary>その場のテクスチャ。size が n より小さければ線形の光で縮めてから、取り込みと同じく DXT に圧縮する</summary>
        public static Texture2D Tex(Color[] px, int n, bool alpha, int size)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++) c32[i] = px[i];
            if (size < n) c32 = RocketboxTextures.Downsample(c32, n, n, size, alpha);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            t.SetPixels32(c32);
            t.Apply(true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            EditorUtility.CompressTexture(t, alpha ? TextureFormat.DXT5 : TextureFormat.DXT1, TextureCompressionQuality.Normal);
            return t;
        }

        /// <summary>印の絵。縮めるときは近い画素を拾い、圧縮しない</summary>
        public static Texture2D MaskTex(Color32[] px, int n, int size)
        {
            var f = n / size;
            var o = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    o[y * size + x] = px[(y * f + f / 2) * n + x * f + f / 2];
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            t.SetPixels32(o);
            t.Apply(true);
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        /// <summary>URP の Lit。環境の映り込みは切る（場面の空の色が髪や目に乗るため）</summary>
        public static Material Lit(string name, Texture tex, float smooth, bool clip)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_Color", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (clip)
            {
                // 髪の房とまつ毛は透けの絵で切り抜き、両面を描く
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                // URP が切り抜きのマテリアルに入れる値に揃える（描き直すたびに差分が出ないように）
                m.SetFloat("_AlphaToMask", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                // 黒い髪の房は拡散の光がほとんど無いので、照り返しだけが灰色に浮く。髪は照り返しを切る
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            return m;
        }

        /// <summary>
        /// 頭のマテリアル。Specular の流儀で、照り返しの強さと滑らかさを絵（spec）で持つ。
        /// 髪の所は照り返しを 0 にする（長い髪の面が横の強い光で灰色のフードのように光るため）。肌は 0.04、目の玉は滑らか
        /// </summary>
        public static Material LitHead(string name, Texture tex, Texture spec, bool clip = false)
        {
            var m = Lit(name, tex, 1f, false);
            m.SetFloat("_WorkflowMode", 0f);
            m.EnableKeyword("_SPECULAR_SETUP");
            m.SetTexture("_SpecGlossMap", spec);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetColor("_SpecColor", Color.white);
            if (clip)
            {
                // 別の人の髪の殻は、絵の透けで髪の縁を切り抜く（表だけを描く）
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_AlphaToMask", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            return m;
        }

        static void WritePainted(Color[] px, int n, string path, bool alpha, int maxSize)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                c32[i] = px[i];
                if (!alpha) c32[i].a = 255;
            }
            RocketboxTextures.WritePng(c32, n, n, path, alpha);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null && ti.maxTextureSize != maxSize)
            {
                ti.maxTextureSize = maxSize;
                ti.SaveAndReimport();
            }
        }

        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new InvalidOperationException("テクスチャが無い: " + path);
            return t;
        }

        static void SaveMaterial(Material m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null)
            {
                old.shader = m.shader;
                old.CopyPropertiesFromMaterial(m);
                old.shaderKeywords = m.shaderKeywords;
                // 描き直すたびに差分が出ないよう、列はシェーダーの既定（-1）のままにし、URP が新しいマテリアルで切る MotionVectors のパスも切る
                old.renderQueue = m.rawRenderQueue;
                old.SetShaderPassEnabled("MotionVectors", false);
                old.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
                EditorUtility.SetDirty(old);
                Object.DestroyImmediate(m);
                return;
            }
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureFolder(RocketboxPerson who)
        {
            var d = who.Dir.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder(RocketboxImport.Root.TrimEnd('/'), who.Name);
            var p = who.Painted.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(d, "Painted");
        }
    }
}
