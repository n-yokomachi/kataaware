using System.Collections.Generic;
using System.Linq;

namespace HalfAware
{
    /// <summary>場面の進行の判定。済んだ対象の集合と必須の一覧を持ち、調べたときに出す文と完了を決める</summary>
    public sealed class SceneProgress
    {
        static readonly string[] NoLines = new string[0];
        readonly HashSet<string> done = new HashSet<string>();
        readonly List<string> required;

        public SceneProgress(IEnumerable<IInteractable> items)
        {
            required = items.Where(i => i.Required).Select(i => i.Id).ToList();
        }

        /// <summary>済んだ対象の id。InteractionPicker に渡す</summary>
        public ICollection<string> Done => done;

        public IReadOnlyList<string> Required => required;

        /// <summary>必須の対象をすべて調べたか</summary>
        public bool IsComplete => required.All(done.Contains);

        /// <summary>
        /// 調べたときに出す文を返す。前提が未達なら、その id の文だけ返して済んだことにはしない。
        /// 済ませた場合は Done に加える
        /// </summary>
        public IReadOnlyList<string> Examine(IInteractable item)
        {
            var unmet = InteractionPicker.UnmetPrerequisite(item, done);
            if (unmet != null) return item.HintFor(unmet) ?? NoLines;
            done.Add(item.Id);
            return item.Lines;
        }
    }
}
