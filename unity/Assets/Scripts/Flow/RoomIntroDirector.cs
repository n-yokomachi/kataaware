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
        [Tooltip("カードとカードのあいだ、部屋が見えている秒数")]
        [SerializeField] float gapSeconds = 1.8f;
        [Tooltip("煙草を取ってから最初のカードまでの秒数")]
        [SerializeField] float leadInSeconds = 0.8f;
        [Tooltip("煙草を取った直後、正面へ向き直すのにかける秒数")]
        [SerializeField] float aimSeconds = 2.5f;

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

        /// <summary>煙草を取ってから吸い終わるまでの秒数。瞬きの回数から決まる</summary>
        public float SmokeSeconds
        {
            get
            {
                return aimSeconds + leadInSeconds + (holdSeconds + gapSeconds) * Mathf.Max(1, cards.Length);
            }
        }

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
                if (cigarette != null) cigarette.Light(SmokeSeconds);
                flow.Freeze(aimSeconds + leadInSeconds + FreezeMargin);
                yield return AimForward(player);
                yield return new WaitForSeconds(leadInSeconds);
                foreach (var card in cards)
                {
                    if (flow.Completed) yield break;
                    flow.Freeze(holdSeconds + gapSeconds + FreezeMargin);
                    yield return Show(card);
                    yield return new WaitForSeconds(gapSeconds);
                }
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

        /// <summary>画面を黒く覆ってカードを出し、読む時間を置いてそのまま戻す。動きは付けない</summary>
        IEnumerator Show(string card)
        {
            hud.SetCurtain(true);
            if (!string.IsNullOrEmpty(card)) hud.SetCenter(card);
            yield return new WaitForSeconds(holdSeconds);
            hud.SetCenter(null);
            hud.SetCurtain(false);
        }
    }
}
