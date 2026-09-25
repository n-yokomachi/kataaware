using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 自室の場面（場面 1・3・5）の部屋の空気の音。雨や雑踏と同じく、プレイヤーの頭上で 2D の輪にして小さく流す。
    ///
    /// 大きさはインスペクターで変える。オーナーが耳で決めるので、組み立てても書き戻さない（インスペクターで変えた値が残る）。
    /// 場面を終えて暗転するときは、その暗転（<see cref="HudView.Fade"/>）に合わせて絞る。
    /// 場面の頭の黒からの明けには合わせない（黒いうちから鳴っている）
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class RoomTone : MonoBehaviour
    {
        /// <summary>大きさの既定値。台詞の邪魔をしない程度に小さく</summary>
        public const float DefaultVolume = 0.5f;

        [SerializeField] SceneFlow flow;
        [Tooltip("暗転の層を持つ HUD")]
        [SerializeField] HudView hud;
        [SerializeField] AudioSource sound;
        [Tooltip("ふだんの大きさ")]
        [SerializeField, Range(0f, 1f)] float volume = DefaultVolume;
        [Tooltip("暗転に付いていく秒数。ふだんの大きさからこの秒数で 0 まで動ける速さで寄せる。" +
            "黒へ切り替える暗転（場面 1 の扉の後）でも、音がぷつりと途切れないように")]
        [SerializeField] float follow = 0.3f;

        float level = -1f;

        /// <summary>
        /// 鳴らしたい大きさ。場面を終えている（closing）ときだけ、暗転の濃さ dark（0〜1）の分だけ絞る
        /// </summary>
        public static float Target(float volume, bool closing, float dark)
        {
            return closing ? volume * (1f - Mathf.Clamp01(dark)) : volume;
        }

        /// <summary>今の大きさ level を target へ、ふだんの大きさ full を seconds 秒で動ききる速さで寄せる</summary>
        public static float Ease(float level, float target, float full, float dt, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(level, target, Mathf.Max(full, 0.01f) * dt / seconds);
        }

        void Update()
        {
            if (sound == null) return;
            var target = Target(volume, flow != null && flow.Completed, hud != null ? hud.Fade : 0f);
            // 場面の頭は寄せずに、その大きさから始める
            level = level < 0f ? target : Ease(level, target, volume, Time.deltaTime, follow);
            sound.volume = level;
        }
    }
}
