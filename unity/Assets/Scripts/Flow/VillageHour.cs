using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 村と庭の場所（<c>Village.unity</c>）の時刻。朝（場面 9・10）と夕方（場面 6）の二つを持ち、
    /// 空・日・霞・環境光を一揃いで差し替える（村と庭の設計書 1 節）。
    ///
    /// 値の中身は場面 4 と同じ <see cref="PlaceSky"/>。時刻ごとの灯り（日と、日の反対の空の青）は
    /// 時刻の名の子にまとめてあり、選ばなかった方の子を切る。
    ///
    /// エディタでは Inspector の <see cref="hour"/> を替えるか、メニューの
    /// <c>HalfAware/Village: morning</c>・<c>HalfAware/Village: evening</c> で切り替えて見る。
    /// 再生したときは始まりに一度 <see cref="Apply"/> する。場面の出来事が時刻を決めるようになったら、
    /// そちらから <see cref="Set"/> を呼ぶ
    /// </summary>
    public sealed class VillageHour : MonoBehaviour
    {
        public enum Hour
        {
            /// <summary>8 月中旬の 07:30 ほど。日は東北東の 15 度。薄い朝靄</summary>
            Morning,
            /// <summary>8 月中旬の 20:15 ほど。日は西北西の 5 度。暖かい色</summary>
            Evening,
        }

        [Tooltip("いまの時刻。替えるとその場で空と日が差し替わる")]
        [SerializeField] Hour hour = Hour.Morning;
        [Tooltip("朝の空・霞・環境光・日")]
        [SerializeField] PlaceSky morning;
        [Tooltip("夕方の空・霞・環境光・日")]
        [SerializeField] PlaceSky evening;
        [Tooltip("朝の灯りをまとめた子。夕方のあいだは切る")]
        [SerializeField] GameObject morningLights;
        [Tooltip("夕方の灯りをまとめた子。朝のあいだは切る")]
        [SerializeField] GameObject eveningLights;
        [Tooltip("空の塗り方を合わせるカメラ。空なら Camera.main")]
        [SerializeField] Camera eye;

        public Hour Current { get { return hour; } }

        public void Set(Hour next)
        {
            hour = next;
            Apply();
        }

        /// <summary>いまの時刻の灯りだけを点け、空・霞・環境光をその時刻のものにする</summary>
        public void Apply()
        {
            var morningNow = hour == Hour.Morning;
            if (morningLights != null) morningLights.SetActive(morningNow);
            if (eveningLights != null) eveningLights.SetActive(!morningNow);
            var cam = eye != null ? eye : Camera.main;
            (morningNow ? morning : evening).Apply(cam);
        }

        void Awake()
        {
            if (Application.isPlaying) Apply();
        }

#if UNITY_EDITOR
        // Inspector で時刻を替えたとき。OnValidate の中で子の有効を切り替えると Unity が警告を出すので、一拍おく
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Apply();
            };
        }
#endif
    }
}
