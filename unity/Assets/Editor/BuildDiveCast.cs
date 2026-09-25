using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の記憶の中の人 16 人を Rocketbox の模型で組み、一人ずつのプレハブにする（<c>docs/superpowers/specs/2026-09-26-dive-people-design.md</c>）。
    /// 一段目として単独で組むだけで、場面（Dive.unity）には置かない。動きの移し替えもまだしない。
    ///
    /// 一人の組み（<see cref="Folder"/> の {id}.prefab）:
    /// - 根（id の名前）の子の Figure が模型（FBX のプレハブ）。マテリアルは人ごと。服の色は元のまま、塗り替えは <see cref="RocketboxMemoryPaint"/>
    /// - 骨の縮尺（頭・腕・脚）と背の丸みを掛け、立ちの動き（<see cref="BodyPoser.Stand"/>）の頭の一こまで立たせた形で残す
    /// - 背は <see cref="DiveCast"/> の値。立った形（背の丸みも掛けた形）の髪の上から靴の裏までを測り、Figure ごと縮める。靴の裏は根の高さ 0
    /// - 手首の差込口は、18 以上の大人で手首が出る人だけ（<see cref="RocketboxMemory.Port"/>）
    ///
    /// テクスチャは、塗り替えるか大きさを変える物だけを <see cref="Folder"/> に写して描く。頭と髪の房は 512、体は 256。
    /// それ以外は取り込んだ写し（<c>Assets/Models/rocketbox/</c>）をそのまま使う。
    /// 同じ値で組み直せば同じ物になる（乱数は使わない）
    /// </summary>
    public static class BuildDiveCast
    {
        public const string Folder = BuildDive.Generated + "People/";

        public const int HeadSize = RocketboxMob.MemoryHeadSize;
        public const int BodySize = 256;

        /// <summary>
        /// 背の丸みを分ける骨（親から順）と、その比（背の丸みの度に掛ける）。
        /// Rocketbox（Biped）は腿が背骨の一つ目（Humanoid の Spine）の子なので、背骨の一つ目は曲げない（曲げると脚ごと前へ振れる）。
        /// 胸の二つで前へ丸め、首と頭で起こし返して、顔は 0.25 だけ下を向く（今の Quaternius の分け方と同じ和）。
        /// 鎖骨は首の子なので、肩は胸と一緒に前へ出る
        /// </summary>
        static readonly HumanBodyBones[] CurlBones = { HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head };
        static readonly float[] CurlShare = { 0.45f, 0.55f, -0.35f, -0.40f };

        [MenuItem("HalfAware/Dive people/Build the people", false, 311)]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        /// <summary>16 人を組む。組むのは見えない場面（プレビューの場面）の中で、開いている場面には触らない</summary>
        public static string BuildAll()
        {
            EnsureFolder();
            var sb = new StringBuilder("場面 4 の記憶の人を組んだ（" + Folder + "）\n");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var m in RocketboxMemory.All) sb.Append(Build(m, scene));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        public static string PrefabPath(RocketboxMemory m) { return Folder + m.Id + ".prefab"; }

        /// <summary>一人を組んでプレハブに書く</summary>
        public static string Build(RocketboxMemory m, Scene scene)
        {
            var person = m.Cast;
            string paintNote;
            var mats = Materials(m, out paintNote);
            var scale = ScaleOf(m);

            var root = new GameObject(m.Id);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var src = Source(m.Model);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
                model.name = "Figure";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale;
                var smr = Body(model);
                smr.sharedMaterials = Slots(m.Model, src, mats);
                // 骨の縮尺と姿勢で頭や手が元の箱から出ても消えないように
                smr.updateWhenOffscreen = true;

                // 差込口は、模型の元の姿勢（手首がまっすぐ）の肌に合わせる（主人公と同じ）。
                // 輪と穴の mesh は Animator の物の名前で書くので、その間だけ人ごとの名前にする
                if (m.Port)
                {
                    model.name = "Dive" + m.Id;
                    try { BuildProps.WristPort(model.GetComponent<Animator>()); }
                    finally { model.name = "Figure"; }
                }

                var an = model.GetComponent<Animator>();
                Pose(an, m);
                var box = Extent(root.transform, smr);
                model.transform.localPosition = new Vector3(0f, -box.min.y, 0f);
                var tall = box.size.y;

                bool ok;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(m), out ok);
                if (!ok) throw new System.InvalidOperationException("プレハブを書けない: " + PrefabPath(m));
                return string.Format(CultureInfo.InvariantCulture,
                    "{0}: 背 {1:0.000} m（一覧 {2:0.00}）、縮尺 {3:0.000}、骨 頭 {4:0.00}・腕 {5:0.00}・脚 {6:0.00}、背の丸み {7:0} 度、三角 {8}{9}\n  {10}\n",
                    m, tall, person.height, scale, m.Proportion.head, m.Proportion.arm, m.Proportion.leg, person.curl,
                    smr.sharedMesh.triangles.Length / 3, m.Port ? "、手首の差込口" : "", paintNote);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- 形 -------------------------------------------------------------------

        /// <summary>
        /// 立たせる。立ちの動きの頭の一こまを置き、骨の縮尺を掛け、背を丸める（記憶の中で動かすときの PersonMotion の上書きと同じ順）。
        /// Humanoid の動きは骨の縮尺を書かないが、置いた後に掛けておく
        /// </summary>
        public static void Pose(Animator an, RocketboxMemory m)
        {
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.applyRootMotion = false;
            BodyPoser.Stand(an);
            Size(an, m.Proportion);
            Curl(an, m.Cast.curl);
        }

        /// <summary>骨の縮尺。頭と、腕（上腕から先）と、脚（腿から先）に一様な縮尺を掛ける</summary>
        public static void Size(Animator an, DiveCast.Proportion p)
        {
            Scale(an, HumanBodyBones.Head, p.head);
            Scale(an, HumanBodyBones.LeftUpperArm, p.arm);
            Scale(an, HumanBodyBones.RightUpperArm, p.arm);
            Scale(an, HumanBodyBones.LeftUpperLeg, p.leg);
            Scale(an, HumanBodyBones.RightUpperLeg, p.leg);
        }

        static void Scale(Animator an, HumanBodyBones bone, float k)
        {
            var t = an.GetBoneTransform(bone);
            if (t != null) t.localScale = Vector3.one * k;
        }

        /// <summary>背を丸める（度。模型の右を軸に、正が前）</summary>
        public static void Curl(Animator an, float curl)
        {
            if (Mathf.Abs(curl) < 1e-3f) return;
            var right = an.transform.right;
            for (var i = 0; i < CurlBones.Length; i++)
            {
                var b = an.GetBoneTransform(CurlBones[i]);
                if (b != null) b.rotation = Quaternion.AngleAxis(curl * CurlShare[i], right) * b.rotation;
            }
        }

        /// <summary>
        /// 背を一覧の値にする縮尺。模型の写しを立たせて（骨の縮尺と背の丸みも掛けて）、髪の上から靴の裏までを測る
        /// </summary>
        public static float ScaleOf(RocketboxMemory m)
        {
            var go = (GameObject)Object.Instantiate(Source(m.Model));
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                Pose(go.GetComponent<Animator>(), m);
                var tall = Extent(go.transform, Body(go)).size.y;
                return tall > 0.1f ? m.Cast.height / tall : 1f;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>体の皮（模型の SkinnedMeshRenderer。差込口の部品ではない物）</summary>
        public static SkinnedMeshRenderer Body(GameObject model)
        {
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.transform.parent == model.transform) return smr;
            throw new System.InvalidOperationException("体の皮が無い: " + model.name);
        }

        /// <summary>皮を置いた形の広がりを root のローカルで</summary>
        public static Bounds Extent(Transform root, SkinnedMeshRenderer smr)
        {
            var tmp = new Mesh();
            try
            {
                smr.BakeMesh(tmp, true);
                var at = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var v = tmp.vertices;
                var box = new Bounds(at.MultiplyPoint3x4(v[0]), Vector3.zero);
                for (var i = 1; i < v.Length; i++) box.Encapsulate(at.MultiplyPoint3x4(v[i]));
                return box;
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }

        static GameObject Source(RocketboxMob who)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            if (src == null) throw new System.InvalidOperationException("模型が無い（HalfAware/Dive people/Import the people）: " + who.Model);
            return src;
        }

        // ---- テクスチャとマテリアル ----------------------------------------------------

        /// <summary>一人のマテリアル（体・頭・髪の房・眼鏡。無い物は null）</summary>
        public sealed class Look
        {
            public Material body, head, hair, glasses;
        }

        /// <summary>FBX の面の組の順に、マテリアルを並べる（面の組はマテリアルの名前で見分ける）</summary>
        public static Material[] Slots(RocketboxMob who, GameObject src, Look look)
        {
            var names = Body(src).sharedMaterials;
            var o = new Material[names.Length];
            for (var i = 0; i < o.Length; i++)
            {
                var n = names[i] != null ? names[i].name : "";
                o[i] = n == who.BodySlot ? look.body : n == who.HeadSlot ? look.head : n == who.HairSlot ? look.hair : n == who.GlassesSlot ? look.glasses : null;
                if (o[i] == null) throw new System.InvalidOperationException("面の組のマテリアルが決まらない: " + who.Name + " の " + n);
            }
            return o;
        }

        /// <summary>塗り替えを描いたテクスチャとマテリアルを書く</summary>
        static Look Materials(RocketboxMemory m, out string note)
        {
            var who = m.Model;
            var notes = new List<string>();
            var maps = Maps.Of(who);
            int w, h;

            // 頭
            Texture2D head;
            if (m.Hair != null)
            {
                var px = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out w, out h));
                if (w != HeadSize) throw new System.InvalidOperationException("頭のテクスチャが " + HeadSize + " でない（" + w + "）: " + who.HeadSrc);
                float[] weight;
                string n;
                px = RocketboxMemoryPaint.Head(px, maps.Head, maps.Anchors, m.Hair, m.HairLowest, m.Hairline, out weight, out n);
                notes.Add("頭: " + n);
                head = Write(px, w, h, Folder + m.Id + "_Head.png", false, HeadSize);
            }
            else head = Load(who.HeadSrc);

            // 体（256。上の服を塗る人と、取り込んだ写しが 512 の人は写して描く）
            Texture2D body;
            {
                var src = RocketboxTextures.ReadPng(who.BodySrc, out w, out h);
                if (m.Top != null || w != BodySize)
                {
                    if (w != BodySize) src = RocketboxTextures.DownsampleBy(src, w, h, w / BodySize, false);
                    var px = BuildRocketboxProtagonist.ToColors(src);
                    if (m.Top != null)
                    {
                        string n;
                        px = RocketboxMemoryPaint.Body(px, maps.Body, maps.Tall, m.Top, out n);
                        notes.Add("体: " + n);
                    }
                    body = Write(px, BodySize, BodySize, Folder + m.Id + "_Body.png", false, BodySize);
                }
                else body = Load(who.BodySrc);
            }

            // 髪の房
            Texture2D hair = null;
            if (who.HasOpacity)
            {
                if (m.Hair != null)
                {
                    var px = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HairSrc, out w, out h));
                    string n;
                    px = RocketboxMemoryPaint.Strands(px, w, h, maps.Hair, maps.Anchors, m.Hair, out n);
                    notes.Add(n);
                    hair = Write(px, w, h, Folder + m.Id + "_Hair.png", true, HeadSize);
                }
                else hair = Load(who.HairSrc);
            }

            var look = new Look
            {
                body = Save(BuildRocketboxProtagonist.Lit(m.Id + "_Body", body, 0.12f, false), Folder + m.Id + "_Body.mat"),
                head = Save(BuildRocketboxProtagonist.Lit(m.Id + "_Head", head, 0.22f, false), Folder + m.Id + "_Head.mat"),
            };
            if (hair != null) look.hair = Save(BuildRocketboxProtagonist.Lit(m.Id + "_Hair", hair, 0.34f, true), Folder + m.Id + "_Hair.mat");
            if (who.HasGlasses) look.glasses = Save(BuildRocketboxProtagonist.Lit(m.Id + "_Glasses", Load(who.GlassesSrc), 0.5f, true), Folder + m.Id + "_Glasses.mat");
            note = notes.Count > 0 ? string.Join("\n  ", notes.ToArray()) : "塗り替え無し";
            return look;
        }

        /// <summary>模型の束ねた姿勢の、頭と髪の房の面の位置の地図（512）、体の面の位置の地図（256）、顔の骨の位置、背（m）</summary>
        sealed class Maps
        {
            public RocketboxPaint.Surface Head, Hair, Body;
            public RocketboxPaint.Anchors Anchors;
            public float Tall;

            public static Maps Of(RocketboxMob who)
            {
                var go = (GameObject)Object.Instantiate(Source(who));
                go.hideFlags = HideFlags.HideAndDontSave;
                var baked = new Mesh();
                try
                {
                    go.transform.position = Vector3.zero;
                    go.transform.rotation = Quaternion.identity;
                    var smr = Body(go);
                    smr.BakeMesh(baked, true);
                    var v = baked.vertices;
                    for (var i = 0; i < v.Length; i++) v[i] = go.transform.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                    var uv = smr.sharedMesh.uv;
                    var maps = new Maps { Anchors = RocketboxPaint.Anchors.Of(go.transform) };
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
                    maps.Tall = hi - lo;
                    var names = smr.sharedMaterials;
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (names[i] == null) continue;
                        if (names[i].name == who.HeadSlot) maps.Head = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(i), HeadSize);
                        if (names[i].name == who.HairSlot) maps.Hair = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(i), HeadSize);
                        if (names[i].name == who.BodySlot) maps.Body = RocketboxPaint.Surface.Of(v, uv, smr.sharedMesh.GetTriangles(i), BodySize);
                    }
                    if (maps.Head == null || maps.Body == null) throw new System.InvalidOperationException("頭か体の面の組が無い: " + who.Name);
                    return maps;
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                    Object.DestroyImmediate(go);
                }
            }
        }

        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new System.InvalidOperationException("テクスチャが無い（HalfAware/Dive people/Import the people）: " + path);
            return t;
        }

        /// <summary>PNG を書いて取り込む（sRGB、ミップマップ、双線形、繰り返し無し、圧縮）。alpha なら透けを持つ</summary>
        static Texture2D Write(Color[] px, int w, int h, string path, bool alpha, int maxSize)
        {
            var c32 = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                c32[i] = px[i];
                if (!alpha) c32[i].a = 255;
            }
            RocketboxTextures.WritePng(c32, w, h, path, alpha);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = alpha;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = maxSize;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.isReadable = false;
                ti.SaveAndReimport();
            }
            return Load(path);
        }

        /// <summary>マテリアルを書く。あれば中身だけ差し替える（GUID を保つ）</summary>
        static Material Save(Material m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            old.shader = m.shader;
            old.CopyPropertiesFromMaterial(m);
            old.shaderKeywords = m.shaderKeywords;
            old.renderQueue = m.rawRenderQueue;
            old.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
            EditorUtility.SetDirty(old);
            Object.DestroyImmediate(m);
            return old;
        }

        static void EnsureFolder()
        {
            var dir = Folder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(BuildDive.Generated.TrimEnd('/'), "People");
        }
    }
}
