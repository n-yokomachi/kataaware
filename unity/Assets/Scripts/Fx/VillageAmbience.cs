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
    /// **朝の麦の風は、路地を村の中へ歩くと薄れて消える**（オーナー、2026-09-27。同日「もう少し手前から」で前倒し）。
    /// 車を降りた所は麦畑の間の未舗装路で風が鳴っているが、歩き出してすぐ薄れ始め、いちばん手前の家（家 C）に
    /// たどり着く頃には消えきる。家並みの中では聞こえない。
    /// 立ち位置（東西の x）だけで決めるので、西へ戻れば、また聞こえる（<see cref="Reach"/>）。
    /// 朝の村の鳥の声は残す。夕方（場面 6 の庭）の風は立ち位置によらず今のまま。
    ///
    /// 大きさと薄れ始め・消えきる位置はインスペクターで変える。オーナーが耳で決めるので、組み直しても書き戻さない
    /// （<c>BuildVillage.Rig</c> が前の値を引き継ぐ）
    /// </summary>
    public sealed class VillageAmbience : MonoBehaviour
    {
        public const float DefaultMorningVillage = 0.4f;
        public const float DefaultMorningWheat = 0.5f;
        public const float DefaultEveningWheat = 0.6f;
        /// <summary>
        /// 朝の麦の風が薄れ始める x（オーナー、2026-09-27「もう少し手前からフェードアウトするように」で前より西へ動かした）。
        /// 車を降りた所（<c>BuildVillage.ArriveAt.x</c> -76.3）から 6 m ほど、農場の門（車の着く所の側、x -73.6〜-70.2）を
        /// 過ぎたあたり
        /// </summary>
        public const float DefaultWheatFadeFrom = -70f;
        /// <summary>
        /// 朝の麦の風が消えきる x。いちばん手前の家（家 C、路地の南の x -52.6〜-42.4）の西の端。
        /// ここまでに消えているので、家 C にたどり着く頃には聞こえない（オーナーの言葉どおり）。薄れ始めから 17 m、歩いて 12 秒ほど
        /// </summary>
        public const float DefaultWheatFadeTo = -52.6f;

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

        [Header("朝の麦の風を、路地の途中から薄くする")]
        [Tooltip("薄れ始める東西の位置（世界の x）。ここより西（車の着く所の側）は朝の麦の風の大きさのまま")]
        [SerializeField] float wheatFadeFrom = DefaultWheatFadeFrom;
        [Tooltip("消えきる東西の位置（世界の x）。ここより東（家並みと片割れの家の側）では朝の麦の風を鳴らさない")]
        [SerializeField] float wheatFadeTo = DefaultWheatFadeTo;

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

        /// <summary>
        /// 立ち位置 x での朝の麦の風の残り。from より西は 1、to より東は 0、その間はなめらかに（smoothstep）下がる。
        /// 位置だけで決まるので、戻れば同じ大きさに戻る。from と to が逆でも、同じ向き（西が 1）で読む
        /// </summary>
        public static float Reach(float x, float from, float to)
        {
            var a = Mathf.Min(from, to);
            var b = Mathf.Max(from, to);
            if (b - a < 1e-4f) return x < a ? 1f : 0f;
            var t = Mathf.Clamp01((x - a) / (b - a));
            return 1f - t * t * (3f - 2f * t);
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
            // 朝の麦の風は立ち位置で薄くする。耳は Player の頭上にあるので、その x で見る。
            // 寄せ（follow）の前に掛けるので、時刻の入れ替えと同じ速さで追う。歩く速さでは遅れない
            if (h == VillageHour.Hour.Morning) w *= Reach(transform.position.x, wheatFadeFrom, wheatFadeTo);
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
