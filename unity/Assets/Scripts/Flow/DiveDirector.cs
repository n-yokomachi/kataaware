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
    /// **`E 次へ` は無い。** 板が出ていないときの E は何もしない
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
        [Tooltip("場所ごとの空の色。places と同じ並び。開口の向こうと、見上げた先に出る")]
        [SerializeField] Color[] skies = new Color[0];
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
        [Tooltip("一行を出したままにしておく秒数")]
        [SerializeField] float talkSeconds = 4f;
        [Tooltip("一行出してから、次の行を出せるようになるまでの間。秒")]
        [SerializeField] float lineGap = 1.5f;

        [Header("眩暈")]
        [Tooltip("cutAfter 人まで渡ったときの眩暈の濃さ")]
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
        CharacterController hull;
        /// <summary>目のカメラ。人が画面に映っているかを見るのに要る</summary>
        Camera lens;
        /// <summary>記憶の頭からの秒</summary>
        float clock;
        bool called;
        /// <summary>次に出す会話の行。entry.said での番号。この行の点だけが armed</summary>
        int spoken;
        /// <summary>この秒で字幕を消す。0 以下なら出ていない</summary>
        float silence;
        /// <summary>この秒までは次の行を出さない。点が近いと、読む間もなく次が重なる</summary>
        float held;
        /// <summary>いま目を留めている相手。外していれば null</summary>
        Transform aimed;
        /// <summary>板がいま誰の脇に出ているか</summary>
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
            // **速さで時計を倍にしない。** entry.speed が掛かるのは歩く速さだけ。
            // この時計が運ぶのは人と鳩の動き（Mover）と、名前を呼ぶ声の頭だけで、
            // 速さを掛けると設計書の秒数で書かれたその二つが早回しになる
            clock += Time.deltaTime;
            Drift();
            Voice();
            Talk();
            Watch();
            Choose();
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
            // 同じ人へ戻れば頭から流し直す。Mover は有効になった瞬間に開始位置へ戻る
            movers = take.GetComponentsInChildren<Mover>(true);

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
            spoken = 0;
            silence = 0f;
            held = 0f;
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
        /// 空の色。場所ごとに時刻が違うので、開口の向こうと見上げた先の色を変える。
        ///
        /// **空は張っていない。** 記憶はどれも屋内か暗がりで、天球を回すほどの
        /// 空は映らない。カメラの塗り潰しを場所に合わせて差し替えるだけで足りる
        /// </summary>
        void Sky(string id)
        {
            var eye = Camera.main;
            if (eye == null) return;
            var which = System.Array.IndexOf(DiveIds.Places, id);
            if (which < 0 || which >= skies.Length) return;
            eye.backgroundColor = skies[which];
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
                if (movers[i] != null) movers[i].Play(clock);
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

        /// <summary>
        /// 記憶の中のやりとりを字幕帯に出す。設計書 7 節。
        ///
        /// **送るのは歩くこと。** 一行目は記憶に入った瞬間（名前を呼ばれる声）に出て、
        /// 二行目からは、場所に置いた点へ主が入るたびに一つずつ出る。秒で流していた版は、
        /// 何をすれば進むのか読めないと差し戻された。
        ///
        /// 点は順に armed になるので、先の点の中を通り抜けても順番は飛ばない。
        /// 入らなければその行は出ないまま残り、後から戻れば出る。
        ///
        /// **独白は無い。** 顔は見せないので、誰が喋っているかは声の向きと
        /// 一行に含まれた名前でしか伝わらない。
        ///
        /// <c>Dive.unity</c> に SceneFlow は無いので、<see cref="HudView"/> を直に触る
        /// </summary>
        void Talk()
        {
            if (hud == null) return;
            var said = entry.said;
            if (clock >= held && DiveEntry.Due(said, spoken, Footing()) >= 0)
            {
                hud.SetSubtitle(said[spoken].line, SubtitleKind.Line);
                spoken++;
                held = clock + lineGap;
                silence = clock + talkSeconds;
                return;
            }
            if (silence <= 0f || clock < silence) return;
            silence = 0f;
            hud.SetSubtitle(null);
        }

        /// <summary>
        /// 主の足元を場所のローカルで。会話の点は場所のローカルで置いてあるので、
        /// 世界の座で測ると場所ごとの離し（<c>BuildDive.PlaceOrigin</c>）のぶんだけずれる。
        ///
        /// **高さも一緒に測る。** 団地は同じ場所に三つの階が重なっているので、
        /// 平らに潰すと、一階の点が三階に立っているだけで開いてしまう
        /// </summary>
        Vector3 Footing()
        {
            var at = player.transform.position;
            return place != null ? place.InverseTransformPoint(at) : at;
        }

        /// <summary>渡るたびに眩暈を一段濃くする。cutAfter 人で最大に達し、以後は最大のまま</summary>
        void Deepen()
        {
            if (daze == null) return;
            var step = Mathf.Clamp01((float)chain.Hops / Mathf.Max(1, cutAfter)) * dazeMax;
            daze.Hold(step, step);
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
        void Watch()
        {
            if (panel == null) return;
            if (shown != null && !OnScreen(shown)) Drop();

            var who = Nearest();
            // もっと中央に近い人が現れなければ、狙いは出ている人のまま
            if (who == null) who = shown;
            if (who != aimed) { aimed = who; dwell = 0f; }
            else dwell += Time.deltaTime;

            // 出すのも、別の人へ乗り換えるのも、同じだけ目を留めてから
            if (aimed != null && aimed != shown && dwell >= watchSeconds)
            {
                shown = aimed;
                lastStep = 0;
                panel.Show(aimed, entry.row, Row(Target(aimed)));
            }
            if (shown == null) return;
            panel.Grow(chain.CutSize);
            Guide();
        }

        /// <summary>
        /// 渡り歩いた分だけ目を疲れさせ、画面の角を白く曇らせる。
        ///
        /// **一人目は素のまま。** 借りた目の出来を最初から出すと、潜った瞬間に世界が溶けて
        /// 何を見ればよいのか分からないと差し戻された。`切断` が育つのと同じ歩みで上げ、
        /// 押せるようになったところで出きる。
        ///
        /// 角の白い膜だけは最初から出しておく。あれは目の出来ではなく、
        /// 他人の記憶の中にいること自体の見え方だから
        /// </summary>
        void Wear()
        {
            if (body != null) body.Strain = HostBody.StrainOf(chain.CutSize);
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
        /// 首を少し振るたびに板が行き来すると、どちらの脇に出ているのか読めなくなる
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
                var toward = Head(who) - eye.position;
                if (toward.sqrMagnitude < 1e-4f) continue;
                var apart = Vector3.Angle(eye.forward, toward);
                if (apart >= closest) continue;
                closest = apart;
                best = who;
            }
            return best;
        }

        // ---- 画面の下の案内 --------------------------------------------------

        /// <summary>鍵の案内の頭。場面 1・2・3・8 の `E ○○` と同じ書式</summary>
        const string Key = "E  ";

        /// <summary>`E` の後ろに続く、潜る先の言い方</summary>
        const string DiveLabel = "この人の記憶へ潜る";

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
        /// **ここは `潜る` 専用にする。** 会話は歩いて点へ入れば出るもので、鍵を待たせない。
        /// 案内を出さないことが、そのまま「この行に押す鍵は無い」という合図になる
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
        /// 板が出ていなければ E は何もしない
        /// </summary>
        void Choose()
        {
            if (panel == null || shown == null) return;
            // 車輪を手前へ回すと 1。上が `潜る`、下が `切断` なので向きを裏返す
            var step = -player.LogStep;
            if (step != 0 && step != lastStep)
            {
                panel.Select(step > 0 ? 1 : 0);
                // 案内は Watch が毎フレーム出しているが、選んだ手応えを一フレーム遅らせない
                Guide();
            }
            lastStep = step;
            if (!player.InteractPressed) return;
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
