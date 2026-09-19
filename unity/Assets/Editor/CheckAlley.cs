using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 組み立てた路地裏を機械で見直す。`HalfAware/Build the alley` の最後に必ず走る。
    ///
    /// 目で見て気づくまで放っておくと、看板が柱の陰に入っていても分からない。
    /// ここで数えられるものは数えておく。
    ///
    /// - 板の面が他の物に隠れていないか
    /// - 調べる対象のピンが物の中に埋まっていないか
    /// - 文面の id と、シーンに立てた対象の id が食い違っていないか
    /// - 人が床に立っているか（浮いたり沈んだりしていないか）
    /// </summary>
    public static class CheckAlley
    {
        /// <summary>この割合より多く隠れていたら知らせる</summary>
        const float Hidden = 0.25f;

        /// <summary>板を見る立ち位置までの距離</summary>
        const float Watch = 2.2f;

        [MenuItem("HalfAware/Check the alley", false, 215)]
        public static void Menu()
        {
            var root = GameObject.Find("Alley");
            if (root == null) { Debug.LogWarning("Alley が無い。先に組み立てる"); return; }
            Run(root.transform);
        }

        public static void Run(Transform root)
        {
            var bad = 0;
            bad += Boards(root);
            bad += Buried(root);
            bad += Pins(root);
            bad += Ids(root);
            bad += Feet(root);
            if (bad == 0) Debug.Log("見直し: 気になるところは無し");
            else Debug.LogWarning("見直し: 気になるところ " + bad + " 件。上を参照");
        }

        /// <summary>
        /// 板の面が隠れていないか。読む人の側に立ち位置を取り、
        /// 面に散らした点へ線を引いて、途中で何かに当たるかを見る。
        ///
        /// 路地裏の物はほとんどが当たり判定を持たない焼いた mesh なので、
        /// ここだけ一時的に当たり判定を立てる。
        /// 頂点を数えるやり方だと、騎戸の桟のような長い一枚板を取り逃す
        /// </summary>
        static int Boards(Transform root)
        {
            var boards = root.Find("Boards");
            if (boards == null) { Debug.LogWarning("見直し: Boards が無い"); return 1; }
            var temp = new List<MeshCollider>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponent<Collider>() != null) continue;
                if (r.transform.parent == boards) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                temp.Add(r.gameObject.AddComponent<MeshCollider>());
            }
            var bad = 0;
            foreach (Transform t in boards)
            {
                if (t.GetComponent<MeshRenderer>() == null) continue;
                if (t.name.EndsWith(".Back")) continue;              // 背板は見えなくてよい
                // 絵は板の裏側に乗っているので、読む人は -forward の側に立つ
                var eye = t.position - t.forward * Watch;
                var blocked = 0;
                var total = 0;
                var who = new Dictionary<string, int>();
                for (var u = -0.45f; u <= 0.46f; u += 0.225f)
                    for (var v = -0.45f; v <= 0.46f; v += 0.225f)
                    {
                        var on = t.position + t.right * (u * t.localScale.x) + t.up * (v * t.localScale.y);
                        total++;
                        var to = on - eye;
                        RaycastHit hit;
                        if (!Physics.Raycast(eye, to.normalized, out hit, to.magnitude - 0.02f)) continue;
                        blocked++;
                        var name = hit.collider.transform.parent == null
                            ? hit.collider.name
                            : hit.collider.transform.parent.name + "/" + hit.collider.name;
                        if (!who.ContainsKey(name)) who[name] = 0;
                        who[name]++;
                    }
                if (total == 0 || blocked / (float)total <= Hidden) continue;
                // 板そのものは判定を持たないので、当たったものはすべて手前にある
                var names = "";
                foreach (var kv in who) names += (names == "" ? "" : ", ") + kv.Key + "×" + kv.Value;
                Debug.LogWarning(string.Format("見直し: 板 {0} は {1}/{2} が隠れている。塞いでいるもの: {3}",
                    t.name, blocked, total, names), t.gameObject);
                bad++;
            }
            for (var i = 0; i < temp.Count; i++) Object.DestroyImmediate(temp[i]);
            return bad;
        }

        /// <summary>
        /// 板が他の物に食い込んでいないか。
        ///
        /// 路地裏の物はほとんどが当たり判定を持たない焼いた mesh なので、
        /// 線を引くだけでは見つからない。板の前後に薄い箱を想定して、
        /// そこへ入り込んでいる頂点を数える。
        /// 板そのものと背板は当然入るので除く
        /// </summary>
        static int Buried(Transform root)
        {
            var boards = root.Find("Boards");
            if (boards == null) return 0;
            var plates = new List<Transform>();
            foreach (Transform t in boards)
                if (t.GetComponent<MeshRenderer>() != null && !t.name.EndsWith(".Back")) plates.Add(t);
            if (plates.Count == 0) return 0;

            var bad = 0;
            foreach (var plate in plates)
            {
                var half = new Vector3(plate.localScale.x * 0.5f, plate.localScale.y * 0.5f, 0.06f);
                var found = new Dictionary<string, int>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r.transform == plate) continue;
                    if (r.transform.parent == boards && r.name.StartsWith(plate.name)) continue;  // 自分の背板
                    if (r.name == "SignArm") continue;                                           // 板を留めている腕木
                    if (!r.bounds.Intersects(new Bounds(plate.position, half * 2f + Vector3.one * 0.1f))) continue;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var verts = filter.sharedMesh.vertices;
                    var into = r.transform.localToWorldMatrix;
                    var hits = 0;
                    for (var i = 0; i < verts.Length; i++)
                    {
                        var local = plate.InverseTransformPoint(into.MultiplyPoint3x4(verts[i]));
                        if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f) continue;   // 板の外
                        if (Mathf.Abs(local.z * plate.localScale.z) > half.z) continue;         // 板より前後
                        hits++;
                    }
                    if (hits == 0) continue;
                    var path = r.transform.parent == null ? r.name : r.transform.parent.name + "/" + r.name;
                    found[path] = hits;
                }
                if (found.Count == 0) continue;
                var names = "";
                foreach (var kv in found) names += (names == "" ? "" : ", ") + kv.Key + "（頂点 " + kv.Value + "）";
                Debug.LogWarning("見直し: 板 " + plate.name + " に食い込んでいる物: " + names, plate.gameObject);
                bad++;
            }
            return bad;
        }

        /// <summary>ピンが物の中に埋まっていないか。埋まると印が見えない</summary>
        static int Pins(Transform root)
        {
            var items = root.Find("Items");
            if (items == null) return 0;
            var bad = 0;
            foreach (Transform t in items)
            {
                var it = t.GetComponent<Interactable>();
                if (it == null) continue;
                // ピンは対象の少し上に出る。そこが物の中なら見えない
                var at = it.Position + Vector3.up * 0.17f;
                var inside = "";
                foreach (var c in Physics.OverlapSphere(at, 0.12f))
                {
                    var p = c.ClosestPoint(at);
                    if (Vector3.Distance(p, at) > 0.001f) continue;   // 触れているだけなら見逃す
                    inside += (inside == "" ? "" : ", ") + c.name;
                }
                if (inside == "") continue;
                Debug.LogWarning(string.Format("見直し: {0} のピンが {1} に埋まっている", it.Id, inside), t.gameObject);
                bad++;
            }
            return bad;
        }

        /// <summary>文面とシーンの id が揃っているか。片方だけあると黙って何も出ない</summary>
        static int Ids(Transform root)
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>("Assets/Data/AlleyScript.asset");
            if (script == null) { Debug.LogWarning("見直し: 場面 2 の文面が無い"); return 1; }
            var inScene = new HashSet<string>();
            var items = root.Find("Items");
            if (items != null)
                foreach (Transform t in items)
                {
                    var it = t.GetComponent<Interactable>();
                    if (it == null) continue;
                    if (string.IsNullOrEmpty(it.Id)) { Debug.LogWarning("見直し: " + t.name + " に id が無い", t.gameObject); continue; }
                    inScene.Add(it.Id);
                }
            var bad = 0;
            foreach (var id in inScene)
                if (script.Find(id).id == null)
                {
                    Debug.LogWarning("見直し: シーンの " + id + " が文面に無い");
                    bad++;
                }
            foreach (var id in script.Ids())
            {
                if (AlleyIds.IsPage(id)) continue;                    // 段は看板から引くので、対象は無くてよい
                if (inScene.Contains(id)) continue;
                Debug.LogWarning("見直し: 文面の " + id + " を出す対象がシーンに無い");
                bad++;
            }
            return bad;
        }

        /// <summary>人が床に立っているか。焼いた形の最下点を床と比べる</summary>
        static int Feet(Transform root)
        {
            var crowd = root.Find("Crowd/Crowd");
            var filter = crowd == null ? null : crowd.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return 0;
            var verts = filter.sharedMesh.vertices;
            var low = float.PositiveInfinity;
            var at = Vector3.zero;
            for (var i = 0; i < verts.Length; i++)
                if (verts[i].y < low) { low = verts[i].y; at = verts[i]; }
            // 中庭の敷石が 0.02、車道が 0.00。5 cm 沈んでいたら知らせる
            if (low > -0.05f) return 0;
            Debug.LogWarning(string.Format("見直し: 人の最下点が {0}（{1} のあたり）。床にめり込んでいる",
                low.ToString("F3"), at.ToString("F1")));
            return 1;
        }
    }
}
