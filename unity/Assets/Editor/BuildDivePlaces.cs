using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の五つの場所。
    ///
    /// **どれも暗がりと色味で誤魔化せる最小の箱にする。** 記憶は他人の頭から抜いてきた絵で、
    /// 隅々まで見えている必要がない。天井と壁を暗く、床だけ少し明るく、灯りは一つ。
    /// 明るさと色は記憶ごとに Volume が寄せるので、ここは形だけを持つ。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 組み立て --------------------------------------------------------

        static Transform Places(Transform root)
        {
            var parent = Child(root, "Places");
            Clear(parent);
            Estate(Spot(parent, DiveIds.Estate));
            Park(Spot(parent, DiveIds.Park));
            Train(Spot(parent, DiveIds.Train));
            Kitchen(Spot(parent, DiveIds.Kitchen));
            Classroom(Spot(parent, DiveIds.Classroom));
            // 潜るまでは全部伏せる。DiveDirector が一つだけ起こす
            for (var i = 0; i < parent.childCount; i++) parent.GetChild(i).gameObject.SetActive(false);
            return parent;
        }

        static Transform Spot(Transform parent, string id)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = PlaceOrigin(id);
            return go.transform;
        }

        // ---- 道具 --------------------------------------------------------------

        /// <summary>
        /// 光る板を一枚。
        ///
        /// **Quad は法線が -z。** 角度で渡すと裏表を取り違えて板が消えるので（場面 3 で踏んだ）、
        /// 受け取るのは「どちら側から見えてほしいか」の向きにして、回すのはこちらで引き受ける
        /// </summary>
        static Transform Pane(Transform parent, string name, Vector3 at, Vector2 size, Vector3 facing, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<MeshCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            // 真下や真上を向く板は、上向きを world の上に取ると回しようが無くなって消える
            var aim = facing.normalized;
            go.transform.localRotation = Quaternion.LookRotation(-aim,
                Mathf.Abs(aim.y) > 0.99f ? Vector3.forward : Vector3.up);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
