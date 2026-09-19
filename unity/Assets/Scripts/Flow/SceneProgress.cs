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
        /// 二択を持つ対象は、ここでは済んだことにせず Confirm を待つ。
        /// 二択を持たない対象は、この場で Done に加える
        /// </summary>
        public IReadOnlyList<string> Examine(IInteractable item)
        {
            var unmet = InteractionPicker.UnmetPrerequisite(item, done);
            if (unmet != null) return item.HintFor(unmet) ?? NoLines;
            if (!item.Asks) done.Add(item.Id);
            return item.Lines;
        }

        /// <summary>
        /// 二択で「はい」を選んだときに呼ぶ。ここで初めて済んだことになる。
        /// 「いいえ」なら呼ばない。対象は選べるまま残る
        /// </summary>
        public IReadOnlyList<string> Confirm(IInteractable item)
        {
            if (item == null) return NoLines;
            done.Add(item.Id);
            return item.AfterYes;
        }
    }
}
