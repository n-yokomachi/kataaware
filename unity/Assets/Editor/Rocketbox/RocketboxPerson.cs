using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Rocketbox の一人分の定義。ファイルの名前（テクスチャの接頭辞は人の番号と違うことがある。女大 14 は f017）と、
    /// その人に合わせた手入れの値（<see cref="Look"/>）を持つ。組み立て（<see cref="BuildRocketboxProtagonist"/>）と
    /// 撮り比べはこれを受け取って、どの人でも同じ流れで組む
    /// </summary>
    public sealed class RocketboxPerson
    {
        /// <summary>フォルダと FBX の名前（Female_Adult_14 など）</summary>
        public readonly string Name;
        /// <summary>テクスチャとマテリアルの接頭辞（f017 など）</summary>
        public readonly string Prefix;
        /// <summary>一覧の番号での呼び名（女大 14 など）</summary>
        public readonly string Label;

        /// <summary>
        /// 目の玉の絵の虹彩の位置と半径（頭のテクスチャの UV）。塗らない。撮り比べで虹彩の画素を測る印にだけ使う
        /// </summary>
        public Vector2 IrisUv = new Vector2(0.2634f, 0.0675f);
        public float IrisRadius = 0.0171f;

        readonly System.Action<RocketboxPaint.Look> tune;

        RocketboxPerson(string name, string prefix, string label, System.Action<RocketboxPaint.Look> tune)
        {
            Name = name;
            Prefix = prefix;
            Label = label;
            this.tune = tune;
        }

        /// <summary>女大 14。髪は顎の長さのボブ。服はニットのカーディガンで、黒に塗る</summary>
        public static readonly RocketboxPerson Adult14 = new RocketboxPerson("Female_Adult_14", "f017", "女大 14", k =>
        {
            k.blackenKnit = true;
            k.hairLowest = 0.17f;
        });

        /// <summary>
        /// 女大 08。髪は長い。頭のテクスチャの髪が首の後ろまで続くので、髪と見なす高さを下げる。
        /// 服（灰の T シャツとジーンズ）は元のまま
        /// </summary>
        public static readonly RocketboxPerson Adult08 = new RocketboxPerson("Female_Adult_08", "f008", "女大 08", k =>
        {
            k.blackenKnit = false;
            k.hairLowest = 0.34f;
        });

        public static readonly RocketboxPerson[] All = { Adult14, Adult08 };

        public string Dir { get { return RocketboxImport.Root + Name + "/"; } }
        public string Model { get { return Dir + Name + ".fbx"; } }
        public string HeadSrc { get { return Dir + Prefix + "_head_color.png"; } }
        public string BodySrc { get { return Dir + Prefix + "_body_color.png"; } }
        public string HairSrc { get { return Dir + Prefix + "_opacity_color.png"; } }
        /// <summary>手を入れたテクスチャとマテリアルの置き場（組み立てのたびに描き直す）</summary>
        public string Painted { get { return Dir + "Painted/"; } }
        /// <summary>元の TGA の名前（RawAssets の中）</summary>
        public string[] RawTextures { get { return new[] { Prefix + "_head_color", Prefix + "_body_color", Prefix + "_opacity_color" }; } }

        /// <summary>FBX の中のマテリアルの名前（面の組は体・頭・透け）</summary>
        public string BodySlot { get { return Prefix + "_body"; } }
        public string HeadSlot { get { return Prefix + "_head"; } }
        public string HairSlot { get { return Prefix + "_opacity"; } }

        /// <summary>この人の既定の見た目。黒子 5 mm・手入れ 弱め、目は元の色</summary>
        public RocketboxPaint.Look Look()
        {
            var k = new RocketboxPaint.Look();
            if (tune != null) tune(k);
            return k;
        }

        public override string ToString() { return Label + "（" + Name + "）"; }
    }
}
