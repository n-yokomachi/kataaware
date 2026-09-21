using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶一つ分のシーン側。子に人を持ち、主の体の鍵打ちと呼ぶ声を持つ。
    ///
    /// 場所はここに入れない。同じ場所を別の体で見る対（団地・公園・電車・台所・教室）が
    /// あるので、場所は Places の側に一つだけ置いて使い回す。
    ///
    /// 人の名前は <see cref="Seen.name"/> と揃える。綴りが食い違うと板が出ない
    /// </summary>
    public sealed class Take : MonoBehaviour
    {
        [Tooltip("一覧での番号（0 始まり）")]
        [SerializeField] int entry;
        [Tooltip("主の体。at 秒の並びで持つ")]
        [SerializeField] HostKey[] keys = new HostKey[0];
        [Tooltip("名前を呼ぶ声。無ければ黙って進む")]
        [SerializeField] AudioClip call;
        [Tooltip("板の出る相手。名前が DiveEntry の Seen.name と同じ")]
        [SerializeField] Transform[] people = new Transform[0];

        public int Entry { get { return entry; } }

        public HostKey[] Keys { get { return keys; } }

        public AudioClip Call { get { return call; } }

        public Transform[] People { get { return people; } }

        /// <summary>鍵打ちの最後の at。DiveEntry.length と揃っているかを組み立てが見直す</summary>
        public float Length { get { return HostPath.Length(keys); } }

        /// <summary>名前で引く。見つからなければ null</summary>
        public Transform Person(string name)
        {
            if (people == null || string.IsNullOrEmpty(name)) return null;
            for (var i = 0; i < people.Length; i++)
            {
                if (people[i] != null && people[i].name == name) return people[i];
            }
            return null;
        }

        /// <summary>記憶の頭から t 秒の主の体</summary>
        public HostKey At(float t)
        {
            return HostPath.At(keys, t);
        }
    }
}
