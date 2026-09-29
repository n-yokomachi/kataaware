using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の台詞の原稿（docs/scenario/02-alley.md）を、ゲームの文面のアセット（Assets/Data/AlleyScript.asset）へ写す
    /// （オーナー、2026-09-28「ここを正とし、ゲームの文面はここから写す」）。オーナーが原稿を直すたびに押す。手で写さない。
    ///
    /// 場面 2 の文はみなアセットにある（看板の文面と段・案内板・露店・テーブル・買い手とのやり取り・締め）ので、場面（Alley.unity）には書かない。
    /// 原稿の読み方と、見出しと行き先の表は <see cref="AlleyManuscript"/>（読めない行・表に無い見出し・原稿に無い見出しはエラーで止まり、何も書かない）。
    /// アセットは YAML なので手では触らず、ここから SerializedObject で書く。場面 1 の <see cref="RoomScenario"/> と同じ作り
    /// </summary>
    public static class AlleyScenario
    {
        public const string ScriptPath = "Assets/Data/AlleyScript.asset";

        /// <summary>原稿のファイルの在りか</summary>
        public static string ManuscriptFile
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", AlleyManuscript.Path)); }
        }

        [MenuItem("HalfAware/Apply the scenario (alley)", false, 211)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は写さない。止めてからもう一度"); return; }
            AlleyManuscript.Text text;
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
            if (!Write(text, note)) { Debug.LogError(note.ToString()); return; }
            Debug.Log("原稿を写して保存した: " + ScriptPath + "\n" + note);
        }

        /// <summary>原稿を読んで、写す文面を組む。読めなければ <see cref="ManuscriptException"/></summary>
        public static AlleyManuscript.Text Load()
        {
            var file = ManuscriptFile;
            if (!File.Exists(file)) throw new ManuscriptException(0, "原稿が無い: " + file);
            return AlleyManuscript.Read(File.ReadAllText(file, Encoding.UTF8));
        }

        /// <summary>文面のアセットへ書く。項目は <see cref="AlleyIds.All"/> の順。アセットが無ければ作る</summary>
        public static bool Write(AlleyManuscript.Text text, StringBuilder note)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RoomScript>();
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, ScriptPath);
            }
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
                note.AppendFormat("  {0}{1}: {2} ページ{3}", e.id, string.IsNullOrEmpty(e.label) ? "" : "「" + e.label + "」", e.lines.Length,
                    e.choice.Asks ? "、二択「" + e.choice.question + "」" : "").AppendLine();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        static void Fill(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
