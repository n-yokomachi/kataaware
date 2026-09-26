using UnityEngine;
using UnityEngine.InputSystem;

namespace HalfAware
{
    /// <summary>
    /// 歩く・見回す。CharacterController と Input System の Player マップ（Move / Look / Interact）で動く。
    /// カーソルは画面のクリックでロックし、ロック中だけ見回しと移動と調べる操作を受け付ける。
    /// SceneFlow より先に Update が走るよう実行順を前に置く
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 1.4f;         // m/s。歩きの一巡とほぼ同じ速さ
        public const float RunSpeed = 3.0f;          // m/s。Shift を押している間
        public const float StandingEyeHeight = 1.6f;
        /// <summary>上を向ける限り。度</summary>
        public const float PitchUpLimit = 80f;
        /// <summary>
        /// 下を向ける限り。度。**主人公の性別は対面まで見せない**（シナリオ設計 1 節）。
        /// 体の胸が画面の下の縁に入り始めるのは、立って 47 度・座って 56 度なので、その手前で止める。
        /// 演出が視線を寄せるとき（JackPull・JackPlug など）も、この範囲に収める
        /// </summary>
        public const float PitchDownLimit = 40f;
        // 度 / ピクセル。試作は 0.0022 rad/px（＝ 0.126）だったが、実画面で速すぎたので半分にした。
        // **場面をまたいで効く。** 自室も路地裏も車内も同じ速さで振れる
        public const float LookSensitivity = 0.063f;

        [SerializeField] InputActionAsset actions;
        [SerializeField] Transform eye;
        [Tooltip("目は背骨の中心ではなく顔にある。体の前へこれだけ出す。メートル")]
        [SerializeField] float eyeLead = 0.22f;

        CharacterController body;
        readonly HeadTurn head = new HeadTurn();
        InputAction move;
        InputAction look;
        InputAction interact;
        float pitch;

        /// <summary>走っているときの速さ。押している間だけ上げる</summary>
        public static float Speed(bool running)
        {
            return running ? RunSpeed : WalkSpeed;
        }

        /// <summary>借りた体の速さ。倍率が 0 以下なら基準のまま歩かせる</summary>
        public static float Speed(bool running, float scale)
        {
            return Speed(running) * (scale > 0f ? scale : 1f);
        }

        /// <summary>
        /// 歩く速さの倍率。基準が 1。場面 4 は記憶ごとに借りる体が違い、
        /// 六歳の子と七十八歳の老人が同じ速さで歩くと体を借りている感じが消える。
        /// 直列化しない。掛けるのは場面の側で、場面を抜ければ 1 へ戻す
        /// </summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>今このフレームで走っているか。動作確認から読む</summary>
        public bool Running { get; private set; }

        /// <summary>false の間は見回しだけできる（座っている、演出中など）</summary>
        public bool CanMove { get; set; } = true;

        /// <summary>false の間は見回しも受け付けない。自動で進む演出のあいだに使う</summary>
        public bool CanLook { get; set; } = true;

        /// <summary>足元からカメラまでの高さ。座位と立位で変える</summary>
        public float EyeHeight { get; set; } = StandingEyeHeight;

        /// <summary>カメラ。位置と向きの読み取りに使う</summary>
        public Transform Eye => eye;

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>このフレームで調べる操作（E か左クリック）が押されたか。ロック中だけ true になる</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>
        /// 上下の送り。1 で上（車輪を手前に回す・上の矢印・PageUp）、-1 で下、0 で据え置き。
        /// 場面 4 の板の `潜る`・`切断` の選びが読む。TAB のコンソールは自分で鍵盤を読む
        /// </summary>
        public int LogStep { get; private set; }

        /// <summary>
        /// 二択の左右。-1 が左、+1 が右、倒していなければ 0。
        /// 押した瞬間を取るのは呼び手の仕事で、ここは倒れ具合をそのまま渡す
        /// </summary>
        public int ChoiceStep { get; private set; }

        /// <summary>
        /// 座っている間、左右に振れる角度。度。片側の値。0 以下なら体ごと回れる。
        /// 立ち上がるときは ReleaseHead を呼んで、首の向きを体へ渡す
        /// </summary>
        public float HeadYawLimit
        {
            get { return head.Limit; }
            set { head.Limit = value; }
        }

        /// <summary>体から見た首の向き。度</summary>
        public float HeadYaw { get { return head.Yaw; } }

        /// <summary>首の制限を解き、溜めていた向きを体へ移す。見た目は繋がったまま</summary>
        public void ReleaseHead()
        {
            var carried = head.Release();
            if (Mathf.Abs(carried) > 1e-4f) transform.Rotate(0f, carried, 0f);
        }

