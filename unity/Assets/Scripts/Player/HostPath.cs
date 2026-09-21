using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>鍵打ち一つ。at 秒にここにいて、こちらを向いて、この高さで見ている</summary>
    [Serializable]
    public struct HostKey
    {
        public float at;
        public Vector3 position;
        public float yaw;
        public float pitch;
        public float eyeHeight;
    }

    /// <summary>
    /// 主の体の道筋。鍵打ちの間を SmoothStep で繋ぐ。
    /// 記憶の速さ（DiveEntry.speed）は時刻の側に掛けるので、ここは倍率を知らない
    /// </summary>
    public static class HostPath
    {
        public static HostKey At(HostKey[] keys, float t)
        {
            if (keys == null || keys.Length == 0) return new HostKey();
            if (t <= keys[0].at) return keys[0];
            for (var i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].at) continue;
                var a = keys[i - 1];
                var b = keys[i];
                var span = b.at - a.at;
                var k = span <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (t - a.at) / span);
                return new HostKey
                {
                    at = t,
                    position = Vector3.Lerp(a.position, b.position, k),
                    yaw = Mathf.LerpAngle(a.yaw, b.yaw, k),
                    pitch = Mathf.Lerp(a.pitch, b.pitch, k),
                    eyeHeight = Mathf.Lerp(a.eyeHeight, b.eyeHeight, k),
                };
            }
            return keys[keys.Length - 1];
        }

        public static float Length(HostKey[] keys)
        {
            return keys == null || keys.Length == 0 ? 0f : keys[keys.Length - 1].at;
        }
    }
}
