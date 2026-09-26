using UnityEngine;

namespace HalfAware
{
    /// <summary>場面 8 の終わりの、村へ着く一連の今の段</summary>
    public enum ArrivalBeat
    {
        /// <summary>まだ始まっていない。最後の景色の余韻が明けるまでここ</summary>
        Waiting,
        /// <summary>黒のまま、車を止めてハンドブレーキを引く音が鳴っている</summary>
        Stopping,
        /// <summary>止まる音が終わってから、ドアを閉めるまでの間</summary>
        Pause,
        /// <summary>ドアを閉めた。村へ切り替わるのを待っている</summary>
        Door,
        /// <summary>村へ切り替えてよい</summary>
        Arrived,
    }

    /// <summary>
    /// 村へ着く一連の秒数。どれも仮置きで、オーナーが耳で決める。
    /// <see cref="stop"/> だけは素材の長さそのもの
    /// </summary>
    public struct ArrivalTimes
    {
        /// <summary>車を止めてハンドブレーキを引く音の長さ。秒。鳴り終わったらドアへ</summary>
        public float stop;
        /// <summary>黒へ入ってから、走行音と麦の風を下げきるまで。秒。止まる音の中で車が止まりきるところに合わせる</summary>
        public float settle;
        /// <summary>止まる音が終わってから、ドアを閉める音を鳴らすまで。秒</summary>
        public float gap;
        /// <summary>ドアを閉める音を鳴らしてから、村へ切り替えるまで。秒</summary>
        public float hold;
    }

    /// <summary>
    /// 場面 8 の終わりの段取り。最後の景色の余韻が明けたところで黒へ切り替え、
    /// 黒のまま 止まる音 → 間 → ドアを閉める音 → 待つ → 村へ、と運ぶ。
    ///
    /// 走行音と麦の風は、黒へ入ったところから <see cref="ArrivalTimes.settle"/> 秒かけて下げる（<see cref="Level"/>）。
    /// 音が鳴るのは <see cref="TakeStop"/> と <see cref="TakeDoor"/> が true を返した一度だけ。
    ///
    /// <see cref="BandClock"/> と同じく MonoBehaviour の外に出してあるのは、テストで丸ごと確かめたいため
    /// </summary>
    public sealed class ArrivalClock
    {
        /// <summary>今の段に入ってから</summary>
        float since;
        /// <summary>黒へ入ってから</summary>
        float elapsed;
        bool stopDue;
        bool doorDue;

        public ArrivalBeat Beat { get; private set; }

        /// <summary>始まっていて、まだ村へ着いていない</summary>
        public bool Underway { get { return Beat != ArrivalBeat.Waiting && Beat != ArrivalBeat.Arrived; } }

        /// <summary>黒へ入った。ここから止まる音を鳴らす。二度目以降は何もしない</summary>
        public void Begin()
        {
            if (Beat != ArrivalBeat.Waiting) return;
            Beat = ArrivalBeat.Stopping;
            since = 0f;
            elapsed = 0f;
            stopDue = true;
        }

        /// <summary>
        /// 時を進める。始まる前と着いた後は何も起きない。
        /// 秒数が 0 でも詰まらないよう、1 回の Tick で先まで進めることがある（<see cref="BandClock.Tick"/> と同じ）
        /// </summary>
        public void Tick(float dt, ArrivalTimes times)
        {
            if (!Underway) return;
            if (dt > 0f)
            {
                since += dt;
                elapsed += dt;
            }
            // 進む先は 3 段しか無いので、この数を使い切ることは無い
            for (var guard = 0; guard < 4; guard++)
            {
                if (Beat == ArrivalBeat.Stopping)
                {
                    if (since < times.stop) return;
                    since -= Spent(times.stop);
                    Beat = ArrivalBeat.Pause;
                    continue;
                }
                if (Beat == ArrivalBeat.Pause)
                {
                    if (since < times.gap) return;
                    since -= Spent(times.gap);
                    Beat = ArrivalBeat.Door;
                    doorDue = true;
                    continue;
                }
                if (Beat == ArrivalBeat.Door)
                {
                    if (since < times.hold) return;
                    since = 0f;
                    Beat = ArrivalBeat.Arrived;
                    return;
                }
                return;
            }
        }

        /// <summary>止まる音を鳴らす時。黒へ入った最初の一度だけ true を返し、呼んだら下ろす</summary>
        public bool TakeStop()
        {
            if (!stopDue) return false;
            stopDue = false;
            return true;
        }

        /// <summary>ドアを閉める音を鳴らす時。間が明けた最初の一度だけ true を返し、呼んだら下ろす</summary>
        public bool TakeDoor()
        {
            if (!doorDue) return false;
            doorDue = false;
            return true;
        }

        /// <summary>
        /// 走行音と麦の風に掛ける大きさ。1 で元のまま、0 で消える。
        /// 黒へ入ってから <see cref="ArrivalTimes.settle"/> 秒かけて、両端を緩めて下げる。
        /// 車は止まる音の途中でゆっくり止まるので、真っ直ぐ下げると止まりきる前に段が付いて聞こえる
        /// </summary>
        public float Level(ArrivalTimes times)
        {
            if (Beat == ArrivalBeat.Waiting) return 1f;
            if (times.settle <= 0f) return 0f;
            var k = Mathf.Clamp01(elapsed / times.settle);
            return 1f - k * k * (3f - 2f * k);
        }

        /// <summary>
        /// 村へ切り替わるまで、あと何秒か。着いたら 0。
        /// 黒のあいだの操作の止め方（SceneFlow.Freeze）をこの長さで打ち切り、
        /// 着いたフレームに場面を閉じられるようにする
        /// </summary>
        public float Left(ArrivalTimes times)
        {
            switch (Beat)
            {
                case ArrivalBeat.Waiting:
                    return Spent(times.stop) + Spent(times.gap) + Spent(times.hold);
                case ArrivalBeat.Stopping:
                    return Spent(times.stop - since) + Spent(times.gap) + Spent(times.hold);
                case ArrivalBeat.Pause:
                    return Spent(times.gap - since) + Spent(times.hold);
                case ArrivalBeat.Door:
                    return Spent(times.hold - since);
                default:
                    return 0f;
            }
        }

        /// <summary>その段に使った秒数。負の数を打たれても次の段に貸しを作らない</summary>
        static float Spent(float seconds)
        {
            return seconds > 0f ? seconds : 0f;
        }
    }
}
