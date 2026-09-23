using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 主人公と片割れを Microsoft Rocketbox の Female_Adult_14（女大 14）から組み立てる。
    /// 顔も体もこの一人。髪とカーディガンを黒に、虹彩を琥珀に、目の下に黒子、顔に控えめな手入れ
    /// （<see cref="RocketboxPaint"/>）。場面には置かない（置くのは次の段で、場面の組み立てから <see cref="Build"/> を呼ぶ）。
    ///
    /// 片割れは主人公の鏡像。模型の根の x を裏返して、体ごと左右を入れ替える（<see cref="Twin"/>）。
    /// 黒子の絵は主人公と同じ一枚で、裏返した結果として本人の右目の下に来る。髪の分け目も逆になる
    /// </summary>
    public static class BuildRocketboxProtagonist
    {
        public const string Dir = "Assets/Models/rocketbox/Female_Adult_14/";
        public const string Model = Dir + "Female_Adult_14.fbx";
        public const string HeadSrc = Dir + "f017_head_color.png";
        public const string BodySrc = Dir + "f017_body_color.png";
        public const string HairSrc = Dir + "f017_opacity_color.png";
        /// <summary>手を入れたテクスチャとマテリアルの置き場（組み立てのたびに描き直す）</summary>
        public const string Painted = Dir + "Painted/";
        /// <summary>立ちと歩きの状態機械（今の Protagonist.controller の写しに、Humanoid へ移し替えた動きを差した物。<see cref="RocketboxRetarget"/> が作る）</summary>
        public const string Controller = "Assets/Animation/Humanoid/ProtagonistHumanoid.controller";

        /// <summary>FBX の中のマテリアルの名前（面の組の順は体・頭・透け）</summary>
        public const string BodySlot = "f017_body", HeadSlot = "f017_head", HairSlot = "f017_opacity";

        /// <summary>片割れの作り方</summary>
        public enum TwinMode
        {
            /// <summary>模型の根の x を裏返す。髪の分け目も黒子も逆になる</summary>
            MirrorWhole,
            /// <summary>同じ模型のまま、黒子だけ右目の下に描いた絵にする</summary>
            MoleOnly,
        }

        public const TwinMode Twin = TwinMode.MirrorWhole;

        /// <summary>頭のテクスチャの大きさ（256 か 512）</summary>
        public const int HeadSize = 512;

        /// <summary>今の見た目の値。手入れと黒子の大きさはオーナーが撮り比べて決める</summary>
        public static RocketboxPaint.Look Look()
        {
            return new RocketboxPaint.Look();
        }

        // ---- 組み立て ---------------------------------------------------------

        /// <summary>
        /// 組み立てて返す。parent が null なら場面の根に置く。twin なら片割れ。
        /// 手を入れたテクスチャが無ければ描いてから使う
        /// </summary>
        public static GameObject Build(Transform parent, bool twin)
        {
            var skin = LoadPainted(twin && Twin == TwinMode.MoleOnly);
            if (skin == null)
            {
                Paint(Look(), Twin == TwinMode.MoleOnly);
                skin = LoadPainted(twin && Twin == TwinMode.MoleOnly);
            }
            if (skin == null) throw new InvalidOperationException("手を入れたマテリアルを作れない: " + Painted);
            skin.JawScale = Look().JawScale;
            return Assemble(parent, twin, skin, Twin);
        }

        /// <summary>マテリアルの組を模型に着せる。撮り比べでは描いたばかりの（アセットでない）組を渡す</summary>
        public static GameObject Assemble(Transform parent, bool twin, Skin skin, TwinMode mode)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (src == null) throw new InvalidOperationException("模型が無い: " + Model);
            var her = (GameObject)Object.Instantiate(src, parent);
            her.name = twin ? "Twin" : "Protagonist";
            her.transform.localPosition = Vector3.zero;
            her.transform.localRotation = Quaternion.identity;
            her.transform.localScale = twin && mode == TwinMode.MirrorWhole ? new Vector3(-1f, 1f, 1f) : Vector3.one;
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
            foreach (var r in her.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var ms = r.sharedMaterials;
                for (var i = 0; i < ms.Length; i++)
                {
                    var n = ms[i] == null ? "" : ms[i].name;
                    if (n == BodySlot) ms[i] = skin.Body;
                    else if (n == HeadSlot) ms[i] = twinHead && skin.HeadTwin != null ? skin.HeadTwin : skin.Head;
                    else if (n == HairSlot) ms[i] = skin.Hair;
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
            public Material Body, Head, HeadTwin, Hair;
            /// <summary>顎の骨の左右の倍率（<see cref="RocketboxPaint.Look.JawScale"/>）</summary>
            public float JawScale = 1f;
            /// <summary>撮り比べで測るための、頭の印の絵と黒子の位置</summary>
            public Texture2D MaskHead, MaskHeadTwin;
            public RocketboxPaint.HeadResult HeadInfo, HeadTwinInfo;
            /// <summary>その場で作った物（アセットでない）。使い終えたら <see cref="Destroy"/></summary>
            public readonly List<Object> Made = new List<Object>();

            public void Destroy()
            {
                foreach (var o in Made) if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
                Made.Clear();
            }
        }

        [MenuItem("HalfAware/Rocketbox/Paint the protagonist")]
        public static void PaintMenu()
        {
            Debug.Log(Paint(Look(), Twin == TwinMode.MoleOnly));
        }

        /// <summary>
        /// 縮めたテクスチャに手を入れ、Painted/ に PNG とマテリアルで書く。
        /// withTwinHead なら片割れ用の頭（黒子が右目の下）も書く
        /// </summary>
        public static string Paint(RocketboxPaint.Look look, bool withTwinHead)
        {
            var sb = new System.Text.StringBuilder();
            EnsureFolder();
            // 描くのはいつも 512。頭を 256 にするときは取り込みの設定で縮める
            var maps = Maps.Get(512);
            int n;
            var head = RocketboxTextures.ReadPng(HeadSrc, out n, out n);
            var self = RocketboxPaint.Head(ToColors(head), maps.Head, maps.Anchors, look, false);
            WritePainted(self.Px, n, "Head_self", false, HeadSize);
            sb.AppendLine("頭（主人公）: " + self.Note + "、UV " + self.MoleUv.ToString("F4"));
            if (withTwinHead)
            {
                var twin = RocketboxPaint.Head(ToColors(head), maps.Head, maps.Anchors, look, true);
                WritePainted(twin.Px, n, "Head_twin", false, HeadSize);
                sb.AppendLine("頭（片割れ）: " + twin.Note);
            }
            var body = RocketboxTextures.ReadPng(BodySrc, out n, out n);
            float[] knit;
            WritePainted(RocketboxPaint.Body(ToColors(body), maps.Body, look, out knit), n, "Body", false, 512);
            var hair = RocketboxTextures.ReadPng(HairSrc, out n, out n);
            WritePainted(RocketboxPaint.Hair(ToColors(hair), look), n, "Hair", true, 512);

            SaveMaterial(Lit("Body", Load(Painted + "Body.png"), 0.12f, false), Painted + "Body.mat");
            SaveMaterial(Lit("Head_self", Load(Painted + "Head_self.png"), 0.22f, false), Painted + "Head_self.mat");
            if (withTwinHead) SaveMaterial(Lit("Head_twin", Load(Painted + "Head_twin.png"), 0.22f, false), Painted + "Head_twin.mat");
            SaveMaterial(Lit("Hair", Load(Painted + "Hair.png"), 0.34f, true), Painted + "Hair.mat");
            AssetDatabase.SaveAssets();
            sb.AppendLine("書いた所: " + Painted);
            return sb.ToString();
        }

        static Skin LoadPainted(bool twinHead)
        {
            var s = new Skin
            {
                Body = AssetDatabase.LoadAssetAtPath<Material>(Painted + "Body.mat"),
                Head = AssetDatabase.LoadAssetAtPath<Material>(Painted + "Head_self.mat"),
                HeadTwin = AssetDatabase.LoadAssetAtPath<Material>(Painted + "Head_twin.mat"),
                Hair = AssetDatabase.LoadAssetAtPath<Material>(Painted + "Hair.mat"),
            };
            if (s.Body == null || s.Head == null || s.Hair == null) return null;
            if (twinHead && s.HeadTwin == null) return null;
            return s;
        }

        /// <summary>
        /// 描いたばかりの組を、アセットにせずに作る（撮り比べ用）。テクスチャは取り込みと同じく DXT に圧縮する。
        /// headSize が 256 なら頭だけ 512 で描いてから縮める
        /// </summary>
        public static Skin MakeSkin(RocketboxPaint.Look look, int headSize, bool withTwinHead)
        {
            var skin = new Skin { JawScale = look.JawScale };
            try
            {
                var maps = Maps.Get(512);
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
                float[] knit;
                var body = RocketboxPaint.Body(ToColors(RocketboxTextures.ReadPng(BodySrc, out n, out n)), maps.Body, look, out knit);
                skin.Body = Keep(skin, Lit("Body", Keep(skin, Tex(body, n, false, 512)), 0.12f, false));
                var hair = RocketboxPaint.Hair(ToColors(RocketboxTextures.ReadPng(HairSrc, out n, out n)), look);
                skin.Hair = Keep(skin, Lit("Hair", Keep(skin, Tex(hair, n, true, 512)), 0.34f, true));
                return skin;
            }
            catch
            {
                skin.Destroy();
                throw;
            }
        }

        /// <summary>頭のマテリアルだけを描いて作る。作った物は into に入れる。印の絵（撮り比べで測る）も返す</summary>
        public static Material MakeHead(Skin into, RocketboxPaint.Look look, int headSize, bool twin, out RocketboxPaint.HeadResult info, out Texture2D mask)
        {
            var maps = Maps.Get(512);
            int n;
            var head = ToColors(RocketboxTextures.ReadPng(HeadSrc, out n, out n));
            info = RocketboxPaint.Head(head, maps.Head, maps.Anchors, look, twin);
            mask = Keep(into, MaskTex(info.Mask, n, headSize));
            return Keep(into, Lit(twin ? "Head_twin" : "Head_self", Keep(into, Tex(info.Px, n, false, headSize)), 0.22f, false));
        }

        /// <summary>手を入れていない縮めたテクスチャのままの組（撮り比べの「前」）</summary>
        public static Skin MakeRawSkin()
        {
            var skin = new Skin();
            skin.Head = Keep(skin, Lit("Head_raw", Load(HeadSrc), 0.22f, false));
            skin.Body = Keep(skin, Lit("Body_raw", Load(BodySrc), 0.12f, false));
            skin.Hair = Keep(skin, Lit("Hair_raw", Load(HairSrc), 0.34f, true));
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
            static readonly Dictionary<int, Maps> cache = new Dictionary<int, Maps>();

            /// <summary>模型の束ねた姿勢から作る。同じ大きさなら使い回す</summary>
            public static Maps Get(int n)
            {
                Maps m;
                if (cache.TryGetValue(n, out m)) return m;
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
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
                        if (names[i] != null && names[i].name == HeadSlot) head = i;
                        if (names[i] != null && names[i].name == BodySlot) body = i;
                    }
                    if (head < 0 || body < 0) throw new InvalidOperationException("頭か体の面の組が無い");
                    m = new Maps
                    {
                        Head = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(head), n),
                        Body = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(body), n),
                        Anchors = RocketboxPaint.Anchors.Of(go.transform),
                    };
                    cache[n] = m;
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
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            return m;
        }

        static void WritePainted(Color[] px, int n, string name, bool alpha, int maxSize)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                c32[i] = px[i];
                if (!alpha) c32[i].a = 255;
            }
            var path = Painted + name + ".png";
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
                old.renderQueue = m.renderQueue;
                old.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
                EditorUtility.SetDirty(old);
                Object.DestroyImmediate(m);
                return;
            }
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureFolder()
        {
            var p = Painted.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Painted");
        }
    }
}
