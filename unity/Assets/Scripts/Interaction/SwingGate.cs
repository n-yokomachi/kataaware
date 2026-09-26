using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 調べて開ける戸。村の片割れの家の格子戸（村と庭の設計書 7 節）。
    ///
    /// 目を留めると、ほかの場面の調べる操作と同じ案内（<c>E  開ける</c>）を <see cref="HudView.SetPrompt"/> に出し、
    /// 調べる操作で開く。選び方は <see cref="InteractionPicker.Select"/> で、距離と視線の角度もほかの場面と同じ。
    /// 開くと戸の板（丁番を原点にした子）を y まわりに回し、閉じている間の当たりを切る。開いたままにする。
    ///
    /// **場面の進行（SceneFlow）には繋がない。** 必須や出来事はまだ入れない（設計書 4 節）。
    /// 場面の流れが入ったら、この案内と SceneFlow の案内がぶつからないよう、どちらかへまとめる
    /// </summary>
    public sealed class SwingGate : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("調べる対象。位置と案内の文（RoomScript の label）を持つ")]
        [SerializeField] Interactable item;
        [Tooltip("戸の板。丁番を原点にした子で、y まわりに回すと開く")]
        [SerializeField] Transform leaf;
        [Tooltip("開いたときの向き（度）。閉じた向きからの回り")]
        [SerializeField] float openYaw = 100f;
        [Tooltip("開き切るまでの秒")]
        [SerializeField] float seconds = 1f;
        [Tooltip("閉じている間だけ効く当たり")]
        [SerializeField] Collider shut;
        [SerializeField] AudioSource source;
        [Tooltip("開ける音。頭から clipLength 秒だけ鳴らす（開けると閉めるが一つに入った音から、開ける所だけ）")]
        [SerializeField] AudioClip clip;
        [SerializeField] float clipLength = 0.9f;

        readonly List<IInteractable> items = new List<IInteractable>(1);
        readonly HashSet<string> done = new HashSet<string>();
        Quaternion closed = Quaternion.identity;
        bool known;
        bool open;
        float t;
        bool prompting;

        public bool IsOpen { get { return open; } }

        void Awake()
        {
            Remember();
            items.Clear();
            if (item != null) items.Add(item);
        }

        void Update()
        {
            if (open)
            {
                if (t < 1f)
                {
                    t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, seconds));
                    Pose(t);
                }
                return;
            }
            if (player == null || hud == null || items.Count == 0) return;
            var eye = player.Eye;
            var picked = eye != null ? InteractionPicker.Select(eye.position, eye.forward, items, done) : null;
            if (picked != null || prompting)
            {
                hud.SetPrompt(picked != null ? "E  " + picked.Label : null);
                prompting = picked != null;
            }
            if (picked != null && player.InteractPressed) Open();
        }

        /// <summary>開ける。当たりを切り、音を鳴らし、板を回し始める</summary>
        public void Open()
        {
            if (open) return;
            open = true;
            t = 0f;
            if (shut != null) shut.enabled = false;
            if (hud != null && prompting) hud.SetPrompt(null);
            prompting = false;
            if (source != null && clip != null)
            {
                source.clip = clip;
                source.Play();
                source.SetScheduledEndTime(AudioSettings.dspTime + clipLength);
            }
        }

        /// <summary>開き具合（0 で閉じ、1 で開き切る）の形に置く。確かめの道具が play mode に入らずに開いた形を撮るのにも使う</summary>
        public void Pose(float amount)
        {
            if (leaf == null) return;
            Remember();
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
            leaf.localRotation = closed * Quaternion.Euler(0f, openYaw * k, 0f);
        }

        /// <summary>閉じた向きを一度だけ覚える。エディタでは Awake が鳴らないので、使う所で覚える</summary>
        void Remember()
        {
            if (known || leaf == null) return;
            closed = leaf.localRotation;
            known = true;
        }

        /// <summary>エディタで開いた形と閉じた形を行き来する。当たりも合わせる。保存する形は閉じた形</summary>
        public void Set(bool opened)
        {
            Remember();
            open = opened;
            t = opened ? 1f : 0f;
            if (shut != null) shut.enabled = !opened;
            Pose(t);
        }
    }
}
