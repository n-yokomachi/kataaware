using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 麦を微風になびかせる波。
    ///
    /// 麦は区切り 1 つにつき 300 株あまりを 1 枚の mesh に焼いてあるので、株ごとに
    /// Transform を動かす手は取れない。頂点をずらすシェーダー（HalfAware/Wheat）が
    /// 実際に動かしていて、ここはその式と数を C# 側にも置いたもの。
    ///
    /// **二重に持っているのには用がある。** 組み立てはここの数をそのまま
    /// マテリアルへ書き込むので、値の出どころはひとつに保たれる。見直し（CheckDrive）は
    /// 穂先がどれだけ横へ振れるかをここから知って、揺れたときに轍へ倒れ込まないかを見る。
    /// mesh の頂点だけ測っても、なびいた先までは分からない。
    ///
    /// 位相は区切りの中の座標（object 空間）から取る。世界の座標から取ると、
    /// 区切りが手前へ流れるぶんだけ位相が動いて、なびくのではなく細かく震えて見える。
    /// そのかわり波長は区切りの長さ（20 m）を割り切る数でないと、区切りの継ぎ目で波が途切れる
    /// </summary>
    public static class WheatWind
    {
        /// <summary>穂先の振れ幅。m。微風なので、1.3 m の株がおよそ 4 度傾くところに置く</summary>
        public const float Amp = 0.10f;
        /// <summary>細かい震えの割合。Amp に対して</summary>
        public const float Flutter = 0.32f;
        /// <summary>x 方向の波数。rad/m。波長およそ 14.3 m。x は区切りを跨いでも続くので端数でよい</summary>
        public const float Across = 0.44f;
        /// <summary>
        /// z 方向の波数。rad/m。波長ちょうど 10 m。
        /// 区切りの長さ 20 m に 2 波きっかり入るので、継ぎ目で波が途切れない。
        /// 変えるなら 2π/20 の整数倍から選ぶこと
        /// </summary>
        public const float Along = 0.62831853f;
        /// <summary>波の進む速さ。rad/s。一点から見ると 0.35 Hz でひと揺れする</summary>
        public const float Rate = 2.2f;
        /// <summary>細かい震えの速さ。rad/s。0.83 Hz</summary>
        public const float FlutterRate = 5.2f;
        /// <summary>
        /// 重みが 1 に届く高さ。m。株の背は 1.00〜1.50 なので、
        /// 背の低い株はここに届かず、そのぶん揺れが浅くなる。
        /// BuildDrive.Furrows が振る背の上端と揃えること
        /// </summary>
        public const float High = 1.05f;
        /// <summary>z 方向の振れの割合。x より浅くして、穂先が小さな楕円を描くようにする</summary>
        public const float Side = 0.40f;

        /// <summary>
        /// 根からの高さの重み。根は動かさない。
        /// 二乗するのは、真っ直ぐ倒れるのではなく穂先ほど大きく撓ませるため
        /// </summary>
        public static float Weight(float y)
        {
            var w = Mathf.Clamp01(y / High);
            return w * w;
        }

        /// <summary>
        /// その点の横ずれ。x は区切りの中の x、z は区切りの中の z、y は根からの高さ。
        /// HalfAware/Wheat の頂点シェーダーと同じ式
        /// </summary>
        public static Vector2 Offset(float x, float z, float y, float t)
        {
            var phase = x * Across + z * Along;
            var slow = phase + t * Rate;
            var quick = phase * 2f + t * FlutterRate;
            var w = Weight(y);
            return new Vector2(
                (Mathf.Sin(slow) + Mathf.Sin(quick) * Flutter) * Amp * w,
                Mathf.Cos(slow) * Amp * Side * w);
        }

        /// <summary>
        /// 穂先が横（x）へ振れうる最大。m。
        /// 見直しが、麦の立っている位置からこれだけ轍の側へ寄せて見るのに使う
        /// </summary>
        public static float Reach { get { return Amp * (1f + Flutter); } }
    }
}
