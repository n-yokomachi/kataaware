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
    /// 時間帯（空・霧・日射し）も帯ごとに持っていて、Dress が黒のあいだに差し替える。
    /// RenderSettings はシーンにひとつしか無いので、組み立てで一度置くだけでは
    /// 全部の帯が同じ時間帯になり、朝の小麦畑まで夜のまま出る。
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
        [Tooltip("車内の、帯を問わず置く対象。乗り込むまで伏せる。" +
            "ガレージから届いてしまうと once: true のせいで走行中は二度と出ない")]
        [SerializeField] GameObject cabin;
        [Tooltip("ガレージの床を歩く足音。乗り込んだら止める")]
        [SerializeField] Footsteps feet;

        [Header("腕")]
        [Tooltip("腕組みの腕。手動運転の帯だけ伏せる")]
        [SerializeField] GameObject folded;
        [Tooltip("ハンドルに乗せた手。手動運転の帯だけ出す")]
        [SerializeField] GameObject onWheel;
        [Tooltip("手動で運転する帯。0 から数える")]
        [SerializeField] int drivenBand = 4;

        [Header("空と灯り")]
        [Tooltip("日射し。帯ごとに色と強さと向きを差し替える")]
        [SerializeField] Light sun;
        [Tooltip("背景を塗るカメラ")]
        [SerializeField] Camera eye;
        [Tooltip("乗り込む前の空と灯り。ガレージの天井の灯りを塗り潰さない明るさに留める")]
        [SerializeField] DriveSky garageSky;
        [Tooltip("帯ごとの空の物。雲など。帯と同じ並び。中身の無い帯は空の入れ物")]
        [SerializeField] Transform[] skies = new Transform[0];
        [Tooltip("前照灯が路面を照らした跡の板。車の子で、環には乗らない。" +
            "強さは帯が持つ（DriveBand.sky.beam）ので、ここへ渡すのは差し替える先だけ")]
        [SerializeField] Renderer beams;

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
            Arms(-1);
            // 走り出す前はガレージの中。ここへ帯の空を入れると、天井の灯りが二つある
            // 室内に朝日が差して壁も床も白く飛ぶ。空の物も出さない
            ShowSky(-1);
            garageSky.Apply(sun, eye, beams);
            // 車内の対象はガレージからでも距離が届いてしまう。乗り込むまで伏せておく
            if (cabin != null) cabin.SetActive(false);
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
            // **足音は自分で止める。** Footsteps は CharacterController の velocity を見ていて、
            // PlayerController は CanMove が false のあいだ Move を一度も呼ばない。
            // 呼ばれない限り velocity は最後の値を持ち越すので、座ったまま歩き続けたことになり、
            // 走行中ずっとコンクリートの足音が鳴る。伏せる先がガレージではなく
            // プレイヤーの下にあるので、garage.SetActive(false) では消えない
            if (feet != null) feet.enabled = false;
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
            if (cabin != null) cabin.SetActive(true);
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
            Arms(which);
            // 空と灯りもここで差し替える。**黒のあいだに呼ばれるのが要る。**
            // 走っている最中に時間帯が変わると、夜から朝へ切り替わるその一瞬が見える
            ShowSky(which);
            At(which).sky.Apply(sun, eye, beams);
        }

        /// <summary>which 番目の帯の空の物だけ出す。-1 でどれも出さない</summary>
        void ShowSky(int which)
        {
            for (var i = 0; i < skies.Length; i++)
                if (skies[i] != null) skies[i].gameObject.SetActive(i == which);
        }

        /// <summary>which 番目の帯のきっかけだけ出す。-1 でどれも出さない</summary>
        void ShowTrigger(int which)
        {
            for (var i = 0; i < triggers.Length; i++)
                if (triggers[i] != null) triggers[i].SetActive(i == which);
        }

        /// <summary>
        /// which 番目の帯の腕を出す。-1 でどちらも伏せる。
        ///
        /// 最後の帯だけは原作どおり手動運転なので、ハンドルに手を乗せる。
        /// ただし見た目だけで、入力は受け付けない。帯と一緒に黒のあいだに入れ替わる。
        /// 乗り込む前に伏せるのは、ガレージを歩いているあいだ運転席に腕だけが浮いて見えるため
        /// </summary>
        void Arms(int which)
        {
            var driving = which == drivenBand;
            if (folded != null) folded.SetActive(which >= 0 && !driving);
            if (onWheel != null) onWheel.SetActive(which >= 0 && driving);
        }
    }
}
