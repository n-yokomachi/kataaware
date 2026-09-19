using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の文面と、それに合わせたシーンの詰め直し。
    /// 文面のアセットは YAML なので手では触らず、ここから SerializedObject で書く。
    /// 一度当てれば済むが、シナリオを直したときに当て直せるよう残しておく
    /// </summary>
    public static class RoomText
    {
        public const string ScriptPath = "Assets/Data/RoomScript.asset";

        [MenuItem("HalfAware/Apply the room text")]
        public static void ApplyMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("再生中は当てない"); return; }
            Apply();
            Debug.Log("自室の文面を当て直した");
        }

        public static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ScriptPath);
            if (asset == null) { Debug.LogError("文面のアセットが無い: " + ScriptPath); return; }
            var so = new SerializedObject(asset);
            var entries = so.FindProperty("entries");

            // チップ: ラベルは 1 ページにまとめ、読み終えてから二択を出す
            var chips = Entry(entries, "chips");
            if (chips != null)
            {
                Lines(chips, new string[]
                {
                    "メモリが6枚。今日の分だ",
                    "08/15 #1 男 41 『ディエゴ』 3分40秒\n" +
                    "08/15 #2 女 23 『ミア』 2分05秒\n" +
                    "08/15 #3 男 8 『ゆうと』 1分50秒\n" +
                    "08/15 #4 女 35 『阿明』 4分00秒\n" +
                    "08/15 #5 男 19 『リアム』 1分30秒\n" +
                    "08/15 #6 女 52 『マチルド』 3分55秒",
                });
                Ask(chips, "チップを抜く", new string[0]);
            }

            // モニター: 顔を見せた後に二択。解除してから走査条件を 1 ページで出す
            var terminal = Entry(entries, "terminal");
            if (terminal != null)
            {
                Lines(terminal, new string[]
                {
                    "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている",
                    "というのもほら、",
                    "こうして反射で自分の顔が見られるからだ",
                    "黒い髪に琥珀色の目。左目の下のほくろ",
                    "滅多にないことだが、潜り込んだ他人の記憶が薬などでトリップしていると",
                    "私まで影響を受ける",
                    "抜け出した後の自己同定のために、鏡を見ることは大切だ",
                });
                Ask(terminal, "スリープを解除する", new string[]
                {
                    "スリープを解除すると、今日の記憶走査条件が表示される",
                    "条件　名前を呼ばれた時刻\n" +
                    "対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日\n" +
                    "候補の簡易抽出　56,232,318",
                });
            }

            // メモ: 売り上げは 3 行に割って 1 ページで出す
            var clipboard = Entry(entries, "clipboard");
            if (clipboard != null)
            {
                Lines(clipboard, new string[]
                {
                    "売り上げのメモだ",
                    "2166/08/13　5枚\n2166/08/14　4枚\n2166/08/15　―",
                });
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        static SerializedProperty Entry(SerializedProperty entries, string id)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("id").stringValue == id) return e;
            }
            Debug.LogWarning("文面に id が無い: " + id);
            return null;
        }

        static void Lines(SerializedProperty entry, string[] lines)
        {
            Fill(entry.FindPropertyRelative("lines"), lines);
        }

        static void Ask(SerializedProperty entry, string question, string[] afterYes)
        {
            var choice = entry.FindPropertyRelative("choice");
            choice.FindPropertyRelative("question").stringValue = question;
            Fill(choice.FindPropertyRelative("afterYes"), afterYes);
        }

        static void Fill(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
