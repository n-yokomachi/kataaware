using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// エンディングの景色の帯（シナリオ設計 13.1.1）。帯ごとに景色・時刻（空と光）・走り・入る曲の秒を持つ。
    /// 場面 8 と同じく暗転を挟まずに切り替える（<see cref="EndingDirector"/> が曲の秒で DriveWorld の沿道と空を差し替える）。
    ///
    /// **並びと秒の表は組み立て（BuildEndingLand.Route）の一か所。** ここは場面に書き込まれた値で、
    /// 帯を外すなら組み立ての表の行を消して組み直す（前の帯がそのぶん長くなる）
    /// </summary>
    [Serializable]
    public sealed class EndingBand
    {
        [Tooltip("景色の名。確かめ用")]
        public string name;
        [Tooltip("この帯へ替わる曲の秒。帯は秒の小さい順に並べる。最初の帯は 0")]
        public float from;
        [Tooltip("走る速さ。m/s")]
        public float speed = 9f;
        [Tooltip("路面の粗さ。1 が舗装")]
        public float rough = 1f;
        [Tooltip("空と光（時刻）。前照灯の強さ（beam）もここ")]
        public DriveSky sky;

        /// <summary>曲の秒 song に出している帯の番号。from が song を越えない最後の帯（曲の前は 0）。帯が無ければ -1</summary>
        public static int At(IList<EndingBand> bands, float song)
        {
            if (bands == null || bands.Count == 0) return -1;
            var at = 0;
            for (var i = 1; i < bands.Count; i++)
                if (bands[i] != null && song >= bands[i].from) at = i;
            return at;
        }
    }
}
