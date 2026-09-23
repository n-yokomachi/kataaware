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

        /// <summary>
        /// この人の髪を別の人の顔に載せるとき、こめかみのどこまでを髪の殻に入れて黒く塗るか（本人の左・右）。
        /// x は目の玉の中心から外へ、y は目の玉の中心から奥へ（m。目の 1 cm 上より下は、もみあげのように細らせる）。
        /// 髪の人のこめかみの肌の窓を埋めるため（<see cref="RocketboxHairSwap.SideDepth"/>）
        /// </summary>
        public Vector2 TempleLeft = new Vector2(0.035f, 0.01f), TempleRight = new Vector2(0.035f, 0.01f);

        readonly System.Action<RocketboxPaint.Look> tune;

        /// <summary>服の色（主人公）。<see cref="Look"/> の最後に掛ける。null なら体の人の服のまま</summary>
        public System.Action<RocketboxPaint.Look> Outfit;
        /// <summary>服の色（片割れ）。<see cref="TwinLook"/> で、主人公の見た目の上に掛ける。null なら片割れも主人公と同じ服</summary>
        public System.Action<RocketboxPaint.Look> TwinOutfit;

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
            // 元の模型は口が少し開いていて、斜めから歯が見える。2.5 度で唇が軽く合う（4 度で下唇が潰れ、6 度で上唇を突き抜けた）
            k.jawClose = 2.5f;
        });

        /// <summary>
        /// 女大 08。髪は長い。頭のテクスチャの髪が首の後ろまで続くので、髪と見なす高さを下げる。
        /// 服（灰の T シャツとジーンズ）は元のまま
        /// </summary>
        public static readonly RocketboxPerson Adult08 = new RocketboxPerson("Female_Adult_08", "f008", "女大 08", k =>
        {
            k.blackenKnit = false;
            k.hairLowest = 0.34f;
        })
        {
            // 本人の右のこめかみは肌の窓が目に近く広い。左は髪が流れて窓が狭く、右と同じだけ取ると目尻の横に殻が四角くはみ出した
            TempleRight = new Vector2(0.025f, -0.005f),
            TempleLeft = new Vector2(0.035f, 0.01f),
        };

        /// <summary>女大 08 の頭（長い髪ごと）を、女大 14 の体（黒いカーディガン）に載せた人</summary>
        public static readonly RocketboxPerson Head08Body14 = Compose("Head08_Body14", "女大 08 の頭と女大 14 の体", Adult08, Adult14);

        /// <summary>女大 14 の顔と体に、女大 08 の髪（長さと形ごと）を載せた人</summary>
        public static readonly RocketboxPerson Face14Hair08 = Dress(ComposeHair("Face14_Hair08", "女大 14 の顔と体に女大 08 の髪", Adult14, Adult08, Adult14),
            // 主人公: 都会のモード系。カーディガンと靴を黒、パンツは女大 14 の元のダークデニム、中は白い丸首のシャツ
            // （カーディガンの開きから見える胸の肌と、中のトップスを白く塗る）
            k =>
            {
                k.blackenKnit = true;
                k.recolourShoes = true;
                k.shoeShadow = new Color(0.008f, 0.008f, 0.009f);
                k.shoeShine = new Color(0.100f, 0.098f, 0.100f);
                k.recolourTop = true;
                k.topShadow = new Color(0.80f, 0.80f, 0.78f);
                k.topShine = new Color(0.93f, 0.93f, 0.91f);
                k.shirt = true;
            },
            // 片割れ: 田舎のアースカラー。カーディガンは女大 14 の元の生成りのまま、パンツをオリーブ、靴を茶の革、中のトップスをテラコッタ
            k =>
            {
                k.blackenKnit = false;
                k.recolourPants = true;
                k.pantsShadow = new Color(0.085f, 0.085f, 0.045f);
                k.pantsShine = new Color(0.420f, 0.400f, 0.250f);
                k.recolourShoes = true;
                k.shoeShadow = new Color(0.060f, 0.035f, 0.020f);
                k.shoeShine = new Color(0.380f, 0.240f, 0.140f);
                k.recolourTop = true;
                k.topShadow = new Color(0.110f, 0.055f, 0.035f);
                k.topShine = new Color(0.520f, 0.300f, 0.190f);
            });

        static RocketboxPerson Dress(RocketboxPerson p, System.Action<RocketboxPaint.Look> self, System.Action<RocketboxPaint.Look> twin)
        {
            p.Outfit = self;
            p.TwinOutfit = twin;
            return p;
        }

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
                if (Outfit != null) Outfit(k);
                return k;
            }
            var own = new RocketboxPaint.Look();
            if (tune != null) tune(own);
            if (Outfit != null) Outfit(own);
            return own;
        }

        /// <summary>片割れの見た目。主人公の見た目に <see cref="TwinOutfit"/> を掛ける（顔・髪・黒子は同じ。黒子は模型ごと裏返して右目の下へ）</summary>
        public RocketboxPaint.Look TwinLook()
        {
            var k = Look();
            if (TwinOutfit != null) TwinOutfit(k);
            return k;
        }

        public override string ToString() { return Label + "（" + Name + "）"; }
    }
}
