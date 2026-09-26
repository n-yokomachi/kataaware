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

            // 6. 同じ台所の戸口。夫に棚の場所を教え、夫が先に玄関から出ていき、息子を送り出す
            entries.Add(Of("女　49　『リンダ』　2156/03/03 06:56", DiveIds.Kitchen, 30f, 1.58f,
                Blur.Sharp, 0f, 1.0f, Tone(0.90f, 0.95f, 1.00f), 0f, false,
                Who("Husband", 5), Who("Son", 12)));

            // 7. 昼の教室。手を挙げた生徒のノートを覗き、隣の居眠りを叩く
            entries.Add(Of("男　41　『リー』　2156/03/03 11:38", DiveIds.Classroom, 35f, 1.70f,
                Blur.Sharp, 0f, 1.0f, Tone(1.00f, 1.00f, 0.96f), 0f, false,
                Who("Pupil", 13), Who("Sleeper", 14)));

            // 8. 団地の廊下。新聞を取りに出て隣の母親を眺めていたところで、居間の妻に呼ばれる。
            // 新聞を持って中へ入り、居間で妻と話す
            entries.Add(Of("男　66　『ジョルジョ』　2156/03/02 09:03", DiveIds.Estate, 25f, 1.65f,
                Blur.Near, 0.6f, 0.7f, Tone(0.92f, 0.90f, 0.86f), Aged, false,
                Who("Wife", 15), Who("Mother", 1)));

            // 9. 公園の門。隣を歩いていた孫息子が鳩を追って駆け出し、呼び戻して餌の袋を持たせる。孫は門の前で撒く
            entries.Add(Of("女　72　『ローザ』　2156/03/02 15:50", DiveIds.Park, 30f, 1.50f,
                Blur.Near, 0.6f, 0.6f, Tone(0.98f, 0.94f, 0.82f), Aged, false,
                Who("Grandson", 10), Who("Husband", 2), Who("Granddaughter", 3)));

            // 10. 同じ公園。鳩を追って池の縁まで来ていて祖母に呼ばれ、戻って餌の袋を渡され、門の前で撒く
            entries.Add(Of("男　11　『ルーカス』　2156/03/02 15:50", DiveIds.Park, 25f, 1.32f,
                Blur.Sharp, 0f, 1.4f, Tone(1.00f, 0.98f, 0.86f), 0f, true,
                Who("Grandmother", 9), Who("Grandfather", 2)));

            // 11. 同じ電車。先輩の背中に声を掛けて本を差し出したところ
            entries.Add(Of("女　18　『プリヤ』　2156/03/02 18:20", DiveIds.Train, 25f, 1.55f,
                Blur.Sharp, 0f, 1.1f, Tone(0.92f, 0.96f, 1.00f), 0f, false,
                Who("Senior", 4)));

            // 12. 同じ家の階段。母に呼ばれて降り、昼食の袋を受け取る。父が先に玄関から出ていき、あとから出ていく。
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

        /// <summary>相手を替える印の頭。<see cref="With"/> が付ける</summary>
        const string PartnerMark = "@";

        /// <summary>
        /// 〔区切り〕の印。同じ相手でもここで会話を切る。次の会話は、主が行き先
        /// （<c>BuildDive</c> の鍵打ちの <c>Stop</c>）まで歩いてから始まる（設計書 7 節）
        /// </summary>
        const string Cut = "〔区切り〕";

        /// <summary>
        /// 相手を替える印。設計書 7 節の「**相手: ○○**」。後ろに続く行を、この人と交わす。
        /// 名前は <c>BuildDive</c> が記憶に置く人の名前（<c>Cast(take, "Mother", ...)</c> の一つ目の文字列）と一字も違えない
        /// </summary>
        static string With(string partner)
        {
            return PartnerMark + partner;
        }

        /// <summary>
        /// 記憶 i の会話。<see cref="Talks"/> の印を解いて、行ごとに相手と区切りを付ける。
        /// 一行目は名前を呼ばれる声なので相手を持たず、記憶に入った瞬間に出る。
        ///
        /// 相手が同じ行が続く所が一つの会話になり、プレイヤーはその人に目を留めて
        /// `E　話す` で始め、一行ずつ E で送る。〔区切り〕では同じ相手でも会話を切る（設計書 7 節）
        /// </summary>
        static Said[] Talk(int i)
        {
            var talk = i < Talks.Length ? Talks[i] : new string[0];
            var said = new List<Said>();
            var partner = "";
            var cut = false;
            foreach (var t in talk)
            {
                if (t == Cut) { cut = true; continue; }
                if (t.StartsWith(PartnerMark)) { partner = t.Substring(PartnerMark.Length); continue; }
                said.Add(new Said { line = t, partner = said.Count == 0 ? "" : partner, cut = cut });
                cut = false;
            }
            return said.ToArray();
        }

        /// <summary>
        /// 記憶ごとの会話。設計書 7 節の写しで、**台詞は一字一句そのまま**（全角の空白・句読点・記号も）。
        /// 並びも 7 節のまま。頭の文字列が一行目（名を呼ぶ声）、<see cref="With"/> が「相手: ○○」、
        /// <see cref="Cut"/> が〔区切り〕。行の後ろの注は 7 節の括弧書きと区切りの説明。
        /// 7 節と食い違っていないかは <c>DiveRosterSpecTests</c> が設計書を読んで比べる。
        ///
        /// **独白は入れない。** 顔は見せないので、誰が喋っているかは声の向きと
        /// ここの名前でしか伝わらない。話者と鉤括弧はこの文字列に含めて、
        /// <see cref="HudView"/> の字幕帯へそのまま流す。
        /// 主の台詞も、相手の台詞も、そばにいる人の台詞（記憶 6 のリンダが二階へ声を掛ける行など）も、
        /// 7 節で相手を付けた会話に入れる
        /// </summary>
        static readonly string[][] Talks =
        {
            // 1. メイ（6）── 公営住宅の外階段、07:14
            new[]
            {
                "ハンナ「メイ！　忘れもの！　上がっておいで！」",
                With("Mother"),  // ハンナ（階段の下から見上げて）
                "メイ「えーなにー？」",
                "ハンナ「水筒！」",
                "メイ「投げてよー」",
                "ハンナ「投げません。いいから上がっておいで」",
                Cut,  // 三階まで駆け上がる
                With("Mother"),  // ハンナ（三階の戸口）
                "ハンナ「水筒忘れるの毎日でしょ」",
                "メイ「きのうは忘れなかったもん」",
                "ハンナ「おとといは？」",
                "メイ「おとといはママが入れてくれた」",
                "ハンナ「もう。ほら、それじゃあいってらっしゃい。車に気をつけてね」",
                "メイ「いってきまーす」",
            },
            // 2. ハンナ（34）── 公営住宅の踊り場、09:02
            new[]
            {
                "ジョルジョ「おはようハンナさん」",
                With("Neighbour"),  // ジョルジョ
                "ハンナ「おはようございます。冷えますね」",
                "ジョルジョ「三月とは思えないね。今日は遅いんだね」",
                "ハンナ「今日は十時からなんです」",
                "ジョルジョ「おや。誰か上がってくるぞ」",
                With("Daughter"),  // メイ
                "メイ「ママ！」",
                "ハンナ「メイ？　学校は？」",
                "メイ「体操着わすれた」",
                "ハンナ「体操着ならママが持ってる。いま届けに行くとこだった」",
                "メイ「よかったー」",
                "ハンナ「先生には言ってきたの？」",
                "メイ「うん。すぐ戻りますって」",
                "ハンナ「えらい。じゃあ走って戻りなさい」",
                "メイ「いってきます！」",
                "ハンナ「転ばないでよ」",
            },
            // 3. アルベルト（78）── 公園のベンチ、15:47
            new[]
            {
                "ローザ「アルベルト、帰りましょう」",
                With("Granddaughter"),  // ソフィア（ベンチの前でしゃがんでいる孫娘に）
                "アルベルト「ソフィア、そろそろ帰るぞ」",
                "ソフィア「はーい。つかまって」",
                "アルベルト「よっこらしょ」",
                "ソフィア「ゆっくりでいいよ」",
                "アルベルト「助かるよ」",
                Cut,  // 池の縁まで歩く
                With("Granddaughter"),  // ソフィア
                "ソフィア「見て！　鳩がいっせいに飛んだ」",
                "アルベルト「見えた見えた。すごい数だ」",
                "ソフィア「手出して。いいものあげる」",
                "アルベルト「石か。すべすべしてるな」",
                "ソフィア「白いの。さっきベンチの前で見つけたの」",
                "アルベルト「よく見えないな。でも形がきれいだ」",
                "ソフィア「なくさないでね」",
                "アルベルト「ありがとう。大事にするよ」",
                With("Wife"),  // ローザ（門の前）
                "ローザ「あなた前が開いてるわよ」",
                "アルベルト「本当だ」",
                "ローザ「風が出てきたわ。早く帰りましょう」",
            },
            // 4. ソフィア（7）── 同じ公園、15:47
            new[]
            {
                "アルベルト「ソフィア、そろそろ帰るぞ」",
                With("Grandfather"),  // アルベルト
                "ソフィア「はーい。つかまって」",
                "アルベルト「よっこらしょ」",
                "ソフィア「ゆっくりでいいよ」",
                "アルベルト「助かるよ」",
                Cut,  // 池の縁まで歩く
                With("Grandfather"),  // アルベルト
                "ソフィア「見て！　鳩がいっせいに飛んだ」",
                "アルベルト「見えた見えた。すごい数だ」",
                "ソフィア「手出して。いいものあげる」",
                "アルベルト「石か。すべすべしてるな」",
                "ソフィア「白いの。さっきベンチの前で見つけたの」",
                "アルベルト「よく見えないな。でも形がきれいだ」",
                "ソフィア「なくさないでね」",
                "アルベルト「ありがとう。大事にするよ」",
                With("Grandmother"),  // ローザ
                "ローザ「おじいちゃんを引っぱりすぎないの」",
                "ソフィア「引っぱってないよ。手つないでるだけ」",
            },
            // 5. エミリー（19）── 電車、18:20
            new[]
            {
                "プリヤ「エミリー！　待って」",
                With("Junior"),  // プリヤ
                "エミリー「プリヤ？　同じ電車だったんだ」",
                "プリヤ「借りてた本をちょうど持ってて」",
                "エミリー「もう読んだの？　早いね」",
                "プリヤ「止まらなくて。ゆうべ最後まで」",
                "エミリー「どうだった？　最後のとこ」",
                "プリヤ「ずるいですよ。泣きました」",
                "プリヤ「わっ」",
                "エミリー「大丈夫？　掴まってていいよ」",
                "プリヤ「すみません……」",
                "エミリー「続きもあるよ。持ってこようか」",
                "プリヤ「いいんですか。お願いします」",
                "エミリー「じゃあまた来週。次で降りるね」",
                "プリヤ「はい。また来週」",
            },
            // 6. マーク（52）── 台所、06:55
            new[]
            {
                "リンダ「マーク、ランチ忘れてる」",
                With("Wife"),  // リンダ
                "マーク「助かった。中身は？」",
                "リンダ「ゆうべのチキンの残り。文句は受け付けません」",
                "マーク「言ってないだろ」",
                "マーク「リンダ、大皿ってどこにしまうんだっけ」",
                "リンダ「上の棚。右の扉」",
                "マーク「毎朝聞いてる気がする」",
                "リンダ「毎朝答えてるわよ」",
                "リンダ「ダニエル、七時のバスでしょ」",
                "ダニエル「分かってる」",
                With("Son"),  // ダニエル（階段から降りてくる）
                "ダニエル「おはよ」",
                "マーク「寝ぐせがすごいぞ」",
                "ダニエル「知ってる」",
                Cut,  // 玄関で靴を履き、戸を開けて振り返る
                With("Wife"),  // リンダ
                "マーク「行ってくる」",
                "リンダ「傘持った？　午後から降るって」",
                "マーク「鞄に入ってる」",
            },
            // 7. リンダ（49）── 同じ台所の戸口、06:56
            new[]
            {
                "マーク「リンダ、大皿ってどこにしまうんだっけ」",
                With("Husband"),  // マーク
                "リンダ「上の棚。右の扉」",
                "マーク「毎朝聞いてる気がする」",
                "リンダ「毎朝答えてるわよ」",
                With("Son"),  // ダニエル（階段を見上げて）
                "リンダ「ダニエル、七時のバスでしょ」",
                "ダニエル「分かってる」",
                Cut,  // 息子が降りてくる
                With("Son"),  // ダニエル
                "ダニエル「おはよ」",
                "マーク「寝ぐせがすごいぞ」",
                "ダニエル「知ってる」",
                "リンダ「ダニエルのランチ。今日は残さないで」",
                "ダニエル「中身なに？」",
                "リンダ「チキンのサンドイッチ。お父さんと同じ」",
                With("Husband"),  // マーク（玄関）
                "マーク「行ってくる」",
                "リンダ「傘持った？　午後から降るって」",
                "マーク「鞄に入ってる」",
                With("Son"),  // ダニエル（玄関）
                "ダニエル「いってきます」",
                "リンダ「いってらっしゃい。前見て歩くのよ」",
            },
            // 8. リー（41）── 教室、11:38
            new[]
            {
                "アイシャ「リー先生。三問目が分かりません」",
                With("Pupil"),  // アイシャ（ホワイトボードの前から）
                "リー「アイシャ、どこだ。見せてみろ」",
                Cut,  // 机の列を歩いて近づく
                With("Pupil"),  // アイシャ
                "アイシャ「三問目の三行目です」",
                "リー「符号だな。移項したらマイナスになる」",
                "アイシャ「そっか」",
                "リー「符号のほかは合ってる。いい線だ」",
                "アイシャ「ありがとうございます」",
                "アイシャ「マテオ、起きて。先生来てる」",
                With("Sleeper"),  // マテオ
                "リー「マテオ。起きろ」",
                "マテオ「起きてます」",
                "リー「目が開いてないぞ」",
                "マテオ「まぶしくて」",
                "リー「ならいちばん前の席に来るか？」",
                "マテオ「起きます」",
            },
            // 9. ジョルジョ（66）── 公営住宅の廊下、09:03
            new[]
            {
                "エレナ「ジョルジョ、早く閉めて。寒いわ」",
                With("Wife"),  // エレナ（居間まで入ってから）
                "ジョルジョ「隣の子がまた忘れものだ」",
                "エレナ「あらあら。今度はなに？」",
                "ジョルジョ「体操着だとさ。母親が抱き上げてたよ」",
                "エレナ「いいわねえ。あなたもよく抱っこしてたじゃない」",
                "ジョルジョ「腰が元気なころの話だ」",
                "ジョルジョ「ハンナさんは今日十時からだそうだ」",
                "エレナ「じゃあ帰ってくるころにスープでも持っていこうかしら」",
                "ジョルジョ「また世話を焼く」",
                "エレナ「焼かせてちょうだい。ほかに焼く相手もいないんだから」",
                "エレナ「眼鏡知らない？」",
                "ジョルジョ「頭の上だ」",
                "エレナ「あら」",
            },
            // 10. ローザ（72）── 公園、15:50
            new[]
            {
                "アルベルト「ローザ、行くぞ」",
                With("Husband"),  // アルベルト（門の柱に手を置いて振り返っている夫に）
                "ローザ「はいはい。膝が言うことを聞かないのよ」",
                "アルベルト「ベンチに座りすぎたんだ」",
                "ローザ「あなたに言われたくないわ」",
                With("Grandson"),  // ルーカス（池の方へ駆け出した孫を呼ぶ）
                "ローザ「ルーカス！　戻りなさい」",
                "ルーカス「今行くって。鳩がすごいんだよ」",
                Cut,  // 孫が戻ってくる
                With("Grandson"),  // ルーカス
                "ローザ「餌の袋持ってて」",
                "ルーカス「まだ入ってるよ。撒いていい？」",
                "ローザ「門のとこでね。全部どうぞ」",
            },
            // 11. ルーカス（11）── 同じ公園、15:50
            new[]
            {
                "ローザ「ルーカス！　戻りなさい」",
                With("Grandmother"),  // ローザ（池の縁から）
                "ルーカス「今行くって。鳩がすごいんだよ」",
                Cut,  // 祖母のところへ戻る
                With("Grandmother"),  // ローザ
                "ローザ「餌の袋持ってて」",
                "ルーカス「まだ入ってるよ。撒いていい？」",
                "ローザ「門のとこでね。全部どうぞ」",
                Cut,  // 門の前で袋から撒く
                With("Grandmother"),  // ローザ
                "ルーカス「うわっ！　全部こっち来た」",
                "ローザ「ほらね。追いかけるから逃げるのよ」",
                "ルーカス「明日も来る？」",
                "ローザ「晴れたらね」",
                With("Grandfather"),  // アルベルト（門の前）
                "アルベルト「門を閉めるぞ」",
                "ルーカス「待って。あと一回」",
            },
            // 12. プリヤ（18）── 同じ電車、18:20
            new[]
            {
                "エミリー「プリヤ？　同じ電車だったんだ」",
                With("Senior"),  // エミリー
                "プリヤ「借りてた本をちょうど持ってて」",
                "エミリー「もう読んだの？　早いね」",
                "プリヤ「止まらなくて。ゆうべ最後まで」",
                "エミリー「どうだった？　最後のとこ」",
                "プリヤ「ずるいですよ。泣きました」",
                "プリヤ「わっ」",
                "エミリー「大丈夫？　掴まってていいよ」",
                "プリヤ「すみません……」",
                "エミリー「続きもあるよ。持ってこようか」",
                "プリヤ「いいんですか。お願いします」",
                "エミリー「じゃあまた来週。次で降りるね」",
                "プリヤ「はい。また来週」",
            },
            // 13. ダニエル（15）── 同じ家の階段、06:56
            new[]
            {
                "リンダ「ダニエル、七時のバスでしょ」",
                With("Mother"),  // リンダ（階段の途中から）
                "ダニエル「分かってる」",
                Cut,  // 階段を降りる
                With("Father"),  // マーク
                "ダニエル「おはよ」",
                "マーク「寝ぐせがすごいぞ」",
                "ダニエル「知ってる」",
                With("Mother"),  // リンダ
                "リンダ「ダニエルのランチ。今日は残さないで」",
                "ダニエル「中身なに？」",
                "リンダ「チキンのサンドイッチ。お父さんと同じ」",
                "マーク「行ってくる」",
                "リンダ「傘持った？　午後から降るって」",
                "マーク「鞄に入ってる」",
                Cut,  // 玄関で靴を履く
                With("Mother"),  // リンダ
                "ダニエル「いってきます」",
                "リンダ「いってらっしゃい。前見て歩くのよ」",
            },
            // 14. アイシャ（16）── 同じ教室、11:38
            new[]
            {
                "リー「アイシャ、どこだ。見せてみろ」",
                With("Teacher"),  // リー（先生が机の列を歩いて近づいてから）
                "アイシャ「三問目の三行目です」",
                "リー「符号だな。移項したらマイナスになる」",
                "アイシャ「そっか」",
                "リー「符号のほかは合ってる。いい線だ」",
                "アイシャ「ありがとうございます」",
                With("Neighbour"),  // マテオ
                "アイシャ「マテオ、起きて。先生来てる」",
                "マテオ「んー」",
                "リー「マテオ。起きろ」",
                "マテオ「起きてます」",
                "リー「目が開いてないぞ」",
                "マテオ「まぶしくて」",
                "リー「ならいちばん前の席に来るか？」",
                "マテオ「起きます」",
                "マテオ「いま何ページ？」",
                "アイシャ「四十二ページ。三問目」",
            },
            // 15. マテオ（16）── 同じ教室、11:39
            new[]
            {
                "アイシャ「マテオ、起きて。先生来てる」",
                With("Neighbour"),  // アイシャ
                "マテオ「んー」",
                With("Teacher"),  // リー
                "リー「マテオ。起きろ」",
                "マテオ「起きてます」",
                "リー「目が開いてないぞ」",
                "マテオ「まぶしくて」",
                "リー「ならいちばん前の席に来るか？」",
                "マテオ「起きます」",
                With("Neighbour"),  // アイシャ
                "マテオ「いま何ページ？」",
                "アイシャ「四十二ページ。三問目」",
                "マテオ「ボードの字が読めない」",
                "アイシャ「あとでノート見せてあげる」",
            },
            // 16. エレナ（63）── 公営住宅の部屋、09:03
            new[]
            {
                "ジョルジョ「エレナ、新聞来てたぞ」",
                With("Husband"),  // ジョルジョ（戸口から居間へ入ってくる）
                "エレナ「ジョルジョ、早く閉めて。寒いわ」",
                "ジョルジョ「隣の子がまた忘れものだ」",
                "エレナ「あらあら。今度はなに？」",
                "ジョルジョ「体操着だとさ。母親が抱き上げてたよ」",
                "エレナ「いいわねえ。あなたもよく抱っこしてたじゃない」",
                "ジョルジョ「腰が元気なころの話だ」",
                "ジョルジョ「ハンナさんは今日十時からだそうだ」",
                "エレナ「じゃあ帰ってくるころにスープでも持っていこうかしら」",
                "ジョルジョ「また世話を焼く」",
                "エレナ「焼かせてちょうだい。ほかに焼く相手もいないんだから」",
                "エレナ「眼鏡知らない？」",
                "ジョルジョ「頭の上だ」",
                "エレナ「あら」",
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
            var cuts = 0;
            foreach (var e in entries)
            {
                if (e.said == null) continue;
                said += e.said.Length;
                foreach (var s in e.said) if (s.Partnered) partnered++;
                foreach (var t in DiveEntry.Exchanges(e.said)) { talks++; if (t.Cut) cuts++; }
            }
            Debug.Log(string.Format("場面 4 の記憶の一覧を書き出した。{0} 人、会話 {1} 行、うち相手を持つ行 {2}、会話 {3} 組（うち区切りの後 {4}） → {5}",
                entries.Count, said, partnered, talks, cuts, Path));
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
                p.FindPropertyRelative("cut").boolValue = said[i].cut;
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
