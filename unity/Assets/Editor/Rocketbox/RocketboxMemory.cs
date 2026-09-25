using System;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 場面 4 の記憶の中の人ひとりを、Rocketbox の模型で作る定義（<c>docs/superpowers/specs/2026-09-26-dive-people-design.md</c>）。
    ///
    /// 名前・年・出身・背・背の丸みは <see cref="DiveCast"/> の十六人から引く（<see cref="Id"/> で結ぶ）。ここで持つのは次の所だけ:
    /// - 当てる模型（設計メモ 3 節の案）
    /// - 骨の縮尺。子どもは 10〜12 歳の模型に対する比、15・16 歳は大人の模型に対する比（<see cref="DiveCast.ProportionOf"/> の 10 代）
    /// - 塗り替え。年寄りの白髪・灰髪、メイとソフィアの髪、メイの上の服（<see cref="RocketboxMemoryPaint"/>）。服と肌の色は元のまま
    /// - 手首の差込口。18 以上の大人で手首が出る人だけ（エミリーとプリヤ）
    ///
    /// 組み立ては <see cref="BuildDiveCast"/>
    /// </summary>
    public sealed class RocketboxMemory
    {
        /// <summary><see cref="DiveCast"/> の id（Hanna など）</summary>
        public readonly string Id;
        public readonly RocketboxMob Model;
        /// <summary>骨の縮尺（頭・腕・脚。どれも骨ごとの一様な縮尺）</summary>
        public DiveCast.Proportion Proportion = new DiveCast.Proportion { head = 1f, arm = 1f, leg = 1f };
        /// <summary>頭のテクスチャの髪と、髪の房の塗り替え。null なら元の色のまま</summary>
        public RocketboxMemoryPaint.HairTone Hair;
        /// <summary>上の服の塗り替え。null なら元の色のまま</summary>
        public RocketboxMemoryPaint.TopTone Top;
        /// <summary>右の手首の差込口と銀の回路（<see cref="BuildProps.WristPort"/>）を付けるか</summary>
        public bool Port;
        /// <summary>頭のテクスチャで髪と見なす一番低い所（目の高さから下へ m）。後ろで束ねた髪が首の後ろへ下がる人は大きく</summary>
        public float HairLowest = 0.17f;
        /// <summary>
        /// 目の高さからこの分（m）より上は、色に関わらず髪として塗る。0 なら色で見分けるだけ。
        /// 生え際の細い毛が肌の色に近い金髪を暗く塗る人（メイ・ソフィア）に使う（<see cref="RocketboxMemoryPaint.Head"/>）
        /// </summary>
        public float Hairline;

        RocketboxMemory(string id, RocketboxMob model)
        {
            Id = id;
            Model = model;
        }

        /// <summary><see cref="DiveCast"/> の一人（背・背の丸み・年）</summary>
        public Person Cast
        {
            get
            {
                Person p;
                if (!DiveCast.TryById(Id, out p)) throw new InvalidOperationException("DiveCast に無い人: " + Id);
                return p;
            }
        }

        public override string ToString() { return Cast.name + "（" + Id + "、" + Model.Label + "）"; }

        // ---- 骨の縮尺 --------------------------------------------------------------

        /// <summary>
        /// 3 歳（ルーカス）を 10〜12 歳の模型から作る比。今の <see cref="DiveCast.ProportionOf"/> の幼児（大人の模型に対して頭 1.42・腕 0.80・脚 0.72）を、
        /// 子どもの模型に対する比へ読み替えた値（設計メモ 3 節）
        /// </summary>
        public static readonly DiveCast.Proportion ToddlerOnChild = new DiveCast.Proportion { head = 1.15f, arm = 0.88f, leg = 0.80f };
        /// <summary>6〜7 歳（メイ・ソフィア）を 10〜12 歳の模型から作る比（設計メモ 3 節）</summary>
        public static readonly DiveCast.Proportion ChildOnChild = new DiveCast.Proportion { head = 1.08f, arm = 0.94f, leg = 0.90f };
        /// <summary>15・16 歳を大人の模型から作る比。今の 10 代の頭（1.03）のまま</summary>
        public static DiveCast.Proportion TeenOnAdult { get { return DiveCast.ProportionOf(AgeBand.Teen); } }

        // ---- 塗り替えの色（sRGB） --------------------------------------------------------

        /// <summary>メイの髪。主人公の黒髪と同じ影と艶（<see cref="RocketboxPaint.Look"/> の既定）</summary>
        public static readonly RocketboxMemoryPaint.HairTone Black = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.016f, 0.015f, 0.018f), shine = new Color(0.215f, 0.205f, 0.225f), gamma = 1.25f,
        };
        /// <summary>ソフィアの髪。焦げ茶</summary>
        public static readonly RocketboxMemoryPaint.HairTone DarkBrown = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.050f, 0.030f, 0.020f), shine = new Color(0.330f, 0.210f, 0.130f), gamma = 1.15f,
        };
        /// <summary>アルベルト（78）の白髪</summary>
        public static readonly RocketboxMemoryPaint.HairTone White = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.50f, 0.49f, 0.47f), shine = new Color(0.93f, 0.92f, 0.90f), gamma = 0.9f,
        };
        /// <summary>
        /// ジョルジョ（66）の白髪。元から白髪なので、色みの黄ばみと赤みを抜いて揃えるだけ。
        /// 影を明るめにして明暗の幅を元に近く保つ（影 0.40 では、後ろ頭の元の影が濃い灰のむらに強まった）
        /// </summary>
        public static readonly RocketboxMemoryPaint.HairTone WhiteGrey = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.54f, 0.53f, 0.52f), shine = new Color(0.90f, 0.89f, 0.87f), gamma = 1.0f,
        };
        /// <summary>ローザ（72）の白と灰の混じった髪。影は灰、艶は白</summary>
        public static readonly RocketboxMemoryPaint.HairTone SaltPepper = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.27f, 0.26f, 0.25f), shine = new Color(0.88f, 0.87f, 0.85f), gamma = 1.0f,
        };
        /// <summary>エレナ（63）の灰の髪</summary>
        public static readonly RocketboxMemoryPaint.HairTone Grey = new RocketboxMemoryPaint.HairTone
        {
            shadow = new Color(0.22f, 0.21f, 0.20f), shine = new Color(0.72f, 0.71f, 0.69f), gamma = 1.0f,
        };
        /// <summary>
        /// メイの上の服。女子 01 のマゼンタの T シャツを、今のメイの上の色（黄、<see cref="DiveCast"/> の E0B63A）へ。
        /// ソフィアは元のマゼンタのまま（今のソフィアの赤に近い）。白い長袖とジーンズは二人とも元のまま
        /// </summary>
        public static readonly RocketboxMemoryPaint.TopTone Yellow = new RocketboxMemoryPaint.TopTone
        {
            hue = 300f, hueWidth = 40f, satMin = 0.30f,
            shadow = new Color(0.40f, 0.28f, 0.05f), shine = new Color(0.96f, 0.80f, 0.30f),
        };

        /// <summary>女子 01 の生え際（目の高さから上へ m、束ねた姿勢）。額の上の細い金髪の所から上を髪にする</summary>
        public const float ChildHairline = 0.058f;

        // ---- 十六人 ---------------------------------------------------------------

        static RocketboxMemory Who(string id, string model) { return new RocketboxMemory(id, RocketboxMob.DiveModel(model)); }

        /// <summary>十六人。並びは自分の記憶の番号（<see cref="DiveCast.People"/> と同じ）</summary>
        public static readonly RocketboxMemory[] All =
        {
            new RocketboxMemory("Mei", RocketboxMob.DiveModel("Female_Child_01")) { Proportion = ChildOnChild, Hair = Black, Top = Yellow, HairLowest = 0.30f, Hairline = ChildHairline },
            Who("Hanna", "Female_Adult_11"),
            new RocketboxMemory("Albert", RocketboxMob.DiveModel("Male_Adult_05")) { Hair = White },
            new RocketboxMemory("Sofia", RocketboxMob.DiveModel("Female_Child_01")) { Proportion = ChildOnChild, Hair = DarkBrown, HairLowest = 0.30f, Hairline = ChildHairline },
            new RocketboxMemory("Emily", RocketboxMob.DiveModel("Female_Adult_08")) { Port = true },
            Who("Mark", "Business_Male_04"),
            Who("Linda", "Business_Female_02"),
            Who("Lee", "Business_Male_02"),
            new RocketboxMemory("Giorgio", RocketboxMob.DiveModel("Male_Adult_03")) { Hair = WhiteGrey },
            new RocketboxMemory("Rosa", RocketboxMob.DiveModel("Female_Adult_02")) { Hair = SaltPepper, HairLowest = 0.30f },
            new RocketboxMemory("Lucas", RocketboxMob.DiveModel("Male_Child_01")) { Proportion = ToddlerOnChild },
            new RocketboxMemory("Priya", RocketboxMob.DiveModel("Female_Party_02")) { Port = true },
            new RocketboxMemory("Daniel", RocketboxMob.DiveModel("Male_Adult_06")) { Proportion = TeenOnAdult },
            new RocketboxMemory("Aisha", RocketboxMob.DiveModel("Business_Female_01")) { Proportion = TeenOnAdult },
            new RocketboxMemory("Mateo", RocketboxMob.DiveModel("Business_Male_06")) { Proportion = TeenOnAdult },
            new RocketboxMemory("Elena", RocketboxMob.DiveModel("Female_Adult_09")) { Hair = Grey, HairLowest = 0.30f },
        };

        public static RocketboxMemory ById(string id)
        {
            foreach (var m in All) if (m.Id == id) return m;
            throw new ArgumentException("記憶の人に無い: " + id);
        }
    }
}
