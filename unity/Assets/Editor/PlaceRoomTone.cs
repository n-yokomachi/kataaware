using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の空気の音（<see cref="RoomTone"/>）を、場面 1 の <c>Room.unity</c> の Player の下に置く。
    /// 雨や雑踏と同じく、プレイヤーの頭上で 2D の輪にして流す。
    ///
    /// 場面 3（<c>Connect.unity</c>）と場面 5（<c>Rest.unity</c>）は Room.unity を写して組む（<see cref="BuildConnect"/> → <see cref="BuildRest"/>）ので、
    /// Room.unity に置いてから組み直せば付いてくる。どちらの組み立ても、念のためここを通して繋ぎ直す。
    ///
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を置く前と比べ、
    /// Player/RoomTone のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceRoomTone
    {
        /// <summary>音の入れ物の名前。Player の子に置く</summary>
        public const string Name = "RoomTone";

        [MenuItem("HalfAware/Put the room tone in the room", false, 207)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PlaceProtagonist.RoomPath)
            {
                Debug.LogError("自室の空気の音を置くのは場面 1（Room.unity）だけ。場面 3 と 5 は組み直せば付いてくる。今開いているのは " + scene.path);
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
                Debug.LogError("自室の空気の音を置いたが、ほかの物が " + unexpected + " 個変わった（または置けなかった）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("自室の空気の音を置いて保存した: " + scene.path + "\n" + note);
        }

        /// <summary>
        /// Player の頭上に輪で流す 2D の音を置き、<see cref="RoomTone"/> を繋ぐ。前に置いた物があれば使い回す。
        /// 大きさはオーナーが耳で決めるので書き戻さない（インスペクターで変えた値が残る）
        /// </summary>
        public static bool Put(StringBuilder note)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null || flow.Player == null) { note.AppendLine("SceneFlow か Player が無い"); return false; }
            var hud = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) note.AppendLine("HudView が無い。暗転に合わせて絞れない");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoomAudioImport.TonePath);
            if (clip == null) { note.AppendLine("音のファイルが無い: " + RoomAudioImport.TonePath); return false; }

            var player = flow.Player.transform;
            var t = player.Find(Name);
            var go = t != null ? t.gameObject : new GameObject(Name);
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            go.transform.localRotation = Quaternion.identity;
            var a = go.GetComponent<AudioSource>();
            if (a == null) a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = true;
            a.spatialBlend = 0f;
            a.priority = 200;

            var tone = go.GetComponent<RoomTone>();
            if (tone == null) tone = go.AddComponent<RoomTone>();
            var so = new SerializedObject(tone);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("sound").objectReferenceValue = a;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 鳴り始めの一こまは、ふだんの大きさで鳴らす
            a.volume = so.FindProperty("volume").floatValue;
            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(tone);
            note.AppendFormat("自室の空気の音: {0}（{1:0.0} 秒）を Player/{2} で輪にして鳴らす。大きさ {3:0.00}", clip.name, clip.length, Name, a.volume).AppendLine();
            return true;
        }

        /// <summary>二つの一覧の差。Player/RoomTone の差は書くが、思いがけない差には数えない</summary>
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
                var allowed = k == "Player/" + Name;
                if (!allowed) unexpected++;
                sb.AppendFormat("  {0}{1}: {2} → {3}", allowed ? "" : "（思いがけない）", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            if (sb.Length == 0) sb.AppendLine("  無し");
            return sb.ToString();
        }
    }
}
