using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>調べる対象の読み取り口。シーン上の Interactable と、テストの代用品が実装する</summary>
    public interface IInteractable
    {
        string Id { get; }
        Vector3 Position { get; }
        /// <summary>今この対象が場に在るか。false の間は選べない。前腕のように出たり消えたりする物に使う</summary>
        bool Active { get; }
        /// <summary>この距離以内で選べる。既定は InteractionPicker.DefaultRadius</summary>
        float Radius { get; }
        bool Required { get; }
        /// <summary>true なら一度調べると選べなくなる。既定は true</summary>
        bool Once { get; }
        /// <summary>ここに挙げた id が済むまで選べない。ただし HintFor に文がある id については選べて、その文だけ出る。null にせず、無ければ空にする</summary>
        IReadOnlyList<string> After { get; }
        string Label { get; }
        /// <summary>調べたときに出す文。null にせず、無ければ空にする</summary>
        IReadOnlyList<string> Lines { get; }
        /// <summary>文の後に二択を出すか。出すなら「はい」を選ぶまで済んだことにならない</summary>
        bool Asks { get; }
        /// <summary>二択の問い。Asks が false なら空</summary>
        string Question { get; }
        /// <summary>「はい」の後に出す文。null にせず、無ければ空にする</summary>
        IReadOnlyList<string> AfterYes { get; }
        /// <summary>After の id が未達のときに出す文。無ければ null</summary>
        IReadOnlyList<string> HintFor(string afterId);
    }
}
