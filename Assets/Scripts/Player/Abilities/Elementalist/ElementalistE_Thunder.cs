using UnityEngine;
using Category5.Player;

namespace Category5
{
    // elementalist thunder e ability - short-range arc that damages, pushes back, and stuns enemies
    public class ElementalistE_Thunder : AbilityBase
    {
        [Header("thunder arc settings")]
        [SerializeField] private float arcRange = 5f;
        [SerializeField] private float arcAngle = 120f;
        [SerializeField] private float knockbackForce = 15f;
        [SerializeField] private float stunDuration = 1f;
        [SerializeField] private float stunDelay = 0.12f;
        [SerializeField] private LayerMask enemyLayers = 1 << 6;

        [Header("debug")]
        [SerializeField] private bool showDebugArc = true;
        [SerializeField] private float debugArcDuration = 0.75f;
        [SerializeField] private float debugLineWidth = 0.04f;
        [SerializeField] private int debugArcSegments = 18;
        [SerializeField] private Color debugArcColor = new Color(0.1f, 0.1f, 0.2f, 0.35f);

        // events for vfx/sfx hooks
        public static event System.Action<Vector3, Vector3, float, float> OnThunderArcExecute;

        // public method to invoke event from PlayerAbilityManager rpcs
        public static void InvokeThunderArcExecute(Vector3 position, Vector3 forward, float range, float angle)
        {
            OnThunderArcExecute?.Invoke(position, forward, range, angle);
        }

        public override void Execute()
        {
            Vector3 position = playerController.transform.position + Vector3.up * 1f;
            Vector3 forward = playerController.transform.forward;

            Debug.Log($"[ElementalistE_Thunder] executing thunder arc at {position}, forward {forward}");

            OnThunderArcExecute?.Invoke(position, forward, arcRange, arcAngle);
            SpawnVfx(position);
            PlayAudio(position);

            if (showDebugArc)
            {
                CreateDebugArcVisual(position, forward, arcRange, arcAngle);
            }

            // send coefficient to server, server calculates damage
            float coefficient = abilityData.damageCoefficient;

            // request server to execute the arc damage
            abilityManager.ExecuteThunderArcServerRpc(
                position, forward, coefficient, arcAngle, arcRange, knockbackForce, stunDuration, stunDelay, enemyLayers.value
            );
        }

        // gizmos showing the cone
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 origin = playerController.transform.position + Vector3.up * 1f;
            Vector3 forward = playerController.transform.forward;

            Gizmos.color = Color.yellow;

            // draw range sphere
            Gizmos.DrawWireSphere(origin, arcRange);

            // draw cone edges
            float halfAngle = arcAngle * 0.5f;

            Vector3 leftEdge = Quaternion.Euler(0, -halfAngle, 0) * forward * arcRange;
            Vector3 rightEdge = Quaternion.Euler(0, halfAngle, 0) * forward * arcRange;

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, origin + leftEdge);
            Gizmos.DrawLine(origin, origin + rightEdge);

            // draw arc between edges
            int segments = 12;
            float angleStep = arcAngle / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = -halfAngle + i * angleStep;
                float angle2 = -halfAngle + (i + 1) * angleStep;
                Vector3 p1 = origin + Quaternion.Euler(0, angle1, 0) * forward * arcRange;
                Vector3 p2 = origin + Quaternion.Euler(0, angle2, 0) * forward * arcRange;
                Gizmos.DrawLine(p1, p2);
            }
        }


		// THIS IS ONLY FOR DEBUG IGNORE EVERYTHING BELOW THIS COMMENT I WILL DELETE LATER
        private void CreateDebugArcVisual(Vector3 origin, Vector3 forward, float range, float angle)
        {
            GameObject debugRoot = new GameObject("thunder_e_debug_arc");
            debugRoot.transform.position = origin;
            debugRoot.transform.rotation = Quaternion.identity;

            float halfAngle = angle * 0.5f;

            // arc line
            LineRenderer arcLine = CreateLineRenderer(debugRoot.transform, "arc", debugArcColor, debugLineWidth);
            int segments = Mathf.Max(6, debugArcSegments);
            arcLine.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 dir = Quaternion.Euler(0f, currentAngle, 0f) * forward;
                arcLine.SetPosition(i, origin + dir.normalized * range);
            }

            // left edge
            LineRenderer leftEdge = CreateLineRenderer(debugRoot.transform, "left", debugArcColor, debugLineWidth);
            Vector3 leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            leftEdge.positionCount = 2;
            leftEdge.SetPosition(0, origin);
            leftEdge.SetPosition(1, origin + leftDir.normalized * range);

            // right edge
            LineRenderer rightEdge = CreateLineRenderer(debugRoot.transform, "right", debugArcColor, debugLineWidth);
            Vector3 rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;
            rightEdge.positionCount = 2;
            rightEdge.SetPosition(0, origin);
            rightEdge.SetPosition(1, origin + rightDir.normalized * range);

            Destroy(debugRoot, debugArcDuration);
        }

        private LineRenderer CreateLineRenderer(Transform parent, string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;

            Material mat = CreateDebugMaterial();
            if (mat != null)
            {
                mat.color = color;
                lr.material = mat;
            }

            return lr;
        }

        private Material CreateDebugMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[ElementalistE_Thunder] no suitable unlit shader found for debug arc");
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
