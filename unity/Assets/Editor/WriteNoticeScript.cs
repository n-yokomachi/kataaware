using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 7（自室・気づき）の文面を書き出す。シナリオ設計 11 節の流れの文をここに持ち、
    /// `Assets/Data/NoticeScript.asset` へ流し込む。**台詞を直すならここを直して走らせ直す。**
    /// 頭の独白（<see cref="NoticeLines"/>）は対象に紐づかないので、アセットではなく組み立て（BuildNotice）が
    /// <see cref="NoticeDirector"/> へ書き込む。直したら組み直す。
    ///
    /// **本文はどれも仮置き。** 流れを通すための叩き台で、原作の 36〜38 段から短く抜いた。
    /// 独白は 2（気づく）と 3（ログの後の一行）だけ（ゲーム設計 5 節）。ジャック・ジャケット・ドアは文を持たず、
    /// 抜く・着る・出るのしぐさと音だけで表す
    /// </summary>
    public static class WriteNoticeScript
    {
        const string Path = "Assets/Data/NoticeScript.asset";

        /// <summary>
        /// 庭の記憶の主の端末の場所（仮）。村の作りに合わせた、コッツウォルズ寄りのイングランドの架空の村。
        /// 原作のエディンバラ近郊は使わない（シナリオ設計 11.2、2026-09-27）
        /// </summary>
        public const string Village = "ロウアー・ハザーウィック";
        /// <summary>村のある州（仮）。コッツウォルズの丘の西側</summary>
        public const string County = "グロスターシャー";
        /// <summary>
        /// 庭の記憶の日時（仮）。場面 6 の見出しの「2163/08/16 19:45」と同じ時刻（シナリオ設計 10.3）。
        /// 表は空白で列を分けるので、日付と時刻のあいだに空白を入れない書き方にする（場面 1 の走査条件の「2156年3月2日」に揃えた）
        /// </summary>
        public const string Stamp = "2163年8月16日19時45分";

        /// <summary>
        /// 戻った時の独白（2〜3 行、仮）。原作の「あの違和感がないという違和感」「視力の違いも、伸ばした手の距離感も」の筋を短く言い切る
        /// </summary>
        public static readonly string[] NoticeLines =
        {
            "違和感が来ない。他人の記憶から戻った時の、あの違和感が",
            "視力の違いも、伸ばした手の距離感も、何もずれていなかった",
            "まるで、私自身の記憶を観ていたみたいに",
        };

        /// <summary>
        /// アクセスログの画面（1 ページにまとめて出す）と、その後の独白の一行（どちらも仮）。
        /// 並びは表に組まれる（空白で区切った塊が列になる）。どの行も「項目　値」の 2 列にそろえ、値の中に空白を入れない
        /// </summary>
        public static string[] LogLines
        {
            get
            {
                return new[]
                {
                    "記憶の日時　" + Stamp + "\n" +
                    "端末の場所　" + Village + "（" + County + "）\n" +
                    "切断　接続先の側から",
                    "やはり田舎の村だ。名前に覚えはない",
                };
            }
        }

        /// <summary>
        /// 書き出す項目をそのまま組んで返す。Write はこれをアセットへ流し込むだけにしてある。
        /// 分けてあるのは、見直しが「いま書き出すもの」とアセットの中身を突き合わせられるようにするため
        /// </summary>
        public static List<ScriptEntry> Entries()
        {
            var entries = new List<ScriptEntry>();
            entries.Add(Say(NoticeIds.Log, "アクセスログを見る", LogLines));
            // 場面 1 と同じ抜き方と印。文は持たない（独白は 2 と 3 だけ）
            entries.Add(Say(NoticeIds.Jack, "インプラントジャックを抜く", new string[0]));
            // 場面 3 で掛けたジャケットを取って着る。文は持たず、着る音と見た目は NoticeDirector が出す。印は仮置き
            entries.Add(Say(NoticeIds.Coat, "ジャケットを着る", new string[0]));
            // 出れば場面が閉じて戻れないので、場面 1 のドアと同じく二択で確かめる
            var door = Say(NoticeIds.Door, "ドア", new string[0]);
            door.choice = new ScriptChoice { question = "部屋を出る", afterYes = new string[0] };
            entries.Add(door);
            return entries;
        }

        static ScriptEntry Say(string id, string label, string[] lines)
        {
            return new ScriptEntry
            {
                id = id,
                label = label,
                lines = lines,
                hints = new ScriptHint[0],
            };
        }

        [MenuItem("HalfAware/Write the notice script", false, 260)]
        public static void Write()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            var made = asset == null;
            if (made) asset = ScriptableObject.CreateInstance<RoomScript>();

            var entries = Entries();
            var so = new SerializedObject(asset);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = entries[i].id;
                e.FindPropertyRelative("label").stringValue = entries[i].label;
                Fill(e.FindPropertyRelative("lines"), entries[i].lines);
                e.FindPropertyRelative("hints").arraySize = 0;
                var choice = e.FindPropertyRelative("choice");
                choice.FindPropertyRelative("question").stringValue = entries[i].choice.question ?? "";
                Fill(choice.FindPropertyRelative("afterYes"), entries[i].choice.afterYes);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, Path);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("場面 7 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty list, string[] values)
        {
            var n = values == null ? 0 : values.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++) list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
