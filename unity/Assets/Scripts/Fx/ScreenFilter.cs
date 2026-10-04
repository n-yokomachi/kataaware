using UnityEngine;

namespace HalfAware
{
    /// <summary>画面のフィルターの型。設定の「フィルター」（<see cref="GameSettings.Filter"/>）の並び</summary>
    public enum ScreenFilterKind
    {
        /// <summary>標準。色ごとに 32 段へ刻み、4×4 の規則的な点で段のあいだを埋める</summary>
        Standard = 0,

        /// <summary>減色＋ディザ。色の組の中の近い二色を、画面に貼り付いた青色雑音の点で混ぜる</summary>
        Dither = 1,
    }

    /// <summary>
    /// 画面の加工（PC_Renderer の Ps1 のパス、設計書 2.5 節）の型を切り替える。
    /// 型はシェーダーのグローバルの値 <see cref="GlobalName"/>（0 が標準、1 が減色＋ディザ）で渡す。
    /// Ps1.mat には書かない（アセットを書き換えると、エディタで遊んだ後に変更として残る）。
    ///
    /// 遊び始めに設定の値を入れ、設定の枠で動かすとその場で入れ直す（<see cref="SettingChoice.Changed"/>）。
    /// 遊び終えたら標準へ戻す。グローバルの値は再生を抜けても残るので、戻さないとエディタの絵が減色のままになる。
    /// タイトルの画面の背景（前もって撮った絵）も同じ値を読んで、同じ色の組で減色する（HalfAware/FilteredPicture）
    /// </summary>
    public static class ScreenFilter
    {
        /// <summary>鍵に書く符丁。<see cref="ScreenFilterKind"/> の並び</summary>
        public static readonly string[] Ids = { "Standard", "Dither" };

        /// <summary>設定の枠に出す名。<see cref="ScreenFilterKind"/> の並び</summary>
        public static readonly string[] Labels = { "標準", "減色＋ディザ" };

        /// <summary>型を渡すグローバルの値の名。Ps1 と FilteredPicture のシェーダーが読む（Properties には置かない。置くとマテリアルの値が勝つ）</summary>
        public const string GlobalName = "_HaFilter";

        /// <summary>タイトルの画面の背景の絵を敷くマテリアル（HalfAware/FilteredPicture）。Resources の中の名</summary>
        public const string PictureMaterial = "ScreenFilterPicture";

        static int id = -1;

        /// <summary>設定のいまの型</summary>
        public static ScreenFilterKind Current { get { return (ScreenFilterKind)GameSettings.Filter.Value; } }

        /// <summary>設定のいまの型を画面へ効かせる</summary>
        public static void Apply()
        {
            Use(Current);
        }

        /// <summary>kind の型を画面へ効かせる。設定は書かない（エディタで撮り比べるとき）</summary>
        public static void Use(ScreenFilterKind kind)
        {
            if (id < 0) id = Shader.PropertyToID(GlobalName);
            Shader.SetGlobalFloat(id, kind == ScreenFilterKind.Dither ? 1f : 0f);
        }

        /// <summary>シェーダーのグローバルの値として、いま入っている型</summary>
        public static ScreenFilterKind InEffect
        {
            get { return Shader.GetGlobalFloat(GlobalName) > 0.5f ? ScreenFilterKind.Dither : ScreenFilterKind.Standard; }
        }

        /// <summary>遊び始め。最初の場面を読む前に、残してあった型を入れる</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // ドメインを読み直さない設定でも、前の再生の繋ぎを重ねない
            GameSettings.Filter.Changed -= Apply;
            GameSettings.Filter.Changed += Apply;
            Application.quitting -= Leave;
            Application.quitting += Leave;
            Apply();
        }

        static void Leave()
        {
            GameSettings.Filter.Changed -= Apply;
            Application.quitting -= Leave;
            Use(ScreenFilterKind.Standard);
        }
    }
}
