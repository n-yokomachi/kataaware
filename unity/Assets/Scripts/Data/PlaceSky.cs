using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware
{
    /// <summary>
    /// 場所ごとの空・霞・環境光・日。記憶が切り替わるたびに <see cref="DiveDirector"/> が
    /// <see cref="Apply"/> でその場所の分へ差し替える（設計書 9.1 節「空と光」）。
    ///
    /// **空・霞・環境光・日を一揃いで持つ。** どれか一つだけ差し替えると、前の場所の霞が
    /// 次の場所に残ったり、空の絵の日と影を落とす日が別々の向きを指したりする。
    ///
    /// 空の絵（<see cref="skybox"/>）を持たない場所は、カメラを <see cref="flat"/> の一色で塗る。
    /// 団地と公園の他の三つは、作り込むまでこちらのまま置く。
    ///
    /// **霞は ExponentialSquared に固定する。** 組み立ての一覧に入っている場面 2 と場面 8 が
    /// この形の霧を使っていて、書き出しのときに霧のシェーダの型が残るのはこの形だけになる。
    /// 別の形にすると、エディタでは掛かって WebGL では掛からない
    /// </summary>
    [Serializable]
    public struct PlaceSky
    {
        [Tooltip("空の絵（Skybox のマテリアル）。空ならカメラを flat の一色で塗る")]
        public Material skybox;
        [Tooltip("空の絵が無いときの塗り潰しの色。開口の向こうと、見上げた先に出る")]
        public Color flat;
        [Tooltip("霞を掛けるか")]
        public bool haze;
        [Tooltip("霞の色。空の地平の色と揃える。揃っていないと、遠い棟と空の境目が浮く")]
        public Color hazeColor;
        [Tooltip("霞の濃さ。ExponentialSquared の density。d m 先で 1 - exp(-(density·d)²) だけ霞む")]
        public float hazeDensity;
        [Tooltip("環境光の上。日の当たらない面が空から受ける色")]
        public Color ambientSky;
        [Tooltip("環境光の横")]
        public Color ambientEquator;
        [Tooltip("環境光の下。地面からの照り返し")]
        public Color ambientGround;
        [Tooltip("影を落とす日。空の絵の日と同じ向きを指す灯り。無ければ一番明るい Directional が使われる")]
        public Light sun;

        /// <summary>
        /// 霞も空の絵も持たない、一色の空。環境光は三色
        /// </summary>
        public static PlaceSky Plain(Color flat, Color ambientSky, Color ambientEquator, Color ambientGround)
        {
            return new PlaceSky
            {
                flat = flat,
                ambientSky = ambientSky,
                ambientEquator = ambientEquator,
                ambientGround = ambientGround,
            };
        }

        /// <summary>
        /// d m 先がどれだけ霞むか。0 で素のまま、1 で霞の色そのもの。
        /// URP の ExponentialSquared と同じ式
        /// </summary>
        public static float Haze(float density, float distance)
        {
            var k = density * distance;
            return 1f - Mathf.Exp(-k * k);
        }

        /// <summary>
        /// いまの設定へ差し替える。<paramref name="eye"/> は塗り潰しを持つカメラ。null でもよい。
        ///
        /// **環境光は Trilight の三色で渡す。** 空の絵から環境光を取る形（Skybox）にすると、
        /// 差し替えのたびに <c>DynamicGI.UpdateEnvironment</c> を回さないと前の場所の光が残る。
        /// 三色なら代入した時点で環境の光が入れ替わる
        /// </summary>
        public void Apply(Camera eye)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.fog = haze;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = hazeColor;
            RenderSettings.fogDensity = hazeDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.sun = sun;
            if (eye == null) return;
            eye.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            eye.backgroundColor = flat;
        }
    }
}
