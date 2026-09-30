using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 1 の物音を <c>Room.unity</c> に当てる（オーナー、2026-09-29）。
    /// - 椅子から立ち上がる音（<c>ChairRise.wav</c>、「椅子から立ち上がる際の音を追加」）: <see cref="SceneFlow"/> の rise と、
    ///   鳴らす音源 foley（<see cref="RoomIntroDirector"/> の口元の音源 Voice。煙草の息と同じ 2D の音源）。
    ///   立ち上がり始めた時（ジャケットの後。場面 7 はジャックの後）に鳴る（モニターは座ったまま調べるので、そこでは立たない）
    /// - メモの紙の音（<c>PaperTurn.wav</c>、「メモにインタラクトしたときの紙の音を追加」）: クリップボード（<see cref="RoomIds.Clipboard"/>）の
    ///   <see cref="Interactable"/> の sound。場面 3 の同じメモ（note）は <see cref="BuildConnect"/> が同じ音を付ける
    /// - ラグの足音（<c>Rug1〜6.wav</c>、「自室のラグの上を歩く時の音を変更」）: <c>Room/Rug</c> に当たり（BoxCollider）と
    ///   <see cref="StepGround"/> を付ける。当たりはラグ自身に付けるので、ラグを動かせば一緒に動く。
    ///   当たりの上面は床の上面から <see cref="RugLift"/> だけ上（見た目のラグの上面は 3cm 上だが、そこまで上げると
    ///   ラグへ乗り降りするたびに目線が 3cm 跳ねる）。足元を見る線（<see cref="Footsteps"/>）は上から下りてくるので、床より先にラグに当たる
    ///
    /// 場面 3・5・7 は場面 1 を写して組む（<see cref="BuildConnect"/> → <see cref="BuildRest"/>・<see cref="BuildNotice"/>）ので、組み直せば付いてくる。
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を当てる前と比べ、Room/Rug のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceRoomFoley
    {
        /// <summary>椅子から立ち上がる音</summary>
        public const string RisePath = "Assets/Audio/ChairRise.wav";
        /// <summary>メモの紙をめくる音</summary>
        public const string PaperPath = "Assets/Audio/PaperTurn.wav";
        /// <summary>ラグ。部屋の子</summary>
        public const string RugPath = "Room/Rug";
        /// <summary>ラグの当たりの上面を床の上面からどれだけ上げるか。m。乗っても目線は 5mm しか上がらない</summary>
        public const float RugLift = 0.005f;

        [MenuItem("HalfAware/Put the foley in the room", false, 214)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PlaceProtagonist.RoomPath)
            {
                Debug.LogError("物音を当てるのは場面 1（Room.unity）だけ。今開いているのは " + scene.path);
                return;
            }
            if (scene.isDirty) { Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + scene.path); return; }
            var before = PlaceProtagonist.Snapshot();
            var note = new StringBuilder();
            var ok = Put(note);
            var after = PlaceProtagonist.Snapshot();
            var diff = Diff(before, after, out var unexpected);
            note.AppendLine("場面の中の物の差:").Append(diff);
            if (!ok || unexpected > 0)
            {
                Debug.LogError("物音を当てたが、ほかの物が " + unexpected + " 個変わった（または当てられなかった）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("物音を当てて保存した: " + scene.path + "\n" + note);
        }

        /// <summary>開いている場面 1 に物音を当てる。保存はしない</summary>
        public static bool Put(StringBuilder note)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { note.AppendLine("SceneFlow が無い"); return false; }
            var intro = Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro == null) { note.AppendLine("RoomIntroDirector が無い"); return false; }
            var voice = new SerializedObject(intro).FindProperty("voice").objectReferenceValue as AudioSource;
            if (voice == null) { note.AppendLine("RoomIntroDirector に口元の音源（voice）が繋がっていない"); return false; }
            var rise = AssetDatabase.LoadAssetAtPath<AudioClip>(RisePath);
            var paper = AssetDatabase.LoadAssetAtPath<AudioClip>(PaperPath);
            if (rise == null || paper == null) { note.AppendLine("音のファイルが無い: " + RisePath + " / " + PaperPath); return false; }

            var fso = new SerializedObject(flow);
            fso.FindProperty("foley").objectReferenceValue = voice;
            fso.FindProperty("rise").objectReferenceValue = rise;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
            note.AppendFormat("椅子から立ち上がる音: {0}（{1:0.000} 秒）を {2}（音量 {3:0.00}）で鳴らす",
                rise.name, rise.length, Path(voice.transform), voice.volume).AppendLine();

            var memo = 0;
            foreach (var item in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.Id != RoomIds.Clipboard) continue;
                Paper(item, paper);
                memo++;
            }
            if (memo != 1) { note.AppendLine("クリップボード（" + RoomIds.Clipboard + "）の調べる対象が " + memo + " 個ある。一つのはず"); return false; }
            note.AppendFormat("メモの紙の音: {0}（{1:0.000} 秒）をクリップボードに", paper.name, paper.length).AppendLine();

            var rug = GameObject.Find(RugPath);
            if (rug == null) { note.AppendLine("ラグ（" + RugPath + "）が無い"); return false; }
            var clips = StepSets.Clips(StepSets.Rug);
            if (clips.Length != StepSets.Rug.Length) { note.AppendLine("ラグの足音の素材が欠けている"); return false; }
            return Rug(rug, clips, note);
        }

        /// <summary>調べる対象に紙の音を付ける（場面 1 のクリップボードと場面 3 のメモ）</summary>
        public static void Paper(Interactable item, AudioClip clip)
        {
            var so = new SerializedObject(item);
            so.FindProperty("sound").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        /// <summary>
        /// ラグに当たりと足音の地面を付ける。当たりの広さは見た目のメッシュの広さ（ラグの座標で）、
        /// 厚みは床の上面を挟んで下へ RugLift・上へ RugLift
        /// </summary>
        public static bool Rug(GameObject rug, AudioClip[] clips, StringBuilder note)
        {
            var filter = rug.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) { note.AppendLine("ラグにメッシュが無い"); return false; }
            var floor = FloorTop(rug);
            if (float.IsNaN(floor)) { note.AppendLine("ラグの下に床の当たりが見つからない"); return false; }
            var t = rug.transform;
            var local = filter.sharedMesh.bounds;
            // 上下は世界の高さで決めてから、ラグの座標へ戻す（ラグは傾けていない前提。傾いていたら断る）
            if (Vector3.Angle(t.up, Vector3.up) > 0.5f) { note.AppendLine("ラグが傾いている。当たりの厚みを床に合わせられない"); return false; }
            var bottom = t.InverseTransformPoint(new Vector3(t.position.x, floor - RugLift, t.position.z)).y;
            var top = t.InverseTransformPoint(new Vector3(t.position.x, floor + RugLift, t.position.z)).y;

            var box = rug.GetComponent<BoxCollider>();
            if (box == null) box = rug.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(local.center.x, (bottom + top) * 0.5f, local.center.z);
            box.size = new Vector3(local.size.x, top - bottom, local.size.z);
            EditorUtility.SetDirty(box);

            var ground = rug.GetComponent<StepGround>();
            if (ground == null) ground = rug.AddComponent<StepGround>();
            var so = new SerializedObject(ground);
            var row = so.FindProperty("clips");
            row.arraySize = clips.Length;
            for (var i = 0; i < clips.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.FindProperty("patches").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ground);

            var b = box.bounds;
            note.AppendFormat("ラグの足音: {0} 個。当たりは世界の x {1:0.00}〜{2:0.00}・z {3:0.00}〜{4:0.00}、高さ {5:0.000}〜{6:0.000}（床の上面 {7:0.000}）",
                clips.Length, b.min.x, b.max.x, b.min.z, b.max.z, b.min.y, b.max.y, floor).AppendLine();
            return true;
        }

        /// <summary>ラグの真ん中の真下にある床の当たりの上面の高さ。ラグ自身の当たりは数えない。無ければ NaN</summary>
        static float FloorTop(GameObject rug)
        {
            var r = rug.GetComponent<Renderer>();
            var at = r != null ? r.bounds.center : rug.transform.position;
            var own = rug.GetComponent<Collider>();
            var best = float.NaN;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 1f, Vector3.down, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == own || hit.collider.gameObject.scene != rug.scene) continue;
                if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
            }
            return best;
        }

        static string Path(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>二つの一覧の差。Room/Rug の差は書くが、思いがけない差には数えない</summary>
        static string Diff(Dictionary<string, string> a, Dictionary<string, string> b, out int unexpected)
        {
            unexpected = 0;
            var sb = new StringBuilder();
            var keys = new SortedSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            foreach (var k in keys)
            {
                a.TryGetValue(k, out var x);
                b.TryGetValue(k, out var y);
                if (x == y) continue;
                var allowed = k == RugPath;
                if (!allowed) unexpected++;
                sb.AppendFormat("  {0}{1}: {2} → {3}", allowed ? "" : "（思いがけない）", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            if (sb.Length == 0) sb.AppendLine("  無し");
            return sb.ToString();
        }
    }
}
