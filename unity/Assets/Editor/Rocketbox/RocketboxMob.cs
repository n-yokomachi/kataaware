using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 場面 2（路地裏）の群衆・ヤードの売り手・買い手に使う Rocketbox の一人分（<c>docs/superpowers/specs/2026-09-25-alley-mob-design.md</c>）。
    /// 主人公の <see cref="RocketboxPerson"/> と違い、顔や体に手は入れない。取り込み（FBX の写しと縮めたテクスチャ）と、群衆の中での役だけを持つ。
    /// 肌に描くインプラントは置くたびに選ぶ（<see cref="BuildAlleyCrowd"/>）
    /// </summary>
    public sealed class RocketboxMob
    {
        /// <summary>群衆の中での役</summary>
        public enum Role
        {
            /// <summary>通りの人（サイバーパンカー寄り）</summary>
            Street,
            /// <summary>通りの人（ふつうの身なり）</summary>
            Plain,
            /// <summary>ヤードの売り手（出店の奥に座る）</summary>
            Seller,
            /// <summary>買い手だけ（群衆には出さない）</summary>
            Buyer,
        }

        /// <summary>フォルダの名前（Male_Adult_04 など）</summary>
        public readonly string Name;
        /// <summary>一覧の番号での呼び名（男大 04 など）</summary>
        public readonly string Label;
        public readonly Role Part;

        RocketboxMob(string name, string label, Role part)
        {
            Name = name;
            Label = label;
            Part = part;
        }

        public string Dir { get { return RocketboxImport.Root + Name + "/"; } }
        public string Model { get { return Dir + Name + ".fbx"; } }

        string prefix;

        /// <summary>
        /// テクスチャとマテリアルの接頭辞（m006 など。人の番号と違う）。取り込んだ PNG の名前から拾い、無ければ元の TGA の名前から拾う
        /// </summary>
        public string Prefix
        {
            get
            {
                if (prefix != null) return prefix;
                prefix = Find(Path.Combine(RocketboxTextures.ProjectRoot, Dir), "_body_color.png")
                    ?? Find(Path.Combine(RocketboxTextures.ProjectRoot, RocketboxTextures.RawDir, Name), "_body_color.tga");
                if (prefix == null) throw new FileNotFoundException("体のテクスチャが無い（取り込みも元のファイルも無い）: " + Name);
                return prefix;
            }
        }

        static string Find(string dir, string suffix)
        {
            if (!Directory.Exists(dir)) return null;
            foreach (var f in Directory.GetFiles(dir, "*" + suffix))
            {
                var n = Path.GetFileName(f);
                return n.Substring(0, n.Length - suffix.Length);
            }
            return null;
        }

        /// <summary>髪の房の透けのテクスチャを持つか（持たない人は、髪が頭の面に描かれている）</summary>
        public bool HasOpacity
        {
            get
            {
                return File.Exists(Path.Combine(RocketboxTextures.ProjectRoot, HairSrc))
                    || File.Exists(Path.Combine(RocketboxTextures.ProjectRoot, RocketboxTextures.RawDir, Name, Prefix + "_opacity_color.tga"));
            }
        }

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

        /// <summary>買い手か（寄りで見るので、テクスチャを大きく残す）</summary>
        public bool IsBuyer { get { return Array.IndexOf(Buyers, this) >= 0; } }

        /// <summary>
        /// 取り込むテクスチャの大きさ。群衆は 256、寄りで見る売り手と買い手は 512。
        /// 主人公の組み立て（<see cref="RocketboxPerson"/>）が同じ人の 512 の写しを使っている人（女大 02・03・19）は 512 のまま
        /// </summary>
        public int SourceSize { get { return Part == Role.Seller || IsBuyer || Shared ? 512 : 256; } }

        /// <summary>主人公の組み立てが同じ人のテクスチャを使っているか</summary>
        public bool Shared
        {
            get
            {
                foreach (var p in RocketboxPerson.Sources)
                    if (p.Name == Name) return true;
                return false;
            }
        }

        // ---- 人 ------------------------------------------------------------------

        static RocketboxMob Street(string name, string label) { return new RocketboxMob(name, label, Role.Street); }
        static RocketboxMob Plain(string name, string label) { return new RocketboxMob(name, label, Role.Plain); }
        static RocketboxMob Seller(string name, string label) { return new RocketboxMob(name, label, Role.Seller); }

        /// <summary>女大 15。通りの人（ふつうの身なり）で、買い手 C でもある</summary>
        public static readonly RocketboxMob Adult15F = Plain("Female_Adult_15", "女大 15");
        /// <summary>男大 14。買い手 A（若い女の記憶を欲しがる常連）</summary>
        public static readonly RocketboxMob Adult14M = new RocketboxMob("Male_Adult_14", "男大 14", Role.Buyer);
        /// <summary>男大 02。買い手 B（煙草で払う常連）</summary>
        public static readonly RocketboxMob Adult02M = new RocketboxMob("Male_Adult_02", "男大 02", Role.Buyer);

        /// <summary>通りとヤードの人。サイバーパンカー寄り 16 人と、ふつうの身なり 11 人（設計メモ 2 節）</summary>
        public static readonly RocketboxMob[] Crowd =
        {
            Street("Female_Adult_04", "女大 04"),
            Street("Female_Adult_12", "女大 12"),
            Street("Female_Adult_07", "女大 07"),
            Street("Female_Adult_03", "女大 03"),
            Street("Female_Adult_13", "女大 13"),
            Street("Female_Adult_17", "女大 17"),
            Street("Female_Party_02", "女大 19"),
            Street("Male_Adult_04", "男大 04"),
            Street("Male_Adult_09", "男大 09"),
            Street("Male_Adult_10", "男大 10"),
            Street("Male_Adult_17", "男大 17"),
            Street("Male_Adult_18", "男大 18"),
            Street("Male_Adult_12", "男大 12"),
            Street("Male_Adult_07", "男大 07"),
            Street("Male_Adult_11", "男大 11"),
            Street("Delivery_Male_01", "配達の男"),
            Plain("Female_Adult_01", "女大 01"),
            Plain("Female_Adult_05", "女大 05"),
            Plain("Female_Adult_09", "女大 09"),
            Adult15F,
            Plain("Male_Adult_01", "男大 01"),
            Plain("Male_Adult_06", "男大 06"),
            Plain("Male_Adult_08", "男大 08"),
            Plain("Male_Adult_16", "男大 16"),
            Plain("Male_Adult_20", "男大 20"),
            Plain("Business_Male_07", "会社員の男 07"),
            Plain("Business_Female_03", "会社員の女 03"),
        };

        /// <summary>ヤードの売り手（出店の奥に座る）</summary>
        public static readonly RocketboxMob[] Sellers =
        {
            Seller("Male_Adult_03", "男大 03"),
            Seller("Male_Adult_05", "男大 05"),
            Seller("Male_Adult_13", "男大 13"),
            Seller("Female_Adult_02", "女大 02"),
            Seller("Wood_Male_01", "木工の男"),
        };

        /// <summary>買い手 A・B・C（シナリオ 6.4 の順。A と B は男、C は女）</summary>
        public static readonly RocketboxMob[] Buyers = { Adult14M, Adult02M, Adult15F };

        /// <summary>取り込む人の全部</summary>
        public static IEnumerable<RocketboxMob> All
        {
            get
            {
                foreach (var w in Crowd) yield return w;
                foreach (var w in Sellers) yield return w;
                yield return Adult14M;
                yield return Adult02M;
            }
        }

        // ---- 取り込み ----------------------------------------------------------

        [MenuItem("HalfAware/Alley crowd/Import the people", false, 300)]
        public static void ImportMenu()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var who in All) sb.Append(Import(who));
            AssetDatabase.SaveAssets();
            Debug.Log("路地裏の人を取り込んだ\n" + sb);
        }

        /// <summary>
        /// 一人分を取り込む。FBX は RawAssets から Assets/Models/rocketbox/{Name}/ へ写し（取り込みの設定は <see cref="RocketboxImport"/>）、
        /// テクスチャは <see cref="SourceSize"/> の PNG へ縮めて同じ所に置く。
        /// 主人公の組み立てが使っている 512 の写しは縮め直さない（無い物だけ 512 で作る）。元が無ければ例外（落とすのはオーナーの許しが要る）
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
            var names = new List<string>();
            foreach (var n in who.RawTextures)
                if (!who.Shared || !File.Exists(Path.Combine(RocketboxTextures.ProjectRoot, who.Dir + n + ".png"))) names.Add(n);
            var tex = names.Count > 0 ? RocketboxTextures.Shrink(who.Name, names.ToArray(), who.SourceSize) : "テクスチャは主人公の組み立ての写しを使う\n";
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
