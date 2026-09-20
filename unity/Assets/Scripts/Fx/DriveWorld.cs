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
    /// 入れ物の中身を道と同じ環に乗せれば、継ぎ目は勝手に揃う。
    ///
    /// 対向車だけは同じ環に乗せない。すれ違う車は自分の速さと相手の速さの和で
    /// 近づいてくるので、道と同じ速さで流すと隣を並んで走っているように見える。
    ///
    /// 車体の揺れもここが出す。走った距離を持っているのがここだけで、
    /// 揺れの位相はその距離から取るため。場面 8 では EyeSway を付けないので、
    /// PlayerController.EyeOffset を書くのはここひとつだけになる。
    ///
    /// 実行順を -20 に置くのは、PlayerController（-10）がそのフレームの EyeOffset を
    /// 読む前に書き終えるため。既定の 0 のままだと揺れが 1 フレーム遅れる。
    /// EyeSway と同じ順で、DriveDirector（-5）より先に走る
    /// </summary>
    [DefaultExecutionOrder(-20)]
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
        [Tooltip("対向車。帯ごとの入れ物。中身は沿道と同じく区切りに割る")]
        [SerializeField] Transform[] oncoming = new Transform[0];
        [Tooltip("対向車が流れる速さ。道の何倍か。1 だと並んで走っているように見える。" +
            "走った距離に掛けるので、対向車の速さはこちらの速さに連れて変わる。" +
            "対向車を出すのが速さの変わらない帯 1 だけのうちは構わないが、ほかの帯にも出すなら見直す")]
        [SerializeField] float oncomingRate = 2.2f;

        [Header("揺れ")]
        [Tooltip("揺れの幅。m。舗装はごく小さく、未舗装は粗く。組み直すと BuildDrive.Shake に戻る")]
        [SerializeField] float shake = 0.004f;
        [Tooltip("揺れの速さ。走った距離に掛ける。1.0 で基本の波長がおよそ 6.3 m。" +
            "組み直すと BuildDrive.ShakeRate に戻る")]
        [SerializeField] float shakeRate = 1.0f;
        [Tooltip("止まったままの震えが位相を進める速さ。m/s 相当。" +
            "揺れの位相は本来なら走った距離から取るが、止まっているあいだは距離が進まない。" +
            "組み直すと BuildDrive.IdleRate に戻る")]
        [SerializeField] float idleRate = 7f;
        [Tooltip("ずれを渡す先")]
        [SerializeField] PlayerController player;

        /// <summary>走る速さ。m/s。0 で止まる</summary>
        public float Speed { get; set; }

        /// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。車体の揺れ幅に掛かる</summary>
        public float Rough { get; set; }

        /// <summary>走り出したか。乗り込むまでは動かさない</summary>
        public bool Rolling { get; set; }

        /// <summary>
        /// 止まったままエンジンだけ掛かっている震え。<see cref="Rough"/> と同じ扱いで、0 で震えない。
        ///
        /// 走り出したら見ない。走っているあいだの揺れは路面が決めるもので、
        /// そこへエンジンの震えを足すと二重に揺れる
        /// </summary>
        public float Idling { get; set; }

        /// <summary>今の帯に入ってから走った距離。帯を跨ぐと 0 に戻る。揺れがこれを位相に使う</summary>
        public float Travelled { get; private set; }

        /// <summary>今出している沿道。道と同じ環に乗せるので Place が面倒を見る</summary>
        Transform dressed;

        /// <summary>今出している対向車。道より速い環に乗せる</summary>
        Transform rushing;

        readonly RoadShake bump = new RoadShake();

        /// <summary>止まったままの震えの位相。時間で進める。走っているあいだは触らない</summary>
        float idlePhase;

        void Awake()
        {
            if (player == null) Debug.LogError("DriveWorld: player が未接続。揺れを渡せない", this);
        }

        void Update()
        {
            if (Rolling)
            {
                Travelled += Speed * Time.deltaTime;
                Place();
            }
            else if (Idling > 0f)
            {
                // **位相を時間で進める。** RoadShake は走った距離を位相に使うが、
                // 止まっているあいだ距離は進まない。そのまま渡すと震えが凍りつく
                idlePhase += idleRate * Time.deltaTime;
            }
            Shake();
        }

        /// <summary>
        /// 目の位置のずれを渡す。自分で eye.localPosition を書かないのは、
        /// PlayerController が毎フレームそこを書き直しているため。直に触ると
        /// 上書きされるか、こちらが勝った場合は EyeHeight を初回の値で固めてしまう
        /// （<see cref="EyeSway"/> の説明文と同じ理由）。
        ///
        /// 走っていない間は粗さ 0 で渡す。ガレージを歩いているあいだ、
        /// 止まっている車の揺れを目に足さない
        /// </summary>
        void Shake()
        {
            if (player == null) return;
            if (Rolling) bump.Tick(Travelled, Rough, shake, shakeRate);
            else bump.Tick(idlePhase, Idling, shake, shakeRate);
            player.EyeOffset = bump.Offset;
        }

        /// <summary>今の走行距離で、道と沿道と対向車を並べ直す</summary>
        public void Place()
        {
            for (var i = 0; i < tiles.Length; i++) Slide(tiles[i], i, tiles.Length, Travelled);
            // 沿道は出ている帯のぶんだけ。道と同じ環に乗せるので、道との継ぎ目がずれない
            if (dressed != null)
            {
                var n = dressed.childCount;
                for (var i = 0; i < n; i++) Slide(dressed.GetChild(i), i, n, Travelled);
            }
            // 対向車だけは別の環。速く流さないと、並んで走っているように見える
            if (rushing != null)
            {
                var m = rushing.childCount;
                for (var i = 0; i < m; i++) Slide(rushing.GetChild(i), i, m, Travelled * oncomingRate);
            }
        }

        /// <summary>
        /// 環の i 番目の枠へ置く。x と y は組み立てのときのまま残すので、
        /// 車線のずらしや路面の反りは組み立て側で決められる。
        /// 距離を外から渡すのは、対向車だけ別の速さで流すため
        /// </summary>
        void Slide(Transform what, int i, int n, float travelled)
        {
            if (what == null) return;
            var z = RoadRing.Slot(i, n, tileLength, travelled, behind);
            var at = what.localPosition;
            what.localPosition = new Vector3(at.x, at.y, z);
        }

        /// <summary>which 番目の帯の沿道と対向車だけ出す。-1 でどれも出さない</summary>
        public void Dress(int which)
        {
            dressed = Only(roadsides, which);
            rushing = Only(oncoming, which);
        }

        /// <summary>which 番目だけ出して、それを返す。-1 でどれも出さない</summary>
        static Transform Only(Transform[] row, int which)
        {
            Transform lit = null;
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                var on = i == which;
                row[i].gameObject.SetActive(on);
                if (on) lit = row[i];
            }
            return lit;
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
