using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 の車を、ほかの場面の組み立てから組むための口。エンディング（<see cref="BuildEnding"/>）が使う。
    ///
    /// **中身は <see cref="Car"/> を呼ぶだけ。** 車の寸法も形も場面 8 の組み立てが持ち、ここでは何も足さない・変えない。
    /// 形のメッシュとマテリアルは場面 8 と同じアセット（generated/drive・Materials/Drive）へ同じ中身で書き直されるので、
    /// 場面 8 の絵は動かない
    /// </summary>
    public static partial class BuildDrive
    {
        /// <summary>parent の下に場面 8 の車を組む。原点は車の中心、目は <see cref="SeatAt"/> + <see cref="EyeLead"/></summary>
        internal static void CarInto(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/drive"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "drive");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Drive"))
                AssetDatabase.CreateFolder("Assets/Materials", "Drive");
            shapes.Clear();
            Car(parent);
        }
    }
}
