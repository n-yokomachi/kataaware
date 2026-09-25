using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 3 の段の進行。<see cref="SceneFlow.Examined"/> を受けて、演出と対象の開閉を並べるだけ。
    /// 状態は「いまどの段まで来たか」しか持たない。
    ///
    /// **玄関先のコートハンガーを調べたら、ジャケットを脱いで掛ける。** 着る音（<c>JacketOn.wav</c>）を鳴らし、音の終わりの少し前に
    /// 体のジャケットを消してハンガーに掛けたジャケットを出す（脱ぐ動きは作らない）。間合いは <see cref="JacketBeats"/>。
    /// 場面 3 は路地裏から着たまま帰ってくるので、頭では着せておく。椅子はハンガーの後（<see cref="ConnectIds.After"/>）
    ///
    /// **jack と monitor は after ではなく有効・無効で開く。** after は「その id が済んだか」
    /// しか見ないので、座る演出と挿す演出は調べた後に数秒かかる。after だけだと
    /// その途中で次の対象が拾えてしまう。演出の終わりで開けば、開く時刻が演出の終わりと一致する。
    /// SceneFlow.Awake は切ってある対象も数えるので、伏せて始めても必須の数え上げは狂わない
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class ConnectDirector : MonoBehaviour
    {
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [SerializeField] SceneFlow flow;
        [Tooltip("座位の姿勢。立って始まるので、頭で解いて座ったところで掛ける")]
        [SerializeField] SeatedPose pose;
        [Tooltip("モニターの画面。挿さった瞬間に灯し、リストを送る間だけ流す")]
        [SerializeField] TerminalScreen screen;
        [Tooltip("ジャックを挿すしぐさ。挿さったか・終わったかをここから読む")]
        [SerializeField] JackPlug plug;
        [Tooltip("歩き回る間だけ効かせる椅子の当たり。座ったら切る")]
        [SerializeField] GameObject chairBlocker;
        [Tooltip("座り終えたら開く対象")]
        [SerializeField] GameObject jackItem;
        [Tooltip("挿し終えたら開く対象")]
        [SerializeField] GameObject monitorItem;
        [Tooltip("間をおく行を引く文面。list の行を数える")]
        [SerializeField] RoomScript script;

        [Header("ジャケットをコートハンガーに掛ける")]
        [Tooltip("体に付けたジャケット。頭では着ていて、コートハンガーで脱ぐ")]
        [SerializeField] Garment garment;
        [Tooltip("コートハンガーに掛けたジャケット。掛けたところで出す")]
        [SerializeField] GameObject hung;
        [Tooltip("脱ぐ音を鳴らす口元の音源")]
        [SerializeField] AudioSource voice;
        [Tooltip("脱ぐ音（着る音と同じもの）")]
        [SerializeField] AudioClip jacketOff;
        [Tooltip("音の終わりから、脱がせるまでさかのぼる秒")]
        [SerializeField] float swapBeforeEnd = 0.6f;

        [Header("座る")]
        [Tooltip("腰を下ろす場所。足元の位置")]
        [SerializeField] Vector3 seatSpot = new Vector3(1.5f, 0.05f, 1.2f);
        [Tooltip("座ったときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;
        [Tooltip("座り終えたときの体の向き。度。モニターの方")]
        [SerializeField] float seatYaw;
        [Tooltip("腰を下ろすのにかける秒数")]
        [SerializeField] float sitSeconds = 1.4f;

        [Header("リストを送る間")]
        [Tooltip("list の何行目で間をおくか。0 から数える。負なら間をおかない")]
        [SerializeField] int pauseAfterLine = 4;
        [Tooltip("その間の長さ。秒")]
        [SerializeField] float pauseSeconds = 2f;

        /// <summary>間をおく行。文面から引いておく</summary>
        string pauseLine;
        bool sitting;
        bool seated;
        bool hanging;
        bool booted;
        bool opened;
        bool scrolling;
        bool paused;

        /// <summary>もう座ったか。動作確認から読む</summary>
        public bool Seated { get { return seated; } }

        void OnEnable()
        {
            if (flow == null)
            {
                Debug.LogError("ConnectDirector: flow が未接続", this);
                enabled = false;
                return;
            }
            // 場面 3 は戸口から歩いて始まる。SeatedPose.Seated は直列化されないので、
            // ここで解かないと歩いている間じゅう体だけ座った形で運ばれる
            if (pose != null) pose.Seated = false;
            // 路地裏から着たまま帰ってくる。コートハンガーにはまだ掛かっていない
            if (garment != null) garment.Worn = true;
            if (hung != null) hung.SetActive(false);
            if (chairBlocker != null) chairBlocker.SetActive(true);
            Shut(jackItem);
            Shut(monitorItem);
            pauseLine = PauseLine();
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null)
            {
                flow.Examined -= OnExamined;
                // 止められた場合は finally が走らないので、ここでも見回しを戻す
                if (flow.Player != null) flow.Player.CanLook = true;
                // 掛けている途中で止められたら、足も戻す
                if (hanging && flow.Player != null) flow.Player.CanMove = true;
            }
            StopAllCoroutines();
            sitting = false;
            hanging = false;
        }

        void Update()
        {
            if (plug != null)
            {
                // 画面は挿さった瞬間に灯す。二度瞬いてから明るさが上がるので、
                // しぐさの終わりを待つと挿してから間が空く
                if (!booted && plug.Plugged)
                {
                    booted = true;
                    if (screen != null) screen.Boot();
                }
                // 開くのはしぐさが終わってから
                if (!opened && plug.Done)
                {
                    opened = true;
                    Open(monitorItem);
                }
            }
            if (!scrolling || paused || pauseLine == null) return;
            if (flow.CurrentLine != pauseLine) return;
            // **一度だけ。** 毎フレーム掛け直すと、この行から先へ送れなくなる
            paused = true;
            flow.Freeze(pauseSeconds);
        }

        /// <summary>
        /// 座ったら以後は歩かない。SceneFlow.CloseChoice は二択を閉じるたびに
        /// player.CanMove を立てる（standAfter が空だと立ち上がりの判定そのものが無く、
        /// 必ず歩ける側へ倒れる）。「潜る」で「いいえ」を選ぶと、そこで
        /// 座ったまま歩き出せてしまうので伏せ直す。
        /// PlayerController は実行順が前なので、次のフレームに間に合う LateUpdate で書く
        /// </summary>
        void LateUpdate()
        {
            if (!seated || flow == null || flow.Player == null) return;
            flow.Player.CanMove = false;
        }

        void OnExamined(IInteractable item)
        {
            if (item == null) return;
            // note と monitor はここでは何もしない。次の段は after が開く
            if (item.Id == ConnectIds.Coat)
            {
                if (!hanging) StartCoroutine(Hanging());
                return;
            }
            if (item.Id == ConnectIds.Chair)
            {
                if (!sitting && !seated) StartCoroutine(Sitting());
                return;
            }
            if (item.Id == ConnectIds.List)
            {
                scrolling = true;
                if (screen != null) screen.Scroll(true);
                return;
            }
            if (item.Id != ConnectIds.Dive) return;
            // 「はい」を選んだところで来る。ここで送りを止めると、
            // 暗転していく画面が流れたままにならない
            scrolling = false;
            if (screen != null) screen.Scroll(false);
        }

        /// <summary>
        /// コートハンガーにジャケットを掛ける。脱ぐ音を鳴らし、音の終わりの少し前に体のジャケットを消して、ハンガーのジャケットを出す。
        /// 鳴り終わるまで、ほかを調べさせず、足も止める（掛けながら歩き去らない）。見回しは止めない
        /// </summary>
        IEnumerator Hanging()
        {
            var player = flow.Player;
            hanging = true;
            if (player != null) player.CanMove = false;
            try
            {
                var beats = new JacketBeats(jacketOff != null ? jacketOff.length : 0f, swapBeforeEnd);
                if (voice != null && jacketOff != null) voice.PlayOneShot(jacketOff);
                var off = false;
                for (var t = 0f; t < beats.Sound; t += Time.deltaTime)
                {
                    flow.Freeze(FreezeMargin);
                    if (!off && beats.Swapped(t))
                    {
                        off = true;
                        TakeOff();
                    }
                    yield return null;
                }
                if (!off) TakeOff();
            }
            finally
            {
                hanging = false;
                if (player != null) player.CanMove = true;
            }
        }

        /// <summary>
        /// 椅子に腰を下ろす。停止は毎フレーム掛け直す。ひとつの長い停止にすると
        /// 先に切れて、下ろしている途中で次を調べられる
        /// </summary>
        IEnumerator Sitting()
        {
            var player = flow.Player;
            if (player == null) yield break;
            sitting = true;
            player.CanMove = false;
            // 下ろし終わるまでは見回しも受け付けない。視線はこちらで運ぶ
            player.CanLook = false;
            try
            {
                var from = player.transform.position;
                var fromEye = player.EyeHeight;
                var fromYaw = player.Yaw;
                var fromPitch = player.Pitch;
                for (var t = 0f; t < sitSeconds; t += Time.deltaTime)
                {
                    flow.Freeze(FreezeMargin);
                    var k = Mathf.SmoothStep(0f, 1f, t / sitSeconds);
                    player.transform.position = Vector3.Lerp(from, seatSpot, k);
                    player.EyeHeight = Mathf.Lerp(fromEye, seatEyeHeight, k);
                    player.Yaw = Mathf.LerpAngle(fromYaw, seatYaw, k);
                    player.Pitch = Mathf.Lerp(fromPitch, 0f, k);
                    yield return null;
                }
                player.transform.position = seatSpot;
                player.EyeHeight = seatEyeHeight;
                player.Yaw = seatYaw;
                player.Pitch = 0f;
                // 腰を下ろしきってから切る。立っているうちに切ると椅子をすり抜ける
                if (chairBlocker != null) chairBlocker.SetActive(false);
                if (pose != null) pose.Seated = true;
                // 座ったら体は据えて首だけ振る
                player.HeadYawLimit = HeadTurn.DefaultLimit;
                seated = true;
            }
            finally
            {
                sitting = false;
                if (player != null) player.CanLook = true;
            }
            Open(jackItem);
        }

        /// <summary>体のジャケットを消し、コートハンガーに掛けたジャケットを出す</summary>
        void TakeOff()
        {
            if (garment != null) garment.Worn = false;
            if (hung != null) hung.SetActive(true);
        }

        /// <summary>
        /// 間をおく行。文をここへ書き写すと、文面を直したときにここだけ取り残される。
        /// 何行目かだけを持って、文面から引く
        /// </summary>
        string PauseLine()
        {
            if (script == null || pauseAfterLine < 0) return null;
            var lines = script.Find(ConnectIds.List).Lines;
            if (pauseAfterLine < lines.Count) return lines[pauseAfterLine];
            Debug.LogWarning("ConnectDirector: list に " + pauseAfterLine + " 行目が無い", this);
            return null;
        }

        /// <summary>対象を開く。Interactable.Active は isActiveAndEnabled を見ている</summary>
        static void Open(GameObject item)
        {
            if (item != null) item.SetActive(true);
        }

        static void Shut(GameObject item)
        {
            if (item != null) item.SetActive(false);
        }
    }
}
