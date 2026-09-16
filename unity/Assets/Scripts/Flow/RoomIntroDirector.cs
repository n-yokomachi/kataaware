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
        /// <summary>カードを見せている間の停止に足す余裕。停止が先に切れて、独白より前に調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;
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

        bool smoking;

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
            hud.CancelSmoke();
        }

        /// <summary>導入は場面の始めに 1 度だけ。切って入れ直しても最初からやり直さない</summary>
        void Start()
        {
            if (enabled) StartCoroutine(Intro());
        }

        /// <summary>
        /// カードを見せている間だけ止め、最後のカードを消したらその場で最初の独白を出す。
        /// 停止と独白を同じ流れの中で決めるので、独白より先に調べられることがない
        /// </summary>
        IEnumerator Intro()
        {
            foreach (var line in credits)
            {
                if (flow.Completed) yield break;
                flow.Freeze(CreditSeconds + FreezeMargin);
                hud.SetCenter(line);
                yield return new WaitForSeconds(CreditSeconds);
            }
            if (flow.Completed) yield break;
            flow.Freeze(TitleSeconds + FreezeMargin);
            hud.SetCenter(titleCard);
            yield return new WaitForSeconds(TitleSeconds);
            if (flow.Completed) yield break;
            hud.SetCenter(null);
            flow.Say(firstLines);
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id != cigaretteId || smoking) return;
            StartCoroutine(Smoke());
        }

        /// <summary>煙草を取った後は吸い終わるまで自動で進む。その間は調べる操作を止める</summary>
        IEnumerator Smoke()
        {
            smoking = true;
            flow.Freeze(SmokeSeconds);
            hud.ShowSmoke(SmokeSeconds);
            yield return new WaitForSeconds(SmokeSeconds);
            smoking = false;
            if (flow.Completed) yield break;
            flow.Say(afterSmokeLines);
        }
    }
}
