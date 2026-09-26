using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 村と庭の二つの時刻の空・日・霞・環境光（設計書 1 節）。
    /// 仕組みは場面 4 の場所と同じで、空の絵は <c>BuildDiveSky.cs</c> の <c>PaintedSky</c> が焼く。
    /// ここに持つのは村の値だけ。
    ///
    /// **空の中の日と、影を落とす日の向きを揃える**（場面 4 の設計書 9.1 節）。
    /// 空の絵の日は、時刻の子に置いた Directional（Sun）の逆向きに描かれる
    /// </summary>
    public static partial class BuildVillage
    {
        const string MorningSkyPath = Textures + "VillageMorningSky.png";
        const string MorningSkyMatPath = Materials + "VillageMorningSkybox.mat";
        const string EveningSkyPath = Textures + "VillageEveningSky.png";
        const string EveningSkyMatPath = Materials + "VillageEveningSkybox.mat";

        /// <summary>
        /// 環境光の倍率。屋根の無い所だけなので、空から取った値を公園（1.6）よりさらに上げる。
        /// 朝も夕方も日が低く、塀と家と温室の影が庭の半分を覆う。1.6 では影の中の花の縁と芝が黒い帯に沈んだ
        /// </summary>
        const float VillageAmbientGain = 2.2f;

        // ---- 朝 ---------------------------------------------------------------------------

        /// <summary>
        /// 朝の日の向き（Euler）。8 月中旬の 07:30 ほど、日は東北東（方位 70 度）の仰角 15 度。
        /// 灯りは日から場所へ向かうので、方位に 180 度を足した向きを指す
        /// </summary>
        static readonly Vector3 MorningAim = new Vector3(15f, 70f + 180f, 0f);

        /// <summary>
        /// 朝の霞。薄い朝靄。裏庭の奥（20 m）で 2.6%、路地の端から端（80 m）で 34%。
        /// 公営住宅の朝（0.003）より濃い。畑の上の靄がまだ残っている
        /// </summary>
        const float MorningHaze = 0.0080f;

        static readonly Color MorningZenith = new Color(0.33f, 0.48f, 0.70f);
        static readonly Color MorningMiddle = new Color(0.56f, 0.67f, 0.80f);
        /// <summary>地平の色。霞の色もこれにする。靄で白む</summary>
        static readonly Color MorningHorizon = new Color(0.83f, 0.84f, 0.82f);
        static readonly Color MorningWarm = new Color(1.00f, 0.87f, 0.68f);
        static readonly Color MorningGlow = new Color(1.00f, 0.90f, 0.72f);
        static readonly Color MorningDisc = new Color(1.00f, 0.97f, 0.88f);
        static readonly Color MorningCloudLit = new Color(0.98f, 0.92f, 0.84f);
        static readonly Color MorningCloudShade = new Color(0.70f, 0.72f, 0.78f);
        /// <summary>地面の照り返し。芝と麦</summary>
        static readonly Color MorningBounce = new Color(0.42f, 0.44f, 0.26f);

        static SkyPaint.Look MorningLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = MorningZenith.linear,
                middle = MorningMiddle.linear,
                horizon = MorningHorizon.linear,
                whiteDepth = 0.12f,
                warm = MorningWarm.linear,
                warmth = 0.70f,
                warmDepth = 0.12f,
                warmFocus = 3.5f,
                sun = sun.normalized,
                glow = MorningGlow.linear,
                glowNear = 0.45f,
                glowNearWidth = 4f,
                glowWide = 0.24f,
                glowWideWidth = 28f,
                disc = MorningDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = MorningCloudLit.linear,
                cloudShade = MorningCloudShade.linear,
                cloudCover = 0.35f,
                cloudLow = Mathf.Sin(2f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(14f * Mathf.Deg2Rad),
                cloudSeed = 23,
            };
        }

        /// <summary>
        /// 朝の灯り。日と、日の反対（西南西の高い所）の空の青の代わり。
        /// 日の当たらない西向きの面（家の正面の西寄り、格子戸）が環境光だけの平らな色にならないように
        /// </summary>
        static void MorningLights(Transform parent)
        {
            var sun = BuildDive.Lamp(parent, "Sun", LightType.Directional, new Vector3(0f, 20f, 0f),
                MorningAim, new Color(1f, 0.93f, 0.80f), 2.1f, 10f);
            sun.shadows = LightShadows.Soft;
            BuildDive.Lamp(parent, "Fill", LightType.Directional, new Vector3(0f, 20f, 0f),
                new Vector3(40f, 70f, 0f), new Color(0.70f, 0.78f, 0.95f), 0.55f, 10f);
        }

        static PlaceSky MorningSky(Transform lights)
        {
            var sunward = -(lights.Find("Sun").forward);
            return BuildDive.PaintedSky(lights, "Sun", MorningLook(sunward), MorningHorizon, MorningHaze,
                MorningBounce, VillageAmbientGain, MorningSkyPath, MorningSkyMatPath);
        }

        // ---- 夕方 -------------------------------------------------------------------------

        /// <summary>
        /// 夕方の日の向き（Euler）。8 月中旬の 20:15 ほど、日は西北西（方位 295 度）の仰角 5 度
        /// </summary>
        static readonly Vector3 EveningAim = new Vector3(5f, 295f - 180f, 0f);

        /// <summary>夕方の霞。朝より薄い。暖かい地平の色に遠くを溶かす</summary>
        const float EveningHaze = 0.0045f;

        static readonly Color EveningZenith = new Color(0.27f, 0.37f, 0.60f);
        static readonly Color EveningMiddle = new Color(0.54f, 0.57f, 0.70f);
        static readonly Color EveningHorizon = new Color(0.90f, 0.78f, 0.64f);
        static readonly Color EveningWarm = new Color(1.00f, 0.66f, 0.38f);
        static readonly Color EveningGlow = new Color(1.00f, 0.74f, 0.46f);
        static readonly Color EveningDisc = new Color(1.00f, 0.88f, 0.64f);
        static readonly Color EveningCloudLit = new Color(1.00f, 0.74f, 0.54f);
        static readonly Color EveningCloudShade = new Color(0.56f, 0.52f, 0.62f);
        static readonly Color EveningBounce = new Color(0.46f, 0.40f, 0.24f);

        static SkyPaint.Look EveningLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = EveningZenith.linear,
                middle = EveningMiddle.linear,
                horizon = EveningHorizon.linear,
                whiteDepth = 0.10f,
                warm = EveningWarm.linear,
                warmth = 0.95f,
                warmDepth = 0.18f,
                warmFocus = 2.5f,
                sun = sun.normalized,
                glow = EveningGlow.linear,
                glowNear = 0.60f,
                glowNearWidth = 5f,
                glowWide = 0.34f,
                glowWideWidth = 36f,
                disc = EveningDisc.linear,
                discRadius = 1.4f,
                discSoft = 0.5f,
                cloudLit = EveningCloudLit.linear,
                cloudShade = EveningCloudShade.linear,
                cloudCover = 0.45f,
                cloudLow = Mathf.Sin(1.5f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(16f * Mathf.Deg2Rad),
                cloudSeed = 29,
            };
        }

        /// <summary>
        /// 夕方の灯り。低い暖かい日と、日の反対（東南東の高い所）の薄紫の空の代わり
        /// </summary>
        static void EveningLights(Transform parent)
        {
            var sun = BuildDive.Lamp(parent, "Sun", LightType.Directional, new Vector3(0f, 20f, 0f),
                EveningAim, new Color(1f, 0.74f, 0.50f), 2.3f, 10f);
            sun.shadows = LightShadows.Soft;
            BuildDive.Lamp(parent, "Fill", LightType.Directional, new Vector3(0f, 20f, 0f),
                new Vector3(42f, 295f, 0f), new Color(0.66f, 0.68f, 0.90f), 0.50f, 10f);
        }

        static PlaceSky EveningSky(Transform lights)
        {
            var sunward = -(lights.Find("Sun").forward);
            return BuildDive.PaintedSky(lights, "Sun", EveningLook(sunward), EveningHorizon, EveningHaze,
                EveningBounce, VillageAmbientGain, EveningSkyPath, EveningSkyMatPath);
        }
    }
}
