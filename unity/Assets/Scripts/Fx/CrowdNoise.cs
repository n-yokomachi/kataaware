using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の雑踏の音。雨と同じく、プレイヤーの頭上で 2D の輪にして流し、いる所で大きさを変える。
    /// 通りではうっすら鳴り、ヤード手前の小路では通りより絞って静けさを立て、ヤードに入ると大きくなる。
    /// 売り買いのあいだ（露店の内側に立っているあいだ）もヤードの中なので、ヤードの大きさのまま。
    ///
    /// 小路とヤードの範囲は組み立て（BuildAlley の定数）が書き込む。大きさの値はオーナーが耳で決めるので、
    /// 組み直しても書き戻さない（インスペクターで変えた値が残る）
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class CrowdNoise : MonoBehaviour
    {
        /// <summary>通りでの大きさの既定値。うっすら</summary>
        public const float StreetDefault = 0.25f;
        /// <summary>小路での大きさの既定値。通りより小さく、静けさを立てる</summary>
        public const float LaneDefault = 0.10f;
        /// <summary>ヤードでの大きさの既定値</summary>
        public const float YardDefault = 0.70f;

        [SerializeField] Transform player;
        [SerializeField] AudioSource sound;
        [Tooltip("小路の範囲。組み立てが書き込む")]
        [SerializeField] Bounds lane;
        [Tooltip("ヤードの範囲。組み立てが書き込む")]
        [SerializeField] Bounds yard;

        [Header("いる所ごとの大きさ")]
        [Tooltip("グレビル・ストリート。うっすら鳴っている")]
        [SerializeField, Range(0f, 1f)] float streetVolume = StreetDefault;
        [Tooltip("ヤード手前の小路。通りより小さく")]
        [SerializeField, Range(0f, 1f)] float laneVolume = LaneDefault;
        [Tooltip("ヤード。売り買いのあいだもこの大きさ")]
        [SerializeField, Range(0f, 1f)] float yardVolume = YardDefault;
        [Tooltip("境をまたいだとき、新しい大きさへ寄せる秒数。境で急に変わると嘘に聞こえる")]
        [SerializeField] float blend = 1.2f;

        float volume = -1f;

        /// <summary>いまいる所。動作確認から読む</summary>
        public AlleyPlace Place { get; private set; }

        void Update()
        {
            if (player == null || sound == null) return;
            Place = AlleySound.Where(player.position, lane, yard);
            var target = AlleySound.Level(Place, streetVolume, laneVolume, yardVolume);
            // 場面の頭は寄せずに、その場の大きさから始める
            volume = volume < 0f ? target : AlleySound.Ease(volume, target, Time.deltaTime, blend);
            sound.volume = volume;
        }
    }
}
