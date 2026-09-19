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

        [MenuItem("HalfAware/Build the implant jack")]
        public static void BuildJackMenu()
        {
            var wrist = FindBone("Wrist.R");
            if (wrist == null) { Debug.LogError("右手首の骨が見つからない"); return; }
            var jack = BuildJack(wrist);
            Selection.activeGameObject = jack;
            Mark(jack);
        }

        [MenuItem("HalfAware/Build the binder")]
        public static void BuildBinderMenu()
        {
            var room = GameObject.Find("Room");
            if (room == null) { Debug.LogError("部屋が場面に無い"); return; }
            var made = BuildBinder(room.transform);
            Selection.activeGameObject = made;
            Mark(made);
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

        // ---- 紙ばさみ -----------------------------------------------------

        /// <summary>
        /// 売り上げを書きつけた紙ばさみ。厚紙の台に紙束を挟んで、頭に金具の口金。
        /// ソファの毛布の上に投げてある
        /// </summary>
        public static GameObject BuildBinder(Transform room)
        {
            const float w = 0.108f;    // 幅の半分。A4 より少し小さい
            const float h = 0.150f;    // 丈の半分

            var old = GameObject.Find("Room/Binder");
            if (old != null) Object.DestroyImmediate(old);
            var oldClip = GameObject.Find("Room/HairClip");
            if (oldClip != null) Object.DestroyImmediate(oldClip);

            var go = new GameObject("Binder");
            go.transform.SetParent(room, false);
            // 毛布の襞の上。座面そのものは毛布に覆われている
            go.transform.position = new Vector3(-2.40f, 0.636f, 0.19f);
            go.transform.rotation = Quaternion.Euler(0f, 62f, 0f);

            Slab(go.transform, "Board", new Vector3(0f, 0.0025f, 0f), Vector3.zero,
                new Vector3(w * 2f, 0.005f, h * 2f), Mat("PanelDark"));
            // 紙束。台より一回り小さく、少しずれて重なっている
            Slab(go.transform, "Paper", new Vector3(0.002f, 0.0075f, -0.004f), new Vector3(0f, 1.2f, 0f),
                new Vector3(w * 1.90f, 0.005f, h * 1.90f), Mat("Steel"));
            Slab(go.transform, "Paper2", new Vector3(-0.003f, 0.0106f, 0.002f), new Vector3(0f, -0.8f, 0f),
                new Vector3(w * 1.86f, 0.002f, h * 1.86f), Mat("Steel"));
            // 口金。頭に渡した金具と、押さえの爪
            Slab(go.transform, "Clip", new Vector3(0f, 0.0128f, h * 0.80f), Vector3.zero,
                new Vector3(w * 1.05f, 0.005f, 0.030f), Mat("SteelDark"));
            Slab(go.transform, "ClipLip", new Vector3(0f, 0.0150f, h * 0.80f - 0.017f), new Vector3(-16f, 0f, 0f),
                new Vector3(w * 0.98f, 0.003f, 0.012f), Mat("SteelDark"));
            Slab(go.transform, "HingeL", new Vector3(-w * 0.52f, 0.0128f, h * 0.92f), Vector3.zero,
                new Vector3(0.012f, 0.009f, 0.012f), Mat("Steel"));
            Slab(go.transform, "HingeR", new Vector3(w * 0.52f, 0.0128f, h * 0.92f), Vector3.zero,
                new Vector3(0.012f, 0.009f, 0.012f), Mat("Steel"));
            // 走り書き。紙の上に薄い帯を並べて、字が書いてあるように見せる
            for (var i = 0; i < 5; i++)
            {
                var y = Mathf.Lerp(h * 0.42f, -h * 0.55f, i / 4f);
                var len = w * (i == 0 ? 1.30f : Random.Range(0.85f, 1.55f));
                Slab(go.transform, "Line" + i, new Vector3(-w * 0.10f + len * 0.10f, 0.0119f, y), Vector3.zero,
                    new Vector3(len, 0.001f, 0.0035f), Mat("Ink"));
            }
            return go;
        }

        // ---- 手首のジャックとケーブル -------------------------------------

        /// <summary>
        /// 手首に刺さっているジャック。骨は 100 倍なので入れ物で打ち消す。
        /// 掌側の、肘寄りに刺す。座位では掌が上を向くので、下を見ると目に入る
        /// </summary>
        public static GameObject BuildJack(Transform wrist)
        {
            var old = wrist.Find("Jack");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var parts = new List<Mesh>();
            // 差し込み口の座金 → 胴 → ケーブルの根元、と +Z へ伸ばす
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, -0.002f), 0.0115f, 0.0115f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0035f), 0.0118f, 0.0118f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0050f), 0.0092f, 0.0092f),
            }, 10));
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0050f), 0.0088f, 0.0088f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0165f), 0.0086f, 0.0086f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0185f), 0.0062f, 0.0062f),
            }, 10));
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0185f), 0.0058f, 0.0058f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0300f), 0.0040f, 0.0040f),
            }, 8));
            var mesh = ProcMesh.Save(ProcMesh.Combine(parts, null), Generated + "Jack.asset");

            var go = new GameObject("Jack");
            go.transform.SetParent(wrist, false);
            go.transform.localScale = Vector3.one / wrist.lossyScale.x;
            // 掌の側へ、肘寄り（骨の -up）に寄せて刺す。
            // 掌の向きは骨の -forward（親指の位置から求めた）。座位では右腕だけ掌を上に返すので、
            // 差し込み口はそのまま目に入る
            go.transform.position = wrist.position - wrist.forward * 0.019f - wrist.up * 0.014f;
            go.transform.rotation = Quaternion.LookRotation(-wrist.forward, -wrist.up);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("SteelDark");

            var end = new GameObject("CableEnd");
            end.transform.SetParent(go.transform, false);
            end.transform.localPosition = new Vector3(0f, 0f, 0.030f);
            return go;
        }

        /// <summary>
        /// 椅子の差込口と手首のジャックを結ぶケーブル。毎フレーム張り直すので、
        /// 抜いて手が離れても繋がったまま垂れる
        /// </summary>
        [MenuItem("HalfAware/Wire the jack to the chair")]
        public static void WireCableMenu()
        {
            var chair = GameObject.Find("Room/Chair");
            var port = chair == null ? null : chair.transform.Find("PortHole");
            var jack = FindBone("Wrist.R");
            var end = jack == null ? null : jack.Find("Jack/CableEnd");
            if (port == null || end == null) { Debug.LogError("差込口かジャックが見つからない"); return; }

            var old = chair.transform.Find("Cable");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Cable");
            go.transform.SetParent(chair.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Ink");
            var cable = go.AddComponent<Cable>();
            var so = new SerializedObject(cable);
            so.FindProperty("from").objectReferenceValue = port;
            so.FindProperty("to").objectReferenceValue = end;
            // 座位で差込口とジャックは 12 cm ほどしか離れない。少し輪になって垂れるぶんを足す。
            // 組み立て時の腕は下ろした姿勢なので、そこからは測れない
            so.FindProperty("length").floatValue = 0.38f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 抜いた後にジャックを置く場所。差込口の脇
            var rest = chair.transform.Find("JackRest");
            if (rest == null)
            {
                rest = new GameObject("JackRest").transform;
                rest.SetParent(chair.transform, false);
            }
            rest.position = port.position + new Vector3(0f, 0.006f, -0.075f);
            rest.rotation = Quaternion.Euler(90f, 18f, 0f);
            Mark(go);
        }

        static Transform FindBone(string name)
        {
            var pro = GameObject.Find("Player/Protagonist");
            if (pro == null) return null;
            foreach (var t in pro.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ---- 調べられる物のピン --------------------------------------------

        /// <summary>
        /// 地図のピン。丸い頭に穴が開いていて、下が尖っている。
        /// 高さ 1 で、尖った先が原点。置いた点をそのまま指す。
        /// こちらを向かせて距離で伸ばすのは PinMarkers の仕事
        /// </summary>
        [MenuItem("HalfAware/Build the interaction pins")]
        public static void BuildPinsMenu()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogError("SceneFlow が見つからない"); return; }
            var made = BuildPins(flow);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildPins(SceneFlow flow)
        {
            var body = new List<Mesh>();
            // 頭。球
            var head = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 12; i++)
            {
                var a = Mathf.PI * i / 12f;
                var r = Mathf.Sin(a) * 0.30f;
                head.Add(new ProcMesh.Ring(new Vector3(0f, 0.70f - Mathf.Cos(a) * 0.30f, 0f), r, r,
                    Quaternion.LookRotation(Vector3.up, Vector3.forward)));
            }
            body.Add(ProcMesh.Loft(head, 12));
            // 首から先端まで
            var spike = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 8; i++)
            {
                var k = i / 8f;
                var y = Mathf.Lerp(0.62f, 0f, k);
                var r = Mathf.Lerp(0.20f, 0.0f, Mathf.Pow(k, 0.65f));
                spike.Add(new ProcMesh.Ring(new Vector3(0f, y, 0f), r, r,
                    Quaternion.LookRotation(Vector3.up, Vector3.forward)));
            }
            body.Add(ProcMesh.Loft(spike, 12, false, false));
            var mesh = ProcMesh.Save(ProcMesh.Combine(body, null), Generated + "Pin.asset");

            // 穴。手前側にだけ見えればよいので、薄い円盤を前へ出す
            var hole = ProcMesh.Save(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0.70f, -0.255f), 0.125f, 0.125f, Quaternion.identity),
                new ProcMesh.Ring(new Vector3(0f, 0.70f, -0.300f), 0.125f, 0.125f, Quaternion.identity),
            }, 10), Generated + "PinHole.asset");

            var old = GameObject.Find("Pins");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Pins");
            var source = new GameObject("PinSource");
            source.transform.SetParent(root.transform, false);
            source.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r0 = source.AddComponent<MeshRenderer>();
            r0.sharedMaterial = Glow("PinHead", new Color(0.96f, 0.74f, 0.22f), 2.2f);
            r0.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var eye = Place(source.transform, "Hole", hole, Tinted("PinHole", new Color(0.06f, 0.05f, 0.05f), 0f, 0.1f));
            eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            source.SetActive(false);

            var markers = root.AddComponent<PinMarkers>();
            var so = new SerializedObject(markers);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pin").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>自分で光るマテリアル。暗い部屋でもピンが沈まないように</summary>
        static Material Glow(string name, Color col, float strength)
        {
            var m = Tinted(name, col, 0f, 0.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", col * strength);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ---- 立ち上がる場所 ---------------------------------------------------

        /// <summary>
        /// 立ち上がって足を下ろす場所を、椅子と机のあいだへ置く。
        /// 椅子の当たりと机に触れない z を、椅子の前から机へ向かって探す
        /// </summary>
        [MenuItem("HalfAware/Fit the standing spot")]
        public static void FitStandSpotMenu()
        {
            var player = GameObject.Find("Player");
            var chair = GameObject.Find("Room/Chair");
            var spot = GameObject.Find("Room/StandSpot");
            if (player == null || chair == null || spot == null) { Debug.LogError("Player / Chair / StandSpot のどれかが無い"); return; }
            var cc = player.GetComponent<CharacterController>();
            var blocker = chair.transform.Find("Blocker");
            var wasOn = blocker != null && blocker.gameObject.activeSelf;
            if (blocker != null) blocker.gameObject.SetActive(true);

            var found = false;
            var at = spot.transform.position;
            // 椅子の前（机寄り）から探す。見つからなければ椅子の後ろへ下がる
            for (var z = 1.72f; z <= 2.10f; z += 0.02f)
            {
                var p = new Vector3(1.5f, 0.05f, z);
                if (!Clear(cc, player.transform, p)) continue;
                at = p;
                found = true;
                break;
            }
            if (!found)
            {
                for (var z = 0.60f; z >= 0.10f; z -= 0.02f)
                {
                    var p = new Vector3(1.5f, 0.05f, z);
                    if (!Clear(cc, player.transform, p)) continue;
                    at = p;
                    found = true;
                    Debug.LogWarning("椅子と机のあいだに立てる幅が無いので、椅子の後ろへ回した");
                    break;
                }
            }
            if (blocker != null) blocker.gameObject.SetActive(wasOn);
            if (!found) { Debug.LogError("立てる場所が見つからない"); return; }
            spot.transform.position = at;
            Debug.Log("立ち上がる場所を " + at.ToString("F3") + " にした");
            Mark(spot);
        }

        /// <summary>その場所に体の当たりが収まるか</summary>
        static bool Clear(CharacterController cc, Transform player, Vector3 at)
        {
            var bottom = at + cc.center + Vector3.up * (-cc.height * 0.5f + cc.radius);
            var top = at + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius);
            foreach (var col in Physics.OverlapCapsule(bottom, top, cc.radius))
            {
                if (col.transform.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        // ---- 部屋の埃 -------------------------------------------------------

        /// <summary>
        /// 空中を漂う埃。ごく小さい粒を部屋いっぱいに撒いて、ほとんど動かさない。
        /// 明かりの中を横切ったときにだけ見える程度に留める
        /// </summary>
        [MenuItem("HalfAware/Build the dust")]
        public static void BuildDustMenu()
        {
            var room = GameObject.Find("Room");
            if (room == null) { Debug.LogError("部屋がシーンに無い"); return; }
            var made = BuildDust(room.transform);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildDust(Transform room)
        {
            var old = room.Find("Dust");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Dust");
            go.transform.SetParent(room, false);
            go.transform.position = new Vector3(0f, 1.45f, 0f);
            go.transform.rotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 12f;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;                       // 入った瞬間から漂っている
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.016f);
            // 1/3 の解像度で描くので、小さすぎると点にもならない
            main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.038f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.88f, 0.87f, 0.84f, 0.34f), new Color(0.80f, 0.80f, 0.78f, 0.62f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.0012f);   // ゆっくり沈む
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 420;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 22f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5.6f, 2.6f, 5.6f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.006f, 0.010f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

            // ちらつき。粒が回って光を拾ったり落としたりする
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.75f, 0.70f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.010f);
            noise.frequency = 0.15f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.03f);
            noise.damping = true;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat("Smoke");            // 煙と同じ柔らかい粒の絵を使い回す
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.None;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            return go;
        }

        // ---- 煙草の煙 -----------------------------------------------------

        /// <summary>
        /// 口元から立ちのぼる煙。粒は世界の座標で動かすので、首を振っても置き去りになる。
        /// 一人称なので、目の少し下・少し前から出して視界を横切らせる
        /// </summary>
        [MenuItem("HalfAware/Build the cigarette smoke")]
        public static void BuildSmokeMenu()
        {
            var cam = GameObject.Find("Player/Main Camera");
            if (cam == null) { Debug.LogError("カメラが見つからない"); return; }
            var made = BuildSmoke(cam.transform);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildSmoke(Transform cam)
        {
            var old = cam.Find("SmokePuffs");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("SmokePuffs");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0.05f, -0.19f, 0.23f);
            go.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 6.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.82f, 0.81f, 0.78f, 0.44f), new Color(0.74f, 0.73f, 0.71f, 0.56f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.007f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 7.5f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.015f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.075f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.05f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var grow = new AnimationCurve();
            grow.AddKey(0f, 0.30f);
            grow.AddKey(0.4f, 1.3f);
            grow.AddKey(1f, 2.4f);
            size.size = new ParticleSystem.MinMaxCurve(1f, grow);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.62f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.085f);
            noise.frequency = 0.5f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.12f);
            noise.damping = true;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat("Smoke");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;

            var puffs = go.AddComponent<SmokePuffs>();
            var so = new SerializedObject(puffs);
            so.FindProperty("puffs").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---- 下ごしらえ -------------------------------------------------

        /// <summary>箱 1 つ。板や金具のように、曲面の要らないところに使う</summary>
        static GameObject Slab(Transform parent, string name, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

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
            if (m == null) Debug.LogWarning("マテリアルが無い: " + name);
            return m;
        }

        /// <summary>Steel を下地に色だけ変えたマテリアルを用意する。無ければ作って残す</summary>
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
