using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// UI の粗さの設定。コンソールと HUD（字幕・印・暗転）を、3D の絵と同じように粗い画素で見せる。
    /// **値はこの一つのアセット（Resources/UiLook）だけが持つ。** インスペクターで変えれば、
    /// 再生中でもその場で効く。設定の画面からは <see cref="UiLens.Scale"/> で上書きする
    /// </summary>
    [CreateAssetMenu(menuName = "HalfAware/UI Look", fileName = "UiLook")]
    public sealed class UiLook : ScriptableObject
    {
        /// <summary>Resources の中の名</summary>
        public const string Path = "UiLook";

        /// <summary>
        /// 粗さの既定。UI を画面の何分の一の解像度で描くか。
        /// 3D は 1/3（960×540 で中が 320×180）。UI はそこまで落とさない。
        /// 1/2 から始めたが、字の下限（縦 10 画素ほど）を守ったまま字を画面の上で小さくし、余白を広く取るため
        /// 0.75 にした（960×540 で中が 720×405）。字幕の字の大きさはキャンバスの単位で決めているので、粗さを変えても画面の上では変わらない
        /// </summary>
        public const float DefaultScale = 0.75f;

        [Tooltip("UI を画面の何分の一の解像度で描くか。1 でくっきり、小さいほど粗い。3D は 1/3")]
        [Range(0.25f, 1f)]
        public float scale = DefaultScale;

        [Tooltip("粗く描いた UI を画面へ重ねるマテリアル（HalfAware/UiLens）。乗算済みのアルファのまま重ねる")]
        public Material show;

        [Tooltip("UI を描くカメラのレンダラー。全画面の後処理（Ps1・Daze）を持たない物。パイプラインのアセットのレンダラーの一覧に入れておく")]
        public UnityEngine.Rendering.Universal.ScriptableRendererData renderer;
    }
}
