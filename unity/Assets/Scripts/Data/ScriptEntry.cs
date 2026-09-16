using System;
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>前提が未達のときに出す文。出しても済んだことにはならない</summary>
    [Serializable]
    public struct ScriptHint
    {
        /// <summary>対象の after に挙げた id</summary>
        public string after;
        public string[] lines;
    }

    /// <summary>調べる対象 1 つ分の文面。位置と前提はシーンの Interactable が持つ</summary>
    [Serializable]
    public struct ScriptEntry
    {
        static readonly string[] NoLines = new string[0];

        public string id;
        /// <summary>印に出す短い文。空なら id を出す</summary>
        public string label;
        public string[] lines;
        public ScriptHint[] hints;

        public string Label => string.IsNullOrEmpty(label) ? (id ?? "") : label;

        public IReadOnlyList<string> Lines => lines ?? NoLines;

        /// <summary>afterId が未達のときに出す文。無ければ null</summary>
        public IReadOnlyList<string> HintFor(string afterId)
        {
            if (hints == null) return null;
            foreach (var hint in hints)
            {
                if (hint.after == afterId) return hint.lines;
            }
            return null;
        }

        /// <summary>id で引く。見つからなければ id が null の空の項目を返す</summary>
        public static ScriptEntry Find(IReadOnlyList<ScriptEntry> entries, string id)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].id == id) return entries[i];
                }
            }
            return new ScriptEntry();
        }
    }
}
