using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 調べる対象の選択。距離と視線の角度、前提の判定だけを扱い、シーンには触れない。
    ///
    /// **狙った物を選ぶ**（2026-09-29）。選べる物のうち、視線の真ん中からの角度が一番小さい物を選ぶ。
    /// 角度がほぼ同じ（<see cref="TieAngle"/> の内）物が並んだ時だけ、近い方を選ぶ（視線の先に重なって並ぶ物は手前が勝つ）。
    /// 前は角度の内の物から一番近い物を選んでいたので、場面 1 の座ったまま灰皿を狙っても、手前の煙草の箱が選ばれていた
    /// （狙った物と違う物の印が出ても、遊ぶ側にはなぜかが分からない）
    /// </summary>
    public static class InteractionPicker
    {
        public const float DefaultRadius = 2f;
        /// <summary>ラジアン。視線からこの角度以内の対象だけ選ぶ</summary>
        public const float MaxAngle = 0.7f;
        /// <summary>
        /// ラジアン（約 3 度）。視線からの角度の差がこの内なら、角度は同じと見て近い方を選ぶ。
        /// 場面 1 の机の灰皿と箱は、座った目から 7 度ほど離れて並ぶ（灰皿を狙えば灰皿、箱を狙えば箱）
        /// </summary>
        public const float TieAngle = 0.05f;

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
        /// 距離が Radius 以内、視線からの角度が maxAngle 以内、前提が済んでいる対象のうち、視線からの角度が一番小さいものを返す。
        /// 角度の差が <see cref="TieAngle"/> の内に並んだ物の間では、近いものを返す。
        /// 前提が未達でも、その id の文があれば選べる。該当が無ければ null
        /// </summary>
        public static IInteractable Select(
            Vector3 camPos,
            Vector3 forward,
            IEnumerable<IInteractable> items,
            ICollection<string> done,
            float maxAngle = MaxAngle)
        {
            // 一巡目: 選べる物の、視線からの角度の一番小さい値
            var least = float.PositiveInfinity;
            foreach (var item in items)
            {
                if (Eligible(item, camPos, forward, done, maxAngle, out var angle, out _) && angle < least) least = angle;
            }
            if (float.IsPositiveInfinity(least)) return null;
            // 二巡目: その角度から TieAngle の内の物のうち、一番近い物。距離も同じなら角度の小さい方
            IInteractable best = null;
            var bestDist = float.PositiveInfinity;
            var bestAngle = float.PositiveInfinity;
            foreach (var item in items)
            {
                if (!Eligible(item, camPos, forward, done, maxAngle, out var angle, out var dist)) continue;
                if (angle > least + TieAngle) continue;
                if (dist < bestDist || (dist == bestDist && angle < bestAngle))
                {
                    best = item;
                    bestDist = dist;
                    bestAngle = angle;
                }
            }
            return best;
        }

        /// <summary>
        /// 選べる物か。今この場に在り、済んでおらず（一度きりの物）、前提が済んでいるか前提の文があり、
        /// 距離が Radius の内で、視線からの角度が maxAngle の内。angle は視線からの角度（ラジアン。目の位置にある物は 0）、dist は距離
        /// </summary>
        static bool Eligible(IInteractable item, Vector3 camPos, Vector3 forward, ICollection<string> done, float maxAngle,
            out float angle, out float dist)
        {
            angle = 0f;
            dist = 0f;
            if (item == null || !item.Active) return false;
            if (item.Once && done.Contains(item.Id)) return false;
            var unmet = UnmetPrerequisite(item, done);
            if (unmet != null && !HasHint(item, unmet)) return false;
            var to = item.Position - camPos;
            dist = to.magnitude;
            if (dist > item.Radius) return false;
            if (dist > 1e-6f)
            {
                var cos = Vector3.Dot(to, forward) / dist;
                angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
                if (angle > maxAngle) return false;
            }
            return true;
        }
    }
}
