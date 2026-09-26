using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 村と庭の場所（<c>Village.unity</c>）の環境音。朝は朝の村（鳥の声の輪）と麦の風の輪を重ね、夕方は麦の風だけを流す。
    /// 自室の空気の音（<see cref="RoomTone"/>）と同じく、プレイヤーの頭上で 2D の輪にして流す。
    ///
    /// 鳴らす物は時刻（<see cref="VillageHour.Current"/>）で決める。時刻が替わったら <see cref="follow"/> 秒かけて入れ替える。
    /// 場面の頭は寄せずに、その時刻の大きさから始める。
    ///
    /// 大きさはインスペクターで変える。オーナーが耳で決めるので、組み直しても書き戻さない
    /// （<c>BuildVillage.Rig</c> が前の値を引き継ぐ）
    /// </summary>
    public sealed class VillageAmbience : MonoBehaviour
    {
        public const float DefaultMorningVillage = 0.6f;
        public const float DefaultMorningWheat = 0.5f;
        public const float DefaultEveningWheat = 0.6f;

        [Tooltip("いまの時刻を持つ物")]
        [SerializeField] VillageHour hour;
        [Tooltip("朝の村の輪（VillageMorning）")]
        [SerializeField] AudioSource village;
        [Tooltip("麦の風の輪（WheatWind）")]
        [SerializeField] AudioSource wheat;

        [Header("大きさ")]
        [Tooltip("朝の、朝の村の輪")]
        [SerializeField, Range(0f, 1f)] float morningVillage = DefaultMorningVillage;
        [Tooltip("朝の、麦の風の輪")]
        [SerializeField, Range(0f, 1f)] float morningWheat = DefaultMorningWheat;
        [Tooltip("夕方の、麦の風の輪。夕方は朝の村を鳴らさない")]
        [SerializeField, Range(0f, 1f)] float eveningWheat = DefaultEveningWheat;
        [Tooltip("時刻が替わったとき、入れ替えにかける秒数")]
        [SerializeField] float follow = 2f;

        float villageLevel = -1f;
        float wheatLevel = -1f;

        /// <summary>
        /// 時刻 h で鳴らしたい大きさ。朝は朝の村と麦の風、夕方は麦の風だけ（朝の村は 0）
        /// </summary>
        public static void Mix(VillageHour.Hour h, float morningVillage, float morningWheat, float eveningWheat,
            out float village, out float wheat)
        {
            if (h == VillageHour.Hour.Morning)
            {
                village = morningVillage;
                wheat = morningWheat;
            }
            else
            {
                village = 0f;
                wheat = eveningWheat;
            }
        }

        /// <summary>今の大きさ level を target へ寄せる。0 から 1 までを seconds 秒で動ききる速さ。seconds が 0 以下ならその場で</summary>
        public static float Ease(float level, float target, float dt, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(level, target, dt / seconds);
        }

        void Update()
        {
            var h = hour != null ? hour.Current : VillageHour.Hour.Morning;
            Mix(h, morningVillage, morningWheat, eveningWheat, out var v, out var w);
            // 場面の頭は寄せずに、その大きさから始める
            villageLevel = villageLevel < 0f ? v : Ease(villageLevel, v, Time.deltaTime, follow);
            wheatLevel = wheatLevel < 0f ? w : Ease(wheatLevel, w, Time.deltaTime, follow);
            Drive(village, villageLevel);
            Drive(wheat, wheatLevel);
        }

        /// <summary>大きさを渡し、0 になったら止め、0 より大きくなったら鳴らし始める</summary>
        static void Drive(AudioSource source, float level)
        {
            if (source == null) return;
            source.volume = level;
            if (level <= 0f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            if (!source.isPlaying && source.clip != null) source.Play();
        }
    }
}
