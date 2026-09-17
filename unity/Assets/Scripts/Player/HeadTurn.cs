using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 座っている間の首の振り。体は据えたまま、左右にこの角度までしか向けない。
    /// 立ち上がったら溜めた分を体へ渡して、以後は体ごと回る
    /// </summary>
    public sealed class HeadTurn
    {
        /// <summary>座位で左右に振れる角度。度。片側の値</summary>
        public const float DefaultLimit = 90f;

        /// <summary>0 以下なら制限しない。体ごと回ってよい</summary>
        public float Limit { get; set; }

        /// <summary>体から見た首の向き。度</summary>
        public float Yaw { get; private set; }

        public bool Limited { get { return Limit > 0f; } }

        /// <summary>
        /// 左右の入力を渡す。制限中は首に溜めて true を返す。
        /// 制限が無ければ何もせず false を返すので、呼び手が体を回す
        /// </summary>
        public bool Add(float degrees)
        {
            if (!Limited) return false;
            Yaw = Mathf.Clamp(Yaw + degrees, -Limit, Limit);
            return true;
        }

        /// <summary>首の向きを直に置く。制限の内側に収まる</summary>
        public void Set(float degrees)
        {
            Yaw = Limited ? Mathf.Clamp(degrees, -Limit, Limit) : degrees;
        }

        /// <summary>制限を解く。溜めていた分を返すので、呼び手はそれだけ体を回して繋ぐ</summary>
        public float Release()
        {
            var carried = Yaw;
            Yaw = 0f;
            Limit = 0f;
            return carried;
        }
    }
}
