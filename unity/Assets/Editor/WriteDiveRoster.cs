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

            // 3. 同じ公園。祖父を引っ張って立たせ、拾った石を握らせる。
            // 芝生の奥をイヤホンをした少女（11 のプリヤ）が横切る。公園から電車へ出る口はここ一つ
            entries.Add(Of("女　7　『ソフィア』　2156/03/02 15:47", DiveIds.Park, 30f, 1.05f,
                Blur.Sharp, 0f, 1.4f, Tone(1.00f, 0.96f, 0.84f), 0f, true,
                Who("Grandfather", 2), Who("Grandmother", 9), Who("Passerby", 11)));

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

            // 12. 同じ家の階段。母に呼ばれて降り、昼食の袋を受け取って出ていく。
            // 玄関の外で同級生（13 のアイシャ）が待っている。台所から教室へ出る口はここ一つ
            entries.Add(Of("男　15　『ダニエル』　2156/03/03 06:56", DiveIds.Kitchen, 25f, 1.65f,
                Blur.Sharp, 0f, 1.2f, Tone(0.90f, 0.95f, 1.00f), 0f, false,
                Who("Mother", 6), Who("Father", 5), Who("Classmate", 13)));

            // 13. 同じ教室。手を挙げていて先生が近づいてくる
            entries.Add(Of("女　16　『アイシャ』　2156/03/03 11:38", DiveIds.Classroom, 25f, 1.20f,
                Blur.Sharp, 0f, 1.1f, Tone(1.00f, 1.00f, 0.96f), 0f, false,
                Who("Teacher", 7), Who("Neighbour", 14)));

            // 14. 同じ教室。机に伏せていて、目を開けると眩しく黒板の字が読めない
            entries.Add(Of("男　16　『マテオ』　2156/03/03 11:39", DiveIds.Classroom, 25f, 1.20f,
                Blur.Far, 0.7f, 0.8f, Tone(1.00f, 1.00f, 0.92f), 0f, false,
                Who("Neighbour", 13), Who("Teacher", 7)));

            // 15. 公営住宅の居間。肘掛け椅子に座ったまま、新聞を持った夫が入ってくる。
            // 開いた玄関の外のデッキを、買い物袋を提げた老女（9 のローザ）が通る。
            // 公営住宅から公園へ出る口はここ一つ（設計書 6 節の通りすがり）
            entries.Add(Of("女　63　『エレナ』　2156/03/02 09:03", DiveIds.Estate, 25f, 1.15f,
                Blur.Near, 0.6f, 0.7f, Tone(0.92f, 0.90f, 0.86f), Aged, false,
                Who("Husband", 8), Who("Shopper", 9)));

            // 会話は番号で結ぶ。人ごとの Of の引数に混ぜると一行が長くなりすぎて、
            // 設計書 7 節と突き合わせられなくなる
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.said = Talk(i);
                entries[i] = e;
            }

            return entries;
        }

        /// <summary>
        /// 記憶 i の会話。一行目は名前を呼ばれる声なので相手を持たず、記憶に入った瞬間に出る。
        /// 二行目からは <see cref="PartnerOf"/> の相手を付ける
        /// </summary>
        static Said[] Talk(int i)
        {
            var talk = i < Talks.Length ? Talks[i] : new string[0];
            var said = new Said[talk.Length];
            for (var k = 0; k < talk.Length; k++)
            {
                said[k].line = talk[k];
                said[k].partner = k == 0 ? "" : PartnerOf(i, k);
            }
            return said;
        }

        /// <summary>
        /// 記憶 i の k 行目（0 始まり）を交わしている人。<c>BuildDive</c> が記憶に置く人の名前
        /// （<c>Cast(take, "Mother", ...)</c> の一つ目の文字列）と一字も違えない。空なら相手を持たない。
        ///
        /// 相手が同じ行が続く所が一つの会話になり、プレイヤーはその人に目を留めて
        /// `E　話す` で始め、一行ずつ E で送る（設計書 7 節）。
        ///
        /// **いまは団地の四本（記憶 0・1・8・15）だけ。** 残りの十二本は相手を持たないので、
        /// 一行目だけ出て、板はすぐ出る。公園・電車・台所・教室は、その場所を詰めるときに付ける
        /// </summary>
        static string PartnerOf(int i, int k)
        {
            switch (i)
            {
                // 0. メイ。二行目から全部、戸口の母と
                case 0: return "Mother";
                // 1. ハンナ。二〜四行目は隣の老人（ジョルジョ）と、五〜八行目は駆け上がってくる娘と。
                // 娘は四行目（ハンナ「ええ、午後からで」）が出たら階段を上がり始める（Mover の合図 4）
                case 1: return k <= 3 ? "Neighbour" : "Daughter";
                // 8. ジョルジョ。二行目から全部、硝子戸の奥の妻と
                case 8: return "Wife";
                // 15. エレナ。二行目から全部、新聞を持って入ってくる夫と
                case 15: return "Husband";
            }
            return "";
        }

        /// <summary>
        /// 記憶ごとの会話。設計書 7 節の写し。頭の数字は設計書の秒で、
        /// どの順で出るかの目安として残してある。
        ///
        /// **独白は入れない。** 顔は見せないので、誰が喋っているかは声の向きと
        /// ここの名前でしか伝わらない。話者と鉤括弧はこの文字列に含めて、
        /// <see cref="HudView"/> の字幕帯へそのまま流す
        /// </summary>
        static readonly string[][] Talks =
        {
            // 1. メイ
            new[]
            {
                "母「メイ！　忘れもの！」",  //  0 秒
                "メイ「えー」",  //  7 秒
                "母「はい、水筒」",  // 23 秒
                "メイ「ありがとう」",  // 27 秒
                "母「もう、毎日でしょ」",  // 31 秒
                "メイ「わっ」",  // 37 秒
                "母「気をつけてね」",  // 45 秒
                "メイ「いってきます」",  // 51 秒
            },
            // 2. ハンナ
            new[]
            {
                "ジョルジョ「ハンナさん」",  //  0 秒
                "ハンナ「おはようございます」",  //  4 秒
                "ジョルジョ「今日は遅いんだね」",  //  8 秒
                "ハンナ「ええ、午後からで」",  // 12 秒
                "メイ「ママ！」",  // 17 秒
                "ハンナ「どうしたの」",  // 20 秒
                "メイ「体操着、わすれた」",  // 23 秒
                "ハンナ「……もう」",  // 26 秒
            },
            // 3. アルベルト
            new[]
            {
                "ローザ「アルベルト、帰りましょう」",  //  0 秒
                "ソフィア「おじいちゃん、手」",  //  5 秒
                "アルベルト「ああ、ありがとう」",  // 10 秒
                "ソフィア「これあげる」",  // 26 秒
                "アルベルト「……石か」",  // 32 秒
                "ソフィア「白いの。きれいでしょ」",  // 36 秒
                "アルベルト「とっておくよ」",  // 48 秒
            },
            // 4. ソフィア
            new[]
            {
                "アルベルト「ソフィア、そろそろだ」",  //  0 秒
                "ソフィア「うん。立てる？」",  //  4 秒
                "アルベルト「ゆっくりならな」",  //  9 秒
                "ソフィア「はい、これ」",  // 18 秒
                "アルベルト「……石か」",  // 23 秒
            },
            // 5. エミリー
            new[]
            {
                "プリヤ「エミリー。あの、本」",  //  0 秒
                "エミリー「ああ、もう読んだの」",  //  5 秒
                "プリヤ「はい。面白かったです」",  // 11 秒
                "エミリー「次のも持ってこようか」",  // 17 秒
                "プリヤ「いいんですか」",  // 23 秒
            },
            // 6. マーク
            new[]
            {
                "リンダ「マーク、お弁当」",  //  0 秒
                "マーク「そこ置いといて」",  //  4 秒
                "ダニエル「おはよう」",  // 12 秒
                "リンダ「はい、あなたの」",  // 16 秒
                "ダニエル「いってきます」",  // 22 秒
                "リンダ「傘は」",  // 28 秒
                "ダニエル「いらない」",  // 31 秒
            },
            // 7. リンダ
            new[]
            {
                "マーク「リンダ、これどこだ」",  //  0 秒
                "リンダ「上の棚」",  //  4 秒
                "ダニエル「おはよう」",  // 11 秒
                "リンダ「はい、お弁当」",  // 15 秒
                "ダニエル「いってきます」",  // 21 秒
            },
            // 8. リー
            new[]
            {
                "アイシャ「リー先生。ここが分かりません」",  //  0 秒
                "リー「どこだ」",  //  5 秒
                "アイシャ「この式の、三行目」",  //  9 秒
                "リー「ああ、符号だな」",  // 15 秒
                "リー「おい、起きろ」",  // 24 秒
                "マテオ「……はい」",  // 29 秒
            },
            // 9. ジョルジョ
            new[]
            {
                "エレナ「ジョルジョ」",  //  0 秒
                "ジョルジョ「ああ」",  //  4 秒
                "エレナ「新聞、来てる？」",  //  8 秒
                "ジョルジョ「来てるよ」",  // 11 秒
                "ジョルジョ「隣、また忘れもの」",  // 16 秒
                "エレナ「あらあら」",  // 21 秒
            },
            // 10. ローザ
            new[]
            {
                "アルベルト「ローザ、行くぞ」",  //  0 秒
                "ローザ「はいはい」",  //  5 秒
                "ローザ「ルーカス！　戻りなさい」",  // 11 秒
                "ルーカス「はと、いっぱい」",  // 17 秒
                "ローザ「ほら、手を」",  // 23 秒
            },
            // 11. ルーカス
            new[]
            {
                "ローザ「ルーカス、戻りなさい」",  //  0 秒
                "ルーカス「はと、いっぱい」",  //  5 秒
                "ローザ「はい、これあげて」",  // 12 秒
                "ルーカス「いっぱいきた」",  // 19 秒
            },
            // 12. プリヤ
            new[]
            {
                "エミリー「プリヤ？」",  //  0 秒
                "プリヤ「あ、すみません。本、ありがとうございました」",  //  4 秒
                "エミリー「どうだった」",  // 12 秒
                "プリヤ「面白かったです」",  // 17 秒
            },
            // 13. ダニエル
            new[]
            {
                "リンダ「ダニエル、遅れるよ」",  //  0 秒
                "ダニエル「分かってる」",  //  4 秒
                "リンダ「はい、お弁当」",  // 11 秒
                "ダニエル「いってきます」",  // 17 秒
            },
            // 14. アイシャ
            new[]
            {
                "リー「アイシャ。どうした」",  //  0 秒
                "アイシャ「ここが分かりません」",  //  4 秒
                "リー「ああ、符号だな」",  // 10 秒
                "リー「おい、起きろ」",  // 17 秒
            },
            // 15. マテオ
            new[]
            {
                "アイシャ「マテオ。起きて」",  //  0 秒
                "マテオ「……ん」",  //  5 秒
                "リー「おい、起きろ」",  // 11 秒
                "マテオ「……はい」",  // 16 秒
            },
            // 16. エレナ
            new[]
            {
                "ジョルジョ「エレナ」",  //  0 秒
                "エレナ「なあに」",  //  4 秒
                "ジョルジョ「新聞、あったよ」",  //  8 秒
                "エレナ「そこ置いといて」",  // 12 秒
                "ジョルジョ「隣の子、また走ってた」",  // 17 秒
                "エレナ「元気ねえ」",  // 21 秒
            },
        };

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
            var partnered = 0;
            var talks = 0;
            foreach (var e in entries)
            {
                if (e.said == null) continue;
                said += e.said.Length;
                foreach (var s in e.said) if (s.Partnered) partnered++;
                talks += DiveEntry.Exchanges(e.said).Length;
            }
            Debug.Log(string.Format("場面 4 の記憶の一覧を書き出した。{0} 人、会話 {1} 行、うち相手を持つ行 {2}、会話 {3} 組 → {4}",
                entries.Count, said, partnered, talks, Path));
        }

        static void Lines(SerializedProperty list, Said[] said)
        {
            var n = said == null ? 0 : said.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++)
            {
                var p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("line").stringValue = said[i].line;
                p.FindPropertyRelative("partner").stringValue = said[i].partner ?? "";
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
