using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// エンディング（シナリオ設計 13 節）。場面 10 の最後の独白の後の暗転から入り、片割れを助手席に乗せて走る。
    /// 景色は帯（<see cref="EndingBand"/>）に分け、曲の秒で暗転を挟まずに切り替える（村の近くの森の朝から、湖沿いの夜まで）。
    /// 1. 黒のまま、イグニッションの音
    /// 2. 黒から明けるのと同時に、走行音と曲「HALF AWARE」を鳴らし始める。曲は初め車のオーディオから流れているような版（車の版）。
    ///    明けて 5 秒で、目が助手席の片割れの体へ一度向き（顔は映さない）、前へ戻る
    /// 3. 曲のドロップの前の溜めで、走行音を消し、車の版から素の版へ入れ替える（同じ頭から切り出した二つを、同じ時刻に鳴らしている）
    /// 4. ドロップからクレジットが縦に流れ、曲の尾と一緒にフェードアウトして、タイトルの画面へ戻る
    ///
    /// 段取りの秒は <see cref="EndingBeats"/>。明けてから後は曲の再生の位置（素の版の <see cref="AudioSource.time"/>）で数え、曲とずれない。
    /// 見回しと歩きはさせない。目の向きは段取りが書く。
    /// コンソールを開いている間（<see cref="ImplantConsole.IsOpen"/>）は、この場面の音を止めて段取りも止める（戻れば同じ所から続く）。
    ///
    /// 実行順を -15 に置くのは、PlayerController（-10）がそのフレームの目の向きを書く前に向きを渡すため。
    /// 車体の揺れは DriveWorld（-20）が渡す
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class EndingDirector : MonoBehaviour
    {
        [SerializeField] EndingBeats beats = new EndingBeats();

        [Header("走る")]
        [SerializeField] DriveWorld world;
        [Tooltip("景色の帯。DriveWorld の沿道の帯と同じ並び。入る曲の秒・走り・空と光を持つ。組み立て（BuildEndingLand.Route）が書く")]
        [SerializeField] EndingBand[] bands = new EndingBand[0];
        [Tooltip("帯ごとの動かない遠景（月・星・遠くの丘や村）。帯と同じ並び。無い帯は空")]
        [SerializeField] GameObject[] backdrops = new GameObject[0];
        [SerializeField] Light sun;
        [Tooltip("前照灯の照らし（場面 8 の車の Beam）。夜の帯だけ点く（帯の空の beam）")]
        [SerializeField] Renderer beams;

        [Header("目")]
        [SerializeField] PlayerController player;
        [SerializeField] Camera eye;
        [Tooltip("Player の根の置き場。運転席の目より窓の側へ寄せた所")]
        [SerializeField] Vector3 seat;
        [Tooltip("前を見ている向き。度（左右は +z が 0 で右回り、上下は下が正）。画面の左 2/3 が車内、右 1/3 が窓の外")]
        [SerializeField] Vector2 front = new Vector2(8f, 6f);
        [Tooltip("片割れを見る向き。度。顔が画面に入らず、膝と手と胸の下までが入る所")]
        [SerializeField] Vector2 twin = new Vector2(-78f, 30f);
        [Tooltip("片割れを見る間に、目を寄せる量（Player の根から見た m）。少し身を低くして覗き込む。" +
            "下を向ける限り（40 度）のまま、画面の上の縁を片割れの顎より下へ下ろすため。回る割合と同じだけ効かせる")]
        [SerializeField] Vector3 glanceShift = new Vector3(-0.03f, -0.06f, 0.02f);
        [Tooltip("座っている間、首だけで振り向ける角度。度")]
        [SerializeField] float headLimit = 120f;

        [Header("音")]
        [SerializeField] AudioSource ignition;
        [SerializeField] AudioSource road;
        [Tooltip("車の版（HalfAwareCar.ogg）")]
        [SerializeField] AudioSource songCar;
        [Tooltip("素の版（HalfAware.ogg）。この再生の位置を曲の時計にする")]
        [SerializeField] AudioSource songOpen;
        [SerializeField, Range(0f, 1f)] float ignitionVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] float roadVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float carVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float openVolume = 0.85f;
        [Tooltip("曲の展開を待つ上限。秒。過ぎたら読めた物だけで始める（Web で展開が遅れた時に、黒のまま止まらないように）")]
        [SerializeField] float loadWait = 8f;

        [Header("画面")]
        [Tooltip("粗い画面で描く Canvas（クレジットの見出しと名前の行、黒）")]
        [SerializeField] Canvas lensCanvas;
        [Tooltip("画面を覆う黒")]
        [SerializeField] Image cover;
        [SerializeField] CreditRoll credits;
        [Tooltip("クレジットの暗がりが濃くなりきるまで。秒")]
        [SerializeField] float shadeRise = 2.5f;
        [Tooltip("終わって戻る所")]
        [SerializeField] string nextScene = SaveFlow.TitleScene;

        float clock;
        float song = -1f;
        bool ignited;
        bool started;
        bool paused;
        bool leaving;
        float waited;
        int band = -1;
        /// <summary>コンソールを開いた時に止めた音。閉じたらこれだけを続ける（止めていなかった物を鳴らし始めない）</summary>
        readonly System.Collections.Generic.List<AudioSource> held = new System.Collections.Generic.List<AudioSource>();

        /// <summary>段取りの秒</summary>
        public EndingBeats Beats { get { return beats; } }

        /// <summary>いまの曲の秒。曲を鳴らし始める前は負</summary>
        public float Song { get { return song; } }

        /// <summary>いま出している景色の帯の番号</summary>
        public int Band { get { return band; } }

        /// <summary>景色の帯</summary>
        public EndingBand[] Bands { get { return bands; } }

        void Awake()
        {
            if (player != null)
            {
                player.CanMove = false;
                player.CanLook = false;
                player.EyeHeight = 0f;
                player.HeadYawLimit = headLimit;
            }
            // 黒のあいだから走らせておく。明けた時にはもう走っている
            if (world != null) world.Rolling = Application.isPlaying;
            band = -1;
            foreach (var s in new[] { songCar, songOpen })
                if (s != null && s.clip != null && s.clip.loadState == AudioDataLoadState.Unloaded) s.clip.LoadAudioData();
            if (Application.isPlaying && lensCanvas != null) UiLens.Adopt(lensCanvas, 0);
            Apply(-1f);
        }

        void Start()
        {
            // 場面 10 の曲が残っていれば、ここで止める。この場面の曲は自前の口で鳴らす
            if (Application.isPlaying) MusicBed.Stop();
        }

        void Update()
        {
            if (leaving) return;
            if (Hold()) return;
            clock += Time.deltaTime;
            if (!ignited && clock >= beats.ignitionAt)
            {
                ignited = true;
                if (ignition != null && ignition.clip != null)
                {
                    ignition.volume = ignitionVolume;
                    ignition.Play();
                }
            }
            if (!started && clock >= beats.OpenAt)
            {
                waited += Time.deltaTime;
                if (Ready() || waited >= loadWait) Begin();
            }
            if (started) song = Heard();
            Apply(started ? song : -1f);
            Sound(started ? song : -1f);
            if (started && beats.Finished(song))
            {
                leaving = true;
                if (!SaveFlow.ToTitle()) UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }
        }

        /// <summary>コンソールを開いている間は、この場面の音を止めて段取りも止める</summary>
        bool Hold()
        {
            var open = ImplantConsole.IsOpen;
            if (open == paused) return open;
            paused = open;
            if (open)
            {
                held.Clear();
                foreach (var s in new[] { ignition, road, songCar, songOpen })
                {
                    if (s == null || !s.isPlaying) continue;
                    s.Pause();
                    held.Add(s);
                }
            }
            else
            {
                foreach (var s in held) if (s != null) s.UnPause();
                held.Clear();
            }
            return open;
        }

        /// <summary>二つの版とも展開が済んだか</summary>
        bool Ready()
        {
            foreach (var s in new[] { songCar, songOpen })
                if (s != null && s.clip != null && s.clip.loadState != AudioDataLoadState.Loaded) return false;
            return true;
        }

        /// <summary>明ける。曲の二つの版を同じフレームに頭から鳴らし、走行音を鳴らす</summary>
        void Begin()
        {
            started = true;
            song = 0f;
            if (songCar != null && songCar.clip != null) { songCar.volume = carVolume; songCar.Play(); }
            if (songOpen != null && songOpen.clip != null) { songOpen.volume = 0f; songOpen.Play(); }
            if (road != null && road.clip != null) { road.volume = roadVolume; road.Play(); }
        }

        /// <summary>
        /// 曲の秒。素の版が鳴っていればその再生の位置、鳴っていなければ（読めなかった・鳴り終えた）フレームの秒を足す。
        /// 戻ることはない
        /// </summary>
        float Heard()
        {
            var next = songOpen != null && songOpen.clip != null && songOpen.isPlaying ? songOpen.time : song + Time.deltaTime;
            return Mathf.Max(song, next);
        }

        /// <summary>
        /// 曲の秒 at の見え方を当てる（黒の濃さ・目の向き・クレジット）。at が負なら黒のまま前を向く。
        /// エディタで撮る時にも呼ぶ（再生していなければ、目は Player を置き直して書く）
        /// </summary>
        public void Apply(float at)
        {
            Scenery(EndingBand.At(bands, at));
            if (cover != null)
            {
                var c = cover.color;
                c.a = beats.Cover(at);
                cover.color = c;
                cover.enabled = c.a > 0f;
            }
            Look(at < 0f ? 0f : beats.Glance(at));
            if (credits != null)
            {
                var shown = at >= 0f && beats.Rolling(at);
                var dim = shadeRise > 0f ? Mathf.Clamp01((at - beats.creditsFrom) / shadeRise) : 1f;
                credits.Set(beats.Roll(at), dim, shown);
            }
        }

        /// <summary>
        /// which 番の帯を出す。沿道・遠景・空と光・走りをその場で差し替える（暗転を挟まない。場面 8 と同じ）。
        /// 走った距離は戻さない（道は同じ環のまま続く）
        /// </summary>
        void Scenery(int which)
        {
            if (which == band || which < 0 || which >= bands.Length) return;
            band = which;
            var b = bands[which];
            if (world != null)
            {
                world.Dress(which);
                world.Speed = b.speed;
                world.Rough = b.rough;
                // 替えた帯の区切りを、このフレームのうちに今の走った距離の並びへ置く。
                // DriveWorld はこのフレームの並べ直しをもう済ませている（実行順が先）ので、置かないと 1 フレームだけ組んだ時の並びで映る
                if (Application.isPlaying) world.Place();
            }
            for (var i = 0; i < backdrops.Length; i++)
                if (backdrops[i] != null) backdrops[i].SetActive(i == which);
            b.sky.Apply(sun, eye, beams);
        }

        /// <summary>目を前（0）と片割れ（1）のあいだの k へ向ける</summary>
        void Look(float k)
        {
            if (player == null) return;
            var a = Gaze.Blend(front, twin, k);
            if (Application.isPlaying)
            {
                player.Yaw = a.x;
                player.Pitch = a.y;
                // 車体の揺れ（DriveWorld が同じフレームに先に書く）に足す
                player.EyeOffset += glanceShift * k;
            }
            else
            {
                player.EyeOffset = glanceShift * k;
                player.PlaceAt(seat, 0f, headLimit, a.x, a.y, 0f);
            }
        }

        /// <summary>曲の秒 at の音の大きさ</summary>
        void Sound(float at)
        {
            if (ignition != null && ignition.isPlaying && at >= 0f)
            {
                var tail = beats.IgnitionTail(at);
                ignition.volume = ignitionVolume * tail;
                if (tail <= 0f) ignition.Stop();
            }
            if (at < 0f) return;
            var open = beats.Open(at);
            if (songOpen != null) songOpen.volume = openVolume * open;
            if (songCar != null && songCar.isPlaying)
            {
                songCar.volume = carVolume * (1f - open);
                if (open >= 1f) songCar.Stop();
            }
            if (road != null && road.isPlaying)
            {
                var r = beats.Road(at);
                road.volume = roadVolume * r;
                if (r <= 0f) road.Stop();
            }
        }
    }
}
