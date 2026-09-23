using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Rocketbox の一人分の定義。ファイルの名前（テクスチャの接頭辞は人の番号と違うことがある。女大 14 は f017）と、
    /// その人に合わせた手入れの値（<see cref="Look"/>）を持つ。組み立て（<see cref="BuildRocketboxProtagonist"/>）と
    /// 撮り比べはこれを受け取って、どの人でも同じ流れで組む。
    ///
    /// 「頭はこの人、体はこの人」（<see cref="Compose"/>）や、「顔はこの人、髪はこの人、体はこの人」（<see cref="ComposeHair"/>）と
    /// 組み合わせることもできる。Rocketbox の女性は骨の並びと束ねた姿勢が同じ（どの骨も 0.3 mm 以内）なので、
    /// 別の人の頭や髪を、体の人の骨にそのまま載せられる。組み合わせたメッシュは <see cref="RocketboxCompose"/> が作る
    /// </summary>
    public sealed class RocketboxPerson
    {
        /// <summary>フォルダの名前（Female_Adult_14 など）。組み合わせは Head08_Body14 など</summary>
        public readonly string Name;
        /// <summary>テクスチャとマテリアルの接頭辞（f017 など）。組み合わせでは使わない</summary>
        public readonly string Prefix;
        /// <summary>一覧の番号での呼び名（女大 14 など）</summary>
        public readonly string Label;
        /// <summary>
        /// 顔（顔の形・顔のテクスチャ・まつ毛・目の玉・首）を持つ人、髪（頭の面の髪の殻と髪の房）を持つ人、
        /// 体（服・手・靴）と骨を持つ人。一人なら自分
        /// </summary>
        public readonly RocketboxPerson FaceFrom, HairFrom, BodyFrom;

        /// <summary>頭の人（顔の人と同じ）。顔と髪が同じ人なら頭ごと</summary>
        public RocketboxPerson HeadFrom { get { return FaceFrom; } }

        /// <summary>
        /// 目の玉の絵の虹彩の位置と半径（頭のテクスチャの UV）。塗らない。撮り比べで虹彩の画素を測る印と、
        /// 目の玉に艶を持たせる範囲にだけ使う
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
            FaceFrom = this;
            HairFrom = this;
            BodyFrom = this;
        }

        RocketboxPerson(string name, string label, RocketboxPerson face, RocketboxPerson hair, RocketboxPerson body)
        {
            Name = name;
            Label = label;
            FaceFrom = face;
            HairFrom = hair;
            BodyFrom = body;
            IrisUv = face.IrisUv;
            IrisRadius = face.IrisRadius;
        }

        /// <summary>頭の人と体の人を組み合わせる</summary>
        public static RocketboxPerson Compose(string name, string label, RocketboxPerson head, RocketboxPerson body)
        {
            return new RocketboxPerson(name, label, head, head, body);
        }

        /// <summary>顔の人・髪の人・体の人を組み合わせる</summary>
        public static RocketboxPerson ComposeHair(string name, string label, RocketboxPerson face, RocketboxPerson hair, RocketboxPerson body)
        {
            return new RocketboxPerson(name, label, face, hair, body);
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

        /// <summary>女大 08 の頭（長い髪ごと）を、女大 14 の体（黒いカーディガン）に載せた人</summary>
        public static readonly RocketboxPerson Head08Body14 = Compose("Head08_Body14", "女大 08 の頭と女大 14 の体", Adult08, Adult14);

        /// <summary>女大 14 の顔と体に、女大 08 の髪（長さと形ごと）を載せた人</summary>
        public static readonly RocketboxPerson Face14Hair08 = ComposeHair("Face14_Hair08", "女大 14 の顔と体に女大 08 の髪", Adult14, Adult08, Adult14);

        /// <summary>手を入れて撮り比べる人の全部</summary>
        public static readonly RocketboxPerson[] All = { Adult14, Adult08, Head08Body14, Face14Hair08 };

        /// <summary>取り込んだ一人（元の FBX とテクスチャを持つ人）</summary>
        public static readonly RocketboxPerson[] Sources = { Adult14, Adult08 };

        public bool IsComposite { get { return FaceFrom != this || HairFrom != this || BodyFrom != this; } }

        /// <summary>顔と髪が別の人（組み合わせたメッシュの面の組は 体・顔・髪の殻・髪の房・まつ毛 の五つ）</summary>
        public bool IsHairSwap { get { return FaceFrom != HairFrom; } }

        public string Dir { get { return RocketboxImport.Root + Name + "/"; } }
        /// <summary>骨と Animator と Avatar を持つ FBX（組み合わせでは体の人の FBX）</summary>
        public string Model { get { return IsComposite ? BodyFrom.Model : Dir + Name + ".fbx"; } }
        /// <summary>組み合わせたメッシュ（組み合わせのときだけ）</summary>
        public string CompositeMesh { get { return Dir + Name + "_mesh.asset"; } }
        /// <summary>顔（と頭）のテクスチャ</summary>
        public string HeadSrc { get { return IsComposite ? FaceFrom.HeadSrc : Dir + Prefix + "_head_color.png"; } }
        public string BodySrc { get { return IsComposite ? BodyFrom.BodySrc : Dir + Prefix + "_body_color.png"; } }
        /// <summary>髪の房（と、顔と髪が同じ人ならまつ毛）の透けの絵</summary>
        public string HairSrc { get { return IsComposite ? HairFrom.HairSrc : Dir + Prefix + "_opacity_color.png"; } }
        /// <summary>髪の殻の絵（髪の人の頭のテクスチャ。顔と髪が別の人のときだけ使う）</summary>
        public string ShellSrc { get { return HairFrom.HeadSrc; } }
        /// <summary>まつ毛の透けの絵（顔の人の。顔と髪が別の人のときだけ使う）</summary>
        public string LashSrc { get { return FaceFrom.HairSrc; } }
        /// <summary>手を入れたテクスチャとマテリアルの置き場（組み立てのたびに描き直す）</summary>
        public string Painted { get { return Dir + "Painted/"; } }
        /// <summary>元の TGA の名前（RawAssets の中）</summary>
        public string[] RawTextures { get { return new[] { Prefix + "_head_color", Prefix + "_body_color", Prefix + "_opacity_color" }; } }

        /// <summary>FBX の中のマテリアルの名前（面の組は体・頭・透け）。組み合わせたメッシュは名前を持たず、面の組の順が体・頭・透け</summary>
        public string BodySlot { get { return BodyFrom.Prefix + "_body"; } }
        public string HeadSlot { get { return FaceFrom.Prefix + "_head"; } }
        public string HairSlot { get { return HairFrom.Prefix + "_opacity"; } }

        /// <summary>
        /// この人の既定の見た目。黒子 5 mm・手入れ 弱め、目は元の色。
        /// 組み合わせでは、頭と髪の値は頭の人から、服の値は体の人から取り、体の手の肌の色を頭の肌に揃える
        /// </summary>
        public RocketboxPaint.Look Look()
        {
            if (IsComposite)
            {
                var k = FaceFrom.Look();
                k.blackenKnit = BodyFrom.Look().blackenKnit;
                k.matchSkin = FaceFrom != BodyFrom;
                if (IsHairSwap)
                {
                    // 顔の人の頭皮は別の人の髪の下地になるので一色にし、元の前髪の影を額から消す
                    k.flatHair = true;
                    k.liftForehead = 1f;
                }
                return k;
            }
            var own = new RocketboxPaint.Look();
            if (tune != null) tune(own);
            return own;
        }

        public override string ToString() { return Label + "（" + Name + "）"; }
    }
}
