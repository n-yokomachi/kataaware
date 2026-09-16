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
        public const float WalkSpeed = 2.6f;         // m/s
        public const float StandingEyeHeight = 1.6f;
        public const float PitchLimit = 80f;         // 度
        public const float LookSensitivity = 0.126f; // 度 / ピクセル。試作の 0.0022 rad/px と同じ

        [SerializeField] InputActionAsset actions;
        [SerializeField] Transform eye;

        CharacterController body;
        InputAction move;
        InputAction look;
        InputAction interact;
        float pitch;

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

        /// <summary>左右の向き。度。書き込むと体ごと向き直る。演出から正面へ戻すのに使う</summary>
        public float Yaw
        {
            get { return transform.eulerAngles.y; }
            set { transform.rotation = Quaternion.Euler(0f, value, 0f); }
        }

        /// <summary>上下の向き。度。書き込むと範囲に収まる。動作確認から視線を向けるのにも使う</summary>
        public float Pitch
        {
            get { return pitch; }
            set { pitch = Mathf.Clamp(value, -PitchLimit, PitchLimit); }
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

        void Update()
        {
            InteractPressed = false;
            if (CursorLocked)
            {
                InteractPressed = interact.WasPressedThisFrame();
                if (CanLook) Look(look.ReadValue<Vector2>());
                if (CanMove) Walk(move.ReadValue<Vector2>());
            }
            else
            {
                // ロックが外れている間はカーソルを見せる。ロックするためのクリックは調べる操作に使わない
                Cursor.visible = true;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Lock();
            }
            // 目の位置と向きは、ロックの有無にかかわらず毎フレーム書き直す。
            // 上乗せしていく形にすると、書き直されない間にずれが溜まって視界が回り続ける。
            // 傾きをこの順で組むと、左右の傾きが親の水平面で効くので、下を向いていても画面が回らない
            eye.localPosition = new Vector3(0f, EyeHeight, 0f) + EyeOffset;
            eye.localRotation = Quaternion.Euler(pitch + EyeTilt.x, EyeTilt.y, 0f);
        }

        static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * LookSensitivity, 0f);
            Pitch -= delta.y * LookSensitivity;
        }

        void Walk(Vector2 input)
        {
            var local = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            body.SimpleMove(transform.TransformDirection(local) * WalkSpeed);
        }
    }
}
