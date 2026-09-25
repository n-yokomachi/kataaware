using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地の遠景の書き割り（設計書 9.1 節「遠景の書き割り」）。
    ///
    /// **場面 2 の通りの両端と同じく、撮ってから貼る。** 遠景に置く街並みを撮るためだけに組み
    /// （<see cref="EstateTown"/>）、見る位置を中心にした 16 枚の板の輪へ、一枚ごとにその板と
    /// ぴったり同じ視錐台で撮った切れ端を貼る。組んだ街並みは撮り終えたら捨てるので、
    /// 実行時の重さは輪の 16 枚だけ。
    ///
    /// <list type="table">
    /// <item><term>中心</term><description>三階の廊下の真ん中の真下、二階の廊下の目の高さ（4.42 m）。
    /// 廊下（7.22 m）と庭（1.62 m）の両方から八方を見て、本物の地面の遠い縁と書き割りの地面の境目の
    /// 上下のずれが、最悪の方角でいちばん小さくなる所（<see cref="BackdropRing.Seam"/>）。
    /// ずれは目の高さの差でほぼ決まり、中心を水平に動かしてもほとんど変わらないので、
    /// 水平の位置は一番長く立つ三階の廊下に合わせて、歩いたときの横のずれを小さくした</description></item>
    /// <item><term>板</term><description>中心から辺まで 200 m の正十六角形。歩ける所のどこから見ても、
    /// 板の一番遠い角まで 250 m を超えない（カメラの far は 260 m）</description></item>
    /// <item><term>本物の地面</term><description>同じ中心・同じ向きの、辺まで 80 m の正十六角形で切る。
    /// そこから先の地面は書き割りの絵が持つ。60 m より手前の棟と道路は組んだ形のまま残り、
    /// 歩いたときの視差はそちらが持つ</description></item>
    /// </list>
    ///
    /// **板は霞を受けない**（<c>HalfAware/Backdrop</c>）。撮った時点で霞が焼き込んであり、
    /// 二重に白むため。**空は板に写さない。** 同じ構図を背景の黒と白で二回撮り、
    /// 差の出た画素を空として抜く。板の上には本物の空が見える。
    ///
    /// 置く・撮る・抜く道具は公園と分け合うので <c>BuildDiveFar.cs</c> にある。ここに持つのは団地の輪の寸法だけ
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>書き割りの輪の中心。場所のローカル。y は使わない</summary>
        public static readonly Vector3 EstateFarCentre = new Vector3(3.4f, 0f, EstateWalk);
        /// <summary>撮る目の高さ。二階の廊下の目の高さに当たる</summary>
        public const float EstateFarEye = EstateTop - Floor + 1.62f;                       // 4.42
        /// <summary>中心から板の辺まで</summary>
        public const float EstateFarRadius = 200f;
        /// <summary>本物の地面の遠い縁。中心から辺まで。絵の中の街並みはここより外にだけ置く</summary>
        public const float EstateFarGround = 80f;
        /// <summary>板の枚数</summary>
        public const int EstateFarPanels = 16;
        /// <summary>
        /// 板の上端。絵の中の一番高い物（中心から仰角 12.5 度まで）が収まる高さ
        /// </summary>
        public const float EstateFarTop = 56f;

        /// <summary>団地の輪。寸法は上の値</summary>
        static readonly FarRing EstateRing = new FarRing
        {
            Centre = EstateFarCentre,
            Eye = EstateFarEye,
            Radius = EstateFarRadius,
            Ground = EstateFarGround,
            Panels = EstateFarPanels,
            Top = EstateFarTop,
            Picture = DiveTextures + "EstateBackdrop.png",
            Material = Materials + "EstateBackdrop.mat",
            Name = "EstateBackdrop",
        };

        /// <summary>
        /// 板の下端。絵の中で地面の遠い縁が落ちる線（<see cref="BackdropRing.GroundLine"/>）より 10 m 下げる。
        /// 中心より高い目（三階の廊下）から見ると本物の地面の縁が線より下に見えるので、
        /// 板をそこで切ると、縁と板の間から空が覗く
        /// </summary>
        public static float EstateFarBottom
        {
            get { return EstateRing.Bottom; }
        }

        /// <summary>書き割りの輪を置く。撮った絵が無ければ置かずに一行だけ知らせる</summary>
        static void EstateBackdrop(Transform place)
        {
            Backdrop(place, EstateRing, "HalfAware/Shoot the estate backdrop");
        }

        /// <summary>
        /// 遠い地面のマテリアル。本物の地面（80 m まで）と、書き割りの中の地面の両方に使う。
        /// 同じマテリアルで撮るので、縁の所で色が揃う
        /// </summary>
        static Material EstateLandMat()
        {
            return Glow("EstateLand", new Color(0.60f, 0.62f, 0.53f), 0.52f);
        }

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮る。撮るためだけの街並みを組み（<see cref="EstateTown"/>）、団地の空・霞・日・環境光のまま 16 枚を撮り、
        /// 一枚の絵に並べて書き出す。組んだ物は全部捨ててから返る
        /// </summary>
        [MenuItem("HalfAware/Shoot the estate backdrop", false, 251)]
        public static void ShootEstateBackdrop()
        {
            ShootBackdrop(DiveIds.Estate, EstateRing, EstatePlaceSky, EstateTown);
        }

        /// <summary>
        /// 16 枚を撮って一枚に並べる。呼ぶ側が <see cref="CheckDiveSky.Stage"/> で場所と空を持っている間に呼ぶ。
        /// <paramref name="build"/> は撮る物を組む手順（ふだんは <see cref="EstateTown"/>）。
        /// 測る道具（<see cref="CheckDiveSky"/>）は、地面だけを塗り分けた物を渡して境目の線を撮る
        /// </summary>
        internal static Texture2D EstateFarShoot(Transform place, PlaceSky sky,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            return FarShoot(place, sky, EstateRing, build);
        }
    }
}
