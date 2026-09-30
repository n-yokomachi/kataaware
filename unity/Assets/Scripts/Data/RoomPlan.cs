using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 自室の間取り（シナリオ設計 5 節「間取り」。オーナー、2026-09-30 に OK）。組むのは <c>HalfAware/Build the room shell</c>（BuildRoomShell）。
    ///
    /// 座標は前の 6 × 6 m のワンルームと同じ（x が東、z が北、床の上面が y 0）。区画は壁の中心の線で持ち、壁の厚みは <see cref="Wall"/>。
    /// 北に LDK（8 × 4.5 m）、その南に張り出し（廊下と、廊下の両脇の風呂・トイレ・物入れ・寝室）。
    /// 廊下は北の端で LDK の南の壁に戸の無い開口で開き、南の端が玄関のドア。
    /// **風呂・トイレ・物入れ・寝室は中を作らない。** 廊下の壁のドアだけ（閉じたまま開かない。調べる対象にもしない）。物入れはドアも付けず、ただの壁にする。
    ///
    /// 実行時には呼ばない。組み立て（部屋の形・場面 3 の立ち位置・ドアの調べる対象）と試験が、この表を読む
    /// </summary>
    public static class RoomPlan
    {
        /// <summary>壁の厚み</summary>
        public const float Wall = 0.2f;
        /// <summary>天井の下の面の高さ（前の部屋と同じ）</summary>
        public const float Ceiling = 3.0f;
        /// <summary>天井の板と床の板の厚み</summary>
        public const float Slab = 0.2f;
        public const float FloorThick = 0.1f;

        // ---- 区画（壁の中心の線。x が横、y が z） ------------------------------------

        public static readonly Rect Ldk = Rect.MinMaxRect(-5f, -1.5f, 3f, 3f);
        /// <summary>台所。LDK の南の壁沿い</summary>
        public static readonly Rect Kitchen = Rect.MinMaxRect(-5f, -1.5f, -0.6f, -0.9f);
        /// <summary>台所と居間を区切る作業台</summary>
        public static readonly Rect Counter = Rect.MinMaxRect(-4f, -0.1f, -1.2f, 0.5f);
        public static readonly Rect Living = Rect.MinMaxRect(-5f, 0.5f, -0.5f, 3f);
        /// <summary>仕事の区画（机・モニター・椅子・右の卓）。前の部屋のまま</summary>
        public static readonly Rect Work = Rect.MinMaxRect(0f, 0.3f, 3f, 3f);

        public static readonly Rect Hall = Rect.MinMaxRect(-0.6f, -5.1f, 0.6f, -1.5f);
        public static readonly Rect Bath = Rect.MinMaxRect(-2.4f, -3.3f, -0.6f, -1.5f);
        public static readonly Rect Toilet = Rect.MinMaxRect(-2.4f, -4.4f, -0.6f, -3.3f);
        public static readonly Rect Store = Rect.MinMaxRect(-2.4f, -5.1f, -0.6f, -4.4f);
        public static readonly Rect Bedroom = Rect.MinMaxRect(0.6f, -5.1f, 3f, -1.5f);

        /// <summary>廊下が LDK へ開く口の上の垂れ壁の下の面。戸は無い</summary>
        public const float MouthHead = 2.1f;

        /// <summary>廊下の口（LDK の南の壁の中の、廊下の幅の開口）の真ん中</summary>
        public static Vector2 HallMouth
        {
            get { return new Vector2(Hall.center.x, Ldk.yMin); }
        }

        // ---- 窓 --------------------------------------------------------------------

        /// <summary>窓の壁の抜けの幅と、下と上の縁の高さ。窓はどれも同じ作り</summary>
        public const float WindowWide = 0.96f;
        public const float WindowSill = 0.68f;
        public const float WindowHead = 2.22f;

        /// <summary>窓一つ。east なら東の壁（x が壁の線）、そうでなければ北の壁（z が壁の線）。centre は壁に沿った向きの真ん中</summary>
        public struct Window
        {
            public string Name;
            public bool East;
            public float Centre;

            public Window(string name, bool east, float centre)
            {
                Name = name;
                East = east;
                Centre = centre;
            }

            /// <summary>壁に沿った向きの、抜けの両端</summary>
            public Vector2 Span { get { return new Vector2(Centre - WindowWide * 0.5f, Centre + WindowWide * 0.5f); } }
        }

        /// <summary>
        /// 北の窓（前からの窓・居間の窓）と東の窓。名前は部屋の子の名前。
        /// 居間の窓は、前からの窓と同じ作り・大きさで、LDK の西へ広がった所に開ける
        /// </summary>
        public static readonly Window[] Windows =
        {
            new Window("WindowFront", false, -1.3f),
            new Window("WindowFrontWest", false, -3.8f),
            new Window("Window", true, -0.5f),
        };

        public static Window NorthWindow { get { return Windows[0]; } }
        public static Window WestWindow { get { return Windows[1]; } }
        public static Window EastWindow { get { return Windows[2]; } }

        // ---- ドア ------------------------------------------------------------------

        /// <summary>ドアの枠の高さ（Kenney の doorway を 2 倍）。壁の抜けは枠より 1 cm 低く、幅は 2 cm 狭い（枠が壁に 1 cm ずつ掛かる）</summary>
        public const float DoorHigh = 2.02f;

        /// <summary>ドア一つ。centre は壁の中心の線の上の真ん中（x, z）、inward は見る側（廊下）へ向く壁の法線、wide は枠の幅</summary>
        public struct Door
        {
            public string Name;
            public Vector2 Centre;
            public Vector2 Inward;
            public float Wide;

            public Door(string name, Vector2 centre, Vector2 inward, float wide)
            {
                Name = name;
                Centre = centre;
                Inward = inward;
                Wide = wide;
            }

            /// <summary>壁が x に沿って走るか（南の壁の玄関）。そうでなければ z に沿う（廊下の両脇）</summary>
            public bool AlongX { get { return Mathf.Abs(Inward.y) > 0.5f; } }

            /// <summary>壁に沿った向きの、枠の両端</summary>
            public Vector2 Span
            {
                get
                {
                    var c = AlongX ? Centre.x : Centre.y;
                    return new Vector2(c - Wide * 0.5f, c + Wide * 0.5f);
                }
            }
        }

        /// <summary>
        /// 玄関（廊下の南の端）と、廊下の両脇の閉じた部屋のドア。玄関は前の部屋の <c>Door</c> を移し、ほかはその複製。
        /// 物入れはドアを付けない（オーナー「玄関横の収納扉が目立ちすぎる。扉自体を削除」）。区画（<see cref="Store"/>）は残し、廊下の側は壁のまま
        /// </summary>
        public static readonly Door[] Doors =
        {
            new Door("Door", new Vector2(0f, -5.1f), new Vector2(0f, 1f), 0.9f),
            new Door("DoorBath", new Vector2(-0.6f, -2.3f), new Vector2(1f, 0f), 0.8f),
            new Door("DoorToilet", new Vector2(-0.6f, -3.85f), new Vector2(1f, 0f), 0.7f),
            new Door("DoorBedroom", new Vector2(0.6f, -3.2f), new Vector2(-1f, 0f), 0.8f),
        };

        public static Door Entrance { get { return Doors[0]; } }

        /// <summary>
        /// 場面 1 のドア（<c>door</c>）と場面 7 のドアの調べる対象。玄関のドアの真ん中の、壁の面から 0.1 m 手前、高さ 1.2 m
        /// （前の部屋の対象と同じ置き方）
        /// </summary>
        public static Vector3 DoorItemAt
        {
            get
            {
                var e = Entrance;
                var at = e.Centre + e.Inward * (Wall * 0.5f + 0.1f);
                return new Vector3(at.x, 1.2f, at.y);
            }
        }

        /// <summary>
        /// 場面 3 の始まりの立ち位置（x, z）。玄関の内側、廊下の南の端で北（廊下の奥）を向く。
        /// ドアの面から 0.4 m（体の当たりの半径 0.3 m の外）
        /// </summary>
        public static readonly Vector2 EntranceStand = new Vector2(0f, -4.6f);

        // ---- 明かり --------------------------------------------------------------------

        /// <summary>廊下の天井の明かりの真ん中（x, z）。廊下の口と玄関の間</summary>
        public static readonly Vector2 HallLamp = new Vector2(0f, -3.3f);

        // ---- 歩ける所 ------------------------------------------------------------------

        /// <summary>
        /// 床の上の点が、歩ける内側（LDK と廊下と廊下の口の、壁の面の内）から margin 以上離れているか。
        /// 閉じた部屋の中は外
        /// </summary>
        public static bool Open(Vector2 p, float margin)
        {
            var h = Wall * 0.5f + margin;
            if (Inside(p, Ldk, h)) return true;
            // 廊下は口（LDK の南の壁の中）を抜けて LDK へ続く。廊下の幅の帯を LDK の奥まで伸ばして見る（LDK の中は上で済んでいる）
            var hall = Rect.MinMaxRect(Hall.xMin, Hall.yMin, Hall.xMax, Ldk.yMax);
            return Inside(p, hall, h);
        }

        static bool Inside(Vector2 p, Rect r, float inset)
        {
            return p.x >= r.xMin + inset && p.x <= r.xMax - inset && p.y >= r.yMin + inset && p.y <= r.yMax - inset;
        }
    }
}
