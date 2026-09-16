# Unity 移行 段階 4-a: 描画の質感 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-unity-port-design.md` の 3 節と `docs/superpowers/specs/2026-09-16-assets-design.md` の 2 節を実装する。画面を 3 分の 1 の解像度で描いて最近傍で拡大し、減色とディザを重ねて PS1 風の粗さを出す。段階 3 で数値だけ動いていた眩暈を画面に出し、場面の色味を Volume で寄せる。シーンの中身（物の配置、調べる対象、HUD）には触らない。

**Architecture:** 後処理は URP の Full Screen Pass Renderer Feature を 2 つ使う。眩暈は後処理の前（`BeforeRenderingPostProcessing`）、減色とディザは後処理の後（`AfterRenderingPostProcessing`）に挿し、色味はその間に入る URP の Color Adjustments が受け持つ。これで設計書の「描画 → 眩暈 → 色味 → 減色とディザ → 出力」の順になる。解像度の拡大は URP の最後の転送で行われるので、ディザは低解像度のまま打たれてから拡大される。眩暈の強さは `DazeVolume` がグローバルのシェーダ変数に毎フレーム入れ、シェーダはそれを読むだけにする。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3（`FullScreenPassRendererFeature`）、手書きの HLSL（Shader Graph は使わない）、MCP for Unity 10.0。

