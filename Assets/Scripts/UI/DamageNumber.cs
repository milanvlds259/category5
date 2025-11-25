using UnityEngine;
using TMPro;
using System.Collections;

namespace Category5.UI
{
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float floatSpeed = 2f; // speed in world units
        [SerializeField] private Vector3 randomSpread = new Vector3(0.5f, 0.5f, 0.5f);

        private Vector3 _initialWorldPosition;
        private float _timer;
        private Camera _mainCamera;

        public void Initialize(int damage, Vector3 worldPosition)
        {
            _mainCamera = Camera.main;
            if (text != null) text.text = damage.ToString();
            
            // add randomness so numbers don't stack perfectly
            _initialWorldPosition = worldPosition + new Vector3(
                Random.Range(-randomSpread.x, randomSpread.x),
                Random.Range(-randomSpread.y, randomSpread.y),
                Random.Range(-randomSpread.z, randomSpread.z)
            );

            _timer = 0f;
        }

        private void Update()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            _timer += Time.deltaTime;
            if (_timer >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // calculate current world position (floating up)
            Vector3 currentWorldPos = _initialWorldPosition + Vector3.up * (floatSpeed * _timer);

            // convert to screen position
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(currentWorldPos);

            // check if target is behind the camera
            if (screenPos.z < 0)
            {
                if (text != null) text.enabled = false;
            }
            else
            {
                if (text != null) text.enabled = true;
                transform.position = screenPos;
            }

            // fade out
            if (text != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, _timer / lifetime);
                text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
            }
        }
    }
}
