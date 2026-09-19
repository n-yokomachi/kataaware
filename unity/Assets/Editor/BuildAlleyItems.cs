using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の「調べる対象」を立てる。通りの看板、小路の口の表示板、
    /// 自分の露店の看板とテーブル、テーブルに並べるチップ、売るときに立つ場所。
    ///
    /// 位置はここ（シーン）、文はアセット（WriteAlleyScript）、
    /// 読み順と売り買いは AlleyDirector が持つ。BuildAlley から呼ぶ
    /// </summary>
    public static class BuildAlleyItems
    {
        const string ScriptPath = "Assets/Data/AlleyScript.asset";

        /// <summary>
        /// 看板を読める距離。判定は 3D の距離と、視線から 0.7 rad の円錐で取る。
        /// ネオンそのものは頭上 4〜10 m に出ているので、そこへ判定点を置くと
        /// 真下に立っても仰角が円錐から外れて読めない。判定点は目線の高さに下ろし、
        /// 看板の真下あたりで壁の方を向けば拾えるようにする
        /// </summary>
        const float SignRadius = 7.5f;

        /// <summary>看板の判定点を置く高さ。立っているときの目線に合わせる</summary>
        const float SignEyeLevel = PlayerController.StandingEyeHeight;
        const float BoardRadius = 3.2f;
        const float TableRadius = 2.6f;

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

            var signs = Signs(root);
            for (var i = 0; i < signs.Count; i++)
                Put(parent, "Sign" + i, signs[i], script, AlleyIds.Sign(i), SignRadius, false, false);

            var board = Find(root, "Boards/SignYardName");
            if (board != null) Put(parent, "YardBoard", board.position, script, AlleyIds.Board, BoardRadius, false, true);

            var stallSign = Find(root, "Boards/SignMemories");
            if (stallSign != null) Put(parent, "StallSign", stallSign.position, script, AlleyIds.StallSign, BoardRadius, false, true);

            Stall(parent, root, script);

            Wire(root, script, parent);
            Debug.Log(string.Format("場面 2 の対象を立てた。看板 {0} 枚、表示板と露店の看板とテーブル", signs.Count));
        }

        /// <summary>
        /// 通りの看板。歩ける範囲に出ているネオンから、間を空けて選ぶ。
        /// どれから読んでも同じ列から引くので、どれを選んでも筋は通る
        /// </summary>
        static List<Vector3> Signs(Transform root)
        {
            var spots = new List<Vector3>();
            var neon = root.Find("Neon");
            if (neon == null) return spots;
            var found = new List<Transform>();
            foreach (Transform t in neon)
            {
                if (!t.name.StartsWith("Sign")) continue;
                if (t.name.EndsWith(".Back")) continue;          // 裏面は数えない
                var z = t.position.z;
                if (z < BuildAlley.WalkSouth + 1f || z > BuildAlley.LaneZ - 1f) continue;
                found.Add(t);
            }
            found.Sort(delegate (Transform a, Transform b) { return a.position.z.CompareTo(b.position.z); });
            var want = WriteAlleyScript.Signs;
            if (found.Count == 0 || want == 0) return spots;
            for (var i = 0; i < want; i++)
            {
                // 端に寄らないよう、通りを等分した位置から拾う
                var k = Mathf.RoundToInt((found.Count - 1) * (i + 0.5f) / want);
                var at = found[Mathf.Clamp(k, 0, found.Count - 1)].position;
                // 壁の位置はそのまま、高さだけ目線に下ろす
                spots.Add(new Vector3(at.x, SignEyeLevel, at.z));
            }
            return spots;
        }

        /// <summary>自分の露店。テーブルとチップと、売るときに立つ場所</summary>
        static void Stall(Transform parent, Transform root, RoomScript script)
        {
            var yaw = BuildAlley.MyStallYaw;
            var spin = Quaternion.Euler(0f, yaw, 0f);
            var at = new Vector3(BuildAlley.MyStallX, 0f, BuildAlley.MyStallZ);

            // テーブルの天面は 0.81。手前の縁あたりを調べさせる
            var table = at + spin * new Vector3(0f, 0.85f, -0.55f);
            Put(parent, "Table", table, script, AlleyIds.Table, TableRadius, true, true);

            // 並べるチップ。はじめは伏せておき、置いたときに出す
            var chips = Child(parent, "Chips");
            var made = new GameObject[MarketSale.Chips];
            for (var i = 0; i < made.Length; i++)
            {
                var slot = (i - (made.Length - 1) * 0.5f) * 0.17f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Chip" + i;
                go.transform.SetParent(chips, false);
                go.transform.position = at + spin * new Vector3(slot, 0.845f, -0.42f);
                go.transform.rotation = spin * Quaternion.Euler(0f, (i % 2 == 0 ? 4f : -5f), 0f);
                go.transform.localScale = new Vector3(0.115f, 0.014f, 0.078f);
                go.GetComponent<MeshRenderer>().sharedMaterial = ChipMat();
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.SetActive(false);
                made[i] = go;
            }

            // 売るときに立つ場所。テーブルの向こう、店の内側
            var spot = Child(parent, "SellSpot");
            spot.position = at + spin * new Vector3(0f, 0f, 0.95f);
            spot.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);

            Chips = made;
            SellSpot = spot;
        }

        static GameObject[] Chips;
        static Transform SellSpot;

        /// <summary>AlleyDirector を SceneFlow の隣に置いて、繋ぎ直す</summary>
        static void Wire(Transform root, RoomScript script, Transform parent)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogWarning("SceneFlow が見つからない。AlleyDirector を繋げない"); return; }
            var director = flow.GetComponent<AlleyDirector>();
            if (director == null) director = flow.gameObject.AddComponent<AlleyDirector>();
            var hud = Object.FindFirstObjectByType<HudView>();
            var player = Object.FindFirstObjectByType<PlayerController>();

            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("sellSpot").objectReferenceValue = SellSpot;
            var list = so.FindProperty("chips");
            list.arraySize = Chips == null ? 0 : Chips.Length;
            for (var i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = Chips[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        /// <summary>調べる対象をひとつ立てる</summary>
        static void Put(Transform parent, string name, Vector3 at, RoomScript script,
            string id, float radius, bool required, bool once)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
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

        static Material ChipMat()
        {
            var path = "Assets/Materials/Alley/Chip.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Chip";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", new Color(0.115f, 0.125f, 0.145f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.62f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
            EditorUtility.SetDirty(m);
            return m;
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
