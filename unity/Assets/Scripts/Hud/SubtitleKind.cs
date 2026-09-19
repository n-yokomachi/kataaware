namespace HalfAware
{
    /// <summary>字幕の見せ方。寄せ方と表に組むかがここで決まる</summary>
    public enum SubtitleKind
    {
        /// <summary>ふだんの台詞と説明。左に寄せ、並びになっているものは表に組む</summary>
        Line,

        /// <summary>二択。表に組まず、帯の真ん中へ寄せる</summary>
        Choice,
    }
}
