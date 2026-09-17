using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 主人公を組み立てる。素体は Quaternius の Ultimate Modular Women（Suit）で、
    /// そこに顔の絵を貼る面と、革のライダースの襟・襟返し・ジッパー・ベルトを重ねる。
    /// 素体には UV が無いので、顔は前にかぶせた面の方に UV を持たせて描く。
    /// 手で組むと再生を抜けるたびに消えるので、ここに手順として残す。
    /// </summary>
    public static class BuildProtagonist
    {
        public const string SourceModel = "Assets/Models/quaternius/W_Suit.fbx";
        public const string FacePlateMesh = "Assets/Models/generated/FacePlate.asset";
        public const string FaceMaterial = "Assets/Materials/Room/Face.mat";
        public const string Controller = "Assets/Animation/Protagonist.controller";

        static readonly Color Hair = new Color(0.045f, 0.042f, 0.050f);
        static readonly Color Jacket = new Color(0.055f, 0.053f, 0.062f);
        static readonly Color Shirt = new Color(0.80f, 0.80f, 0.82f);

        [MenuItem("HalfAware/Build the protagonist")]
        public static void BuildMenu()
        {
            var go = Build(null);
            Selection.activeGameObject = go;
        }

        /// <summary>組み立てて返す。parent が null なら場面の根に置く</summary>
        public static GameObject Build(Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (src == null) { Debug.LogError("素体が見つからない: " + SourceModel); return null; }
            var her = (GameObject)Object.Instantiate(src, parent);
            her.name = "Protagonist";
            her.transform.localPosition = Vector3.zero;
            her.transform.localRotation = Quaternion.identity;

            Recolour(her);
            AddFacePlate(her);
            AddRidersJacket(her);
            AddAnimator(her);
            return her;
        }

        /// <summary>
        /// 立ちと歩きの動き。進むのは CharacterController の仕事なので、
        /// 動作そのものに体を運ばせない。姿は自分の足元しか映らないが、
        /// 画面の外に出ても骨は回し続けさせる
        /// </summary>
        static void AddAnimator(GameObject her)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            if (ctrl == null) { Debug.LogWarning("動作の状態機械が見つからない: " + Controller); return; }
            var an = her.GetComponent<Animator>();
            if (an == null) an = her.AddComponent<Animator>();
            an.runtimeAnimatorController = ctrl;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.updateMode = AnimatorUpdateMode.Normal;
        }

        /// <summary>髪を黒、上着を革の黒、シャツを白へ。眉と目は顔の絵に任せるので肌で塗り潰す</summary>
        static void Recolour(GameObject her)
        {
            Material skin = null;
            foreach (var r in her.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name == "Skin") skin = m;

            foreach (var r in her.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                var next = new Material[ms.Length];
                for (var i = 0; i < ms.Length; i++)
                {
                    var n = ms[i] == null ? "" : ms[i].name;
                    if (n == "Hair_Blond") next[i] = Tint(ms[i], Hair, 0f, 0.30f);
                    else if (n == "Hair_Brown" || n == "Brown") next[i] = skin;
                    else if (n == "Black") next[i] = Tint(ms[i], Jacket, 0.02f, 0.52f);
                    else if (n == "White") next[i] = Tint(ms[i], Shirt, 0f, 0.18f);
                    else next[i] = ms[i];
                }
                r.sharedMaterials = next;
            }
        }

        static Material Tint(Material src, Color col, float metal, float smooth)
        {
            var m = new Material(src);
            m.name = src.name + " (protagonist)";
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>顔の絵を貼る面。頭の骨に付けて追従させる。骨は 100 倍なので入れ物で打ち消す</summary>
        static void AddFacePlate(GameObject her)
        {
            var head = FindBone(her, "Head");
            if (head == null) { Debug.LogWarning("頭の骨が見つからない"); return; }
            var anchor = new GameObject("FacePlate");
            anchor.transform.SetParent(head, false);
            anchor.transform.localScale = Vector3.one * 0.01f;
            // 素体の安静時、顔の中心はここ
            anchor.transform.position = her.transform.TransformPoint(new Vector3(0.014f, 1.633f, 0f));
            anchor.transform.rotation = her.transform.rotation;
            anchor.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FacePlateMesh);
            anchor.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FaceMaterial);
        }

        /// <summary>襟・襟返し・ジッパー・ベルト。胸の骨に付ける</summary>
        static void AddRidersJacket(GameObject her)
        {
            var chest = FindBone(her, "Chest");
            if (chest == null) { Debug.LogWarning("胸の骨が見つからない"); return; }
            var leather = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Leather.mat");
            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Steel.mat");

            var rig = new GameObject("Riders");
            rig.transform.SetParent(chest, false);
            rig.transform.localScale = Vector3.one * 0.01f;
            rig.transform.position = chest.position;
            rig.transform.rotation = her.transform.rotation;

            var neck = new Vector3(0f, 0.132f, 0.019f);

            var rings = new List<ProcMesh.Ring>();
            const int steps = 15;
            for (var i = 0; i <= steps; i++)
            {
                var rad = Mathf.Lerp(-148f, 148f, (float)i / steps) * Mathf.Deg2Rad;
                var pos = neck + new Vector3(Mathf.Sin(rad) * 0.070f, 0.006f * Mathf.Cos(rad * 0.5f), Mathf.Cos(rad) * 0.070f);
                var tangent = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
                var up = Quaternion.AngleAxis(-34f, tangent) * Vector3.up;
                var h = Mathf.Lerp(0.030f, 0.021f, Mathf.Abs(Mathf.Cos(rad)));
                rings.Add(new ProcMesh.Ring(pos, 0.007f, h, Quaternion.LookRotation(tangent, up)));
            }
            Attach(rig.transform, "Collar", ProcMesh.Loft(rings, 6), leather);

            Attach(rig.transform, "LapelL",
                Flap(neck + new Vector3(0.062f, 0.004f, 0.050f), neck + new Vector3(0.028f, -0.150f, 0.082f), 0.030f, -52f), leather);
            Attach(rig.transform, "LapelR",
                Flap(neck + new Vector3(-0.062f, 0.004f, 0.050f), neck + new Vector3(-0.044f, -0.150f, 0.080f), 0.030f, 52f), leather);

            Slab(rig.transform, "Zip", new Vector3(-0.030f, -0.075f, 0.098f), new Vector3(4f, 0f, 6f), new Vector3(0.014f, 0.185f, 0.010f), steel);
            Slab(rig.transform, "ZipPull", new Vector3(-0.036f, -0.165f, 0.100f), new Vector3(4f, 0f, 6f), new Vector3(0.011f, 0.024f, 0.008f), steel);

            var belt = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 18; i++)
            {
                var rad = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / 18);
                var pos = new Vector3(Mathf.Sin(rad) * 0.108f, -0.262f, 0.024f + Mathf.Cos(rad) * 0.080f);
                var tangent = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
                belt.Add(new ProcMesh.Ring(pos, 0.008f, 0.020f, Quaternion.LookRotation(tangent, Vector3.up)));
            }
            Attach(rig.transform, "Belt", ProcMesh.Loft(belt, 6, false, false), leather);
            Slab(rig.transform, "Buckle", new Vector3(-0.012f, -0.262f, 0.106f), Vector3.zero, new Vector3(0.036f, 0.026f, 0.012f), steel);
            Slab(rig.transform, "StudL", new Vector3(0.052f, -0.012f, 0.108f), Vector3.zero, new Vector3(0.010f, 0.010f, 0.008f), steel);
            Slab(rig.transform, "StudR", new Vector3(-0.052f, -0.012f, 0.108f), Vector3.zero, new Vector3(0.010f, 0.010f, 0.008f), steel);
        }

        static Mesh Flap(Vector3 a, Vector3 b, float w, float tilt)
        {
            var dir = (b - a).normalized;
            var rot = Quaternion.LookRotation(dir, Quaternion.AngleAxis(tilt, dir) * Vector3.up);
            var rr = new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(a, 0.006f, w, rot),
                new ProcMesh.Ring(Vector3.Lerp(a, b, 0.55f), 0.006f, w * 0.86f, rot),
                new ProcMesh.Ring(b, 0.005f, w * 0.40f, rot),
            };
            return ProcMesh.Loft(rr, 6);
        }

        static void Attach(Transform parent, string name, Mesh mesh, Material mat)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void Slab(Transform parent, string name, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        static Transform FindBone(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == name) return t;
            return null;
        }
    }
}
