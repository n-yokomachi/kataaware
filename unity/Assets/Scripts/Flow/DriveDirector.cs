using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 8 の演出。ガレージで乗り込ませ、以降は帯を順に送る。
    ///
    /// 帯が終わる条件は、その帯のきっかけの対象を調べて独白を送り切ったことだけ。
    /// 経過時間でも走行距離でも進まないので、調べなければいつまでも走っていられる。
    /// 段取りそのものは BandClock が持っていて、ここはそれを
    /// SceneFlow・HudView・DriveWorld に繋ぐだけにしてある。
    ///
    /// 黒へは切り替えで入り、明けるときだけフェードする。
    /// 場面 1 のドアを閉める暗転と同じ扱い。
    ///
    /// 帯の並びは Awake で写し取る。秒数は bands から毎フレーム直に読むので、
    /// 再生しながら Inspector で触れば効く。ただし配列の長さを再生中に変えると、
    /// route の数え方（Count・IsLast・BandOf）は古いまま取り残される
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class DriveDirector : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [SerializeField] PlayerController player;
        [Tooltip("帯の段を引く文面のアセット")]
        [SerializeField] RoomScript script;
        [SerializeField] DriveWorld world;

        [Header("ガレージ")]
        [Tooltip("乗り込んだら伏せる。ガレージの建物ごと")]
        [SerializeField] GameObject garage;
        [Tooltip("運転席。乗り込んだらここへ立たせる")]
        [SerializeField] Transform seat;

        [Header("帯")]
        [Tooltip("景色の帯。DriveIds.Triggers と同じ並びにする")]
        [SerializeField] DriveBand[] bands = new DriveBand[0];
        [Tooltip("きっかけの対象。帯と同じ並び。その帯に入るまで伏せておく")]
        [SerializeField] GameObject[] triggers = new GameObject[0];

        DriveRoute route;
        readonly BandClock clock = new BandClock();
        /// <summary>段取りを刻んでいる帯。黒のあいだも、終わる側のまま置く</summary>
        int band = -1;
        /// <summary>景色を並べてある帯。黒へ入った時点で次へ進む</summary>
        int shown = -1;
        bool aboard;
        /// <summary>最後の帯の余韻が明けた。あとは SceneFlow が閉じるのを待つだけ</summary>
        bool handedOver;

        /// <summary>
        /// i 番目の帯の値。DriveRoute は並びの問い合わせにだけ使い、秒数はここから直に読む。
        /// 秒数はオーナーが再生しながら Inspector で決めるので、
        /// Awake で写し取ると触っても効かなくなる
        /// </summary>
        DriveBand At(int i)
        {
            return i >= 0 && i < bands.Length ? bands[i] : new DriveBand();
        }

        DriveBand Now { get { return At(band); } }

        void Awake()
        {
            if (flow == null || hud == null || player == null || world == null)
            {
                Debug.LogError("DriveDirector: flow か hud か player か world が未接続", this);
                enabled = false;
                return;
            }
            if (bands.Length == 0 || triggers.Length != bands.Length)
                Debug.LogWarning("DriveDirector: 帯 " + bands.Length + " に対してきっかけ " + triggers.Length, this);
            route = new DriveRoute(bands);
            world.Rolling = false;
            world.Dress(-1);
            ShowTrigger(-1);
            band = -1;
            shown = -1;
            flow.Examined += Examined;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.Examined -= Examined;
            // 走っている途中で消えたときに、場面を閉じられないまま置き去りにしない
            if (aboard) flow.Held = false;
        }

        void Update()
        {
            // 場面が閉じ始めたら手を引く。SceneFlow.Complete も同じ暗幕を書くので、
            // 毎フレーム Dark を書き戻すと「続く」が明るいまま出る
            if (!aboard || flow.Completed) return;

            // 一度渡したら二度と押さえ直さない。上げ直すと、余韻の途中で任意の対象を
            // 読み始めた人が読み終えたとき、閉じられないまま走り続けることになる
            if (handedOver) { flow.Held = false; return; }

            // 独白を送り切ったか。積んだ字幕が尽きたところで余韻へ移る
            if (clock.Beat == DriveBeat.Talking && !flow.Talking) clock.Spoken();

            // 速さと粗さは秒数と同じ扱いで、毎フレーム渡す。
            // Dress のときだけ渡すと、再生しながら Inspector で速さを触っても
            // 次の暗転まで効かない。今の帯は一生効かないことになる
            world.Speed = At(shown).speed;
            world.Rough = At(shown).rough;

            // 最後の帯には次の景色が無い。ただし余韻は流す。
            // 送り切った瞬間に場面が閉じると、窓を開けた最後の一行のあとが忙しない
            if (route.IsLast(band) && clock.Beat != DriveBeat.Running && clock.Beat != DriveBeat.Talking)
            {
                if (!flow.Talking) clock.Tick(Time.deltaTime, Now);
                // 次の帯は無いので、入れ替えの知らせは受け取って捨てる
                clock.TakeSwap();
                if (clock.Beat != DriveBeat.Afterglow) handedOver = true;
                flow.Held = !handedOver;
                return;
            }
            flow.Held = true;

            // 黒と明けの秒数は「終わる側」の帯のもの。仮眠は帯 2 の末尾の暗転にあたる
            var closing = Now;
            // 字幕が出ているあいだは段取りを止める。余韻の途中で任意の対象を読み始めた人を、
            // 読み終える前に黒へ落とさない。黒と明けのあいだは Freeze で送りを止めているので、
            // そこへ持ち越すと読めないまま暗転を跨ぐことになる
            if (!flow.Talking) clock.Tick(Time.deltaTime, closing);
            hud.SetFade(clock.Dark(closing));

            // 黒へ入った一度だけ、景色を先に入れ替える。段取りはまだ終わる側のまま。
            // ここで clock.Reset を呼んではいけない。黒が 1 フレームで終わる
            if (clock.TakeSwap()) Dress(band + 1);

            // 黒と明けのあいだは操作を止める
            if (clock.Beat == DriveBeat.Black || clock.Beat == DriveBeat.FadingIn) flow.Freeze(0.25f);

            // 明け切ったら、段取りも次の帯へ渡す。
            // TakeSwap より後に置く。秒数が全部 0 のとき、同じフレームで両方進む必要がある
            if (clock.Beat == DriveBeat.Running) band = shown;
        }

        void Examined(IInteractable item)
        {
            if (item == null) return;
            // ガレージのドアは二択を出す。「はい」を選んだあとに Examined が鳴るので、
            // ここへ来た時点で乗ると決まっている
            if (item.Id == DriveIds.Door) { Board(); return; }
            if (!aboard || band < 0) return;
            // 走っている最中にしか始めない。時計が受け付けない段で段を積むと、
            // 独白だけ流れて帯が終わらなくなる
            if (clock.Beat != DriveBeat.Running) return;
            // その帯のきっかけでなければ、対象そのものの文だけで終わる。
            // band を先に弾いておくのは、BandOf の「見つからない」も -1 で返るため。
            // 両方 -1 のまま比べると、どの対象を調べても通ってしまう
            if (route.BandOf(item.Id) != band) return;
            clock.Trigger();
            if (script == null) { Debug.LogWarning("DriveDirector: 文面が未接続", this); return; }
            var page = script.Find(DriveIds.Page(band));
            if (page.id == null) { Debug.LogWarning("DriveDirector: 段が文面に無い: " + DriveIds.Page(band), this); return; }
            flow.Say(page.Lines);
        }

        /// <summary>乗り込む。ガレージを伏せ、運転席に据えて走り出す</summary>
        void Board()
        {
            if (aboard) return;
            aboard = true;
            if (garage != null) garage.SetActive(false);
            if (seat != null)
            {
                // seat は足元ではなく目の位置。PlayerController は毎フレーム
                // eye を足元から EyeHeight だけ上へ置き直すので、ここで 0 にして
                // seat をそのまま目の高さにする。立っていたときの 1.6 のままだと
                // 目が屋根（1.52）の上へ突き抜け、車内のどの対象も判定の距離から外れて、
                // 帯 0 のきっかけすら調べられなくなる。
                // 車内は座ったまま歩かないので、足元の高さはもう誰も使わない
                player.EyeHeight = 0f;
                player.transform.position = seat.position;
                player.Yaw = seat.eulerAngles.y;
                player.Pitch = 0f;
            }
            player.CanMove = false;
            world.Rolling = true;
            Dress(0);
            band = 0;
            // 組み直すのは場面の頭でだけ。帯を跨ぐときには呼ばない
            clock.Reset();
        }

        /// <summary>
        /// which 番目の帯の景色を並べる。段取りの時計には触らない。
        /// 黒のあいだに呼ばれるので、入れ替えそのものは見えない
        /// </summary>
        void Dress(int which)
        {
            if (which < 0 || which >= route.Count)
            {
                // 普通はここへ来ない。最後の帯は余韻で段取りを止めるので、
                // その先を並べにくることが無い。黙って止めると mesh か当たりの不具合に見えるので、
                // 繋ぎ間違いだと分かるように残す
                Debug.LogWarning("DriveDirector: " + which + " 番目の帯が無い。走りを止める", this);
                world.Rolling = false;
                return;
            }
            shown = which;
            world.Dress(which);
            world.Rewind();
            ShowTrigger(which);
        }

        /// <summary>which 番目の帯のきっかけだけ出す。-1 でどれも出さない</summary>
        void ShowTrigger(int which)
        {
            for (var i = 0; i < triggers.Length; i++)
                if (triggers[i] != null) triggers[i].SetActive(i == which);
        }
    }
}
