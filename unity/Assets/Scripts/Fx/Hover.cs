using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 風に乗って浮かぶ物（エンディングの海辺の崖のカモメ）。据えた所を中心に、ゆっくり上下と前後に揺れ、少し傾く。
    /// 崖に吹き上げる風の中のカモメは、ほとんど羽ばたかずに同じ所に留まって見えるので、動かない遠景の中で揺らすだけにする
    /// </summary>
    public sealed class Hover : MonoBehaviour
    {
        [Tooltip("上下の揺れの幅。m")]
        [SerializeField] float lift = 0.8f;
        [Tooltip("前後と左右の揺れの幅。m")]
        [SerializeField] float drift = 1.5f;
        [Tooltip("揺れの速さ。ラジアン/秒")]
        [SerializeField] float rate = 0.6f;
        [Tooltip("傾き。度")]
        [SerializeField] float bank = 8f;
        [Tooltip("揺れの位相のずらし（一羽ずつ変える）")]
        [SerializeField] float phase;

        Vector3 home;
        Quaternion turn;

        void Awake()
        {
            home = transform.localPosition;
            turn = transform.localRotation;
        }

        void Update()
        {
            var t = Time.time * rate + phase;
            transform.localPosition = home + new Vector3(Mathf.Sin(t * 0.7f) * drift, Mathf.Sin(t) * lift, Mathf.Sin(t * 0.43f + 1f) * drift);
            transform.localRotation = turn * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f + 0.5f) * bank);
        }
    }
}
