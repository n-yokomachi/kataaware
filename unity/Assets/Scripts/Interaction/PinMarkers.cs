using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 調べられる物の上に立てる目印。地図の鋲のような形を、今できることの上にだけ出す。
    /// 済んだ物と、まだ前提が揃っていない物には立てない。
    /// 鋲は常にこちらを向き、遠くても同じ大きさに見えるよう距離で伸ばす
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class PinMarkers : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("複製のもと。切ったまま場面に置いておく")]
        [SerializeField] GameObject pin;
        [Tooltip("対象の上にどれだけ浮かせるか。メートル")]
        [SerializeField] float lift = 0.17f;
        [Tooltip("1 メートル先での高さ。メートル")]
        [SerializeField] float sizeAtOneMetre = 0.075f;
        [Tooltip("これより近いと大きくなりすぎるので、ここで頭打ちにする")]
        [SerializeField] float nearest = 0.8f;
        [Tooltip("上下に揺らす幅。メートル")]
        [SerializeField] float bob = 0.012f;
        [Tooltip("揺れの速さ")]
        [SerializeField] float bobSpeed = 2.2f;

        readonly List<IInteractable> items = new List<IInteractable>();
        readonly List<GameObject> pins = new List<GameObject>();
        Camera eye;

        void Start()
        {
            if (flow == null || pin == null)
            {
                enabled = false;
                return;
            }
            pin.SetActive(false);
            foreach (var item in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                items.Add(item);
                var made = Instantiate(pin, transform);
                made.name = "Pin_" + item.Id;
                made.SetActive(false);
                pins.Add(made);
            }
            eye = Camera.main;
        }

        void LateUpdate()
        {
            if (eye == null) eye = Camera.main;
            if (eye == null || flow.Progress == null) return;
            var done = flow.Progress.Done;
            var camPos = eye.transform.position;
            for (var i = 0; i < items.Count; i++)
            {
                var show = !flow.Completed && InteractionPicker.Marked(items[i], done);
                var made = pins[i];
                if (made.activeSelf != show) made.SetActive(show);
                if (!show) continue;
                var at = items[i].Position + Vector3.up * lift;
                at.y += Mathf.Sin(Time.time * bobSpeed + i) * bob;
                made.transform.position = at;
                var away = camPos - at;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) made.transform.rotation = Quaternion.LookRotation(-away.normalized, Vector3.up);
                var size = sizeAtOneMetre * Mathf.Max(nearest, Vector3.Distance(camPos, at));
                made.transform.localScale = Vector3.one * size;
            }
        }
    }
}
