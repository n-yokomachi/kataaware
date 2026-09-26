using System;
using System.Runtime.InteropServices;

namespace HalfAware
{
    /// <summary>
    /// その土地の時刻。セーブの行に出す「書いた日時」に使う。
    ///
    /// WebGL の書き出しでは、C# の側が時差を知らない（<see cref="DateTime.Now"/> が UTC のまま返る）。
    /// そこだけブラウザに時差を訊く（<c>Plugins/WebGL/LocalClock.jslib</c>）。エディタと他の書き出しは C# の時差をそのまま使う
    /// </summary>
    public static class LocalClock
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>ブラウザの時差。分。UTC から引く向き（日本なら -540）</summary>
        [DllImport("__Internal")]
        static extern int HalfAwareTimezoneOffset();
#endif

        /// <summary>UTC の時刻を、その土地の時刻へ</summary>
        public static DateTime ToLocal(DateTime utc)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return DateTime.SpecifyKind(utc.AddMinutes(-HalfAwareTimezoneOffset()), DateTimeKind.Unspecified);
#else
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
#endif
        }
    }
}
