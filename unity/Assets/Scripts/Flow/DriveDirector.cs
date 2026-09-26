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
    /// 最後の景色だけは明けない。余韻が明けたら黒へ切り替え、黒のまま車を止める音 → 間 →
    /// ドアを閉める音 → 待つ、と音だけで運んでから、場面を閉じるのを許す。
    /// 村（Village）への切り替えは SceneFlow の nextScene が受け持ち、暗転を挟まない（SceneExit.FadesOut）。
    /// 段取りそのものは ArrivalClock が持つ
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
        [Tooltip("ガレージでだけ調べられる対象。乗り込んだら伏せる。" +
            "伏せないと走っている車の後ろにピンが浮いたまま残る")]
        [SerializeField] GameObject garageOnly;

        [Header("雨")]
        [Tooltip("風防に付く水とワイパー。雨の景色でだけ出す")]
        [SerializeField] GameObject rainRig;

        [Header("煙草")]
        [Tooltip("火を点けて一服する一連。場面 1 と同じ仕組みを使い回す")]
        [SerializeField] Cigarette cigarette;
        [Tooltip("煙。一服のあとも暗転まで出し続ける")]
        [SerializeField] SmokePuffs smoke;
        [Tooltip("何服吸うか")]
        [SerializeField] int drags = 1;
        [Tooltip("窓が下りきってから吐き始めるまで。秒")]
        [SerializeField] float beforeExhale = 0.35f;
        [Tooltip("吐き終わってから独白が出るまで。秒")]
        [SerializeField] float afterSmoke = 0.8f;

        [Header("家")]
        [Tooltip("小麦畑の農家。独白を送り切るまで伏せておく。" +
            "環の区切りごとに 1 つあるので配列になる。帯の直下にまとめられない" +
            "（DriveWorld が帯の子の数を環の枠の数に使うため、区切り以外を混ぜると環がずれる）")]
        [SerializeField] GameObject[] crofts = new GameObject[0];
        [Tooltip("農家が現れてよい、いちばん手前の z。m。" +
            "これより遠い区切りが回ってきたときだけ出す。道の先は 110 m")]
        [SerializeField] float croftFrom = 88f;

        /// <summary>農家を出してよいか。独白を送り切ったところで立つ</summary>
        bool croftsDue;
        [Tooltip("ガレージの床を歩く足音。乗り込んだら止める")]
        [SerializeField] Footsteps feet;

        [Header("乗り込み")]
        [Tooltip("ドア・イグニッション・動き出しと走行音")]
        [SerializeField] DriveSound sound;
        [Tooltip("運転席のドアの板。乗り込みで開いて閉める")]
        [SerializeField] CarDoor carDoor;
        [Tooltip("目をドアへ向け、板の退く先から外れるまで。秒")]
        [SerializeField] float doorFace = 0.7f;
        [Tooltip("板が開ききるまで。秒。この間、目は据えたまま開くところを見ている")]
        [SerializeField] float doorSwing = 0.9f;
        [Tooltip("板が開ききってから目が動き出すまで。秒")]
        [SerializeField] float doorHold = 0.45f;
        [Tooltip("戸口をくぐって運転席まで目を運ぶ秒数")]
        [SerializeField] float seatMove = 1.5f;
        [Tooltip("車内を正面に置いたまま待つ秒数")]
        [SerializeField] float sitHold = 0.6f;
        [Tooltip("車内からドアへ向き直るまで。秒")]
        [SerializeField] float doorLook = 0.55f;
        [Tooltip("板が閉まりきるまで。秒。閉まったところで音が鳴る")]
        [SerializeField] float doorShut = 0.45f;
        [Tooltip("閉めてから前を向ききるまで。秒")]
        [SerializeField] float faceFront = 0.7f;
        [Tooltip("前を向いてからイグニッションまで。秒")]
        [SerializeField] float shutHold = 0.5f;
        [Tooltip("板の面から空ける隙間。m。**板は目の高さを薙いで開く。** " +
            "迫ってきた面がこの隙間より近づくぶんだけ、蝶番から遠ざかる向きに退く")]
        [SerializeField] float doorClear = 0.25f;
        [Tooltip("板のいちばん外へ出る点までの、蝶番からの距離。m。面から測った値")]
        [SerializeField] float doorReach = 1.65f;
        [Tooltip("戸口の通り道。座席から運転席側へ出す距離。m。回り込む曲線の制御点になる")]
        [SerializeField] float doorGate = 0.75f;
        [Tooltip("戸口の後ろと前の縁。車から見た z。通り道はこの間に収める")]
        [SerializeField] Vector2 doorMouth = new Vector2(-0.50f, 0.75f);
        [Tooltip("目を向けるドアの取っ手。蝶番から見た座。板と一緒に動く")]
        [SerializeField] Vector3 doorMark = new Vector3(-0.05f, 1.13f, -0.84f);
        [Tooltip("目を向ける車内。運転席から見た座。計器盤の助手席寄り")]
        [SerializeField] Vector3 cabinMark = new Vector3(-0.36f, -0.22f, 0.60f);
        [Tooltip("イグニッションを鳴らし終えてから黒へ切り替わるまで。秒。" +
            "0 なら鳴らし終えた時点で黒へ落ちる。この間だけエンジンの震えと" +
            "止まったままの音が入るので、0 のときはどちらも出さない")]
        [SerializeField] float ignitionHold = 0f;
        [Tooltip("エンジンだけ掛かっているときの震え。走行中の粗さ 1.0 に対する割合")]
        [SerializeField] float idleRough = 0.70f;
        [Tooltip("イグニッションを鳴らし始めてから車体が震え出すまで。秒。" +
            "鍵を回し切ってエンジンが掛かるところに合わせる")]
        [SerializeField] float shakeAt = 5f;
        [Tooltip("座ってから左右に振れる角度。度。片側の値。90 で前方 180 度")]
        [SerializeField] float seatedYawLimit = 90f;
        // **秒数はここの既定が正。** 組み立て（BuildDrive.Wire）は DriveDirector を
        // 作り直すので、シーンで触った値は次の組み直しで既定へ戻る。変えるならここを変える

        [Tooltip("黒のまま置く秒数。**0 なら暗転を挟まず、鍵の音が鳴り終わったその場で走り出す。** " +
            "0 より大きいときは、黒へ落ちるのは鳴らし終えるこの秒数前になり、" +
            "黒が明けるのと鍵の音が鳴り終わるのが同じ瞬間になる")]
        [SerializeField] float pullHold = 0f;
        [Tooltip("黒から一つ目の景色へ浮かび上がる秒数。**0 なら一瞬で切り替わる。** " +
            "景色どうしの切り替えがフェード無しなので、場面の頭もそれに揃えてある")]
        [SerializeField] float pullFade = 0f;

        [Header("村へ")]
        [Tooltip("最後の景色の余韻が明けて黒へ切り替えてから、走行音と麦の風を下げきるまで。秒。" +
            "止まる音（CarStopHandbrake）の中で車が止まりきるのが 5.7 秒あたり。両端を緩めて下げる")]
        [SerializeField] float settleSeconds = 5.7f;
        [Tooltip("止まる音が鳴り終わってから、ドアを閉める音を鳴らすまで。秒。0 なら鳴り終わったその場で閉める")]
        [SerializeField] float doorGap = 0.6f;
        [Tooltip("ドアを閉める音を鳴らしてから、村へ切り替えるまで。秒。黒のまま待ち、切り替えは暗転を挟まない")]
        [SerializeField] float villageHold = 2f;

        [Header("体")]
        [Tooltip("主人公の体。ガレージでは立って歩き、運転席に着いたら座った形になる。" +
            "座った形は腕組み、手動運転の帯だけ二つ目の形（ハンドルに手を乗せる）")]
        [SerializeField] SeatedPose body;
        [Tooltip("手動で運転する帯。0 から数える")]
        [SerializeField] int drivenBand = 2;

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
        /// <summary>最後の景色の余韻が明けてから村へ着くまでの段取り</summary>
        readonly ArrivalClock arrival = new ArrivalClock();
        /// <summary>段取りを刻んでいる帯。黒のあいだも、終わる側のまま置く</summary>
        int band = -1;
        /// <summary>景色を並べてある帯。黒へ入った時点で次へ進む</summary>
        int shown = -1;
        bool aboard;
        /// <summary>乗り込みの最中。aboard はこの一連が終わってから立てる</summary>
        bool boarding;
        /// <summary>場面が閉じたときに走行音を止めた</summary>
        bool hushed;
        /// <summary>村へ着く一連を終えた。あとは SceneFlow が村へ切り替えるのを待つだけ</summary>
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
            // ガレージでは立って歩く。座った形は運転席に着いてから
            if (body != null) body.Seated = false;
            Arms(-1);
            // 走り出す前はガレージの中。ここへ帯の空を入れると、天井の灯りが二つある
            // 室内に朝日が差して壁も床も白く飛ぶ。空の物も出さない
            ShowSky(-1);
            garageSky.Apply(sun, eye, beams);
            // ガレージは屋内。雨は降っていない
            if (rainRig != null) rainRig.SetActive(false);
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
            if (flow.Completed)
            {
                // 場面が閉じたら走行音も切る。輪なので、放っておくと
                // 「続く」の字幕の裏で走り続ける
                if (!hushed && sound != null) { sound.Hush(); hushed = true; }
                return;
            }
            if (!aboard) return;

            // 一度渡したら二度と押さえ直さない。上げ直すと、余韻の途中で任意の対象を
            // 読み始めた人が読み終えたとき、閉じられないまま走り続けることになる
            if (handedOver) { flow.Held = false; return; }

            // 村へ着く一連の最中。黒のまま音だけで運ぶ。**下の帯の分岐より前に置く。**
            // 最後の帯の時計は、余韻が明けると黒と明けの 0 秒を越えて Running へ戻っているので、
            // 下へ流すと帯の分岐を素通りして、無い次の帯を並べにいく
            if (arrival.Underway) { Arrive(Time.deltaTime); return; }

            // 独白を送り切ったか。積んだ字幕が尽きたところで余韻へ移る
            if (clock.Beat == DriveBeat.Talking && !flow.Talking)
            {
                clock.Spoken();
                // ここから家が現れてよくなる。**その場では出さない。**
                // 目の前に湧くのは見ていて分かるので、環が一周して
                // 道の先から入ってくる区切りにだけ載せる
                croftsDue = true;
            }

            // **最後の景色の return より前に置く。** 小麦畑は最後の景色なので、
            // 余韻に入ると下の分岐が return してしまい、ここから先は走らない。
            // 下に置いていたあいだ、家は秒数をどれだけ延ばしても一度も出なかった
            AdmitCrofts();

            // 速さと粗さは秒数と同じ扱いで、毎フレーム渡す。
            // Dress のときだけ渡すと、再生しながら Inspector で速さを触っても
            // 次の暗転まで効かない。今の帯は一生効かないことになる
            world.Speed = At(shown).speed;
            world.Rough = At(shown).rough;

            // 最後の帯には次の景色が無い。ただし余韻は流す。
            // 送り切った瞬間に場面が閉じると、窓を開けた最後の一行のあとが忙しない。
            // 余韻が明けたら黒へ切り替えて、村へ着く一連（Arrive）へ渡す
            if (route.IsLast(band) && clock.Beat != DriveBeat.Running && clock.Beat != DriveBeat.Talking)
            {
                if (!flow.Talking) clock.Tick(Time.deltaTime, Now);
                // 次の帯は無いので、入れ替えの知らせは受け取って捨てる
                clock.TakeSwap();
                flow.Held = true;
                if (clock.Beat == DriveBeat.Afterglow) return;
                // 黒の頭で止まる音を鳴らす。このフレームの分の時は進めない
                arrival.Begin();
                Arrive(0f);
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
            // 窓は調べたその場で下ろす。文が出るのと同時に音が鳴り、こもりも取れる。
            // 麦畑の風もここから重ねる。**窓を下ろす音はもう一か所（夜の高速の煙草）でも鳴るが、
            // あちらには麦畑が無いので Smoking では鳴らさない**
            if (item.Id == DriveIds.Window)
            {
                if (sound != null) { sound.WindowDown(); sound.Open(true); sound.Field(true); }
                Drift(true);
            }

            // 煙草だけ、独白の前に一連の間が入る。**時計を進めるのはその後。**
            // ここで clock.Trigger を呼んでしまうと、段を積む前に Update が
            // 「話し終えた」と見なして（Talking かつ flow.Talking が false）、
            // 独白を出さないまま余韻へ移る
            if (item.Id == DriveIds.Cigar && cigarette != null)
            {
                StartCoroutine(Smoking());
                return;
            }
            clock.Trigger();
            Speak();
        }

        /// <summary>
        /// 村へ着く一連を 1 フレーム進める。黒のまま、車を止める音 → 間 → ドアを閉める音 → 待つ。
        /// 走行音と麦の風は、黒へ入ったところから止まる音に合わせて下げる。
        ///
        /// 着いたら場面を閉じるのを許す。SceneFlow が nextScene（Village）へ切り替える。
        /// **暗転は足さない。** 次の場面へ進むときは SceneFlow が幕を触らない（SceneExit.FadesOut）ので、
        /// ここで置いた黒が読み込みまで残り、村は明けた絵で映る。
        ///
        /// 秒数は毎フレーム値から読む。帯の秒数と同じく、再生しながら Inspector で触れば効く
        /// </summary>
        void Arrive(float dt)
        {
            var times = new ArrivalTimes
            {
                stop = sound != null ? sound.ParkSeconds : 0f,
                settle = settleSeconds,
                gap = doorGap,
                hold = villageHold,
            };
            arrival.Tick(dt, times);
            if (arrival.TakeStop() && sound != null) sound.Park();
            if (arrival.TakeDoor() && sound != null) sound.DoorShut();
            if (sound != null) sound.Settle(arrival.Level(times));
            if (arrival.Beat == ArrivalBeat.Arrived)
            {
                handedOver = true;
                flow.Held = false;
                return;
            }
            flow.Held = true;
            hud.SetFade(1f);
            // 黒のあいだは操作を止める。**着く時刻を越えて止めない。** ほかの演出と同じく
            // FreezeStep ずつ延ばすと、着いてから最大でその分 SceneFlow が閉じられず、
            // ドアから村までが villageHold より延びる
            flow.Freeze(Mathf.Min(FreezeStep, arrival.Left(times)));
        }

        /// <summary>
        /// 煙草に火を点け、一服して、窓を下ろしてから独白を出す。
        ///
        /// 対象そのものの文（「煙草に火をつけ、窓を開ける」）は SceneFlow が先に出すので、
        /// それを送り切るまで待つ。待たずに始めると、字幕の裏で火が点いて煙が立つ
        /// </summary>
        System.Collections.IEnumerator Smoking()
        {
            while (flow.Talking) yield return null;

            // 火を点けて吸うところまでは Cigarette の時刻表どおり。
            // **吐くところまで任せない。** あちらは吸い終わって 0.55 秒で吐き始めるが、
            // 窓を下ろす音は 4.45 秒あるので、任せると窓の途中で吐いてしまう。
            // 順は「火を点ける → 吸う → 窓が下りる → 吐く」でなければならない
            cigarette.Light(drags);
            var lit = Time.time;
            var drawn = SmokeBeats.DragAt(0, drags) + SmokeBeats.DragSeconds;
            while (cigarette.Smoking && Time.time - lit < drawn)
            {
                flow.Freeze(FreezeStep);
                yield return null;
            }

            // 吸い終わったところで Cigarette を降ろす。ここから先はこちらで組む。
            // Stop は煙も消すので、消えたぶんはすぐ焚き直す
            cigarette.Stop();
            if (smoke != null) smoke.Begin(SmokeSeconds);

            if (sound != null) { sound.WindowDown(); sound.Open(true); }
            Drift(true);
            yield return Wait((sound != null ? sound.WindowSeconds : 0f) + beforeExhale);

            // 窓が下りきってから吐く。煙のひと吹きも合わせる
            if (sound != null) sound.Exhale();
            if (smoke != null) smoke.Blow();
            yield return Wait((sound != null ? sound.ExhaleSeconds : 0f) + afterSmoke);

            clock.Trigger();
            Speak();
        }

        /// <summary>
        /// 一服したあと煙を出し続ける長さ。秒。
        /// 独白を読む速さはプレイヤー次第なので、余韻の長さから見ても余る値を置く。
        /// 暗転のところで Dress が畳むので、長すぎて困ることは無い
        /// </summary>
        const float SmokeSeconds = 600f;

        /// <summary>今の景色の独白を積む</summary>
        void Speak()
        {
            if (script == null) { Debug.LogWarning("DriveDirector: 文面が未接続", this); return; }
            var page = script.Find(DriveIds.Page(band));
            if (page.id == null) { Debug.LogWarning("DriveDirector: 段が文面に無い: " + DriveIds.Page(band), this); return; }
            flow.Say(page.Lines);
        }

        /// <summary>乗り込む。一連の演出は Boarding が持つ</summary>
        void Board()
        {
            if (aboard || boarding) return;
            boarding = true;
            StartCoroutine(Boarding());
        }

        /// <summary>
        /// 乗り込みの一連。ドアを開ける音 → 目が運転席へ滑る → ドアを閉める音 →
        /// イグニッション → 黒へ切り替え、動き出しの音 → フェードインして一つ目の景色。
        ///
        /// **aboard はこの一連が終わってから立てる。** 立てた時点で Update が
        /// hud.SetFade を毎フレーム書き始めるので、先に立てるとここで置いた黒が
        /// 次のフレームに上書きされて、暗転そのものが出ない。
        ///
        /// 代わりに flow.Held を先頭で立てる。garage.door は場面を閉じる対象なので、
        /// 「はい」を選んだ時点で SceneFlow が閉じにかかる。Update の flow.Held = true は
        /// aboard が立つまで走らないため、ここで押さえないと演出の途中で場面が終わる
        /// </summary>
        System.Collections.IEnumerator Boarding()
        {
            flow.Held = true;
            // 窓も煙も、始まりは閉め切った車内の扱い
            if (sound != null) sound.Shut();
            Drift(false);
            if (garageOnly != null) garageOnly.SetActive(false);
            // **足音は自分で止める。** Footsteps は CharacterController の velocity を見ていて、
            // PlayerController は CanMove が false のあいだ Move を一度も呼ばない。
            // 呼ばれない限り velocity は最後の値を持ち越すので、座ったまま歩き続けたことになり、
            // 走行中ずっとコンクリートの足音が鳴る。伏せる先がガレージではなく
            // プレイヤーの下にあるので、garage.SetActive(false) では消えない
            if (feet != null) feet.enabled = false;
            // 体の歩きも同じ理由で止める。止めないと、ドアが開くのを待つあいだ足踏みを続ける
            var motion = body != null ? body.GetComponent<BodyMotion>() : null;
            if (motion != null) motion.enabled = false;
            player.CanMove = false;

            // 板は閉じた姿から始める
            if (carDoor != null) carDoor.Set(0f);

            // **目の高さを足元へ写してから、足元そのものを目にする。**
            //
            // seat は足元ではなく目の位置。PlayerController は毎フレーム eye を
            // 足元から EyeHeight だけ上へ置き直すので、EyeHeight を 0 にして
            // 足元そのものを目として扱う。立っていたときの 1.6 のままだと目が屋根（1.52）の
            // 上へ突き抜け、車内のどの対象も判定の距離から外れて、
            // 一つ目のきっかけすら調べられなくなる。車内は座ったまま歩かないので、
            // 足元の高さはもう誰も使わない。
            //
            // 写さずに EyeHeight だけ 0 にすると、その 1 フレームで目が 1.6 m 落ちてしまう
            var eye = player.transform.position + Vector3.up * player.EyeHeight;
            // 体は Player の子なので、根を目へ上げた分だけ体の根を下げて地に残す。
            // 運転席の形（BuildDrive.Protagonist）も、この下げた根で解いてある
            if (body != null) body.transform.localPosition += Vector3.down * player.EyeHeight;
            player.EyeHeight = 0f;
            player.transform.position = eye;

            var seatYaw = seat != null ? seat.eulerAngles.y : player.Yaw;
            var sit = seat != null ? seat.position : eye;
            float yaw, pitch;

            // 蝶番から見た目の向き。閉じた板が 0 度、開ききった板が CarDoor.Swing 度。
            // 開くにつれ carDoor は回るので、向きの基は閉じているうちに控えておく
            var stood = player.transform.position;
            var hinge = carDoor != null ? carDoor.transform.position : stood;
            var along = carDoor != null ? -carDoor.transform.forward : Vector3.back;
            var outward = carDoor != null ? carDoor.transform.right : Vector3.right;
            var flat = stood - hinge;
            flat.y = 0f;
            var stoodFar = Mathf.Max(flat.magnitude, 1e-3f);
            var high = stood.y;
            var bearing = Mathf.Atan2(Vector3.Dot(flat, outward), Vector3.Dot(flat, along)) * Mathf.Rad2Deg;

            // ---- 一。立ったところでドアの方を向く ---------------------------------
            //
            // **足は動かさない。** ただし開ききった板より前（<see cref="CarDoor.Swing"/> 度
            // より大きい角）に立っていたときだけは、車体沿いに後ろへ滑らせる。
            // そこは開いた板の向こう側で、開いてしまうと板が目と戸口の間に立つ。
            // 板が閉じているうちなら邪魔は無いので、滑るのはこの段のあいだ。
            // 動くのは蝶番を中心にした弧の上だけで、車から離れはしない
            var kept = Mathf.Min(bearing, CarDoor.Swing);
            var stand = Reckon(hinge, along, outward, kept, stoodFar, high);
            Aim(stand, DoorAt(), out yaw, out pitch);
            yield return Slew(stood, stand, player.Yaw, yaw, player.Pitch, pitch, doorFace);
            bearing = kept;
            var way = (stand - hinge); way.y = 0f; way = way.normalized;

            // ---- 二。板が開く。迫ってきたぶんだけ譲る ----------------------------
            //
            // **退いてから開けるのではなく、立った場所から開ける。**
            // 決め打ちの場所へ先に退かせると、ドアのすぐ脇にいたときに
            // 2 m 以上も離れてから開くことになり、ドアから離れて見える。
            //
            // 板は目の高さを薙いで開くので、面が来たぶんだけは譲らざるを得ない。
            // 譲るのは蝶番から遠ざかる向きだけで、譲り終わるのは面が目の脇を
            // 通り過ぎるその瞬間。調べられる立ち位置 566 通りで測ると、
            // 退く距離は平均 0.44 m に収まる

            // 譲り終えた先と、面が目の脇を通り過ぎる頃合い
            var far = stoodFar;
            var pass = 1f;
            for (var s = 0f; s <= 1.0001f; s += 0.005f)
            {
                var turn = Ease(s) * CarDoor.Swing;
                far = Mathf.Max(far, Yield(bearing, turn));
                if (pass >= 1f && turn >= bearing) pass = s;
            }

            if (sound != null) sound.DoorOpen();
            for (var t = 0f; t < doorSwing; t += Time.deltaTime)
            {
                var k = doorSwing <= 0f ? 1f : t / doorSwing;
                if (carDoor != null) carDoor.Set(Ease(k));
                var gave = Mathf.Lerp(stoodFar, far, Ease(pass <= 0f ? 1f : k / pass));
                var at = hinge + way * gave;
                player.transform.position = new Vector3(at.x, high, at.z);
                // 板が開くにつれ取っ手も外へ出る。目はそれを追う
                Aim(player.transform.position, DoorAt(), out yaw, out pitch);
                player.Yaw = yaw;
                player.Pitch = pitch;
                flow.Freeze(FreezeStep);
                yield return null;
            }
            if (carDoor != null) carDoor.Set(1f);
            var stop = hinge + way * far;
            var clear = new Vector3(stop.x, high, stop.z);
            player.transform.position = clear;
            Aim(clear, DoorAt(), out yaw, out pitch);
            player.Yaw = yaw;
            player.Pitch = pitch;
            yield return Wait(doorHold);

            // ---- 三。戸口をくぐって運転席へ。車内が正面に来る ---------------------
            //
            // **直線で滑らせない。** 退いた場所から座席へ真っ直ぐ引くと、開いた板の
            // 外を掠めて斜めに吸い込まれる。戸口を制御点にした二次曲線で回り込ませ、
            // 向きはドアから車内（<see cref="cabinMark"/>）へ振る
            var gate = Gate(sit, clear);
            float inYaw, inPitch;
            Aim(sit, CabinAt(sit), out inYaw, out inPitch);
            var fromYaw = player.Yaw;
            var fromPitch = player.Pitch;
            for (var t = 0f; t < seatMove; t += Time.deltaTime)
            {
                var k = Ease(seatMove <= 0f ? 1f : t / seatMove);
                player.transform.position = Bend(clear, gate, sit, k);
                player.Yaw = Mathf.LerpAngle(fromYaw, inYaw, k);
                player.Pitch = Mathf.Lerp(fromPitch, inPitch, k);
                flow.Freeze(FreezeStep);
                yield return null;
            }
            player.transform.position = sit;
            // **座ったら体は運転席の向きに据え、ここからは首だけで振り向く。**
            // 体は Player の子なので、根を回したままドアを見ると、座った脚ごとドアの方を向く。
            // 根を座席の正面へ戻してから首を制限し、見ている向き（inYaw）は首の側へ移す
            player.transform.rotation = Quaternion.Euler(0f, seatYaw, 0f);
            player.HeadYawLimit = seatedYawLimit;
            player.Yaw = inYaw;
            player.Pitch = inPitch;
            if (body != null) body.Seated = true;
            yield return Wait(sitHold);

            // ---- 四。板の方へ向き直り、引いて閉める -------------------------------
            Aim(sit, DoorAt(), out yaw, out pitch);
            yield return Slew(sit, sit, inYaw, yaw, inPitch, pitch, doorLook);
            for (var t = 0f; t < doorShut; t += Time.deltaTime)
            {
                if (carDoor != null) carDoor.Set(1f - Ease(doorShut <= 0f ? 1f : t / doorShut));
                // 閉まっていく取っ手を目で追う。板より先に前を向くと、
                // 誰も触っていない板が勝手に閉まったように見える
                Aim(sit, DoorAt(), out yaw, out pitch);
                player.Yaw = yaw;
                player.Pitch = pitch;
                flow.Freeze(FreezeStep);
                yield return null;
            }
            if (carDoor != null) carDoor.Set(0f);
            if (sound != null) sound.DoorShut();
            Aim(sit, DoorAt(), out yaw, out pitch);

            // ---- 五。前を向く ----------------------------------------------------
            // 首の制限は、運転席に着いて体を正面へ据えたとき（三の終わり）に掛けてある。
            // **体の向きを決める前に掛けてはいけない。** PlayerController.Yaw は首が制限されていると
            // 体ではなく首を回すので、振り向いた先が体の正面のまま残る
            yield return Slew(sit, sit, yaw, seatYaw, pitch, 0f, faceFront);
            yield return Wait(shutHold);
            if (sound != null) sound.Ignition();

            // **黒へ落ちるのは、イグニッションを鳴らし終える pullHold 秒前。**
            // 黒のまま置く長さが pullHold なので、こうすると黒が明けるのと
            // 鍵の音が鳴り終わるのが同じ瞬間になる。走行音はそこから始まるので、
            // 明けた絵と走り出しの音がぴったり揃う。
            // 音より黒の方が長いときは、鳴らし始めたその場で黒へ落とす
            var lit = sound != null ? sound.IgnitionSeconds : 0f;
            // **掛かったところで震え出す。** 鍵を回している間は静かで、
            // 掛かってから黒へ落ちるまで、走行中よりおさえた震えが続く
            var black = Mathf.Max(0f, lit - pullHold);
            var quake = Mathf.Min(shakeAt, black);
            yield return Wait(quake);
            world.Idling = idleRough;
            yield return Wait(black - quake);

            // 鳴らし終えてから黒へ落ちるまでに間を置きたいときだけ、その間を埋める。
            // **間が 0 なら何も出さない。** 1 フレームだけ震えて鳴って消えるのは、
            // 演出ではなく不具合に見える
            if (ignitionHold > 0f)
            {
                world.Idling = idleRough;
                if (sound != null) sound.Idle(true);
                yield return Wait(ignitionHold);
            }

            // 黒を置くときだけ幕を下ろす。0 なら幕そのものを出さず、絵が切り替わるだけ
            if (pullHold > 0f) hud.SetFade(1f);
            if (garage != null) garage.SetActive(false);
            world.Rolling = true;
            // 走り出したら揺れは路面が持つ。残すと二重に揺れる
            world.Idling = 0f;
            if (sound != null) sound.Idle(false);
            Dress(0);
            band = 0;
            // 組み直すのは場面の頭でだけ。帯を跨ぐときには呼ばない
            clock.Reset();
            // **ここでは走行音を鳴らさない。** 黒のあいだはイグニッションの残りが鳴っている
            yield return Wait(pullHold);

            // 明ける。走行音はこの瞬間から。鍵の音が鳴り終わるのもここ
            if (sound != null) { sound.Road(At(0).gravel); sound.Weather(At(0).rain); }
            for (var t = 0f; t < pullFade; t += Time.deltaTime)
            {
                hud.SetFade(pullFade <= 0f ? 0f : 1f - t / pullFade);
                flow.Freeze(FreezeStep);
                yield return null;
            }
            hud.SetFade(0f);
            boarding = false;
            aboard = true;
        }

        /// <summary>演出のあいだ送りを止めておく長さ。秒。毎フレーム延長する</summary>
        const float FreezeStep = 0.25f;

        /// <summary>演出の間。待っているあいだも送りを止めておく</summary>
        System.Collections.IEnumerator Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                flow.Freeze(FreezeStep);
                yield return null;
            }
        }

        /// <summary>両端を緩めた 0〜1。目が急に動き出したり止まったりしないように</summary>
        static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>
        /// 目を from から to へ、向きを yawFrom/pitchFrom から yawTo/pitchTo へ、
        /// seconds 秒かけて移す。両端は緩める。待っているあいだ送りは止めておく
        /// </summary>
        System.Collections.IEnumerator Slew(Vector3 from, Vector3 to,
            float yawFrom, float yawTo, float pitchFrom, float pitchTo, float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Ease(seconds <= 0f ? 1f : t / seconds);
                player.transform.position = Vector3.Lerp(from, to, k);
                player.Yaw = Mathf.LerpAngle(yawFrom, yawTo, k);
                player.Pitch = Mathf.Lerp(pitchFrom, pitchTo, k);
                flow.Freeze(FreezeStep);
                yield return null;
            }
            player.transform.position = to;
            player.Yaw = yawTo;
            player.Pitch = pitchTo;
        }

        /// <summary>from から at を見るときの左右と上下の向き。度。上下は下向きが正</summary>
        static void Aim(Vector3 from, Vector3 at, out float yaw, out float pitch)
        {
            var way = at - from;
            var flat = new Vector2(way.x, way.z).magnitude;
            yaw = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            pitch = flat < 1e-4f ? 0f : -Mathf.Atan2(way.y, flat) * Mathf.Rad2Deg;
        }

        /// <summary>いまのドアの取っ手の座。板が開いていれば一緒に外へ出ている</summary>
        Vector3 DoorAt()
        {
            return carDoor != null ? carDoor.transform.TransformPoint(doorMark) : Vector3.zero;
        }

        /// <summary>目を向ける車内の座</summary>
        Vector3 CabinAt(Vector3 sit)
        {
            return seat != null ? seat.TransformPoint(cabinMark) : sit + Vector3.forward;
        }

        /// <summary>
        /// 板が turn 度のとき、その面から <see cref="doorClear"/> だけ空けるのに
        /// 要る、蝶番からの水平の距離。m。
        ///
        /// 面までの垂線は「蝶番からの距離 × 角の差の正弦」なので、隙間を保つ距離は
        /// その逆数で出る。角の差が閉じるほど遠くへ要求されるが、板の先
        /// （<see cref="doorReach"/>）を越えればもう面は無いので、そこで頭打ちにする
        /// </summary>
        float Yield(float bearing, float turn)
        {
            var off = Mathf.Abs(bearing - turn) * Mathf.Deg2Rad;
            var sin = Mathf.Sin(off);
            if (sin < 1e-3f) return doorReach + doorClear;
            return Mathf.Min(doorReach + doorClear, doorClear / sin);
        }

        /// <summary>
        /// 戸口の通り道。座席から運転席側へ <see cref="doorGate"/> だけ出したところ。
        ///
        /// 前後は目のいる位置に合わせ、戸口の縁（<see cref="doorMouth"/>）で止める。
        /// 座席の真横で決め打ちにすると、戸口の前寄りに立っていたときに
        /// いったん後ろへ振ってから入ることになり、開いた板を掠める
        /// </summary>
        Vector3 Gate(Vector3 sit, Vector3 from)
        {
            if (seat == null) return sit;
            var near = seat.InverseTransformPoint(from);
            return seat.TransformPoint(new Vector3(doorGate, 0f,
                Mathf.Clamp(near.z, doorMouth.x, doorMouth.y)));
        }

        /// <summary>蝶番から見て turn 度・far m のところ。高さは high</summary>
        static Vector3 Reckon(Vector3 hinge, Vector3 along, Vector3 outward, float turn, float far, float high)
        {
            var rad = turn * Mathf.Deg2Rad;
            var at = hinge + (along * Mathf.Cos(rad) + outward * Mathf.Sin(rad)) * far;
            return new Vector3(at.x, high, at.z);
        }

        /// <summary>三点の二次曲線。a から c へ、b の側へ膨らませて</summary>
        static Vector3 Bend(Vector3 a, Vector3 b, Vector3 c, float k)
        {
            var one = 1f - k;
            return one * one * a + 2f * one * k * b + k * k * c;
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
            // **速さと粗さをここで渡す。** Update は aboard が立つまで走らないので、
            // 渡さないと黒のあいだも明けるあいだも速さが 0 のままで、
            // フェードが明けたところから走り出すことになる。
            // 景色は走っているところから始まらなければならない
            world.Speed = At(which).speed;
            world.Rough = At(which).rough;
            world.Dress(which);
            world.Rewind();
            ShowTrigger(which);
            Arms(which);
            // 空と灯りもここで差し替える。**黒のあいだに呼ばれるのが要る。**
            // 走っている最中に時間帯が変わると、夜から朝へ切り替わるその一瞬が見える
            ShowSky(which);
            At(which).sky.Apply(sun, eye, beams);
            // 走行音も黒のあいだに入れ替える。舗装のまま続く景色では鳴らし直さない。
            // 乗り込みのときだけ Boarding が明けてから鳴らすので、ここでは出さない
            if (aboard && sound != null) sound.Road(At(which).gravel);
            // 雨は景色が持つ。風防の水とワイパーも音も、降っている景色でだけ出す
            if (rainRig != null) rainRig.SetActive(At(which).rain);
            if (aboard && sound != null) sound.Weather(At(which).rain);
            // 煙は景色を跨がない。黒のあいだに畳む
            if (smoke != null) smoke.Cancel();
            // 窓も景色を跨がない。開けたのは前の景色の中の話。
            // **寄せずにその場で戻す。** 寄せると、次の景色が窓の開いた音から始まって
            // 1.6 秒かけて閉まっていく
            if (sound != null) sound.Shut();
            // 煙の流れも戻す。窓を開けるまでは真っ直ぐ立ちのぼる
            Drift(false);
            // 家は独白を送り切ってから出す。景色に入った時点では小麦だけ
            croftsDue = false;
            ShowCrofts(false);
        }

        /// <summary>
        /// 煙を窓の方へ流すか。**窓を開けるまでは流さない。**
        /// 閉め切った車内で煙が横へ持っていかれる理由が無い。
        /// 開けた瞬間から、開いた窓（運転席側＝右）の方へ抜けていく
        /// </summary>
        void Drift(bool open)
        {
            if (smoke == null) return;
            var ps = smoke.GetComponent<ParticleSystem>();
            if (ps == null) return;
            var vel = ps.velocityOverLifetime;
            vel.x = open
                ? new ParticleSystem.MinMaxCurve(SmokeDriftLow, SmokeDriftHigh)
                : new ParticleSystem.MinMaxCurve(-SmokeStill, SmokeStill);
        }

        /// <summary>窓を開けたあと煙が横へ流れる速さ。m/s</summary>
        const float SmokeDriftLow = 0.10f;
        const float SmokeDriftHigh = 0.26f;
        /// <summary>閉め切った車内での漂い。m/s。BuildProps.BuildSmoke の既定と同じ</summary>
        const float SmokeStill = 0.018f;

        /// <summary>小麦畑の農家を出す／伏せる。区切りごとに 1 つあるのでまとめて切る</summary>
        void ShowCrofts(bool on)
        {
            for (var i = 0; i < crofts.Length; i++)
                if (crofts[i] != null && crofts[i].activeSelf != on) crofts[i].SetActive(on);
        }

        /// <summary>
        /// 道の先から入ってくる区切りにだけ農家を載せる。
        ///
        /// **一度に全部出さない。** 独白を送り切った瞬間に出すと、すぐ横にも
        /// 目の前にも家が湧いて見える。区切りは環を回って道の先（110 m）から
        /// 入ってくるので、そこまで下がった区切りにだけ載せれば、
        /// 家はいつも遠景から現れて近づいてくる
        /// </summary>
        void AdmitCrofts()
        {
            if (!croftsDue) return;
            for (var i = 0; i < crofts.Length; i++)
            {
                var held = crofts[i];
                if (held == null || held.activeSelf) continue;
                var slice = held.transform.parent;
                if (slice == null || slice.position.z < croftFrom) continue;
                held.SetActive(true);
            }
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
        /// which 番目の帯の腕の形にする。手動で運転する帯だけハンドルに手を乗せ、ほかは腕組み。
        /// -1（走り出す前）も腕組み。
        ///
        /// 最後の帯だけは原作どおり手動運転なので、ハンドルに手を乗せる。
        /// ただし見た目だけで、入力は受け付けない。帯と一緒に黒のあいだに入れ替わる
        /// </summary>
        void Arms(int which)
        {
            if (body != null) body.UseAlternate = which >= 0 && which == drivenBand;
        }
    }
}
