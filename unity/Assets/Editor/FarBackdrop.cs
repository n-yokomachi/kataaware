using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 書き割りの輪の寸法と置き場。寸法は場所のローカル。
    /// 場面 4 の場所（<c>BuildDive*Far.cs</c>）と村（<c>BuildVillageFar.cs</c>）が持つ
    /// </summary>
    public sealed class FarRing
    {
        /// <summary>輪の中心。y は使わない</summary>
        public Vector3 Centre;
        /// <summary>撮る目の高さ</summary>
        public float Eye;
        /// <summary>中心から板の辺まで</summary>
        public float Radius = 200f;
        /// <summary>本物の地面の遠い縁。中心から辺まで。絵の中の街並みはここより外にだけ置く</summary>
        public float Ground = 80f;
        public int Panels = 16;
        /// <summary>板の上端。絵の中の一番高い物が収まる高さ</summary>
        public float Top;
        /// <summary>撮った絵と、その絵を貼るマテリアル</summary>
        public string Picture;
        public string Material;
        /// <summary>輪の mesh の名前</summary>
        public string Name;

        /// <summary>
        /// 板の下端。絵の中で地面の遠い縁が落ちる線（<see cref="BackdropRing.GroundLine"/>）より 10 m 下げる。
        /// 中心より高い目から見ると本物の地面の縁が線より下に見えるので、
        /// 板をそこで切ると、縁と板の間から空が覗く
        /// </summary>
        public float Bottom
        {
            get { return BackdropRing.GroundLine(Eye, Ground, Radius) - 10f; }
        }
    }

    /// <summary>
    /// 遠景の書き割りの、場所に依らない道具（場面 4 の設計書 9.1 節「遠景の書き割り」）。
    ///
    /// 撮って貼る仕組みは公営住宅で決めた（<c>BuildDiveEstateFar.cs</c> の冒頭）。
    /// 見る位置を中心にした正多角形の板の輪へ、一枚ごとにその板とぴったり同じ視錐台で撮った切れ端を貼る。
    /// 板は霞を受けず（<c>HalfAware/Backdrop</c>）、空は板に写さない（黒と白の背景で二回撮り、差の出た画素を抜く）。
    ///
    /// もとは場面 4 の中（<c>BuildDiveFar.cs</c>）にあり、場所の id から場所と空を引いていた。
    /// 村でも使うので、置く・撮る・抜く・書き出すをここへ切り出し、場所（Transform）と空（<see cref="PlaceSky"/>）を
    /// 呼ぶ側から受け取る形にした。場所と空を撮るあいだだけ整える（Stage）のは呼ぶ側の仕事
    /// </summary>
    public static class FarBackdrop
    {
        /// <summary>一枚ぶんの横と縦の画素。縦は上下に余白を持たせた一段の高さ。絵は 8 枚ずつ二段に並べる</summary>
        public const int TileWide = 256;
        public const int TileHigh = 224;
        public const int RowHigh = 256;
        /// <summary>一段の上下の余白。mipmap が小さくなっても隣の段の地面が滲まないように</summary>
        public const int Pad = (RowHigh - TileHigh) / 2;
        public const int PerRow = 8;
        /// <summary>撮るときに縦横これだけ細かく撮って、縮めて縁を滑らかにする</summary>
        public const int Fine = 2;

        /// <summary>i 番の板の絵の uv の範囲（左下と右上）</summary>
        public static Rect Uv(FarRing ring, int i)
        {
            var row = i / PerRow;
            var col = i % PerRow;
            var wide = TileWide * PerRow;
            var high = RowHigh * (ring.Panels / PerRow);
            return Rect.MinMaxRect(
                col * TileWide / (float)wide,
                (row * RowHigh + Pad) / (float)high,
                (col + 1) * TileWide / (float)wide,
                (row * RowHigh + Pad + TileHigh) / (float)high);
        }

        // ---- 置く ----------------------------------------------------------------

        /// <summary>
        /// 書き割りの輪を置く。撮った絵が無ければ置かずに一行だけ知らせる（場面 2 の Backdrop と同じ）。
        /// mesh は <paramref name="generated"/> へ書く
        /// </summary>
        public static GameObject Place(Transform parent, FarRing ring, string shootMenu, string generated)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ring.Picture);
            if (tex == null)
            {
                Debug.Log("遠景の絵がまだ無い: " + ring.Picture + "。" + shootMenu + " で撮る");
                return null;
            }
            var bank = new Bank { Texel = 1f };
            var bottom = ring.Bottom;
            for (var i = 0; i < ring.Panels; i++)
            {
                var c = BackdropRing.Corners(ring.Centre, ring.Radius, ring.Panels, i, bottom, ring.Top);
                var uv = Uv(ring, i);
                bank.Patch(c[0], c[1], c[2], c[3],
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin),
                    new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax));
            }
            var made = bank.Emit(parent, ring.Name, Mat(ring, tex), false, generated);
            if (made == null) return null;
            var r = made.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return made;
        }

        /// <summary>板のマテリアル。霞を受けない HalfAware/Backdrop に絵を結ぶ</summary>
        public static Material Mat(FarRing ring, Texture2D tex)
        {
            var shader = Shader.Find("HalfAware/Backdrop");
            var m = AssetDatabase.LoadAssetAtPath<Material>(ring.Material);
            if (m == null)
            {
                m = new Material(shader);
                m.name = Path.GetFileNameWithoutExtension(ring.Material);
                AssetDatabase.CreateAsset(m, ring.Material);
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
        public static Vector2[] Rim(FarRing ring)
        {
            var rim = new Vector2[ring.Panels];
            var reach = BackdropRing.Corner(ring.Ground, ring.Panels);
            for (var k = 0; k < ring.Panels; k++)
            {
                // 角は板の縁の向き。角度の減る向きに回すと、上から見て左回りになる
                var az = BackdropRing.Azimuth(ring.Panels - k, ring.Panels) - 180f / ring.Panels;
                var at = ring.Centre + BackdropRing.Heading(az) * reach;
                rim[k] = new Vector2(at.x, at.z);
            }
            return rim;
        }

        /// <summary>
        /// 本物の遠い地面。輪と同じ中心・同じ向きの正多角形で切る。
        /// そうすると撮った絵の中の地面の始まりが、どの板でも同じ高さの一本の線になり、
        /// 中心から見て本物の地面の縁とちょうど繋がる。書き割りの地面も同じマテリアルで撮る。
        /// 影は落とさない
        /// </summary>
        public static GameObject Land(Transform parent, FarRing ring, string name, Material mat, string generated, bool rooted = false)
        {
            // 麦畑の地のように HalfAware/Wheat で塗るときは、読まれる uv1 を空にしない（rooted）
            var land = new Bank { Texel = 0.35f, Rooted = rooted, RootY = 0f, RootHigh = 1f };
            land.FanY(new Vector3(ring.Centre.x, -0.05f, ring.Centre.z), Rim(ring));
            var made = land.Emit(parent, name, mat, false, generated);
            if (made != null) made.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return made;
        }

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 輪の板を全部撮って一枚に並べる。呼ぶ側が場所と空を整えている間に呼ぶ。
        /// <paramref name="place"/> の下のレンダラーは撮るあいだ伏せ、灯りだけ残す。
        /// <paramref name="build"/> は撮る物を組む手順で、作った物を list へ入れて返す。捨てるのはここ。
        ///
        /// **一回の呼び出しの中で始めから終わりまで済ませ、全部を元に戻してから返る。**
        /// 伏せた mesh・組んだ街並み・後処理のつまみを途中の状態で残さない
        /// </summary>
        public static Texture2D Take(Transform place, PlaceSky sky, FarRing ring,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            var hidden = new List<Renderer>();
            var made = new List<Object>();
            var keepBlur = Shader.GetGlobalFloat("_DazeBlur");
            var keepWobble = Shader.GetGlobalFloat("_DazeWobble");
            GameObject eyeGo = null;
            try
            {
                // 場所の形は伏せ、灯りだけ残す。撮るのは本物の地面の縁より外の街並みだけ
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

                eyeGo = new GameObject("FarEye");
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

                var wide = TileWide * PerRow;
                var high = RowHigh * (ring.Panels / PerRow);
                var sheet = new Texture2D(wide, high, TextureFormat.RGBA32, false, false);
                var fill = sky.hazeColor;
                var blank = new Color32(Byte(fill.r), Byte(fill.g), Byte(fill.b), 0);
                var all = new Color32[wide * high];
                for (var i = 0; i < all.Length; i++) all[i] = blank;
                sheet.SetPixels32(all);

                var fw = TileWide * Fine;
                var fh = TileHigh * Fine;
                var centre = place.TransformPoint(new Vector3(ring.Centre.x, ring.Eye, ring.Centre.z));
                var proj = BackdropRing.Frustum(ring.Radius, ring.Panels, ring.Bottom, ring.Top, ring.Eye, near, far);
                for (var i = 0; i < ring.Panels; i++)
                {
                    eyeGo.transform.position = centre;
                    eyeGo.transform.rotation = place.rotation * BackdropRing.Facing(i, ring.Panels);
                    cam.aspect = fw / (float)fh;
                    cam.projectionMatrix = proj;
                    cam.backgroundColor = Color.black;
                    var dark = CheckDiveSky.Grab(cam, fw, fh, false);
                    cam.backgroundColor = Color.white;
                    var light = CheckDiveSky.Grab(cam, fw, fh, false);
                    var tile = Cut(dark, light, fw, fh, blank);
                    Object.DestroyImmediate(dark);
                    Object.DestroyImmediate(light);
                    var col = i % PerRow;
                    var row = i / PerRow;
                    sheet.SetPixels32(col * TileWide, row * RowHigh + Pad, TileWide, TileHigh, tile);
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
        /// 撮った絵を ring.Picture へ書き出して取り込む。絵は捨てる。
        /// 取り込みの値は場面 2 の StreetBackdrop に揃え、抜いた所を切るのに要る分だけ変える
        /// </summary>
        public static void Write(FarRing ring, Texture2D picture)
        {
            var dir = Path.GetDirectoryName(ring.Picture).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
            File.WriteAllBytes(ring.Picture, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(ring.Picture);
            var importer = AssetImporter.GetAtPath(ring.Picture) as TextureImporter;
            if (importer == null) return;
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
        public static Color32[] Cut(Texture2D dark, Texture2D light, int fw, int fh, Color32 blank)
        {
            var d = dark.GetPixels32();
            var l = light.GetPixels32();
            var w = fw / Fine;
            var h = fh / Fine;
            var tile = new Color32[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var j = 0; j < Fine; j++)
                        for (var i = 0; i < Fine; i++)
                        {
                            var k = (y * Fine + j) * fw + x * Fine + i;
                            var gap = Mathf.Max(l[k].r - d[k].r, Mathf.Max(l[k].g - d[k].g, l[k].b - d[k].b));
                            var cover = 1f - Mathf.Clamp01(gap / 255f);
                            a += cover;
                            // 黒の背景で撮った色は、掛かった分だけ色を持つ
                            r += d[k].r;
                            g += d[k].g;
                            b += d[k].b;
                        }
                    var n = Fine * Fine;
                    if (a <= 1e-3f) { tile[y * w + x] = blank; continue; }
                    tile[y * w + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / n * 255f), 0, 255));
                }
            return tile;
        }

        static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }
    }
}
