using UnityEngine;
using System;
using System.Collections;
using Category5.Player;
using Category5.Core;

namespace Category5
{

    // enchanter r - lightning bolt that buffs allies in an area, radius scales with charges
    public class EnchanterR : AbilityBase
    {
        [Header("Debug")]
        [SerializeField] private bool showDebugRadius = true;
        [SerializeField] private Color debugColor = new Color(1f, 1f, 0f, 0.2f);
        [SerializeField] private float debugDuration = 1.5f;

        public static event Action<Vector3, float, int> OnLightningStrike;

        private Coroutine _debugRoutine;
        private GameObject _debugSphere;

        public override void Execute()
        {
            if (!CanUse()) return;

            Vector3 position = transform.position;
            abilityManager.ExecuteEnchanterRBuffServerRpc(position);
        }

        public static void InvokeLightningStrike(Vector3 position, float radius, int alliesBuffed)
        {
            OnLightningStrike?.Invoke(position, radius, alliesBuffed);
        }

        public void ShowDebugSphere(Vector3 position, float radius)
        {
            if (!showDebugRadius) return;

            if (_debugRoutine != null)
            {
                StopCoroutine(_debugRoutine);
            }

            if (_debugSphere != null)
            {
                Destroy(_debugSphere);
            }

            _debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debugSphere.name = "enchanter_r_debug_sphere";
            _debugSphere.transform.position = position;
            _debugSphere.transform.localScale = Vector3.one * radius * 2f;

            Collider col = _debugSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer renderer = _debugSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = CreateDebugMaterial();
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", debugColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", debugColor);
                    renderer.material = mat;
                }
            }

            _debugRoutine = StartCoroutine(DestroyDebugSphereAfterDelay());
        }

        private IEnumerator DestroyDebugSphereAfterDelay()
        {
            yield return new WaitForSeconds(debugDuration);
            if (_debugSphere != null)
            {
                Destroy(_debugSphere);
                _debugSphere = null;
            }
        }

        private Material CreateDebugMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[EnchanterR] no suitable unlit shader found for debug sphere");
                return null;
            }

            Material mat = new Material(shader);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = 3000;
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }
    }
}
