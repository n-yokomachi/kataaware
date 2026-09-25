using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 3 の文面を書き出す。設計書 7.2 の文をここに持ち、
    /// `Assets/Data/ConnectScript.asset` へ流し込む。
    ///
    /// 文面をアセットへ置くのは、位置と規則（シーン）と文（アセット）を分けるため。
    /// ただし手で YAML を書くと日本語が読めなくなるので、元はこのファイルに置いて
    /// メニューから書き出す。**台詞を直すならここを直して走らせ直す。**
    ///
    /// ソファのメモだけは設計書に文が無い。場面 1 のメモへ今日のぶんを足した仮のもので、
    /// 文章はオーナーが決める
    /// </summary>
    public static class WriteConnectScript
    {
        const string Path = "Assets/Data/ConnectScript.asset";

        /// <summary>
        /// 今日売った枚数。場面 2 で売り切るので、売れ行きの側から出す。
        /// 文字で「6枚」と書くと、露店の売れ行きを直したときにここだけ取り残される
        /// </summary>
        public static int SoldToday
        {
            get { return MarketSale.Chips - MarketSale.Left(MarketSale.Count); }
        }

        /// <summary>ソファのメモ。書き足す前の並びと、書き足した後の並びを続けて出す</summary>
        public static string[] NoteLines
        {
            get
            {
                return new[]
                {
                    "売り上げのメモだ。今日のぶんがまだ空いている",
                    "2166/08/13　5枚\n" +
                    "2166/08/14　4枚\n" +
                    "2166/08/15　―",
                    SoldToday + "枚。書き足しておく",
                    "2166/08/13　5枚\n" +
                    "2166/08/14　4枚\n" +
                    "2166/08/15　" + SoldToday + "枚",
                };
            }
        }

        /// <summary>モニターに並ぶ走査条件と候補。場面 1 で見せた条件で絞られた側を見せる</summary>
        public static readonly string[] MonitorLines =
        {
            "モニターにクラック対象の個人情報リストが並んでいる",
            "条件　名前を呼ばれた時刻\n" +
            "対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日\n" +
            "候補の簡易抽出　56,232,318",
            "女　6　『メイ』　2156/03/02 07:14\n" +
            "女　34　『ハンナ』　2156/03/02 09:02\n" +
            "男　78　『アルベルト』　2156/03/02 15:47\n" +
            "女　19　『エミリー』　2156/03/02 18:20\n" +
            "男　52　『マーク』　2156/03/03 06:55\n" +
            "男　41　『リー』　2156/03/03 11:38",
        };

        /// <summary>
        /// リストを送るあいだの独白。設計書 7.2 の並びのまま。
        ///
        /// ルビは青空文庫の記法で持つ。書式の指定を文面に書くと、
        /// 字幕の折り返しがタグを字数に数えて行が崩れる。
        /// 設計書の「しばらく間を開けて」は文ではなく間なので、ここには入れない。
        /// 空行を混ぜると、空の字幕のページとして出てしまう
        /// </summary>
        public static readonly string[] ListLines =
        {
            "思い出の価値がどれほどのものか、私には分からない",
            "それが、他人の思い出を数えきれないほど盗み見てきて感覚が麻痺したからなのか、それとも私自身の思い出がなにもないからなのかはもまた、私には分からない",
            "私にはある日を境に記憶がない",
            "気づいたときにはこの｜倫敦《ロンドン》の路地に突っ立っていた。まるで突然大人の姿でこの世に生まれてきたみたいなのに、言葉もナーヴ・ターミナルも使い方は分かっているのだからおかしな感じがしたものだった",
            "その瞬間の驚きや嘆かわしさや言い表しようのない疼きを表そうにも、赤子のように泣きわめくこともできず、なんとか言葉にしようとしてそれができないのだからもどかしかった",
            "だからたとえ食っていくだけの金が稼げても、私は他人の思い出を除いて回るのをやめられない。毎日大半の時間はこの椅子に座って過ごした。毎日数十人の頭を覗き見た",
            "どこかに自分の痕跡を探して。誰かの記憶に自分の姿を探して",
        };

        /// <summary>
        /// 書き出す項目をそのまま組んで返す。Write はこれをアセットへ流し込むだけにしてある。
        /// 分けてあるのは、見直しが「いま書き出すもの」とアセットの中身を
        /// 突き合わせられるようにするため
        /// </summary>
        public static List<ScriptEntry> Entries()
        {
            var entries = new List<ScriptEntry>();

            entries.Add(Say(ConnectIds.Note, "メモを見る", NoteLines));

            // 玄関先のコートハンガーにジャケットを掛ける。文は持たず、掛ける音と見た目は ConnectDirector が出す。ラベルは仮置き
            entries.Add(Say(ConnectIds.Coat, "ジャケットを掛ける", new string[0]));

            // 座る演出がすぐに続くので短く
            entries.Add(Say(ConnectIds.Chair, "椅子に座る", new[] { "椅子に座る" }));

            entries.Add(Say(ConnectIds.Jack, "ケーブルを繋ぐ", new[]
            {
                "手首のジャックにケーブルを挿す。冷たい感触が皮膚の下に侵入してくる",
            }));

            entries.Add(Say(ConnectIds.Monitor, "リストを見る", MonitorLines));
            entries.Add(Say(ConnectIds.List, "リストを送る", ListLines));

            // 潜れば場面が閉じて戻れないので、二択で確かめる。
            // 二択を出すのはここだけ。他が出すと SceneProgress が済んだことにしなくなる
            var dive = Say(ConnectIds.Dive, "潜る", new string[0]);
            dive.choice = new ScriptChoice { question = "潜る", afterYes = new string[0] };
            entries.Add(dive);

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

        [MenuItem("HalfAware/Write the connect script", false, 239)]
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
            Debug.Log(string.Format("場面 3 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty list, string[] values)
        {
            var n = values == null ? 0 : values.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++) list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
