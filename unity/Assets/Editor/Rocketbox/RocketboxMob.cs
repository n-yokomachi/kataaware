using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 場面 2（路地裏）の通りの人とヤードの売り手に使う Rocketbox の一人分（<c>docs/superpowers/specs/2026-09-25-alley-mob-design.md</c>）。
    /// 主人公の <see cref="RocketboxPerson"/> と違い、顔や体に手は入れない。取り込み（FBX の写しと縮めたテクスチャ）と、
    /// 肌に描くインプラント（<see cref="RocketboxMobPaint"/>）の選びだけを持つ
    /// </summary>
    public sealed class RocketboxMob
    {
        /// <summary>フォルダの名前（Male_Adult_04 など）</summary>
        public readonly string Name;
        /// <summary>テクスチャとマテリアルの接頭辞（m006 など。人の番号と違うことがある）</summary>
        public readonly string Prefix;
        /// <summary>一覧の番号での呼び名（男大 04 など）</summary>
        public readonly string Label;
        /// <summary>髪の房の透けのテクスチャを持つか（男大 17 と 20 は持たない。髪は頭の面に描かれている）</summary>
        public readonly bool HasOpacity;
        /// <summary>肌に描くインプラント。通りの人は大胆な形を二つか三つ、売り手は小さく目立たない形を一つ</summary>
        public readonly RocketboxMobPaint.Implant[] Implants;

        RocketboxMob(string name, string prefix, string label, bool opacity, params RocketboxMobPaint.Implant[] implants)
        {
            Name = name;
            Prefix = prefix;
            Label = label;
            HasOpacity = opacity;
            Implants = implants;
        }

        public string Dir { get { return RocketboxImport.Root + Name + "/"; } }
        public string Model { get { return Dir + Name + ".fbx"; } }
        public string HeadSrc { get { return Dir + Prefix + "_head_color.png"; } }
        public string BodySrc { get { return Dir + Prefix + "_body_color.png"; } }
        public string HairSrc { get { return Dir + Prefix + "_opacity_color.png"; } }
        /// <summary>FBX の中のマテリアルの名前（面の組を見分ける名前）</summary>
        public string BodySlot { get { return Prefix + "_body"; } }
        public string HeadSlot { get { return Prefix + "_head"; } }
        public string HairSlot { get { return Prefix + "_opacity"; } }

        public string[] RawTextures
        {
            get
            {
                return HasOpacity
                    ? new[] { Prefix + "_head_color", Prefix + "_body_color", Prefix + "_opacity_color" }
                    : new[] { Prefix + "_head_color", Prefix + "_body_color" };
            }
        }

        // ---- 色の試しの 6 人 ------------------------------------------------------

        /// <summary>
        /// 女大 04。通りの人（サイバーパンカー寄り）。茶の革のフード付きの上着、カーキのパンツ、長靴。髪は結い上げて、うなじとこめかみが出ている。
        /// 紫のうなじの差込口から首の両脇を回って首の付け根へ下りる線と、水色の左のこめかみから顎まで。
        /// 光の色は、顔の物を人ごとに違う色（水色・桃色・緑・橙・紫、売り手は山吹）にし、首と手は隣の人と被らない色にした
        /// </summary>
        public static readonly RocketboxMob Adult04F = new RocketboxMob("Female_Adult_04", "f004", "女大 04", true,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.NapePort, false, RocketboxMobPaint.Purple),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.TempleLines, true, RocketboxMobPaint.Cyan));
        /// <summary>
        /// 男大 04。通りの人（サイバーパンカー寄り）。黒い上着に灰のフード、水色の T シャツ。顔と手が出ている。
        /// 桃色の、右目を囲む輪から耳へ伸びる線と、黄の左の手の甲
        /// </summary>
        public static readonly RocketboxMob Adult04M = new RocketboxMob("Male_Adult_04", "m006", "男大 04", true,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.EyeRing, false, RocketboxMobPaint.Pink),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.HandGlow, true, RocketboxMobPaint.Yellow));
        /// <summary>
        /// 男大 17。通りの人（サイバーパンカー寄り）。帽子、青と白のジャージの上着（襟が高い）。透けのテクスチャが無い。
        /// 赤の首を巻く太い帯と、青の右の手の甲、緑の左のこめかみから顎まで
        /// </summary>
        public static readonly RocketboxMob Adult17M = new RocketboxMob("Male_Adult_17", "m022", "男大 17", false,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.NeckRing, false, RocketboxMobPaint.Red),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.HandGlow, false, RocketboxMobPaint.Blue),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.TempleLines, true, RocketboxMobPaint.Green));
        /// <summary>
        /// 女大 01。通りの人（ふつうの身なり）。袖をまくった桃色のシャツとジーンズ。前腕が出ている。
        /// 青緑の左の手首から肘までの板と、橙の右のこめかみから顎まで
        /// </summary>
        public static readonly RocketboxMob Adult01F = new RocketboxMob("Female_Adult_01", "f001", "女大 01", true,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.ForearmPlate, true, RocketboxMobPaint.Teal),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.TempleLines, false, RocketboxMobPaint.Orange));
        /// <summary>
        /// 男大 20。通りの人（ふつうの身なり）。茶のパーカーとジーンズ。透けのテクスチャが無い。
        /// 紫の右のこめかみから顎までと、薔薇色の左の手の甲、水色の首を巻く帯
        /// </summary>
        public static readonly RocketboxMob Adult20M = new RocketboxMob("Male_Adult_20", "m027", "男大 20", false,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.TempleLines, false, RocketboxMobPaint.Purple),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.HandGlow, true, RocketboxMobPaint.Rose),
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.NeckRing, false, RocketboxMobPaint.Cyan));
        /// <summary>男大 03。ヤードの売り手。ツイードの上着に臙脂のシャツ。通りの人ほど派手でない、右のこめかみの短い山吹の線を一つ</summary>
        public static readonly RocketboxMob Adult03M = new RocketboxMob("Male_Adult_03", "m004", "男大 03", true,
            new RocketboxMobPaint.Implant(RocketboxMobPaint.Kind.TempleLines, false, RocketboxMobPaint.Amber, false));

        /// <summary>色の試しの 6 人（通りの 5 人、最後が売り手）</summary>
        public static readonly RocketboxMob[] Trial = { Adult04F, Adult04M, Adult17M, Adult01F, Adult20M, Adult03M };

        // ---- 取り込み ----------------------------------------------------------

        /// <summary>縮めたテクスチャの大きさ。試しは主人公と同じ 512（インプラントの細い線を寄りで確かめるため）。群衆へ広げるときに 256 へ落とす</summary>
        public const int TextureSize = 512;

        [MenuItem("HalfAware/Alley mob trial/Import the six")]
        public static void ImportMenu()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var who in Trial) sb.Append(Import(who));
            Debug.Log("路地裏の試しの 6 人を取り込んだ\n" + sb);
        }

        /// <summary>
        /// 一人分を取り込む。FBX は RawAssets から Assets/Models/rocketbox/{Name}/ へ写し（取り込みの設定は <see cref="RocketboxImport"/>）、
        /// テクスチャは <see cref="TextureSize"/> の PNG へ縮めて同じ所に置く。元が無ければ例外（落とすのはオーナーの許しが要る）
        /// </summary>
        public static string Import(RocketboxMob who)
        {
            var root = RocketboxImport.Root.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(root + "/" + who.Name)) AssetDatabase.CreateFolder(root, who.Name);
            var src = Path.Combine(RocketboxTextures.ProjectRoot, RocketboxTextures.RawDir, who.Name, who.Name + ".fbx");
            if (!File.Exists(src)) throw new FileNotFoundException("元の FBX が無い（RawAssets に落としておく）: " + src);
            var dst = Path.Combine(RocketboxTextures.ProjectRoot, who.Model);
            var copied = !File.Exists(dst) || !Same(src, dst);
            if (copied)
            {
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(who.Model, ImportAssetOptions.ForceUpdate);
            }
            var tex = RocketboxTextures.Shrink(who.Name, who.RawTextures, TextureSize);
            return string.Format("{0}（{1}）: FBX {2}\n{3}", who.Label, who.Name, copied ? "を写した" : "は写してある", tex);
        }

        static bool Same(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            var ba = File.ReadAllBytes(a);
            var bb = File.ReadAllBytes(b);
            for (var i = 0; i < ba.Length; i++)
                if (ba[i] != bb[i]) return false;
            return true;
        }
    }
}
