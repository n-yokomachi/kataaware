using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。眩暈の中でクレジットとタイトルを順に出し、その間は調べる操作を止める。
    /// 終わったら最初の独白を流す。煙草を取ったら煙を立てて数秒止め、吸い終わりの独白を流す。
    /// SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>クレジット 1 枚を見せている秒数</summary>
        public const float CreditSeconds = 2f;
        /// <summary>タイトルを見せている秒数</summary>
        public const float TitleSeconds = 2.5f;
        /// <summary>導入のあいだ調べる操作を止める秒数。クレジットとタイトルの合計より長くする</summary>
        public const float IntroSeconds = 6.2f;
        /// <summary>煙草を取ってから吸い終わるまでの秒数</summary>
        public const float SmokeSeconds = 4f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] credits = { "制作 〔名義〕" };
        [SerializeField] string titleCard = "HALF AWARE";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        void OnEnable()
        {
            if (flow == null || hud == null)
            {
                Debug.LogError("RoomIntroDirector: flow か hud が未接続", this);
                enabled = false;
                return;
            }
            flow.Examined += OnExamined;
            StartCoroutine(Intro());
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            StopAllCoroutines();
            if (hud == null) return;
            hud.SetCenter(null);
            hud.CancelSmoke();
        }

        IEnumerator Intro()
        {
            flow.Freeze(IntroSeconds);
            var shown = 0f;
            foreach (var line in credits)
            {
                hud.SetCenter(line);
                yield return new WaitForSeconds(CreditSeconds);
                shown += CreditSeconds;
            }
            hud.SetCenter(titleCard);
            yield return new WaitForSeconds(TitleSeconds);
            shown += TitleSeconds;
            hud.SetCenter(null);
            yield return new WaitForSeconds(Mathf.Max(0f, IntroSeconds - shown));
            flow.Say(firstLines);
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id != cigaretteId) return;
            StartCoroutine(Smoke());
        }

        /// <summary>煙草を取った後は吸い終わるまで自動で進む。その間は調べる操作を止める</summary>
        IEnumerator Smoke()
        {
            flow.Freeze(SmokeSeconds);
            hud.ShowSmoke(SmokeSeconds);
            yield return new WaitForSeconds(SmokeSeconds);
            flow.Say(afterSmokeLines);
        }
    }
}
