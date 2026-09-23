using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>印を持つ面。renderer の slot 番目のマテリアルを、撮るときだけ印の絵へ差し替える</summary>
    public struct MaskSlot
    {
        public Renderer renderer;
        public int slot;
        /// <summary>顔の UV の上の印。R = ほくろ、G = 虹彩、B = 他の顔の部品、地は灰 0.15。α は元の絵と同じ</summary>
        public Texture mask;

        public MaskSlot(Renderer renderer, int slot, Texture mask)
        {
            this.renderer = renderer;
            this.slot = slot;
            this.mask = mask;
        }
    }

    /// <summary>
    /// 撮影台に載せる頭。どんな模型でも、これを満たせば同じ条件で撮れる。
    /// 位置は Root の中の値で持ち、ワールドへはその場で直す（鏡像にするとき Root の x を裏返すため）
    /// </summary>
    public class FaceSubject : IDisposable
    {
        public GameObject Root;
        /// <summary>顔の表面の中心（鼻筋の上、目と口の中ほど）。Root の中</summary>
        public Vector3 FaceCentreLocal;
        /// <summary>目の高さの顔の中心。顔の幅はこの高さで測る。Root の中</summary>
        public Vector3 EyeLineLocal;
        public Vector3 ForwardLocal = Vector3.forward;
        /// <summary>目の高さの肌の左右の広がり。m</summary>
        public float FaceWidth;
        public readonly List<MaskSlot> MaskSlots = new List<MaskSlot>();

        /// <summary>
        /// ほくろを消した絵へ差し替えるマテリアル（renderer の slot 番目を material へ）。
        /// 同じ構図をほくろ有りと無しで撮り比べ、ほくろで変わる画素を数えるのに使う。無ければ比べない
        /// </summary>
        public readonly List<KeyValuePair<MaskSlot, Material>> MoleOff = new List<KeyValuePair<MaskSlot, Material>>();

        /// <summary>ほくろを消した絵へ差し替え、戻すための元のマテリアルの並びを返す</summary>
        public Dictionary<Renderer, Material[]> HideMole()
        {
            var keep = new Dictionary<Renderer, Material[]>();
            foreach (var kv in MoleOff)
            {
                var r = kv.Key.renderer;
                if (r == null) continue;
                if (!keep.ContainsKey(r)) keep[r] = r.sharedMaterials;
                var ms = r.sharedMaterials;
                ms[kv.Key.slot] = kv.Value;
                r.sharedMaterials = ms;
            }
            return keep;
        }

        public static void Restore(Dictionary<Renderer, Material[]> keep)
        {
            foreach (var kv in keep) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
        }
        /// <summary>壊すときに一緒に壊す、その場で作ったもの（マテリアル・メッシュ）</summary>
        public readonly List<Object> Made = new List<Object>();
        public string Note = "";

        public Vector3 FaceCentre { get { return Root.transform.TransformPoint(FaceCentreLocal); } }
        public Vector3 EyeLine { get { return Root.transform.TransformPoint(EyeLineLocal); } }
        public Vector3 Forward { get { return Root.transform.TransformDirection(ForwardLocal).normalized; } }
        public Vector3 Right { get { return Vector3.Cross(Vector3.up, Forward).normalized; } }

        /// <summary>三角の数とテクスチャの大きさ</summary>
        public string Describe()
        {
            long tris = 0;
            var faceTris = 0L;
            var texes = new HashSet<Texture>();
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) mesh = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh != null)
                {
                    var n = 0L;
                    for (var s = 0; s < mesh.subMeshCount; s++) n += mesh.GetIndexCount(s) / 3;
                    tris += n;
                    foreach (var ms in MaskSlots) if (ms.renderer == r) { faceTris += n; break; }
                }
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    foreach (var id in new[] { "_BaseMap", "_EmissionMap", "_BumpMap" })
                        if (m.HasProperty(id) && m.GetTexture(id) != null) texes.Add(m.GetTexture(id));
                }
            }
            var sb = new StringBuilder();
            sb.AppendFormat("三角 {0}（顔の面 {1}）テクスチャ", tris, faceTris);
            if (texes.Count == 0) sb.Append(" 無し");
            foreach (var t in texes) sb.AppendFormat(" {0} {1}×{2}", t.name, t.width, t.height);
            if (!string.IsNullOrEmpty(Note)) sb.Append(" / " + Note);
            return sb.ToString();
        }

        /// <summary>頭のすべての面の光の受け方を、場面の焼いた光から切り離す</summary>
        public void Isolate()
        {
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
            {
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
            }
        }

        public static void HideAll(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>レンダラーが持つ、アセットでないマテリアル（組み立ての途中で作られた写し）を壊すものへ加える</summary>
        public void AdoptLooseMaterials()
        {
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !EditorUtility.IsPersistent(m) && !Made.Contains(m))
                    {
                        m.hideFlags = HideFlags.HideAndDontSave;
                        Made.Add(m);
                    }
        }

        public virtual void Dispose()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            foreach (var o in Made) if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            Made.Clear();
        }
    }
}
