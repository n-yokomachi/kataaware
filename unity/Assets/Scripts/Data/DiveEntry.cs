using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>ぼやけ方。老眼は近くが、近視は遠くがぼやける</summary>
    public enum Blur { Sharp, Near, Far }

    /// <summary>記憶の中で見える人ひとり。板が出る相手と、板から飛ぶ先</summary>
    [Serializable]
    public struct Seen
    {
        [Tooltip("Take の下の GameObject の名前")]
        public string name;
        [Tooltip("飛び先。一覧での番号（0 始まり）")]
        public int target;
    }

    /// <summary>
    /// 記憶の中で交わされる一行と、その行を交わしている相手。
    /// 顔は見せないので、誰が喋っているかは声の向きとこの文字列の名前でしか伝わらない。
    /// 話者と鉤括弧は line に含める。
    ///
    /// **会話は人を選んで進める。** 場所の点へ入ると出る作りは、いつ何が出るのか
    /// 読めないと差し戻された（設計書 2・7 節）。二行目からは、相手に目を留めて E で始め、
    /// 一行ずつ E で送る。どの行を誰と交わすかを partner が持つ
    /// </summary>
    [Serializable]
    public struct Said
    {
        [Tooltip("話者と鉤括弧つきの一行")]
        public string line;
        [Tooltip("この行を交わしている人。Take の下の GameObject の名前。空なら相手を持たない行で、流さない")]
        public string partner;
        [Tooltip("この行の前に〔区切り〕がある。同じ相手でもここで会話を切る。次の会話は主が行き先まで歩いてから始まる")]
        public bool cut;

        /// <summary>相手を持つか。持たない行は会話に数えず、流さない</summary>
        public bool Partnered { get { return !string.IsNullOrEmpty(partner); } }
    }

    /// <summary>
    /// 会話ひとつ。二行目から、相手が同じ行が続く所。〔区切り〕（<see cref="Said.cut"/>）があればそこでも切る。
    /// 相手を持たない行は飛ばして数えるので、lines の番号は続いているとは限らない
    /// </summary>
    public struct Exchange
    {
        /// <summary>この会話の相手。Take の下の GameObject の名前</summary>
        public string partner;
        /// <summary>この会話で出す行。<see cref="DiveEntry.said"/> での番号を並びの順に</summary>
        public int[] lines;
        /// <summary>
        /// 〔区切り〕の後の会話なら、記憶の頭から数えて何番目の区切りか（0 始まり）。区切りの後でなければ -1。
        /// 行き先は <see cref="Take.Stop"/> がこの番号で持つ
        /// </summary>
        public int stop;

        /// <summary>〔区切り〕の後の会話か。始めるには、主が行き先まで歩いてきている要る</summary>
        public bool Cut { get { return stop >= 0; } }
    }

    /// <summary>記憶一つ分の値。場所と人の形はシーン（Take）が持ち、ここは数と文字だけ</summary>
    [Serializable]
    public struct DiveEntry
    {
        [Tooltip("右上と板に出す行。場面 3 の列と同じ書式")]
        public string row;
        [Tooltip("場所の id。DiveIds.Places のどれか")]
        public string place;
        [Tooltip("鍵打ちと人の動きが一巡する秒。記憶はここで終わらない")]
        public float length;
        [Tooltip("目の高さ。m")]
        public float eyeHeight;
        public Blur blur;
        [Tooltip("ぼやけの強さ。0〜1")]
        public float blurAmount;
        [Tooltip("体の動きの速さ。基準 1")]
        public float speed;
        [Tooltip("色味。Volume の Color Filter に入れる")]
        public Color tint;
        [Tooltip("耳の詰まり。0 で素、1 で低域だけ")]
        public float muffle;
        public bool heartbeat;
        public Seen[] seen;
        [Tooltip("記憶の中の会話。一行目は名を呼ぶ声。設計書 7 節")]
        public Said[] said;

        // ---- 会話の決まり（設計書 7 節） -------------------------------------
        //
        // 一行目は名を呼ぶ声で、記憶に入った瞬間に出て、決まった秒で消える。送らない。
        // 二行目からは、相手が同じ行が続く所を一つの会話とし、並びの順にしか始められない。
        // 板は、会話の相手にはその人との会話が済んでから、会話を持たない人には
        // その記憶の会話が全部済んでから出す。
        //
        // 進み具合は「済んだ会話の数」（done）一つで持つ。会話は順にしか進まないので、
        // それだけで誰と何が済んだかが決まる

        /// <summary>
        /// 記憶に入ってから since 秒のときに出しておく一行目。hold 秒を過ぎたら null。
        /// 一行も無ければ null
        /// </summary>
        public static string Calling(Said[] said, float since, float hold)
        {
            if (said == null || said.Length == 0) return null;
            return since < hold ? said[0].line : null;
        }

        /// <summary>
        /// 二行目からを会話に分ける。相手が同じ行が続く所を一つとし、並びの順に返す。
        ///
        /// **一行目は相手を持っていても数えない。** 記憶に入った瞬間に勝手に出る声で、
        /// 人を選んで始めるものではないから。
        /// **相手を持たない行は飛ばす。** 会話に数えず、流さない。前後が同じ相手なら、
        /// 飛ばした行を挟んでも一つの会話のまま続く。
        /// **〔区切り〕では同じ相手でも切る**（設計書 7 節）。区切りの後の会話は <see cref="Exchange.stop"/> に
        /// 区切りの番号を持ち、主が行き先まで歩いてくるまで始められない。
        /// 相手を持たない行に付いた区切りは、次の相手を持つ行へ持ち越す
        /// </summary>
        public static Exchange[] Exchanges(Said[] said)
        {
            var all = new List<Exchange>();
            if (said == null) return all.ToArray();
            var lines = new List<int>();
            string partner = null;
            var stop = -1;
            var cuts = 0;
            var pending = false;
            for (var k = 1; k < said.Length; k++)
            {
                if (said[k].cut) pending = true;
                if (!said[k].Partnered) continue;
                if (partner != null && (said[k].partner != partner || pending))
                {
                    all.Add(new Exchange { partner = partner, lines = lines.ToArray(), stop = stop });
                    lines.Clear();
                }
                if (lines.Count == 0) stop = pending ? cuts++ : -1;
                pending = false;
                partner = said[k].partner;
                lines.Add(k);
            }
            if (partner != null) all.Add(new Exchange { partner = partner, lines = lines.ToArray(), stop = stop });
            return all.ToArray();
        }

        /// <summary>done 個の会話を済ませたときに、次に始められる会話の相手。全部済んでいれば null</summary>
        public static string Next(Exchange[] talks, int done)
        {
            if (talks == null || done < 0 || done >= talks.Length) return null;
            return talks[done].partner;
        }

        /// <summary>
        /// who と会話を始められるか。**並びの順にしか始められない。**
        /// 先の会話の相手に目を留めても、手前の会話が済むまでは何も起きない
        /// </summary>
        public static bool CanTalk(Exchange[] talks, int done, string who)
        {
            var next = Next(talks, done);
            return next != null && next == who;
        }

        /// <summary>who がこの記憶のどこかの会話の相手か</summary>
        public static bool Partner(Exchange[] talks, string who)
        {
            if (talks == null || string.IsNullOrEmpty(who)) return false;
            for (var i = 0; i < talks.Length; i++)
                if (talks[i].partner == who) return true;
            return false;
        }

        /// <summary>
        /// who との会話は済んだか。同じ人と二度話す記憶では、その人との最後の会話まで済んで初めて true。
        /// 会話を持たない人は、済ませる会話が無いので false
        /// </summary>
        public static bool Finished(Exchange[] talks, int done, string who)
        {
            if (!Partner(talks, who)) return false;
            for (var i = talks.Length - 1; i >= 0; i--)
                if (talks[i].partner == who) return i < done;
            return false;
        }

        /// <summary>その記憶の会話は全部済んだか。会話を持たない記憶は初めから済んでいる</summary>
        public static bool AllDone(Exchange[] talks, int done)
        {
            return talks == null || done >= talks.Length;
        }

        /// <summary>
        /// who の脇に板を出してよいか。会話の相手なら、その人との会話が済んだあと。
        /// 会話を持たない人なら、その記憶の会話が全部済んだあと。
        ///
        /// **会話は一通り必ず流す。** 相手の話を聞き終える前に板を出すと、
        /// 話の途中で他人の頭へ移れてしまう（設計書 7 節）
        /// </summary>
        public static bool MayDive(Exchange[] talks, int done, string who)
        {
            if (string.IsNullOrEmpty(who)) return false;
            return Partner(talks, who) ? Finished(talks, done, who) : AllDone(talks, done);
        }
    }
}
