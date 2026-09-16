namespace HalfAware
{
    /// <summary>
    /// 前腕を出すかどうかの判定だけ。ピッチは正が下向き（PlayerController.Pitch と同じ向き）。
    /// 出る角度と消える角度をずらして、境目でちらつかないようにする
    /// </summary>
    public static class ForearmView
    {
        /// <summary>この角度より下を向くと出る。度</summary>
        public const float DefaultShowBelow = 35f;

        /// <summary>出ている間は、しきい値からこの角度ぶん戻るまで消えない。度</summary>
        public const float DefaultHysteresis = 8f;

        /// <summary>
        /// showing は今出ているかどうか。出ている間はしきい値を hysteresis ぶん緩めるので、
        /// しきい値の上で視線が揺れても点いたり消えたりしない
        /// </summary>
        public static bool ShouldShow(float pitch, bool showing, float showBelow, float hysteresis)
        {
            if (showing) return pitch > showBelow - hysteresis;
            return pitch > showBelow;
        }
    }
}
