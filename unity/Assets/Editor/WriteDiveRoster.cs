using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の記憶の一覧を書き出す。設計書 6 節の 16 人をここに持ち、
    /// `Assets/Data/DiveRoster.asset` へ流し込む。
    ///
    /// 一覧をアセットへ置くのは、場所と人の形（シーン）と数や文字（アセット）を分けるため。
    /// ただし手で YAML を書くと日本語が読めなくなるので、元はこのファイルに置いて
    /// メニューから書き出す。**一覧を直すならここを直して走らせ直す。**
    ///
    /// row は場面 3 のモニターの列と同じ書式。列に載っている 6 人
    /// （<see cref="DiveIds.Listed"/>）は `WriteConnectScript.MonitorLines` の
    /// 三ページ目と一字も違えない。違えば同じ人だと読み取れなくなる。
    ///
    /// 見える人の name は Task 8 で組む `Take` の子の名前。綴りが食い違うと板が出ない。
    /// 秒数・色味・目の高さはオーナーが決める。ここに入れてあるのは設計書 6 節の仮置き
    /// </summary>
    public static class WriteDiveRoster
    {
        const string Path = "Assets/Data/DiveRoster.asset";

        /// <summary>老いた耳の詰まり。低域だけが残るところまでは寄せない</summary>
        const float Aged = 0.4f;

        /// <summary>
        /// 書き出す 16 人をそのまま組んで返す。Write はこれをアセットへ流し込むだけにしてある。
        /// 分けてあるのは、見直しが「いま書き出すもの」とアセットの中身を
        /// 突き合わせられるようにするため
        /// </summary>
        public static List<DiveEntry> Entries()
        {
            var entries = new List<DiveEntry>();

            // 0. 団地の外階段。母に呼ばれて駆け上がり、抱き上げられて降ろされる
            entries.Add(Of("女　6　『メイ』　2156/03/02 07:14", DiveIds.Estate, 60f, 1.00f,
                Blur.Sharp, 0f, 1.5f, Tone(1.00f, 0.98f, 0.92f), 0f, true,
                Who("Mother", 1)));

            // 1. 同じ朝の踊り場。娘に体操着を渡し、隣の老人が新聞を広げている
            entries.Add(Of("女　34　『ハンナ』　2156/03/02 09:02", DiveIds.Estate, 30f, 1.55f,
                Blur.Sharp, 0f, 1.0f, Tone(0.94f, 0.92f, 0.88f), 0f, false,
                Who("Daughter", 0), Who("Neighbour", 8)));

            // 2. 公園のベンチ。妻に呼ばれて立ち、孫娘に手を取られて門へ
            entries.Add(Of("男　78　『アルベルト』　2156/03/02 15:47", DiveIds.Park, 60f, 1.75f,
                Blur.Near, 0.7f, 0.6f, Tone(0.98f, 0.92f, 0.78f), Aged, false,
                Who("Granddaughter", 3), Who("Wife", 9)));

            // 3. 同じ公園。祖父を引っ張って立たせ、拾った石を握らせる
            entries.Add(Of("女　7　『ソフィア』　2156/03/02 15:47", DiveIds.Park, 30f, 1.05f,
                Blur.Sharp, 0f, 1.4f, Tone(1.00f, 0.96f, 0.84f), 0f, true,
                Who("Grandfather", 2), Who("Grandmother", 9)));

            // 4. 夕方の電車。後輩に本を返され、揺れて腕を掴まれる
            entries.Add(Of("女　19　『エミリー』　2156/03/02 18:20", DiveIds.Train, 30f, 1.58f,
                Blur.Sharp, 0f, 1.1f, Tone(0.92f, 0.96f, 1.00f), 0f, false,
                Who("Junior", 11), Who("Passenger", 5)));

            // 5. 朝の台所。妻に昼食の袋を渡され、玄関から白い光の外へ
            entries.Add(Of("男　52　『マーク』　2156/03/03 06:55", DiveIds.Kitchen, 35f, 1.72f,
                Blur.Far, 0.6f, 0.9f, Tone(0.88f, 0.94f, 1.00f), 0f, false,
                Who("Wife", 6), Who("Son", 12)));

            // 6. 同じ台所の戸口。夫に棚の場所を教え、息子を送り出す
            entries.Add(Of("女　49　『リンダ』　2156/03/03 06:56", DiveIds.Kitchen, 30f, 1.58f,
                Blur.Sharp, 0f, 1.0f, Tone(0.90f, 0.95f, 1.00f), 0f, false,
                Who("Husband", 5), Who("Son", 12)));

            // 7. 昼の教室。手を挙げた生徒のノートを覗き、隣の居眠りを叩く
            entries.Add(Of("男　41　『リー』　2156/03/03 11:38", DiveIds.Classroom, 35f, 1.70f,
                Blur.Sharp, 0f, 1.0f, Tone(1.00f, 1.00f, 0.96f), 0f, false,
                Who("Pupil", 13), Who("Sleeper", 14)));

            // 8. 団地の廊下。新聞を取りに出て、隣で娘を抱き上げる母親を眺めている
            entries.Add(Of("男　66　『ジョルジョ』　2156/03/02 09:03", DiveIds.Estate, 25f, 1.65f,
                Blur.Near, 0.6f, 0.7f, Tone(0.92f, 0.90f, 0.86f), Aged, false,
                Who("Wife", 15), Who("Mother", 1)));

            // 9. 公園の門。孫が鳩を追って駆け出し、呼び戻して手を取り直す
            entries.Add(Of("女　72　『ローザ』　2156/03/02 15:50", DiveIds.Park, 30f, 1.50f,
                Blur.Near, 0.6f, 0.6f, Tone(0.98f, 0.94f, 0.82f), Aged, false,
                Who("Toddler", 10), Who("Husband", 2), Who("Granddaughter", 3)));

            // 10. 同じ公園。鳩を追いかけていて祖母に呼ばれる。地面が近い
            entries.Add(Of("男　3　『ルーカス』　2156/03/02 15:50", DiveIds.Park, 25f, 0.90f,
                Blur.Sharp, 0f, 1.6f, Tone(1.00f, 0.98f, 0.86f), 0f, true,
                Who("Grandmother", 9), Who("Grandfather", 2)));

            // 11. 同じ電車。先輩の背中に声を掛けて本を差し出したところ
            entries.Add(Of("女　18　『プリヤ』　2156/03/02 18:20", DiveIds.Train, 25f, 1.55f,
                Blur.Sharp, 0f, 1.1f, Tone(0.92f, 0.96f, 1.00f), 0f, false,
                Who("Senior", 4)));

            // 12. 同じ家の階段。母に呼ばれて降り、昼食の袋を受け取って出ていく
            entries.Add(Of("男　15　『ダニエル』　2156/03/03 06:56", DiveIds.Kitchen, 25f, 1.65f,
                Blur.Sharp, 0f, 1.2f, Tone(0.90f, 0.95f, 1.00f), 0f, false,
                Who("Mother", 6), Who("Father", 5)));

            // 13. 同じ教室。手を挙げていて先生が近づいてくる
            entries.Add(Of("女　16　『アイシャ』　2156/03/03 11:38", DiveIds.Classroom, 25f, 1.20f,
                Blur.Sharp, 0f, 1.1f, Tone(1.00f, 1.00f, 0.96f), 0f, false,
                Who("Teacher", 7), Who("Neighbour", 14)));

            // 14. 同じ教室。机に伏せていて、目を開けると眩しく黒板の字が読めない
            entries.Add(Of("男　16　『マテオ』　2156/03/03 11:39", DiveIds.Classroom, 25f, 1.20f,
                Blur.Far, 0.7f, 0.8f, Tone(1.00f, 1.00f, 0.92f), 0f, false,
                Who("Neighbour", 13), Who("Teacher", 7)));

            // 15. 団地の部屋。テレビの前に座ったまま、新聞を持った夫が隣に座る
            entries.Add(Of("女　63　『エレナ』　2156/03/02 09:03", DiveIds.Estate, 25f, 1.15f,
                Blur.Near, 0.6f, 0.7f, Tone(0.92f, 0.90f, 0.86f), Aged, false,
                Who("Husband", 8)));

            // 会話は番号で結ぶ。人ごとの Of の引数に混ぜると一行が長くなりすぎて、
            // 設計書 7 節と突き合わせられなくなる
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.said = i < Talks.Length ? Talks[i] : new Said[0];
                entries[i] = e;
            }

            return entries;
        }

        /// <summary>
        /// 記憶ごとの会話。設計書 7 節の写し。秒は記憶の頭から。
        ///
        /// **独白は入れない。** 顔は見せないので、誰が喋っているかは声の向きと
        /// ここの名前でしか伝わらない。話者と鉤括弧はこの文字列に含めて、
        /// <see cref="HudView"/> の字幕帯へそのまま流す
        /// </summary>
        static readonly Said[][] Talks =
        {
            // 1. メイ
            new[]
            {
                At(0f, "母「メイ！　置いていくよ」"),
                At(4f, "メイ「待って、まだ結んでる」"),
                At(20f, "母「ほら、来なさい」"),
                At(26f, "メイ「重い？」"),
                At(30f, "母「重くなった」"),
                At(40f, "母「気をつけて行くんだよ」"),
            },
            // 2. ハンナ
            new[]
            {
                At(0f, "ジョルジョ「ハンナ。おはよう」"),
                At(3f, "ハンナ「おはようございます」"),
                At(9f, "メイ「ママ！　体操着！」"),
                At(13f, "ハンナ「もう。ほら」"),
                At(19f, "ハンナ「行ってらっしゃい」"),
            },
            // 3. アルベルト
            new[]
            {
                At(0f, "ローザ「アルベルト、帰りましょう」"),
                At(5f, "ソフィア「おじいちゃん、手」"),
                At(10f, "アルベルト「ああ、ありがとう」"),
                At(26f, "ソフィア「これあげる」"),
                At(32f, "アルベルト「……石か」"),
                At(36f, "ソフィア「白いの。きれいでしょ」"),
                At(48f, "アルベルト「とっておくよ」"),
            },
            // 4. ソフィア
            new[]
            {
                At(0f, "アルベルト「ソフィア、そろそろだ」"),
                At(4f, "ソフィア「うん。立てる？」"),
                At(9f, "アルベルト「ゆっくりならな」"),
                At(18f, "ソフィア「はい、これ」"),
                At(23f, "アルベルト「……石か」"),
            },
            // 5. エミリー
            new[]
            {
                At(0f, "プリヤ「エミリー。あの、本」"),
                At(5f, "エミリー「ああ、もう読んだの」"),
                At(11f, "プリヤ「はい。面白かったです」"),
                At(17f, "エミリー「次のも持ってこようか」"),
                At(23f, "プリヤ「いいんですか」"),
            },
            // 6. マーク
            new[]
            {
                At(0f, "リンダ「マーク、お弁当」"),
                At(4f, "マーク「そこ置いといて」"),
                At(12f, "ダニエル「おはよう」"),
                At(16f, "リンダ「はい、あなたの」"),
                At(22f, "ダニエル「いってきます」"),
                At(28f, "リンダ「傘は」"),
                At(31f, "ダニエル「いらない」"),
            },
            // 7. リンダ
            new[]
            {
                At(0f, "マーク「リンダ、これどこだ」"),
                At(4f, "リンダ「上の棚」"),
                At(11f, "ダニエル「おはよう」"),
                At(15f, "リンダ「はい、お弁当」"),
                At(21f, "ダニエル「いってきます」"),
            },
            // 8. リー
            new[]
            {
                At(0f, "アイシャ「リー先生。ここが分かりません」"),
                At(5f, "リー「どこだ」"),
                At(9f, "アイシャ「この式の、三行目」"),
                At(15f, "リー「ああ、符号だな」"),
                At(24f, "リー「おい、起きろ」"),
                At(29f, "マテオ「……はい」"),
            },
            // 9. ジョルジョ
            new[]
            {
                At(0f, "エレナ「ジョルジョ、新聞」"),
                At(4f, "ジョルジョ「今取ってる」"),
                At(11f, "ハンナ「おはようございます」"),
                At(15f, "ジョルジョ「おはよう。今日は早いね」"),
            },
            // 10. ローザ
            new[]
            {
                At(0f, "アルベルト「ローザ、行くぞ」"),
                At(5f, "ローザ「はいはい」"),
                At(11f, "ローザ「ルーカス！　戻りなさい」"),
                At(17f, "ルーカス「はと、いっぱい」"),
                At(23f, "ローザ「ほら、手を」"),
            },
            // 11. ルーカス
            new[]
            {
                At(0f, "ローザ「ルーカス、戻りなさい」"),
                At(5f, "ルーカス「はと、いっぱい」"),
                At(12f, "ローザ「はい、これあげて」"),
                At(19f, "ルーカス「いっぱいきた」"),
            },
            // 12. プリヤ
            new[]
            {
                At(0f, "エミリー「プリヤ？」"),
                At(4f, "プリヤ「あ、すみません。本、ありがとうございました」"),
                At(12f, "エミリー「どうだった」"),
                At(17f, "プリヤ「面白かったです」"),
            },
            // 13. ダニエル
            new[]
            {
                At(0f, "リンダ「ダニエル、遅れるよ」"),
                At(4f, "ダニエル「分かってる」"),
                At(11f, "リンダ「はい、お弁当」"),
                At(17f, "ダニエル「いってきます」"),
            },
            // 14. アイシャ
            new[]
            {
                At(0f, "リー「アイシャ。どうした」"),
                At(4f, "アイシャ「ここが分かりません」"),
                At(10f, "リー「ああ、符号だな」"),
                At(17f, "リー「おい、起きろ」"),
            },
            // 15. マテオ
            new[]
            {
                At(0f, "アイシャ「マテオ。起きて」"),
                At(5f, "マテオ「……ん」"),
                At(11f, "リー「おい、起きろ」"),
                At(16f, "マテオ「……はい」"),
            },
            // 16. エレナ
            new[]
            {
                At(0f, "ジョルジョ「エレナ、新聞」"),
                At(4f, "エレナ「そこ置いて」"),
                At(11f, "ジョルジョ「隣の子、また走ってたよ」"),
                At(17f, "エレナ「元気ねえ」"),
            },
        };

        static Said At(float at, string line)
        {
            return new Said { at = at, line = line };
        }

        /// <summary>色味は Volume の Color Filter に入るので、透明度は触らない</summary>
        static Color Tone(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        static Seen Who(string name, int target)
        {
            return new Seen { name = name, target = target };
        }

        static DiveEntry Of(string row, string place, float length, float eyeHeight,
            Blur blur, float blurAmount, float speed, Color tint, float muffle, bool heartbeat,
            params Seen[] seen)
        {
            return new DiveEntry
            {
                row = row,
                place = place,
                length = length,
                eyeHeight = eyeHeight,
                blur = blur,
                blurAmount = blurAmount,
                speed = speed,
                tint = tint,
                muffle = muffle,
                heartbeat = heartbeat,
                seen = seen ?? new Seen[0],
                said = new Said[0],
            };
        }

        [MenuItem("HalfAware/Write the dive roster", false, 249)]
        public static void Write()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DiveRoster>(Path);
            var made = asset == null;
            if (made) asset = ScriptableObject.CreateInstance<DiveRoster>();

            var entries = Entries();

            var so = new SerializedObject(asset);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("row").stringValue = entries[i].row;
                e.FindPropertyRelative("place").stringValue = entries[i].place;
                e.FindPropertyRelative("length").floatValue = entries[i].length;
                e.FindPropertyRelative("eyeHeight").floatValue = entries[i].eyeHeight;
                e.FindPropertyRelative("blur").enumValueIndex = (int)entries[i].blur;
                e.FindPropertyRelative("blurAmount").floatValue = entries[i].blurAmount;
                e.FindPropertyRelative("speed").floatValue = entries[i].speed;
                e.FindPropertyRelative("tint").colorValue = entries[i].tint;
                e.FindPropertyRelative("muffle").floatValue = entries[i].muffle;
                e.FindPropertyRelative("heartbeat").boolValue = entries[i].heartbeat;
                Fill(e.FindPropertyRelative("seen"), entries[i].seen);
                Lines(e.FindPropertyRelative("said"), entries[i].said);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, Path);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            var said = 0;
            foreach (var e in entries) said += e.said == null ? 0 : e.said.Length;
            Debug.Log(string.Format("場面 4 の記憶の一覧を書き出した。{0} 人、会話 {1} 行 → {2}",
                entries.Count, said, Path));
        }

        static void Lines(SerializedProperty list, Said[] said)
        {
            var n = said == null ? 0 : said.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++)
            {
                var p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("at").floatValue = said[i].at;
                p.FindPropertyRelative("line").stringValue = said[i].line;
            }
        }

        static void Fill(SerializedProperty list, Seen[] people)
        {
            var n = people == null ? 0 : people.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++)
            {
                var p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("name").stringValue = people[i].name;
                p.FindPropertyRelative("target").intValue = people[i].target;
            }
        }
    }
}
