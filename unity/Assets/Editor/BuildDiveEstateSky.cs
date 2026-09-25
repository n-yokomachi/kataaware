using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地の空・日・霞・環境光（設計書 9.1 節「空と光」）。
    ///
    /// **一揃いで作る。** 一色の背景色で空を塗っていたのをやめ、
    /// 空の絵（<see cref="SkyPaint"/> で焼いた正距円筒の絵を <c>Skybox/Panoramic</c> に貼ったもの）、
    /// 絵の中の日と同じ向きの灯り、空の地平と同じ色の霞、空から取った環境光を、
    /// <see cref="PlaceSky"/> 一つにまとめて <see cref="DiveDirector"/> に持たせる。
    ///
    /// **空は絵に焼く。** <c>Skybox/Procedural</c> は上下の移りも日の周りも決まった式で、
    /// 雲を出す口が無い。絵なら WebGL でも同じに出て、遠さの限り（far 260）も受けず、
    /// 雲の帯と日の方角の暖かさを好きな形に決められる。
    ///
    /// **環境光は三色（Trilight）で渡す。** 空の絵から取る形（Skybox）は、差し替えのたびに
    /// 環境の光を焼き直さないと前の場所の光が残る。三色は空の絵から数えて出す（<see cref="SkyAmbient"/>）。
    ///
    /// 絵を焼く・マテリアルを作る・環境光を取る道具は公園と分け合うので <c>BuildDiveSky.cs</c> にある。
    /// ここに持つのは団地の見え方の値だけ
    /// </summary>
    public static partial class BuildDive
    {
        const string EstateSkyPath = DiveTextures + "EstateSky.png";
        /// <summary>空のマテリアル。前に遠景のビルに使っていた EstateSky.mat とは別の名前にする</summary>
        const string EstateSkyMatPath = Materials + "EstateSkybox.mat";

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。
        ///
        /// **近くはほとんど霞まず、遠くで急に白む形。** 隣の棟（25 m）で 0.6%、中の棟（50 m）で 2%、
        /// 書き割りの始まり（80 m）で 6%、板（200 m）で 30%、400 m で 76%、600 m で 96%。
        /// 部屋や廊下の中の色は変えず、書き割りの中の遠近だけを霞に持たせる
        /// </summary>
        public const float EstateHazeDensity = 0.003f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す
        static readonly Color EstateZenith = new Color(0.27f, 0.40f, 0.63f);
        static readonly Color EstateMiddle = new Color(0.47f, 0.59f, 0.76f);
        /// <summary>地平の色。霞の色もこれにする</summary>
        static readonly Color EstateHorizon = new Color(0.76f, 0.80f, 0.85f);
        static readonly Color EstateWarm = new Color(0.97f, 0.85f, 0.70f);
        static readonly Color EstateGlow = new Color(1.00f, 0.88f, 0.70f);
        static readonly Color EstateDisc = new Color(1.00f, 0.97f, 0.90f);
        static readonly Color EstateCloudLit = new Color(0.96f, 0.88f, 0.80f);
        static readonly Color EstateCloudShade = new Color(0.66f, 0.68f, 0.75f);

        /// <summary>地面の照り返しの色。乾いたコンクリートと土。sRGB</summary>
        static readonly Color EstateBounce = new Color(0.52f, 0.44f, 0.34f);

        /// <summary>
        /// 団地の空の見え方。<paramref name="sun"/> は日へ向かう向き（影を落とす灯りの逆）。
        ///
        /// 日の円は実際より大きく取る（半径 1.3 度）。ゲームは 320×180 で描くので、
        /// 実際の 0.27 度では一画素にも満たず、空のどこに日があるのか読めない
        /// </summary>
        static SkyPaint.Look EstateSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = EstateZenith.linear,
                middle = EstateMiddle.linear,
                horizon = EstateHorizon.linear,
                whiteDepth = 0.09f,
                warm = EstateWarm.linear,
                warmth = 0.75f,
                warmDepth = 0.10f,
                warmFocus = 4f,
                sun = sun.normalized,
                glow = EstateGlow.linear,
                glowNear = 0.45f,
                glowNearWidth = 4f,
                glowWide = 0.20f,
                glowWideWidth = 26f,
                disc = EstateDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = EstateCloudLit.linear,
                cloudShade = EstateCloudShade.linear,
                cloudCover = 0.55f,
                // 仰角 1.5 度から 15 度。地平の白みの上に薄く流れる
                cloudLow = Mathf.Sin(1.5f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(15f * Mathf.Deg2Rad),
                cloudSeed = 7,
            };
        }

        /// <summary>日へ向かう向き。団地の日射し（Morning）の逆。灯りがまだ無ければ組み立ての値から出す</summary>
        static Vector3 EstateSunward(Transform place)
        {
            return Sunward(place, "Morning", EstateMorningAim);
        }

        /// <summary>
        /// 団地の空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す
        /// </summary>
        static PlaceSky EstatePlaceSky(Transform place)
        {
            return PaintedSky(place, "Morning", EstateSkyLook(EstateSunward(place)), EstateHorizon, EstateHazeDensity,
                EstateBounce, 1f, EstateSkyPath, EstateSkyMatPath);
        }
    }
}
