using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 道と沿道を流す。車は原点に置いたままで、世界の方を手前へ送る。
    ///
    /// タイルは組み立てのときに作って渡してもらう。ここでは位置だけ動かす。
    /// 沿道はタイルの子にはせず、帯ごとの入れ物に分けて持つ。
    /// 子にすると帯の出し分けがタイルの数だけ増えるうえ、
    /// 入れ物を切り替えるだけでは済まなくなる。
    /// 入れ物の中身を道と同じ環に乗せれば、継ぎ目は勝手に揃う
    /// </summary>
    public sealed class DriveWorld : MonoBehaviour
    {
        [Tooltip("道のタイル。環にして流す。i 番目が環の i 番目の枠に入るので、並べ替えると道の出方が変わる")]
        [SerializeField] Transform[] tiles = new Transform[0];
        [Tooltip("タイル 1 枚の長さ。m。mesh の z 方向の長さとぴったり同じにする。ずれると継ぎ目に隙間が開く")]
        [SerializeField] float tileLength = 20f;
        [Tooltip("車の後ろのどこまで残すか。負の値")]
        [SerializeField] float behind = -30f;
        [Tooltip("帯ごとの沿道。今の帯のものだけ出す。中身はタイルと同じ枚数に割って並べる")]
        [SerializeField] Transform[] roadsides = new Transform[0];

        /// <summary>走る速さ。m/s。0 で止まる</summary>
        public float Speed { get; set; }

        /// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。車体の揺れ幅に掛かる</summary>
        public float Rough { get; set; }

        /// <summary>走り出したか。乗り込むまでは動かさない</summary>
        public bool Rolling { get; set; }

        /// <summary>今の帯に入ってから走った距離。帯を跨ぐと 0 に戻る。揺れがこれを位相に使う</summary>
        public float Travelled { get; private set; }

        /// <summary>今出している沿道。道と同じ環に乗せるので Place が面倒を見る</summary>
        Transform dressed;

        void Update()
        {
            if (!Rolling) return;
            Travelled += Speed * Time.deltaTime;
            Place();
        }

        /// <summary>今の走行距離で、道と沿道を並べ直す</summary>
        public void Place()
        {
            for (var i = 0; i < tiles.Length; i++) Slide(tiles[i], i, tiles.Length);
            // 沿道は出ている帯のぶんだけ。道と同じ環に乗せるので、道との継ぎ目がずれない
            if (dressed == null) return;
            var n = dressed.childCount;
            for (var i = 0; i < n; i++) Slide(dressed.GetChild(i), i, n);
        }

        /// <summary>
        /// 環の i 番目の枠へ置く。x と y は組み立てのときのまま残すので、
        /// 車線のずらしや路面の反りは組み立て側で決められる
        /// </summary>
        void Slide(Transform what, int i, int n)
        {
            if (what == null) return;
            var z = RoadRing.Slot(i, n, tileLength, Travelled, behind);
            var at = what.localPosition;
            what.localPosition = new Vector3(at.x, at.y, z);
        }

        /// <summary>which 番目の帯の沿道だけ出す。-1 でどれも出さない</summary>
        public void Dress(int which)
        {
            dressed = null;
            for (var i = 0; i < roadsides.Length; i++)
            {
                if (roadsides[i] == null) continue;
                var on = i == which;
                roadsides[i].gameObject.SetActive(on);
                if (on) dressed = roadsides[i];
            }
        }

        /// <summary>
        /// 頭から走り直す。帯を跨ぐときに距離を戻すと、沿道の並びも頭から出る。
        /// 明けたときの見え方が毎回同じになるので、オーナーが目で決めた画が再現する
        /// </summary>
        public void Rewind()
        {
            Travelled = 0f;
            Place();
        }
    }
}
