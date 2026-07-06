using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Category5.Player.WindRiding
{
    // defines a wind tunnel along a unity spline
    // the procedural gen team provides the SplineContainer, this component wraps it
    // with a rider-friendly API and configurable radius
    [RequireComponent(typeof(SplineContainer))]
    public class WindTunnel : MonoBehaviour
    {
        [Header("Tunnel Settings")]
        [SerializeField] private float tunnelRadius = 5f;

        [Header("Gizmo Display")]
        [SerializeField] private int gizmoSegments = 40;
        [SerializeField] private int gizmoCircleSegments = 16;
        [SerializeField] private Color gizmoPathColor = new Color(0.3f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color gizmoRadiusColor = new Color(0.3f, 0.8f, 1f, 0.15f);

        public List<PlayerController> riders = new List<PlayerController>();

        private SplineContainer _splineContainer;
        private float _cachedLength = -1f;

        public float TunnelRadius => tunnelRadius;

        public void SetTunnelRadius(float radius)
        {
            tunnelRadius = Mathf.Max(0.1f, radius);
        }

        public float SplineLength
        {
            get
            {
                if (_splineContainer == null || _cachedLength <= 0f)
                    RefreshSplineData();
                return _cachedLength;
            }
        }

        private void Awake()
        {
            RefreshSplineData();
        }

        public void RefreshSplineData()
        {
            _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null || _splineContainer.Spline == null)
            {
                Debug.LogError("WindTunnel: no SplineContainer or Spline found on this object");
                _cachedLength = 0f;
                return;
            }

            if (_splineContainer.Spline.Count < 2)
            {
                _cachedLength = 0f;
                return;
            }

            _cachedLength = _splineContainer.CalculateLength();
        }

        // evaluate world position at normalized t (0-1) along the spline
        public Vector3 EvaluatePosition(float t)
        {
            if (_splineContainer == null) _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null || _splineContainer.Spline == null) return transform.position;

            t = Mathf.Clamp01(t);
            var localPos = SplineUtility.EvaluatePosition(_splineContainer.Spline, t);
            return transform.TransformPoint((Vector3)(float3)localPos);
        }

        // evaluate normalized forward tangent at t
        public Vector3 EvaluateTangent(float t)
        {
            t = Mathf.Clamp01(t);
            var localTangent = SplineUtility.EvaluateTangent(_splineContainer.Spline, t);
            Vector3 worldTangent = transform.TransformDirection((Vector3)(float3)localTangent);
            return worldTangent.sqrMagnitude < 0.001f ? Vector3.forward : worldTangent.normalized;
        }

        // evaluate up vector at t for banking on curves
        public Vector3 EvaluateUp(float t)
        {
            t = Mathf.Clamp01(t);
            var localUp = SplineUtility.EvaluateUpVector(_splineContainer.Spline, t);
            Vector3 worldUp = transform.TransformDirection((Vector3)(float3)localUp);
            return worldUp.sqrMagnitude < 0.001f ? Vector3.up : worldUp.normalized;
        }

        // get the right vector perpendicular to tangent, projected onto the horizontal plane
        // used for lateral sway offset
        public Vector3 GetRightVector(float t)
        {
            Vector3 tangent = EvaluateTangent(t);
            Vector3 up = EvaluateUp(t);

            // cross tangent with up to get right, then flatten to horizontal
            Vector3 right = Vector3.Cross(tangent, up).normalized;
            if (right == Vector3.zero)
            {
                // fallback: use world up to derive right
                right = Vector3.Cross(tangent, Vector3.up).normalized;
            }
            return right;
        }

        // find the closest normalized t value for a world position
        public float GetNearestT(Vector3 worldPos)
        {
            var localPos = transform.InverseTransformPoint(worldPos);
            SplineUtility.GetNearestPoint(
                _splineContainer.Spline,
                (float3)localPos,
                out _,
                out float nearestT
            );
            return nearestT;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var container = GetComponent<SplineContainer>();
            if (container == null || container.Spline == null || container.Spline.Count < 2)
                return;

            // draw path line
            Gizmos.color = gizmoPathColor;
            Vector3 prevPos = EvalGizmoPos(container, 0f);
            for (int i = 1; i <= gizmoSegments; i++)
            {
                float t = (float)i / gizmoSegments;
                Vector3 pos = EvalGizmoPos(container, t);
                Gizmos.DrawLine(prevPos, pos);
                prevPos = pos;
            }

            // draw radius circles at intervals
            Gizmos.color = gizmoRadiusColor;
            int circleCount = Mathf.Max(4, gizmoSegments / 4);
            for (int i = 0; i <= circleCount; i++)
            {
                float t = (float)i / circleCount;
                DrawGizmoCircle(container, t);
            }

            // draw start and end markers
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(EvalGizmoPos(container, 0f), 0.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(EvalGizmoPos(container, 1f), 0.5f);
        }

        private Vector3 EvalGizmoPos(SplineContainer container, float t)
        {
            var localPos = SplineUtility.EvaluatePosition(container.Spline, t);
            return transform.TransformPoint((Vector3)(float3)localPos);
        }

        private void DrawGizmoCircle(SplineContainer container, float t)
        {
            Vector3 center = EvalGizmoPos(container, t);
            var localTangent = SplineUtility.EvaluateTangent(container.Spline, t);
            Vector3 tangent = transform.TransformDirection((Vector3)(float3)localTangent);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
            tangent.Normalize();

            var localUp = SplineUtility.EvaluateUpVector(container.Spline, t);
            Vector3 up = transform.TransformDirection((Vector3)(float3)localUp);
            if (up.sqrMagnitude < 0.001f) up = Vector3.up;
            up.Normalize();

            Vector3 right = Vector3.Cross(tangent, up).normalized;
            if (right == Vector3.zero) right = Vector3.Cross(tangent, Vector3.up).normalized;

            Vector3 prevPoint = center + right * tunnelRadius;
            for (int i = 1; i <= gizmoCircleSegments; i++)
            {
                float angle = (float)i / gizmoCircleSegments * 360f * Mathf.Deg2Rad;
                Vector3 point = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * tunnelRadius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
#endif
    }
}
