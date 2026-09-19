using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 道と沿道を流す。車は原点に置いたままで、世界の方を手前へ送る。
    ///
    /// タイルは組み立てのときに作って渡してもらう。ここでは位置だけ動かす。
    /// 帯を跨ぐときは Dress を呼んで、沿道の並びを今の帯のものに入れ替える
    /// </summary>
    public sealed class DriveWorld : MonoBehaviour
    {
        [Tooltip("道のタイル。環にして流す。i 番目が環の i 番目の枠に入るので、並べ替えると沿道の出方が変わる")]
        [SerializeField] Transform[] tiles = new Transform[0];
        [Tooltip("タイル 1 枚の長さ。m")]
        [SerializeField] float tileLength = 20f;
        [Tooltip("車の後ろのどこまで残すか。負の値")]
        [SerializeField] float behind = -30f;
        [Tooltip("帯ごとの沿道。今の帯のものだけ出す")]
        [SerializeField] Transform[] roadsides = new Transform[0];

        /// <summary>走る速さ。m/s。0 で止まる</summary>
        public float Speed { get; set; }

        /// <summary>走り出したか。乗り込むまでは動かさない</summary>
        public bool Rolling { get; set; }

        /// <summary>走った距離。揺れと音がこれを見る</summary>
        public float Travelled { get; private set; }

        void Update()
        {
            if (!Rolling) return;
            Travelled += Speed * Time.deltaTime;
            Place();
        }

        /// <summary>今の走行距離でタイルを並べ直す</summary>
        public void Place()
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                var z = RoadRing.Slot(i, tiles.Length, tileLength, Travelled, behind);
                var at = tiles[i].localPosition;
                tiles[i].localPosition = new Vector3(at.x, at.y, z);
            }
        }

        /// <summary>which 番目の帯の沿道だけ出す。-1 でどれも出さない</summary>
        public void Dress(int which)
        {
            for (var i = 0; i < roadsides.Length; i++)
                if (roadsides[i] != null) roadsides[i].gameObject.SetActive(i == which);
        }

        /// <summary>頭から走り直す。帯を跨ぐときに距離を戻すと、沿道の並びも頭から出る</summary>
        public void Rewind()
        {
            Travelled = 0f;
            Place();
        }
    }
}
