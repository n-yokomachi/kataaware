using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>ログの行の種類。コンソールでは行の頭の札になる</summary>
    public enum LogKind
    {
        /// <summary>調べた物の文</summary>
        Examine,

        /// <summary>人と交わした台詞。名前を持つ</summary>
        Talk,

        /// <summary>主人公の独白。名前を持たない</summary>
        Monologue,

        /// <summary>二択で選んだ物</summary>
        Choice,
    }

    /// <summary>ログの 1 行。種類と、話した人（調べた物）の名前と、文</summary>
    public struct LogEntry
    {
        public LogKind kind;
        /// <summary>会話なら話した人、調べたなら調べた物の名。独白は空</summary>
        public string who;
        public string text;

        public LogEntry(LogKind kind, string who, string text)
        {
            this.kind = kind;
            this.who = who ?? string.Empty;
            this.text = text ?? string.Empty;
        }

        /// <summary>札に出す字</summary>
        public static string Tag(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Examine: return "調べる";
                case LogKind.Talk: return "会話";
                case LogKind.Monologue: return "独白";
                default: return "選ぶ";
            }
        }
    }

    /// <summary>
    /// これまでに出した文のログ。TAB のコンソールで読む。古い順に溜める。
    ///
    /// **場面ごとに持つ。** 場面を移ったら新しい場面のログから始まる（<see cref="Follow"/>）。
    /// コンソールそのものは場面をまたいで残るが、中身はここで入れ替わる。
    ///
    /// どの場面からも <see cref="Shared"/> の入口（<see cref="Examined"/>・<see cref="Said"/>・<see cref="Picked"/>）を通して足す。
    /// 場面ごとの流れ（SceneFlow・DiveDirector）はそれぞれの見せ方を持つが、ログに残す道はここひとつ
    /// </summary>
    public sealed class ConsoleLog
    {
        /// <summary>これより多くは持たない。古い方から捨てる</summary>
        public const int Keep = 400;

        /// <summary>二択の問いと選んだ物のあいだ</summary>
        public const string ChoseMark = "　▶ ";

        readonly List<LogEntry> entries = new List<LogEntry>();
        bool following;
        int scene;

        public IReadOnlyList<LogEntry> Entries { get { return entries; } }

        public int Count { get { return entries.Count; } }

        /// <summary>1 行を足す。空の文は入れない</summary>
        public void Add(LogKind kind, string who, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            entries.Add(new LogEntry(kind, who, text));
            if (entries.Count > Keep) entries.RemoveRange(0, entries.Count - Keep);
        }

        /// <summary>
        /// 行をまとめて足す。「名前「台詞」」の形の行は会話にし、名前と台詞に分ける。
        /// それ以外は plain の種類で、名前は who（独白なら空）
        /// </summary>
        public void AddLines(LogKind plain, string who, IEnumerable<string> lines)
        {
            if (lines == null) return;
            foreach (var line in lines) AddLine(plain, who, line);
        }

        public void AddLine(LogKind plain, string who, string line)
        {
            string name, said;
            if (Speech.Split(line, out name, out said)) Add(LogKind.Talk, name, said);
            else Add(plain, plain == LogKind.Monologue ? string.Empty : who, line);
        }

        /// <summary>二択で選んだ物。問いの後ろに選んだ物を添える</summary>
        public void AddChoice(string question, string answer)
        {
            var head = string.IsNullOrEmpty(question) ? string.Empty : question + ChoseMark;
            Add(LogKind.Choice, string.Empty, head + answer);
        }

        /// <summary>
        /// いまの場面に合わせる。key が前と違えば（場面を移った、読み直した）ログを空にして true を返す。
        /// key は場面を読むたびに変わる値（Scene.handle）を渡す
        /// </summary>
        public bool Follow(int key)
        {
            if (following && key == scene) return false;
            following = true;
            scene = key;
            entries.Clear();
            return true;
        }

        public void Clear()
        {
            entries.Clear();
        }

        // ---- どの場面からも通る入口 ------------------------------------------

        /// <summary>遊んでいる間のログ。いまの場面の分だけを持つ</summary>
        public static readonly ConsoleLog Shared = new ConsoleLog();

        /// <summary>いまの場面の分に合わせてから返す。場面を移っていれば空から始まる</summary>
        public static ConsoleLog Here()
        {
            Shared.Follow(SceneManager.GetActiveScene().handle);
            return Shared;
        }

        /// <summary>調べた物の文。label は調べた物の名（印に出る名）</summary>
        public static void Examined(string label, IEnumerable<string> lines)
        {
            Here().AddLines(LogKind.Examine, label, lines);
        }

        /// <summary>場面の演出が出す文。名前があれば会話、無ければ独白</summary>
        public static void Said(IEnumerable<string> lines)
        {
            Here().AddLines(LogKind.Monologue, string.Empty, lines);
        }

        public static void Said(string line)
        {
            Here().AddLine(LogKind.Monologue, string.Empty, line);
        }

        /// <summary>二択で選んだ物</summary>
        public static void Picked(string question, string answer)
        {
            Here().AddChoice(question, answer);
        }
    }
}
