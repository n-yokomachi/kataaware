using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ヤードの出店に置いた古いラジオから流す曲（BGM の設計メモ 2 節）。
    /// 曲を順に流し、最後の曲の後は最初へ戻る。場面の始めに、全部をつないだ中から始める位置を選ぶので、
    /// ヤードに入ったときには曲の途中から流れている。
    ///
    /// 音はラジオからの 3D の音（距離で小さくなる）に、いる所ごとの倍率を掛ける。
    /// ヤードでは 1、小路ではヤードの口へ近づくほど漏れ（口で <see cref="laneLeak"/>、通りの側の端で 0）、通りでは 0。
    /// 台詞と独白の字幕が出ているあいだ（<see cref="SceneFlow.Talking"/>）は下げる。
    /// 帯域を絞って響きを足す加工はファイルに焼いてある（Web では Unity のオーディオのフィルターが効かない）。
    ///
    /// 曲は全部を先に読まない。Web ではブラウザが曲を展開して持つので、4 曲を読むとメモリが大きい。
    /// 取り込みで Preload Audio Data を切り、今の曲と次の曲だけを読む（<see cref="AudioClip.LoadAudioData"/>）。
    /// 次の曲は今の曲の残りが <see cref="lead"/> 秒を切ってから読み、流し終えた曲は捨てる
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class StallRadio : MonoBehaviour
    {
        /// <summary>ラジオそのものの大きさの既定値</summary>
        public const float VolumeDefault = 0.8f;
        /// <summary>小路のヤードの口での倍率の既定値。かすかに漏れる程度</summary>
        public const float LaneLeakDefault = 0.3f;
        /// <summary>字幕のあいだの倍率の既定値</summary>
        public const float TalkingDefault = 0.5f;

        [SerializeField] AudioSource sound;
        [Tooltip("流す曲。この順に流し、最後の曲の後は最初へ戻る")]
        [SerializeField] AudioClip[] tracks = new AudioClip[0];
        [SerializeField] Transform player;
        [Tooltip("字幕が出ているかを見る")]
        [SerializeField] SceneFlow flow;
        [Tooltip("小路の範囲。組み立てが書き込む")]
        [SerializeField] Bounds lane;
        [Tooltip("ヤードの範囲。組み立てが書き込む")]
        [SerializeField] Bounds yard;

        [Header("聞こえ方")]
        [Tooltip("ラジオそのものの大きさ。ここに距離の減衰といる所の倍率が掛かる")]
        [SerializeField, Range(0f, 1f)] float volume = VolumeDefault;
        [Tooltip("小路のヤードの口での倍率。通りの側の端では 0。通りでは聞こえない")]
        [SerializeField, Range(0f, 1f)] float laneLeak = LaneLeakDefault;
        [Tooltip("買い手の台詞と独白のあいだの倍率")]
        [SerializeField, Range(0f, 1f)] float talking = TalkingDefault;
        [Tooltip("倍率が変わったとき、新しい値へ寄せる秒数")]
        [SerializeField] float blend = 0.8f;

        [Header("読み込み")]
        [Tooltip("今の曲の残りがこの秒数を切ったら、次の曲を読み始める")]
        [SerializeField] float lead = 30f;

        float[] lengths;
        int current = -1;
        int next = -1;
        /// <summary>今の曲を流し始める位置（秒）。読み終わるのを待っているあいだ持っておく</summary>
        float startAt;
        bool waiting;
        bool playing;
        /// <summary>今の曲を流し始めてからの秒数。止まったのが曲の終わりかどうかを見分ける</summary>
        float heard;
        float place = -1f;
        float duck = 1f;

        /// <summary>今の曲の番号。動作確認から読む</summary>
        public int Current => current;

        void Start()
        {
            if (sound == null || tracks == null || tracks.Length == 0) { enabled = false; return; }
            sound.playOnAwake = false;
            sound.loop = false;
            lengths = new float[tracks.Length];
            // 長さは中身を読まなくても分かる
            for (var i = 0; i < tracks.Length; i++) lengths[i] = tracks[i] != null ? tracks[i].length : 0f;
            int index;
            float time;
            if (!Playlist.Locate(Random.Range(0f, Playlist.Total(lengths)), lengths, out index, out time))
            {
                enabled = false;
                return;
            }
            Queue(index, time);
        }

        void OnDestroy()
        {
            // 場面を離れたら、読んだ曲を捨てる
            if (tracks == null) return;
            foreach (var t in tracks) if (t != null && !t.preloadAudioData) t.UnloadAudioData();
        }

        /// <summary>index の曲を time 秒から流す。読み終わるまで待つ</summary>
        void Queue(int index, float time)
        {
            current = index;
            startAt = time;
            next = Playlist.Next(index, lengths);
            waiting = true;
            playing = false;
            tracks[current].LoadAudioData();
        }

        void Update()
        {
            if (waiting) Begin();
            else if (playing) Keep();
            Mix();
        }

        void Begin()
        {
            var clip = tracks[current];
            if (clip.loadState == AudioDataLoadState.Loading || clip.loadState == AudioDataLoadState.Unloaded) return;
            waiting = false;
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning("StallRadio: 曲を読めない。次へ送る: " + clip.name, this);
                Advance();
                return;
            }
            sound.clip = clip;
            startAt = Mathf.Clamp(startAt, 0f, Mathf.Max(0f, clip.length - 0.05f));
            sound.time = startAt;
            sound.Play();
            playing = true;
            heard = 0f;
        }

        void Keep()
        {
            var clip = tracks[current];
            if (!AudioListener.pause) heard += Time.unscaledDeltaTime;
            // 次の曲は、今の曲の終わりが近づいてから読む。今の曲と次の曲の二つだけをメモリに持つ
            if (next >= 0 && next != current && clip.length - (startAt + heard) < lead
                && tracks[next].loadState == AudioDataLoadState.Unloaded)
                tracks[next].LoadAudioData();
            if (sound.isPlaying || AudioListener.pause) return;
            // 止まったのが曲の終わりのときだけ次へ送る。ブラウザが音を止めている（まだ一度も操作していない）あいだに
            // 次々と送って、読んでは捨てるのを繰り返さないように
            if (startAt + heard < clip.length - 1f) return;
            Advance();
        }

        /// <summary>今の曲を捨てて、次の曲を頭から流す</summary>
        void Advance()
        {
            var done = current;
            var after = Playlist.Next(done, lengths);
            if (after < 0) { enabled = false; return; }
            if (after != done && !tracks[done].preloadAudioData)
            {
                sound.Stop();
                sound.clip = null;
                tracks[done].UnloadAudioData();
            }
            Queue(after, 0f);
        }

        /// <summary>いる所と字幕で倍率を決め、寄せて掛ける</summary>
        void Mix()
        {
            var want = 0f;
            if (player != null)
            {
                var where = AlleySound.Where(player.position, lane, yard);
                want = where == AlleyPlace.Yard ? 1f
                    : where == AlleyPlace.Lane ? laneLeak * AlleySound.Depth(player.position, lane, yard)
                    : 0f;
            }
            // 場面の頭は寄せずに、その場の値から始める
            place = place < 0f ? want : AlleySound.Ease(place, want, Time.deltaTime, blend);
            var hush = flow != null && flow.Talking ? talking : 1f;
            duck = AlleySound.Ease(duck, hush, Time.deltaTime, blend);
            sound.volume = volume * place * duck;
        }
    }
}
