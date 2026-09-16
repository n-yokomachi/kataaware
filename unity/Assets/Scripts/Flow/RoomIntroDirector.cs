using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。始まってすぐ最初の独白を流す。
    /// 煙草を取ったら煙を立てて操作を止め、そのあいだに 3 回の瞬きを挟む。
    /// 瞼が閉じているあいだにクレジットとタイトルのカードを出し、目を開けると消えている。
    /// 吸い終わりの独白で締める。SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>瞼が閉じるのにかける秒数。開くより速い</summary>
        public const float BlinkCloseSeconds = 0.18f;
        /// <summary>閉じきって止まっている秒数。カードを読む時間</summary>
        public const float BlinkHoldSeconds = 1.3f;
        /// <summary>瞼が開くのにかける秒数</summary>
        public const float BlinkOpenSeconds = 0.25f;
        /// <summary>瞬きと瞬きのあいだ、目を開けている秒数</summary>
        public const float BlinkGapSeconds = 1f;
        /// <summary>煙草を取ってから最初の瞬きまでの秒数</summary>
        public const float LeadInSeconds = 0.8f;
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [Tooltip("瞬き 1 回につき 1 枚。空の要素は文字を出さずに閉じて開くだけ")]
        [SerializeField, TextArea] string[] blinkCards = new string[0];
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        bool smoking;

        /// <summary>煙草を取ってから吸い終わるまでの秒数。瞬きの回数から決まる</summary>
        public float SmokeSeconds
        {
            get
            {
                var blink = BlinkCloseSeconds + BlinkHoldSeconds + BlinkOpenSeconds + BlinkGapSeconds;
                return LeadInSeconds + blink * Mathf.Max(1, blinkCards.Length);
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
            hud.SetEyelids(0f);
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
            flow.Freeze(LeadInSeconds + FreezeMargin);
            yield return new WaitForSeconds(LeadInSeconds);
            foreach (var card in blinkCards)
            {
                if (flow.Completed) yield break;
                flow.Freeze(BlinkCloseSeconds + BlinkHoldSeconds + BlinkOpenSeconds + BlinkGapSeconds + FreezeMargin);
                yield return Blink(card);
                yield return new WaitForSeconds(BlinkGapSeconds);
            }
            smoking = false;
            if (flow.Completed) yield break;
            flow.Say(afterSmokeLines);
        }

        /// <summary>瞼を閉じ、閉じきったあいだにカードを出し、また開く。閉じる方が速く、開く方が遅い</summary>
        IEnumerator Blink(string card)
        {
            for (var t = 0f; t < BlinkCloseSeconds; t += Time.deltaTime)
            {
                hud.SetEyelids(t / BlinkCloseSeconds);
                yield return null;
            }
            hud.SetEyelids(1f);
            if (!string.IsNullOrEmpty(card)) hud.SetCenter(card);
            yield return new WaitForSeconds(BlinkHoldSeconds);
            hud.SetCenter(null);
            for (var t = 0f; t < BlinkOpenSeconds; t += Time.deltaTime)
            {
                hud.SetEyelids(1f - t / BlinkOpenSeconds);
                yield return null;
            }
            hud.SetEyelids(0f);
        }
    }
}
