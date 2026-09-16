using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。始まってすぐ最初の独白を流す。
    /// 煙草を取ったら煙を立てて操作を止め、そのあいだに黒い幕を 3 回通す。
    /// 幕が覆っているあいだにクレジットとタイトルのカードを出し、幕が抜けると消えている。
    /// 吸い終わりの独白で締める。SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [Header("幕の間。遊びながら詰められるよう Inspector に出してある")]
        [Tooltip("黒い幕が画面を通り抜けるのにかける秒数。入りと出でそれぞれこの長さ")]
        [SerializeField] float panSeconds = 0.55f;
        [Tooltip("幕が覆ったまま止まっている秒数。カードを読む時間")]
        [SerializeField] float holdSeconds = 2.6f;
        [Tooltip("幕と幕のあいだ、画面が見えている秒数")]
        [SerializeField] float gapSeconds = 1.2f;
        [Tooltip("煙草を取ってから最初の幕までの秒数")]
        [SerializeField] float leadInSeconds = 0.8f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [Tooltip("幕 1 回につき 1 枚。空の要素は文字を出さずに通り抜けるだけ")]
        [SerializeField, TextArea] string[] cards = new string[0];
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        bool smoking;

        /// <summary>煙草を取ってから吸い終わるまでの秒数。瞬きの回数から決まる</summary>
        public float SmokeSeconds
        {
            get
            {
                var one = panSeconds * 2f + holdSeconds + gapSeconds;
                return leadInSeconds + one * Mathf.Max(1, cards.Length);
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
            if (flow != null) flow.Examined -= OnExamined;
            StopAllCoroutines();
            smoking = false;
            if (hud == null) return;
            hud.SetCenter(null);
            hud.SetCurtain(1f);
            hud.CancelSmoke();
        }

        /// <summary>最初の独白は場面の始めに 1 度だけ。切って入れ直してもやり直さない</summary>
        void Start()
        {
            if (enabled) flow.Say(firstLines);
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
            smoking = true;
            hud.ShowSmoke(SmokeSeconds);
            flow.Freeze(leadInSeconds + FreezeMargin);
            yield return new WaitForSeconds(leadInSeconds);
            foreach (var card in cards)
            {
                if (flow.Completed) yield break;
                flow.Freeze(panSeconds * 2f + holdSeconds + gapSeconds + FreezeMargin);
                yield return Pan(card);
                yield return new WaitForSeconds(gapSeconds);
            }
            smoking = false;
            if (flow.Completed) yield break;
            flow.Say(afterSmokeLines);
        }

        /// <summary>
        /// 黒い幕を上から下へ通す。覆いきったあいだにカードを出し、そのまま下へ抜ける。
        /// 抜けた後は上へ戻しておく。画面の外なので見えない
        /// </summary>
        IEnumerator Pan(string card)
        {
            for (var t = 0f; t < panSeconds; t += Time.deltaTime)
            {
                hud.SetCurtain(1f - t / panSeconds);
                yield return null;
            }
            hud.SetCurtain(0f);
            if (!string.IsNullOrEmpty(card)) hud.SetCenter(card);
            yield return new WaitForSeconds(holdSeconds);
            hud.SetCenter(null);
            for (var t = 0f; t < panSeconds; t += Time.deltaTime)
            {
                hud.SetCurtain(-t / panSeconds);
                yield return null;
            }
            hud.SetCurtain(1f);
        }
    }
}
