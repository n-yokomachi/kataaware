using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室（<c>Room.unity</c>）の部屋の形を、間取りの表（<see cref="RoomPlan"/>）から組む（シナリオ設計 5 節「間取り」）。
    ///
    /// <list type="table">
    /// <item><term>床・壁・天井</term><description>壁の中心の線と厚みから箱を並べ、マテリアルごとに一つの mesh（<c>Floor</c>・<c>Walls</c>・<c>Ceiling</c>）に焼く。
    /// テクスチャは世界の座標で貼る（1.8 m で一巡。前の 6 m の部屋の床と壁と同じ細かさ）。当たりは箱ごとの BoxCollider。
    /// 閉じた部屋（風呂・トイレ・物入れ・寝室）は外の壁と床と天井だけで、仕切りの壁は作らない（どこからも見えない）</description></item>
    /// <item><term>窓</term><description>前からの二つ（<c>WindowFront</c>・<c>Window</c>）はそのまま。居間の窓（<c>WindowFrontWest</c>）は WindowFront を写して西へずらし、
    /// カーテン（<c>CurtainFrontWest</c>）と窓からの明かり（<c>WindowLightFrontWest</c>）も同じく写す。
    /// ガラスと影止めとカーテンの開き方は <see cref="BuildRoomView"/>（組み終えたら続けて組み直す）</description></item>
    /// <item><term>ドア</term><description>玄関は前の <c>Door</c> を廊下の南の端へ移す。風呂・トイレ・物入れ・寝室のドアはその複製を、枠の幅に合わせて横だけ縮める。
    /// どれも閉じたまま。戸の向こうに板（<c>Back</c>）を立て、枠と戸の隙間から向こうの何も無い所が見えないようにする</description></item>
    /// <item><term>明かり</term><description>廊下に控えめな天井の明かり（<c>HallLamp</c>・<c>HallLight</c>）。埃（<c>Dust</c>）を LDK に合わせる</description></item>
    /// <item><term>場面 1 のドア</term><description>調べる対象 <c>door</c> を玄関のドアの前へ（<see cref="RoomPlan.DoorItemAt"/>）</description></item>
    /// </list>
    ///
    /// **家具には触らない**（家具は <c>HalfAware/Build the room furniture</c>）。組み終えたら、LDK と廊下の外へ出た物・壁に埋まった物を知らせる。
    /// 何度押しても同じ物になる（名前で探して置き直し、mesh は上書きで焼く）。組む前と後の物の一覧を比べて知らせる
    /// </summary>
    public static class BuildRoomShell
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string MeshDir = "Assets/Models/generated/roomshell";

        const string FloorName = "Floor";
        const string WallsName = "Walls";
        const string CeilingName = "Ceiling";
        const string BackName = "Back";
        const string HallLampName = "HallLamp";
        const string HallLightName = "HallLight";
        const string WestCurtainName = "CurtainFrontWest";
        const string WestLightName = "WindowLightFrontWest";

        /// <summary>テクスチャ一巡の長さ（m）× マテリアルの繰り返し。横は 6 m で 3.33 回（1.8 m）、壁の縦は 3 m で 1.67 回（1.8 m）</summary>
        const float SpanAcross = 6f;
        const float SpanUp = 3f;

        /// <summary>Kenney の doorway の枠の幅（等倍）と、2 倍にした時の、壁の中心の線から置き場の原点までの奥行き（前の Door の置き方から）</summary>
        const float DoorNativeWide = 0.49f;
        const float DoorScale = 2f;
        const float DoorPivotBack = 0.103f;
        /// <summary>戸の向こうの板の厚みと、壁の向こうの面からの離れ（枠は向こうの面から 3 cm 出るので、その先）</summary>
        const float BackThick = 0.03f;
        const float BackGap = 0.03f;

        /// <summary>
        /// 廊下の天井の明かり。部屋の明かり（RoomLight、強さ 10・届き 10 m・影なし）は壁を抜けて廊下も照らすので、足すのは控えめに
        /// （3 と 1.5 と 0.8 で撮り比べて、見え方の差は小さかった）
        /// </summary>
        const float HallIntensity = 1.5f;
        const float HallRange = 3.5f;
        const float HallLampScale = 1.5f;

        [MenuItem("HalfAware/Build the room shell", false, 205)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>
        /// Room.unity を開いて部屋の形を組み、保存する。続けて窓の外（<see cref="BuildRoomView.Build"/>）を組み直す（居間の窓のガラスと影止めとカーテン）。
        /// 開いている場面に未保存の変更があれば何もしない。Room を写して組む場面（場面 3・5・7）は、この後に組み直す
        /// </summary>
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
            var sb = new StringBuilder();
            sb.AppendLine(made);
            sb.AppendLine(BuildRoomView.Compare(before, after).Replace("（景色の根の外）", ""));
            sb.AppendLine("---- 窓の外 ----");
            sb.Append(BuildRoomView.Build());
            return sb.ToString();
        }

        /// <summary>開いている場面（Room）へ部屋の形を組む。**保存はしない**（<see cref="Build"/> が保存する）</summary>
        public static string Assemble(Scene scene)
        {
            var room = Root(scene, "Room");
            if (room == null) return "Room が無い";
            var sb = new StringBuilder();

            var floors = Floors();
            var walls = Walls();
            var ceilings = Ceilings();
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Models/generated", "roomshell");
            var floorMesh = Bake(FloorName, floors, false);
            var wallMesh = Bake(WallsName, walls);
            var ceilingMesh = Bake(CeilingName, ceilings, false);
            Shell(room, FloorName, floorMesh, "Assets/Materials/Room/ConcreteFloor.mat", floors);
            Shell(room, WallsName, wallMesh, "Assets/Materials/Room/Concrete.mat", walls);
            Shell(room, CeilingName, ceilingMesh, "Assets/Materials/Room/ConcreteCeiling.mat", null);
            var old = Obsolete(room);
            sb.AppendFormat("床 {0} 枚・壁 {1} 個・天井 {2} 枚の箱を焼いた（三角 {3}・{4}・{5}）。前の壁を {6} 個外した",
                floors.Count, walls.Count, ceilings.Count, floorMesh.triangles.Length / 3, wallMesh.triangles.Length / 3, ceilingMesh.triangles.Length / 3, old).AppendLine();

            sb.AppendLine(Windows(room));
            sb.AppendLine(Doors(room));
            sb.AppendLine(Lights(room));
            sb.AppendLine(Dust(room));
            sb.AppendLine(DoorItem(scene));
            sb.Append(Intrusions(room));
            return sb.ToString().TrimEnd();
        }

        // ---- 箱 --------------------------------------------------------------------

        struct Box
        {
            public Vector3 Min;
            public Vector3 Max;

            public Box(Vector3 min, Vector3 max)
            {
                Min = min;
                Max = max;
            }

            public Vector3 Centre { get { return (Min + Max) * 0.5f; } }
            public Vector3 Size { get { return Max - Min; } }
        }

        /// <summary>壁の抜け。from〜to は壁に沿った向き、low〜high は高さ</summary>
        struct Hole
        {
            public float From;
            public float To;
            public float Low;
            public float High;

            public Hole(float from, float to, float low, float high)
            {
                From = from;
                To = to;
                Low = low;
                High = high;
            }
        }

        /// <summary>張り出し（廊下と閉じた部屋）の外の線</summary>
        static Rect Wing
        {
            get { return Rect.MinMaxRect(RoomPlan.Bath.xMin, RoomPlan.Hall.yMin, RoomPlan.Bedroom.xMax, RoomPlan.Ldk.yMin); }
        }

        /// <summary>
        /// 壁の箱。x に沿って走る壁は角まで（両端を厚みの半分伸ばす）、z に沿って走る壁は x の壁の間だけ。
        /// 抜けの所は、抜けの下（窓の下の腰壁）と上（窓とドアと廊下の口の上の垂れ壁）だけを立てる
        /// </summary>
        static List<Box> Walls()
        {
            var h = RoomPlan.Wall * 0.5f;
            var ldk = RoomPlan.Ldk;
            var hall = RoomPlan.Hall;
            var wing = Wing;
            var boxes = new List<Box>();
            // 北の壁（窓二つ）と、東の壁の LDK の所（窓一つ）
            RunX(boxes, ldk.yMax, ldk.xMin - h, ldk.xMax + h, WindowHoles(false));
            RunZ(boxes, ldk.xMax, ldk.yMin + h, ldk.yMax - h, WindowHoles(true));
            // LDK の西の壁
            RunZ(boxes, ldk.xMin, ldk.yMin + h, ldk.yMax - h);
            // LDK の南の壁。廊下の口は戸の無い開口
            RunX(boxes, ldk.yMin, ldk.xMin - h, ldk.xMax + h, new Hole(hall.xMin + h, hall.xMax - h, 0f, RoomPlan.MouthHead));
            // 張り出しの南の端の壁（玄関）と、東（寝室）と西（風呂・トイレ・物入れ）の外の壁
            RunX(boxes, wing.yMin, wing.xMin - h, wing.xMax + h, DoorHoles(true, wing.yMin));
            RunZ(boxes, wing.xMax, wing.yMin + h, wing.yMax - h);
            RunZ(boxes, wing.xMin, wing.yMin + h, wing.yMax - h);
            // 廊下の両脇（閉じた部屋のドア）
            RunZ(boxes, hall.xMin, hall.yMin + h, hall.yMax - h, DoorHoles(false, hall.xMin));
            RunZ(boxes, hall.xMax, hall.yMin + h, hall.yMax - h, DoorHoles(false, hall.xMax));
            return boxes;
        }

        static Hole[] WindowHoles(bool east)
        {
            var holes = new List<Hole>();
            foreach (var w in RoomPlan.Windows)
                if (w.East == east) holes.Add(new Hole(w.Span.x, w.Span.y, RoomPlan.WindowSill, RoomPlan.WindowHead));
            return holes.ToArray();
        }

        /// <summary>壁の線 line に載るドアの抜け。枠は壁に 1 cm ずつ掛かるので、抜けは枠より幅で 2 cm・高さで 1 cm 小さい</summary>
        static Hole[] DoorHoles(bool alongX, float line)
        {
            var holes = new List<Hole>();
            foreach (var d in RoomPlan.Doors)
            {
                if (d.AlongX != alongX) continue;
                var at = alongX ? d.Centre.y : d.Centre.x;
                if (Mathf.Abs(at - line) > 1e-3f) continue;
                holes.Add(new Hole(d.Span.x + 0.01f, d.Span.y - 0.01f, 0f, RoomPlan.DoorHigh - 0.01f));
            }
            return holes.ToArray();
        }

        /// <summary>z = line を x の from〜to に走る壁</summary>
        static void RunX(List<Box> boxes, float line, float from, float to, params Hole[] holes)
        {
            Run(boxes, from, to, holes, (a, b, low, high) =>
                new Box(new Vector3(a, low, line - RoomPlan.Wall * 0.5f), new Vector3(b, high, line + RoomPlan.Wall * 0.5f)));
        }

        /// <summary>x = line を z の from〜to に走る壁</summary>
        static void RunZ(List<Box> boxes, float line, float from, float to, params Hole[] holes)
        {
            Run(boxes, from, to, holes, (a, b, low, high) =>
                new Box(new Vector3(line - RoomPlan.Wall * 0.5f, low, a), new Vector3(line + RoomPlan.Wall * 0.5f, high, b)));
        }

        static void Run(List<Box> boxes, float from, float to, Hole[] holes, System.Func<float, float, float, float, Box> make)
        {
            var top = RoomPlan.Ceiling;
            var sorted = new List<Hole>(holes);
            sorted.Sort((p, q) => p.From.CompareTo(q.From));
            var at = from;
            foreach (var hole in sorted)
            {
                if (hole.From > at + 1e-4f) boxes.Add(make(at, hole.From, 0f, top));
                if (hole.Low > 1e-4f) boxes.Add(make(hole.From, hole.To, 0f, hole.Low));
                if (hole.High < top - 1e-4f) boxes.Add(make(hole.From, hole.To, hole.High, top));
                at = hole.To;
            }
            if (to > at + 1e-4f) boxes.Add(make(at, to, 0f, top));
        }

        /// <summary>
        /// 床の板（上の面が y 0）。LDK を張り出しの西の線で二つに割り、張り出しと辺の頂点を揃える
        /// （廊下の口で、床の継ぎ目に T 字の頂点を作らない）
        /// </summary>
        static List<Box> Floors()
        {
            return Slabs(-RoomPlan.FloorThick, 0f);
        }

        static List<Box> Ceilings()
        {
            return Slabs(RoomPlan.Ceiling, RoomPlan.Ceiling + RoomPlan.Slab);
        }

        /// <summary>
        /// 床と天井の板。LDK を張り出しの西の線で二つに割り、割った辺と張り出しとの辺の頂点を揃える
        /// （継ぎ目に T 字の頂点を作らない。LDK の南の壁の西の下だけは板が無いが、壁の下なので見えない）
        /// </summary>
        static List<Box> Slabs(float low, float high)
        {
            var h = RoomPlan.Wall * 0.5f;
            var ldk = RoomPlan.Ldk;
            var wing = Wing;
            return new List<Box>
            {
                new Box(new Vector3(ldk.xMin - h, low, ldk.yMin), new Vector3(wing.xMin - h, high, ldk.yMax + h)),
                new Box(new Vector3(wing.xMin - h, low, ldk.yMin), new Vector3(ldk.xMax + h, high, ldk.yMax + h)),
                new Box(new Vector3(wing.xMin - h, low, wing.yMin - h), new Vector3(wing.xMax + h, high, ldk.yMin)),
            };
        }

        /// <summary>
        /// 箱を一つの mesh に焼く。面ごとに法線を持ち、uv は世界の座標から（縦の面は横が水平の向き・縦が高さ、横の面は x と z）。
        /// 同じ名前の mesh があれば中身を上書きする（guid を保つ）。
        ///
        /// **隣の箱と接して隠れる面は張らない。** 同じ面の上で向かい合う面は、覆われた所を削る（覆い切られた面は張らず、
        /// 壁の端の面は窓とドアの抜けの縁の所だけ残す）。残すと、表の面との継ぎ目で奥の面が深さを争って、暗い破線が出る
        /// （廊下の口の床の継ぎ目で出た）。sides が false なら上と下の面だけ（床と天井の板。横の面は壁の下と上に隠れる）
        /// </summary>
        static Mesh Bake(string name, List<Box> boxes, bool sides = true)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (var i = 0; i < boxes.Count; i++)
                for (var axis = 0; axis < 3; axis++)
                {
                    if (!sides && axis != 1) continue;
                    foreach (var sign in new[] { 1, -1 })
                        foreach (var r in Uncovered(boxes, i, axis, sign))
                            Face(v, n, uv, t, boxes[i], axis, sign, r);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return ProcMesh.Save(mesh, MeshDir + "/" + name + ".asset");
        }

        /// <summary>面の上の二つの向き（表が +法線になる順）。x の面は (y, z)、y の面は (z, x)、z の面は (x, y)。裏向きは入れ替える</summary>
        static void Tangents(int axis, int sign, out int a, out int b)
        {
            a = (axis + 1) % 3;
            b = (axis + 2) % 3;
            if (sign < 0) { var k = a; a = b; b = k; }
        }

        /// <summary>
        /// 箱 i の面（axis の向き、sign の側）のうち、向かい合う箱の面に覆われていない所（面の上の二つの向きの範囲、x が a・y が b）。
        /// 覆う物がどれも一つの向きに面の端から端まで掛かるなら、もう一つの向きで削る。そうでなければ削らずに丸ごと返す
        /// </summary>
        static List<Rect> Uncovered(List<Box> boxes, int i, int axis, int sign)
        {
            int a, b;
            Tangents(axis, sign, out a, out b);
            var me = boxes[i];
            var plane = sign > 0 ? me.Max[axis] : me.Min[axis];
            var face = Rect.MinMaxRect(me.Min[a], me.Min[b], me.Max[a], me.Max[b]);
            var covers = new List<Rect>();
            for (var j = 0; j < boxes.Count; j++)
            {
                if (j == i) continue;
                var o = boxes[j];
                var at = sign > 0 ? o.Min[axis] : o.Max[axis];
                if (Mathf.Abs(at - plane) > 1e-4f) continue;
                var c = Rect.MinMaxRect(Mathf.Max(face.xMin, o.Min[a]), Mathf.Max(face.yMin, o.Min[b]), Mathf.Min(face.xMax, o.Max[a]), Mathf.Min(face.yMax, o.Max[b]));
                if (c.width > 1e-4f && c.height > 1e-4f) covers.Add(c);
            }
            var left = new List<Rect>();
            if (covers.Count == 0) { left.Add(face); return left; }
            var acrossA = covers.TrueForAll(c => c.xMin <= face.xMin + 1e-4f && c.xMax >= face.xMax - 1e-4f);
            var acrossB = covers.TrueForAll(c => c.yMin <= face.yMin + 1e-4f && c.yMax >= face.yMax - 1e-4f);
            if (acrossB)
                foreach (var span in Subtract(face.xMin, face.xMax, covers.ConvertAll(c => new Vector2(c.xMin, c.xMax))))
                    left.Add(Rect.MinMaxRect(span.x, face.yMin, span.y, face.yMax));
            else if (acrossA)
                foreach (var span in Subtract(face.yMin, face.yMax, covers.ConvertAll(c => new Vector2(c.yMin, c.yMax))))
                    left.Add(Rect.MinMaxRect(face.xMin, span.x, face.xMax, span.y));
            else left.Add(face);
            return left;
        }

        /// <summary>from〜to から cuts の範囲を除いた残り</summary>
        static List<Vector2> Subtract(float from, float to, List<Vector2> cuts)
        {
            cuts.Sort((p, q) => p.x.CompareTo(q.x));
            var left = new List<Vector2>();
            var at = from;
            foreach (var c in cuts)
            {
                if (c.x > at + 1e-4f) left.Add(new Vector2(at, Mathf.Min(c.x, to)));
                at = Mathf.Max(at, c.y);
            }
            if (to > at + 1e-4f) left.Add(new Vector2(at, to));
            return left;
        }

        /// <summary>箱の面の一部 r（面の上の二つの向きの範囲）を張る。表は du × dv の向き（Unity の時計回り）</summary>
        static void Face(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Box box, int axis, int sign, Rect r)
        {
            int a, b;
            Tangents(axis, sign, out a, out b);
            var p = Vector3.zero;
            p[axis] = sign > 0 ? box.Max[axis] : box.Min[axis];
            p[a] = r.xMin;
            p[b] = r.yMin;
            var du = Vector3.zero;
            du[a] = r.width;
            var dv = Vector3.zero;
            dv[b] = r.height;
            var normal = Vector3.zero;
            normal[axis] = sign;
            var i = v.Count;
            foreach (var q in new[] { p, p + du, p + du + dv, p + dv })
            {
                v.Add(q);
                n.Add(normal);
                if (axis == 1) uv.Add(new Vector2(q.x / SpanAcross, q.z / SpanAcross));
                else if (axis == 0) uv.Add(new Vector2(q.z / SpanAcross, q.y / SpanUp));
                else uv.Add(new Vector2(q.x / SpanAcross, q.y / SpanUp));
            }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>
        /// 床・壁・天井の物を置き直す。部屋の子の、置き方の無い（原点・等倍の）物に mesh とマテリアルを当て、
        /// boxes があれば箱ごとの BoxCollider を揃える（足りなければ足し、余れば外す）
        /// </summary>
        static void Shell(Transform room, string name, Mesh mesh, string materialPath, List<Box> boxes)
        {
            var t = room.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(room, false);
            }
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) Debug.LogWarning("マテリアルが無い: " + materialPath);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            var colliders = new List<BoxCollider>(t.GetComponents<BoxCollider>());
            var want = boxes != null ? boxes.Count : 0;
            for (var i = colliders.Count - 1; i >= want; i--)
            {
                Object.DestroyImmediate(colliders[i]);
                colliders.RemoveAt(i);
            }
            for (var i = 0; i < want; i++)
            {
                var c = i < colliders.Count ? colliders[i] : t.gameObject.AddComponent<BoxCollider>();
                c.isTrigger = false;
                c.center = boxes[i].Centre;
                c.size = boxes[i].Size;
                EditorUtility.SetDirty(c);
            }
            EditorUtility.SetDirty(t.gameObject);
        }

        /// <summary>前の部屋の壁（一枚ずつの立方体の Wall_*）を外す。数を返す</summary>
        static int Obsolete(Transform room)
        {
            var gone = 0;
            for (var i = room.childCount - 1; i >= 0; i--)
            {
                var c = room.GetChild(i);
                if (!c.name.StartsWith("Wall_")) continue;
                Object.DestroyImmediate(c.gameObject);
                gone++;
            }
            return gone;
        }

        // ---- 窓 --------------------------------------------------------------------

        /// <summary>居間の窓とカーテンと窓からの明かりを、前からの北の窓のものを写して西へずらして置く</summary>
        static string Windows(Transform room)
        {
            var shift = new Vector3(RoomPlan.WestWindow.Centre - RoomPlan.NorthWindow.Centre, 0f, 0f);
            var notes = new List<string>();
            notes.Add(Twin(room, RoomPlan.NorthWindow.Name, RoomPlan.WestWindow.Name, shift));
            notes.Add(Twin(room, "CurtainFront", WestCurtainName, shift));
            notes.Add(Twin(room, "WindowLightFront", WestLightName, shift));
            return "居間の窓: " + string.Join("、", notes.ToArray());
        }

        /// <summary>
        /// 部屋の子 source を写した target を置く（無ければ複製する）。target は source を shift だけずらした所に置き、
        /// 子の置き方は source の子と揃える（子の中身は複製した時のまま）
        /// </summary>
        static string Twin(Transform room, string source, string target, Vector3 shift)
        {
            var src = room.Find(source);
            if (src == null) return "写す元が無い: " + source;
            var dst = room.Find(target);
            var made = dst == null;
            if (made)
            {
                dst = Object.Instantiate(src.gameObject, room).transform;
                dst.name = target;
            }
            dst.localPosition = src.localPosition + shift;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
            dst.gameObject.SetActive(src.gameObject.activeSelf);
            foreach (Transform c in src)
            {
                var d = dst.Find(c.name);
                if (d == null)
                {
                    d = Object.Instantiate(c.gameObject, dst).transform;
                    d.name = c.name;
                }
                d.localPosition = c.localPosition;
                d.localRotation = c.localRotation;
                d.localScale = c.localScale;
                EditorUtility.SetDirty(d);
            }
            EditorUtility.SetDirty(dst);
            return target + (made ? "（複製した）" : "") + " x " + dst.position.x.ToString("F2");
        }

        // ---- ドア ------------------------------------------------------------------

        /// <summary>玄関のドア（前の Door）を移し、閉じた部屋のドアをその複製で置く。どれも戸の向こうに板を立てる</summary>
        static string Doors(Transform room)
        {
            var template = room.Find(RoomPlan.Entrance.Name);
            if (template == null) return "玄関のドア（Room/" + RoomPlan.Entrance.Name + "）が無い。ドアを置けない";
            var notes = new List<string>();
            foreach (var d in RoomPlan.Doors)
            {
                var t = room.Find(d.Name);
                var made = t == null;
                if (made)
                {
                    t = Object.Instantiate(template.gameObject, room).transform;
                    t.name = d.Name;
                }
                Place(t, d);
                Back(t, d);
                notes.Add(string.Format("{0}{1} 幅 {2:0.00}", d.Name, made ? "（複製した）" : "", d.Wide));
            }
            return "ドア: " + string.Join("、", notes.ToArray());
        }

        /// <summary>
        /// Kenney の doorway（原点は枠の片方の縁、枠は原点から -x へ、見る側は -z）を、壁の線の上のドアの所へ置く。
        /// 枠の見る側の面が壁の面に揃う。幅は横の倍率だけで合わせる
        /// </summary>
        static void Place(Transform t, RoomPlan.Door d)
        {
            var inward = new Vector3(d.Inward.x, 0f, d.Inward.y);
            var turn = Quaternion.LookRotation(-inward, Vector3.up);
            var right = turn * Vector3.right;
            var centre = new Vector3(d.Centre.x, 0f, d.Centre.y);
            t.localPosition = centre - inward * DoorPivotBack + right * (d.Wide * 0.5f);
            t.localRotation = turn;
            t.localScale = new Vector3(d.Wide / DoorNativeWide, DoorScale, DoorScale);
            EditorUtility.SetDirty(t);
        }

        /// <summary>戸の向こうの板。壁の向こうの面より先（枠の後ろ）に、抜けより一回り大きく立てる。当たりは無い（ドアの当たりが抜けを塞ぐ）</summary>
        static void Back(Transform door, RoomPlan.Door d)
        {
            var back = door.Find(BackName);
            if (back == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = BackName;
                back = go.transform;
                back.SetParent(door, false);
            }
            var inward = new Vector3(d.Inward.x, 0f, d.Inward.y);
            var centre = new Vector3(d.Centre.x, RoomPlan.DoorHigh * 0.5f, d.Centre.y);
            var behind = RoomPlan.Wall * 0.5f + BackGap + BackThick * 0.5f;
            back.position = door.parent.TransformPoint(centre - inward * behind);
            back.rotation = door.rotation;
            var size = new Vector3(d.Wide + 0.1f, RoomPlan.DoorHigh + 0.05f, BackThick);
            var parent = door.localScale;
            back.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
            var r = back.GetComponent<MeshRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/SteelDark.mat");
            r.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(back);
        }

        // ---- 明かり ------------------------------------------------------------------

        /// <summary>
        /// 廊下の天井の明かり。器（HallLamp）は部屋の天井の明かり（CeilingLamp）の形を小さくしたもの、光（HallLight）は影を落とさない点の明かり
        /// </summary>
        static string Lights(Transform room)
        {
            var at = RoomPlan.HallLamp;
            var lamp = room.Find(HallLampName);
            var source = room.Find("CeilingLamp");
            if (lamp == null && source != null)
            {
                lamp = Object.Instantiate(source.gameObject, room).transform;
                lamp.name = HallLampName;
            }
            var hang = 0f;
            if (lamp != null)
            {
                var mf = lamp.GetComponent<MeshFilter>();
                var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
                // 器の原点は下の面の角。真ん中を明かりの所へ、上の面を天井へ
                var s = HallLampScale;
                lamp.localRotation = Quaternion.identity;
                lamp.localScale = Vector3.one * s;
                lamp.localPosition = new Vector3(at.x - b.center.x * s, RoomPlan.Ceiling - b.max.y * s, at.y - b.center.z * s);
                hang = RoomPlan.Ceiling - (b.min.y * s + lamp.localPosition.y);
                EditorUtility.SetDirty(lamp);
            }
            var light = room.Find(HallLightName);
            if (light == null)
            {
                light = new GameObject(HallLightName).transform;
                light.SetParent(room, false);
            }
            light.localPosition = new Vector3(at.x, RoomPlan.Ceiling - hang - 0.05f, at.y);
            light.localRotation = Quaternion.identity;
            var l = light.GetComponent<Light>();
            if (l == null) l = light.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.white;
            l.intensity = HallIntensity;
            l.range = HallRange;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.GetUniversalAdditionalLightData();
            EditorUtility.SetDirty(l);
            return string.Format("廊下の明かり: 器 {0}、光 {1}（強さ {2}・届き {3} m）", lamp != null ? lamp.position.ToString("F2") : "無し",
                light.position.ToString("F2"), HallIntensity, HallRange);
        }

        /// <summary>埃を LDK に合わせる。広さ（36 m²）は前の部屋と同じなので、数と出る速さはそのまま</summary>
        static string Dust(Transform room)
        {
            var dust = room.Find("Dust");
            if (dust == null) return "埃（Room/Dust）が無い";
            FitDust(dust);
            return "埃: " + dust.position.ToString("F2") + " の " + dust.GetComponent<ParticleSystem>().shape.scale.ToString("F1");
        }

        /// <summary>埃の置き場と撒く箱。LDK の壁の面から 0.1 m 内（<see cref="BuildProps.BuildDust"/> も使う）</summary>
        public static void FitDust(Transform dust)
        {
            var ldk = RoomPlan.Ldk;
            dust.position = new Vector3(ldk.center.x, 1.45f, ldk.center.y);
            dust.rotation = Quaternion.identity;
            var ps = dust.GetComponent<ParticleSystem>();
            if (ps == null) return;
            var shape = ps.shape;
            var inset = RoomPlan.Wall + 0.2f;
            shape.scale = new Vector3(ldk.width - inset, 2.6f, ldk.height - inset);
            EditorUtility.SetDirty(ps);
        }

        // ---- 場面 1 のドア --------------------------------------------------------------

        static string DoorItem(Scene scene)
        {
            var items = Root(scene, "Interactables");
            var door = items != null ? items.Find("Interactable_door") : null;
            if (door == null) return "場面 1 のドアの調べる対象（Interactables/Interactable_door）が無い";
            door.position = RoomPlan.DoorItemAt;
            EditorUtility.SetDirty(door);
            return "場面 1 のドアの調べる対象: " + door.position.ToString("F2");
        }

        // ---- 見直し ------------------------------------------------------------------

        /// <summary>部屋の形の物の名前（見直しで数えない）</summary>
        static bool Owned(string name)
        {
            if (name == FloorName || name == WallsName || name == CeilingName || name == HallLampName || name == HallLightName) return true;
            if (name == WestCurtainName || name == WestLightName || name == "CurtainFront" || name == "CurtainRight") return true;
            if (name == "Dust" || name == "StandSpot" || name == "CeilingLamp" || name.StartsWith("WindowLight")) return true;
            foreach (var w in RoomPlan.Windows) if (w.Name == name) return true;
            foreach (var d in RoomPlan.Doors) if (d.Name == name) return true;
            return false;
        }

        /// <summary>
        /// 部屋の子（部屋の形の物を除く）のうち、歩ける内側（LDK と廊下）の外へ出た物と、壁の箱に食い込んだ物を並べる。
        /// 見た目の外箱（Renderer の bounds）で見て、2 cm までの食い込みは数えない
        /// </summary>
        public static string Intrusions(Transform room)
        {
            var walls = Walls();
            var bad = new List<string>();
            foreach (Transform c in room)
            {
                if (Owned(c.name)) continue;
                var rs = c.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var why = new List<string>();
                foreach (var p in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z) })
                    if (!RoomPlan.Open(p, -0.02f)) { why.Add("外へ出る"); break; }
                foreach (var w in walls)
                {
                    var into = Mathf.Min(b.max.x, w.Max.x) - Mathf.Max(b.min.x, w.Min.x);
                    var up = Mathf.Min(b.max.y, w.Max.y) - Mathf.Max(b.min.y, w.Min.y);
                    var deep = Mathf.Min(b.max.z, w.Max.z) - Mathf.Max(b.min.z, w.Min.z);
                    if (into > 0.02f && up > 0.02f && deep > 0.02f) { why.Add("壁に食い込む"); break; }
                }
                if (why.Count > 0) bad.Add(string.Format("{0}（{1}〜{2}）{3}", c.name, b.min.ToString("F2"), b.max.ToString("F2"), string.Join("・", why.ToArray())));
            }
            return bad.Count == 0 ? "見直し: 部屋の中の物はどれも LDK と廊下の内に収まっている"
                : "見直し: LDK と廊下の外へ出た物・壁に食い込んだ物 " + bad.Count + " 個: " + string.Join("、", bad.ToArray());
        }

        // ---- 物の一覧 --------------------------------------------------------------------

        /// <summary>場面の物の一覧（根からの道の名前。景色の根 RoomView の下も含む）</summary>
        public static List<string> Census(Scene scene)
        {
            var list = new List<string>();
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, r.name, list);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        static void Walk(Transform t, string path, List<string> list)
        {
            list.Add(path);
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path + "/" + t.GetChild(i).name, list);
        }

        static Transform Root(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r.transform;
            return null;
        }
    }
}
