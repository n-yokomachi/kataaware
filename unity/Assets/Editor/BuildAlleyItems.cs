using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の「調べる対象」と、売り買いに出てくる物を立てる。
    /// 通りの板、小路の口の表示板、自分の露店の看板とテーブル、
    /// テーブルに並べるチップ、買い手が置いていく煙草、買い手そのもの、売るときに立つ場所。
    ///
    /// 位置はここ（シーン）、文はアセット（WriteAlleyScript）、
    /// 読み順と売り買いの進行は AlleyDirector が持つ。BuildAlley から呼ぶ
    /// </summary>
    public static class BuildAlleyItems
    {
        const string ScriptPath = "Assets/Data/AlleyScript.asset";

        /// <summary>
        /// 板を読める距離。判定は 3D の距離と、視線から 0.7 rad の円錐で取る。
        /// 板は目の高さに掛かっているので、近づいて向けば拾える
        /// </summary>
        const float SignRadius = 5.5f;
        const float BoardRadius = 3.2f;
        const float TableRadius = 2.6f;

        /// <summary>板の判定点を、読む人の居る側へ引き出す距離</summary>
        const float ReadReach = 0.75f;

        public static void Build(Transform root)
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
            {
                Debug.LogWarning("場面 2 の文面が無い。先に HalfAware/Write the alley script を走らせる");
                return;
            }
            var parent = Child(root, "Items");
            Clear(parent);

            var signs = 0;
            for (var i = 0; i < BuildAlley.StreetSignCount && i < WriteAlleyScript.Signs; i++)
            {
                var board = Find(root, "Boards/StreetSign" + i);
                if (board == null) continue;
                Put(parent, "Sign" + i, ReadFrom(board), script, AlleyIds.Sign(i), SignRadius, false, false);
                signs++;
            }

            var yardBoard = Find(root, "Boards/SignYardName");
            if (yardBoard != null) Put(parent, "YardBoard", ReadFrom(yardBoard), script, AlleyIds.Board, BoardRadius, false, true);

            var stallSign = Find(root, "Boards/SignMemories");
            if (stallSign != null) Put(parent, "StallSign", ReadFrom(stallSign), script, AlleyIds.StallSign, BoardRadius, false, true);

            Stall(parent, script);

            Wire(root, script);
            Debug.Log(string.Format("場面 2 の対象を立てた。通りの板 {0} 枚、チップ {1} 枚、煙草 {2} 個、買い手 {3} 人",
                signs, MarketSale.Chips, Smokes.Length, Buyers.Length));
        }

        /// <summary>
        /// 板の判定点。板は壁に貼ってあるので、読む人の居る側へ少し引き出す。
        /// BuildAlley の Board は絵の乗る面を -forward に向けているので、そちらが読む側
        /// </summary>
        static Vector3 ReadFrom(Transform board)
        {
            return board.position - board.forward * ReadReach;
        }

        /// <summary>自分の露店。テーブル・チップ・煙草・買い手・売るときに立つ場所</summary>
        static void Stall(Transform parent, RoomScript script)
        {
            var yaw = BuildAlley.MyStallYaw;
            var spin = Quaternion.Euler(0f, yaw, 0f);
            var at = new Vector3(BuildAlley.MyStallX, 0f, BuildAlley.MyStallZ);

            // テーブルの天面は 0.81。手前の縁の少し上に置く。
            // 天面に合わせるとピンが卓に埋まって見えない
            Put(parent, "Table", at + spin * new Vector3(0f, 1.06f, -0.62f),
                script, AlleyIds.Table, TableRadius, true, true);

            var chips = Child(parent, "Chips");
            Chips = new GameObject[MarketSale.Chips];
            for (var i = 0; i < Chips.Length; i++)
            {
                var slot = (i - (Chips.Length - 1) * 0.5f) * 0.098f;
                Chips[i] = Chip(chips, "Chip" + i,
                    at + spin * new Vector3(slot, 0.812f, -0.40f),
                    spin * Quaternion.Euler(0f, i % 2 == 0 ? 6f : -7f, 0f));
            }

            // 買い手 B が置いていく煙草。卓の向こう寄り、買い手の手が届くあたり
            var smokes = Child(parent, "Smokes");
            Smokes = new GameObject[3];
            for (var i = 0; i < Smokes.Length; i++)
            {
                var slot = (i - (Smokes.Length - 1) * 0.5f) * 0.075f;
                Smokes[i] = Pack(smokes, "Smoke" + i,
                    at + spin * new Vector3(slot + 0.30f, 0.822f, 0.02f - i * 0.03f),
                    spin * Quaternion.Euler(0f, -22f + i * 19f, 0f));
            }

            Buyers = MakeBuyers(Child(parent, "Buyers"), at, spin, yaw);

            // 売るときに立つ場所。テーブルの向こう、店の内側
            var spot = Child(parent, "SellSpot");
            spot.position = at + spin * new Vector3(0f, 0f, 0.95f);
            spot.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            SellSpot = spot;
        }

        static GameObject[] Chips;
        static GameObject[] Smokes;
        static GameObject[] Buyers;
        static Transform SellSpot;

        /// <summary>
        /// 買い手。卓の手前に立ち、店の方を向く。
        ///
        /// 6.4 のとおり 1 人目と 2 人目は男、3 人目は女。
        /// 色は群衆より濃くして、後ろの人だかりから浮かせる。
        /// はじめは伏せておき、その買い手の番だけ AlleyDirector が出す
        /// </summary>
        static GameObject[] MakeBuyers(Transform parent, Vector3 at, Quaternion spin, float yaw)
        {
            // 卓の前面はここから -z の側。買い手はそちらに立って、店を向く
            var front = spin * new Vector3(0f, 0f, -1f);
            var mat = BuildAlley.BuyerMat();
            var made = new GameObject[MarketSale.Count];
            var men = new[] { "M_Suit", "M_Worker" };
            var women = new[] { "W_Formal", "W_Casual" };
            var poses = new[] { 1, 2, 0 };
            var sway = new[] { -0.12f, 0.10f, -0.04f };
            var man = 0;
            var lady = 0;
            for (var i = 0; i < made.Length; i++)
            {
                var woman = MarketSale.Woman(i);
                var model = woman ? women[lady++ % women.Length] : men[man++ % men.Length];
                // 卓の前端は露店の中心から 0.98 m、体が入るのは 1.23 m から。
                // 買い手は縁から少し下がって立つ
                var spot = at + front * 1.55f + spin * new Vector3(sway[i], 0f, 0f);
                spot.y = Ground(spot);
                var go = BuildAlley.BakeOne(parent, "Buyer" + i, model,
                    spot, yaw, poses[i % poses.Length], Vector3.one, mat);
                if (go == null) continue;
                go.SetActive(false);
                made[i] = go;
            }
            return made;
        }

        /// <summary>そこの床の高さ。中庭の敷石は 0.02 で、素のままだと足が沈む</summary>
        static float Ground(Vector3 at)
        {
            RaycastHit hit;
            return Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out hit, 6f) ? hit.point.y : at.y;
        }

        /// <summary>
        /// メモリーチップ 1 枚。
        ///
        /// 寸法もマテリアルも自室のラックに差さっているものと揃える。
        /// 向こうは 15 × 46 × 34 mm の小さな板で、立てて差してある。
        /// こちらは卓に平らに置くので、同じ板を寝かせて札を上に向ける
        /// </summary>
        static GameObject Chip(Transform parent, string name, Vector3 at, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = rot;
            Part(go.transform, "Body", new Vector3(0f, 0.0075f, 0f), new Vector3(0.046f, 0.015f, 0.034f), ChipMat());
            Part(go.transform, "Label", new Vector3(0.013f, 0.0160f, 0f), new Vector3(0.010f, 0.002f, 0.026f), LabelMat());
            go.SetActive(false);
            return go;
        }

        /// <summary>煙草 1 箱。自室にあるものと同じ絵を巻く</summary>
        static GameObject Pack(Transform parent, string name, Vector3 at, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = rot;
            Part(go.transform, "Box", new Vector3(0f, 0.011f, 0f), new Vector3(0.056f, 0.022f, 0.086f), PackMat());
            go.SetActive(false);
            return go;
        }

        /// <summary>部品ひとつ。当たり判定は要らないので落とす</summary>
        static void Part(Transform parent, string name, Vector3 offset, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>AlleyDirector を SceneFlow の隣に置いて、繋ぎ直す</summary>
        static void Wire(Transform root, RoomScript script)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogWarning("SceneFlow が見つからない。AlleyDirector を繋げない"); return; }
            var director = flow.GetComponent<AlleyDirector>();
            if (director == null) director = flow.gameObject.AddComponent<AlleyDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>();
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("sellSpot").objectReferenceValue = SellSpot;
            Fill(so, "chips", Chips);
            Fill(so, "buyers", Buyers);
            Fill(so, "smokes", Smokes);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        static void Fill(SerializedObject so, string field, GameObject[] made)
        {
            var list = so.FindProperty(field);
            if (list == null) { Debug.LogWarning("AlleyDirector に " + field + " が無い"); return; }
            list.arraySize = made == null ? 0 : made.Length;
            for (var i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = made[i];
        }

        /// <summary>調べる対象をひとつ立てる</summary>
        static void Put(Transform parent, string name, Vector3 at, RoomScript script,
            string id, float radius, bool required, bool once)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            // Interactable の OnValidate は AddComponent の中で走るので、
            // ここで「id がない」と一度警告が出る。id はこの直後に入れている
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("required").boolValue = required;
            so.FindProperty("once").boolValue = once;
            so.FindProperty("after").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Solid(string name, Color colour, float smooth, float metal)
        {
            var path = "Assets/Materials/Alley/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>チップの本体。自室のラックと同じ物を使う</summary>
        static Material ChipMat()
        {
            return Shared("Assets/Materials/Room/SteelDark.mat", "Chip",
                new Color(0.115f, 0.125f, 0.145f), 0.62f, 0.35f);
        }

        /// <summary>貼った札。これも自室と同じ</summary>
        static Material LabelMat()
        {
            return Shared("Assets/Materials/Room/Steel.mat", "ChipLabel",
                new Color(0.560f, 0.545f, 0.490f), 0.18f, 0f);
        }

        /// <summary>あれば自室のものを使う。無ければ同じ色で作る</summary>
        static Material Shared(string path, string fallback, Color colour, float smooth, float metal)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Debug.LogWarning("自室のマテリアルが無い: " + path);
            return Solid(fallback, colour, smooth, metal);
        }

        /// <summary>煙草の箱。自室で使っている絵をそのまま巻く</summary>
        static Material PackMat()
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/CigarettePack.mat");
            if (shared != null) return shared;
            Debug.LogWarning("自室の煙草のマテリアルが無い。無地で置く");
            return Solid("Smoke", new Color(0.320f, 0.180f, 0.155f), 0.24f, 0f);
        }

        static Transform Find(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null) Debug.LogWarning("見つからない: " + path);
            return t;
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
    }
}
