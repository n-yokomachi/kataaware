using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>調べる対象の選択。距離と視線の角度、前提の判定だけを扱い、シーンには触れない</summary>
    public static class InteractionPicker
    {
        public const float DefaultRadius = 2f;
        /// <summary>ラジアン。視線からこの角度以内の対象だけ選ぶ</summary>
        public const float MaxAngle = 0.7f;

        /// <summary>After のうち、まだ済んでいない最初の id。すべて済んでいれば null</summary>
        public static string UnmetPrerequisite(IInteractable item, ICollection<string> done)
        {
            foreach (var id in item.After)
            {
                if (!done.Contains(id)) return id;
            }
            return null;
        }

        /// <summary>
        /// ピンを立てる対象か。今この場に在って、まだ済んでおらず、前提も済んでいるもの。
        /// 前提が未達で文だけ出る対象は、まだ用が無いので印を立てない
        /// </summary>
        public static bool Marked(IInteractable item, ICollection<string> done)
        {
            if (item == null || !item.Active) return false;
            if (item.Once && done.Contains(item.Id)) return false;
            return UnmetPrerequisite(item, done) == null;
        }

        static bool HasHint(IInteractable item, string afterId)
        {
            var hint = item.HintFor(afterId);
            return hint != null && hint.Count > 0;
        }

        /// <summary>
        /// forward は正規化済みの視線方向。
        /// 距離が Radius 以内、視線からの角度が maxAngle 以内、前提が済んでいる対象のうち最も近いものを返す。
        /// 前提が未達でも、その id の文があれば選べる。該当が無ければ null
        /// </summary>
        public static IInteractable Select(
            Vector3 camPos,
            Vector3 forward,
            IEnumerable<IInteractable> items,
            ICollection<string> done,
            float maxAngle = MaxAngle)
        {
            IInteractable best = null;
            var bestDist = float.PositiveInfinity;
            foreach (var item in items)
            {
                if (!item.Active) continue;
                if (item.Once && done.Contains(item.Id)) continue;
                var unmet = UnmetPrerequisite(item, done);
                if (unmet != null && !HasHint(item, unmet)) continue;
                var to = item.Position - camPos;
                var dist = to.magnitude;
                if (dist > item.Radius) continue;
                if (dist > 1e-6f)
                {
                    var cos = Vector3.Dot(to, forward) / dist;
                    if (Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) > maxAngle) continue;
                }
                if (dist < bestDist)
                {
                    best = item;
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}
