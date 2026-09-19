using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の演出。看板の読み順と、チップを置いてからの売り買いを持つ。
    ///
    /// 看板はどれから読んでも決めた順に流れる。テーブルで「はい」を選ぶと、
    /// 暗転して露店の内側へ回り、買い手が順に来る。買い手が去るたびに暗転して、
    /// テーブルの上のチップが減る。
    ///
    /// 場面を閉じる判定は SceneFlow が持っている。止め方は二つ使い分ける。
    /// 売り買いのあいだは通して `flow.Held` で閉じるのを押さえ、暗転のあいだだけ
    /// `flow.Freeze` で操作も止める。Freeze は字幕送りまで止めてしまうので、
    /// 台詞のあいだに使うと読み進められなくなる
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class AlleyDirector : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [SerializeField] PlayerController player;
        [Tooltip("看板の段を引く文面のアセット")]
        [SerializeField] RoomScript script;

        [Header("売り買い")]
        [Tooltip("チップを置いたあと立つ場所。露店の内側、テーブルの向こう")]
        [SerializeField] Transform sellSpot;
        [Tooltip("テーブルの上に並べるチップ。左から順に消える")]
        [SerializeField] GameObject[] chips = new GameObject[0];
        [Tooltip("暗転にかける秒数")]
        [SerializeField] float fadeSeconds = 1.0f;
        [Tooltip("暗転したまま置く秒数")]
        [SerializeField] float blackSeconds = 0.5f;

        SignQueue signs;
        bool selling;
        /// <summary>暗転しているあいだだけ立てる。ここで場面が閉じるのを止める</summary>
        bool holding;

        void Awake()
        {
            if (flow == null || hud == null || player == null)
            {
                Debug.LogError("AlleyDirector: flow か hud か player が未接続", this);
                enabled = false;
                return;
            }
            signs = new SignQueue(Pages());
            Show(0);
            flow.Examined += Examined;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.Examined -= Examined;
            // 売り買いの途中で消えたときに、場面を閉じられないまま置き去りにしない
            if (selling) flow.Held = false;
        }

        void Update()
        {
            // 暗転のあいだは進行を止める。台詞のあいだは止めない（送れなくなる）
            if (holding) flow.Freeze(0.25f);
        }

        /// <summary>文面から看板の段を順に拾う。無くなったところで終わり</summary>
        IReadOnlyList<IReadOnlyList<string>> Pages()
        {
            var pages = new List<IReadOnlyList<string>>();
            if (script == null) return pages;
            for (var i = 0; ; i++)
            {
                var entry = script.Find(AlleyIds.Page(i));
                if (entry.id == null) break;
                pages.Add(entry.Lines);
            }
            if (pages.Count == 0) Debug.LogWarning("AlleyDirector: 看板の段が文面に無い", this);
            return pages;
        }

        void Examined(IInteractable item)
        {
            if (item == null) return;
            if (item.Id == AlleyIds.Table)
            {
                if (!selling) StartCoroutine(Sell());
                return;
            }
            // 看板はどれでも同じ列から引く
            if (AlleyIds.IsSign(item.Id)) flow.Say(signs.Next());
        }

        /// <summary>
        /// チップを置いてから売り切れるまで。
        /// 台詞のあいだは止めず、暗転のあいだだけ止める
        /// </summary>
        IEnumerator Sell()
        {
            selling = true;
            player.CanMove = false;
            // テーブルは場面 2 の唯一の必須なので、「はい」を選んだ時点で場面は「済んだ」ことになる。
            // 押さえておかないと、買い手ひとりぶんの台詞を読み終えた次の 1 フレームで場面が閉じる
            flow.Held = true;

            // 露店の内側へ回る。切り替わったように見せる
            yield return Black(true);
            Seat();
            Show(MarketSale.Chips);
            yield return new WaitForSeconds(blackSeconds);
            yield return Black(false);

            for (var i = 0; i < MarketSale.Count; i++)
            {
                flow.Say(MarketSale.Lines(i));
                yield return Spoken();
                // 買い手が去る。暗転しているあいだに、持っていったぶんを引く
                yield return Black(true);
                Show(MarketSale.Left(i));
                yield return new WaitForSeconds(blackSeconds);
                yield return Black(false);
            }

            Show(0);
            flow.Say(MarketSale.Closing);
            // 締めの文は積んである。読み終えたところで場面が閉じてよい
            flow.Held = false;
            selling = false;
        }

        /// <summary>字幕を読み終えるまで待つ。積んだ次のフレームから見る</summary>
        IEnumerator Spoken()
        {
            yield return null;
            while (flow.Talking) yield return null;
        }

        /// <summary>暗転と、そこから明けるの両方。あいだは進行を止めておく</summary>
        IEnumerator Black(bool dark)
        {
            holding = true;
            flow.Freeze(fadeSeconds + blackSeconds + 0.5f);
            yield return hud.FadeTo(dark ? 1f : 0f, fadeSeconds);
            if (!dark) holding = false;
        }

        /// <summary>露店の内側、テーブルの向こうへ立たせる</summary>
        void Seat()
        {
            if (sellSpot == null) return;
            player.transform.position = sellSpot.position;
            player.Yaw = sellSpot.eulerAngles.y;
            player.Pitch = 0f;
        }

        /// <summary>テーブルの上のチップを left 枚だけ見せる</summary>
        void Show(int left)
        {
            for (var i = 0; i < chips.Length; i++)
                if (chips[i] != null) chips[i].SetActive(i < left);
        }
    }
}
