using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の遠景の書き割り（設計書 9.1 節「遠景の書き割り」）の、場面 4 の側の口。
    ///
    /// 置く・撮る・抜く・書き出すの本体は場所に依らない形で <see cref="FarBackdrop"/>（<c>FarBackdrop.cs</c>）にあり、
    /// 村と分け合う。ここに残すのは、場面 4 の生成物の置き場（<see cref="Generated"/>）と、
    /// 場所の id から場所を起こして空を整える（<see cref="CheckDiveSky.Stage"/>）ところだけ。
    /// 撮る街並みは場所ごとに組む（<c>EstateTown</c>・<c>ParkTown</c>）
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>撮るときに縦横これだけ細かく撮って、縮めて縁を滑らかにする（電車の窓の外も同じ）</summary>
        const int FarFine = FarBackdrop.Fine;

        /// <summary>書き割りの輪を置く。撮った絵が無ければ置かずに一行だけ知らせる</summary>
        static void Backdrop(Transform place, FarRing ring, string shootMenu)
        {
            FarBackdrop.Place(place, ring, shootMenu, Generated);
        }

        static Material FarMat(FarRing ring, Texture2D tex)
        {
            return FarBackdrop.Mat(ring, tex);
        }

        /// <summary>本物の遠い地面。輪と同じ中心・同じ向きの正多角形で切る（<see cref="FarBackdrop.Land"/>）</summary>
        static void FarLand(Transform place, FarRing ring, string name, Material mat)
        {
            FarBackdrop.Land(place, ring, name, mat, Generated);
        }

        /// <summary>
        /// 遠景を撮って絵に書き出す。場所を起こして空を整え（<see cref="CheckDiveSky.Stage"/>）、
        /// 撮るためだけの街並みを <paramref name="build"/> で組み、輪の板を一枚ずつ撮り、一枚の絵に並べて書き出す。
        ///
        /// **一回の呼び出しの中で始めから終わりまで済ませ、全部を元に戻してから返る。**
        /// 同じエディタで別の担当が同時に測っているので、場所・記憶・RenderSettings・
        /// 組んだ街並み・伏せた mesh を途中の状態で残さない。
        /// 輪そのものは置き直さない。置くのは `HalfAware/Build the dive`
        /// </summary>
        static void ShootBackdrop(string placeId, FarRing ring, System.Func<Transform, PlaceSky> skyOf,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("再生中とコンパイル中は撮らない");
                return;
            }
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + placeId) : null;
            if (place == null)
            {
                Debug.LogError(placeId + " がまだ組まれていない。先に HalfAware/Build the dive");
                return;
            }
            var sky = skyOf(place);

            Texture2D picture;
            using (new CheckDiveSky.Stage(placeId))
                picture = FarShoot(place, sky, ring, build);
            if (picture == null) return;
            FarBackdrop.Write(ring, picture);
            Debug.Log("遠景を撮った: " + ring.Picture + "。HalfAware/Build the dive で輪に貼る");
        }

        /// <summary>
        /// 輪の板を全部撮って一枚に並べる。呼ぶ側が <see cref="CheckDiveSky.Stage"/> で場所と空を持っている間に呼ぶ。
        /// 測る道具（<see cref="CheckDiveSky"/>）は、地面だけを塗り分けた物を渡して境目の線を撮る
        /// </summary>
        static Texture2D FarShoot(Transform place, PlaceSky sky, FarRing ring,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            return FarBackdrop.Take(place, sky, ring, build);
        }

        /// <summary>黒と白の背景で撮った二枚から、空を抜いた一枚を作る（<see cref="FarBackdrop.Cut"/>）</summary>
        static Color32[] FarCut(Texture2D dark, Texture2D light, int fw, int fh, Color32 blank)
        {
            return FarBackdrop.Cut(dark, light, fw, fh, blank);
        }
    }
}
