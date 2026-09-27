using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 場面 9 の終わりと場面 10（対面）の段の進行（シナリオ設計書 12 節）。村（<c>Village.unity</c>）の朝で、読み込みを挟まずに続ける。
    ///
    /// **場面 9 の終わり**
    /// - 卓のまわりの区画（<see cref="table"/> から <see cref="zoneRadius"/>）に入ると、歩きを封じて独白を一行出す。E で送ると場面 10 に替わる
    ///   （<see cref="SaveFlow.EnterStage"/> で 10。ここが場面 10 の頭）
    ///
    /// **場面 10**
    /// 1. 裏口が開く。戸の音、戸の板が内へ回る。目が片割れの顔へゆっくり向き（<see cref="PlayerController.Follow"/>）、片割れが戸口の暗がりから出てくる
    /// 2. 片割れが手で口元を覆い（<see cref="PersonMotion.Pose"/> の <see cref="MouthPose"/>）、一歩踏み出す。このあいだ見回しと歩きは封じる
    /// 3. 封じを解く。プレイヤーが歩み寄る。片割れもゆっくり寄る（テラスの上だけ、<see cref="twinReach"/> まで）
    /// 4. 目と顔が <see cref="meetDistance"/> まで近づくと、歩きと見回しを封じ、片割れがもう半歩寄って、手を頬へ伸ばす（<see cref="CheekPose"/>）。手は画面の縁から入る
    /// 5. モンタージュ。暗転を挟まない直の切り替えで、(a) 場面 1 の端末の黒い画面に映った自分、(b) 場面 6 の続きで振り返った女性の顔、(c) 目の前の片割れの顔（その場の絵）
    /// 6. 庭で聞き取れなかった言葉が、崩れずに字幕に出る（<see cref="Reunion.Voice"/>）
    /// 7. 片割れが言う（<see cref="Reunion.TwinSays"/>）
    /// 8. 最後の独白（<see cref="Reunion.LastLine"/>）→ 暗転（環境音も絞る）
    /// 9. クリアの印を付け（<see cref="SaveStore.MarkCleared"/>）、エンディング（<see cref="nextScene"/>、片割れを乗せたドライブとクレジット。
    ///    シナリオ設計書 13 節、別の場面）を読む。エンディングがまだ組み立ての一覧に無い間は、タイトルの画面へ戻る
    ///    （タイトルのカード・終わりのクレジット・エンディングの曲は、エンディングの場面が受け持つ）
    ///
    /// **村は場面 6・9・10 が同じシーン。** 場面 6（<see cref="GardenHandoff"/>）で入った時は何もしない。
    /// 場面 10 の頭から入る時（場面 10 のセーブを思い出した時と、デバッグの一覧の「対面」）は <see cref="ReunionHandoff"/> で受け取り、
    /// 卓の前（<see cref="headSpot"/>）に立たせて黒から明ける。
    ///
    /// **記憶するは場面 10 の頭を残す**（<see cref="ISceneMemory"/>）。場面 10 を流している間は <see cref="Settled"/> が false で、
    /// 記憶するは場面の頭（場面 10、卓の前）を書く。場面 9 の間は邪魔しない（独白の間も、区画の中の立ち位置のまま残る。思い出すと独白からやり直す）。
    ///
    /// エディタで撮るときは、<see cref="Begin"/>・<see cref="Step"/> を直に呼び、人の動き（<see cref="PersonMotion.Step"/>・<see cref="PersonMotion.Late"/>）は呼び手が回す
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class ReunionDirector : MonoBehaviour, ISceneMemory
    {
        /// <summary>段</summary>
        public enum Beat
        {
            /// <summary>場面 6（庭の記憶）。何もしない</summary>
            Off,
            /// <summary>場面 9。卓のまわりの区画に入るのを待つ</summary>
            Waiting,
            /// <summary>場面 9 の終わり。卓の前の独白を E で送るのを待つ</summary>
            Musing,
            /// <summary>場面 10 の頭から、裏口が開いて片割れが出てきて、口元を覆い、一歩踏み出すまで。見回しと歩きは封じる</summary>
            Door,
            /// <summary>歩み寄る</summary>
            Approach,
            /// <summary>頬に触れる</summary>
            Touch,
            /// <summary>モンタージュ</summary>
            Montage,
            /// <summary>庭で聞き取れなかった言葉</summary>
            Voice,
            /// <summary>片割れの言葉</summary>
            Words,
            /// <summary>最後の独白</summary>
            Last,
            /// <summary>暗転</summary>
            Closing,
            /// <summary>クリアの印を付けて、エンディングを読んだ</summary>
            Done,
        }

        /// <summary>片割れの形の番号（<see cref="PersonMotion.Pose"/>）。口元を覆う手</summary>
        public const int MouthPose = 0;
        /// <summary>頬へ伸ばす手</summary>
        public const int CheekPose = 1;

        [Header("繋ぎ")]
        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("場面 10 だけで起こす物の入れ物（片割れ）。シーンには伏せて置く")]
        [SerializeField] GameObject reunion;
        [Tooltip("片割れの根。人の動きを持つ")]
        [SerializeField] PersonMotion twin;
        [Tooltip("裏口の戸の板（丁番を原点にした子）")]
        [SerializeField] Transform door;
        [Tooltip("場面 10 の頭から入った時に開いた形にする格子戸")]
        [SerializeField] SwingGate[] gates = new SwingGate[0];
        [Tooltip("村の環境音。暗転で絞る")]
        [SerializeField] VillageAmbience ambience;
        [Tooltip("場面 10 の間だけ点ける、片割れの顔を起こす灯り。無ければ点けない")]
        [SerializeField] Light faceLight;

        [Header("卓")]
        [Tooltip("白いパラソルの卓の足元")]
        [SerializeField] Vector3 table;
        [Tooltip("卓のまわりの区画。卓の芯から、上から見てこの内に入ると場面 9 が終わる。m")]
        [SerializeField] float zoneRadius = 2.2f;
        [Tooltip("場面 10 の頭（卓の前）。セーブを思い出した時とデバッグの一覧から入った時に立たせる足元")]
        [SerializeField] Vector3 headSpot;
        [Tooltip("その時の体の向き。度（+z が 0 で東回り）")]
        [SerializeField] float headYaw = 95f;
        [Tooltip("場面 10 の頭から入った時に、黒から明ける秒")]
        [SerializeField] float openSeconds = 1.2f;

        [Header("裏口")]
        [Tooltip("戸の開き。度。閉じた向きからの y の回り（負で内へ）")]
        [SerializeField] float doorOpenYaw = -88f;
        [Tooltip("戸が開き切るまでの秒")]
        [SerializeField] float doorSeconds = 1.1f;
        [Tooltip("戸の音。裏口に置いた 3D の音源")]
        [SerializeField] AudioSource doorSound;
        [Tooltip("戸の音を鳴らす長さ。秒（開ける所だけ。0 なら終わりまで）")]
        [SerializeField] float doorSoundSeconds = 1.0f;

        [Header("片割れの道")]
        [Tooltip("戸が開く前に立っている所。戸口の奥の暗がり（戸の板の回る所の外）")]
        [SerializeField] Vector3 twinInside;
        [Tooltip("戸口を出た所。踏み石の先、テラスの上")]
        [SerializeField] Vector3 twinOut;
        [Tooltip("戸口を出てから、そのまま卓の方（プレイヤーの方）へ歩いて立ち止まるまで。m。戸口のすぐ外で立ち止まると、卓の前から 7 m 余りあって顔が見分けられない")]
        [SerializeField] float outWalk = 1.6f;
        [Tooltip("出てくる速さ。m/s（ならした速さ。歩き出しと立ち止まりはなめらかに）")]
        [SerializeField] float outSpeed = 0.6f;
        [Tooltip("一歩踏み出す長さ。m")]
        [SerializeField] float stepLength = 0.45f;
        [Tooltip("一歩の秒")]
        [SerializeField] float stepSeconds = 1.0f;
        [Tooltip("歩み寄る速さ。m/s。ゆっくり")]
        [SerializeField] float twinSpeed = 0.35f;
        [Tooltip("歩み寄るのはここまで（戸口の外から）。プレイヤーが寄らなければ待つ。m")]
        [SerializeField] float twinReach = 2.2f;
        [Tooltip("片割れが歩ける所（テラスの上）。x の小・大、z の小・大")]
        [SerializeField] Vector4 walkArea = new Vector4(-3.3f, 6.1f, 15.55f, 18.3f);
        [Tooltip("テラスの上面の高さ")]
        [SerializeField] float walkY = 0.1f;

        [Header("場面 10 の頭からの秒")]
        [Tooltip("戸が開く（戸の音）")]
        [SerializeField] float openAt = 1.4f;
        [Tooltip("目が片割れの顔へ向き始める")]
        [SerializeField] float lookAt = 1.7f;
        [Tooltip("片割れが出てき始める")]
        [SerializeField] float comeOutAt = 2.3f;
        [Tooltip("立ち止まってから、口元を覆い始めるまで")]
        [SerializeField] float gaspDelay = 0.4f;
        [Tooltip("手を口元へ上げる秒")]
        [SerializeField] float gaspSeconds = 0.6f;
        [Tooltip("口元を覆ったまま、一歩踏み出すまで")]
        [SerializeField] float gaspHold = 0.9f;
        [Tooltip("一歩踏み出してから、封じを解くまで")]
        [SerializeField] float releaseDelay = 0.5f;
        [Tooltip("口元の手を下ろす秒")]
        [SerializeField] float lowerSeconds = 0.9f;
        [Tooltip("封じを解いてから、片割れが歩み寄り始めるまで")]
        [SerializeField] float approachAfter = 0.8f;

        [Header("頬")]
        [Tooltip("目と片割れの顔が、上から見てこれより近づくと、歩きと見回しを封じる。m")]
        [SerializeField] float meetDistance = 1.0f;
        [Tooltip("頬に触れる時の、目から片割れの足元まで（上から見て）。m。手の形はこの隔たりで撮ってある")]
        [SerializeField] float touchApart = 0.42f;
        [Tooltip("もう半歩寄る秒")]
        [SerializeField] float stepInSeconds = 1.1f;
        [Tooltip("手を頬へ伸ばす秒")]
        [SerializeField] float reachSeconds = 1.1f;
        [Tooltip("頬に触れたまま、モンタージュまで")]
        [SerializeField] float holdSeconds = 0.8f;
        [Tooltip("目を顔へ向ける秒（頬の段）")]
        [SerializeField] float touchLookSeconds = 0.8f;

        [Header("モンタージュ")]
        [Tooltip("(a) 場面 1 で端末の黒い画面に映った自分。前もってゲームの見え方で撮った 320×180")]
        [SerializeField] Texture2D mirrorShot;
        [Tooltip("(b) 場面 6 の続き。振り返った女性の顔。前もって撮った 320×180")]
        [SerializeField] Texture2D gardenShot;
        [Tooltip("一枚の秒。(c) はその場の絵")]
        [SerializeField] float montageSeconds = 1.25f;

        [Header("終わり")]
        [Tooltip("最後の独白を送ってから黒へ落とす秒。環境音も同じ秒で絞る")]
        [SerializeField] float blackSeconds = 2.2f;
        [Tooltip("黒のまま置いてから、エンディングを読むまでの秒")]
        [SerializeField] float blackHold = 1.0f;
        [Tooltip("暗転の後に読むシーン（エンディング、シナリオ設計書 13 節）。組み立ての一覧に無ければタイトルの画面へ戻る")]
        [SerializeField] string nextScene = EndingScene;

        /// <summary>エンディングのシーンの名。片割れを乗せたドライブとクレジット（別の担当が作る）</summary>
        public const string EndingScene = "Ending";

        Beat beat = Beat.Off;
        float clock;
        float beatClock;
        bool pending;
        bool fromHead;
        bool attending;
        bool doorOpened;
        bool steppedOut;
        bool walkedOut;
        Vector3 stopAt;
        Quaternion doorClosed;
        bool doorKnown;
        Transform face;
        Vector3 stepFrom;
        Vector3 stepTo;
        Vector3 touchFrom;
        Vector3 touchTo;
        float walked;
        int frame = -1;
        GameObject montageRoot;
        RawImage picture;
        readonly List<AudioSource> quieted = new List<AudioSource>();
        readonly List<float> quietFrom = new List<float>();
        bool cleared;

        /// <summary>いまの段。動作確認から読む</summary>
        public Beat Current { get { return beat; } }

        /// <summary>場面 10 を流しているか</summary>
        public bool Running { get { return beat >= Beat.Door; } }

        /// <summary>場面 10 の頭からの秒</summary>
        public float Clock { get { return clock; } }

        /// <summary>いまの段に入ってからの秒</summary>
        public float BeatClock { get { return beatClock; } }

        /// <summary>モンタージュのどの一枚か（0 が (a)、1 が (b)、2 が (c)）。出していなければ -1</summary>
        public int MontageFrame { get { return beat == Beat.Montage ? frame : -1; } }

        /// <summary>クリアの印を付けたか</summary>
        public bool Cleared { get { return cleared; } }

        /// <summary>暗転の後に読むシーン。組み立ての一覧に無ければ null（タイトルの画面へ戻る）</summary>
        public string Next
        {
            get
            {
                var target = SceneExit.Target(nextScene);
                return target != null && Application.CanStreamedLevelBeLoaded(target) ? target : null;
            }
        }

        /// <summary>片割れの顔と目のあいだ（上から見て）。m</summary>
        public float Apart
        {
            get
            {
                if (player == null || player.Eye == null || twin == null) return float.PositiveInfinity;
                var f = Face();
                var e = player.Eye.position;
                return new Vector2(f.x - e.x, f.z - e.z).magnitude;
            }
        }

        /// <summary>片割れの顔の点（両目の真ん中。目の骨が無ければ頭の骨の少し上）</summary>
        public Vector3 Face()
        {
            if (face == null && twin != null)
            {
                var an = twin.GetComponentInChildren<Animator>(true);
                face = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                leftEye = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.LeftEye) : null;
                rightEye = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.RightEye) : null;
                if ((leftEye == null || rightEye == null) && face != null)
                    foreach (var t in face.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Bip01 LEye") leftEye = t;
                        else if (t.name == "Bip01 REye") rightEye = t;
                    }
            }
            if (leftEye != null && rightEye != null) return (leftEye.position + rightEye.position) * 0.5f;
            if (face != null) return face.position + Vector3.up * 0.07f;
            return twin != null ? twin.transform.position + Vector3.up * 1.52f : transform.position;
        }
        Transform leftEye;
        Transform rightEye;

        /// <summary>次の一歩で E を一度押したことにする。動作確認から呼ぶ</summary>
        public void PressInteract() { pending = true; }

        void Awake()
        {
            // 場面 6（庭の記憶）で入った。場面 10 の物は伏せたまま
            if (GardenHandoff.Pending || GardenHandoff.Active)
            {
                ReunionHandoff.Take();
                Hide();
                enabled = false;
                return;
            }
            Hide();
            beat = Beat.Waiting;
            if (!ReunionHandoff.Take()) return;
            // 場面 10 の頭から。セーブの流れ（sceneLoaded）が場面の番号を 10 にするよう、ここで立てる
            fromHead = true;
            ReunionHandoff.Active = true;
        }

        /// <summary>場面 10 の物を伏せる（組み立てが伏せて保存している）</summary>
        void Hide()
        {
            if (reunion != null && reunion.activeSelf) reunion.SetActive(false);
            if (faceLight != null) faceLight.enabled = false;
        }

        IEnumerator Start()
        {
            if (beat == Beat.Off) yield break;
            if (Application.isPlaying) Montage();
            if (!fromHead) yield break;
            // 卓の前に立たせて、黒から明ける
            OpenAtTable();
            if (hud == null) yield break;
            hud.SetFade(1f);
            yield return hud.FadeTo(0f, openSeconds);
        }

        /// <summary>
        /// 場面 10 の頭から入った形。卓の前に立たせ、格子戸は開いた形にして、場面 10 を始める。エディタで撮るときにも呼ぶ
        /// </summary>
        public void OpenAtTable()
        {
            if (player != null)
            {
                player.PlaceAt(headSpot, headYaw, 0f, 0f, 0f, PlayerController.StandingEyeHeight);
                player.CanMove = true;
                player.CanLook = true;
            }
            for (var i = 0; i < gates.Length; i++)
                if (gates[i] != null) gates[i].Set(true);
            Begin();
        }

        void OnDisable()
        {
            if (beat == Beat.Off) return;
            Release();
        }

        void OnDestroy()
        {
            if (beat >= Beat.Door) ReunionHandoff.Active = false;
            if (montageRoot != null) Destroy(montageRoot);
        }

        void Update()
        {
            if (beat == Beat.Off || beat == Beat.Done) return;
            // TAB のコンソールを開いている間は止める
            if (ImplantConsole.IsOpen) return;
            var press = (player != null && player.InteractPressed) || pending;
            pending = false;
            Step(Time.deltaTime, press);
        }

        /// <summary>一フレーム分。press はこのフレームで E が押されたか。エディタで撮るときは直に呼ぶ</summary>
        public void Step(float dt, bool press)
        {
            if (beat == Beat.Off || beat == Beat.Done) return;
            if (beat >= Beat.Door) clock += dt;
            beatClock += dt;
            switch (beat)
            {
                case Beat.Waiting:
                    if (InZone()) Muse();
                    break;
                case Beat.Musing:
                    if (!press) break;
                    if (hud != null) hud.SetSubtitle(null);
                    // ここが場面 10 の頭。自動のセーブを場面 10 で書き直す
                    SaveFlow.EnterStage(ReunionHandoff.Stage);
                    Begin();
                    break;
                case Beat.Door:
                    Door();
                    break;
                case Beat.Approach:
                    Approach(dt);
                    break;
                case Beat.Touch:
                    Touch();
                    break;
                case Beat.Montage:
                    Flash();
                    break;
                case Beat.Voice:
                    if (press) Say(Beat.Words, Reunion.TwinSays);
                    break;
                case Beat.Words:
                    if (press) Say(Beat.Last, Reunion.LastLine);
                    break;
                case Beat.Last:
                    if (press) Close();
                    break;
                case Beat.Closing:
                    Closing(dt);
                    break;
            }
        }

        // ---- 場面 9 の終わり ------------------------------------------------------

        /// <summary>卓のまわりの区画に、自分の足で入ったか</summary>
        bool InZone()
        {
            if (player == null || !player.CanMove || player.MoveHeld) return false;
            return Within(player.transform.position, table, zoneRadius);
        }

        /// <summary>足元 at が、卓 table のまわりの区画（上から見て radius の内）にあるか。高さは見ない</summary>
        public static bool Within(Vector3 at, Vector3 table, float radius)
        {
            return new Vector2(at.x - table.x, at.z - table.z).magnitude <= radius;
        }

        /// <summary>卓の前に立った。歩きを封じて、独白を一行（E で送る）</summary>
        void Muse()
        {
            beat = Beat.Musing;
            beatClock = 0f;
            if (player != null) player.HoldMove(this);
            if (hud != null)
            {
                hud.SetPrompt(null);
                hud.SetSubtitle(Reunion.TableLine, SubtitleKind.Line, true);
            }
            ConsoleLog.Said(Reunion.TableLine);
        }

        // ---- 場面 10 -------------------------------------------------------------

        /// <summary>
        /// 場面 10 を始める。片割れを戸口の奥の暗がりに置き、戸を閉じた形にし、歩きを封じる。
        /// 独白を送った時と、場面 10 の頭から入った時に呼ぶ（エディタで撮るときは直に呼ぶ）
        /// </summary>
        public void Begin()
        {
            beat = Beat.Door;
            clock = 0f;
            beatClock = 0f;
            doorOpened = false;
            steppedOut = false;
            walkedOut = false;
            walked = 0f;
            frame = -1;
            cleared = false;
            ReunionHandoff.Active = true;
            if (player != null)
            {
                player.HoldMove(this);
                player.EyeOffset = Vector3.zero;
            }
            if (reunion != null) reunion.SetActive(true);
            if (faceLight != null) faceLight.enabled = true;
            DoorAt(0f);
            if (twin != null)
            {
                twin.transform.position = twinInside;
                twin.transform.rotation = Quaternion.Euler(0f, Toward(twinInside, twinOut), 0f);
                twin.Pose(-1, 0f);
                if (player != null) twin.Watch(player.Eye);
            }
            face = null;
            leftEye = null;
            rightEye = null;
        }

        /// <summary>
        /// 裏口が開き、片割れが出てきて、卓の方へ少し歩いて立ち止まり、口元を覆い、一歩踏み出す。見回しと歩きは封じたまま、目は片割れの顔を追う
        /// </summary>
        void Door()
        {
            if (!doorOpened && clock >= openAt)
            {
                doorOpened = true;
                if (doorSound != null && doorSound.clip != null)
                {
                    doorSound.Play();
                    if (doorSoundSeconds > 0f && Application.isPlaying) doorSound.SetScheduledEndTime(AudioSettings.dspTime + doorSoundSeconds);
                }
            }
            DoorAt(doorSeconds > 0f ? Mathf.Clamp01((clock - openAt) / doorSeconds) : (clock >= openAt ? 1f : 0f));
            if (!attending && clock >= lookAt) Attend(PlayerController.FaceSeconds);
            if (twin == null) { if (clock >= ReleaseAt) Free(); return; }

            // 戸口の奥から戸口の外へ出て、そのまま卓の方（プレイヤーの方）へ歩いて立ち止まる。
            // 二本の線を一続きの道にして、歩き出しと立ち止まりだけをなめらかにする
            if (!walkedOut)
            {
                walkedOut = true;
                stopAt = Clamp(twinOut + TowardEye(twinOut) * outWalk);
            }
            var first = Vector3.Distance(Flat(twinInside), Flat(twinOut));
            var second = Vector3.Distance(Flat(twinOut), Flat(stopAt));
            var k = OutSpan > 0f ? Mathf.Clamp01((clock - comeOutAt) / OutSpan) : 1f;
            var along = (first + second) * Gaze.Ease(k);
            Vector3 at;
            if (along < first) at = Vector3.Lerp(twinInside, twinOut, first > 0f ? along / first : 1f);
            else at = Vector3.Lerp(twinOut, stopAt, second > 0f ? (along - first) / second : 1f);
            // 戸口の奥の床（敷石より低い）から、戸口を出たらテラスの上面へ
            at.y = Mathf.Lerp(twinInside.y, walkY, first > 0f ? Mathf.Clamp01(along / (first * 0.5f)) : 1f);
            if (clock >= StepAt)
            {
                // 一歩踏み出す。向きはプレイヤーの目の方
                if (!steppedOut)
                {
                    steppedOut = true;
                    stepFrom = stopAt;
                    stepTo = Clamp(stopAt + TowardEye(stopAt) * stepLength);
                }
                var s = stepSeconds > 0f ? Mathf.Clamp01((clock - StepAt) / stepSeconds) : 1f;
                at = Vector3.Lerp(stepFrom, stepTo, Gaze.Ease(s));
            }
            // 歩いている間は進む向き、立ち止まったらプレイヤーの目の方を向く
            var heading = at - twin.transform.position;
            heading.y = 0f;
            twin.transform.position = at;
            Vector3 look;
            if (clock < comeOutAt) look = Flat(twinOut - twinInside).normalized;
            else look = k < 1f && heading.sqrMagnitude > 1e-8f ? heading.normalized : TowardEye(at);
            if (look.sqrMagnitude > 1e-6f) twin.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 0f);

            // 口元を覆う手。一歩踏み出す間も覆ったまま
            var mouth = gaspSeconds > 0f ? Mathf.Clamp01((clock - GaspAt) / gaspSeconds) : (clock >= GaspAt ? 1f : 0f);
            twin.Pose(MouthPose, Gaze.Ease(mouth));

            if (clock >= ReleaseAt) Free();
        }

        /// <summary>戸口の奥から、立ち止まる所まで歩く秒</summary>
        float OutSpan
        {
            get { return (Vector3.Distance(Flat(twinInside), Flat(twinOut)) + Mathf.Max(0f, outWalk)) / Mathf.Max(0.05f, outSpeed); }
        }

        /// <summary>立ち止まる秒（場面 10 の頭から）</summary>
        public float StopAt { get { return comeOutAt + OutSpan; } }

        /// <summary>口元を覆い始める秒</summary>
        public float GaspAt { get { return StopAt + gaspDelay; } }

        /// <summary>一歩踏み出す秒</summary>
        public float StepAt { get { return GaspAt + gaspSeconds + gaspHold; } }

        /// <summary>封じを解いて、歩み寄れるようにする秒</summary>
        public float ReleaseAt { get { return StepAt + stepSeconds + releaseDelay; } }


        /// <summary>封じを解いて、歩み寄れるようにする。目は向けるのをやめ、その向きのまま渡す</summary>
        void Free()
        {
            beat = Beat.Approach;
            beatClock = 0f;
            walked = 0f;
            Release();
        }

        /// <summary>
        /// 歩み寄る。プレイヤーは自分の足で寄る。片割れは口元の手を下ろし、少し遅れてゆっくり寄る（テラスの上だけ、<see cref="twinReach"/> まで）。
        /// 目と顔が <see cref="meetDistance"/> まで近づいたら頬の段へ
        /// </summary>
        void Approach(float dt)
        {
            if (twin != null)
            {
                var lower = lowerSeconds > 0f ? Mathf.Clamp01(beatClock / lowerSeconds) : 1f;
                twin.Pose(lower < 1f ? MouthPose : -1, 1f - Gaze.Ease(lower));
                var at = twin.transform.position;
                if (beatClock >= approachAfter && walked < twinReach && Apart > meetDistance)
                {
                    var toward = TowardEye(at);
                    var go = Mathf.Min(twinSpeed * dt, twinReach - walked);
                    var next = Clamp(at + toward * go);
                    walked += Vector3.Distance(Flat(at), Flat(next));
                    at = next;
                }
                twin.transform.position = at;
                var look = TowardEye(at);
                if (look.sqrMagnitude > 1e-6f) twin.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 0f);
            }
            // 片割れが繋がっていなければ（動作確認）、そのまま頬の段へ
            if (twin == null || Apart <= meetDistance) Meet();
        }

        /// <summary>近づいた。歩きと見回しを封じ、目を片割れの顔へ向ける。片割れはもう半歩寄る</summary>
        void Meet()
        {
            beat = Beat.Touch;
            beatClock = 0f;
            if (player != null)
            {
                player.HoldMove(this);
                player.HoldLook(this);
            }
            Attend(touchLookSeconds);
            if (twin == null) return;
            twin.Pose(-1, 0f);
            touchFrom = twin.transform.position;
            var eye = player != null && player.Eye != null ? player.Eye.position : touchFrom;
            var away = Flat(touchFrom - eye);
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            touchTo = new Vector3(eye.x, touchFrom.y, eye.z) + away * touchApart;
        }

        /// <summary>もう半歩寄って、手を頬へ伸ばす。触れたまま少し置いてモンタージュへ</summary>
        void Touch()
        {
            if (twin != null)
            {
                var k = stepInSeconds > 0f ? Mathf.Clamp01(beatClock / stepInSeconds) : 1f;
                var at = Vector3.Lerp(touchFrom, touchTo, Gaze.Ease(k));
                twin.transform.position = at;
                var look = TowardEye(at);
                if (look.sqrMagnitude > 1e-6f) twin.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 0f);
                var r = reachSeconds > 0f ? Mathf.Clamp01((beatClock - stepInSeconds) / reachSeconds) : (beatClock >= stepInSeconds ? 1f : 0f);
                twin.Pose(CheekPose, Gaze.Ease(r));
            }
            if (beatClock >= stepInSeconds + reachSeconds + holdSeconds)
            {
                beat = Beat.Montage;
                beatClock = 0f;
                frame = -1;
                Flash();
            }
        }

        // ---- モンタージュ -----------------------------------------------------------

        /// <summary>
        /// (a)・(b) は前もって撮った絵を画面いっぱいに、(c) はその場の絵（絵を伏せる）。暗転を挟まず、次のフレームで切り替える
        /// </summary>
        void Flash()
        {
            var n = montageSeconds > 0f ? Mathf.FloorToInt(beatClock / montageSeconds) : 3;
            if (n >= 3)
            {
                Show(null);
                beat = Beat.Voice;
                beatClock = 0f;
                frame = -1;
                if (hud != null) hud.SetSubtitle(Reunion.Voice, SubtitleKind.Line, true);
                ConsoleLog.Said(Reunion.Voice);
                return;
            }
            if (n == frame) return;
            frame = n;
            Show(n == 0 ? mirrorShot : n == 1 ? gardenShot : null);
        }

        /// <summary>モンタージュの絵を出す。null で伏せる（その場の絵に戻る）</summary>
        void Show(Texture2D shot)
        {
            if (montageRoot == null || picture == null) return;
            picture.texture = shot;
            montageRoot.SetActive(shot != null);
        }

        /// <summary>
        /// モンタージュの絵を敷く Canvas を作る。3D の絵の上、粗い画面（HUD）の下に重ねる。
        /// 絵はゲームの見え方で撮った 320×180 を最近傍で引き伸ばす。縦横比が違う画面では、はみ出す側を切る
        /// </summary>
        void Montage()
        {
            if (montageRoot != null || (mirrorShot == null && gardenShot == null)) return;
            montageRoot = new GameObject("ReunionMontage", typeof(RectTransform), typeof(Canvas));
            if (montageRoot.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(montageRoot, gameObject.scene);
            var canvas = montageRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiLens.ShowOrder - 1;
            var pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            pic.transform.SetParent(montageRoot.transform, false);
            var rect = (RectTransform)pic.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            picture = pic.GetComponent<RawImage>();
            picture.raycastTarget = false;
            var fit = pic.GetComponent<AspectRatioFitter>();
            fit.aspectRatio = 16f / 9f;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            montageRoot.SetActive(false);
        }

        // ---- 言葉と最後の独白 ---------------------------------------------------------

        void Say(Beat next, string line)
        {
            beat = next;
            beatClock = 0f;
            if (hud != null) hud.SetSubtitle(line, SubtitleKind.Line, true);
            ConsoleLog.Said(line);
        }

        // ---- 暗転とエンディング --------------------------------------------------------

        /// <summary>最後の独白を送った。黒へ落とし始め、環境音を絞る</summary>
        void Close()
        {
            beat = Beat.Closing;
            beatClock = 0f;
            if (hud != null) hud.SetSubtitle(null);
            // 環境音は黒へ落とすのに合わせて絞る。村の環境音は毎フレーム大きさを書くので止める
            if (ambience != null) ambience.enabled = false;
            quieted.Clear();
            quietFrom.Clear();
            // 絞るのは村のシーンの音源だけ。場面をまたいで残る音源（曲など）は、それを持つ者に任せる
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (s == null || !s.isPlaying || s.gameObject.scene != gameObject.scene) continue;
                quieted.Add(s);
                quietFrom.Add(s.volume);
            }
        }

        /// <summary>黒へ落とし始めてから、エンディングを読むまでの秒</summary>
        public float EndAt { get { return blackSeconds + blackHold; } }

        void Closing(float dt)
        {
            var t = beatClock;
            // 黒へ落とす（場面 10 の終わりだけ暗転を使う。ゲーム設計 8 節）
            var k = blackSeconds > 0f ? Mathf.Clamp01(t / blackSeconds) : 1f;
            if (hud != null) hud.SetFade(k);
            for (var i = 0; i < quieted.Count; i++)
                if (quieted[i] != null) quieted[i].volume = quietFrom[i] * (1f - k);
            if (k >= 1f && quieted.Count > 0)
            {
                foreach (var s in quieted) if (s != null) s.Stop();
                quieted.Clear();
                quietFrom.Clear();
            }
            if (k >= 1f && faceLight != null) faceLight.enabled = false;
            if (t >= EndAt) Finish();
        }

        /// <summary>
        /// クリアの印を付けて、エンディング（<see cref="nextScene"/>）を読む。エンディングは黒から始まる（イグニッションの音から）。
        /// まだ組み立ての一覧に無ければ、タイトルの画面へ戻る。どちらも無ければ黒のまま「（仮）続く」
        /// </summary>
        void Finish()
        {
            beat = Beat.Done;
            if (!cleared)
            {
                cleared = true;
                SaveStore.MarkCleared();
            }
            if (!Application.isPlaying) return;
            var next = Next;
            if (next != null)
            {
                SceneManager.LoadScene(next);
                return;
            }
            if (!SaveFlow.ToTitle() && hud != null) hud.SetCenter(SceneFlow.ToBeContinued);
        }

        // ---- 目を向ける・封じる ---------------------------------------------------

        void Attend(float seconds)
        {
            if (player == null) return;
            attending = true;
            player.HoldLook(this);
            player.HoldMove(this);
            // 追うのは頭の骨。目の真ん中までのずれは今の形で決める（片割れはいつもプレイヤーの方を向いているので、ずれの向きは変わらない）
            var at = Face();
            if (face != null) player.Follow(face, at - face.position, seconds);
            else if (twin != null) player.Follow(twin.transform, at - twin.transform.position, seconds);
        }

        void Release()
        {
            if (player == null) return;
            if (attending) player.StopFacing();
            attending = false;
            player.FreeLook(this);
            player.FreeMove(this);
        }

        // ---- 道 -----------------------------------------------------------------

        /// <summary>戸の開き具合（0 で閉じ、1 で開き切る）の形に置く</summary>
        void DoorAt(float amount)
        {
            if (door == null) return;
            if (!doorKnown) { doorClosed = door.localRotation; doorKnown = true; }
            door.localRotation = doorClosed * Quaternion.Euler(0f, doorOpenYaw * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)), 0f);
        }

        /// <summary>at からプレイヤーの目の方の、水平の向き（長さ 1）</summary>
        Vector3 TowardEye(Vector3 at)
        {
            if (player == null || player.Eye == null) return Vector3.zero;
            var d = Flat(player.Eye.position - at);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }

        /// <summary>テラスの上に収める</summary>
        Vector3 Clamp(Vector3 p)
        {
            return new Vector3(Mathf.Clamp(p.x, walkArea.x, walkArea.y), walkY, Mathf.Clamp(p.z, walkArea.z, walkArea.w));
        }

        static float Toward(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        // ---- 記憶する・思い出す -----------------------------------------------------

        public string MemoryKey { get { return "reunion"; } }

        /// <summary>場面 9 の間は邪魔しない。場面 10 を流している間は残さない（記憶するは場面 10 の頭を書く）</summary>
        public bool Settled { get { return beat == Beat.Off || beat == Beat.Waiting || beat == Beat.Musing; } }

        public string Capture() { return null; }

        /// <summary>場面 10 は頭からしか始まらない。場面 9 の独白の間に記憶した物は、区画の中の立ち位置から独白をやり直す</summary>
        public void Restore(string data) { }
    }
}
