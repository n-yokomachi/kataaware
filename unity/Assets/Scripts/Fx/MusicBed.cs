using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>
    /// 場面をまたいで一つだけ鳴る BGM の置き場（音楽の設計書 5 節）。場の外から鳴る曲を、2D で流す。
    ///
    /// **場面をまたいで残る一つの物。** コンソール（<see cref="ImplantConsole"/>）と同じく、遊び始めに一つだけ作って
    /// DontDestroyOnLoad で持ち越す。シーンにもプレハブにも置かない。曲の割り当ては <see cref="MusicTable"/>（Resources/MusicTable）が持つ。
    /// 場面の演出は、決めた所で <see cref="Play(MusicCue)"/>・<see cref="FadeOut"/>・<see cref="Stop"/> を呼ぶだけ。
    ///
    /// 音源は二つ。いまの曲と、クロスフェードで消えていく曲。段取りは <see cref="MusicMix"/> が持ち、ここは音源へ写すだけにしてある。
    ///
    /// **場面を移った時。** 新しい場面を読んでから最初の LateUpdate までに（Awake・Start のうちに）、どの演出も曲を頼まなかったら（<see cref="Play(MusicCue)"/> などを呼ばなかったら）、
    /// 前の場面の曲は <see cref="Release"/> 秒で消す。コンソールの一覧で飛んだ時や思い出した時に、前の場面の曲が残らないように。
    /// 読み込みを越えて流し続けたい時は、読む前に <see cref="Carry"/> を呼ぶ。
    /// 村（<see cref="MusicTable.villageScene"/>）に入った時は、誰も頼まなければ <see cref="MusicCue.Village"/> をフェードインで流す。
    /// 場面 6（夕方の庭の記憶）として開いた時は流さない（<see cref="MusicTable.Arrival"/>）。
    ///
    /// **コンソールを開いている間（秒が止まっている間）は、曲はそのまま流れ、フェードだけが止まる。** 環境音（<see cref="VillageAmbience"/>・
    /// <see cref="RoomTone"/>）と同じ扱い。音が途切れると、視界に重ねた画面ではなく一時停止に見える（<see cref="ImplantConsole"/>）。
    /// 場面の段取りも同じ秒で止まるので、フェードと演出の時点はずれない。
    ///
    /// **Web（WebGL）の読み込み。** 曲は Preload Audio Data を切ってある（<c>MusicAudioImport</c>）。鳴らす時に展開が済んでいなければ、
    /// 済むまで待ってから鳴らし、フェードもそこから数える（<see cref="SoundLoad"/>。展開の途中に鳴らすと鳴り出しが後へ延び、
    /// 長さも 0 と読めてしまう）。先に要ると分かっている曲は、演出が場面の頭で <see cref="Warm"/> しておく。
    /// 鳴らし終えた曲は捨てる（<see cref="AudioClip.UnloadAudioData"/>）。展開した曲はステレオで 1 秒 384 KB ほどある
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class MusicBed : MonoBehaviour
    {
        /// <summary>裂け目で止める時の、ごく短い消え。秒。ぶつっと切れて聞こえるが、波形の段で音が弾けない</summary>
        public const float Snap = 0.05f;

        /// <summary>場面を移ったのに、新しい場面の演出が曲を頼まなかった時、前の場面の曲を消す秒</summary>
        public const float Release = 0.5f;

        static MusicBed instance;
        static bool warned;

        readonly MusicMix mix = new MusicMix();
        readonly AudioSource[] sources = new AudioSource[2];
        /// <summary>声ごとに当てた曲の行（大きさと輪）</summary>
        readonly MusicTable.Track[] tracks = new MusicTable.Track[2];
        /// <summary>声ごとに、読み込みを待ち始めた実時間。待っていなければ負</summary>
        readonly float[] waitSince = { -1f, -1f };
        /// <summary>先に読んでおいた曲と、読んだフレーム。鳴らすまでは捨てない。鳴らさないまま場面を移ったら捨てる</summary>
        readonly Dictionary<AudioClip, int> warmed = new Dictionary<AudioClip, int>();

        MusicTable table;
        /// <summary>いちばん最後に曲を頼まれたフレーム</summary>
        int claimed = -1;
        /// <summary>いちばん最後に場面を読んだフレーム</summary>
        int loaded = -2;
        bool checking;
        bool carry;
        MusicCue arrival;

        /// <summary>遊んでいる間の一つ。まだ作っていなければ null</summary>
        public static MusicBed Instance { get { return instance; } }

        /// <summary>段取り。動作確認とテストから読む</summary>
        public MusicMix Mix { get { return mix; } }

        /// <summary>i 番の声の音源。動作確認とテストから読む</summary>
        public AudioSource Source(int i) { return sources[i]; }

        /// <summary>いま流している曲。消えていく途中の曲は数えない</summary>
        public static MusicCue Current { get { return instance != null ? instance.mix.Current : MusicCue.None; } }

        // ---- 作る ------------------------------------------------------------

        /// <summary>再生を始めるたびに静的な状態を戻す。ドメインを読み直さない設定でも前の再生を持ち越さない</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            instance = null;
            warned = false;
        }

        /// <summary>最初の場面の読み込みは sceneLoaded より後なので、ここで同じ扱いをする</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var bed = Ensure();
            if (bed != null) bed.Arrived(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        /// <summary>遊んでいる間の一つを返す。無ければ作って場面をまたいで残す。エディタで遊んでいない時は作らず null</summary>
        public static MusicBed Ensure()
        {
            if (instance != null) return instance;
            if (!Application.isPlaying) return null;
            var t = Resources.Load<MusicTable>(MusicTable.Path);
            if (t == null && !warned)
            {
                warned = true;
                Debug.LogWarning("MusicBed: Resources/" + MusicTable.Path + " が無い。BGM は鳴らさない");
            }
            var made = Make(t);
            DontDestroyOnLoad(made.gameObject);
            return made;
        }

        /// <summary>
        /// 組むだけ。持ち越しは付けない。テストはこれで作り、<see cref="Step"/> を直に回して、終えたら <see cref="Dispose"/> で消す
        /// </summary>
        public static MusicBed Make(MusicTable table)
        {
            var go = new GameObject("MusicBed");
            var bed = go.AddComponent<MusicBed>();
            bed.table = table;
            for (var i = 0; i < bed.sources.Length; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.dopplerLevel = 0f;
                s.priority = 0;
                s.bypassReverbZones = true;
                s.volume = 0f;
                bed.sources[i] = s;
            }
            if (instance == null) instance = bed;
            return bed;
        }

        /// <summary>止めて消す。遊んでいる間の一つだったら、それも下ろす</summary>
        public void Dispose()
        {
            mix.Stop();
            Sync();
            if (instance == this) instance = null;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += Arrived;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= Arrived;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---- 頼む口（場面の演出から呼ぶ） ------------------------------------------

        /// <summary>
        /// cue を流す。同じ曲が流れていれば何もしない（頭へ戻さない）。何も流れていなければ曲の fadeIn 秒で入り、
        /// ほかの曲が流れていれば曲の crossFade 秒で渡る。曲を当てていなければ何も鳴らさない（いま鳴っている曲もそのまま）
        /// </summary>
        public static void Play(MusicCue cue)
        {
            var bed = Ensure();
            if (bed != null) bed.Request(cue, -1f);
        }

        /// <summary><see cref="Play(MusicCue)"/> の秒を seconds に替えたもの（入るのも渡るのも seconds 秒）</summary>
        public static void Play(MusicCue cue, float seconds)
        {
            var bed = Ensure();
            if (bed != null) bed.Request(cue, Mathf.Max(0f, seconds));
        }

        /// <summary>ほかの曲から cue へ seconds 秒で渡る。何も流れていなければ seconds 秒で入る</summary>
        public static void CrossFade(MusicCue cue, float seconds)
        {
            Play(cue, seconds);
        }

        /// <summary>流れている曲を seconds 秒で消す。消えきったら止めて捨てる。0 秒ならその場で止める</summary>
        public static void FadeOut(float seconds)
        {
            var bed = Ensure();
            if (bed != null) bed.Fade(seconds);
        }

        /// <summary>その場で止める。フェードしない（同じフレームのうちに音源を止める）</summary>
        public static void Stop()
        {
            var bed = Ensure();
            if (bed != null) bed.Halt();
        }

        /// <summary>cue の曲を先に読んでおく。Web で、鳴らしたい時に展開を待たずに済むように</summary>
        public static void Warm(MusicCue cue)
        {
            var bed = Ensure();
            if (bed != null) bed.WarmUp(cue);
        }

        /// <summary>次の場面の読み込みを越えて、いまの曲を流し続ける（その読み込みの一度だけ）</summary>
        public static void Carry()
        {
            var bed = Ensure();
            if (bed != null) bed.carry = true;
        }

        // ---- 中身 ------------------------------------------------------------

        /// <summary>cue を頼む。seconds が負なら曲の行の秒を使う</summary>
        public void Request(MusicCue cue, float seconds)
        {
            claimed = Time.frameCount;
            var track = table != null ? table.Find(cue) : null;
            if (track == null) return;
            var fadeIn = seconds >= 0f ? seconds : track.fadeIn;
            var cross = seconds >= 0f ? seconds : track.crossFade;
            if (mix.Play(cue, fadeIn, cross) == MusicStart.Kept) return;
            var i = mix.FrontIndex;
            tracks[i] = track;
            waitSince[i] = -1f;
            Sync();
        }

        /// <summary>流れている曲を seconds 秒で消す</summary>
        public void Fade(float seconds)
        {
            claimed = Time.frameCount;
            mix.FadeOut(seconds);
            Sync();
        }

        /// <summary>その場で止める</summary>
        public void Halt()
        {
            claimed = Time.frameCount;
            mix.Stop();
            Sync();
        }

        /// <summary>cue の曲を先に読む</summary>
        public void WarmUp(MusicCue cue)
        {
            var track = table != null ? table.Find(cue) : null;
            if (track == null) return;
            warmed[track.clip] = Time.frameCount;
            if (track.clip.loadState == AudioDataLoadState.Unloaded) track.clip.LoadAudioData();
        }

        /// <summary>
        /// 場面を読んだ。ここでは印を付けるだけで、同じフレームの LateUpdate で決める（新しい場面の Start が曲を頼むのを待つ）。
        /// sceneLoaded は新しい場面の Awake の後に来るので、場面 6 として開いたか（<see cref="GardenHandoff.Active"/>）はもう読める
        /// </summary>
        void Arrived(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            loaded = Time.frameCount;
            checking = true;
            arrival = MusicTable.Arrival(scene.name, GardenHandoff.Active, table != null ? table.villageScene : null);
            Forsake();
        }

        /// <summary>
        /// 前の場面で先に読んだまま鳴らさなかった曲を捨てる（場面 8 でチップを調べずに移った、など）。
        /// 新しい場面の Awake が同じフレームに読んだ曲は残す
        /// </summary>
        void Forsake()
        {
            var stale = new List<AudioClip>();
            foreach (var pair in warmed)
                if (pair.Value < loaded) stale.Add(pair.Key);
            foreach (var clip in stale)
            {
                warmed.Remove(clip);
                if (clip == null || InUse(clip) || clip.preloadAudioData) continue;
                if (Application.isPlaying) clip.UnloadAudioData();
            }
        }

        /// <summary>どちらかの声が使っている曲か。except 番の声は数えない（その声の曲を外す所で使う）</summary>
        bool InUse(AudioClip clip, int except = -1)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                if (i == except) continue;
                if (sources[i] != null && sources[i].clip == clip) return true;
                if (tracks[i] != null && tracks[i].clip == clip) return true;
            }
            return false;
        }

        void Update()
        {
            Step(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!checking) return;
            checking = false;
            var keep = Keeps(claimed, loaded, carry);
            carry = false;
            if (keep) return;
            if (mix.Sounding) mix.FadeOut(Release);
            if (arrival != MusicCue.None) Request(arrival, -1f);
            Sync();
        }

        /// <summary>
        /// 場面を読んだ後も、いまの曲をそのまま流すか。読んだフレームか、その後（最初の場面の Start は Boot の次のフレームに来ることがある）に
        /// 曲を頼まれていれば流す（新しい場面の演出が曲を決めた）。<see cref="Carry"/> されていても流す
        /// </summary>
        public static bool Keeps(int claimed, int loaded, bool carry)
        {
            return carry || claimed >= loaded;
        }

        /// <summary>dt 秒進める。テストからは直に呼ぶ</summary>
        public void Step(float dt)
        {
            Sync();
            mix.Tick(dt);
            Sync();
        }

        /// <summary>
        /// 声を音源へ写す。当てたばかりの曲は、読み込みが済んでいれば鳴らし始め、済んでいなければ待つ。
        /// 何も流していない声の音源は止めて、曲を捨てる。大きさは曲の行の大きさにフェードを掛けたもの
        /// </summary>
        void Sync()
        {
            for (var i = 0; i < sources.Length; i++)
            {
                var v = mix.Voice(i);
                var src = sources[i];
                if (src == null) continue;
                if (!v.Active)
                {
                    Silence(i);
                    tracks[i] = null;
                    continue;
                }
                var track = tracks[i];
                if (v.Fresh)
                {
                    if (track == null || track.clip == null) { v.Clear(); Silence(i); continue; }
                    if (src.clip != track.clip) Silence(i);
                    src.clip = track.clip;
                    src.loop = track.loop;
                    if (!Loaded(i, track.clip))
                    {
                        v.Waiting = true;
                        src.volume = 0f;
                        continue;
                    }
                    v.Waiting = false;
                    v.Fresh = false;
                    waitSince[i] = -1f;
                    warmed.Remove(track.clip);
                    src.volume = track.volume * v.Gain;
                    if (Application.isPlaying) src.Play();
                    continue;
                }
                src.volume = track != null ? track.volume * v.Gain : 0f;
            }
        }

        /// <summary>鳴らしてよいか。Web で展開の途中なら待つ。上限（<see cref="SoundLoad.WaitLimit"/>、実時間）を過ぎたら待たずに鳴らす</summary>
        bool Loaded(int i, AudioClip clip)
        {
            if (SoundLoad.Ready(clip)) return true;
            if (waitSince[i] < 0f) waitSince[i] = Time.realtimeSinceStartup;
            return Time.realtimeSinceStartup - waitSince[i] >= SoundLoad.WaitLimit;
        }

        /// <summary>i 番の音源を止め、曲を外す。ほかの声が使っておらず、先に読んだ物でもなければ、読んだ中身も捨てる</summary>
        void Silence(int i)
        {
            var src = sources[i];
            if (src == null) return;
            src.volume = 0f;
            var clip = src.clip;
            if (clip == null) return;
            if (Application.isPlaying) src.Stop();
            src.clip = null;
            if (InUse(clip, i) || warmed.ContainsKey(clip) || clip.preloadAudioData) return;
            if (Application.isPlaying) clip.UnloadAudioData();
        }
    }
}
