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
    /// - 一服の暗転は一度だけにし、カードを出す秒は前の 1.95 秒の 1.5 倍。カードの文は原稿から写す（<see cref="RoomScenario"/>）
    /// - 煙草の音を繋ぐ（オーナー、2026-09-28）。箱から一本取る音（<c>PackPull.wav</c>）を <see cref="RoomIntroDirector"/> へ、
    ///   ジッポ（<c>Zippo.wav</c>）と煙草に火が移った音（<c>CigaretteLit.wav</c>）を <see cref="Cigarette"/> へ
    /// - 座っている間の首の限りを 60 度にする（前方 120 度。<see cref="SceneFlow"/> の seatedHeadLimit。ほかの場面は 90 度のまま）
    ///
    /// 呼吸の大きさと瞬きの秒は Inspector で詰めるので、前に置いた物があれば書き戻さない（音源の大きさは RoomIntroDirector が鳴らす時に当てる）。
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を置く前と比べ、Player/Breath のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceRoomOpening
    {
        /// <summary>呼吸の音源の入れ物の名前。Player の子に置く。場面 3 の組み立て（<see cref="BuildConnect"/>）が落とす</summary>
        public const string BreathName = "Breath";

        /// <summary>煙草の箱から一本取る音</summary>
        public const string PackPullPath = "Assets/Audio/PackPull.wav";
        /// <summary>ジッポの音（開く・点く・閉じる）</summary>
        public const string ZippoPath = "Assets/Audio/Zippo.wav";
        /// <summary>煙草に火が移った音</summary>
        public const string LitPath = "Assets/Audio/CigaretteLit.wav";

        /// <summary>カードを出す秒。前の 1.95 秒の 1.5 倍（オーナー「その暗転表示も今の1.5倍に延長」）</summary>
        public const float HoldSeconds = 1.95f * 1.5f;

        /// <summary>
        /// 座っている間の首の限り。片側。前方 120 度（オーナー、2026-09-29「カメラの回転可能範囲を前方120度に」。
        /// 前は「カメラの回転画角を前方140度くらいにして」の 70 度）。この限りでも、右の卓のジャケットと左の卓の煙草・箱・灰皿は
        /// 首を振り切れば狙える（2026-09-29 に確かめた。ジャケットは首を右へ振り切って視線から 26 度、選べる角度 40 度の内）
        /// </summary>
        public const float SeatedHeadLimit = 60f;

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
            so.FindProperty("holdSeconds").floatValue = HoldSeconds;
            var packPull = AssetDatabase.LoadAssetAtPath<AudioClip>(PackPullPath);
            if (packPull == null) { note.AppendLine("音のファイルが無い: " + PackPullPath); return false; }
            so.FindProperty("packPull").objectReferenceValue = packPull;
            so.ApplyModifiedPropertiesWithoutUndo();
            a.volume = so.FindProperty("breathVolume").floatValue;
            EditorUtility.SetDirty(intro);

            // 煙草の音。ジッポと火が移った音は Cigarette が鳴らす（吸う息・吐く息と同じ口元の音源）
            var cigarette = so.FindProperty("cigarette").objectReferenceValue as Cigarette;
            var zippo = AssetDatabase.LoadAssetAtPath<AudioClip>(ZippoPath);
            var lit = AssetDatabase.LoadAssetAtPath<AudioClip>(LitPath);
            if (cigarette == null) { note.AppendLine("RoomIntroDirector に Cigarette が繋がっていない"); return false; }
            if (zippo == null || lit == null) { note.AppendLine("音のファイルが無い: " + ZippoPath + " / " + LitPath); return false; }
            var cso = new SerializedObject(cigarette);
            cso.FindProperty("zippo").objectReferenceValue = zippo;
            cso.FindProperty("lit").objectReferenceValue = lit;
            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cigarette);

            var fso = new SerializedObject(flow);
            fso.FindProperty("seatedHeadLimit").floatValue = SeatedHeadLimit;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);

            note.AppendFormat("冒頭の呼吸: {0}（{1:0.00} 秒）を Player/{2} で輪にして鳴らす。大きさ {3:0.00}。止めるのは {4}",
                clip.name, clip.length, BreathName, a.volume, pull != null ? pull.name + " の JackPull" : "ジャックを調べた時").AppendLine();
            note.AppendFormat("一服: カード 1 枚、出す秒 {0:0.000}。座っている間の首の限り {1} 度", HoldSeconds, SeatedHeadLimit).AppendLine();
            note.AppendFormat("煙草の音: 箱 {0}（{1:0.000} 秒）、ジッポ {2}（{3:0.000} 秒、点火 {4:0.000} 秒）、火が移る {5}（{6:0.000} 秒）",
                packPull.name, packPull.length, zippo.name, zippo.length, SmokeBeats.StrikeInZippo, lit.name, lit.length).AppendLine();
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