        /// <summary>左右の向き。度。体と首を合わせた向きを指す。演出から正面へ戻すのに使う</summary>
        public float Yaw
        {
            get { return transform.eulerAngles.y + head.Yaw; }
            set
            {
                if (head.Limited) head.Set(Mathf.DeltaAngle(transform.eulerAngles.y, value));
                else transform.rotation = Quaternion.Euler(0f, value, 0f);
            }
        }

        /// <summary>上下の向き。度。書き込むと範囲に収まる。動作確認から視線を向けるのにも使う</summary>
        public float Pitch
        {
            get { return pitch; }
            set { pitch = ClampPitch(value); }
        }

        /// <summary>上下の向きを、向けられる範囲（上 <see cref="PitchUpLimit"/>・下 <see cref="PitchDownLimit"/>）に収める。正が下向き</summary>
        public static float ClampPitch(float pitch)
        {
            return Mathf.Clamp(pitch, -PitchUpLimit, PitchDownLimit);
        }

        /// <summary>目の位置に上乗せするずれ。眩暈の漂いが毎フレーム入れる</summary>
        public Vector3 EyeOffset { get; set; }

        /// <summary>目の向きに上乗せする傾き。x が上下、y が左右。度。眩暈の漂いが毎フレーム入れる</summary>
        public Vector2 EyeTilt { get; set; }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            var map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            interact = map.FindAction("Interact", true);
        }

        void OnEnable() => actions.FindActionMap("Player", true).Enable();

        void OnDisable() => actions.FindActionMap("Player", true).Disable();

        /// <summary>
        /// 上下の送りの入力。専用の割り当ては作らず、車輪と上下の矢印を直に見る。
        /// 板を出している間しか使わないので、歩きの入力とは取り合わない
        /// </summary>
        static int ReadLogStep()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                var wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) return 1;      // 手前に回すと古い方へ
                if (wheel < -0.01f) return -1;
            }
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null) return 0;
            if (keys.upArrowKey.wasPressedThisFrame || keys.pageUpKey.wasPressedThisFrame) return 1;
            if (keys.downArrowKey.wasPressedThisFrame || keys.pageDownKey.wasPressedThisFrame) return -1;
            return 0;
        }

        void Update()
        {
            InteractPressed = false;
            LogStep = 0;
            ChoiceStep = 0;
            // コンソールを開いている間は、歩く・見回す・調べる・送るを全部止める。
            // カーソルもコンソールが預かっているので、ここでロックし直さない
            if (ImplantConsole.IsOpen)
            {
                Running = false;
                Aim();
                return;
            }
            LogStep = ReadLogStep();
            if (CursorLocked)
            {
                InteractPressed = interact.WasPressedThisFrame();
                var stick = move.ReadValue<Vector2>();
                ChoiceStep = stick.x > 0.5f ? 1 : stick.x < -0.5f ? -1 : 0;
                if (CanLook) Look(look.ReadValue<Vector2>());
                if (CanMove) Walk(stick);
            }
            else
            {
                // ロックが外れている間はカーソルを見せる。ロックするためのクリックは調べる操作に使わない
                Cursor.visible = true;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Lock();
            }
            Aim();
        }

        /// <summary>
        /// 目の位置と向きは、ロックの有無にかかわらず毎フレーム書き直す。
        /// 上乗せしていく形にすると、書き直されない間にずれが溜まって視界が回り続ける。
        /// 傾きをこの順で組むと、左右の傾きが親の水平面で効くので、下を向いていても画面が回らない
        /// </summary>
        void Aim()
        {
            eye.localPosition = new Vector3(0f, EyeHeight, eyeLead) + EyeOffset;
            eye.localRotation = Quaternion.Euler(pitch + EyeTilt.x, head.Yaw + EyeTilt.y, 0f);
        }

        static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Look(Vector2 delta)
        {
            var turn = delta.x * LookSensitivity;
            // 座っている間は首だけ。立てば体ごと回る
            if (!head.Add(turn)) transform.Rotate(0f, turn, 0f);
            Pitch -= delta.y * LookSensitivity;
        }

        void Walk(Vector2 input)
        {
            var local = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            // Shift を押している間だけ速くする。入力の割り当てを増やさず、鍵盤を直に見る
            var keys = Keyboard.current;
            Running = local.sqrMagnitude > 0.01f && keys != null
                && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            body.SimpleMove(transform.TransformDirection(local) * Speed(Running, SpeedScale));
        }
    }
}
