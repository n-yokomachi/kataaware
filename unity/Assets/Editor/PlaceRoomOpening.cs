using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 1 の冒頭と一服の値を、<c>Room.unity</c> に当てる（オーナー、2026-09-28）。
    /// - 冒頭の呼吸（<c>Breathing.wav</c>）を鳴らす 2D の音源を Player/Breath に置き、<see cref="RoomIntroDirector"/> へ繋ぐ。
    ///   ジャックが抜けたところで止めるので、抜くしぐさ（<see cref="JackPull"/>）も繋ぐ
    /// - 一服の暗転は一度だけにし、場所と時刻のカード（前の 3 枚目と同じ文と組み方）を一枚だけ持たせる。カードを出す秒は前の 1.95 秒の 1.5 倍
    /// - 座っている間の首の限りを 70 度にする（前方 140 度ほど。<see cref="SceneFlow"/> の seatedHeadLimit。ほかの場面は 90 度のまま）
    ///
    /// 呼吸の大きさと瞬きの秒は Inspector で詰めるので、前に置いた物があれば書き戻さない（音源の大きさは RoomIntroDirector が鳴らす時に当てる）。
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を置く前と比べ、Player/Breath のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceRoomOpening
    {
        /// <summary>呼吸の音源の入れ物の名前。Player の子に置く。場面 3 の組み立て（<see cref="BuildConnect"/>）が落とす</summary>
        public const string BreathName = "Breath";

        /// <summary>
        /// 場所と時刻のカード。前の 3 枚目（「制作 : yoko」・題の後に出していた物）と同じ文と組み方。
        /// 原稿（docs/scenario/01-room.md）の「2166年8月15日 18時35分　｜倫敦《ロンドン》　自室」を、ルビを手で組んだ形
        /// </summary>
        public const string Card = "<size=30>2166年8月15日 18時35分　<voffset=1.05em><size=15>ロンドン</size></voffset><space=-2em>倫敦　自室</size>";

        /// <summary>カードを出す秒。前の 1.95 秒の 1.5 倍（オーナー「その暗転表示も今の1.5倍に延長」）</summary>
        public const float HoldSeconds = 1.95f * 1.5f;

        /// <summary>座っている間の首の限り。片側。前方 140 度ほど（オーナー「カメラの回転画角を前方140度くらいにして」）</summary>
        public const float SeatedHeadLimit = 70f;

        [MenuItem("HalfAware/Put the opening in the room", false, 209)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PlaceProtagonist.RoomPath)
            {
                Debug.LogError("冒頭の呼吸を置くのは場面 1（Room.unity）だけ。今開いているのは " + scene.path);
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
                Debug.LogError("冒頭を当てたが、ほかの物が " + unexpected + " 個変わった（または当てられなかった）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("冒頭と一服の値を当てて保存した: " + scene.path + "\n" + note);
        }

        public static bool Put(StringBuilder note)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null || flow.Player == null) { note.AppendLine("SceneFlow か Player が無い"); return false; }
            var intro = Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro == null) { note.AppendLine("RoomIntroDirector が無い"); return false; }
            var pull = Object.FindFirstObjectByType<JackPull>(FindObjectsInactive.Include);
            if (pull == null) note.AppendLine("JackPull が無い。ジャックを調べたところで呼吸を止める");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoomAudioImport.BreathPath);
            if (clip == null) { note.AppendLine("音のファイルが無い: " + RoomAudioImport.BreathPath); return false; }

            var player = flow.Player.transform;
            var t = player.Find(BreathName);
            var go = t != null ? t.gameObject : new GameObject(BreathName);
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            go.transform.localRotation = Quaternion.identity;
            var a = go.GetComponent<AudioSource>();
            if (a == null) a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            // 鳴らし始めるのは RoomIntroDirector（思い出した時にジャックを抜いた後なら鳴らさない）
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            a.priority = 100;
            EditorUtility.SetDirty(a);

            var so = new SerializedObject(intro);
            so.FindProperty("breath").objectReferenceValue = a;
            so.FindProperty("jackPull").objectReferenceValue = pull;
            so.FindProperty("card").stringValue = Card;
            so.FindProperty("holdSeconds").floatValue = HoldSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            a.volume = so.FindProperty("breathVolume").floatValue;
            EditorUtility.SetDirty(intro);

            var fso = new SerializedObject(flow);
            fso.FindProperty("seatedHeadLimit").floatValue = SeatedHeadLimit;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);

            note.AppendFormat("冒頭の呼吸: {0}（{1:0.00} 秒）を Player/{2} で輪にして鳴らす。大きさ {3:0.00}。止めるのは {4}",
                clip.name, clip.length, BreathName, a.volume, pull != null ? pull.name + " の JackPull" : "ジャックを調べた時").AppendLine();
            note.AppendFormat("一服: カード 1 枚、出す秒 {0:0.000}。座っている間の首の限り {1} 度", HoldSeconds, SeatedHeadLimit).AppendLine();
            return true;
        }

        /// <summary>二つの一覧の差。Player/Breath の差は書くが、思いがけない差には数えない</summary>
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
                var allowed = k == "Player/" + BreathName;
                if (!allowed) unexpected++;
                sb.AppendFormat("  {0}{1}: {2} → {3}", allowed ? "" : "（思いがけない）", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            if (sb.Length == 0) sb.AppendLine("  無し");
            return sb.ToString();
        }
    }
}
