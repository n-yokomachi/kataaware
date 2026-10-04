// 画面のフィルターの「減色＋ディザ」（ScreenFilter、設計書 2.5 節）。Ps1 のパスと、タイトルの画面の背景（FilteredPicture）が使う。
//
// 色を色の組（52 色。BuildScreenFilter.Palette）の中のいちばん近い二色に分け、どちらを出すかを点の模様の閾値で決める。
// 近さは OKLab で測る（明るさの差と色の差が、見た目の差に近い重さで並ぶ）。二色を結ぶ線へ色を落とした位置 t を、
// 閾値（0〜1）と比べ、閾値が t より小さい画素だけ二色めを出す。広い面で見ると、二色が t の割合で混ざって元の色に近づく。
//
// 近い二色は前もって引いておく（_HaLut）。sRGB の各色を 64 段に分けた升（64³）ごとに、升の真ん中の色に近い二色の番号を入れてある。
// 画素ごとに 52 色と比べると、標準の型の 7 倍ほど重かった（2026-10-05、エディタの RTX 4070 で 1280×720 に一回 0.19 ms。標準は 0.03 ms、表を引くと 0.03 ms）。
// t は表に入れず、画素の色そのものと二色の OKLab から出す（升の大きさで段が付かない）。
//
// 点の模様は 64×64 の青色雑音（_HaNoise）。画素の座標で引くので、カメラが動いても点は画面に貼り付いて止まって見える。
// テクスチャは添え字で直に読む（Load。WebGL2 では texelFetch）。サンプラーを通さないので、どちらのシェーダーの書き方（URP の HLSL と CG）からも同じに読める
#ifndef HALFAWARE_SCREEN_FILTER
#define HALFAWARE_SCREEN_FILTER

// 点の模様の一辺の画素数。BuildScreenFilter.NoiseSize と揃える
#define HA_NOISE_SIZE 64
// 近い二色の表の、色ごとの段の数。BuildScreenFilter.LutSteps と揃える。表は 512×512 に、青の段を 8×8 に並べて置く
#define HA_LUT_STEPS 64
#define HA_LUT_TILES 8

// 点の強さ。1 で閾値を 0〜1 に広く散らし、0 で点を打たない（いちばん近い一色に塗るだけ）。
// 0.5 ほどまで下げると、二色の境が筋に戻ってくる
#define HA_DOT_STRENGTH 1.0

// 青色雑音 64×64（BlueNoise64.png）
Texture2D _HaNoise;
// 近い二色の表（ScreenFilterLut.png）。r が一番めの番号、g が二番めの番号（0〜255 の値をそのまま）
Texture2D _HaLut;
// 色の組（ScreenFilterPalette.asset、64×2 の半精度）。行 0 が OKLab、行 1 が出す色（リニアの RGB）
Texture2D _HaPalette;

// リニアの RGB を OKLab へ（Björn Ottosson の式。BuildScreenFilter.OkLab と同じ）
float3 HaOkLab(float3 c)
{
    float3 lms = float3(
        0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b,
        0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b,
        0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b);
    lms = pow(max(lms, 0.0), 1.0 / 3.0);
    return float3(
        0.2104542553 * lms.x + 0.7936177850 * lms.y - 0.0040720468 * lms.z,
        1.9779984951 * lms.x - 2.4285922050 * lms.y + 0.4505937099 * lms.z,
        0.0259040371 * lms.x + 0.7827717662 * lms.y - 0.8086757660 * lms.z);
}

// リニアを sRGB へ（表の升を引くため）
float3 HaEncode(float3 c)
{
    float3 low = c * 12.92;
    float3 high = 1.055 * pow(max(c, 0.0031308), 1.0 / 2.4) - 0.055;
    return float3(c.r <= 0.0031308 ? low.r : high.r, c.g <= 0.0031308 ? low.g : high.g, c.b <= 0.0031308 ? low.b : high.b);
}

// 点の模様の値（0〜1、絵の 8 bit の値）を閾値へ。256 段の真ん中に置き、強さで 0.5 の周りへ縮める
float HaThreshold(float noise)
{
    float t = (noise * 255.0 + 0.5) / 256.0;
    return lerp(0.5, t, HA_DOT_STRENGTH);
}

// 色（リニアの RGB）を、色の組の二色と、画素 pixel の点の閾値で一色にする。返すのもリニアの RGB
float3 HaDither(float3 linearRgb, uint2 pixel)
{
    float3 c = saturate(linearRgb);
    uint3 cell = (uint3)(HaEncode(c) * (HA_LUT_STEPS - 1) + 0.5);
    uint2 at = uint2(cell.r + (cell.b % HA_LUT_TILES) * HA_LUT_STEPS, cell.g + (cell.b / HA_LUT_TILES) * HA_LUT_STEPS);
    float2 pair = _HaLut.Load(int3(at, 0)).rg * 255.0 + 0.5;
    int i1 = (int)pair.x;
    int i2 = (int)pair.y;
    float3 lab1 = _HaPalette.Load(int3(i1, 0, 0)).xyz;
    float3 lab2 = _HaPalette.Load(int3(i2, 0, 0)).xyz;
    float3 span = lab2 - lab1;
    float t = saturate(dot(HaOkLab(c) - lab1, span) / max(dot(span, span), 1e-8));
    float threshold = HaThreshold(_HaNoise.Load(int3(pixel % HA_NOISE_SIZE, 0)).r);
    return _HaPalette.Load(int3(threshold < t ? i2 : i1, 1, 0)).rgb;
}

#endif
