using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>
    /// 場面 4 の段の進行。記憶を一本ずつ流し、目を留めた人の脇に板を出し、
    /// その板から次の人へ渡し、`切断` で自室へ返す。
    ///
    /// 渡り歩きの決まりは <see cref="DiveChain"/> が持っている。
    /// ここはそれを場所・記憶・板・眩暈・色味・会話へ繋ぐだけにしてある。
    ///
    /// **記憶の中でもプレイヤーが歩く。** 鍵打ちで体を運んでいた版は、
    /// 振り向き・見上げ・抱き上げられて回るといった動きが続いて何が起きているか追えず、
    /// 差し戻された（設計書 1・2 節）。鍵打ちは記憶の頭の立ち位置と向きを決めるだけに使い、
    /// 据えたら手を離す。歩く速さだけは借りた体の <see cref="DiveEntry.speed"/> で変える。
    ///
    /// **<see cref="PlayerController"/> より先に動かす。** 記憶を切り替えたフレームに
    /// 前の記憶の入力で歩かれると、据えたはずの立ち位置から動いた所で絵が始まる。
    ///
    /// **記憶は勝手に終わらない。** 会話が尽きたら端末が次の人を選ぶ版は、
    /// 「勝手に他人の記憶に飛んでいる」と差し戻された（設計書 1・2・4 節）。
    /// 記憶から出る道は、人の脇の板の `潜る` と `切断` の二つだけで、どちらも
    /// プレイヤーが選ぶ。そのためここから <see cref="DiveChain.Next"/> は呼ばない。
    /// あちらは最初の一人を決める <see cref="DiveChain"/> の作りの一部として残してある。
    ///
    /// **会話は人を選んで進める。** 一行目（名を呼ぶ声）だけは記憶に入った瞬間に出て、
    /// 送らずに消える。二行目からは、次の会話の相手に目を留めて `E　話す` で始め、
    /// 一行ずつ E で送る。板はその人との会話が済んでから出す。決まりは
    /// <see cref="DiveEntry.Exchanges"/> から <see cref="DiveEntry.MayDive"/> までが持っている。
    ///
    /// **E が効くのは三つだけ。** 次の会話の相手への `E　話す`、話しているあいだの送り、
    /// 板の `潜る` と `切断`。一度の押下はこのうち一つにしか使わない。
    /// `E 次へ` は無い（設計書 2 節）
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class DiveDirector : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("右上に出す、いま潜っている人の行")]
        [SerializeField] TMP_Text caption;
        [SerializeField] DazeVolume daze;
        [Tooltip("記憶ごとのぼやけと色味を持つ Volume。中身を書き換えるのは HostBody")]
        [SerializeField] Volume volume;
        [SerializeField] HostBody body;
        [SerializeField] HoloPanel panel;
        [Tooltip("足音。場所ごとに床の音を取り替える")]
        [SerializeField] Footsteps feet;
        [SerializeField] DiveRoster roster;
        [Tooltip("五つの場所。DiveIds.Places の並び")]
        [SerializeField] Transform[] places = new Transform[0];
        [Tooltip("場所ごとの空・霞・環境光・日。places と同じ並び。記憶を切り替えるたびに差し替える")]
        [SerializeField] PlaceSky[] skies = new PlaceSky[0];
        [Tooltip("十六の記憶。一覧の番号の並び")]
        [SerializeField] Transform[] takes = new Transform[0];

        [Header("渡り歩き")]
        [Tooltip("何人渡れば `切断` が `潜る` と同じ大きさになるか")]
        [SerializeField] int cutAfter = 8;

        [Header("板")]
        [Tooltip("目の中央からこの角度の内側にいる人だけ拾う。度")]
        [SerializeField] float watchAngle = 12f;
        [Tooltip("目を留めてから板が出るまで、別の人へ乗り換えるまで。秒")]
        [SerializeField] float watchSeconds = 0.5f;
        [Tooltip("画面の端からこれだけ外へ出るまでは板を消さない。画面の幅・高さに対する割合")]
        [SerializeField] float edgeMargin = 0.08f;

        [Header("足音")]
        [Tooltip("団地・教室・台所の床。コンクリート")]
        [SerializeField] AudioClip[] hardSteps = new AudioClip[0];
        [Tooltip("公園の土と電車の板")]
        [SerializeField] AudioClip[] softSteps = new AudioClip[0];

        [Header("会話")]
        [Tooltip("一行目（名を呼ぶ声）を出しておく秒。送らずに消える。相手を探して振り向く間が要る")]
        [SerializeField] float firstSeconds = 9f;
        [Tooltip("〔区切り〕の後の会話を始められる、主の足元から行き先（か相手）までの横の隔たり。m")]
        [SerializeField] float reach = 1.6f;
        [Tooltip("行き先と主の足元の高さの差がこれを超えたら、横が近くても着いていない。m。階の違う真上と真下を分ける")]
        [SerializeField] float reachHigh = 1.2f;

        [Header("眩暈")]
        [Tooltip("`切断` が押せるようになってから、さらに cutAfter 人渡ったときの眩暈の濃さ")]
        [SerializeField] float dazeMax = 0.8f;

        [Header("切り替え")]
        [Tooltip("場面の頭で黒から明ける秒数")]
        [SerializeField] float openSeconds = 0.6f;
        [Tooltip("記憶の頭から、名前を呼ぶ声が鳴るまでの秒数")]
        [SerializeField] float callAfter = 0.3f;
        [Tooltip("切断してから自室が出るまで。秒。裂ける見え方はまだ無いので暗転で代えている")]
        [SerializeField] float cutSeconds = 0.4f;
        [Tooltip("切断で読むシーン。空なら読まずに黒いまま止まる")]
        [SerializeField] string nextScene = "Rest";
        [Tooltip("立たせるときに床から浮かせる高さ。m。着地ごとに沈み込まないように")]
        [SerializeField] float lift = 0.06f;

        DiveChain chain;
        DiveEntry entry;
        Take take;
        Transform place;
        Mover[] movers = new Mover[0];
        /// <summary>合図を持つ者が動き出した時刻。まだ合図が来ていなければ負</summary>
        float[] cued = new float[0];
        /// <summary>二本目の線に別の合図を持つ者が、二本目を数え始めた時刻。まだなら負</summary>
        float[] cued2 = new float[0];
        CharacterController hull;
        /// <summary>目のカメラ。人が画面に映っているかを見るのに要る</summary>
        Camera lens;
        /// <summary>記憶の頭からの秒</summary>
        float clock;
        bool called;
        /// <summary>
        /// これまでに出した行数。entry.said での番号 + 1 の、いちばん大きいもの。
        /// <see cref="Mover.Cue"/> はこれを見て動き出す
        /// </summary>
        int spoken;
        /// <summary>この記憶の会話。二行目から、相手が同じ行が続く所ごと</summary>
        Exchange[] talks = new Exchange[0];
        /// <summary>済んだ会話の数。会話は並びの順にしか進まないので、これだけで足りる</summary>
        int done;
        /// <summary>話している会話の中で、いま出している行。talks[done].lines での番号。話していなければ -1</summary>
        int line = -1;
        /// <summary>一行目を下げたか。秒が過ぎたときと、会話を始めたときに下げる</summary>
        bool hushed;
        /// <summary>動作確認から入れた E。次の一歩で一度だけ使う</summary>
        bool pending;
        /// <summary>目と相手のあいだを測る光線の当たり。毎フレーム作らない</summary>
        readonly RaycastHit[] hits = new RaycastHit[16];
        /// <summary>いま目を留めている相手。外していれば null</summary>
        Transform aimed;
        /// <summary>板か `E　話す` を、いま誰に出しているか</summary>
        Transform shown;
        /// <summary>同じ相手を留めている（あるいは外している）秒</summary>
        float dwell;
        int lastStep;
        bool cutting;

        /// <summary>いま潜っている人。一覧での番号。動作確認から読む</summary>
        public int Current { get { return chain != null ? chain.Current : -1; } }

        /// <summary>これまでに渡った人数。動作確認から読む</summary>
        public int Hops { get { return chain != null ? chain.Hops : 0; } }

        /// <summary>`切断` が押せるか。動作確認から読む</summary>
        public bool CanCut { get { return chain != null && chain.CanCut; } }

        /// <summary>いまの記憶の再生位置。秒。動作確認から読む</summary>
        public float Clock { get { return clock; } }

        /// <summary>これまでに出した会話の行数。動作確認から読む</summary>
        public int Spoken { get { return spoken; } }

        /// <summary>済んだ会話の数。動作確認から読む</summary>
        public int Done { get { return done; } }

        /// <summary>話しているあいだ。動作確認から読む</summary>
        public bool Talking { get { return line >= 0; } }

        /// <summary>いま板か `E　話す` を出している相手。出していなければ null。動作確認から読む</summary>
        public Transform Shown { get { return shown; } }

        /// <summary>
        /// 次の一歩で E を一度押したことにする。再生中の動作確認から呼ぶ
        /// （<see cref="SceneFlow.PressInteract"/> と同じ口）
        /// </summary>
        public void PressInteract() { pending = true; }

        void Awake()
        {
            if (player != null) hull = player.GetComponent<CharacterController>();
            if (player != null && player.Eye != null) lens = player.Eye.GetComponent<Camera>();
            if (lens == null) lens = Camera.main;
            if (player == null || roster == null || roster.Count == 0)
            {
                Debug.LogError("DiveDirector: player か記憶の一覧が未接続", this);
                enabled = false;
            }
        }

        IEnumerator Start()
        {
            // どれも直列化されないので、組み立てではなくここで掛ける。
            // 首の制限は解く。記憶の中でも場面 1・2・3 と同じに体ごと回って歩く
            player.CanMove = true;
            player.CanLook = true;
            player.HeadYawLimit = 0f;
            // 見るのは sharedProfile の方。profile は読んだだけで空の写しが出来てしまうので、
            // 繋ぎ忘れていても null にならず、ぼやけも色味も掛からないまま素通りする
            if (volume == null || volume.sharedProfile == null)
                Debug.LogWarning("DiveDirector: Volume に profile が無い。記憶ごとのぼやけと色味が掛からない", this);
            else volume.weight = 1f;

            chain = new DiveChain(roster.Count, DiveIds.Listed, cutAfter, new System.Random());
            Shut();
            Play(chain.Current);

            // 場面 3 からは暗転して来る。明けるのはこちらの仕事
            if (hud == null) yield break;
            hud.SetFade(1f);
            yield return hud.FadeTo(0f, openSeconds);
        }

        void OnDisable()
        {
            if (body != null) body.Clear();
            if (player == null) return;
            player.CanLook = true;
            // 借りた体の速さは場面の外へ持ち出さない
            player.SpeedScale = 1f;
        }

        void Update()
        {
            if (cutting || chain == null || take == null) return;
            var press = player.InteractPressed || pending;
            pending = false;
            Step(Time.deltaTime, press);
        }

        /// <summary>
        /// 一フレーム分。press はこのフレームで E が押されたか。
        ///
        /// **一度の E は一つにしか使わない。** 話しているあいだの E は送りにだけ使い、
        /// ここで返す。会話を閉じた E がそのまま板の `潜る` まで決めてしまうと、
        /// 最後の行を読み終えた指で他人の頭へ飛ぶことになる
        /// </summary>
        void Step(float dt, bool press)
        {
            // **速さで時計を倍にしない。** entry.speed が掛かるのは歩く速さだけ。
            // この時計が運ぶのは人と鳩の動き（Mover）と、名前を呼ぶ声の頭だけで、
            // 速さを掛けると設計書の秒数で書かれたその二つが早回しになる
            clock += dt;
            Drift();
            Voice();
            if (Talking) { Converse(press); return; }
            Call();
            Watch(dt);
            Choose(press);
            // ここで記憶を閉じない。会話を出し切っても、鍵打ちの秒を過ぎても、
            // 場所はそのまま続く。出る道は Choose の `潜る` と `切断` だけ
        }

        // ---- 記憶の切り替え --------------------------------------------------

        /// <summary>五つの場所と十六の記憶をすべて伏せる。開いたときの状態に頼らない</summary>
        void Shut()
        {
            for (var i = 0; i < places.Length; i++)
                if (places[i] != null) places[i].gameObject.SetActive(false);
            for (var i = 0; i < takes.Length; i++)
                if (takes[i] != null) takes[i].gameObject.SetActive(false);
        }

        /// <summary>i 番の記憶を頭から流す。前の記憶と場所は伏せる</summary>
        void Play(int i)
        {
            if (roster == null || i < 0 || i >= roster.Count) return;
            if (take != null) take.gameObject.SetActive(false);
            if (place != null) place.gameObject.SetActive(false);

            entry = roster[i];
            place = Where(entry.place);
            take = i < takes.Length && takes[i] != null ? takes[i].GetComponent<Take>() : null;
            if (take == null) { Debug.LogWarning("DiveDirector: 記憶 " + i + " が無い", this); return; }

            if (place != null) place.gameObject.SetActive(true);
            else Debug.LogWarning("DiveDirector: 場所が無い " + entry.place, this);
            Sky(entry.place);
            take.gameObject.SetActive(true);
            // 相手をしている人は、主の目の方へ首と頭を向ける（PersonMotion.Watch）
            var eye = player != null ? player.Eye : null;
            foreach (var person in take.GetComponentsInChildren<PersonMotion>(true)) person.Watch(eye);
            // 同じ人へ戻れば頭から流し直す。Mover は有効になった瞬間に開始位置へ戻る
            movers = take.GetComponentsInChildren<Mover>(true);
            cued = new float[movers.Length];
            cued2 = new float[movers.Length];
            for (var m = 0; m < cued.Length; m++) { cued[m] = -1f; cued2[m] = -1f; }

            if (body != null) body.Apply(entry);
            if (caption != null) caption.text = entry.row ?? "";
            Wear();
            Deepen();

            // 床の音は場所ごとに変える。団地・教室・台所はコンクリート、公園は土、電車は板
            if (feet != null) feet.Use(Soft(entry.place) ? softSteps : hardSteps);

            clock = 0f;
            called = false;
            aimed = null;
            shown = null;
            dwell = 0f;
            lastStep = 0;
            // 同じ人の記憶へ戻ったら、会話も頭から（設計書 7 節）
            spoken = 0;
            talks = DiveEntry.Exchanges(entry.said);
            done = 0;
            line = -1;
            hushed = false;
            pending = false;
            if (panel != null) panel.Hide();
            if (hud != null) { hud.SetSubtitle(null); hud.SetPrompt(null); }
            // 首は溜めた向きを体へ渡して正面へ戻す。前の記憶で振り向いたままだと、
            // 次の記憶が始まった瞬間に壁を見ていることになる
            player.ReleaseHead();
            player.HeadYawLimit = 0f;
            player.Pitch = 0f;
            player.SpeedScale = entry.speed;
            Stand();
            player.CanMove = true;
        }

        /// <summary>土と板の床。公園と電車だけ。ほかはコンクリート</summary>
        static bool Soft(string place)
        {
            return place == DiveIds.Park || place == DiveIds.Train;
        }

        /// <summary>
        /// 空・霞・環境光・日を、場所の分へ差し替える。場所ごとに時刻が違うので、
        /// 開口の向こうと見上げた先の色も、日の当たらない面の色も変わる。
        ///
        /// **一揃いで差し替える。** 五つの場所がそれぞれの時刻の空の絵と霞を持つ。
        /// 霞だけ差し替え忘れると、教室の昼の白い霞が電車の夕暮れの車内まで掛かる（<see cref="PlaceSky"/>）
        /// </summary>
        void Sky(string id)
        {
            var which = System.Array.IndexOf(DiveIds.Places, id);
            if (which < 0 || which >= skies.Length) return;
            skies[which].Apply(Camera.main);
        }

        /// <summary>id の場所。一覧の並びで探す</summary>
        Transform Where(string id)
        {
            var which = System.Array.IndexOf(DiveIds.Places, id);
            return which >= 0 && which < places.Length ? places[which] : null;
        }

        // ---- 主の体 ----------------------------------------------------------

        /// <summary>
        /// 記憶の頭の立ち位置と向きへ据える。
        ///
        /// **使うのは鍵打ちの先頭だけ。** 残りと <see cref="HostPath"/> はもう読まないが、
        /// <see cref="Take.Keys"/> は <see cref="Mover"/> の秒と揃えて書かれていて、
        /// 場所を作り直すときの下敷きになるので消さずに置いてある。
        ///
        /// **当たりを一度切ってから動かす。** 入れたまま置き直すと床や壁に押し出されて、
        /// 狙った立ち位置から数十センチずれる（<c>BuildAlley.Place</c>・<c>BuildDrive.Rig</c> と同じ手）。
        /// 目の高さは <see cref="HostBody.Apply"/> が記憶の頭で一度だけ入れる
        /// </summary>
        void Stand()
        {
            if (take == null) return;
            var keys = take.Keys;
            if (keys == null || keys.Length == 0) return;
            var key = keys[0];
            var turn = place != null ? place.eulerAngles.y + key.yaw : key.yaw;
            var foot = place != null ? place.TransformPoint(key.position) : key.position;

            if (hull != null) hull.enabled = false;
            player.transform.position = foot + Vector3.up * lift;
            player.transform.rotation = Quaternion.Euler(0f, turn, 0f);
            if (hull != null) hull.enabled = true;
        }

        /// <summary>
        /// 人と鳩を進める。<see cref="Mover"/> は自分の時計を持たないので、
        /// ここが渡さないと始まりの位置に止まったままになる
        /// </summary>
        void Drift()
        {
            for (var i = 0; i < movers.Length; i++)
            {
                if (movers[i] == null) continue;
                // 二本目に別の合図を持つ者は、その行が出てから二本目を数え始める（記憶 10 の孫息子）
                if (movers[i].NextCue >= 0 && cued2[i] < 0f && spoken >= movers[i].NextCue) cued2[i] = clock;
                var after = cued2[i] < 0f ? -1f : clock - cued2[i];
                // 合図を持たない者は記憶の時計で動く。鳩の飛び立ちのように、
                // 会話と関わりなく起きる出来事はこちら
                if (movers[i].Cue < 0) { movers[i].Play(clock, after); continue; }
                if (cued[i] < 0f)
                {
                    if (spoken < movers[i].Cue) { movers[i].Play(0f, after); continue; }
                    cued[i] = clock;
                }
                movers[i].Play(clock - cued[i], after);
            }
        }

        /// <summary>
        /// その人がいま線の上を歩いているか。合図がまだ来ていない者は、始まりの位置に立っている
        /// </summary>
        bool Walking(Transform who)
        {
            for (var i = 0; i < movers.Length; i++)
            {
                if (movers[i] == null || movers[i].transform != who) continue;
                var after = cued2[i] < 0f ? -1f : clock - cued2[i];
                if (movers[i].Cue < 0) return movers[i].Moving(clock, after);
                if (cued[i] < 0f) return false;
                return movers[i].Moving(clock - cued[i], after);
            }
            return false;
        }

        /// <summary>
        /// 次の会話を始められる所まで来ているか。
        ///
        /// **相手が歩いているあいだは始めない。** 降りてくる息子や戻ってくる孫、机の列を歩いてくる先生に、
        /// 着く前から話しかけられてしまう（設計書 7 節の「先生が机の列を歩いて近づいてから」）。
        /// **〔区切り〕の後の会話は、主が体を動かしてから次を話す所**（設計書 7 節）。三階まで駆け上がる、池の縁まで歩く、
        /// 玄関で靴を履く。主の足元が行き先（<see cref="Take.Stop"/>）の近くに来るまで始めない。行き先が無ければ相手のそば
        /// </summary>
        bool Arrived(Transform who)
        {
            if (DiveEntry.AllDone(talks, done)) return false;
            if (who == null || Walking(who)) return false;
            var talk = talks[done];
            if (!talk.Cut) return true;
            Vector3 goal;
            if (take.Stop(talk.stop, out goal)) goal = place != null ? place.TransformPoint(goal) : goal;
            else goal = who.position;
            var foot = player.transform.position;
            var flat = new Vector2(foot.x - goal.x, foot.z - goal.z);
            return flat.magnitude <= reach && Mathf.Abs(foot.y - goal.y) <= reachHigh;
        }

        /// <summary>
        /// 名前を呼ぶ声。音源はまだ無いので、たいていは何も鳴らずに過ぎる。
        /// 声は借りた体の内側で聞こえるものなので、耳の位置から鳴らす
        /// </summary>
        void Voice()
        {
            if (called || take.Call == null || clock < callAfter) return;
            called = true;
            AudioSource.PlayClipAtPoint(take.Call, player.Eye != null ? player.Eye.position : transform.position);
        }

        // ---- 会話 ------------------------------------------------------------
        //
        // 設計書 7 節。**独白は無い。** 顔は見せないので、誰が喋っているかは声の向きと
        // 一行に含まれた名前でしか伝わらない。
        // Dive.unity に SceneFlow は無いので、HudView を直に触る

        /// <summary>
        /// 一行目。名を呼ぶ声で、記憶に入った瞬間に出て、<see cref="firstSeconds"/> で消える。送らない。
        ///
        /// **帯は薄く細いまま。** E で送る字幕（場面 1・2・3・8 と同じ帯）と同じ画にすると、
        /// 勝手に消える行まで送り待ちに見える（<see cref="HudView.SetPassing"/>）
        /// </summary>
        void Call()
        {
            if (hushed) return;
            var calling = DiveEntry.Calling(entry.said, clock, firstSeconds);
            if (calling == null) { Hush(); return; }
            if (spoken > 0) return;
            spoken = 1;
            if (hud != null) hud.SetPassing(calling);
        }

        /// <summary>一行目を下げる。一度下げたら、その記憶のあいだは出し直さない</summary>
        void Hush()
        {
            if (hushed) return;
            hushed = true;
            if (hud != null) hud.SetPassing(null);
        }

        /// <summary>
        /// 次の会話を始める。話しているあいだは歩けない。見回しはできる。
        /// 足音も止まる（<see cref="Footsteps"/> が <see cref="PlayerController.CanMove"/> を見ている）
        /// </summary>
        void Begin()
        {
            if (DiveEntry.AllDone(talks, done)) return;
            Hush();
            Drop();
            line = 0;
            player.CanMove = false;
            Say();
        }

        /// <summary>いまの行を帯に出す。場面 1・2・3・8 と同じ帯で、E で送るまで出しておく</summary>
        void Say()
        {
            var at = talks[done].lines[line];
            spoken = Mathf.Max(spoken, at + 1);
            if (hud != null) hud.SetSubtitle(entry.said[at].line, SubtitleKind.Line);
        }

        /// <summary>
        /// 話しているあいだ。E で一行ずつ送り、最後の行を送ったら会話を閉じる。
        ///
        /// **閉じたら目の留めを解く。** 閉じたその場で板を出すと、送りの E を重ねて押した
        /// 指がそのまま `潜る` を決める。板は目を留め直して <see cref="watchSeconds"/> 待ってから出す
        /// </summary>
        void Converse(bool press)
        {
            if (!press) return;
            line++;
            if (line < talks[done].lines.Length) { Say(); return; }
            line = -1;
            done++;
            if (hud != null) hud.SetSubtitle(null);
            player.CanMove = true;
            Drop();
        }

        /// <summary>
        /// 渡るたびに眩暈を一段濃くする。
        ///
        /// **`切断` が押せるようになるまでは掛けない。** 最初の一人から Hops / cutAfter で濃くしていた頃は、
        /// 二人目でもう画が二重にぶれ始めて、「視界がぼやけるのが速い。8人までは無効にして」と差し戻された。
        /// 目の疲れ（<see cref="Wear"/>）と同じく <see cref="DiveChain.Past"/> で上げる。
        /// cutAfter 人に届くまで 0、押せるようになったその瞬間もまだ 0、そこから先でもう cutAfter 人渡るあいだに
        /// <see cref="dazeMax"/> まで上がる
        /// </summary>
        void Deepen()
        {
            if (daze == null) return;
            var step = Dizziness(chain, dazeMax);
            daze.Hold(step, step);
        }

        /// <summary>いまの人数での眩暈の濃さ。0 から most。動作確認とテストから読む</summary>
        public static float Dizziness(DiveChain chain, float most)
        {
            return chain != null ? chain.Past * most : 0f;
        }

        // ---- 板 --------------------------------------------------------------

        /// <summary>
        /// 目を留めた相手の脇に板を出す。一つの記憶に人が何人いても、
        /// 出るのは一人だけ。
        ///
        /// **一度出た板は、中央から外れたくらいでは消さない。** 目の中央から外れて
        /// 半秒で消していた頃は、歩きながら人を画面の中央に留め続けられず、
        /// 近づくだけで消えていた（オーナーの差し戻し）。消えるのは、
        /// その人が画面から外れたときと、別の人がもっと中央へ来たときの二つだけ
        /// </summary>
        void Watch(float dt)
        {
            if (panel == null) return;
            if (shown != null && (!OnScreen(shown) || !Offers(shown) || !Visible(shown))) Drop();

            var who = Nearest();
            // もっと中央に近い人が現れなければ、狙いは出ている人のまま
            if (who == null) who = shown;
            if (who != aimed) { aimed = who; dwell = 0f; }
            else dwell += dt;

            // 出すのも、別の人へ乗り換えるのも、同じだけ目を留めてから
            if (aimed != null && aimed != shown && dwell >= watchSeconds)
            {
                shown = aimed;
                lastStep = 0;
                // 板は潜ってよい人にだけ出す。次の会話の相手には、画面の下の `E　話す` だけ
                if (Divable(shown)) panel.Show(shown, entry.row, Row(Target(shown)));
                else panel.Hide();
            }
            if (shown == null) return;
            if (!Divable(shown))
            {
                if (hud != null) hud.SetPrompt(Key + TalkLabel);
                return;
            }
            panel.Grow(chain.CutSize);
            Guide();
        }

        /// <summary>
        /// その人の脇に板を出してよいか。会話の相手なら、その人との会話が済んだあと。
        /// 会話を持たない人なら、この記憶の会話が全部済んだあと
        /// </summary>
        bool Divable(Transform who)
        {
            return who != null && DiveEntry.MayDive(talks, done, who.name);
        }

        /// <summary>
        /// その人と次の会話を始められるか。会話は並びの順にしか始められない。
        /// 〔区切り〕の後の会話は、行き先まで来てから（<see cref="Arrived"/>）
        /// </summary>
        bool Talkable(Transform who)
        {
            return who != null && DiveEntry.CanTalk(talks, done, who.name) && Arrived(who);
        }

        /// <summary>
        /// 目を留める甲斐のある人か。板が出るか、`E　話す` が出るか。
        /// どちらでもない人（まだ順の来ない会話の相手、会話が残っているあいだの会話を持たない人）は
        /// 狙いに入れない。入れると、その人が次の相手より中央に来ただけで何も出なくなる
        /// </summary>
        bool Offers(Transform who)
        {
            return Divable(who) || Talkable(who);
        }

        /// <summary>
        /// 渡り歩いた分だけ目を疲れさせ、画面の角を白く曇らせる。
        ///
        /// **`切断` が押せるようになるまでは素のまま。** 借りた目の出来を早くから出すと、
        /// 何を見ればよいのか分からないと三度差し戻された。<see cref="DiveChain.Past"/> は
        /// cutAfter 人に届くまで 0 で、そこから先で上がる。帰る口が開いてもなお
        /// 潜り続けたぶんだけ目が利かなくなる。
        ///
        /// 角の白い膜だけは最初から出しておく。あれは目の出来ではなく、
        /// 他人の記憶の中にいること自体の見え方だから
        /// </summary>
        void Wear()
        {
            if (body != null) body.Strain = chain.Past;
            if (hud != null) hud.SetHaze(1f);
        }

        /// <summary>板と、画面の下の案内をまとめて下げる</summary>
        void Drop()
        {
            shown = null;
            aimed = null;
            dwell = 0f;
            if (panel != null) panel.Hide();
            if (hud != null) hud.SetPrompt(null);
        }

        /// <summary>
        /// その人がまだ画面に映っているか。一度出た板を消してよいかは、これだけで決める。
        /// 端でふつりと消えないように、少し外へ出るまでは映っていることにする
        /// </summary>
        bool OnScreen(Transform who)
        {
            if (who == null || !who.gameObject.activeInHierarchy) return false;
            if (lens == null) return true;
            var at = lens.WorldToViewportPoint(Head(who));
            if (at.z <= 0f) return false;
            return at.x >= -edgeMargin && at.x <= 1f + edgeMargin
                && at.y >= -edgeMargin && at.y <= 1f + edgeMargin;
        }

        /// <summary>その人の顔のあたり。足元で測ると、近くに立つほど下を向かないと拾えない</summary>
        static Vector3 Head(Transform who)
        {
            return who.position + Vector3.up * 1.2f;
        }

        /// <summary>
        /// 目の中央にいちばん近い人。誰も角の内側にいなければ null。
        ///
        /// **いま板が出ている人より中央に近い人しか返さない。** 二人が並んで立つ記憶で、
        /// 首を少し振るたびに板が行き来すると、どちらの脇に出ているのか読めなくなる。
        ///
        /// **壁の向こうの人は返さない。** 目の向きとの角度だけで拾っていた頃は、
        /// 廊下の壁越しに隣の部屋の人まで狙えた（設計書 7 節）。<see cref="Visible"/> を見る
        /// </summary>
        Transform Nearest()
        {
            var eye = player.Eye;
            if (eye == null || take == null) return null;
            Transform best = null;
            var closest = watchAngle;
            if (shown != null)
            {
                var held = Vector3.Angle(eye.forward, Head(shown) - eye.position);
                if (held < closest) closest = held;
            }
            var people = take.People;
            for (var i = 0; i < people.Length; i++)
            {
                var who = people[i];
                if (who == null || !who.gameObject.activeInHierarchy) continue;
                if (!Offers(who)) continue;
                var toward = Head(who) - eye.position;
                if (toward.sqrMagnitude < 1e-4f) continue;
                var apart = Vector3.Angle(eye.forward, toward);
                if (apart >= closest) continue;
                // 光線は角の内側に入った人にだけ撃つ
                if (!Visible(who)) continue;
                closest = apart;
                best = who;
            }
            return best;
        }

        /// <summary>
        /// 目からその人が見えているか。頭と胸のどちらかへ遮る物無しに届けば見えている。
        /// 片方だけにすると、腰の高さの手すりや、頭の高さの垂れ壁の下から覗く人を落とす
        /// </summary>
        bool Visible(Transform who)
        {
            var eye = player.Eye;
            if (eye == null || who == null) return true;
            Vector3 head, chest;
            Body(who, out head, out chest);
            return Blocking(eye.position, head, who) == null || Blocking(eye.position, chest, who) == null;
        }

        /// <summary>
        /// from から to までを遮る物。無ければ null。
        ///
        /// 除くのは、相手自身・プレイヤーの <see cref="CharacterController"/>・トリガー・
        /// 絵を持たない当たり（地面の端の見えない仕切りや、台や机の見立ての箱）。
        /// 見えない物は目を遮らない。当たりを持たない家具も遮らないが、それでよい。
        ///
        /// **行きと帰りの二度撃つ。** 壁の当たりは面を焼いた mesh なので、裏から撃つと
        /// 抜けてしまう。どちら向きに張った面でも、どちらかの向きで当たる
        /// </summary>
        Collider Blocking(Vector3 from, Vector3 to, Transform who)
        {
            var hit = Blocking1(from, to, who);
            return hit != null ? hit : Blocking1(to, from, who);
        }

        Collider Blocking1(Vector3 from, Vector3 to, Transform who)
        {
            var toward = to - from;
            var far = toward.magnitude;
            if (far < 1e-3f) return null;
            var n = Physics.RaycastNonAlloc(from, toward / far, hits, far,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger) continue;
                if (c == hull) continue;
                if (who != null && c.transform.IsChildOf(who)) continue;
                if (c.GetComponent<Renderer>() == null) continue;
                return c;
            }
            return null;
        }

        /// <summary>
        /// その人の頭と胸。体の大きさで測る。背丈は模型と縮尺でまちまちで、
        /// 高さを決め打ちにすると、子どもでは頭の上の何も無い所を狙うことになる
        /// </summary>
        static void Body(Transform who, out Vector3 head, out Vector3 chest)
        {
            var shape = who.GetComponentInChildren<Renderer>();
            if (shape == null)
            {
                head = Head(who);
                chest = who.position + Vector3.up * 0.9f;
                return;
            }
            var box = shape.bounds;
            head = new Vector3(box.center.x, box.max.y - box.size.y * 0.08f, box.center.z);
            chest = new Vector3(box.center.x, box.min.y + box.size.y * 0.70f, box.center.z);
        }

        // ---- 画面の下の案内 --------------------------------------------------

        /// <summary>鍵の案内の頭。場面 1・2・3・8 の `E ○○` と同じ書式</summary>
        const string Key = "E  ";

        /// <summary>`E` の後ろに続く、潜る先の言い方</summary>
        const string DiveLabel = "この人の記憶へ潜る";

        /// <summary>次の会話の相手に目を留めたときの、`E` の後ろに続く言い方</summary>
        const string TalkLabel = "話す";

        /// <summary>選んでいない方に付ける余白。<see cref="Choice.Compose"/> と同じ形に揃える</summary>
        const string Blank = "　　";

        /// <summary>
        /// 上下で選べることを言い添える。<see cref="Choice"/> の二択は左右で動かすので、
        /// 印だけ倣っても軸までは伝わらない
        /// </summary>
        const string Axis = "（↑↓ で選ぶ）";

        /// <summary>
        /// 板が出ているあいだ、画面の下に `E ○○` を出す。
        ///
        /// **潜るのはプレイヤーが決めることだと、この一行で伝える。** 板の二行目だけでは
        /// 「自分が選んでいるのかどうか分からない」と差し戻された。調べられるものに
        /// 近づくと `E ○○` が出るのは場面 1・2・3・8 で通した決まりなので、
        /// 同じ場所・同じ書式に載せる。
        ///
        /// 会話の `E　話す` は <see cref="Watch"/> が出す。話しているあいだは案内を出さない。
        /// 場面 1・2・3・8 と同じく、帯が出ていて案内が無いことが「E で送る」の合図になる
        /// </summary>
        void Guide()
        {
            if (hud == null || panel == null || chain == null) return;
            if (!chain.CanCut) { hud.SetPrompt(Key + DiveLabel); return; }
            var dive = (panel.Index == 0 ? Choice.Cursor : Blank) + DiveLabel;
            var cut = (panel.Index == 1 ? Choice.Cursor : Blank) + HoloPanel.Cut;
            hud.SetPrompt(Key + dive + Blank + cut + "　" + Axis);
        }

        /// <summary>その人の飛び先。一覧に無ければ -1</summary>
        int Target(Transform who)
        {
            if (who == null) return -1;
            var seen = entry.seen;
            if (seen == null) return -1;
            for (var i = 0; i < seen.Length; i++)
                if (seen[i].name == who.name) return seen[i].target;
            return -1;
        }

        /// <summary>一覧の i 番の行。範囲の外なら空</summary>
        string Row(int i)
        {
            return roster != null && i >= 0 && i < roster.Count ? roster[i].row : "";
        }

        // ---- 選ぶ ------------------------------------------------------------

        /// <summary>
        /// 板が出ているあいだの入力。上下で `潜る` と `切断` を選び、E で決める。
        ///
        /// **左右（<see cref="PlayerController.ChoiceStep"/>）は使わない。**
        /// あれは Move の x をそのまま読むので、記憶の中を歩くようになった今は
        /// 横へ一歩動くたびに選びが入れ替わる。設計書 3 節の「上下（マウスの車輪、
        /// または矢印）」がそのまま <see cref="PlayerController.LogStep"/> にあるので、そちらを読む。
        ///
        /// 板ではなく `E　話す` を出している相手なら、E で会話を始める。
        /// どちらも出ていなければ E は何もしない
        /// </summary>
        void Choose(bool press)
        {
            if (panel == null || shown == null) return;
            if (!Divable(shown))
            {
                if (press && Talkable(shown)) Begin();
                return;
            }
            // 車輪を手前へ回すと 1。上が `潜る`、下が `切断` なので向きを裏返す
            var step = -player.LogStep;
            if (step != 0 && step != lastStep)
            {
                panel.Select(step > 0 ? 1 : 0);
                // 案内は Watch が毎フレーム出しているが、選んだ手応えを一フレーム遅らせない
                Guide();
            }
            lastStep = step;
            if (!press) return;
            if (panel.Index == 1 && chain.CanCut) { Cut(); return; }
            var target = Target(shown);
            if (target < 0) return;
            chain.Hop(target);
            Play(chain.Current);
        }

        // ---- 切断 ------------------------------------------------------------

        /// <summary>ケーブルを抜く。渡った人数だけを場面 5 へ持ち越す</summary>
        void Cut()
        {
            if (cutting) return;
            cutting = true;
            DiveHandoff.Hops = chain.Hops;
            DiveHandoff.FromDive = true;
            StartCoroutine(Cutting());
        }

        IEnumerator Cutting()
        {
            if (panel != null) panel.Hide();
            // 抜けるあいだは歩きも見回しも受け付けない。視線はもう主のものではない
            player.CanMove = false;
            player.CanLook = false;
            if (hud != null)
            {
                hud.SetPrompt(null);
                hud.SetSubtitle(null);
                // 設計書の「画面が裂ける」はまだ作っていない。いまは暗転で代える
                yield return hud.FadeTo(1f, cutSeconds);
            }
            // 借りた体の色味とぼやけ、角の白い膜は、自室が出る前に落とす
            if (body != null) body.Clear();
            if (hud != null) hud.SetHaze(0f);
            if (volume != null) volume.weight = 0f;
            if (!SceneExit.Continues(nextScene))
            {
                Debug.LogWarning("DiveDirector: 切断の行き先が空。黒いまま止まる", this);
                yield break;
            }
            SceneManager.LoadScene(SceneExit.Target(nextScene));
        }
    }
}
