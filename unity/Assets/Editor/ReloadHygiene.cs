using UnityEditor;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// ドメインの読み込み直しの前に、どこからも使われていない一時的な物を片付ける。
    ///
    /// 組み立ての道具や確かめの撮影が、場面を保存せずに組んで撮って捨てると、組んだ時に作った mesh・テクスチャ・マテリアルが
    /// どこからも使われないまま残る。残った物は読み込み直しのたびに運び直されるので、溜まるほど一回ごとに遅くなる
    /// （2026-09-30、エディタを 5 日開いたままで 482 回読み込み直し、一回が 0.8 秒から 113 秒まで伸びた）。
    /// HideAndDontSave の物はこれでは消えないので、プローブのカメラや RenderTexture は今どおり作った呼び出しの中で捨てる
    /// </summary>
    [InitializeOnLoad]
    static class ReloadHygiene
    {
        static ReloadHygiene()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Sweep;
        }

        static void Sweep()
        {
            EditorUtility.UnloadUnusedAssetsImmediate();
        }
    }
}
