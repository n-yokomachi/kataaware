using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>
    /// 場面 6（庭の記憶）の段の進行（シナリオ設計書 10 節）。村（<c>Village.unity</c>）の夕方で、
    /// 片割れの目に入って 1 分ほどを受け身で過ごす。
    ///
    /// 1. **名を呼ばれる**（頭）。芝の西の縁で花に水を撒いていた女性（過去の主人公）が振り返り、主（片割れ）の名を呼ぶ。
    ///    ほかの記憶の頭と同じく（<see cref="DiveDirector"/> の Lead）、黒から明けてから目を女性の顔へ 1 秒で回して追い、見回しと歩きを封じる。
    ///    字幕は場面 4 の一行目と同じ帯で、名前の行を出さず、崩して読めない形（<see cref="GardenMemory.Call"/>）で出す。E で送る
    /// 2. **身を乗り出す**。送ると、聞き返すように目が少し前へ出て戻る（自動）
    /// 3. **庭を見る**。女性は向き直って水を撒き、しばらくしてホースを止める。座ったまま首の届く範囲で見回せる。歩けない
    /// 4. **女性の板**。女性に目を留めると板が出る。名前は文字化けで、「潜る」を押しても渡らない
    /// 5. **近づく**。女性がこちらを向き、ホースを置いて、芝を横切って卓の方へ歩いてくる。近づくほど逆光の影が薄くなる
    /// 6. **途切れる**。顔が見分けられるかという所（目から <see cref="cutDistance"/>）で、直に切れる。
    ///    行き先（<see cref="nextScene"/>）が空なら、黒と「（仮）続く」で止める
    ///
    /// **村は場面 9 と同じシーン。** どちらで入ったかは <see cref="GardenHandoff"/> で受け取る。立っていなければ何もせず、
    /// 場面 6 の物（女性・主の体・ホース、<see cref="memory"/> の下）は伏せたまま、場面 9 のまま始まる。
    /// 立っていれば、時刻を夕方にし、場面 6 の物を起こし、場面 9 の物（格子戸、<see cref="morningOnly"/>）を切る。
    /// 時刻は Awake で替える。セーブの流れ（<see cref="SaveFlow"/>）が sceneLoaded で村の時刻から場面の番号を決めるため
    ///
    /// **記憶するは場面の頭を残す**（<see cref="ISceneMemory"/>）。1 分の受け身の場面なので、途中の形は残さない。
    /// 流している間は <see cref="Settled"/> が false で、記憶するは場面の頭（夕方の村＝場面 6）を書く
    ///
    /// **女性の時計は二つに分ける。** 名を呼ぶ頭のあいだは記憶の時計（<see cref="Clock"/>）で振り返りと口を動かし、
    /// E で送ってからは庭の時計（<see cref="Garden"/>）で向き直り・水・止める・歩く を進める。
    /// 送るのを待つあいだに女性が勝手に水を撒き始めないように
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GardenMemoryDirector : MonoBehaviour, ISceneMemory
    {
        /// <summary>段</summary>
        public enum Beat
        {
            /// <summary>場面 9。何もしない</summary>
            Off,
            /// <summary>頭。名を呼ぶ一行を E で送るのを待つ</summary>
            Calling,
            /// <summary>送った後。目が少し前へ出て戻る</summary>
            Leaning,
            /// <summary>庭を見る。女性が水を撒き、止め、歩いてくる</summary>
            Garden,
            /// <summary>途切れた</summary>
            Cut,
        }

        /// <summary>板を出した相手へ潜るときの案内。<see cref="DiveDirector"/> と同じ言い方</summary>
        public const string DiveLabel = "この人の記憶へ潜る";

        [Header("繋ぎ")]
        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("右上の見出し")]
        [SerializeField] TMP_Text caption;
        [SerializeField] HoloPanel panel;
        [SerializeField] VillageHour hour;
        [Tooltip("場面 6 だけで起こす物の入れ物（女性・主の体・ホース）。シーンには伏せて置く")]
        [SerializeField] GameObject memory;
        [Tooltip("場面 9 の物。場面 6 の間は切る（格子戸）")]
        [SerializeField] Behaviour[] morningOnly = new Behaviour[0];

        [Header("主の体")]
        [Tooltip("主の足元と体の向き。目がここから前へ eyeLead、上へ seatEyeHeight に来るよう、組み立てが座った体の目から決める")]
        [SerializeField] Transform seat;
        [SerializeField] float seatEyeHeight = 1.1f;

        [Header("女性")]
        [Tooltip("女性の根。歩く線を持つ")]
        [SerializeField] Mover woman;
        [SerializeField] PersonMotion womanMotion;
        [Tooltip("口を動かす骨（Bip01 MJaw）")]
        [SerializeField] Transform jaw;
        [Tooltip("水を撒くときに向く向き。度（+z が 0 で東回り）。花の縁の方")]
        [SerializeField] float flowerYaw = 270f;
        [SerializeField] GardenHose hose;
        [Tooltip("水の弧の粒。筒の子")]
        [SerializeField] ParticleSystem spray;

        [Header("音（素材が無ければ鳴らさない）")]
        [Tooltip("手元の水の音。筒に付けた 3D の輪")]
        [SerializeField] AudioSource water;
        [Tooltip("ホースを止める音。筒に付けた単発")]
        [SerializeField] AudioSource stop;
        [Tooltip("途切れと雑音を鳴らす耳の音源（2D）")]
        [SerializeField] AudioSource noise;
        [Tooltip("名を呼ぶ口元が動く間の、途切れと雑音")]
        [SerializeField] AudioClip glitch;
        [Tooltip("終わりに途切れる瞬間の音")]
        [SerializeField] AudioClip cut;
        [Tooltip("水の音の大きさ")]
        [SerializeField, Range(0f, 1f)] float waterVolume = 0.7f;
        [Tooltip("水の音を立ち上げ・落とす秒")]
        [SerializeField] float waterFade = 0.25f;

        [Header("頭（記憶の時計、秒）")]
        [Tooltip("黒から明ける秒。ほかの記憶の頭（DiveDirector.openSeconds）と同じ")]
        [SerializeField] float openSeconds = 0.6f;
        [Tooltip("振り返る秒。それまでは花の方を向いて水を撒いている")]
        [SerializeField] float turnAround = 0.15f;
        [Tooltip("口が動き出す秒（途切れの音も鳴らす）")]
        [SerializeField] float voiceAt = 0.4f;
        [Tooltip("口が動いている秒")]
        [SerializeField] float mouthSeconds = 1.8f;
        [Tooltip("口の開き。度")]
        [SerializeField] float mouthOpen = 9f;

        [Header("身を乗り出す（送ってからの秒）")]
        [SerializeField] float leanOut = 0.7f;
        [SerializeField] float leanHold = 0.5f;
        [SerializeField] float leanBack = 0.8f;
        [Tooltip("目が前へ出る長さ。m")]
        [SerializeField] float leanReach = 0.10f;
        [Tooltip("前へ出るときに下がる長さ。m")]
        [SerializeField] float leanDrop = 0.02f;

        [Header("庭（送ってからの秒）")]
        [Tooltip("女性が花の方へ向き直る")]
        [SerializeField] float turnBack = 1.2f;
        [Tooltip("水を撒き始める")]
        [SerializeField] float sprayAt = 1.8f;
        [Tooltip("ホースを止める")]
        [SerializeField] float stopAt = 27f;
        [Tooltip("こちらを向く")]
        [SerializeField] float lookAt = 30f;
        // これより先は女性の歩きの線（Mover）の秒。ホースを置いて歩き出すのは線の頭（Mover.At）
        [Tooltip("歩き出してから、女性の顔と主の目の隔たりがこれを切ったら途切れる。m")]
        [SerializeField] float cutDistance = 2.6f;
        [Tooltip("顔までの隔たりが縮まらなくても、この秒で切る")]
        [SerializeField] float cutLatest = 60f;

        [Header("逆光の影")]
        [Tooltip("遠いときの暗さ（色に掛ける）")]
        [SerializeField, Range(0f, 1f)] float shadeDark = 0.6f;
        [Tooltip("この隔たりより遠ければ shadeDark のまま。m")]
        [SerializeField] float shadeFar = 5.5f;
        [Tooltip("この隔たりまで来れば素の色。m")]
        [SerializeField] float shadeNear = 2.2f;

        [Header("板")]
        [Tooltip("目の中央からこの角度の内側にいれば留めたことにする。度")]
        [SerializeField] float watchAngle = 12f;
        [Tooltip("目を留めてから板が出るまで。秒")]
        [SerializeField] float watchSeconds = 0.5f;
        [Tooltip("画面の端からこれだけ外へ出るまでは板を消さない。画面の幅・高さに対する割合")]
        [SerializeField] float edgeMargin = 0.08f;

        [Header("途切れた後")]
        [Tooltip("途切れた後に読むシーン。空なら黒と「（仮）続く」で止める")]
        [SerializeField] string nextScene = "";
        [Tooltip("黒のまま「（仮）続く」を出すまでの秒")]
        [SerializeField] float blackHold = 1.2f;

        Beat beat = Beat.Off;
        float clock;
        float garden;
        bool voiced;
        bool stopped;
        bool pending;
        bool attending;
        bool showing;
        float dwell;
        Transform head;
        Quaternion jawRest;
        bool jawKept;
        Renderer[] skins = new Renderer[0];
        MaterialPropertyBlock block;
        Color[][] baseColors = new Color[0][];
        CharacterController hull;
        Camera lens;
        readonly RaycastHit[] hits = new RaycastHit[16];
        float shade = -1f;
        int baseColorId;

        /// <summary>いまの段。動作確認から読む</summary>
        public Beat Current { get { return beat; } }

        /// <summary>場面 6 を流しているか</summary>
        public bool Running { get { return beat != Beat.Off; } }

        /// <summary>記憶の頭からの秒。動作確認から読む</summary>
        public float Clock { get { return clock; } }

        /// <summary>名を呼ぶ一行を送ってからの秒。送る前は 0</summary>
        public float Garden { get { return garden; } }

        /// <summary>板を出しているか。動作確認から読む</summary>
        public bool Showing { get { return showing; } }

        /// <summary>女性の顔と主の目のあいだ。m</summary>
        public float Apart
        {
            get
            {
                if (head == null || player == null || player.Eye == null) return float.PositiveInfinity;
                return Vector3.Distance(Face(), player.Eye.position);
            }
        }

        /// <summary>いまの逆光の影の暗さ（色に掛けている値）。動作確認から読む</summary>
        public float Shade { get { return shade; } }

        /// <summary>次の一歩で E を一度押したことにする。動作確認から呼ぶ</summary>
        public void PressInteract() { pending = true; }

        void Awake()
        {
            if (!GardenHandoff.Take())
            {
                // 場面 9。場面 6 の物は伏せたまま（組み立てが伏せて保存している）
                if (memory != null && memory.activeSelf) memory.SetActive(false);
                enabled = false;
                return;
            }
            Begin();
        }

        /// <summary>
        /// 場面 6 として起こす。時刻を夕方にし、場面 6 の物を起こし、場面 9 の物を切る。
        /// Awake から呼ぶ（エディタで撮るときは直に呼ぶ）
        /// </summary>
        public void Begin()
        {
            beat = Beat.Calling;
            GardenHandoff.Active = true;
            if (hour != null) hour.Set(VillageHour.Hour.Evening);
            if (memory != null) memory.SetActive(true);
            for (var i = 0; i < morningOnly.Length; i++)
                if (morningOnly[i] != null) morningOnly[i].enabled = false;
            if (player != null)
            {
                hull = player.GetComponent<CharacterController>();
                lens = player.Eye != null ? player.Eye.GetComponent<Camera>() : null;
            }
            if (lens == null) lens = Camera.main;
            var an = woman != null ? woman.GetComponentInChildren<Animator>() : null;
            head = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            if (jaw != null) { jawRest = jaw.localRotation; jawKept = true; }
            skins = woman != null ? woman.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            baseColorId = Shader.PropertyToID("_BaseColor");
            block = new MaterialPropertyBlock();
            baseColors = new Color[skins.Length][];
            for (var i = 0; i < skins.Length; i++)
            {
                var mats = skins[i].sharedMaterials;
                baseColors[i] = new Color[mats.Length];
                for (var m = 0; m < mats.Length; m++)
                    baseColors[i][m] = mats[m] != null && mats[m].HasProperty(baseColorId) ? mats[m].GetColor(baseColorId) : Color.white;
            }
        }

        IEnumerator Start()
        {
            if (beat == Beat.Off) yield break;
            // コンソールの頭の行は「潜行中」と、この記憶の日時と場所。sceneLoaded で戻されるので Start で渡す
            ImplantConsole.SetPlace(ConsolePlace.Garden);
            Open();
            if (hud == null) yield break;
            // 場面 5 のモニターから潜る。ほかの記憶の頭と同じく、黒から明ける
            hud.SetFade(1f);
            yield return hud.FadeTo(0f, openSeconds);
        }

        /// <summary>
        /// 場面の頭の形。主を椅子に据え、見出しと角の白い膜を出し、女性を花の方へ向けてホースを持たせ、
        /// 名を呼ぶ一行を出す。エディタで撮るときにも呼ぶ
        /// </summary>
        public void Open()
        {
            clock = 0f;
            garden = 0f;
            voiced = false;
            stopped = false;
            showing = false;
            dwell = 0f;
            Seat();
            if (caption != null) caption.text = GardenMemory.Row;
            if (hud != null) { hud.SetHaze(1f); hud.SetPrompt(null); }
            if (panel != null) panel.Hide();
            if (woman != null)
            {
                woman.Play(0f);
                woman.transform.rotation = Quaternion.Euler(0f, flowerYaw, 0f);
            }
            if (womanMotion != null && player != null) womanMotion.Watch(player.Eye);
            if (hose != null) hose.Hold();
            Spray(true);
            Water(true, true);
            // 名を呼ぶ一行。ほかの記憶の一行目と同じ帯で、E で送る。名前の行は出さない
            var line = GardenMemory.Call;
            ConsoleLog.Said(line);
            if (hud != null) hud.SetSubtitle(line, SubtitleKind.Line, true);
            if (player != null)
            {
                player.HoldLook(this);
                player.HoldMove(this);
            }
            Shadow();
        }

        /// <summary>
        /// 椅子に据える。歩かず、首だけ振る。どれも直列化されない性質なので、場面の頭で掛ける（<see cref="RestDirector"/> と同じ）
        /// </summary>
        void Seat()
        {
            if (player == null) return;
            if (seat != null)
                player.PlaceAt(seat.position, seat.eulerAngles.y, HeadTurn.DefaultLimit, 0f, 0f, seatEyeHeight);
            player.CanMove = false;
            player.CanLook = true;
            player.EyeHeight = seatEyeHeight;
            player.HeadYawLimit = HeadTurn.DefaultLimit;
            player.SpeedScale = 1f;
            player.EyeOffset = Vector3.zero;
        }

        void OnDisable()
        {
            if (beat == Beat.Off) return;
            Unattend();
            if (player != null)
            {
                player.EyeOffset = Vector3.zero;
                player.CanLook = true;
            }
        }

        void OnDestroy()
        {
            if (beat != Beat.Off) GardenHandoff.Active = false;
        }

        void Update()
        {
            if (beat == Beat.Off || beat == Beat.Cut) return;
            // TAB のコンソールを開いている間は、記憶の時計も止める
            if (ImplantConsole.IsOpen) return;
            var press = (player != null && player.InteractPressed) || pending;
            pending = false;
            Step(Time.deltaTime, press);
        }

        /// <summary>口は人の動きが骨を置いた後に開く</summary>
        void LateUpdate()
        {
            if (beat == Beat.Off) return;
            Mouth();
        }

        /// <summary>
        /// 一フレーム分。press はこのフレームで E が押されたか。エディタで撮るときは直に呼ぶ
        /// （そのときは人の動きと筒と口は呼び手が回す）
        /// </summary>
        public void Step(float dt, bool press)
        {
            if (beat == Beat.Off || beat == Beat.Cut) return;
            clock += dt;
            switch (beat)
            {
                case Beat.Calling:
                    Calling(press);
                    break;
                case Beat.Leaning:
                    garden += dt;
                    Lean();
                    Woman();
                    if (garden >= leanOut + leanHold + leanBack) Settle();
                    break;
                case Beat.Garden:
                    garden += dt;
                    Woman();
                    Watch(dt);
                    if (Ending()) Cut();
                    break;
            }
            Shadow();
        }

        // ---- 1. 名を呼ばれる ------------------------------------------------------

        void Calling(bool press)
        {
            // 暗転から明けきってから目を回す。黒いうちに回し終えると、行き先を示す動きが見えない
            if (!attending && clock >= openSeconds) Attend();
            if (woman != null)
            {
                var yaw = clock >= turnAround ? TowardHost() : flowerYaw;
                woman.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                woman.Play(0f);
            }
            // 振り返るときに引き金を離す
            if (clock >= turnAround) { Spray(false); Water(false, false); }
            if (!voiced && clock >= voiceAt)
            {
                voiced = true;
                // 声は聞き取れない。口元が動く間、途切れと雑音だけが鳴る（素材が無いあいだは何も鳴らない）
                if (noise != null && glitch != null) noise.PlayOneShot(glitch);
            }
            if (!press) return;
            // 送った。帯を閉じ、聞き返すように身を乗り出す（見回しと歩きは封じたまま、女性を追い続ける）
            if (hud != null) hud.SetSubtitle(null);
            beat = Beat.Leaning;
            garden = 0f;
        }

        /// <summary>口の骨を開け閉めする。人の動きと同じ 12 こまで刻む</summary>
        public void Mouth()
        {
            if (jaw == null || !jawKept) return;
            var open = 0f;
            var t = clock - voiceAt;
            if (beat == Beat.Calling && t >= 0f && t < mouthSeconds)
            {
                var tick = Mathf.Floor(t * PersonMotion.Fps);
                // こまごとに開きを変える。一定の間で開け閉めすると機械じみる
                var wave = 0.5f + 0.5f * Mathf.Sin(tick * 1.9f) * Mathf.Cos(tick * 0.7f);
                open = mouthOpen * Mathf.Clamp01(wave);
            }
            jaw.localRotation = jawRest * Quaternion.Euler(0f, 0f, open);
        }

        // ---- 2. 身を乗り出す ------------------------------------------------------

        /// <summary>目が少し前へ出て、戻る。前は今の目の向き（首の向き）</summary>
        void Lean()
        {
            if (player == null) return;
            float k;
            if (garden < leanOut) k = Gaze.Ease(garden / Mathf.Max(1e-3f, leanOut));
            else if (garden < leanOut + leanHold) k = 1f;
            else k = 1f - Gaze.Ease(Mathf.Clamp01((garden - leanOut - leanHold) / Mathf.Max(1e-3f, leanBack)));
            var ahead = Quaternion.Euler(0f, player.HeadYaw, 0f) * Vector3.forward;
            player.EyeOffset = ahead * (leanReach * k) + Vector3.down * (leanDrop * k);
        }

        /// <summary>乗り出し終えた。目を離して、首の届く範囲で見回せるようにする</summary>
        void Settle()
        {
            if (player != null) player.EyeOffset = Vector3.zero;
            Unattend();
            beat = Beat.Garden;
        }

        // ---- 3〜5. 庭の女性 --------------------------------------------------------

        /// <summary>庭の時計で女性を動かす。向き・水・止める・歩く</summary>
        void Woman()
        {
            if (woman == null) return;
            var walking = garden >= woman.At;
            if (!walking)
            {
                float yaw;
                if (garden < turnBack) yaw = TowardHost();
                else if (garden < lookAt) yaw = flowerYaw;
                else yaw = TowardHost();
                woman.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            woman.Play(garden);
            var spraying = garden >= sprayAt && garden < stopAt;
            Spray(spraying);
            Water(spraying, false);
            if (!stopped && garden >= stopAt)
            {
                stopped = true;
                if (stop != null && stop.clip != null) stop.Play();
            }
            // 歩き出したらホースを置く。手の据えた形は人の動きが解く（letsGo）
            if (walking && hose != null && !hose.Dropped) hose.Drop();
        }

        /// <summary>女性の足元から主の目の方の向き。度</summary>
        float TowardHost()
        {
            if (woman == null || player == null || player.Eye == null) return flowerYaw;
            var d = player.Eye.position - woman.transform.position;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>いま水を撒いているか。動作確認から読む（エディタでは粒は再生されないので、撮る側がこれを見て流す）</summary>
        public bool Spraying { get; private set; }

        void Spray(bool on)
        {
            Spraying = on;
            if (spray == null) return;
            if (on && !spray.isEmitting) spray.Play(true);
            else if (!on && spray.isEmitting) spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>水の音。now なら寄せずにその場で</summary>
        void Water(bool on, bool now)
        {
            if (water == null || water.clip == null) return;
            var target = on ? waterVolume : 0f;
            water.volume = now || waterFade <= 0f ? target : Mathf.MoveTowards(water.volume, target, Time.deltaTime * waterVolume / waterFade);
            if (water.volume > 0f && !water.isPlaying) water.Play();
            else if (water.volume <= 0f && water.isPlaying) water.Stop();
        }

        /// <summary>
        /// 逆光の影。遠いうちは女性の色を暗く沈め、近づくほど素の色へ戻す。
        /// 夕日は女性の背から当たっていて、顔の側は影になる。寄ってくるほど、顔の向きが日の側へ回り、目も慣れる
        /// </summary>
        void Shadow()
        {
            if (skins.Length == 0 || block == null) return;
            var d = Apart;
            var k = shadeFar <= shadeNear ? 1f : Mathf.Clamp01((shadeFar - d) / (shadeFar - shadeNear));
            var s = Mathf.Lerp(shadeDark, 1f, Gaze.Ease(k));
            if (Mathf.Abs(s - shade) < 1e-3f) return;
            shade = s;
            for (var i = 0; i < skins.Length; i++)
            {
                var r = skins[i];
                if (r == null) continue;
                var cols = baseColors[i];
                for (var m = 0; m < cols.Length; m++)
                {
                    r.GetPropertyBlock(block, m);
                    var c = cols[m];
                    block.SetColor(baseColorId, new Color(c.r * s, c.g * s, c.b * s, c.a));
                    r.SetPropertyBlock(block, m);
                }
            }
        }

        // ---- 目を向ける -----------------------------------------------------------

        Vector3 Face()
        {
            return head != null ? head.position + Vector3.up * 0.07f : (woman != null ? woman.transform.position + Vector3.up * 1.55f : Vector3.zero);
        }

        void Attend()
        {
            if (player == null) return;
            attending = true;
            player.HoldLook(this);
            player.HoldMove(this);
            if (head != null) player.Follow(head, Vector3.up * 0.07f, PlayerController.FaceSeconds);
            else if (woman != null) player.Follow(woman.transform, Vector3.up * 1.55f, PlayerController.FaceSeconds);
        }

        void Unattend()
        {
            if (player == null) return;
            if (attending) player.StopFacing();
            attending = false;
            player.FreeLook(this);
            player.FreeMove(this);
        }

        // ---- 4. 女性の板 ----------------------------------------------------------

        /// <summary>
        /// 女性に目を留めたら板を出す。名前は文字化けで、潜れない（E を押しても何もしない）。
        /// 消えるのは、女性が画面から外れたときと、物の陰に隠れたときだけ（<see cref="DiveDirector"/> と同じ）
        /// </summary>
        void Watch(float dt)
        {
            if (panel == null || woman == null || player == null || player.Eye == null) return;
            var who = woman.transform;
            if (showing && (!OnScreen(who) || !Visible(who))) Drop();
            if (!showing)
            {
                var looking = AngleTo(who) < watchAngle && Visible(who);
                dwell = looking ? dwell + dt : 0f;
                if (dwell >= watchSeconds)
                {
                    showing = true;
                    panel.Show(who, GardenMemory.Row, GardenMemory.WomanRow);
                    panel.Grow(DiveChain.CutStart);
                }
            }
            if (showing && hud != null) hud.SetPrompt(HudView.Prompt(DiveLabel));
        }

        void Drop()
        {
            showing = false;
            dwell = 0f;
            if (panel != null) panel.Hide();
            if (hud != null) hud.SetPrompt(null);
        }

        float AngleTo(Transform who)
        {
            Vector3 face, chest, hips;
            DiveDirector.Aims(who, out face, out chest, out hips);
            var eye = player.Eye;
            var least = 180f;
            foreach (var p in new[] { face, chest, hips })
            {
                var d = p - eye.position;
                if (d.sqrMagnitude < 1e-4f) continue;
                least = Mathf.Min(least, Vector3.Angle(eye.forward, d));
            }
            return least;
        }

        bool OnScreen(Transform who)
        {
            if (lens == null) return true;
            Vector3 face, chest, hips;
            DiveDirector.Aims(who, out face, out chest, out hips);
            return InView(face) || InView(chest) || InView(hips);
        }

        bool InView(Vector3 point)
        {
            var at = lens.WorldToViewportPoint(point);
            if (at.z <= 0f) return false;
            return at.x >= -edgeMargin && at.x <= 1f + edgeMargin && at.y >= -edgeMargin && at.y <= 1f + edgeMargin;
        }

        /// <summary>目から顔か胸へ、遮る物無しに届くか。絵を持たない当たり（花の縁の見えない仕切り）は目を遮らない</summary>
        bool Visible(Transform who)
        {
            Vector3 face, chest, hips;
            DiveDirector.Aims(who, out face, out chest, out hips);
            var eye = player.Eye.position;
            return Clear(eye, face, who) || Clear(eye, chest, who);
        }

        bool Clear(Vector3 from, Vector3 to, Transform who)
        {
            return Blocking(from, to, who) == null && Blocking(to, from, who) == null;
        }

        Collider Blocking(Vector3 from, Vector3 to, Transform who)
        {
            var toward = to - from;
            var far = toward.magnitude;
            if (far < 1e-3f) return null;
            var n = Physics.RaycastNonAlloc(from, toward / far, hits, far, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger || c == hull) continue;
                if (who != null && c.transform.IsChildOf(who)) continue;
                if (c.GetComponent<Renderer>() == null) continue;
                return c;
            }
            return null;
        }

        // ---- 6. 途切れる ----------------------------------------------------------

        /// <summary>歩き出していて、顔が見分けられるかという所まで来たか。来なくても cutLatest で切る</summary>
        bool Ending()
        {
            if (woman == null) return garden >= cutLatest;
            if (garden >= cutLatest) return true;
            return garden >= woman.At && Apart <= cutDistance;
        }

        /// <summary>
        /// 途切れる。ほかの記憶が尽きる時と同じ、直の切れ方。終わりの合図は入れない。
        /// 行き先があれば直に読み、無ければ黒のまま「（仮）続く」で止める
        /// </summary>
        public void Cut()
        {
            if (beat == Beat.Cut) return;
            beat = Beat.Cut;
            Drop();
            Unattend();
            Spray(false);
            if (spray != null) spray.Clear(true);
            if (water != null) water.Stop();
            if (noise != null && cut != null) noise.PlayOneShot(cut);
            if (player != null)
            {
                player.CanLook = false;
                player.EyeOffset = Vector3.zero;
            }
            if (hud != null) hud.SetSubtitle(null);
            if (SceneExit.Continues(nextScene))
            {
                SceneManager.LoadScene(SceneExit.Target(nextScene));
                return;
            }
            if (hud != null)
            {
                hud.SetFade(1f);
                hud.SetHaze(0f);
            }
            if (caption != null) caption.text = string.Empty;
            if (Application.isPlaying) StartCoroutine(Hold());
        }

        IEnumerator Hold()
        {
            if (blackHold > 0f) yield return new WaitForSeconds(blackHold);
            if (hud != null) hud.SetCenter(SceneFlow.ToBeContinued);
        }

        // ---- 記憶する・思い出す -----------------------------------------------------

        public string MemoryKey { get { return "garden.memory"; } }

        /// <summary>流している間は残さない。記憶するは場面の頭（夕方の村＝場面 6）を書く。場面 9 では邪魔しない</summary>
        public bool Settled { get { return beat == Beat.Off; } }

        public string Capture() { return null; }

        /// <summary>場面 6 は場面の頭からしか始まらないので、当てる物は無い</summary>
        public void Restore(string data) { }
    }
}
