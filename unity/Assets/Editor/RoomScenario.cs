using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 1 の台詞の原稿（docs/scenario/01-room.md）を、ゲームの文面へ写す（オーナー、2026-09-28「ここを正とし、ゲームの文面はここから写す」）。
    /// オーナーが原稿を直すたびに押す。手で写さない。
    /// - 文面のアセット（Assets/Data/RoomScript.asset）: 調べる対象ごとの対象の名前・ページ・二択の問い・「はい」の後。前提の文（hints）は空にする
    /// - Room.unity の <see cref="RoomIntroDirector"/>: 最初の独白・暗転のカード・ジャケットを着た後の独白
    ///
    /// 原稿の読み方と、見出しと行き先の表は <see cref="RoomManuscript"/>（読めない行・表に無い見出し・原稿に無い見出しはエラーで止まり、何も書かない）。
    /// 文面のアセットは YAML なので手では触らず、ここから SerializedObject で書く。
    /// 演出の文は場面の中にあるので、Room.unity を開いている時だけ書き、書いた後に場面の中の物の一覧が変わっていないことを確かめてから保存する。
    /// Room.unity を写して組む場面（場面 3・5・7）は、この後に組み直す（<c>HalfAware/Build the connect scene</c> など）
    /// </summary>
    public static class RoomScenario
    {
        public const string ScriptPath = "Assets/Data/RoomScript.asset";

        /// <summary>原稿のファイルの在りか</summary>
        public static string ManuscriptFile
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", RoomManuscript.Path)); }
        }

        [MenuItem("HalfAware/Apply the scenario (room)", false, 210)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は写さない。止めてからもう一度"); return; }
            RoomManuscript.Text text;
            try
            {
                text = Load();
            }
            catch (ManuscriptException e)
            {
                Debug.LogError(e.Message);
                return;
            }
            var note = new StringBuilder();
            var scene = EditorSceneManager.GetActiveScene();
            var room = scene.path == PlaceProtagonist.RoomPath;
            if (room && scene.isDirty)
            {
                Debug.LogError("Room.unity に未保存の変更がある。保存するか捨ててからもう一度（文面のアセットにも書いていない）");
                return;
            }
            if (!WriteScript(text, note)) { Debug.LogError(note.ToString()); return; }
            if (!room)
            {
                Debug.LogWarning("文面のアセットだけ写した。最初の独白・カード・着た後の独白は Room.unity を開いてもう一度押す\n" + note);
                return;
            }
            var before = PlaceProtagonist.Snapshot();
            var ok = WriteScene(text, note);
            var after = PlaceProtagonist.Snapshot();
            var changed = Changed(before, after, note);
            if (!ok || changed > 0)
            {
                Debug.LogError("演出の文を書いたが、場面の中の物が " + changed + " 個変わった（または書けなかった）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("原稿を写して保存した: " + ScriptPath + " と " + scene.path + "\n" + note);
        }

        /// <summary>原稿を読んで、写す文面を組む。読めなければ <see cref="ManuscriptException"/></summary>
        public static RoomManuscript.Text Load()
        {
            var file = ManuscriptFile;
            if (!File.Exists(file)) throw new ManuscriptException(0, "原稿が無い: " + file);
            return RoomManuscript.Read(File.ReadAllText(file, Encoding.UTF8));
        }

        /// <summary>文面のアセットへ書く。項目は <see cref="RoomIds.All"/> の順に並べ直す</summary>
        public static bool WriteScript(RoomManuscript.Text text, StringBuilder note)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (asset == null) { note.AppendLine("文面のアセットが無い: " + ScriptPath); return false; }
            var so = new SerializedObject(asset);
            var entries = so.FindProperty("entries");
            entries.arraySize = text.Entries.Length;
            for (var i = 0; i < text.Entries.Length; i++)
            {
                var e = text.Entries[i];
                var p = entries.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("id").stringValue = e.id;
                p.FindPropertyRelative("label").stringValue = e.label;
                Fill(p.FindPropertyRelative("lines"), e.lines);
                p.FindPropertyRelative("hints").arraySize = 0;
                var choice = p.FindPropertyRelative("choice");
                choice.FindPropertyRelative("question").stringValue = e.choice.question;
                Fill(choice.FindPropertyRelative("afterYes"), e.choice.afterYes);
                note.AppendFormat("  {0}「{1}」: {2} ページ{3}", e.id, e.label, e.lines.Length,
                    e.choice.Asks ? "、二択「" + e.choice.question + "」、「はい」の後 " + e.choice.afterYes.Length + " ページ" : "").AppendLine();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        /// <summary>開いている Room.unity の <see cref="RoomIntroDirector"/> へ、最初の独白・カード・着た後の独白を書く（保存はしない）</summary>
        public static bool WriteScene(RoomManuscript.Text text, StringBuilder note)
        {
            var intro = Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro == null) { note.AppendLine("RoomIntroDirector が無い"); return false; }
            var so = new SerializedObject(intro);
            Fill(so.FindProperty("firstLines"), text.Opening);
            so.FindProperty("card").stringValue = text.Card;
            Fill(so.FindProperty("afterJacketLines"), text.AfterJacket);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(intro);
            note.AppendFormat("  最初の独白 {0} ページ、着た後の独白 {1} ページ、カード「{2}」", text.Opening.Length, text.AfterJacket.Length, text.Card).AppendLine();
            return true;
        }

        static void Fill(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        /// <summary>二つの一覧の差の数。差は note に書く</summary>
        static int Changed(Dictionary<string, string> a, Dictionary<string, string> b, StringBuilder note)
        {
            var n = 0;
            var keys = new SortedSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            foreach (var k in keys)
            {
                a.TryGetValue(k, out var x);
                b.TryGetValue(k, out var y);
                if (x == y) continue;
                n++;
                note.AppendFormat("  （思いがけない差）{0}: {1} → {2}", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            note.AppendFormat("場面の中の物: {0} 個、差 {1} 個", b.Count, n).AppendLine();
            return n;
        }
    }
}
