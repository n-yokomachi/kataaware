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
        [Tooltip("吐き終わってから独白が出るまで。秒")]
        [SerializeField] float afterSmoke = 0.8f;

        [Header("家")]
        [Tooltip("小麦畑の農家。独白を送り切るまで伏せておく")]
        [SerializeField] GameObject crofts;
        [Tooltip("ガレージの床を歩く足音。乗り込んだら止める")]
        [SerializeField] Footsteps feet;

        [Header("乗り込み")]
        [Tooltip("ドア・イグニッション・動き出しと走行音")]
        [SerializeField] DriveSound sound;
        [Tooltip("ドアを開けてから目が動き出すまで。秒")]
        [SerializeField] float doorHold = 0.45f;
        [Tooltip("立ち位置から運転席まで目を滑らせる秒数")]
        [SerializeField] float seatMove = 1.6f;
        [Tooltip("運転席に着いてからドアを閉めるまで。秒")]
        [SerializeField] float sitHold = 0.5f;
        [Tooltip("ドアを閉めてからイグニッションまで。秒")]
        [SerializeField] float shutHold = 0.8f;
        [Tooltip("イグニッションを鳴らし終えてから黒へ切り替わるまで。秒。" +
            "0 なら鳴らし終えた時点で黒へ落ちる。この間だけエンジンの震えと" +
            "止まったままの音が入るので、0 のときはどちらも出さない")]
        [SerializeField] float ignitionHold = 0f;
        [Tooltip("エンジンだけ掛かっているときの震え。走行中の粗さ 1.0 に対する割合")]
        [SerializeField] float idleRough = 0.55f;
        [Tooltip("座ってから左右に振れる角度。度。片側の値。90 で前方 180 度")]
        [SerializeField] float seatedYawLimit = 90f;
        [Tooltip("黒のまま置く秒数。ここで車の動き出す音が流れる")]
        [SerializeField] float pullHold = 4.6f;
        [Tooltip("黒から一つ目の景色へ浮かび上がる秒数")]
        [SerializeField] float pullFade = 1.8f;

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
        /// <summary>乗り込みの最中。aboard はこの一連が終わってから立てる</summary>
        bool boarding;
        /// <summary>場面が閉じたときに走行音を止めた</summary>
        bool hushed;
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

            // 独白を送り切ったか。積んだ字幕が尽きたところで余韻へ移る
            if (clock.Beat == DriveBeat.Talking && !flow.Talking)
            {
                clock.Spoken();
                // ここで家が現れる。小麦だけの畑を走ってきて、
                // 独白を読み終えたところで人の住むところに差し掛かる
                if (crofts != null) crofts.SetActive(true);
            }

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
        /// 煙草に火を点け、一服して、窓を下ろしてから独白を出す。
        ///
        /// 対象そのものの文（「煙草に火をつけ、窓を開ける」）は SceneFlow が先に出すので、
        /// それを送り切るまで待つ。待たずに始めると、字幕の裏で火が点いて煙が立つ
        /// </summary>
        System.Collections.IEnumerator Smoking()
        {
            while (flow.Talking) yield return null;
            cigarette.Light(drags);

            // **窓は吸ってから吐くまでの間に開ける。** 火を点ける → 吸う →
            // 窓が下りる → 吐く、の順。SmokeBeats の時刻表から、一服目を吸い終わる
            // ところを割り出して鳴らす。Cigarette 自身は窓を知らないので、
            // 掛け合わせはここで持つ
            var lit = Time.time;
            var openAt = SmokeBeats.DragAt(0, drags) + SmokeBeats.DragSeconds;
            var opened = false;
            while (cigarette.Smoking)
            {
                if (!opened && Time.time - lit >= openAt)
                {
                    opened = true;
                    if (sound != null) sound.WindowDown();
                }
                flow.Freeze(FreezeStep);
                yield return null;
            }
            // 吸い終わりが早すぎて窓を鳴らしそびれたときの保険
            if (!opened && sound != null) sound.WindowDown();

            // **煙は消さない。** 一服ぶんで切ると、独白のあいだ煙が無い車内になる。
            // 暗転までは吸っている扱いにして、そこで Dress が畳む
            if (smoke != null) smoke.Begin(SmokeSeconds);

            yield return Wait(afterSmoke);
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
            if (garageOnly != null) garageOnly.SetActive(false);
            // **足音は自分で止める。** Footsteps は CharacterController の velocity を見ていて、
            // PlayerController は CanMove が false のあいだ Move を一度も呼ばない。
            // 呼ばれない限り velocity は最後の値を持ち越すので、座ったまま歩き続けたことになり、
            // 走行中ずっとコンクリートの足音が鳴る。伏せる先がガレージではなく
            // プレイヤーの下にあるので、garage.SetActive(false) では消えない
            if (feet != null) feet.enabled = false;
            player.CanMove = false;

            if (sound != null) sound.DoorOpen();
            yield return Wait(doorHold);

            // 目を運転席へ滑らせる。
            //
            // seat は足元ではなく目の位置。PlayerController は毎フレーム eye を
            // 足元から EyeHeight だけ上へ置き直すので、EyeHeight を 0 にして
            // 足元そのものを目として扱う。立っていたときの 1.6 のままだと目が屋根（1.52）の
            // 上へ突き抜け、車内のどの対象も判定の距離から外れて、
            // 一つ目のきっかけすら調べられなくなる。車内は座ったまま歩かないので、
            // 足元の高さはもう誰も使わない。
            //
            // **切り替える前に、今の目の高さを足元へ写す。** 写さずに EyeHeight だけ
            // 0 にすると、その 1 フレームで目が 1.6 m 落ちてから滑り始める
            var from = player.transform.position + Vector3.up * player.EyeHeight;
            player.EyeHeight = 0f;
            player.transform.position = from;
            var to = seat != null ? seat.position : from;
            var yawFrom = player.Yaw;
            var yawTo = seat != null ? seat.eulerAngles.y : yawFrom;
            var pitchFrom = player.Pitch;
            for (var t = 0f; t < seatMove; t += Time.deltaTime)
            {
                var k = Ease(seatMove <= 0f ? 1f : t / seatMove);
                player.transform.position = Vector3.Lerp(from, to, k);
                player.Yaw = Mathf.LerpAngle(yawFrom, yawTo, k);
                player.Pitch = Mathf.Lerp(pitchFrom, 0f, k);
                flow.Freeze(FreezeStep);
                yield return null;
            }
            player.transform.position = to;
            player.Yaw = yawTo;
            player.Pitch = 0f;
            // **首の制限は体の向きを決めたあとで掛ける。** PlayerController.Yaw は
            // 首が制限されていると体ではなく首を回すので、先に掛けると
            // 運転席の正面ではなく立っていたときの向きが体の正面のまま残る
            player.HeadYawLimit = seatedYawLimit;
            yield return Wait(sitHold);

            if (sound != null) sound.DoorShut();
            yield return Wait(shutHold);
            if (sound != null) sound.Ignition();
            // 鳴らし終えるまで待ってから震え出す。掛かった音の途中で震えると、
            // まだ掛かっていないエンジンで車が揺れることになる
            yield return Wait(sound != null ? sound.IgnitionSeconds : 0f);
            // 鳴らし終えてから黒へ落ちるまでに間があるときだけ、その間を埋める。
            // **間が 0 なら何も出さない。** 1 フレームだけ震えて鳴って消えるのは、
            // 演出ではなく不具合に見える
            if (ignitionHold > 0f)
            {
                world.Idling = idleRough;
                if (sound != null) sound.Idle(true);
                yield return Wait(ignitionHold);
            }

            // 黒へは切り替えで入る。場面 1 のドアを閉める暗転と同じ扱い
            hud.SetFade(1f);
            if (sound != null) sound.PullAway();
            if (garage != null) garage.SetActive(false);
            world.Rolling = true;
            // 走り出したら揺れは路面が持つ。残すと二重に揺れる
            world.Idling = 0f;
            // 動き出しの音に渡す。止まっているエンジンの輪はここまで
            if (sound != null) sound.Idle(false);
            Dress(0);
            band = 0;
            // 組み直すのは場面の頭でだけ。帯を跨ぐときには呼ばない
            clock.Reset();
            yield return Wait(pullHold);

            // 明けるのはフェードイン。走行音と雨はここから
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
            // 走行音も黒のあいだに入れ替える。舗装のまま続く景色では鳴らし直さない。
            // 乗り込みのときだけ Boarding が明けてから鳴らすので、ここでは出さない
            if (aboard && sound != null) sound.Road(At(which).gravel);
            // 雨は景色が持つ。風防の水とワイパーも音も、降っている景色でだけ出す
            if (rainRig != null) rainRig.SetActive(At(which).rain);
            if (aboard && sound != null) sound.Weather(At(which).rain);
            // 煙は景色を跨がない。黒のあいだに畳む
            if (smoke != null) smoke.Cancel();
            // 家は独白を送り切ってから出す。景色に入った時点では小麦だけ
            if (crofts != null) crofts.SetActive(false);
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
