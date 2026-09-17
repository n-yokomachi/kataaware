using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 部屋の小物のうち、既製の素材に無いものをここで組む。
    /// 手で置くと再生を抜けるたびに消えるので、手順として残しておく
    /// </summary>
    public static class BuildProps
    {
        public const string Generated = "Assets/Models/generated/";
        public const string Materials = "Assets/Materials/Room/";

        /// <summary>吸い殻の紙。灰をかぶって白は残っていない</summary>
        static readonly Color Paper = new Color(0.72f, 0.70f, 0.66f);
        /// <summary>吸い口。使い古して色が落ちている</summary>
        static readonly Color Filter = new Color(0.52f, 0.42f, 0.28f);
        /// <summary>焦げた先</summary>
        static readonly Color Burnt = new Color(0.10f, 0.09f, 0.08f);

        [MenuItem("HalfAware/Fill the ashtray")]
        public static void FillAshtrayMenu()
        {
            var tray = GameObject.Find("Room/Ashtray");
            if (tray == null) { Debug.LogError("灰皿が場面に無い"); return; }
            FillAshtray(tray.transform);
            Mark(tray);
        }

        [MenuItem("HalfAware/Build the hair clip")]
        public static void BuildHairClipMenu()
        {
            var room = GameObject.Find("Room");
            if (room == null) { Debug.LogError("部屋が場面に無い"); return; }
            var clip = BuildHairClip(room.transform);
            Selection.activeGameObject = clip;
            Mark(clip);
        }

        // ---- 灰皿の中身 -------------------------------------------------

        /// <summary>
        /// 吸い殻を敷き詰める。吸い口・紙・焦げた先の 3 つに分けて、
        /// それぞれ 1 つの mesh へ畳む。物の数を増やさずに本数だけ増やせる
        /// </summary>
        public static void FillAshtray(Transform tray)
        {
            // 前に置いたものは片付ける
            for (var i = tray.childCount - 1; i >= 0; i--)
            {
                var c = tray.GetChild(i);
                if (c.name == "Dish") continue;
                Object.DestroyImmediate(c.gameObject);
            }

            var filters = new List<Mesh>();
            var papers = new List<Mesh>();
            var burnt = new List<Mesh>();
            var ash = new List<Mesh>();

            // 皿の凹みの底は y=0.015、そこでの内側の半径は 0.046
            const float floor = 0.015f;
            const float reach = 0.046f;
            var state = Random.state;
            Random.InitState(20661);
            for (var i = 0; i < 16; i++)
            {
                var radius = Random.Range(0.0030f, 0.0036f);
                var edge = reach - radius;
                var offAngle = Random.Range(0f, Mathf.PI * 2f);
                var offReach = Mathf.Sqrt(Random.value) * edge * 0.72f;
                var layer = i < 9 ? 0f : Random.Range(0.004f, 0.010f);   // 後の方は上に積む
                var mid = new Vector3(Mathf.Sin(offAngle) * offReach, floor + radius + 0.0008f + layer,
                    Mathf.Cos(offAngle) * offReach);
                var axis = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f) * Vector3.forward;
                // 縁からはみ出さない長さに詰める。真ん中へ寄せると固まるので、長さの方を譲る
                var half = Mathf.Min(Random.Range(0.011f, 0.020f), Fits(mid, axis, edge), Fits(mid, -axis, edge));
                var a = mid - axis * half;
                var b = mid + axis * half;
                var squash = Random.value < 0.45f ? 0.55f : 1f;   // 揉み消したもの
                Butt(filters, papers, burnt, a, b, radius, squash);
            }
            // 灰の層。凹みの底を薄く覆う
            ash.Add(Disc(new Vector3(0f, floor + 0.0012f, 0f), reach * 0.95f, 0.0024f, 14));
            for (var i = 0; i < 5; i++)
            {
                var yaw = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var d = Random.Range(0.004f, reach * 0.7f);
                ash.Add(Disc(new Vector3(Mathf.Sin(yaw) * d, floor + 0.0022f, Mathf.Cos(yaw) * d),
                    Random.Range(0.004f, 0.010f), 0.0026f, 8));
            }
            Random.state = state;

            Place(tray, "Ash", ProcMesh.Save(ProcMesh.Combine(ash, null), Generated + "AshBed.asset"), Mat("Ink"));
            Place(tray, "Filters", ProcMesh.Save(ProcMesh.Combine(filters, null), Generated + "ButtFilters.asset"),
                Tinted("ButtFilter", Filter, 0f, 0.10f));
            Place(tray, "Papers", ProcMesh.Save(ProcMesh.Combine(papers, null), Generated + "ButtPapers.asset"),
                Tinted("ButtPaper", Paper, 0f, 0.06f));
            Place(tray, "Burnt", ProcMesh.Save(ProcMesh.Combine(burnt, null), Generated + "ButtBurnt.asset"),
                Tinted("ButtBurnt", Burnt, 0f, 0.04f));
        }

        /// <summary>centre から dir へ、半径 edge の円の内側に収まる長さ</summary>
        static float Fits(Vector3 centre, Vector3 dir, float edge)
        {
            var c = new Vector2(centre.x, centre.z);
            var d = new Vector2(dir.x, dir.z);
            if (d.sqrMagnitude < 1e-8f) return edge;
            d.Normalize();
            var along = Vector2.Dot(c, d);
            var root = along * along + edge * edge - c.sqrMagnitude;
            if (root <= 0f) return 0f;
            return Mathf.Max(0f, -along + Mathf.Sqrt(root));
        }

        /// <summary>吸い殻 1 本。a が吸い口、b が焦げた先。squash は揉み消した潰れ具合</summary>
        static void Butt(List<Mesh> filters, List<Mesh> papers, List<Mesh> burnt, Vector3 a, Vector3 b, float r, float squash)
        {
            var dir = (b - a).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var len = Vector3.Distance(a, b);
            var tip = a + dir * len * 0.34f;        // 吸い口の境
            var ember = a + dir * len * 0.88f;      // 焦げの境

            filters.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(a, r * 0.94f, r * 0.94f * squash, rot),
                new ProcMesh.Ring(tip, r, r * squash, rot),
            }, 7));
            papers.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(tip, r, r * squash, rot),
                new ProcMesh.Ring(ember, r * 0.97f, r * 0.97f * squash, rot),
            }, 7));
            burnt.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(ember, r * 0.97f, r * 0.97f * squash, rot),
                new ProcMesh.Ring(b, r * 0.70f, r * 0.70f * squash, rot),
            }, 7));
        }

        /// <summary>灰の層。平たい円盤</summary>
        static Mesh Disc(Vector3 centre, float radius, float height, int segments)
        {
            return ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(centre + Vector3.down * height * 0.5f, radius * 0.9f, radius * 0.9f, Quaternion.Euler(90f, 0f, 0f)),
                new ProcMesh.Ring(centre + Vector3.up * height * 0.5f, radius, radius, Quaternion.Euler(90f, 0f, 0f)),
            }, segments);
        }

        // ---- 髪ばさみ ---------------------------------------------------

        /// <summary>
        /// 髪を留めるばさみ。爪のある方を上にして、ソファの座面に転がしておく。
        /// 黒い革の上では黒い樹脂は沈むので、地金のままの色にする
        /// </summary>
        public static GameObject BuildHairClip(Transform room)
        {
            const float half = 0.038f;    // 長さの半分
            const float wide = 0.011f;    // 幅の半分
            var body = new List<Mesh>();

            // 下顎。まっすぐな板
            var lower = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 6; i++)
            {
                var x = Mathf.Lerp(-half, half, i / 6f);
                var w = wide * (1f - 0.25f * Mathf.Pow(Mathf.Abs(x) / half, 3f));
                lower.Add(new ProcMesh.Ring(new Vector3(x, 0.0038f, 0f), w, 0.0026f,
                    Quaternion.LookRotation(Vector3.right, Vector3.up)));
            }
            body.Add(ProcMesh.Loft(lower, 8));

            // 上顎。真ん中が持ち上がった弓なり
            var upper = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 10; i++)
            {
                var k = i / 10f;
                var x = Mathf.Lerp(-half, half, k);
                var arc = Mathf.Sin(k * Mathf.PI);
                var w = wide * (0.72f + 0.28f * arc);
                upper.Add(new ProcMesh.Ring(new Vector3(x, 0.0072f + 0.0128f * arc, 0f), w, 0.0024f,
                    Quaternion.LookRotation(Vector3.right, Vector3.up)));
            }
            body.Add(ProcMesh.Loft(upper, 8));

            // 爪。下顎から上へ。先は細くする
            for (var i = 0; i < 6; i++)
            {
                var x = Mathf.Lerp(-half * 0.74f, half * 0.74f, i / 5f);
                var lean = Mathf.Lerp(-9f, 9f, i / 5f);
                var root = new Vector3(x, 0.0052f, 0f);
                var top = root + Quaternion.Euler(0f, 0f, -lean) * Vector3.up * 0.0068f;
                body.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
                {
                    new ProcMesh.Ring(root, 0.0060f, 0.0022f, Quaternion.LookRotation(Vector3.up, Vector3.right)),
                    new ProcMesh.Ring(top, 0.0026f, 0.0007f, Quaternion.LookRotation(Vector3.up, Vector3.right)),
                }, 6));
            }

            var mesh = ProcMesh.Save(ProcMesh.Combine(body, null), Generated + "HairClip.asset");
            var pin = ProcMesh.Save(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(-half + 0.004f, 0.0062f, -wide - 0.0016f), 0.0026f, 0.0026f,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up)),
                new ProcMesh.Ring(new Vector3(-half + 0.004f, 0.0062f, wide + 0.0016f), 0.0026f, 0.0026f,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up)),
            }, 8), Generated + "HairClipPin.asset");

            var old = GameObject.Find("Room/HairClip");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("HairClip");
            go.transform.SetParent(room, false);
            // 座面は毛布に覆われているので、手前の肘掛けの上（y=0.777）に置く。
            // 髪から外してそのまま放ったという置き方
            go.transform.position = new Vector3(-2.46f, 0.777f, -0.79f);
            go.transform.rotation = Quaternion.Euler(0f, 11f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Steel");
            Place(go.transform, "Pin", pin, Mat("SteelDark"));
            return go;
        }

        // ---- 下ごしらえ -------------------------------------------------

        static GameObject Place(Transform parent, string name, Mesh mesh, Material mat)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        static Material Mat(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (m == null) Debug.LogWarning("材質が無い: " + name);
            return m;
        }

        /// <summary>Steel を下地に色だけ変えた材質を用意する。無ければ作って残す</summary>
        static Material Tinted(string name, Color col, float metal, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Mat("Steel"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
