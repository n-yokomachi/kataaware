using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の演出。看板の読み順と、チップを置いてからの売り買いを持つ。
    ///
    /// 看板と地の文の対応は固定。i 番目の板に i 番目の段が紐づく。
    /// 板はシナリオの順に南から北へ並べてあるので、歩いた順に読めば書かれた順に流れる。
    /// テーブルで「はい」を選ぶと、
    /// 暗転して露店の内側へ回り、買い手が順に来る。買い手が去るたびに暗転して、
    /// テーブルの上のチップが減る。
    ///
    /// 場面を閉じる判定は SceneFlow が持っている。止め方は二つ使い分ける。
    /// 売り買いのあいだは通して `flow.Held` で閉じるのを押さえ、暗転のあいだだけ
    /// `flow.Freeze` で操作も止める。Freeze は字幕送りまで止めてしまうので、
    /// 台詞のあいだに使うと読み進められなくなる。
    ///
    /// **売り買いの途中は、買い手と買い手の間を区切りにする**（<see cref="ISceneMemory"/>、設計書 5 節）。
    /// 売り買いの間は歩けず、字幕か暗転がずっと続くので、自由に動けるフレームが無い。
    /// 次の買い手を出す直前に <see cref="SceneFlow.Checkpoint"/> で写しを取り、思い出した時は
    /// 露店の内側に立ち、卓のチップと煙草をその買い手が来る前の数にして、その買い手から続ける
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class AlleyDirector : MonoBehaviour, ISceneMemory
    {
        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [SerializeField] PlayerController player;
        [Tooltip("看板の段を引く文面のアセット")]
        [SerializeField] RoomScript script;

        [Header("売り買い")]
        [Tooltip("チップを置いたあと立つ場所。露店の内側、テーブルの向こう")]
        [SerializeField] Transform sellSpot;
        [Tooltip("露店の内側へ回ったときの始まりの見下ろし。度（正で下を向く）。卓の上のチップと品物、卓の向こうに立つ買い手が目に入る角度")]
        [SerializeField] float sellPitch = 8f;
        [Tooltip("テーブルの上に並べるチップ。左から順に消える")]
        [SerializeField] GameObject[] chips = new GameObject[0];
        [Tooltip("卓の向こうに立つ買い手。台詞のあいだだけ出す")]
        [SerializeField] GameObject[] buyers = new GameObject[0];
        [Tooltip("買い手 B が置いていく煙草。その行で順に出る")]
        [SerializeField] GameObject[] smokes = new GameObject[0];
        [Tooltip("暗転にかける秒数")]
        [SerializeField] float fadeSeconds = 1.0f;
        [Tooltip("暗転したまま置く秒数")]
        [SerializeField] float blackSeconds = 0.5f;
        [Tooltip("買い手が浮かび上がる秒数")]
        [SerializeField] float buyerFade = 0.55f;

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
            Show(0);
            ShowBuyer(-1);
            ShowSmokes(0);
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

        void Examined(IInteractable item)
        {
            if (item == null) return;
            if (item.Id == AlleyIds.Table)
            {
                if (!selling) StartCoroutine(Sell());
                return;
            }
            // 板と地の文は 1 対 1。街路名の板なら街路名の段が出る
            var which = AlleyIds.SignNumber(item.Id);
            if (which < 0) return;
            if (script == null) { Debug.LogWarning("AlleyDirector: 文面が未接続", this); return; }
            var page = script.Find(AlleyIds.Page(which));
            if (page.id == null) { Debug.LogWarning("AlleyDirector: 段が文面に無い: " + AlleyIds.Page(which), this); return; }
            flow.Say(page.Lines);
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
            yield return Buyers(0);
        }

        /// <summary>
        /// from 人目の買い手から売り切れるまで。買い手を出す直前を区切りにして写しを取る（思い出すとそこから続く）
        /// </summary>
        IEnumerator Buyers(int from)
        {
            for (var i = from; i < MarketSale.Count; i++)
            {
                next = i;
                flow.Checkpoint();
                ShowBuyer(i);
                StartCoroutine(Appear(i, buyerFade));
                flow.Say(MarketSale.Lines(i));
                yield return Spoken(MarketSale.PutsSmokes(i), MarketSale.Smokes(i));
                // 買い手が去る。暗転しているあいだに、持っていったぶんを引く
                yield return Black(true);
                ShowBuyer(-1);
                Show(MarketSale.Left(i));
                yield return new WaitForSeconds(blackSeconds);
                yield return Black(false);
            }

            Show(0);
            next = -1;
            flow.Say(MarketSale.Closing);
            // 締めの文は積んである。読み終えたところで場面が閉じてよい
            flow.Held = false;
            selling = false;
        }

        // ---- 記憶する・思い出す ------------------------------------------------

        /// <summary>売り買いの途中で、次に来る買い手。売り買いの外なら -1</summary>
        int next = -1;

        /// <summary>残す形。次に来る買い手（売り買いの外なら -1）</summary>
        [System.Serializable]
        public sealed class Memo
        {
            public int buyer = -1;
        }

        public string MemoryKey { get { return "alley.sale"; } }

        /// <summary>売り買いの間は自由に動ける所ではない（区切りは <see cref="Buyers"/> が自分で取る）</summary>
        public bool Settled { get { return !selling; } }

        public string Capture()
        {
            return selling && next >= 0 ? JsonUtility.ToJson(new Memo { buyer = next }) : null;
        }

        /// <summary>
        /// 売り買いの途中なら、暗転も音も無しに露店の内側へ立たせ、卓をその買い手が来る前の形にして、
        /// 黒から明けきってからその買い手を出す
        /// </summary>
        public void Restore(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var memo = JsonUtility.FromJson<Memo>(data);
            if (memo == null || memo.buyer < 0 || memo.buyer >= MarketSale.Count) return;
            selling = true;
            next = memo.buyer;
            player.CanMove = false;
            flow.Held = true;
            Seat();
            Show(MarketSale.Left(memo.buyer - 1));
            ShowSmokes(MarketSale.SmokesBefore(memo.buyer));
            ShowBuyer(-1);
            if (Application.isPlaying) StartCoroutine(Resume(memo.buyer));
        }

        /// <summary>黒から明けきるのを待ってから、その買い手から続ける。待つ間は調べさせない</summary>
        IEnumerator Resume(int from)
        {
            for (var t = 0f; t < SceneFlow.FadeInSeconds; t += Time.deltaTime)
            {
                flow.Freeze(0.25f);
                yield return null;
            }
            yield return Buyers(from);
        }

        /// <summary>
        /// 字幕を読み終えるまで待つ。積んだ次のフレームから見る。
        /// mark の行が出たところで卓に煙草を出す。
        /// 送りが速くて拾い損ねても、読み終えたところで必ず出す
        /// </summary>
        IEnumerator Spoken(string mark, int count)
        {
            var waiting = mark != null && count > 0;
            yield return null;
            while (flow.Talking)
            {
                if (waiting && flow.CurrentLine == mark)
                {
                    ShowSmokes(count);
                    waiting = false;
                }
                yield return null;
            }
            if (waiting) ShowSmokes(count);
        }

        /// <summary>暗転と、そこから明けるの両方。あいだは進行を止めておく</summary>
        IEnumerator Black(bool dark)
        {
            holding = true;
            flow.Freeze(fadeSeconds + blackSeconds + 0.5f);
            yield return hud.FadeTo(dark ? 1f : 0f, fadeSeconds);
            if (!dark) holding = false;
        }

        /// <summary>
        /// 露店の内側、テーブルの向こうへ立たせる。
        /// 水平のままだと卓の上のチップが視界の下の縁に掛かって切れるので、少し見下ろして始める（<see cref="sellPitch"/>）
        /// </summary>
        void Seat()
        {
            if (sellSpot == null) return;
            player.transform.position = sellSpot.position;
            player.Yaw = sellSpot.eulerAngles.y;
            player.Pitch = sellPitch;
        }

        /// <summary>テーブルの上のチップを left 枚だけ見せる</summary>
        void Show(int left)
        {
            for (var i = 0; i < chips.Length; i++)
                if (chips[i] != null) chips[i].SetActive(i < left);
        }

        /// <summary>i 人目の買い手だけ立たせる。-1 で誰も出さない</summary>
        void ShowBuyer(int which)
        {
            for (var i = 0; i < buyers.Length; i++)
            {
                if (buyers[i] == null) continue;
                buyers[i].SetActive(i == which);
                if (i == which) Tint(buyers[i], 0f);     // 出したては透明。Appear で濃くする
            }
        }

        /// <summary>
        /// 買い手が浮かび上がる。ぱっと現れると人が湧いたように見える。
        /// マテリアルは 3 人で共通なので、濃さは描画部ごとの上書きで持たせる
        /// </summary>
        IEnumerator Appear(int which, float seconds)
        {
            if (which < 0 || which >= buyers.Length || buyers[which] == null) yield break;
            var who = buyers[which];
            if (seconds <= 0f) { Tint(who, 1f); yield break; }
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (who == null || !who.activeSelf) yield break;
                Tint(who, t / seconds);
                yield return null;
            }
            if (who != null) Tint(who, 1f);
        }

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock paint;

        /// <summary>
        /// 買い手の濃さ。0 で透明、1 で元の色。
        /// 買い手が BuyerFade を持っていれば、浮かび上がるあいだだけ透かせるマテリアルに差し替える（ふだんは透かさない）
        /// </summary>
        void Tint(GameObject who, float amount)
        {
            var fade = who.GetComponent<BuyerFade>();
            if (fade != null)
            {
                fade.Set(amount);
                return;
            }
            var r = who.GetComponent<Renderer>();
            if (r == null) return;
            if (paint == null) paint = new MaterialPropertyBlock();
            r.GetPropertyBlock(paint);
            var colour = r.sharedMaterial == null ? Color.black : r.sharedMaterial.GetColor(BaseColour);
            colour.a *= Mathf.Clamp01(amount);
            paint.SetColor(BaseColour, colour);
            r.SetPropertyBlock(paint);
        }

        /// <summary>卓の上の煙草を count 個見せる</summary>
        void ShowSmokes(int count)
        {
            for (var i = 0; i < smokes.Length; i++)
                if (smokes[i] != null) smokes[i].SetActive(i < count);
        }
    }
}
