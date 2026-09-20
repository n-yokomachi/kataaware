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
        /// <summary>
        /// 穂先の振れ幅。m。微風なので、1.18 m の株がおよそ 8 度傾くところに置く。
        /// 0.14 から上げた。低い描画解像度では 0.14 の傾きが 1 画素に届かず、
        /// 手前の株まで立ったまま止まって見えていた
        /// </summary>
        public const float Amp = 0.17f;
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
        /// 重みが 1 に届く高さ。m。株の背は 0.72〜1.18 なので、
        /// 背の低い株はここに届かず、そのぶん揺れが浅くなる。
        /// BuildDrive.Furrows が振る背の上端と揃えること
        /// </summary>
        public const float High = 1.18f;
        /// <summary>z 方向の振れの割合。x より浅くして、穂先が小さな楕円を描くようにする</summary>
        public const float Side = 0.40f;

        // ---- 風のむら ------------------------------------------------------
        //
        // 上の波だけだと、畑ぜんたいが同じ強さで一様に揺れる。オーナーの言う
        // 「ある程度の塊で横になびかせる」には、もっと長い波で振れ幅そのものを
        // 撫でてやる必要がある。振れ幅に (1 + Gust·Gusting) を掛けるので、
        // Reach もそのぶん広がる。
        //
        // **むらは二つの波を足して作る。** 一つだけでは波長が 19 m までしか伸びない。
        // z 方向の波長は区切りの長さ（20 m）を割り切る数でなければならず、
        // いちばん長く取っても 20 m だからで、そこに x の波数が加わるともっと短くなる。
        // 19 m の塊は、走っている車から 40 m 先を見たときに画面の 1/5 ほどしか無い。
        // それだけだと畑が細かく波立つばかりで、風が渡っていくようには見えない。
        //
        // 二つめは z を持たない波（<see cref="RollAcross"/>）で、道と平行な帯が
        // 畑を横切って外へ渡っていく。z を持たないので継ぎ目の心配が要らず、
        // 波長をいくらでも長く取れる。**風が渡っているように見えるのはこちら。**
        // 一つめの斜めの波が、その帯を崩して縞に見せない役をする

        /// <summary>風のむらの深さ。振れ幅に対する増減</summary>
        public const float Gust = 0.45f;
        /// <summary>むらの x 方向の波数。rad/m。z の波数と合わせて波長およそ 19 m</summary>
        public const float GustAcross = 0.11f;
        /// <summary>
        /// むらの z 方向の波数。rad/m。波長ちょうど 20 m。
        /// 区切りの長さと同じなので、継ぎ目でむらが途切れない
        /// </summary>
        public const float GustAlong = 0.31415927f;
        /// <summary>むらの進む速さ。rad/s。0.14 Hz。畑を撫でていくのが見える遅さ</summary>
        public const float GustRate = 0.85f;

        /// <summary>斜めの短い波の取り分。<see cref="Roll"/> と足して 1 にすること</summary>
        public const float Slant = 0.60f;
        /// <summary>道と平行な長い波の取り分。<see cref="Slant"/> と足して 1 にすること</summary>
        public const float Roll = 0.40f;
        /// <summary>
        /// 長い波の x 方向の波数。<see cref="GustAcross"/> に対する割合。
        /// 0.11 × 0.80 ＝ 0.088 rad/m で、波長はおよそ 71 m。
        /// 道の片側だけで帯が二本ほど見えるところに置いてある
        /// </summary>
        public const float RollAcross = 0.80f;
        /// <summary>
        /// 長い波の進む速さ。<see cref="GustRate"/> に対する割合。符号は外向き。
        /// 0.85 × 0.45 ＝ 0.38 rad/s。波長 71 m と合わせて 4.3 m/s で、
        /// 畑を渡っていくのが目で追える速さ
        /// </summary>
        public const float RollRate = -0.45f;
        /// <summary>
        /// むらが明るさを動かす深さ。
        ///
        /// **これは揺れではないので <see cref="Reach"/> には効かない。** 麦が風に倒れると
        /// 日の当たる面の向きが変わり、畑の上を明暗の帯が渡っていく。頂点を動かすだけでは
        /// その帯が出ず、遠い畑は風が吹いていないように見える。
        /// 数をここに置いてあるのは、シェーダーと揃っていることを一箇所で読めるようにするため。
        ///
        /// 0.12 から上げた。**足りなかったのではなく、見えていなかった。**
        /// 株の絵の明暗がそのまま 0.2 倍〜2.0 倍まで振れていた頃は、画面の隣り合う画素が
        /// それより桁違いに大きく振れていて、風の帯は砂嵐に埋もれていた。
        /// 絵の明暗を遠くで均した（Wheat.shader の _DetailFade）ので、
        /// ここを上げたぶんがそのまま畑を渡る帯として出る
        /// </summary>
        public const float Shade = 0.22f;

        /// <summary>
        /// その点の風のむら。-1〜1。
        /// 斜めの短い波と、道と平行な長い波の足し合わせ。
        /// 取り分を足して 1 にしてあるので、振れ幅は 1 を越えない（<see cref="Reach"/>）
        /// </summary>
        public static float Gusting(float x, float z, float t)
        {
            return Slant * Mathf.Sin(x * GustAcross + z * GustAlong + t * GustRate)
                + Roll * Mathf.Sin(x * GustAcross * RollAcross + t * GustRate * RollRate);
        }

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
            // むらは振れ幅そのものに掛ける。別の揺れとして足すと、
            // 塊でなびくのではなく二つの波が重なって細かく震えて見える
            var amp = Amp * (1f + Gust * Gusting(x, z, t));
            return new Vector2(
                (Mathf.Sin(slow) + Mathf.Sin(quick) * Flutter) * amp * w,
                Mathf.Cos(slow) * amp * Side * w);
        }

        /// <summary>
        /// 穂先が横（x）へ振れうる最大。m。
        /// 見直しが、麦の立っている位置からこれだけ轍の側へ寄せて見るのに使う。
        /// むら（<see cref="Gust"/>）が振れ幅に掛かるので、そのぶんも含める
        /// </summary>
        public static float Reach { get { return Amp * (1f + Flutter) * (1f + Gust); } }
    }
}
