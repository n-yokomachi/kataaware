using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の路地裏を組む。グレビル・ストリートの入口から北へ歩き、
    /// 途中の小路を西へ折れ、ブリーディング・ハート・ヤードへ入る一本道。
    /// 迷う余地は作らない。形はすべてここに数値で置いてあり、作り直しても同じ物が出る
    /// </summary>
    public static class BuildAlley
    {
        public const string Materials = "Assets/Materials/Alley/";
        public const string Generated = "Assets/Models/generated/";

        // ---- 一本道の寸法。メートル ----------------------------------------

        /// <summary>通りの半幅。両側の建物はここに面が来る</summary>
        public const float StreetHalf = 4.5f;
        /// <summary>通りの手前の端。ここより南は建物で塞ぐ</summary>
        public const float StreetSouth = -5f;
        /// <summary>通りの奥の端。ここも塞いで、小路へ折れるほかない形にする</summary>
        public const float StreetNorth = 40f;

        /// <summary>小路の中心の z</summary>
        public const float LaneZ = 35f;
        /// <summary>小路の半幅</summary>
        public const float LaneHalf = 1.6f;
        /// <summary>小路の西の端。ここからヤード</summary>
        public const float LaneWest = -16f;

        public const float YardWest = -32f;
        public const float YardSouth = 26f;
        public const float YardNorth = 44f;

        /// <summary>建物の高さ。空はほとんど見えない</summary>
        public const float WallHeight = 14f;
        /// <summary>壁の厚み</summary>
        public const float WallThick = 1.2f;

        /// <summary>歩道の幅と高さ</summary>
        public const float KerbWidth = 1.1f;
        public const float KerbRise = 0.14f;

        [MenuItem("HalfAware/Build the alley")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            var root = Root("Alley");
            Street(Child(root, "Street"));
            Lane(Child(root, "Lane"));
            Yard(Child(root, "Yard"));
            Neon(Child(root, "Neon"));
            Market(Child(root, "Market"));
            Boards(Child(root, "Boards"));
            Rain();
            var temp = GameObject.Find("TempGround");
            if (temp != null) Object.DestroyImmediate(temp);
            Place(root);
            Selection.activeGameObject = root.gameObject;
            Mark(root.gameObject);
        }

        // ---- 通り ----------------------------------------------------------

        /// <summary>
        /// 通り。路面と歩道を敷き、両側に建物の面を立てる。
        /// 面は 1 枚にせず、奥行きと高さを振った塊を並べる。同じ壁が続くと歩いた実感が出ない
        /// </summary>
        static void Street(Transform parent)
        {
            Clear(parent);
            var length = StreetNorth - StreetSouth;
            var mid = (StreetNorth + StreetSouth) * 0.5f;
            Box(parent, "Road", new Vector3(0f, -0.05f, mid), new Vector3(StreetHalf * 2f, 0.1f, length), "Asphalt");
            Kerb(parent, "Kerb.W", -StreetHalf + KerbWidth * 0.5f, StreetSouth, StreetNorth);
            Kerb(parent, "Kerb.E", StreetHalf - KerbWidth * 0.5f, StreetSouth, StreetNorth);

            // 西側。小路の口だけ空ける
            Fronts(parent, "Front.W", -StreetHalf - WallThick * 0.5f, StreetSouth, LaneZ - LaneHalf, -1);
            Fronts(parent, "Front.W2", -StreetHalf - WallThick * 0.5f, LaneZ + LaneHalf, StreetNorth, -1);
            Fronts(parent, "Front.E", StreetHalf + WallThick * 0.5f, StreetSouth, StreetNorth, 1);

            // 行き止まり。北へ抜けさせない
            Box(parent, "Head", new Vector3(0f, WallHeight * 0.5f, StreetNorth + WallThick * 0.5f),
                new Vector3(StreetHalf * 2f + WallThick * 2f, WallHeight, WallThick), "Brick");
            // 振り返ったときの背。ここから来たことにする
            Box(parent, "Back", new Vector3(0f, WallHeight * 0.5f, StreetSouth - WallThick * 0.5f),
                new Vector3(StreetHalf * 2f + WallThick * 2f, WallHeight, WallThick), "Brick");
        }

        /// <summary>歩道。縁石ぶんだけ持ち上げた細長い箱</summary>
        static void Kerb(Transform parent, string name, float x, float from, float to)
        {
            var mid = (from + to) * 0.5f;
            Box(parent, name, new Vector3(x, KerbRise * 0.5f, mid),
                new Vector3(KerbWidth, KerbRise, to - from), "Kerb");
        }

        /// <summary>
        /// 建物の面を、奥行きと高さを振りながら並べる。
        /// side は通りのどちら側か。1 が東、-1 が西
        /// </summary>
        static void Fronts(Transform parent, string name, float x, float from, float to, int side)
        {
            var group = Child(parent, name);
            var rng = new System.Random(name.GetHashCode());
            var z = from;
            var i = 0;
            while (z < to - 0.01f)
            {
                var span = Mathf.Min((float)(5.5 + rng.NextDouble() * 4.5), to - z);
                var height = (float)(WallHeight * (0.72 + rng.NextDouble() * 0.28));
                var depth = (float)(WallThick * (0.8 + rng.NextDouble() * 0.9));
                var mid = z + span * 0.5f;
                Box(group, "Block" + i, new Vector3(x + side * (depth - WallThick) * 0.5f, height * 0.5f, mid),
                    new Vector3(depth, height, span), i % 2 == 0 ? "Brick" : "Facade");
                // 1 階の張り出し。庇と窓の下枠のぶん、面に段を作る
                Box(group, "Sill" + i, new Vector3(x - side * 0.35f, 1.9f, mid),
                    new Vector3(0.7f, 0.35f, span * 0.92f), "Ledge");
                z += span;
                i++;
            }
        }

        // ---- 小路 ----------------------------------------------------------

        /// <summary>通りから西へ折れる小路。人ひとりぶんの幅で、両側は高い壁</summary>
        static void Lane(Transform parent)
        {
            Clear(parent);
            var length = -StreetHalf - LaneWest;
            var mid = (LaneWest - StreetHalf) * 0.5f;
            Box(parent, "Road", new Vector3(mid, -0.05f, LaneZ), new Vector3(length, 0.1f, LaneHalf * 2f), "Asphalt");
            Box(parent, "Wall.S", new Vector3(mid, WallHeight * 0.5f, LaneZ - LaneHalf - WallThick * 0.5f),
                new Vector3(length, WallHeight, WallThick), "Brick");
            Box(parent, "Wall.N", new Vector3(mid, WallHeight * 0.5f, LaneZ + LaneHalf + WallThick * 0.5f),
                new Vector3(length, WallHeight, WallThick), "Brick");
            // 小路の天井。空を切って、抜けた先のヤードを明るく見せる
            Box(parent, "Arch", new Vector3(mid + length * 0.30f, 4.6f, LaneZ),
                new Vector3(length * 0.40f, 0.6f, LaneHalf * 2f + WallThick * 2f), "Brick");
        }

        // ---- ヤード --------------------------------------------------------

        /// <summary>
        /// ブリーディング・ハート・ヤード。四方を建物に囲まれた中庭で、
        /// 入口は小路の口ひとつだけ。奥（西）の端に自分の露店を置く
        /// </summary>
        static void Yard(Transform parent)
        {
            Clear(parent);
            var width = LaneWest - YardWest;
            var depth = YardNorth - YardSouth;
            var midX = (LaneWest + YardWest) * 0.5f;
            var midZ = (YardNorth + YardSouth) * 0.5f;
            Box(parent, "Ground", new Vector3(midX, -0.05f, midZ), new Vector3(width, 0.1f, depth), "Cobble");
            Box(parent, "Wall.W", new Vector3(YardWest - WallThick * 0.5f, WallHeight * 0.5f, midZ),
                new Vector3(WallThick, WallHeight, depth + WallThick * 2f), "Brick");
            Box(parent, "Wall.S", new Vector3(midX, WallHeight * 0.5f, YardSouth - WallThick * 0.5f),
                new Vector3(width, WallHeight, WallThick), "Brick");
            Box(parent, "Wall.N", new Vector3(midX, WallHeight * 0.5f, YardNorth + WallThick * 0.5f),
                new Vector3(width, WallHeight, WallThick), "Brick");
            // 東の壁は小路の口ぶんだけ空ける
            var southSpan = (LaneZ - LaneHalf) - YardSouth;
            Box(parent, "Wall.E.S", new Vector3(LaneWest + WallThick * 0.5f, WallHeight * 0.5f, YardSouth + southSpan * 0.5f),
                new Vector3(WallThick, WallHeight, southSpan), "Brick");
            var northSpan = YardNorth - (LaneZ + LaneHalf);
            Box(parent, "Wall.E.N", new Vector3(LaneWest + WallThick * 0.5f, WallHeight * 0.5f, YardNorth - northSpan * 0.5f),
                new Vector3(WallThick, WallHeight, northSpan), "Brick");
        }


        // ---- ネオン --------------------------------------------------------

        /// <summary>看板のひと枚。どちら側の壁に、どの高さで、面をどちらへ向けるか</summary>
        struct Plate
        {
            public string texture;
            public int side;      // 1 が東の壁、-1 が西の壁
            public float z;
            public float y;
            public bool blade;    // true なら壁から突き出して通りの上下を向く
            public float scale;   // 1 で幅 1.1 メートル

            public Plate(string texture, int side, float z, float y, bool blade, float scale)
            {
                this.texture = texture;
                this.side = side;
                this.z = z;
                this.y = y;
                this.blade = blade;
                this.scale = scale;
            }
        }

        /// <summary>看板の地の色。光の色をここから採る</summary>
        static Color NeonTint(string texture)
        {
            switch (texture)
            {
                case "NeonNerve": return new Color(1.00f, 0.16f, 0.67f);
                case "NeonChiba": return new Color(0.16f, 0.92f, 1.00f);
                case "NeonMemory": return new Color(1.00f, 0.59f, 0.12f);
                case "NeonJack": return new Color(0.27f, 1.00f, 0.47f);
                case "NeonRafu": return new Color(1.00f, 0.24f, 0.24f);
                case "NeonBar": return new Color(0.67f, 0.43f, 1.00f);
                case "NeonNoodle": return new Color(1.00f, 0.86f, 0.24f);
                case "NeonClinic": return new Color(0.35f, 0.78f, 1.00f);
                default: return Color.white;
            }
        }

        /// <summary>縦長の絵か。縦なら高さが幅の倍になる</summary>
        static bool Tall(string texture)
        {
            return texture == "NeonNerve" || texture == "NeonMemory"
                || texture == "NeonRafu" || texture == "NeonBar";
        }

        /// <summary>
        /// 通りのネオン。壁に貼る物と、突き出して通りの上下を向く物を混ぜる。
        /// 突き出した物には灯りを付ける。濡れた路面に色が落ちて、通りが極彩色になる
        /// </summary>
        static void Neon(Transform parent)
        {
            Clear(parent);
            var plates = new List<Plate>
            {
                new Plate("NeonNerve",  -1,  2.5f, 4.6f, true,  1.35f),
                new Plate("NeonChiba",   1,  5.0f, 5.4f, false, 1.60f),
                new Plate("NeonNoodle",  1,  8.5f, 3.9f, true,  1.30f),
                new Plate("NeonRafu",   -1, 11.5f, 6.2f, false, 1.25f),
                new Plate("NeonJack",   -1, 14.5f, 4.1f, true,  1.45f),
                new Plate("NeonBar",     1, 17.0f, 7.0f, false, 1.30f),
                new Plate("NeonMemory",  1, 20.5f, 4.4f, true,  1.35f),
                new Plate("NeonClinic", -1, 23.5f, 5.8f, false, 1.55f),
                new Plate("NeonChiba",  -1, 26.5f, 3.8f, true,  1.40f),
                new Plate("NeonNerve",   1, 29.0f, 6.6f, false, 1.20f),
                new Plate("NeonBar",    -1, 31.5f, 4.5f, true,  1.30f),
                new Plate("NeonNoodle",  1, 33.5f, 5.0f, true,  1.35f),
                new Plate("NeonMemory", -1, 37.5f, 6.0f, false, 1.25f),
                new Plate("NeonJack",    1, 38.0f, 3.9f, true,  1.40f),
            };
            for (var i = 0; i < plates.Count; i++) Sign(parent, "Sign" + i, plates[i]);
            Tubes(Child(parent, "Tubes"));
            Windows(Child(parent, "Windows"));
        }

        /// <summary>
        /// 面に這わせる管。看板のあいだを繋いで、壁そのものを光らせる。
        /// 「熱帯植物のように絡みついている」のはこちらの仕事で、看板だけでは足りない
        /// </summary>
        static void Tubes(Transform parent)
        {
            Clear(parent);
            var tint = new Color[]
            {
                new Color(1.00f, 0.16f, 0.67f), new Color(0.16f, 0.92f, 1.00f),
                new Color(1.00f, 0.59f, 0.12f), new Color(0.27f, 1.00f, 0.47f),
                new Color(0.67f, 0.43f, 1.00f), new Color(1.00f, 0.86f, 0.24f),
            };
            var rng = new System.Random(20260919);
            var n = 0;
            for (var s2 = 0; s2 < 2; s2++)
            {
                var side = s2 == 0 ? -1 : 1;
                var x = side * (StreetHalf - 0.06f);
                var z = StreetSouth + 1f;
                while (z < StreetNorth - 2f)
                {
                    var span = (float)(3.0 + rng.NextDouble() * 5.0);
                    var y = (float)(2.2 + rng.NextDouble() * 8.0);
                    var col = tint[rng.Next(tint.Length)];
                    // 横に長く這う管
                    Strip(parent, "Tube" + n++, new Vector3(x, y, z + span * 0.5f),
                        new Vector3(0.06f, 0.09f, span), col);
                    // ときどき縦へ折れる
                    if (rng.NextDouble() < 0.45)
                    {
                        var drop = (float)(1.5 + rng.NextDouble() * 3.5);
                        Strip(parent, "Tube" + n++, new Vector3(x, y - drop * 0.5f, z + span),
                            new Vector3(0.06f, drop, 0.09f), col);
                    }
                    z += span + (float)(0.8 + rng.NextDouble() * 2.5);
                }
            }
        }

        /// <summary>光る帯をひとつ。細い箱に自発光のマテリアルを貼るだけ</summary>
        static void Strip(Transform parent, string name, Vector3 centre, Vector3 size, Color col)
        {
            var go = Box(parent, name, centre, size, "Ledge");
            go.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(col, 1.9f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// 建物の窓。上の方は暗い板のままだと書き割りに見えるので、
        /// 灯りの点いた窓をまばらに入れる。1 面ぶんを 1 枚の mesh にまとめて軽くする
        /// </summary>
        static void Windows(Transform parent)
        {
            Clear(parent);
            for (var s2 = 0; s2 < 2; s2++)
            {
                var side = s2 == 0 ? -1 : 1;
                var x = side * (StreetHalf - 0.05f);
                var warm = new List<Vector3>();
                var cold = new List<Vector3>();
                var rng = new System.Random(4000 + s2);
                for (var z = StreetSouth + 1.6f; z < StreetNorth - 1.2f; z += 1.9f)
                {
                    for (var y = 3.4f; y < WallHeight - 1.2f; y += 2.3f)
                    {
                        var roll = rng.NextDouble();
                        if (roll < 0.52) continue;
                        (roll < 0.80 ? warm : cold).Add(new Vector3(x, y, z));
                    }
                }
                Pane(parent, "Window.Warm." + s2, warm, side, new Color(1.00f, 0.72f, 0.38f), 0.55f);
                Pane(parent, "Window.Cold." + s2, cold, side, new Color(0.46f, 0.66f, 1.00f), 0.50f);
            }
        }

        /// <summary>窓を並べた 1 枚の板。面は通りの中央を向く</summary>
        static void Pane(Transform parent, string name, List<Vector3> at, int side, Color col, float glow)
        {
            if (at.Count == 0) return;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var w = 0.62f;
            var h = 0.95f;
            foreach (var c in at)
            {
                var i = verts.Count;
                verts.Add(new Vector3(c.x, c.y - h * 0.5f, c.z - w * 0.5f));
                verts.Add(new Vector3(c.x, c.y + h * 0.5f, c.z - w * 0.5f));
                verts.Add(new Vector3(c.x, c.y + h * 0.5f, c.z + w * 0.5f));
                verts.Add(new Vector3(c.x, c.y - h * 0.5f, c.z + w * 0.5f));
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                if (side < 0)
                {
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
                else
                {
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
                }
            }
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ProcMesh.Save(mesh, Generated + name.Replace('.', '_') + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Generated + name.Replace('.', '_') + ".asset");
            go.AddComponent<MeshRenderer>().sharedMaterial = GlowMat(col, glow);
        }

        /// <summary>色だけの自発光マテリアル。同じ色は使い回す</summary>
        static Material GlowMat(Color col, float glow)
        {
            var key = string.Format("Glow_{0:000}_{1:000}_{2:000}_{3:00}",
                (int)(col.r * 255), (int)(col.g * 255), (int)(col.b * 255), (int)(glow * 10));
            var path = Materials + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                AssetDatabase.CreateFolder("Assets/Materials", "Alley");
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.name = key;
            m.SetColor("_BaseColor", new Color(col.r * glow, col.g * glow, col.b * glow, 1f));
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>看板を 1 枚立てる。突き出す物には壁までの腕と灯りを足す</summary>
        static void Sign(Transform parent, string name, Plate p)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + p.texture + ".png");
            if (tex == null) { Debug.LogWarning("テクスチャが無い: " + p.texture); return; }
            var wide = Tall(p.texture) ? p.scale : p.scale * 2f;
            var high = Tall(p.texture) ? p.scale * 2f : p.scale;
            var wallX = p.side * StreetHalf;
            var reach = p.blade ? 0.95f : 0.12f;
            var x = wallX - p.side * reach;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, p.y, p.z);
            // 突き出す物は通りの上下を向き、貼る物は通りの中央を向く
            go.transform.localRotation = p.blade
                ? Quaternion.identity
                : Quaternion.Euler(0f, p.side > 0 ? 90f : -90f, 0f);
            go.transform.localScale = new Vector3(wide, high, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = NeonMat(p.texture, tex);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            if (!p.blade) return;
            // 壁まで繋ぐ腕
            Box(parent, name + ".Arm", new Vector3(wallX - p.side * reach * 0.5f, p.y + high * 0.5f - 0.1f, p.z),
                new Vector3(reach, 0.08f, 0.08f), "Ledge");
            // 通りへ落ちる色。突き出した物にだけ付ける
            var lamp = new GameObject(name + ".Lamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(x - p.side * 0.5f, p.y, p.z);
            var l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = NeonTint(p.texture);
            l.range = 11f;
            l.intensity = 7.5f;
            l.shadows = LightShadows.None;
        }

        /// <summary>
        /// 自分で光る看板のマテリアル。足し算で重ねるので、滲みが背景へそのまま乗る。
        /// 裏からも見えるように面の切り落としは切ってある
        /// </summary>
        static Material NeonMat(string texture, Texture2D tex)
        {
            var path = Materials + "Neon_" + texture + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Neon_" + texture;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.7f, 1.7f, 1.7f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }


        // ---- 蚤の市 --------------------------------------------------------

        /// <summary>歩ける筋の半幅。ここだけは何も置かない</summary>
        public const float AisleHalf = 1.5f;
        /// <summary>自分の露店の場所。いちばん奥</summary>
        public const float MyStallX = -29.3f;

        /// <summary>
        /// ヤードの中身。両脇に出店を詰め、真ん中に人ひとりぶんの筋だけ残す。
        /// 足の置き場もない、という文に合わせて隙間は詰める
        /// </summary>
        static void Market(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(6100);
            var n = 0;
            for (var s2 = 0; s2 < 2; s2++)
            {
                var sideZ = s2 == 0 ? -1 : 1;
                for (var row = 0; row < 2; row++)
                {
                    var z = LaneZ + sideZ * (AisleHalf + 1.35f + row * 3.1f);
                    for (var x = LaneWest - 2.6f; x > MyStallX + 1.6f; x -= 3.2f)
                    {
                        Stall(parent, "Stall" + n++, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.5f, 0f, z),
                            sideZ > 0 ? 180f : 0f, rng, false);
                    }
                }
            }
            MyStall(Child(parent, "MyStall"));
            Bulbs(Child(parent, "Bulbs"));
        }

        /// <summary>
        /// 出店ひとつ。4 本の柱にタープを張り、テーブルと木箱を置く。
        /// yaw は売り手が向く向きで、テーブルは筋の側へ出る
        /// </summary>
        static void Stall(Transform parent, string name, Vector3 at, float yaw, System.Random rng, bool mine)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = g.transform;
            var w = mine ? 2.6f : (float)(2.1 + rng.NextDouble() * 0.6);
            var d = mine ? 2.2f : (float)(1.7 + rng.NextDouble() * 0.5);
            var high = mine ? 2.25f : (float)(1.95 + rng.NextDouble() * 0.25);

            for (var i = 0; i < 4; i++)
            {
                var px = (i % 2 == 0 ? -1 : 1) * w * 0.5f;
                var pz = (i < 2 ? -1 : 1) * d * 0.5f;
                Box(t, "Pole" + i, new Vector3(px, high * 0.5f, pz), new Vector3(0.06f, high, 0.06f), "Pole");
            }

            var tarp = mine ? "TarpMine" : "Tarp";
            if (mine)
            {
                // 自分のぶんは穴が開いている。3 枚に割って、真ん中を空ける
                Sheet(t, "Tarp.N", new Vector3(0f, high, -d * 0.28f), new Vector2(w + 0.3f, d * 0.40f), tarp);
                Sheet(t, "Tarp.S", new Vector3(0f, high, d * 0.30f), new Vector2(w + 0.3f, d * 0.36f), tarp);
                Sheet(t, "Tarp.W", new Vector3(-w * 0.34f, high, 0f), new Vector2(w * 0.30f, d * 0.30f), tarp);
            }
            else
            {
                Sheet(t, "Tarp", new Vector3(0f, high, 0f), new Vector2(w + 0.3f, d + 0.3f), tarp);
            }

            var th = 0.78f;
            Box(t, "Table", new Vector3(0f, th, -d * 0.18f), new Vector3(w * 0.88f, 0.06f, d * 0.52f), "Timber");
            Box(t, "Skirt", new Vector3(0f, th * 0.5f, -d * 0.18f - d * 0.24f), new Vector3(w * 0.88f, th, 0.05f), "Timber");

            var boxes = mine ? 2 : 1 + rng.Next(3);
            for (var i = 0; i < boxes; i++)
            {
                var bw = (float)(0.35 + rng.NextDouble() * 0.2);
                Box(t, "Crate" + i, new Vector3((float)(rng.NextDouble() - 0.5) * w * 0.7f, bw * 0.5f,
                        d * 0.30f + (float)(rng.NextDouble() - 0.5) * 0.3f),
                    new Vector3(bw, bw, bw), "Crate");
            }
        }

        /// <summary>タープの一枚。水平に張った薄い板</summary>
        static void Sheet(Transform parent, string name, Vector3 centre, Vector2 size, string material)
        {
            Box(parent, name, centre, new Vector3(size.x, 0.04f, size.y), material);
        }

        /// <summary>
        /// 自分の露店。穴の開いたタープの下に、テーブルと椅子と看板がひとつ。
        /// いちばん奥に、入口を向いて構える
        /// </summary>
        static void MyStall(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(77);
            Stall(parent, "Stall", new Vector3(MyStallX, 0f, LaneZ), 90f, rng, true);
            var t = parent.Find("Stall");
            Box(t, "Chair.Seat", new Vector3(0f, 0.44f, 0.72f), new Vector3(0.44f, 0.06f, 0.44f), "Timber");
            Box(t, "Chair.Back", new Vector3(0f, 0.70f, 0.94f), new Vector3(0.44f, 0.52f, 0.05f), "Timber");
            for (var i = 0; i < 4; i++)
            {
                var px = (i % 2 == 0 ? -1 : 1) * 0.18f;
                var pz = 0.72f + (i < 2 ? -0.18f : 0.18f);
                Box(t, "Chair.Leg" + i, new Vector3(px, 0.22f, pz), new Vector3(0.04f, 0.44f, 0.04f), "Pole");
            }
        }

        /// <summary>ヤードの裸電球。出店ごとには置かず、まばらに吊る</summary>
        static void Bulbs(Transform parent)
        {
            Clear(parent);
            var at = new Vector3[]
            {
                new Vector3(LaneWest - 3.2f, 3.1f, LaneZ + 0.4f),
                new Vector3(LaneWest - 8.0f, 3.3f, LaneZ - 2.6f),
                new Vector3(LaneWest - 8.6f, 3.2f, LaneZ + 3.0f),
                new Vector3(MyStallX + 2.4f, 3.1f, LaneZ - 0.6f),
                new Vector3(MyStallX + 0.2f, 2.5f, LaneZ + 0.2f),
            };
            for (var i = 0; i < at.Length; i++)
            {
                var g = new GameObject("Bulb" + i);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = at[i];
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Glass";
                ball.transform.SetParent(g.transform, false);
                ball.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
                ball.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(new Color(1.00f, 0.80f, 0.52f), 2.2f);
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.00f, 0.79f, 0.52f);
                l.range = 9f;
                l.intensity = 5.5f;
                l.shadows = LightShadows.None;
            }
        }

        // ---- 表示板 --------------------------------------------------------

        /// <summary>小路の口の表示板と、自分の露店の看板。どちらも光らない板</summary>
        static void Boards(Transform parent)
        {
            Clear(parent);
            Board(parent, "SignYardName", "SignYardName", new Vector3(-StreetHalf + 0.10f, 2.9f, LaneZ - 2.4f),
                Quaternion.Euler(0f, -90f, 0f), new Vector2(1.9f, 0.6f));
            Board(parent, "SignYardName.Lane", "SignYardName", new Vector3(LaneWest + 0.6f, 2.9f, LaneZ - LaneHalf + 0.08f),
                Quaternion.identity, new Vector2(1.9f, 0.6f));
            Board(parent, "SignMemories", "SignMemories", new Vector3(MyStallX - 0.55f, 1.62f, LaneZ),
                Quaternion.Euler(0f, -90f, 0f), new Vector2(1.5f, 0.75f));
        }

        /// <summary>板を 1 枚立てる。読ませるためではなく、そこに何があるかを示すために置く</summary>
        static void Board(Transform parent, string name, string texture, Vector3 at, Quaternion rot, Vector2 size)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + texture + ".png");
            if (tex == null) { Debug.LogWarning("テクスチャが無い: " + texture); return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = BoardMat(texture, tex);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// 板のマテリアル。灯りを受ける Lit だと絵が出なかったので unlit で貼り、
        /// 明るさは色で落としてある。暗い路地で読める程度に留める
        /// </summary>
        static Material BoardMat(string texture, Texture2D tex)
        {
            var path = Materials + "Board_" + texture + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit")
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                if (m != null) AssetDatabase.DeleteAsset(path);
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Board_" + texture;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }


        // ---- 雨 ------------------------------------------------------------

        /// <summary>
        /// 雨。歩く先へ付いてくるよう、粒はプレイヤーの上から降らせる。
        /// 粒そのものは世界の座標で動かすので、走っても雨が斜めに固まらない。
        /// 屋根の下でも降り込むが、1/3 の解像度では気にならない
        /// </summary>
        static void Rain()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            var old = player.transform.Find("Rain");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("Rain");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 9f, 2.5f);
            // 箱の面から真下へ吐かせる
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8.5f, 11.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.020f, 0.038f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.70f, 0.82f, 0.22f), new Color(0.78f, 0.84f, 0.95f, 0.42f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.35f);

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 620f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 17f, 0.2f);

            // 風。まっすぐ落ちる雨は書き割りに見える
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.55f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = RainMat();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3.6f;
            r.velocityScale = 0.06f;
            r.cameraVelocityScale = 0f;
            r.sortMode = ParticleSystemSortMode.None;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>雨粒のマテリアル。煙と同じ柔らかい絵を、細く引き伸ばして筋にする</summary>
        static Material RainMat()
        {
            var path = Materials + "Rain.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                AssetDatabase.CreateFolder("Assets/Materials", "Alley");
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.name = "Rain";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SmokePuff.png");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.1f, 1.15f, 1.25f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ---- 置き方 --------------------------------------------------------

        /// <summary>立ち位置。通りの入口に、北を向いて立たせる</summary>
        static void Place(Transform root)
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0f, 0.05f, StreetSouth + 2.5f);
            player.transform.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }

        // ---- 道具 ----------------------------------------------------------

        static Transform Root(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>箱をひとつ置く。壁も路面もこれで足りる</summary>
        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(material);
            return go;
        }

        /// <summary>色だけ決めたマテリアル。無ければ作って残す</summary>
        static Material Mat(string name)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                AssetDatabase.CreateFolder("Assets/Materials", "Alley");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader);
            m.name = name;
            Color col;
            float smooth;
            Tone(name, out col, out smooth);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>素材ごとの色と艶。雨に濡れているので路面だけ強く光らせる</summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                case "Asphalt": col = new Color(0.055f, 0.060f, 0.075f); smooth = 0.66f; break;
                case "Cobble": col = new Color(0.075f, 0.075f, 0.085f); smooth = 0.46f; break;
                case "Kerb": col = new Color(0.115f, 0.115f, 0.125f); smooth = 0.55f; break;
                case "Brick": col = new Color(0.105f, 0.085f, 0.085f); smooth = 0.18f; break;
                case "Facade": col = new Color(0.085f, 0.090f, 0.105f); smooth = 0.22f; break;
                case "Ledge": col = new Color(0.135f, 0.130f, 0.130f); smooth = 0.20f; break;
                case "Tarp": col = new Color(0.150f, 0.145f, 0.130f); smooth = 0.30f; break;
                case "TarpMine": col = new Color(0.135f, 0.115f, 0.100f); smooth = 0.30f; break;
                case "Timber": col = new Color(0.130f, 0.105f, 0.080f); smooth = 0.15f; break;
                case "Pole": col = new Color(0.090f, 0.090f, 0.095f); smooth = 0.42f; break;
                case "Crate": col = new Color(0.105f, 0.090f, 0.072f); smooth = 0.12f; break;
                default: col = new Color(0.12f, 0.12f, 0.13f); smooth = 0.3f; break;
            }
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
