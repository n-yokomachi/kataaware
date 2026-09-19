using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。始まってすぐ最初の独白を流す。
    /// 煙草を取ったら煙を立てて操作を止め、そのあいだに画面を 3 回黒く覆う。
    /// 覆っているあいだにクレジットとタイトルのカードを出し、戻ると消えている。
    /// 吸い終わりの独白で締める。SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [Header("カードの間。遊びながら詰められるよう Inspector に出してある")]
        [Tooltip("カードを出したまま止まっている秒数。読む時間")]
        [SerializeField] float holdSeconds = 1.95f;
        [Tooltip("煙草を取った直後、正面へ向き直すのにかける秒数")]
        [SerializeField] float aimSeconds = 2.5f;
        [Tooltip("最後のカードから明けるのにかける秒数。ここだけは切り替えずに戻す")]
        [SerializeField] float liftSeconds = 1.4f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("ライターの音と息、そして煙。無くても場面は進む")]
        [SerializeField] Cigarette cigarette;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [Tooltip("1 回につき 1 枚。空の要素は文字を出さずに黒くなるだけ")]
        [SerializeField, TextArea] string[] cards = new string[0];
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        bool smoking;
        /// <summary>座って始めたときの体の向き。煙草のあいだはここへ戻す</summary>
        float seatedYaw;

        /// <summary>何服吸うか。カードの枚数と同じにして、1 服に 1 枚を当てる</summary>
        public int Drags { get { return Mathf.Max(1, cards.Length); } }

        /// <summary>煙草を取ってから吸い終わるまでの秒数</summary>
        public float SmokeSeconds { get { return aimSeconds + SmokeBeats.Total(Drags); } }

        void OnEnable()
        {
            if (flow == null || hud == null)
            {
                Debug.LogError("RoomIntroDirector: flow か hud が未接続", this);
                enabled = false;
                return;
            }
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null)
            {
                flow.Examined -= OnExamined;
                // 止められた場合は finally が走らないので、ここでも見回しを戻す
                if (flow.Player != null) flow.Player.CanLook = true;
            }
            StopAllCoroutines();
            smoking = false;
            if (hud == null) return;
            hud.SetCenter(null);
            hud.SetCurtain(false);
            if (cigarette != null) cigarette.Stop();
        }

        /// <summary>最初の独白は場面の始めに 1 度だけ。切って入れ直してもやり直さない</summary>
        void Start()
        {
            if (!enabled) return;
            if (flow.Player != null) seatedYaw = flow.Player.Yaw;
            flow.Say(firstLines);
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id != cigaretteId || smoking) return;
            StartCoroutine(Smoke());
        }

        /// <summary>
        /// 煙草を取った後は吸い終わるまで自動で進む。停止は段ごとに掛け直し、
        /// 演出の流れと別の時計にしない。そうしないと停止が先に切れて、途中で調べられる
        /// </summary>
        IEnumerator Smoke()
        {
            var player = flow.Player;
            smoking = true;
            // 吸い終わるまで見回しも受け付けない
            if (player != null) player.CanLook = false;
            try
            {
                flow.Freeze(aimSeconds + FreezeMargin);
                yield return AimForward(player);
                // 向き直してから火を点ける。以後はこの時刻表どおりに音と煙とカードが並ぶ
                if (cigarette != null) cigarette.Light(Drags);
                var started = Time.time;
                for (var i = 0; i < Drags; i++)
                {
                    if (flow.Completed) yield break;
                    var last = i == Drags - 1;
                    var at = started + SmokeBeats.CardAt(i, Drags);
                    // 明ける間も止めておく。暗いうちに調べられないように
                    flow.Freeze(at - Time.time + holdSeconds + (last ? liftSeconds : 0f) + FreezeMargin);
                    while (Time.time < at) yield return null;
                    yield return Show(cards[i], last);
                }
                // 最後に吐き終わってから、少し置いて独白へ
                var until = started + SmokeBeats.Total(Drags);
                flow.Freeze(until - Time.time + FreezeMargin);
                while (Time.time < until) yield return null;
            }
            finally
            {
                smoking = false;
                if (player != null) player.CanLook = true;
            }
            if (flow.Completed) yield break;
            flow.Say(afterSmokeLines);
        }

        /// <summary>座って始めたときの向きへ、滑らかに戻す。切り替えではなく回して戻すので繋ぎ目が出ない</summary>
        IEnumerator AimForward(PlayerController player)
        {
            if (player == null || aimSeconds <= 0f) yield break;
            var fromYaw = player.Yaw;
            var fromPitch = player.Pitch;
            for (var t = 0f; t < aimSeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / aimSeconds);
                player.Yaw = Mathf.LerpAngle(fromYaw, seatedYaw, k);
                player.Pitch = Mathf.Lerp(fromPitch, 0f, k);
                yield return null;
            }
            player.Yaw = seatedYaw;
            player.Pitch = 0f;
        }

        /// <summary>
        /// 画面を黒く覆ってカードを出し、読む時間を置いて戻す。
        /// 途中は瞬きのように切り替えるが、最後の一枚だけは薄れさせて明ける
        /// </summary>
        IEnumerator Show(string card, bool lift)
        {
            hud.SetCurtain(true);
            if (!string.IsNullOrEmpty(card)) hud.SetCenter(card);
            yield return new WaitForSeconds(holdSeconds);
            hud.SetCenter(null);
            if (lift) yield return hud.CurtainTo(0f, liftSeconds);
            hud.SetCurtain(false);
        }
    }
}
