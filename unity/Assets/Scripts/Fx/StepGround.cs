using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 足音の地面。歩ける床の当たりに付けて、その上で鳴らす足音を決める。
    /// <see cref="Footsteps"/> が一歩ごとに足元の当たりを見て、これが付いていればその音で鳴らす。
    ///
    /// **床の当たりは素材ごとの一枚ではない。** 村の片割れの敷地は芝も煉瓦の小路もテラスの敷石も
    /// 一枚の当たり（VillagePlot）で、見た目の煉瓦と敷石は当たりを持たない。そこで、当たりの中で
    /// 音の違う所を <see cref="patches"/> に上から見た形（芯の折れ線と半幅の帯か、四角）で持つ。
    /// 形は世界の (x, z)。組み立ての道具が見た目と同じ線から書く。
    ///
    /// 音が空なら Footsteps がいま持っている音（場所の既定の足音）で鳴らす
    /// </summary>
    public sealed class StepGround : MonoBehaviour
    {
        /// <summary>当たりの中で音の違う所。帯（折れ線と半幅）か四角</summary>
        [System.Serializable]
        public struct Patch
        {
            [Tooltip("名前。インスペクターで見分けるだけ")]
            public string name;
            [Tooltip("帯の芯。上から見た世界の (x, z)。点が二つ以上あれば帯として使い、四角は見ない")]
            public Vector2[] line;
            [Tooltip("帯の半幅。m")]
            public float half;
            [Tooltip("四角。上から見た世界の (x, z)。帯の芯が無いときに使う")]
            public Rect box;
            [Tooltip("ここで鳴らす足音")]
            public AudioClip[] clips;
        }

        [Tooltip("この当たりの上で鳴らす足音。空なら Footsteps の既定の音")]
        [SerializeField] AudioClip[] clips = new AudioClip[0];
        [Tooltip("当たりの中で音の違う所。先に書いた物が勝つ")]
        [SerializeField] Patch[] patches = new Patch[0];

        /// <summary>世界の点 at の上で鳴らす足音。空の並びなら既定の音に任せる</summary>
        public AudioClip[] ClipsAt(Vector3 at)
        {
            return Pick(new Vector2(at.x, at.z), clips, patches);
        }

        /// <summary>
        /// 上から見た点 p の足音。p の入っている最初の区画の音、どこにも入っていなければ当たりの音。
        /// 区画の音が空なら当たりの音へ落とす
        /// </summary>
        public static AudioClip[] Pick(Vector2 p, AudioClip[] ground, Patch[] parts)
        {
            if (parts != null)
                foreach (var part in parts)
                {
                    if (!Inside(p, part)) continue;
                    if (part.clips != null && part.clips.Length > 0) return part.clips;
                    break;
                }
            return ground ?? new AudioClip[0];
        }

        /// <summary>p が区画の中か。帯の芯が二点以上なら帯、そうでなければ四角で見る</summary>
        public static bool Inside(Vector2 p, Patch part)
        {
            if (part.line != null && part.line.Length >= 2) return InBand(p, part.line, part.half);
            return part.box.width > 0f && part.box.height > 0f && part.box.Contains(p);
        }

        /// <summary>
        /// p が帯の中か。芯の折れ線のどれかの辺から half 以内。
        /// 両端は芯に直角に切る（丸く膨らませない）。小路の口の先の芝まで硬い音にしないため
        /// </summary>
        public static bool InBand(Vector2 p, Vector2[] line, float half)
        {
            var last = line.Length - 2;
            for (var i = 0; i <= last; i++)
            {
                var a = line[i];
                var d = line[i + 1] - a;
                var len2 = d.sqrMagnitude;
                if (len2 < 1e-8f) continue;
                var t = Vector2.Dot(p - a, d) / len2;
                // 中の継ぎ目は丸めて繋ぐ（曲がりの外に隙間を作らない）。両端だけ直角に切る
                if ((i == 0 && t < 0f) || (i == last && t > 1f)) continue;
                var q = a + d * Mathf.Clamp01(t);
                if ((p - q).sqrMagnitude <= half * half) return true;
            }
            return false;
        }
    }
}
