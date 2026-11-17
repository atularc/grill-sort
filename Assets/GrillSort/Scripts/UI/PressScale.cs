using UnityEngine;
using UnityEngine.EventSystems;

namespace GrillSort
{
    /// <summary>Squishy press feedback for buttons.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressed = .9f;
        Vector3 _base = Vector3.one;
        float _target = 1f, _cur = 1f;

        void Awake() => _base = transform.localScale;
        public void OnPointerDown(PointerEventData e) => _target = pressed;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        void OnDisable() { _target = _cur = 1f; transform.localScale = _base; }

        void Update()
        {
            if (Mathf.Approximately(_cur, _target)) return;
            _cur = Mathf.MoveTowards(_cur, _target, Time.unscaledDeltaTime * 3.5f);
            float k = _target < 1f ? _cur : 1f + Mathf.Sin((1f - _cur) / (1f - pressed) * Mathf.PI) * .04f;
            transform.localScale = _base * (_target < 1f ? _cur : Mathf.Max(_cur, k));
        }
    }
}
