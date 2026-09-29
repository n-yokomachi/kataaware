using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室（<c>Room.unity</c>）の窓の外の景色を組む（シナリオ設計 5 節「窓の外」）。
    ///
    /// 部屋はテムズ川の南岸のバーモンジーの 5 階（床から地面まで 12 m、<see cref="RoomView.Ground"/>）。近くは倫敦の落ち着いた住宅街
    /// （<c>BuildRoomViewTown.cs</c>）、その先に川と名所（<c>BuildRoomViewLandmarks.cs</c>）、遠くは市街のネオンと滲んだ空（<c>BuildRoomViewSky.cs</c>）。
    /// 窓から見える向きの幅（<see cref="RoomView.ArcFrom"/>〜<see cref="RoomView.ArcTo"/>）の外は作らない。
    ///
    /// <list type="table">
    /// <item><term>置き場</term><description>シーンの根に <c>RoomView</c> を一つ。子は街並み（Town。名所もここ）・暈（Glows）・空（Sky）・
    /// 窓の影止め（ShadowStop.*）だけ。部屋の物は、窓のガラスのマテリアルを替えることと、カーテンを開けること（<see cref="Curtains"/>）だけ</description></item>
    /// <item><term>時刻</term><description>夕暮れ（場面 1）と夜（場面 3・5・7）は同じ mesh にマテリアルを替えるだけ（<see cref="SetHour"/>）。
    /// 夜は空を落とし、窓の灯りを増やし（夜のアトラス）、街灯の暈とネオンを強める。場面 3 の組み立て（<see cref="BuildConnect"/>）が
    /// Room を写した直後に夜へ替え、場面 5・7 は場面 3 から写すので夜のまま来る</description></item>
    /// <item><term>ガラス</term><description>不透明に光る板（NightGlass）をやめ、薄い藤色が乗る透けたガラスにする。
    /// ガラスは影を落とさなくなるので、窓の抜けに影だけを落とす板（ShadowStop）を置き、部屋の中の日の当たり方を変えない</description></item>
    /// <item><term>重さ</term><description>描く回数は街並み・暈・空の三つ。mesh は <c>Assets/Models/generated/roomview/</c> に焼き、
    /// 何度押しても上書きで同じ物になる</description></item>
    /// </list>
    ///
    /// **組み直す前と後で、景色の根の外の物の一覧（名前と数）を比べ、増減があれば知らせる**
    /// </summary>
    public static partial class BuildRoomView
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string RootName = "RoomView";
        public const string MeshDir = "Assets/Models/generated/roomview/";
        public const string MaterialDir = "Assets/Materials/RoomView";

        const string TownName = "Town";
        const string GlowName = "Glows";
        const string SkyName = "Sky";

        static readonly string[] GlassPaths = { "Room/Window/Glass", "Room/WindowFront/Glass" };

        [MenuItem("HalfAware/Build the room view", false, 211)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>Room.unity を開いて景色を組み、保存する。開いている場面に未保存の変更があれば何もしない</summary>
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "再生中は組まない。止めてからもう一度";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var scene = EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            var before = Census(scene);
            var made = Assemble(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return "Room.unity を保存できなかった";
            AssetDatabase.SaveAssets();
            var after = Census(scene);
            return made + System.Environment.NewLine + Compare(before, after);
        }

        /// <summary>
        /// 開いている場面へ景色を組む（夕暮れ）。絵・mesh・マテリアルを焼き直し、景色の根を置き直し、ガラスを替え、カーテンを開ける。
        /// **保存はしない。** 保存するのは <see cref="Build"/>。確かめの撮影（<see cref="CheckRoomView.Preview"/>）は組んで撮ってから捨てる
        /// </summary>
        public static string Assemble(Scene scene)
        {
            PaintAll();
            var town = MakeTown();
            var root = Place(scene, town, RoomView.Hour.Dusk);
            var glass = Glass(root.transform);
            var curtains = Curtains();
            AssetDatabase.SaveAssets();
            var sb = new StringBuilder();
            sb.AppendFormat("窓の外を組んだ（夕暮れ）。家 {0} 軒・木 {1} 本・車 {2} 台・街灯 {3} 本・名所 {4}", town.Houses, town.Trees, town.Cars, town.Lamps.Count, town.Landmarks).AppendLine();
            sb.AppendFormat("三角: 街並み {0}・暈 {1}・空 {2}（頂点 {3}・{4}・{5}）。描く回数は 3（ほかにガラス 2 枚は前から）",
                town.Solid.Tris, town.Glow.Tris, town.Sky.Tris, town.Solid.Verts, town.Glow.Verts, town.Sky.Verts).AppendLine();
            sb.AppendLine("mesh の大きさ: " + MeshSizes());
            sb.AppendLine(glass);
            sb.Append(curtains);
            return sb.ToString();
        }

        // ---- 置く ----------------------------------------------------------------

        /// <summary>前の景色を捨てて、新しい根を置く。mesh は上書きで焼く</summary>
        static GameObject Place(Scene scene, Town town, RoomView.Hour hour)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == RootName) Object.DestroyImmediate(r);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var mats = Materials();
            Part(root.transform, TownName, Save(town.Solid.Bake(TownName), MeshDir + TownName + ".asset"));
            Part(root.transform, GlowName, Save(town.Glow.Bake(GlowName), MeshDir + GlowName + ".asset"));
            Part(root.transform, SkyName, Save(town.Sky.Bake(SkyName), MeshDir + SkyName + ".asset"));
            Dress(root, mats, hour);
            return root;
        }

        static void Part(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            go.isStatic = false;
        }

        /// <summary>
        /// mesh をアセットとして上書きする。前の物があれば中身を写して guid を保つ（場面 3・5・7 の参照が切れない）。
        /// ProcMesh.Save は頂点色を写さないので使わない
        /// </summary>
        static Mesh Save(Mesh mesh, string path)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir);
            }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // 中身は mesh の口から写す（CopySerialized で写すと、同じ呼び出しの中で撮るときに前の形のまま描かれる）
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetUVs(0, mesh.uv);
            existing.SetColors(mesh.colors32);
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static string MeshSizes()
        {
            var parts = new List<string>();
            long total = 0;
            foreach (var name in new[] { TownName, GlowName, SkyName })
            {
                var file = MeshDir + name + ".asset";
                if (!File.Exists(file)) continue;
                var size = new FileInfo(file).Length;
                total += size;
                parts.Add(string.Format("{0} {1:0.00} MB", name, size / 1048576f));
            }
            return string.Join("・", parts.ToArray()) + string.Format("（計 {0:0.00} MB）", total / 1048576f);
        }

        // ---- 時刻 ----------------------------------------------------------------

        /// <summary>時刻ごとのマテリアル。街並み・暈・空</summary>
        sealed class Set
        {
            public Material Town;
            public Material Glow;
            public Material Sky;
        }

        static Dictionary<RoomView.Hour, Set> Materials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/Materials", "RoomView");
            var sets = new Dictionary<RoomView.Hour, Set>();
            foreach (RoomView.Hour hour in System.Enum.GetValues(typeof(RoomView.Hour)))
            {
                var night = hour == RoomView.Hour.Night;
                var look = LookOf(hour);
                var suffix = night ? "Night" : "";
                sets[hour] = new Set
                {
                    Town = TownMat("Town" + suffix, night ? TownNightPath : TownDuskPath, look),
                    Glow = GlowMat("Glow" + suffix, look),
                    Sky = SkyMat("Sky" + suffix, night ? SkyNightPath : SkyDuskPath),
                };
            }
            return sets;
        }

        static Material Mat(string name, string shader)
        {
            var path = MaterialDir + "/" + name + ".mat";
            var s = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(s) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != s) m.shader = s;
            return m;
        }

        static void Haze(Material m, Look look)
        {
            m.SetColor("_Haze", look.Haze);
            m.SetColor("_HazeWarm", look.HazeWarm);
            m.SetVector("_WarmDir", new Vector4(Sunward.x, 0f, Sunward.z, 0f));
            m.SetFloat("_WarmWidth", WarmWidth);
            m.SetFloat("_HazeNear", 20f);
            m.SetFloat("_HazeFar", 420f);
            m.SetFloat("_HazeMax", look.HazeMax);
            m.SetFloat("_HazeCurve", 0.8f);
            m.SetFloat("_GlowHaze", look.GlowHaze);
        }

        static Material TownMat(string name, string texture, Look look)
        {
            var m = Mat(name, "HalfAware/Townscape");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            m.SetColor("_Tint", look.Tint);
            m.SetColor("_GlowTint", look.GlowTint);
            m.SetFloat("_GlowShade", 0.5f);
            Haze(m, look);
            m.SetFloat("_Cutoff", 0.25f);
            m.SetFloat("_Additive", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetShaderPassEnabled("DepthOnly", true);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GlowMat(string name, Look look)
        {
            var m = Mat(name, "HalfAware/Townscape");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath));
            m.SetColor("_Tint", Color.white);
            m.SetColor("_GlowTint", look.Halo);
            Haze(m, look);
            m.SetFloat("_Cutoff", -1f);
            m.SetFloat("_Additive", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SkyMat(string name, string texture)
        {
            var m = Mat(name, "HalfAware/Backdrop");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            m.SetColor("_BaseColor", Color.white);
            // 空は抜かない（α は 1）
            m.SetFloat("_Cutoff", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Dress(GameObject root, Dictionary<RoomView.Hour, Set> mats, RoomView.Hour hour)
        {
            var set = mats[hour];
            Assign(root.transform, TownName, set.Town);
            Assign(root.transform, GlowName, set.Glow);
            Assign(root.transform, SkyName, set.Sky);
        }

        static void Assign(Transform root, string child, Material m)
        {
            var t = root.Find(child);
            if (t == null) return;
            var r = t.GetComponent<MeshRenderer>();
            if (r == null) return;
            r.sharedMaterial = m;
            EditorUtility.SetDirty(r);
        }

        /// <summary>
        /// 開いている場面の景色の時刻を替える（マテリアルを替えるだけ）。景色が無ければ false。
        /// 場面 3 の組み立てが Room を写した直後に夜へ替える。保存は呼ぶ側
        /// </summary>
        public static bool SetHour(RoomView.Hour hour)
        {
            GameObject root = null;
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == RootName) root = r;
            if (root == null)
            {
                Debug.LogWarning("窓の外の景色が無い。HalfAware/Build the room view で Room に組んでから写す");
                return false;
            }
            Dress(root, Materials(), hour);
            return true;
        }

        /// <summary>景色の根の時刻（街並みのマテリアルから読む）。試験と確かめのため</summary>
        public static RoomView.Hour? HourIn(Scene scene)
        {
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name != RootName) continue;
                var t = r.transform.Find(TownName);
                var m = t != null ? t.GetComponent<MeshRenderer>().sharedMaterial : null;
                if (m == null) return null;
                return m.name.EndsWith("Night") ? RoomView.Hour.Night : RoomView.Hour.Dusk;
            }
            return null;
        }

        // ---- ガラス ----------------------------------------------------------------

        /// <summary>
        /// 窓のガラスを透けるガラスにし、窓の抜けに影だけを落とす板を置く。
        /// 前のガラス（光る不透明の板）は影を落としていて、日の灯り（Directional Light）を窓で止めていた
        /// </summary>
        static string Glass(Transform root)
        {
            var mat = GlassMat();
            var shadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Concrete.mat");
            var notes = new List<string>();
            foreach (var path in GlassPaths)
            {
                var go = GameObject.Find(path);
                if (go == null) { notes.Add("ガラスが無い: " + path); continue; }
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                EditorUtility.SetDirty(r);
                var stop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stop.name = "ShadowStop." + go.transform.parent.name;
                Object.DestroyImmediate(stop.GetComponent<Collider>());
                stop.transform.SetParent(root, false);
                stop.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
                stop.transform.localScale = go.transform.lossyScale;
                var sr = stop.GetComponent<MeshRenderer>();
                sr.sharedMaterial = shadow;
                sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                sr.receiveShadows = false;
                var b = r.bounds;
                notes.Add(string.Format("{0}: {1}〜{2}", path, b.min.ToString("F2"), b.max.ToString("F2")));
            }
            return "ガラス: " + string.Join("、", notes.ToArray());
        }

        /// <summary>透けるガラス。灯りを受けない薄い藤色で、景色が読める濃さに留める</summary>
        static Material GlassMat()
        {
            var m = Mat("WindowGlass", "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.86f, 0.14f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- カーテン --------------------------------------------------------------

        /// <summary>
        /// カーテンを開ける（オーナー「今ってカーテンは半開きなんだっけ？じゃあ開いてOK」）。
        /// 北の窓は左右の襞を窓の抜けの外まで寄せ、座って始めた目から名所の並ぶ側が見えるようにする。
        /// 東の窓は左右へ 0.18 m ずつ寄せる。襞の間隔は元の 0.09 m のまま、レールは寄せた襞に合わせて伸ばす。
        /// 置く所を決め打ちするので、何度押しても同じ所に来る。カーテンは影を落とすが、日の灯りは窓の影止めで止まるので、部屋の中の当たり方は変わらない
        /// </summary>
        static string Curtains()
        {
            var notes = new List<string>();
            notes.Add(Drape("Room/CurtainFront", true, -2.21f, -0.75f, -1.30f, 1.94f));
            notes.Add(Drape("Room/CurtainRight", false, -1.34f, -0.02f, -0.50f, 1.80f));
            return "カーテン: " + string.Join("、", notes.ToArray());
        }

        /// <summary>
        /// 襞 Fold1〜5 を left から、Fold6〜10 を right から 0.09 m おきに並べる。alongX なら x に、そうでなければ z に。
        /// レールは真ん中 rail・長さ span
        /// </summary>
        static string Drape(string path, bool alongX, float left, float right, float rail, float span)
        {
            var root = GameObject.Find(path);
            if (root == null) return "無い: " + path;
            var moved = 0;
            for (var i = 1; i <= 10; i++)
            {
                var fold = root.transform.Find("Fold" + i);
                if (fold == null) continue;
                var at = i <= 5 ? left + (i - 1) * 0.09f : right + (i - 6) * 0.09f;
                var p = fold.localPosition;
                if (alongX) p.x = at; else p.z = at;
                fold.localPosition = p;
                EditorUtility.SetDirty(fold);
                moved++;
            }
            var bar = root.transform.Find("Rail");
            if (bar != null)
            {
                var p = bar.localPosition;
                var s = bar.localScale;
                if (alongX) { p.x = rail; s.x = span; } else { p.z = rail; s.z = span; }
                bar.localPosition = p;
                bar.localScale = s;
                EditorUtility.SetDirty(bar);
            }
            return string.Format("{0} の襞 {1} 枚", path, moved);
        }

        // ---- 物の一覧 --------------------------------------------------------------

        /// <summary>景色の根の外の物の一覧（根からの道の名前）。同じ名前が並ぶ物は数で見る</summary>
        public static List<string> Census(Scene scene)
        {
            var list = new List<string>();
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == RootName) continue;
                Walk(r.transform, r.name, list);
            }
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        static void Walk(Transform t, string path, List<string> list)
        {
            list.Add(path);
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path + "/" + t.GetChild(i).name, list);
        }

        /// <summary>二つの一覧の違い。増えた物と減った物を名前で</summary>
        public static string Compare(List<string> before, List<string> after)
        {
            var left = new Dictionary<string, int>();
            foreach (var p in before) left[p] = (left.ContainsKey(p) ? left[p] : 0) + 1;
            var right = new Dictionary<string, int>();
            foreach (var p in after) right[p] = (right.ContainsKey(p) ? right[p] : 0) + 1;
            var added = new List<string>();
            var removed = new List<string>();
            foreach (var kv in right)
            {
                var was = left.ContainsKey(kv.Key) ? left[kv.Key] : 0;
                if (kv.Value > was) added.Add(kv.Key + " ×" + (kv.Value - was));
            }
            foreach (var kv in left)
            {
                var now = right.ContainsKey(kv.Key) ? right[kv.Key] : 0;
                if (kv.Value > now) removed.Add(kv.Key + " ×" + (kv.Value - now));
            }
            if (added.Count == 0 && removed.Count == 0)
                return string.Format("物の一覧（景色の根の外）: 前 {0}・後 {1}、増減なし", before.Count, after.Count);
            return string.Format("物の一覧（景色の根の外）: 前 {0}・後 {1}。増えた: {2}。減った: {3}",
                before.Count, after.Count, string.Join("、", added.ToArray()), string.Join("、", removed.ToArray()));
        }
    }
}
