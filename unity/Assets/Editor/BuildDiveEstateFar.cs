using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    /// 差の出た画素を空として抜く。板の上には本物の空が見える
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
        /// <summary>
        /// 板の下端。絵の中で地面の遠い縁が落ちる線（<see cref="BackdropRing.GroundLine"/>）より 10 m 下げる。
        /// 中心より高い目（三階の廊下）から見ると本物の地面の縁が線より下に見えるので、
        /// 板をそこで切ると、縁と板の間から空が覗く
        /// </summary>
        public static float EstateFarBottom
        {
            get { return BackdropRing.GroundLine(EstateFarEye, EstateFarGround, EstateFarRadius) - 10f; }
        }

        /// <summary>撮った絵。16 枚を 8 枚ずつ二段に並べる</summary>
        const string EstateFarPath = DiveTextures + "EstateBackdrop.png";
        const string EstateFarMatPath = Materials + "EstateBackdrop.mat";
        /// <summary>一枚ぶんの横と縦の画素。縦は上下に余白を持たせた一段の高さ</summary>
        const int EstateFarTileWide = 256;
        const int EstateFarTileHigh = 224;
        const int EstateFarRowHigh = 256;
        /// <summary>一段の上下の余白。mipmap が小さくなっても隣の段の地面が滲まないように</summary>
        const int EstateFarPad = (EstateFarRowHigh - EstateFarTileHigh) / 2;
        const int EstateFarPerRow = 8;
        /// <summary>撮るときに縦横これだけ細かく撮って、縮めて縁を滑らかにする</summary>
        const int EstateFarFine = 2;

        /// <summary>i 番の板の絵の uv の範囲（左下と右上）</summary>
        static Rect EstateFarUv(int i)
        {
            var row = i / EstateFarPerRow;
            var col = i % EstateFarPerRow;
            var wide = EstateFarTileWide * EstateFarPerRow;
            var high = EstateFarRowHigh * (EstateFarPanels / EstateFarPerRow);
            return Rect.MinMaxRect(
                col * EstateFarTileWide / (float)wide,
                (row * EstateFarRowHigh + EstateFarPad) / (float)high,
                (col + 1) * EstateFarTileWide / (float)wide,
                (row * EstateFarRowHigh + EstateFarPad + EstateFarTileHigh) / (float)high);
        }

        // ---- 置く ----------------------------------------------------------------

        /// <summary>
        /// 書き割りの輪を置く。撮った絵が無ければ置かずに一行だけ知らせる（場面 2 の Backdrop と同じ）
        /// </summary>
        static void EstateBackdrop(Transform place)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EstateFarPath);
            if (tex == null)
            {
                Debug.Log("団地の遠景の絵がまだ無い。HalfAware/Shoot the estate backdrop で撮る");
                return;
            }
            var ring = new Bank { Texel = 1f };
            var bottom = EstateFarBottom;
            for (var i = 0; i < EstateFarPanels; i++)
            {
                var c = BackdropRing.Corners(EstateFarCentre, EstateFarRadius, EstateFarPanels, i, bottom, EstateFarTop);
                var uv = EstateFarUv(i);
                ring.Patch(c[0], c[1], c[2], c[3],
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin),
                    new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax));
            }
            var made = ring.Emit(place, "EstateBackdrop", EstateFarMat(tex), false, Generated);
            if (made == null) return;
            var r = made.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// 遠い地面のマテリアル。本物の地面（80 m まで）と、書き割りの中の地面の両方に使う。
        /// 同じマテリアルで撮るので、縁の所で色が揃う
        /// </summary>
        static Material EstateLandMat()
        {
            return Glow("EstateLand", new Color(0.60f, 0.62f, 0.53f), 0.52f);
        }

        static Material EstateFarMat(Texture2D tex)
        {
            var shader = Shader.Find("HalfAware/Backdrop");
            var m = AssetDatabase.LoadAssetAtPath<Material>(EstateFarMatPath);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "EstateBackdrop";
                AssetDatabase.CreateAsset(m, EstateFarMatPath);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 本物の地面の遠い縁。書き割りの輪と同じ中心・同じ向きの正多角形の角。
        /// 上から見て左回り（<see cref="Bank.FanY"/> の決まり）
        /// </summary>
        static Vector2[] EstateFarRim()
        {
            var rim = new Vector2[EstateFarPanels];
            var reach = BackdropRing.Corner(EstateFarGround, EstateFarPanels);
            for (var k = 0; k < EstateFarPanels; k++)
            {
                // 角は板の縁の向き。角度の減る向きに回すと、上から見て左回りになる
                var az = BackdropRing.Azimuth(EstateFarPanels - k, EstateFarPanels) - 180f / EstateFarPanels;
                var at = EstateFarCentre + BackdropRing.Heading(az) * reach;
                rim[k] = new Vector2(at.x, at.z);
            }
            return rim;
        }

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮る。撮るためだけの街並みを組み、団地の空・霞・日・環境光のまま 16 枚を撮り、
        /// 一枚の絵に並べて書き出す。
        ///
        /// **一回の呼び出しの中で始めから終わりまで済ませ、全部を元に戻してから返る。**
        /// 同じエディタで別の担当が同時に測っているので、場所・記憶・RenderSettings・
        /// 組んだ街並み・伏せた mesh を途中の状態で残さない。
        /// 輪そのものは置き直さない。置くのは `HalfAware/Build the dive`
        /// </summary>
        [MenuItem("HalfAware/Shoot the estate backdrop", false, 251)]
        public static void ShootEstateBackdrop()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("再生中とコンパイル中は撮らない");
                return;
            }
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            if (place == null)
            {
                Debug.LogError("団地がまだ組まれていない。先に HalfAware/Build the dive");
                return;
            }
            var sky = EstatePlaceSky(place);

            Texture2D picture;
            using (new CheckDiveSky.Stage(DiveIds.Estate))
                picture = EstateFarShoot(place, sky, EstateTown);
            if (picture == null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            File.WriteAllBytes(EstateFarPath, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(EstateFarPath);
            var importer = AssetImporter.GetAtPath(EstateFarPath) as TextureImporter;
            if (importer != null)
            {
                // 場面 2 の StreetBackdrop に揃える。抜いた所を切るのに要る分だけ変える
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                // mipmap の段が小さくなっても、切る境（0.5）で残る面積を保つ。保たないと遠くで輪郭が痩せる
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            Debug.Log("団地の遠景を撮った: " + EstateFarPath + "。HalfAware/Build the dive で輪に貼る");
        }

        /// <summary>
        /// 16 枚を撮って一枚に並べる。呼ぶ側が <see cref="CheckDiveSky.Stage"/> で場所と空を持っている間に呼ぶ。
        /// <paramref name="build"/> は撮る物を組む手順（ふだんは <see cref="EstateTown"/>）。
        /// 測る道具（<see cref="CheckDiveSky"/>）は、地面だけを塗り分けた物を渡して境目の線を撮る
        /// </summary>
        internal static Texture2D EstateFarShoot(Transform place, PlaceSky sky,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            var hidden = new List<Renderer>();
            var made = new List<Object>();
            var keepBlur = Shader.GetGlobalFloat("_DazeBlur");
            var keepWobble = Shader.GetGlobalFloat("_DazeWobble");
            GameObject eyeGo = null;
            try
            {
                // 団地の形は伏せ、灯りだけ残す。撮るのは 80 m より外の街並みだけ
                foreach (var r in place.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hidden.Add(r);
                }
                sky.Apply(null);
                // 眩暈の後処理は素通しにしておく。エディタではふつう 0 だが、残っていれば絵がぶれる
                Shader.SetGlobalFloat("_DazeBlur", 0f);
                Shader.SetGlobalFloat("_DazeWobble", 0f);

                var town = build(place, made);
                made.Add(town);

                eyeGo = new GameObject("EstateFarEye");
                eyeGo.hideFlags = HideFlags.HideAndDontSave;
                var cam = eyeGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.cullingMask = ~0;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                const float near = 5f;
                const float far = 3000f;
                cam.nearClipPlane = near;
                cam.farClipPlane = far;

                var wide = EstateFarTileWide * EstateFarPerRow;
                var high = EstateFarRowHigh * (EstateFarPanels / EstateFarPerRow);
                var sheet = new Texture2D(wide, high, TextureFormat.RGBA32, false, false);
                var fill = sky.hazeColor;
                var blank = new Color32(Byte(fill.r), Byte(fill.g), Byte(fill.b), 0);
                var all = new Color32[wide * high];
                for (var i = 0; i < all.Length; i++) all[i] = blank;
                sheet.SetPixels32(all);

                var fw = EstateFarTileWide * EstateFarFine;
                var fh = EstateFarTileHigh * EstateFarFine;
                var centre = place.TransformPoint(new Vector3(EstateFarCentre.x, EstateFarEye, EstateFarCentre.z));
                var proj = BackdropRing.Frustum(EstateFarRadius, EstateFarPanels, EstateFarBottom, EstateFarTop,
                    EstateFarEye, near, far);
                for (var i = 0; i < EstateFarPanels; i++)
                {
                    eyeGo.transform.position = centre;
                    eyeGo.transform.rotation = place.rotation * BackdropRing.Facing(i, EstateFarPanels);
                    cam.aspect = fw / (float)fh;
                    cam.projectionMatrix = proj;
                    cam.backgroundColor = Color.black;
                    var dark = CheckDiveSky.Grab(cam, fw, fh, false);
                    cam.backgroundColor = Color.white;
                    var light = CheckDiveSky.Grab(cam, fw, fh, false);
                    var tile = EstateFarCut(dark, light, fw, fh, blank);
                    Object.DestroyImmediate(dark);
                    Object.DestroyImmediate(light);
                    var col = i % EstateFarPerRow;
                    var row = i / EstateFarPerRow;
                    sheet.SetPixels32(col * EstateFarTileWide, row * EstateFarRowHigh + EstateFarPad,
                        EstateFarTileWide, EstateFarTileHigh, tile);
                }
                sheet.Apply();
                return sheet;
            }
            finally
            {
                if (eyeGo != null) Object.DestroyImmediate(eyeGo);
                for (var i = made.Count - 1; i >= 0; i--)
                    if (made[i] != null) Object.DestroyImmediate(made[i]);
                foreach (var r in hidden) if (r != null) r.enabled = true;
                Shader.SetGlobalFloat("_DazeBlur", keepBlur);
                Shader.SetGlobalFloat("_DazeWobble", keepWobble);
            }
        }

        /// <summary>
        /// 黒と白の背景で撮った二枚から、空を抜いた一枚を作る。細かく撮った分を縮めながら。
        ///
        /// **色の一致で抜かない。** 霞に沈んだ遠い棟は空の色とほとんど同じで、色で見ると
        /// 棟ごと抜ける。背景を黒と白に替えて差が出た画素だけが空、という抜き方なら、
        /// 霞にも縁の色にも左右されない。
        ///
        /// 縁で半分だけ掛かった画素は、掛かった分の割合で色を戻す。抜いた所の色は霞の色で埋める。
        /// 板は α の境で切るので、縁の色は隣の霞の色と混ざって、空へ溶けるように見える
        /// </summary>
        static Color32[] EstateFarCut(Texture2D dark, Texture2D light, int fw, int fh, Color32 blank)
        {
            var d = dark.GetPixels32();
            var l = light.GetPixels32();
            var w = fw / EstateFarFine;
            var h = fh / EstateFarFine;
            var tile = new Color32[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var j = 0; j < EstateFarFine; j++)
                        for (var i = 0; i < EstateFarFine; i++)
                        {
                            var k = (y * EstateFarFine + j) * fw + x * EstateFarFine + i;
                            var gap = Mathf.Max(l[k].r - d[k].r, Mathf.Max(l[k].g - d[k].g, l[k].b - d[k].b));
                            var cover = 1f - Mathf.Clamp01(gap / 255f);
                            a += cover;
                            // 黒の背景で撮った色は、掛かった分だけ色を持つ
                            r += d[k].r;
                            g += d[k].g;
                            b += d[k].b;
                        }
                    var n = EstateFarFine * EstateFarFine;
                    if (a <= 1e-3f) { tile[y * w + x] = blank; continue; }
                    tile[y * w + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / n * 255f), 0, 255));
                }
            return tile;
        }
    }
}
