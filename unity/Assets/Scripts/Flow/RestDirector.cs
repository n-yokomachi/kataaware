using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 5 の段の進行。潜っていた分の眩暈が薄れるあいだに一本吸い、
    /// 一行だけ言って、モニターを開く。
    ///
    /// **座位の仕度をここで掛ける。** <see cref="SceneFlow"/> が座位を組むのは
    /// <c>standAfter</c> が入っているときだけで、場面 5 は立ち上がらないので空にしてある。
    /// <c>CanMove</c> も <c>EyeHeight</c> も <see cref="SeatedPose.Seated"/> も直列化されないから、
    /// 掛けないと座ったはずの体で立って歩ける。
    ///
    /// **眩暈も SceneFlow ではなくここが掛ける。** SceneFlow の <c>ReleaseDaze</c> は
    /// <c>dazeUntil</c> が空だと最初の Update で自分の秒数に上書きしてしまい、
    /// 何人渡ってきたかが消える。組み立ては SceneFlow の daze を繋がない
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class RestDirector : MonoBehaviour
    {
        /// <summary>停止に加える余裕。停止が先に切れて、吸っている途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [SerializeField] SceneFlow flow;
        [SerializeField] DazeVolume daze;
        [Tooltip("火を点けて一服する一連。場面 1 と同じ仕組みを使い回す")]
        [SerializeField] Cigarette cigarette;
        [Tooltip("座位の姿勢。挿さったまま戻るので、頭から掛ける")]
        [SerializeField] SeatedPose pose;
        [Tooltip("吸い終わってから開く対象。モニター")]
        [SerializeField] GameObject diveItem;

        [Header("椅子")]
        [Tooltip("座っているときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;

        [Header("眩暈")]
        [Tooltip("潜ってきたときの濃さ")]
        [SerializeField] float fromDive = 0.8f;
        [Tooltip("この場面だけを開いたときの濃さ")]
        [SerializeField] float alone = 0.4f;
        [Tooltip("秒。この時間で 0 になる")]
        [SerializeField] float fadeSeconds = 6f;

        [Header("煙草")]
        [Tooltip("何服吸うか")]
        [SerializeField] int drags = 1;
        [Tooltip("吐き終わってから独白が出るまで。秒")]
        [SerializeField] float afterSmoke = 0.8f;
        [Tooltip("吸い終わってから言う一行")]
        [SerializeField] string[] afterSmokeLines = { "次の記憶で今日は最後にしよう" };

        bool smoking;

        /// <summary>もう吸い終わったか。動作確認から読む</summary>
        public bool Smoked { get; private set; }

        /// <summary>吸い終わるまでの長さ。秒。動作確認から読む</summary>
        public float SmokeSeconds { get { return SmokeBeats.Total(drags) + afterSmoke; } }

        IEnumerator Start()
        {
            if (flow == null)
            {
                Debug.LogError("RestDirector: flow が未接続", this);
                yield break;
            }
            Seat();
            if (daze != null)
            {
                // 渡ってきた人数はまだ使っていない。濃さを段で変えるなら DiveHandoff.Hops をここへ
                var deep = DiveHandoff.FromDive ? fromDive : alone;
                daze.Decay(deep, deep, fadeSeconds);
            }
            if (diveItem != null) diveItem.SetActive(false);
            yield return Smoke();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            smoking = false;
            if (cigarette != null) cigarette.Stop();
            if (flow != null && flow.Player != null) flow.Player.CanLook = true;
        }

        /// <summary>挿さったまま椅子に戻る。歩かず、体は据えて首だけ振る</summary>
        void Seat()
        {
            var player = flow.Player;
            if (player == null) return;
            player.CanMove = false;
            player.CanLook = true;
            player.EyeHeight = seatEyeHeight;
            player.HeadYawLimit = HeadTurn.DefaultLimit;
            if (pose != null) pose.Seated = true;
        }

        /// <summary>
        /// 一本吸い終わるまで。停止は毎フレーム掛け直す。ひとつの長い停止にすると
        /// 先に切れて、吸っている途中でモニターを調べられる
        /// </summary>
        IEnumerator Smoke()
        {
            var player = flow.Player;
            smoking = true;
            // 吸い終わるまでは見回しも受け付けない
            if (player != null) player.CanLook = false;
            try
            {
                if (cigarette != null) cigarette.Light(drags);
                var until = Time.time + SmokeSeconds;
                while (Time.time < until)
                {
                    if (flow.Completed) yield break;
                    flow.Freeze(until - Time.time + FreezeMargin);
                    yield return null;
                }
            }
            finally
            {
                smoking = false;
                if (player != null) player.CanLook = true;
            }
            Smoked = true;
            flow.Say(afterSmokeLines);
            // 独白を出してから開く。吸っている最中に印が浮いていると、
            // 手が塞がっているのに調べられるように見える
            if (diveItem != null) diveItem.SetActive(true);
        }

        /// <summary>吸っている最中か。動作確認から読む</summary>
        public bool Smoking { get { return smoking; } }
    }
}
