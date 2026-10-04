using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Microsoft Rocketbox の模型とテクスチャの取り込みの設定。Assets/Models/rocketbox/ の下にだけ掛かる。
    ///
    /// Rocketbox に同梱の FixRocketboxMaxImport.cs はプロジェクトの全部の取り込みに掛かり、
    /// Quaternius の模型を取り込むたびに例外を出すので使わない。要る直しだけをここに書く。
    ///
    /// - 模型: Humanoid（骨は 3ds Max の Biped）。付いてくる動き・カメラ・灯りは読まない。
    ///   マテリアルは名前だけを取り込み、中身は組み立て（<see cref="BuildRocketboxProtagonist"/>）で縮めたテクスチャから作る
    /// - テクスチャ: 512 以下。sRGB、ミップマップ、双線形、繰り返し無し
    /// </summary>
    public sealed class RocketboxImport : AssetPostprocessor
    {
        public const string Root = "Assets/Models/rocketbox/";

        /// <summary>取り込みの設定を替えたら数を上げる（替えた設定で取り込み直させるため）</summary>
        public override uint GetVersion() { return 1; }

        public static bool Ours(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(Root, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            // 組み立てのときに頭のメッシュの写し（黒子の面の位置合わせ・鏡像）を読むので読めるようにする
            mi.isReadable = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.optimizeGameObjects = false;
        }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = Mathf.Min(ti.maxTextureSize, 512);
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.isReadable = false;
        }
    }
}
