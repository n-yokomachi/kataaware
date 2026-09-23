using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 二つ目の道（主人公と片割れだけ別の、もっと細かい模型）を、一つ目の道と同じ撮影台で撮るための指定。
    ///
    /// 使い方（模型をオーナーの許しを得て取り込んだ後）:
    /// 1. Assets/Study/Face/ で右クリック → Create → HalfAware → Study → Face head spec
    /// 2. model に模型を入れ、顔が +z を向き、身長がおよそ 1.7〜1.8 m になるよう euler と scale を合わせる
    /// 3. faceCentre（鼻筋の上、目と口の中ほどの肌の表面）と eyeLine（目の高さの顔の中心）を模型の根の中の m で入れ、
    ///    faceWidth に目の高さの肌の幅を入れる
    /// 4. 顔の絵を持つ面を slots に書く。ほくろ入りの絵（主人公・片割れ）・ほくろ無しの絵・印の絵は、
    ///    <see cref="Stamp"/> で元の顔の絵から起こせる（ほくろと虹彩の UV の位置を渡す）
    /// 5. <c>FaceStudy.ShootSet(() =&gt; FaceHeadSpec.Build(path, twin), tag, light)</c> と
    ///    <c>FaceStudy.ShootMirror(…)</c> で、段 0〜3 と同じ条件で撮って測る
    /// </summary>
    [CreateAssetMenu(menuName = "HalfAware/Study/Face head spec")]
    public sealed class FaceHeadSpec : ScriptableObject
    {
        [Serializable]
        public struct Slot
        {
            [Tooltip("顔の絵を持つレンダラーの名前（模型の中の GameObject の名前）")]
            public string renderer;
            [Tooltip("そのレンダラーのマテリアルの番号")]
            public int slot;
            [Tooltip("主人公（ほくろが左目の下）のマテリアル。空なら模型のまま")]
            public Material self;
            [Tooltip("片割れ（ほくろが右目の下）のマテリアル。空なら self")]
            public Material twin;
            [Tooltip("ほくろ無しのマテリアル。ほくろで変わる画素を数えるのに使う。空なら比べない")]
            public Material noMole;
            [Tooltip("主人公の印の絵（R = ほくろ、G = 虹彩、B = 他の部品、地は灰 0.15）")]
            public Texture2D maskSelf;
            [Tooltip("片割れの印の絵。空なら maskSelf")]
            public Texture2D maskTwin;
        }

        public GameObject model;
        public float scale = 1f;
        public Vector3 euler;
        [Tooltip("顔の表面の中心。模型の根の中（倍率を掛ける前）の m")]
        public Vector3 faceCentre;
        [Tooltip("目の高さの顔の中心。模型の根の中（倍率を掛ける前）の m")]
        public Vector3 eyeLine;
        [Tooltip("目の高さの肌の左右の広がり（倍率を掛けた後の m）")]
        public float faceWidth = 0.15f;
        [Tooltip("顔の正面。模型の根の中")]
        public Vector3 forward = Vector3.forward;
        public Slot[] slots = new Slot[0];

        /// <summary>指定から頭を組み立てる。壊すのは呼んだ側（using）</summary>
        public static FaceSubject Build(string specPath, bool twin)
        {
            var spec = AssetDatabase.LoadAssetAtPath<FaceHeadSpec>(specPath);
            if (spec == null || spec.model == null) throw new FileNotFoundException("指定か模型が無い: " + specPath);
            var holder = new GameObject("FaceStudyHead");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = FaceStudy.Origin;
            var who = new FaceSubject { Root = holder };
            try
            {
                var go = (GameObject)Object.Instantiate(spec.model, holder.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(spec.euler);
                go.transform.localScale = Vector3.one * spec.scale;
                FaceSubject.HideAll(holder);
                foreach (var s in spec.slots)
                {
                    Renderer r = null;
                    foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) if (rr.name == s.renderer) r = rr;
                    if (r == null) throw new InvalidOperationException("レンダラーが無い: " + s.renderer);
                    var ms = r.sharedMaterials;
                    var mat = twin && s.twin != null ? s.twin : s.self;
                    if (mat != null) { ms[s.slot] = mat; r.sharedMaterials = ms; }
                    var mask = twin && s.maskTwin != null ? s.maskTwin : s.maskSelf;
                    if (mask != null) who.MaskSlots.Add(new MaskSlot(r, s.slot, mask));
                    if (s.noMole != null) who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(r, s.slot, null), s.noMole));
                }
                var t = go.transform;
                who.FaceCentreLocal = holder.transform.InverseTransformPoint(t.TransformPoint(spec.faceCentre));
                who.EyeLineLocal = holder.transform.InverseTransformPoint(t.TransformPoint(spec.eyeLine));
                who.ForwardLocal = holder.transform.InverseTransformDirection(t.TransformDirection(spec.forward));
                who.FaceWidth = spec.faceWidth;
                who.Note = "二つ目の道: " + spec.name;
                who.Isolate();
                return who;
            }
            catch
            {
                who.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 元の顔の絵から、主人公・片割れ・ほくろ無しの絵と、主人公・片割れの印の絵を起こして PNG で書く。
        /// 位置と半径は UV（0〜1）。ほくろは色 (0.24, 0.14, 0.10) の縁の柔らかい円、
        /// 虹彩は印の絵にだけ円で入れる（虹彩の色そのものは元の絵か模型の目のマテリアルで決める）。
        /// 書き出し先は <c>{outPrefix}_self.png</c> などで、読み込みの設定は段 0〜3 と同じ
        /// </summary>
        public static string Stamp(string albedoPath, string outPrefix, Vector2 moleSelf, Vector2 moleTwin, float moleR,
            Vector2[] irises, float irisR)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(albedoPath));
            try
            {
                int w = src.width, h = src.height;
                var px = src.GetPixels32();
                var moleCol = new Color(0.24f, 0.14f, 0.10f);
                Func<Vector2, Color32[]> withMole = at =>
                {
                    var o = (Color32[])px.Clone();
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var d = Vector2.Distance(new Vector2((x + 0.5f) / w, (y + 0.5f) / h), at) / moleR;
                            if (d >= 1.15f) continue;
                            var a = Mathf.Clamp01((1.15f - d) / 0.3f);
                            var k = y * w + x;
                            o[k] = Color.Lerp(o[k], new Color(moleCol.r, moleCol.g, moleCol.b, ((Color)o[k]).a), a);
                        }
                    return o;
                };
                Func<Vector2, Color32[]> mask = at =>
                {
                    var o = new Color32[px.Length];
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                            var k = y * w + x;
                            var a = px[k].a;
                            if (Vector2.Distance(uv, at) < moleR) { o[k] = FacePaint.MaskMole(a); continue; }
                            var iris = false;
                            foreach (var c in irises) if (Vector2.Distance(uv, c) < irisR) iris = true;
                            o[k] = iris ? FacePaint.MaskIris(a) : FacePaint.MaskGround(a);
                        }
                    return o;
                };
                FaceStages.EnsureDir();
                FaceStages.SavePng(withMole(moleSelf), w, h, outPrefix + "_self.png", false);
                FaceStages.SavePng(withMole(moleTwin), w, h, outPrefix + "_twin.png", false);
                FaceStages.SavePng(px, w, h, outPrefix + "_nomole.png", false);
                FaceStages.SavePng(mask(moleSelf), w, h, outPrefix + "_mask_self.png", true);
                FaceStages.SavePng(mask(moleTwin), w, h, outPrefix + "_mask_twin.png", true);
                return string.Format("{0}_self / _twin / _nomole / _mask_self / _mask_twin（{1}×{2}）", outPrefix, w, h);
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
        }
    }
}
