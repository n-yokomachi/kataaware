using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    public static partial class PlaceProtagonist
    {
        // ---- 端末の画面に映る口元 ----------------------------------------------
        //
        // 場面 1 の独白「こうして反射で自分の顔が見られるからだ」で、端末の黒い画面に主人公の顔が映る。
        // 主人公の性別は対面まで見せないので、映すのは口元（鼻の下から顎まで、口元のほくろを含む）だけ。
        // 主人公の体を正面から撮って口元を切り出し、画面に映った向きに左右を返して、暗く色を抜いた絵にする。
        // 出し入れと置き場は TerminalReflection が受け持つ

        /// <summary>映り込みの絵と、それを貼るマテリアル</summary>
        public const string ReflectionTexture = "Assets/Textures/TerminalReflection.png";
        public const string ReflectionMaterial = "Assets/Materials/Room/TerminalReflection.mat";
        /// <summary>映り込みの板の名前。端末（調べる対象）の子に置く</summary>
        public const string ReflectionName = "TerminalReflection";

        /// <summary>切り出す口元の、両目の真ん中からの下がり（上の縁は鼻の下、下の縁は顎の先。m）</summary>
        const float MouthTop = 0.046f;
        const float MouthBottom = 0.104f;
        /// <summary>切り出す口元の幅の半分（m）。口の端から頬の下まで</summary>
        const float MouthHalf = 0.05f;
        /// <summary>
        /// 色の残し方（0 で灰色、1 で元の色）と明るさ。黒い画面への映り込みらしく暗く、色を抜く。
        /// 唇の赤みが残ると、口元だけでも女性らしさが立つ
        /// </summary>
        const float MouthSaturation = 0.25f;
        const float MouthBrightness = 0.5f;
        /// <summary>縁をぼかす幅（楕円の半径を 1 とした幅）。黒い画面に溶かす</summary>
        const float MouthFeather = 0.45f;
        /// <summary>
        /// 映り込みを本物の大きさの何倍に拡げるか。鏡に映った顔は、画面の上では本物の半分の大きさになる。
        /// 席から画面まで 1.15 m あるので、本物のままでは口元が画面の上で 5 px ほどにしかならない
        /// </summary>
        const float MouthScale = 3f;
        /// <summary>焼く絵の大きさ（px）。ゲームの画面では 20 px ほどに映るので、この大きさで間に合う</summary>
        const int MouthWide = 160;
        const int MouthHigh = 96;

        [MenuItem("HalfAware/Put the terminal reflection in the room", false, 206)]
        public static void ReflectionMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RoomPath) { Debug.LogError("端末の映り込みは場面 1（Room.unity）だけ。今開いているのは " + scene.path); return; }
            if (scene.isDirty) { Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + scene.path); return; }
            var before = Snapshot();
            var note = new StringBuilder();
            var ok = Reflection(note);
            var after = Snapshot();
            var diff = Diff(before, after, out var unexpected);
            note.AppendLine("場面の中の物の差（Player/Protagonist の下を除く）:").Append(diff);
            if (!ok || unexpected > 0)
            {
                Debug.LogError("端末の映り込みを置いたが、組み立ての外の物が " + unexpected + " 個変わった（または組み立てに失敗した）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("端末の映り込みを置いて保存した: " + scene.path + "\n" + note);
        }

        /// <summary>口元の絵を焼き、端末の子に映り込みの板を置いて繋ぐ</summary>
        public static bool Reflection(StringBuilder note)
        {
            Interactable terminal = null;
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (it.Id == "terminal") terminal = it;
            if (terminal == null) { note.AppendLine("端末（terminal）の調べる対象が無い"); return false; }
            var screen = ScreenNear(terminal.transform.position);
            if (screen == null) { note.AppendLine("端末の画面（Room/Monitors の Face）が無い"); return false; }
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { note.AppendLine("SceneFlow が無い"); return false; }

            var texture = BakeMouth(note);
            if (texture == null) return false;
            var material = ReflectionMat(texture);

            var t = terminal.transform.Find(ReflectionName);
            var go = t != null ? t.gameObject : new GameObject(ReflectionName);
            go.transform.SetParent(terminal.transform, false);
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // 独白の 3 行目までは伏せておく。TerminalReflection が起こす
            mr.enabled = false;

            // 鏡に映った口元は本物の半分の大きさで、目が映る所から、目と口元の真ん中の下がりの半分だけ下に来る
            var size = new Vector2(MouthHalf * 2f, MouthBottom - MouthTop) * 0.5f * MouthScale;
            var drop = (MouthTop + MouthBottom) * 0.5f * 0.5f * MouthScale;
            var reflection = go.GetComponent<TerminalReflection>();
            if (reflection == null) reflection = go.AddComponent<TerminalReflection>();
            var so = new SerializedObject(reflection);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("source").objectReferenceValue = terminal;
            so.FindProperty("fromLine").intValue = 2;
            so.FindProperty("screen").objectReferenceValue = screen;
            so.FindProperty("screenSize").vector2Value = new Vector2(screen.lossyScale.x, screen.lossyScale.y);
            so.FindProperty("screenThick").floatValue = screen.lossyScale.z;
            so.FindProperty("size").vector2Value = size;
            so.FindProperty("drop").floatValue = drop;
            so.FindProperty("face").objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();

            // エディタで見たときの置き場は、座った目から見た所にしておく（再生すると毎フレーム置き直す）
            var player = flow.Player != null ? flow.Player.transform : null;
            var eyeY = new SerializedObject(flow).FindProperty("seatEyeHeight").floatValue;
            var eye = player != null ? player.position + Vector3.up * eyeY + player.forward * EyeLead(player) : screen.position - screen.forward;
            var d = eye - screen.position;
            var spot = TerminalReflection.Spot(new Vector2(Vector3.Dot(d, screen.right), Vector3.Dot(d, screen.up)), drop,
                new Vector2(screen.lossyScale.x, screen.lossyScale.y), size);
            go.transform.SetPositionAndRotation(
                screen.position + screen.right * spot.x + screen.up * spot.y - screen.forward * (screen.lossyScale.z * 0.5f + 0.002f),
                screen.rotation);
            var parent = terminal.transform.lossyScale;
            go.transform.localScale = new Vector3(size.x / parent.x, size.y / parent.y, 1f);
            note.AppendFormat("端末の映り込み: 画面 {0}、口元の板 {1:0.000} × {2:0.000} m（本物の {3} 倍）、目の映る所から {4:0.000} m 下",
                screen.parent != null ? screen.parent.name : screen.name, size.x, size.y, MouthScale, drop).AppendLine();
            return true;
        }

        /// <summary>点にいちばん近い、モニターの画面の板</summary>
        static Transform ScreenNear(Vector3 at)
        {
            var monitors = GameObject.Find("Room/Monitors");
            if (monitors == null) return null;
            Transform best = null;
            var near = float.MaxValue;
            foreach (var t in monitors.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Face") continue;
                var d = Vector3.Distance(t.position, at);
                if (d < near) { near = d; best = t; }
            }
            return best;
        }

        /// <summary>
        /// 主人公の体を正面から撮り、口元を切り出して、映り込みの絵にして置く。
        /// 別の場面（プレビューの場面）で撮るので、開いている場面の灯りも物も写らない
        /// </summary>
        static Texture2D BakeMouth(StringBuilder note)
        {
            const int shot = 1024;
            const float ortho = 0.10f;
            var preview = new PreviewRenderUtility();
            var holder = new GameObject("MouthBake");
            Color[] pixels;
            try
            {
                var her = BuildRocketboxProtagonist.Build(holder.transform, false);
                var an = her.GetComponent<Animator>();
                BodyPoser.Stand(an);
                an.enabled = false;
                var eyes = BodyPoser.Eyes(an);
                var forward = her.transform.forward;
                var centre = eyes + Vector3.down * ((MouthTop + MouthBottom) * 0.5f);
                preview.AddSingleGO(holder);
                preview.BeginStaticPreview(new Rect(0, 0, shot, shot));
                var cam = preview.camera;
                cam.orthographic = true;
                cam.orthographicSize = ortho;
                cam.transform.SetPositionAndRotation(centre + forward * 0.5f, Quaternion.LookRotation(-forward, Vector3.up));
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                // 画面の明かりに照らされた顔。正面のやや上から
                preview.lights[0].intensity = 1.1f;
                preview.lights[0].color = Color.white;
                preview.lights[0].transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.35f);
                preview.lights[1].intensity = 0.3f;
                preview.ambientColor = new Color(0.22f, 0.22f, 0.22f);
                preview.Render(true);
                var tex = (Texture2D)preview.EndStaticPreview();
                pixels = tex.GetPixels();
                Object.DestroyImmediate(tex);
            }
            finally
            {
                preview.Cleanup();
                Object.DestroyImmediate(holder);
            }

            // 撮った絵の 1 px は 2 × ortho / shot m。口元の枠を切り出し、左右を返して縮める
            var perPixel = 2f * ortho / shot;
            var halfW = MouthHalf / perPixel;
            var halfH = (MouthBottom - MouthTop) * 0.5f / perPixel;
            var outPx = new Color[MouthWide * MouthHigh];
            for (var y = 0; y < MouthHigh; y++)
                for (var x = 0; x < MouthWide; x++)
                {
                    var u = (x + 0.5f) / MouthWide;
                    var v = (y + 0.5f) / MouthHigh;
                    // 画面に映った向き: 本人の左（撮った絵の右）が、見る人の左に来る
                    var sx = shot * 0.5f + (0.5f - u) * 2f * halfW;
                    var sy = shot * 0.5f + (v - 0.5f) * 2f * halfH;
                    var c = Sample(pixels, shot, sx, sy);
                    var grey = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    var tint = Color.Lerp(new Color(grey, grey, grey), c, MouthSaturation) * MouthBrightness;
                    // 楕円の縁でぼかす
                    var ex = (u - 0.5f) * 2f;
                    var ey = (v - 0.5f) * 2f;
                    var r = Mathf.Sqrt(ex * ex + ey * ey);
                    var alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - MouthFeather, 1f, r));
                    outPx[y * MouthWide + x] = new Color(tint.r * 0.94f, tint.g * 0.97f, tint.b, alpha);
                }
            var made = new Texture2D(MouthWide, MouthHigh, TextureFormat.RGBA32, false);
            made.SetPixels(outPx);
            made.Apply();
            System.IO.File.WriteAllBytes(ReflectionTexture, made.EncodeToPNG());
            Object.DestroyImmediate(made);
            AssetDatabase.ImportAsset(ReflectionTexture, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ReflectionTexture);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            note.AppendFormat("口元の絵: {0}（{1} × {2}）", ReflectionTexture, MouthWide, MouthHigh).AppendLine();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ReflectionTexture);
        }

        /// <summary>撮った絵の色を双線形で読む。y は下から</summary>
        static Color Sample(Color[] px, int size, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0f, size - 1.001f);
            y = Mathf.Clamp(y - 0.5f, 0f, size - 1.001f);
            var x0 = (int)x;
            var y0 = (int)y;
            var fx = x - x0;
            var fy = y - y0;
            var a = Color.Lerp(px[y0 * size + x0], px[y0 * size + x0 + 1], fx);
            var b = Color.Lerp(px[(y0 + 1) * size + x0], px[(y0 + 1) * size + x0 + 1], fx);
            return Color.Lerp(a, b, fy);
        }

        /// <summary>映り込みのマテリアル。光を受けない半透明の板に、口元の絵を貼る</summary>
        static Material ReflectionMat(Texture2D texture)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ReflectionMaterial);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "TerminalReflection" };
                AssetDatabase.CreateAsset(m, ReflectionMaterial);
            }
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
