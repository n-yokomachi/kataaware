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
        /// <summary>主人公にする人。オーナーが女大 14 と女大 08 を見比べて決める</summary>
        public static readonly RocketboxPerson Chosen = RocketboxPerson.Adult14;

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
            return Build(parent, twin, Chosen);
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
            Shape(her, skin.JawScale);
            AddAnimator(her);
            return her;
        }

        /// <summary>
        /// 顎の骨を左右に縮めて、顎から頬の下を細くする（手入れの一つ）。
        /// 顎は Humanoid の骨に当てていないので、動きに上書きされない
        /// </summary>
        static void Shape(GameObject her, float jaw)
        {
            if (Mathf.Approximately(jaw, 1f)) return;
            foreach (var t in her.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Bip01 MJaw") continue;
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

        static void Dress(GameObject her, Skin skin, bool twinHead)
        {
            var head = twinHead && skin.HeadTwin != null ? skin.HeadTwin : skin.Head;
            foreach (var r in her.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] ms;
                if (skin.Person.IsComposite)
                {
                    // 組み合わせたメッシュはマテリアルの名前を持たない。面の組の順が体・頭・髪
                    ms = skin.Person.IsHairSwap
                        ? new[] { skin.Body, head, skin.Shell, skin.Hair, skin.Lash }
                        : new[] { skin.Body, head, skin.Hair };
                }
                else
                {
                    ms = r.sharedMaterials;
                    for (var i = 0; i < ms.Length; i++)
                    {
                        var n = ms[i] == null ? "" : ms[i].name;
                        if (n == skin.Person.BodySlot) ms[i] = skin.Body;
                        else if (n == skin.Person.HeadSlot) ms[i] = head;
                        else if (n == skin.Person.HairSlot) ms[i] = skin.Hair;
                    }
                }
                r.sharedMaterials = ms;
                // 骨が動いて頭が元の箱から出ても消えないように
                r.updateWhenOffscreen = true;
            }
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
            /// <summary>顎の骨の左右の倍率（<see cref="RocketboxPaint.Look.JawScale"/>）</summary>
            public float JawScale = 1f;
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
                var shell = PaintShell(who);
                WritePainted(shell.Px, 512, dir + "Shell.png", false, HeadSize);
                WritePainted(ToColors(shell.Spec), 512, dir + "Shell_spec.png", true, SpecSize);
                var lash = RocketboxTextures.ReadPng(who.LashSrc, out n, out n);
                WritePainted(RocketboxPaint.Hair(ToColors(lash), look), n, dir + "Lash.png", true, 512);
                SaveMaterial(LitHead("Shell", Load(dir + "Shell.png"), Load(dir + "Shell_spec.png")), dir + "Shell.mat");
                SaveMaterial(Lit("Lash", Load(dir + "Lash.png"), 0.34f, true), dir + "Lash.mat");
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
        public static RocketboxPaint.HeadResult PaintShell(RocketboxPerson who)
        {
            var hair = who.HairFrom;
            var maps = Maps.Get(hair, 512);
            var k = hair.Look();
            k.moleDiameter = 0f;
            k.beauty = 0f;
            int n;
            var tex = ToColors(RocketboxTextures.ReadPng(hair.HeadSrc, out n, out n));
            var r = RocketboxPaint.Head(tex, maps.Head, maps.Anchors, k, false, hair.IrisUv, hair.IrisRadius);
            // 耳のまわりから後ろの肌も殻に入れるので、そこは髪の影の色で塗り、照り返しを 0 にする
            for (var i = 0; i < r.Px.Length; i++)
            {
                if (!maps.Head.On[i] || !RocketboxHairSwap.IsSidePoint(maps.Head.P[i], maps.Anchors)) continue;
                var c = k.hairShadow;
                c.a = r.Px[i].a;
                r.Px[i] = Color.Lerp(r.Px[i], c, 1f - r.Hair[i]);
                r.Spec[i] = new Color32(0, 0, 0, r.Spec[i].a);
            }
            return r;
        }

        static Color[] PaintBody(RocketboxPerson who, RocketboxPaint.Look look, Maps maps, RocketboxPaint.HeadResult head, out string skinNote)
        {
            skinNote = null;
            if (!look.blackenKnit && !look.matchSkin) return null;
            int n;
            var px = ToColors(RocketboxTextures.ReadPng(who.BodySrc, out n, out n));
            if (look.blackenKnit)
            {
                float[] knit;
                px = RocketboxPaint.Body(px, maps.Body, look, out knit);
            }
            if (look.matchSkin)
                px = RocketboxPaint.MatchSkin(px, maps.Body, head.Px, head.Hair, maps.Head, maps.Anchors, look, out skinNote);
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
            };
            if (s.Body == null || s.Head == null || s.Hair == null) return null;
            if (who.IsHairSwap && (s.Shell == null || s.Lash == null)) return null;
            if (twinHead && s.HeadTwin == null) return null;
            return s;
        }

        /// <summary>
        /// 描いたばかりの組を、アセットにせずに作る（撮り比べ用）。テクスチャは取り込みと同じく DXT に圧縮する。
        /// headSize が 256 なら頭だけ 512 で描いてから縮める
        /// </summary>
        public static Skin MakeSkin(RocketboxPerson who, RocketboxPaint.Look look, int headSize, bool withTwinHead)
        {
            var skin = new Skin(who) { JawScale = look.JawScale };
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
                    var shell = PaintShell(who);
                    var shellSpec = Keep(skin, Tex(ToColors(shell.Spec), 512, true, SpecSize));
                    skin.Shell = Keep(skin, LitHead("Shell", Keep(skin, Tex(shell.Px, 512, false, headSize)), shellSpec));
                    var lash = RocketboxPaint.Hair(ToColors(RocketboxTextures.ReadPng(who.LashSrc, out n, out n)), look);
                    skin.Lash = Keep(skin, Lit("Lash", Keep(skin, Tex(lash, n, true, 512)), 0.34f, true));
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
        public static Material LitHead(string name, Texture tex, Texture spec)
        {
            var m = Lit(name, tex, 1f, false);
            m.SetFloat("_WorkflowMode", 0f);
            m.EnableKeyword("_SPECULAR_SETUP");
            m.SetTexture("_SpecGlossMap", spec);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetColor("_SpecColor", Color.white);
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