**先に確かめてあること（親セッションが実機で確認済み）:**
- 有効な描画設定は `Assets/Settings/PC_RPAsset.asset`（`m_RenderScale: 1`、`m_UpscalingFilter: 0`）。描画器は `Assets/Settings/PC_Renderer.asset` で、`ScreenSpaceAmbientOcclusion` が 1 つ載っている
- 色空間は Linear、HDR は有効。だから減色はガンマ側に直してから行い、戻す
- `UpscalingFilterSelection` は `Auto=0, Linear=1, Point=2, FSR=3`
- 次の取り込み先と入口はこの版で通る（使い捨てのシェーダで確認済み、警告 0）。`#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"` と `#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"`、`Vert` / `Varyings.texcoord` / `_BlitTexture` / `_BlitMipLevel` / `sampler_LinearClamp` / `_Time.y` / `LinearToSRGB` / `SRGBToLinear`、`float4x4` への動的な添字
- `FullScreenPassRendererFeature` は `passMaterial`、`injectionPoint`、`fetchColorBuffer` を持つ。描画器への登録は、部分資産として足したうえで `m_RendererFeatures` と `m_RendererFeatureMap`（局所 id）の両方を伸ばす必要がある

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` は作らない。`docs/` はこのファイルのチェックボックス以外触らない
- **シーンを変えない**。このプランで触るのは、シェーダ、材質、`DazeVolume.cs`、`PC_RPAsset`、`PC_Renderer`、新しい Volume の設定資産、そしてシーンの `Global Volume` が指す設定資産の差し替え 1 か所だけ
- **シーンを変える前・再生する前に `EditorApplication.isPlaying` を読む**。再生中ならオーナーが遊んでいる可能性があるので勝手に止めず、親セッションに戻す
- シェーダを書いたら `mcp__UnityMCP__refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`）→ `mcp__UnityMCP__read_console`（`types: ["error", "warning"]`）に加えて、次で異常が無いことを確かめる。コンソールに出ないシェーダの誤りを拾うため:

```csharp
var sh = AssetDatabase.LoadAssetAtPath<Shader>("<path>");
if (sh == null) return "not imported";
var count = UnityEditor.ShaderUtil.GetShaderMessageCount(sh);
var report = "isSupported=" + sh.isSupported + " messages=" + count;
if (count > 0)
{
    var list = UnityEditor.ShaderUtil.GetShaderMessages(sh);
    for (int i = 0; i < list.Length && i < 6; i++)
        report += "\n  [" + list[i].severity + "] line " + list[i].line + ": " + list[i].message + " " + list[i].messageDetails;
}
return report;
```

- EditMode テストは `mcp__UnityMCP__run_tests`（`mode: "EditMode"`, `assembly_names: ["HalfAware.Tests.EditMode"]`）→ `mcp__UnityMCP__get_test_job`（`wait_timeout: 60`）。段階 3 の終わりで 60 件通る。このプランはテストを増やさないので、どのタスクの後も 60 件のまま
- `execute_code` は C# 6 の範囲（`out var`・タプル・パターンマッチ・ローカル関数・文字列補間は使わない）。`HalfAware.*` と `UnityEngine.Rendering.*` の型は名前空間つきなら直接書ける
- **フォントのアトラスをコミットに入れない**: `git status` に `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出たら `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻してからコミットする
- **MCP から再生するとき**は、エディタの窓が前面に無いと `Application.runInBackground` が false のままで `Update` が走らない。実行中に `Application.runInBackground = true` を立て、確認の後に false へ戻し、`unity/ProjectSettings/ProjectSettings.asset` に差分が無いことを確かめる

---

## ファイル構成

| パス（`unity/` 以下） | 変更 | 責務 |
|---|---|---|
| `Assets/Shaders/Ps1.shader` | 新規 | 減色とディザ。ガンマ側で量子化して戻す |
| `Assets/Shaders/Daze.shader` | 新規 | 眩暈。ぼかしと輪郭の揺れ。強さはグローバル変数から読む |
| `Assets/Materials/Fx/Ps1.mat` | 新規 | 段階数・ディザ・効き具合の数値を持つ |
| `Assets/Materials/Fx/Daze.mat` | 新規 | ぼかしの半径と揺れの幅を持つ |
| `Assets/Scripts/Fx/DazeVolume.cs` | 変更 | 強さをグローバルのシェーダ変数へ毎フレーム入れる |
| `Assets/Settings/PC_RPAsset.asset` | 変更 | 描画の倍率 1/3、拡大は最近傍 |
| `Assets/Settings/PC_Renderer.asset` | 変更 | 後処理 2 つの登録、環境遮蔽を止める |
| `Assets/Settings/RoomVolumeProfile.asset` | 新規 | 場面 1 の色味 |
| `Assets/Scenes/Room.unity` | 変更 | `Global Volume` が指す設定資産を差し替える 1 か所のみ |

---

### Task 1: 減色とディザ

**Files:**
- Create: `unity/Assets/Shaders/Ps1.shader`
- Create: `unity/Assets/Materials/Fx/Ps1.mat`

- [x] **Step 1: シェーダを書く**

`unity/Assets/Shaders/Ps1.shader`:

```shaderlab
// 減色とディザ。URP の後処理の後に挿し、拡大される前の低解像度のまま打つ
Shader "HalfAware/Ps1"
{
    Properties
    {
        _Levels ("色の段階数", Range(2, 64)) = 32
        _Dither ("ディザの強さ", Range(0, 1)) = 1
        _Amount ("効き具合。0 で素通し", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "Ps1"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Levels;
            float _Dither;
            float _Amount;

            // 4x4 の規則的なディザ。0/16 から 15/16 までを散らした並び
            static const float4x4 BAYER = float4x4(
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0) / 16.0;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, _BlitMipLevel);
                if (_Amount <= 0.0) return src;

                // 量子化はガンマ側で行う。Linear のまま刻むと暗部だけ段差が粗くなる
                float3 gamma = LinearToSRGB(saturate(src.rgb));
                uint2 p = uint2(input.positionCS.xy) & 3;
                // ディザ 0 のときは四捨五入（0.5）、1 のときは画素ごとの閾値
                float offset = lerp(0.5, BAYER[p.y][p.x], saturate(_Dither));
                float steps = max(2.0, _Levels) - 1.0;
                float3 stepped = floor(gamma * steps + offset) / steps;

                float3 result = SRGBToLinear(saturate(stepped));
                return float4(lerp(src.rgb, result, saturate(_Amount)), src.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
```

- [x] **Step 2: 取り込みと組み立てを確かめる**

`refresh_unity` → `read_console`（`types: ["error", "warning"]`）→ 冒頭の注意にあるシェーダの検査を `Assets/Shaders/Ps1.shader` に対して行う。
Expected: コンソール 0 件、`isSupported=True messages=0`。

- [x] **Step 3: 材質を作る**

`execute_code`:

```csharp
var shader = Shader.Find("HalfAware/Ps1");
if (shader == null) return "shader not found";
if (!AssetDatabase.IsValidFolder("Assets/Materials/Fx")) AssetDatabase.CreateFolder("Assets/Materials", "Fx");
var path = "Assets/Materials/Fx/Ps1.mat";
var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
mat.shader = shader;
mat.SetFloat("_Levels", 32f);
mat.SetFloat("_Dither", 1f);
mat.SetFloat("_Amount", 1f);
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();
return path + " levels=" + mat.GetFloat("_Levels") + " dither=" + mat.GetFloat("_Dither") + " amount=" + mat.GetFloat("_Amount");
```

Expected: 戻り値 `Assets/Materials/Fx/Ps1.mat levels=32 dither=1 amount=1`。

- [x] **Step 4: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Shaders unity/Assets/Shaders.meta unity/Assets/Materials unity/Assets/Materials/Fx.meta && git commit -m "feat: add the posterize and dither pass"
```

---

### Task 2: 眩暈

**Files:**
- Create: `unity/Assets/Shaders/Daze.shader`
- Create: `unity/Assets/Materials/Fx/Daze.mat`

- [x] **Step 1: シェーダを書く**

`unity/Assets/Shaders/Daze.shader`:

```shaderlab
// 眩暈。ぼかしと輪郭の揺れ。強さは DazeVolume がグローバルに入れる _DazeBlur / _DazeWobble を読む
Shader "HalfAware/Daze"
{
    Properties
    {
        _BlurRadius ("ぼかしの半径（UV）", Range(0, 0.05)) = 0.012
        _WobbleX ("横の揺れの幅", Range(0, 0.1)) = 0.03
        _WobbleY ("縦の揺れの幅", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "Daze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurRadius;
            float _WobbleX;
            float _WobbleY;

            // DazeVolume が毎フレーム入れる。0 のときは素通しになる
            float _DazeBlur;
            float _DazeWobble;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_DazeBlur <= 0.0 && _DazeWobble <= 0.0)
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                float t = _Time.y;
                uv.x += _DazeWobble * _WobbleX * sin(uv.y * 14.0 + t * 2.5);
                uv.y += _DazeWobble * _WobbleY * sin(uv.x * 11.0 + t * 1.9);

                float r = _DazeBlur * _BlurRadius;
                float4 sum = 0.0;
                [unroll]
                for (int i = -2; i <= 2; i++)
                {
                    [unroll]
                    for (int j = -2; j <= 2; j++)
                    {
                        float2 at = uv + float2(i, j) * r;
                        sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, at, _BlitMipLevel);
                    }
                }
                return sum / 25.0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
```

揺れと半径の数値は試作（`prototype-three/src/fx/daze-pass.ts`）と同じ。ただし試作は画面いっぱいの解像度で掛けていたのに対し、ここは 3 分の 1 の上で掛かるので、見え方が強すぎるか弱すぎるかは Task 6 で見て決める。

- [x] **Step 2: 取り込みと組み立てを確かめる**

`refresh_unity` → `read_console` → シェーダの検査を `Assets/Shaders/Daze.shader` に対して行う。
Expected: コンソール 0 件、`isSupported=True messages=0`。

- [x] **Step 3: 材質を作る**

`execute_code`:

```csharp
var shader = Shader.Find("HalfAware/Daze");
if (shader == null) return "shader not found";
var path = "Assets/Materials/Fx/Daze.mat";
var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
mat.shader = shader;
mat.SetFloat("_BlurRadius", 0.012f);
mat.SetFloat("_WobbleX", 0.03f);
mat.SetFloat("_WobbleY", 0.02f);
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();
return path + " radius=" + mat.GetFloat("_BlurRadius") + " wobbleX=" + mat.GetFloat("_WobbleX") + " wobbleY=" + mat.GetFloat("_WobbleY");
```

Expected: 戻り値 `Assets/Materials/Fx/Daze.mat radius=0.012 wobbleX=0.03 wobbleY=0.02`。

- [x] **Step 4: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Shaders unity/Assets/Shaders.meta unity/Assets/Materials && git commit -m "feat: add the daze blur and wobble pass"
```

---

### Task 3: `DazeVolume` が数値をシェーダへ渡す

**Files:**
- Modify: `unity/Assets/Scripts/Fx/DazeVolume.cs`

- [x] **Step 1: 書き換える**

`unity/Assets/Scripts/Fx/DazeVolume.cs` を全文次に置き換える:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さをシーンに置く。SceneFlow が Hold と Decay を呼び、毎フレーム シェーダのグローバル変数へ入れる。
    /// SceneFlow より後に Update が走るよう実行順を後ろに置く
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DazeVolume : MonoBehaviour
    {
        static readonly int BlurId = Shader.PropertyToID("_DazeBlur");
        static readonly int WobbleId = Shader.PropertyToID("_DazeWobble");

        readonly Daze daze = new Daze();

        public float Blur => daze.Blur;
        public float Wobble => daze.Wobble;
        public bool IsClear => daze.IsClear;

        public void Hold(float blur, float wobble) => daze.Hold(blur, wobble);

        public void Decay(float blur, float wobble, float seconds) => daze.Decay(blur, wobble, seconds);

        public void Clear() => daze.Clear();

        void OnEnable() => Push();

        /// <summary>グローバル変数は再生を抜けても残るので、消えるときに 0 へ戻す。戻さないとシーンビューが眩暈のままになる</summary>
        void OnDisable()
        {
            Shader.SetGlobalFloat(BlurId, 0f);
            Shader.SetGlobalFloat(WobbleId, 0f);
        }

        void Update()
        {
            daze.Tick(Time.deltaTime);
            Push();
        }

        void Push()
        {
            Shader.SetGlobalFloat(BlurId, daze.Blur);
            Shader.SetGlobalFloat(WobbleId, daze.Wobble);
        }
    }
}
```

`Daze` は変えない。テストもそのまま通る。

- [x] **Step 2: コンパイルとテストを確かめる**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 60 件 passed。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Fx/DazeVolume.cs && git commit -m "feat: hand the daze strength to the shaders"
```

---

### Task 4: 描画の設定と後処理の登録

**Files:**
- Modify: `unity/Assets/Settings/PC_RPAsset.asset`
- Modify: `unity/Assets/Settings/PC_Renderer.asset`

- [x] **Step 1: 再生中でないことを確かめる**

`execute_code` で `return "isPlaying=" + EditorApplication.isPlaying;`。
Expected: `isPlaying=False`。`True` なら止めて親セッションに戻す。

- [x] **Step 2: 解像度の倍率と拡大の仕方**

`execute_code`:

```csharp
var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
if (rp == null) return "pipeline asset not found";
var so = new SerializedObject(rp);
so.FindProperty("m_RenderScale").floatValue = 1f / 3f;
so.FindProperty("m_UpscalingFilter").enumValueIndex = 2; // Point（最近傍）
so.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(rp);
AssetDatabase.SaveAssets();
var check = new SerializedObject(rp);
return "renderScale=" + check.FindProperty("m_RenderScale").floatValue
    + " upscaling=" + check.FindProperty("m_UpscalingFilter").enumValueIndex;
```

Expected: 戻り値 `renderScale=0.3333333 upscaling=2`。

- [x] **Step 3: 後処理 2 つを登録し、環境遮蔽を止める**

`execute_code`:

```csharp
var rendererPath = "Assets/Settings/PC_Renderer.asset";
var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(rendererPath);
if (data == null) return "renderer data not found";
var daze = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fx/Daze.mat");
var ps1 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fx/Ps1.mat");
if (daze == null || ps1 == null) return "materials missing";

System.Func<string, Material, UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint, string> add = (featureName, mat, point) => {
    foreach (var existing in AssetDatabase.LoadAllAssetsAtPath(rendererPath))
    {
        if (existing != null && existing.name == featureName) return "kept " + featureName;
    }
    var feature = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>();
    feature.name = featureName;
    feature.passMaterial = mat;
    feature.injectionPoint = point;
    feature.fetchColorBuffer = true;
    AssetDatabase.AddObjectToAsset(feature, data);
    string guid;
    long localId;
    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);
    var rso = new SerializedObject(data);
    var list = rso.FindProperty("m_RendererFeatures");
    list.arraySize++;
    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
    var map = rso.FindProperty("m_RendererFeatureMap");
    map.arraySize++;
    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
    rso.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(data);
    return "added " + featureName;
};

// 順: 描画 → 眩暈 → （URP の後処理 = 色味）→ 減色とディザ → 拡大して出力
var a = add("Daze", daze, UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing);
var b = add("Ps1", ps1, UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing);

// 環境遮蔽は PS1 風には合わず、3 分の 1 の解像度ではほとんど見えないので止める。戻すのは Inspector の印 1 つ
var ssao = "no ssao";
foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(rendererPath))
{
    var f = obj as UnityEngine.Rendering.Universal.ScriptableRendererFeature;
    if (f == null || f.name != "ScreenSpaceAmbientOcclusion") continue;
    var fso = new SerializedObject(f);
    fso.FindProperty("m_Active").boolValue = false;
    fso.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(f);
    ssao = "ssao off";
}

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
return a + " / " + b + " / " + ssao;
```

Expected: 戻り値 `added Daze / added Ps1 / ssao off`。`read_console` でエラー 0。

- [x] **Step 4: 登録された結果を読み返す**

`execute_code`:

```csharp
var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
var so = new SerializedObject(data);
var list = so.FindProperty("m_RendererFeatures");
var map = so.FindProperty("m_RendererFeatureMap");
var report = "features=" + list.arraySize + " map=" + map.arraySize;
for (int i = 0; i < list.arraySize; i++)
{
    var f = list.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Rendering.Universal.ScriptableRendererFeature;
    report += "\n  " + (f != null ? f.name : "null") + " active=" + (f != null && f.isActive);
    var full = f as UnityEngine.Rendering.Universal.FullScreenPassRendererFeature;
    if (full != null) report += " point=" + full.injectionPoint + " material=" + (full.passMaterial != null ? full.passMaterial.name : "null");
}
return report;
```

Expected: `features=3 map=3`、`ScreenSpaceAmbientOcclusion active=False`、`Daze active=True point=BeforeRenderingPostProcessing material=Daze`、`Ps1 active=True point=AfterRenderingPostProcessing material=Ps1`。`features` と `map` の数が食い違っていたら止めて報告する。

- [x] **Step 5: コミット**

```bash
cd /d/work/kataaware && git status --short && git add unity/Assets/Settings/PC_RPAsset.asset unity/Assets/Settings/PC_Renderer.asset && git commit -m "feat: render at a third of the resolution and hang the two passes on the renderer"
```

---

### Task 5: 場面 1 の色味

テンプレート由来の `SampleSceneProfile` はブルームと動きぼかしを持っていて、どちらも PS1 風とは合わない。場面 1 用の設定資産を新しく作り、シーンの `Global Volume` をそちらへ向ける。テンプレートの設定資産は触らない。

**Files:**
- Create: `unity/Assets/Settings/RoomVolumeProfile.asset`
- Modify: `unity/Assets/Scenes/Room.unity`（`Global Volume` が指す設定資産のみ）

- [ ] **Step 1: 設定資産を作る**

`execute_code`:

```csharp
var path = "Assets/Settings/RoomVolumeProfile.asset";
var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
if (profile == null)
{
    profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
    AssetDatabase.CreateAsset(profile, path);
}
System.Func<System.Type, UnityEngine.Rendering.VolumeComponent> ensure = (type) => {
    var existing = profile.components.Find(c => c.GetType() == type);
    if (existing != null) return existing;
    var made = (UnityEngine.Rendering.VolumeComponent)ScriptableObject.CreateInstance(type);
    made.name = type.Name;
    profile.components.Add(made);
    AssetDatabase.AddObjectToAsset(made, profile);
    return made;
};

// 試作の自室は 0xc8d0ff へ 0.25 寄せていた。白からその色へ 0.25 混ぜた値を色の掛け算に使う
var tone = Color.Lerp(Color.white, new Color(200f / 255f, 208f / 255f, 1f), 0.25f);
var color = (UnityEngine.Rendering.Universal.ColorAdjustments)ensure(typeof(UnityEngine.Rendering.Universal.ColorAdjustments));
color.colorFilter.overrideState = true;
color.colorFilter.value = tone;
color.saturation.overrideState = true;
color.saturation.value = -12f;
color.active = true;

var tonemap = (UnityEngine.Rendering.Universal.Tonemapping)ensure(typeof(UnityEngine.Rendering.Universal.Tonemapping));
tonemap.mode.overrideState = true;
tonemap.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.Neutral;
tonemap.active = true;

EditorUtility.SetDirty(profile);
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
var names = "";
foreach (var c in profile.components) names += c.GetType().Name + " ";
return path + " components: " + names.Trim() + " filter=" + color.colorFilter.value;
```

Expected: 戻り値に `components: ColorAdjustments Tonemapping` が含まれ、`filter` が白よりわずかに青い値（各成分が 0.94 前後から 1.0）。

- [ ] **Step 2: シーンの `Global Volume` を向け直す**

`execute_code`:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/RoomVolumeProfile.asset");
if (profile == null) return "profile missing";
var go = GameObject.Find("Global Volume");
if (go == null) return "Global Volume not found";
var volume = go.GetComponent<UnityEngine.Rendering.Volume>();
var before = volume.sharedProfile != null ? volume.sharedProfile.name : "none";
volume.sharedProfile = profile;
EditorUtility.SetDirty(volume);
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return before + " -> " + volume.sharedProfile.name + " isGlobal=" + volume.isGlobal;
```

Expected: 戻り値 `SampleSceneProfile -> RoomVolumeProfile isGlobal=True`。

- [ ] **Step 3: シーンの差分が 1 か所だけか確かめてコミット**

```bash
cd /d/work/kataaware && git diff --stat unity/Assets/Scenes/Room.unity && git diff unity/Assets/Scenes/Room.unity | grep -E "^[-+]" | grep -v "^[-+][-+]" | head -10
```

Expected: 変更行は `sharedProfile` の guid 1 行のみ（削除 1 行・追加 1 行）。他の行が動いていたら止めて報告する。

```bash
cd /d/work/kataaware && git add unity/Assets/Settings/RoomVolumeProfile.asset unity/Assets/Settings/RoomVolumeProfile.asset.meta unity/Assets/Scenes/Room.unity && git commit -m "feat: give scene 1 its own colour"
```

---

### Task 6: 再生での確認

見た目の最終判断はオーナー。ここでは「効いていること」と「効き具合が数値で切れること」を確かめ、写真を残す。

**Files:** なし

- [ ] **Step 1: 再生に入る**

`read_console`（`action: "clear"`）→ `execute_code` で `EditorApplication.isPlaying` を読み、`False` を確かめる → `manage_editor`（`action: "play"`）→ `execute_code` で `Application.runInBackground = true;` → `read_console`（`types: ["error", "warning"]`）。
Expected: エラー 0。シェーダの実行時の誤りはここで出る。

- [ ] **Step 2: 導入のあいだ（眩暈が最大）の画面を撮る**

`execute_code`:

```csharp
var daze = GameObject.Find("SceneFlow").GetComponent<HalfAware.DazeVolume>();
Time.timeScale = 0.1f; // 導入は 2 秒ずつしかないので、確認の往復に間に合うよう遅くする
ScreenCapture.CaptureScreenshot("Temp/stage4a-01-daze.png");
return "blur=" + daze.Blur.ToString("0.00") + " wobble=" + daze.Wobble.ToString("0.00") + " t=" + Time.time.ToString("0.0");
```

数秒後に `D:\work\kataaware\unity\Temp\stage4a-01-daze.png` を Read で見る。
Expected: `blur=1.00`。画面は画素が粗く、輪郭が歪み、全体がぼやけている。字幕と中央の文字は粗くならず、くっきりしたまま。

- [ ] **Step 3: 眩暈を切った画面を撮る**

`execute_code`:

```csharp
var daze = GameObject.Find("SceneFlow").GetComponent<HalfAware.DazeVolume>();
daze.Clear();
Time.timeScale = 1f;
return "blur=" + daze.Blur.ToString("0.00") + " wobble=" + daze.Wobble.ToString("0.00");
```

次の呼び出しで `ScreenCapture.CaptureScreenshot("Temp/stage4a-02-clear.png");` を撮り、Read で見る。
Expected: `blur=0.00 wobble=0.00`。画面は粗いまま（減色とディザは効いている）が、歪みとぼやけが消えている。

- [ ] **Step 4: 減色とディザを素通しにして比べる**

`execute_code`:

```csharp
var ps1 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fx/Ps1.mat");
ps1.SetFloat("_Amount", 0f);
return "amount=" + ps1.GetFloat("_Amount");
```

`ScreenCapture.CaptureScreenshot("Temp/stage4a-03-nops1.png");` を撮って Read で見る。
Expected: 色の段差とディザの網目が消え、画素の粗さ（3 分の 1 の描画と最近傍の拡大）だけが残る。これで 2 つの効果が別々に切れることを確かめられる。

戻す:

```csharp
var ps1 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fx/Ps1.mat");
ps1.SetFloat("_Amount", 1f);
EditorUtility.SetDirty(ps1);
AssetDatabase.SaveAssets();
return "amount=" + ps1.GetFloat("_Amount");
```

Expected: `amount=1`。

- [ ] **Step 5: 止めて後始末**

`execute_code` で `Time.timeScale = 1f; Application.runInBackground = false;` → `manage_editor`（`action: "stop"`）→ `read_console`（`types: ["error"]`）。

```bash
cd /d/work/kataaware && git status --short && grep -n "runInBackground" unity/ProjectSettings/ProjectSettings.asset
```

Expected: エラー 0、`runInBackground: 0`、作業ツリーはこのプランのファイル以外きれい。`Room.unity` に差分が出ていたら `git checkout` で戻す。フォントのアトラスが出ていたら同じく戻す。

- [ ] **Step 6: オーナーに手元の確認を頼む**

（親セッションが行う。あなたは実施しない）

---

## 段階 4-b 以降に持ち越すこと

- Kenney の家具の取り込みと配置。卓・机・椅子の位置はここで一緒に動かす
- 固有物（椅子、メモリハブ、端末、灰皿、煙草の箱、前腕とジャック）の生成。前腕をカメラの子にして下を向いたときだけ出す仕組みも含む。今あるジャックの箱は部屋に固定されたままなので、そのとき `Interactable_jack` も一緒に動かす必要がある
- 頂点の揺れ（PS1 の位置精度の粗さ）を入れるかどうか。材質のシェーダに手を入れる話なので、家具を差し替えた後に試して決める
- 煙の見た目。今は画面に重ねた平らな層で、HUD と同じ画面空間の Canvas に載っているため、この段の粗い描画の影響を受けず、背景より鮮明に見える。世界の側に見せたいなら Canvas から出す必要がある。オーナーの判断を待つ
- 端末の黒い画面に映る顔の反射。片割れの顔のモデルが要るので、固有物の生成より後
- `Daze` が素通しのときも後処理は走る。`DazeVolume.IsClear` を見て `FullScreenPassRendererFeature.SetActive(false)` に切り替えれば省ける。WebGL の負荷を見てから決める
- 音（効果音、英語の合成音声のサンプリング、BGM）
