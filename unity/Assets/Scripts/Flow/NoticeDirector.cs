using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 7（自室・気づき）の段の進行（シナリオ設計 11 節）。庭の記憶が途切れた次のフレームに、夜の自室の椅子で戻る。
    /// 座ったまま、ジャックは手首に挿さったまま、モニターは灯ったまま（場面 5 と同じ形）。
    ///
    /// **眩暈を出さない。** ほかの記憶から戻った時は必ずぼやけと二重像が出るが、ここだけ出さない。
    /// 組み立て（BuildNotice）は SceneFlow に眩暈を繋がず、黒からの明けも 0 秒にする（庭が途切れた次のフレームに部屋が映る）。
    /// 部屋の音だけが鳴る短い間（<see cref="quietSeconds"/>）を置いてから、違和感が無いことに気づく独白を出し、モニターを開く。
    ///
    /// 座る・立つは SceneFlow の standAfter（<see cref="NoticeIds.Jack"/>）が受け持つ。抜くしぐさは場面 1 の <see cref="JackPull"/> がそのまま動く。
    /// ここが受け持つのは、頭の間と独白、モニターを開くこと、コートハンガーのジャケットを取って着ること。
    /// ドアは場面 1 と同じく SceneFlow の二択・出がけの音・暗転で、ガレージ（場面 8）へ切り替わる
    ///
    /// **思い出した時**（<see cref="ISceneMemory"/>）は、頭の間も独白も出さず、モニターを開いた形から始める。
    /// 独白を読み終えるまでは自由に動ける所が無いので、手動のセーブが場面の中の状態を持つのは、いつも気づいた後。
    /// ジャックを抜いたか（<see cref="JackPull"/> と SceneFlow の立ち上がり）とジャケットを着たか（ここ）は、調べ済みの印から戻す
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class NoticeDirector : MonoBehaviour, ISceneMemory
    {
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [SerializeField] SceneFlow flow;
        [Tooltip("気づく独白を出したところで開く対象。モニター（アクセスログ）")]
        [SerializeField] GameObject logItem;

        [Header("戻る")]
        [Tooltip("明けてから気づく独白までの間。部屋の音だけが鳴る。秒")]
        [SerializeField] float quietSeconds = 2.5f;
        [Tooltip("戻った時の違和感が無いことに気づく独白。本文は WriteNoticeScript が持ち、組み立てが書き込む")]
        [SerializeField] string[] noticeLines = new string[0];

        [Header("ジャケットを取って着る")]
        [Tooltip("この id を調べたら、コートハンガーのジャケットを取って着る")]
        [SerializeField] string coatId = NoticeIds.Coat;
        [Tooltip("体に付けたジャケット。頭では脱いでいて、着る音の終わりの少し前に着せる")]
        [SerializeField] Garment garment;
        [Tooltip("コートハンガーに掛けたジャケット（場面 3 で掛けた物）。着せたところで消す")]
        [SerializeField] GameObject hung;
        [Tooltip("着る音を鳴らす口元の音源")]
        [SerializeField] AudioSource voice;
        [Tooltip("着る音")]
        [SerializeField] AudioClip jacketOn;
        [Tooltip("音の終わりから、着せ替えるまでさかのぼる秒")]
        [SerializeField] float swapBeforeEnd = 0.6f;

        bool quiet;
        bool dressing;
        bool resumed;

        /// <summary>気づく独白を出したか（モニターを開いたか）。動作確認から読む</summary>
        public bool Noticed { get; private set; }

        /// <summary>頭の、部屋の音だけの間の最中か。動作確認から読む</summary>
        public bool Quiet { get { return quiet; } }

        /// <summary>ジャケットを着ている最中か。動作確認から読む</summary>
        public bool Dressing { get { return dressing; } }

        void OnEnable()
        {
            if (flow == null)
            {
                Debug.LogError("NoticeDirector: flow が未接続", this);
                enabled = false;
                return;
            }
            // 場面 3 でコートハンガーに掛けたまま。Garment.Worn は直列化されるが、組み立てと食い違っても頭はこの形
            if (garment != null) garment.Worn = false;
            if (hung != null) hung.SetActive(true);
            // 気づく独白を出すまでは伏せておく
            if (logItem != null) logItem.SetActive(false);
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null)
            {
                flow.Examined -= OnExamined;
                // 着ている途中で止められたら、足を戻す（finally は走らない）
                if (dressing && flow.Player != null) flow.Player.CanMove = true;
            }
            StopAllCoroutines();
            quiet = false;
            dressing = false;
        }

        /// <summary>
        /// 頭の間を置いてから気づく。思い出した時は何も出さない（Restore がモニターを開いてある）。
        /// 間のあいだは見回せるが、調べられる物はまだ無い
        /// </summary>
        IEnumerator Start()
        {
            if (flow == null || resumed) yield break;
            if (quietSeconds > 0f)
            {
                quiet = true;
                try
                {
                    yield return new WaitForSeconds(quietSeconds);
                }
                finally
                {
                    quiet = false;
                }
            }
            if (flow.Completed) yield break;
            Notice();
        }

        /// <summary>気づく独白を積み、モニターを開く。独白を送っている間は SceneFlow が調べる操作を受け付けない</summary>
        void Notice()
        {
            Noticed = true;
            if (noticeLines != null && noticeLines.Length > 0) flow.Say(noticeLines);
            if (logItem != null) logItem.SetActive(true);
        }

        void OnExamined(IInteractable item)
        {
            if (item == null) return;
            if (item.Id == coatId && !dressing) StartCoroutine(PutOn());
        }

        /// <summary>
        /// コートハンガーのジャケットを取って着る。着る音を鳴らし、音の終わりの少し前に、ハンガーのジャケットを消して体へ着せる
        /// （取る・着る動きは作らない。場面 3 で掛けたのと同じ扱い）。鳴り終わるまでほかを調べさせず、足も止める。見回しは止めない
        /// </summary>
        IEnumerator PutOn()
        {
            var player = flow.Player;
            dressing = true;
            if (player != null) player.CanMove = false;
            try
            {
                // Web では、鳴らす時に読み込み始めると鳴り出しが展開の後へ延び、着る音と着せ替えがずれる。
                // 展開を待ってから鳴らす。待つ間も止めておく。エディタとスタンドアロンでは待たない（SoundLoad）
                if (!SoundLoad.Ready(jacketOn)) yield return SoundLoad.Wait(jacketOn, () => flow.Freeze(FreezeMargin));
                var beats = new JacketBeats(jacketOn != null ? jacketOn.length : 0f, swapBeforeEnd);
                if (voice != null && jacketOn != null) voice.PlayOneShot(jacketOn);
                var on = false;
                for (var t = 0f; t < beats.Sound; t += Time.deltaTime)
                {
                    flow.Freeze(FreezeMargin);
                    if (!on && beats.WornPuttingOn(t))
                    {
                        on = true;
                        Dress();
                    }
                    yield return null;
                }
                if (!on) Dress();
            }
            finally
            {
                dressing = false;
                if (player != null) player.CanMove = true;
            }
        }

        /// <summary>ハンガーのジャケットを消し、体へ着せる</summary>
        void Dress()
        {
            if (hung != null) hung.SetActive(false);
            if (garment != null) garment.Worn = true;
        }

        // ---- 記憶する・思い出す ------------------------------------------------

        public string MemoryKey { get { return "notice.steps"; } }

        /// <summary>頭の間と、着ている間は残さない</summary>
        public bool Settled { get { return !quiet && !dressing; } }

        /// <summary>どこまで来たかは調べ済みの印から決まるので、自分では残さない</summary>
        public string Capture() { return null; }

        /// <summary>
        /// 頭の間と独白を出さず、モニターを開いた形にする（残せるのは気づいた後だけ）。
        /// ジャケットを調べ済みなら、音も無しに着た形へ置く。ジャックを抜いた形は <see cref="JackPull"/> と SceneFlow が戻す
        /// </summary>
        public void Restore(string data)
        {
            resumed = true;
            Noticed = true;
            if (logItem != null) logItem.SetActive(true);
            if (flow != null && flow.Progress != null && flow.Progress.Done.Contains(coatId)) Dress();
        }
    }
}
