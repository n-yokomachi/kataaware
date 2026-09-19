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
                case "Asphalt": col = new Color(0.055f, 0.060f, 0.075f); smooth = 0.82f; break;
                case "Cobble": col = new Color(0.075f, 0.075f, 0.085f); smooth = 0.68f; break;
                case "Kerb": col = new Color(0.115f, 0.115f, 0.125f); smooth = 0.55f; break;
                case "Brick": col = new Color(0.105f, 0.085f, 0.085f); smooth = 0.18f; break;
                case "Facade": col = new Color(0.085f, 0.090f, 0.105f); smooth = 0.22f; break;
                case "Ledge": col = new Color(0.135f, 0.130f, 0.130f); smooth = 0.20f; break;
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
