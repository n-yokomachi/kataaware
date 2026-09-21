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

            return entries;
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
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, Path);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("場面 4 の記憶の一覧を書き出した。{0} 人 → {1}", entries.Count, Path));
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
