using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の遠景の書き割りの、場所をまたいで使う道具（設計書 9.1 節「遠景の書き割り」）。
    ///
    /// 撮って貼る仕組みは公営住宅で決めた（<c>BuildDiveEstateFar.cs</c> の冒頭）。
    /// 見る位置を中心にした正多角形の板の輪へ、一枚ごとにその板とぴったり同じ視錐台で撮った切れ端を貼る。
    /// 板は霞を受けず（<c>HalfAware/Backdrop</c>）、空は板に写さない（黒と白の背景で二回撮り、差の出た画素を抜く）。
    ///
    /// 公園も同じ仕組みで撮るので、輪の寸法（<see cref="FarRing"/>）を場所ごとに持たせ、
    /// 置く・撮る・抜くはここで分け合う。撮る街並みは場所ごとに組む（<c>EstateTown</c>・<c>ParkTown</c>）
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>
        /// 書き割りの輪の寸法と置き場。寸法は場所のローカル
        /// </summary>
        sealed class FarRing
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

        /// <summary>一枚ぶんの横と縦の画素。縦は上下に余白を持たせた一段の高さ。絵は 8 枚ずつ二段に並べる</summary>
        const int FarTileWide = 256;
        const int FarTileHigh = 224;
        const int FarRowHigh = 256;
        /// <summary>一段の上下の余白。mipmap が小さくなっても隣の段の地面が滲まないように</summary>
        const int FarPad = (FarRowHigh - FarTileHigh) / 2;
        const int FarPerRow = 8;
        /// <summary>撮るときに縦横これだけ細かく撮って、縮めて縁を滑らかにする</summary>
        const int FarFine = 2;

        /// <summary>i 番の板の絵の uv の範囲（左下と右上）</summary>
        static Rect FarUv(FarRing ring, int i)
        {
            var row = i / FarPerRow;
            var col = i % FarPerRow;
            var wide = FarTileWide * FarPerRow;
            var high = FarRowHigh * (ring.Panels / FarPerRow);
            return Rect.MinMaxRect(
                col * FarTileWide / (float)wide,
                (row * FarRowHigh + FarPad) / (float)high,
                (col + 1) * FarTileWide / (float)wide,
                (row * FarRowHigh + FarPad + FarTileHigh) / (float)high);
        }

        // ---- 置く ----------------------------------------------------------------

        /// <summary>
        /// 書き割りの輪を置く。撮った絵が無ければ置かずに一行だけ知らせる（場面 2 の Backdrop と同じ）
        /// </summary>
        static void Backdrop(Transform place, FarRing ring, string shootMenu)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ring.Picture);
            if (tex == null)
            {
                Debug.Log("遠景の絵がまだ無い: " + ring.Picture + "。" + shootMenu + " で撮る");
                return;
            }
            var bank = new Bank { Texel = 1f };
            var bottom = ring.Bottom;
            for (var i = 0; i < ring.Panels; i++)
            {
                var c = BackdropRing.Corners(ring.Centre, ring.Radius, ring.Panels, i, bottom, ring.Top);
                var uv = FarUv(ring, i);
                bank.Patch(c[0], c[1], c[2], c[3],
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin),
                    new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax));
            }
            var made = bank.Emit(place, ring.Name, FarMat(ring, tex), false, Generated);
            if (made == null) return;
            var r = made.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        static Material FarMat(FarRing ring, Texture2D tex)
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
        static Vector2[] FarRim(FarRing ring)
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
        /// 中心から見て本物の地面の縁とちょうど繋がる。書き割りの地面も同じマテリアルで撮る
        /// </summary>
        static void FarLand(Transform place, FarRing ring, string name, Material mat)
        {
            var land = new Bank { Texel = 0.35f };
            land.FanY(new Vector3(ring.Centre.x, -0.05f, ring.Centre.z), FarRim(ring));
            NoShadow(EstateEmit(place, name, land, mat, false));
        }

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮って絵に書き出す。撮るためだけの街並みを <paramref name="build"/> で組み、場所の空・霞・日・環境光のまま
        /// 輪の板を一枚ずつ撮り、一枚の絵に並べて書き出す。
        ///
        /// **一回の呼び出しの中で始めから終わりまで済ませ、全部を元に戻してから返る。**
        /// 同じエディタで別の担当が同時に測っているので、場所・記憶・RenderSettings・
        /// 組んだ街並み・伏せた mesh を途中の状態で残さない。
        /// 輪そのものは置き直さない。置くのは `HalfAware/Build the dive`
        /// </summary>
        static void ShootBackdrop(string placeId, FarRing ring, System.Func<Transform, PlaceSky> skyOf,
            System.Func<Transform, List<Object>, GameObject> build)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("再生中とコンパイル中は撮らない");
                return;
            }
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + placeId) : null;
            if (place == null)
            {
                Debug.LogError(placeId + " がまだ組まれていない。先に HalfAware/Build the dive");
                return;
            }
            var sky = skyOf(place);

            Texture2D picture;
            using (new CheckDiveSky.Stage(placeId))
                picture = FarShoot(place, sky, ring, build);
            if (picture == null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            File.WriteAllBytes(ring.Picture, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(ring.Picture);
            var importer = AssetImporter.GetAtPath(ring.Picture) as TextureImporter;
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
            Debug.Log("遠景を撮った: " + ring.Picture + "。HalfAware/Build the dive で輪に貼る");
        }

        /// <summary>
        /// 輪の板を全部撮って一枚に並べる。呼ぶ側が <see cref="CheckDiveSky.Stage"/> で場所と空を持っている間に呼ぶ。
        /// <paramref name="build"/> は撮る物を組む手順。
        /// 測る道具（<see cref="CheckDiveSky"/>）は、地面だけを塗り分けた物を渡して境目の線を撮る
        /// </summary>
        static Texture2D FarShoot(Transform place, PlaceSky sky, FarRing ring,
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

                var wide = FarTileWide * FarPerRow;
                var high = FarRowHigh * (ring.Panels / FarPerRow);
                var sheet = new Texture2D(wide, high, TextureFormat.RGBA32, false, false);
                var fill = sky.hazeColor;
                var blank = new Color32(Byte(fill.r), Byte(fill.g), Byte(fill.b), 0);
                var all = new Color32[wide * high];
                for (var i = 0; i < all.Length; i++) all[i] = blank;
                sheet.SetPixels32(all);

                var fw = FarTileWide * FarFine;
                var fh = FarTileHigh * FarFine;
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
                    var tile = FarCut(dark, light, fw, fh, blank);
                    Object.DestroyImmediate(dark);
                    Object.DestroyImmediate(light);
                    var col = i % FarPerRow;
                    var row = i / FarPerRow;
                    sheet.SetPixels32(col * FarTileWide, row * FarRowHigh + FarPad, FarTileWide, FarTileHigh, tile);
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
        static Color32[] FarCut(Texture2D dark, Texture2D light, int fw, int fh, Color32 blank)
        {
            var d = dark.GetPixels32();
            var l = light.GetPixels32();
            var w = fw / FarFine;
            var h = fh / FarFine;
            var tile = new Color32[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var j = 0; j < FarFine; j++)
                        for (var i = 0; i < FarFine; i++)
                        {
                            var k = (y * FarFine + j) * fw + x * FarFine + i;
                            var gap = Mathf.Max(l[k].r - d[k].r, Mathf.Max(l[k].g - d[k].g, l[k].b - d[k].b));
                            var cover = 1f - Mathf.Clamp01(gap / 255f);
                            a += cover;
                            // 黒の背景で撮った色は、掛かった分だけ色を持つ
                            r += d[k].r;
                            g += d[k].g;
                            b += d[k].b;
                        }
                    var n = FarFine * FarFine;
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
