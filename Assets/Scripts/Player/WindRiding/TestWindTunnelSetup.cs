using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Category5.Player.WindRiding
{
    // creates a simple test wind tunnel between two scene-placed launch pads
    // assign the start and end pads in the inspector and this builds the spline between them on start
    public class TestWindTunnelSetup : MonoBehaviour
    {
        private const string GeneratedTunnelName = "GeneratedWindTunnel";
        private const int PreviewSegments = 32;

        [Header("Launch Pad References")]
        [SerializeField] private WindLaunchPad startLaunchPad;
        [SerializeField] private WindLaunchPad endLaunchPad;

        [Header("Tunnel Shape")]
        [SerializeField] private float endpointHeightOffset = 2f;
        [SerializeField] private float tunnelHeight = 15f;
        [SerializeField] private float curveWidth = 15f;
        [SerializeField] private float tangentLengthMultiplier = 0.2f;
        [SerializeField] private float tunnelRadius = 5f;

        [Header("Preview Gizmos")]
        [SerializeField] private Color previewPathColor = new Color(0.25f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color previewHandleColor = new Color(1f, 0.75f, 0.2f, 0.8f);
        [SerializeField] private Color previewPadLinkColor = new Color(0.4f, 1f, 0.5f, 0.7f);

        private void Start()
        {
            BuildTestTunnel();
        }

        private void BuildTestTunnel()
        {
            if (startLaunchPad == null || endLaunchPad == null)
            {
                Debug.LogError("TestWindTunnelSetup: assign both start and end launch pads in the inspector");
                return;
            }

            if (startLaunchPad == endLaunchPad)
            {
                Debug.LogError("TestWindTunnelSetup: start and end launch pads must be different objects");
                return;
            }

            CleanupExistingTunnel();

            if (!TryBuildPreviewData(out PreviewData preview))
            {
                Debug.LogError("TestWindTunnelSetup: could not build preview data from the assigned launch pads");
                return;
            }

            float tunnelLength = preview.TunnelLength;

            // create the tunnel object with spline
            var tunnelObj = new GameObject(GeneratedTunnelName);
            tunnelObj.transform.SetParent(transform);
            tunnelObj.transform.localPosition = Vector3.zero;
            tunnelObj.transform.localRotation = Quaternion.identity;

            var splineContainer = tunnelObj.AddComponent<SplineContainer>();
            var windTunnel = tunnelObj.AddComponent<WindTunnel>();

            windTunnel.SetTunnelRadius(tunnelRadius);

            // build a gentle elevated s-curve between the two pads
            var spline = splineContainer.Spline;
            spline.Clear();

            var knots = new BezierKnot[]
            {
                new BezierKnot(
                    ToLocalPoint(tunnelObj.transform, preview.P0),
                    ToLocalDirection(tunnelObj.transform, preview.P0InTangent),
                    ToLocalDirection(tunnelObj.transform, preview.P0OutTangent)
                ),
                new BezierKnot(
                    ToLocalPoint(tunnelObj.transform, preview.P1),
                    ToLocalDirection(tunnelObj.transform, preview.P1InTangent),
                    ToLocalDirection(tunnelObj.transform, preview.P1OutTangent)
                ),
                new BezierKnot(
                    ToLocalPoint(tunnelObj.transform, preview.P2),
                    ToLocalDirection(tunnelObj.transform, preview.P2InTangent),
                    ToLocalDirection(tunnelObj.transform, preview.P2OutTangent)
                ),
                new BezierKnot(
                    ToLocalPoint(tunnelObj.transform, preview.P3),
                    ToLocalDirection(tunnelObj.transform, preview.P3InTangent),
                    ToLocalDirection(tunnelObj.transform, preview.P3OutTangent)
                ),
                new BezierKnot(
                    ToLocalPoint(tunnelObj.transform, preview.P4),
                    ToLocalDirection(tunnelObj.transform, preview.P4InTangent),
                    ToLocalDirection(tunnelObj.transform, preview.P4OutTangent)
                ),
            };

            foreach (var knot in knots)
            {
                spline.Add(knot, TangentMode.Continuous);
            }

            windTunnel.RefreshSplineData();

            var visualizer = tunnelObj.AddComponent<Category5.Player.WindRiding.WindTunnelVisualizer>();
            visualizer.RefreshVisuals();

            startLaunchPad.ConfigureTunnel(windTunnel, true);
            endLaunchPad.ConfigureTunnel(windTunnel, false);

            Debug.Log($"TestWindTunnelSetup: created test tunnel between '{startLaunchPad.name}' and '{endLaunchPad.name}' ({tunnelLength:F1}m long)");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryBuildPreviewData(out PreviewData preview))
            {
                return;
            }

            DrawPreviewCurve(preview);
            DrawPreviewHandles(preview);
            DrawPreviewPadLinks(preview);
        }
#endif

        private void CleanupExistingTunnel()
        {
            Transform existingTunnel = transform.Find(GeneratedTunnelName);
            if (existingTunnel != null)
            {
                Destroy(existingTunnel.gameObject);
            }
        }

        private Vector3 GetPlanarForward(Transform source, Vector3 fallback)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(source.forward, Vector3.up).normalized;
            if (planarForward.sqrMagnitude < 0.001f)
            {
                return fallback.normalized;
            }

            return planarForward;
        }

        private float3 ToLocalPoint(Transform target, Vector3 worldPoint)
        {
            return (float3)target.InverseTransformPoint(worldPoint);
        }

        private float3 ToLocalDirection(Transform target, Vector3 worldDirection)
        {
            return (float3)target.InverseTransformDirection(worldDirection);
        }

        private bool TryBuildPreviewData(out PreviewData preview)
        {
            preview = default;

            if (startLaunchPad == null || endLaunchPad == null || startLaunchPad == endLaunchPad)
            {
                return false;
            }

            Vector3 startPadPos = startLaunchPad.transform.position;
            Vector3 endPadPos = endLaunchPad.transform.position;
            Vector3 startPos = startPadPos + Vector3.up * endpointHeightOffset;
            Vector3 endPos = endPadPos + Vector3.up * endpointHeightOffset;
            Vector3 tunnelDirection = endPos - startPos;
            float tunnelLength = tunnelDirection.magnitude;
            if (tunnelLength < 1f)
            {
                return false;
            }

            Vector3 forward = tunnelDirection.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }

            Vector3 startForward = GetPlanarForward(startLaunchPad.transform, forward);
            Vector3 endForward = GetPlanarForward(endLaunchPad.transform, -forward);
            float tangentLength = tunnelLength * tangentLengthMultiplier;

            preview = new PreviewData
            {
                TunnelLength = tunnelLength,
                P0 = startPos,
                P1 = Vector3.Lerp(startPos, endPos, 0.25f) + right * (curveWidth * 0.5f) + Vector3.up * (tunnelHeight * 0.7f),
                P2 = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * tunnelHeight,
                P3 = Vector3.Lerp(startPos, endPos, 0.75f) - right * (curveWidth * 0.5f) + Vector3.up * (tunnelHeight * 0.7f),
                P4 = endPos,
                StartPadPos = startPadPos,
                EndPadPos = endPadPos,
                P0InTangent = -startForward * tangentLength * 0.5f,
                P0OutTangent = startForward * tangentLength,
                P1InTangent = -forward * tangentLength * 0.5f,
                P1OutTangent = forward * tangentLength * 0.5f,
                P2InTangent = -forward * tangentLength * 0.6f,
                P2OutTangent = forward * tangentLength * 0.6f,
                P3InTangent = -forward * tangentLength * 0.5f,
                P3OutTangent = forward * tangentLength * 0.5f,
                P4InTangent = -endForward * tangentLength,
                P4OutTangent = endForward * tangentLength * 0.5f,
            };

            return true;
        }

#if UNITY_EDITOR
        private void DrawPreviewCurve(PreviewData preview)
        {
            Gizmos.color = previewPathColor;

            Vector3 previous = preview.P0;
            for (int i = 1; i <= PreviewSegments; i++)
            {
                float t = (float)i / PreviewSegments;
                Vector3 current = EvaluatePreviewPoint(preview, t);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            Gizmos.DrawWireSphere(preview.P0, 0.4f);
            Gizmos.DrawWireSphere(preview.P4, 0.4f);
        }

        private void DrawPreviewHandles(PreviewData preview)
        {
            Gizmos.color = previewHandleColor;
            DrawHandle(preview.P0, preview.P0 + preview.P0OutTangent);
            DrawHandle(preview.P1, preview.P1 + preview.P1InTangent, preview.P1 + preview.P1OutTangent);
            DrawHandle(preview.P2, preview.P2 + preview.P2InTangent, preview.P2 + preview.P2OutTangent);
            DrawHandle(preview.P3, preview.P3 + preview.P3InTangent, preview.P3 + preview.P3OutTangent);
            DrawHandle(preview.P4, preview.P4 + preview.P4InTangent);
        }

        private void DrawPreviewPadLinks(PreviewData preview)
        {
            Gizmos.color = previewPadLinkColor;
            Gizmos.DrawLine(preview.StartPadPos, preview.P0);
            Gizmos.DrawLine(preview.EndPadPos, preview.P4);
            Gizmos.DrawWireSphere(preview.StartPadPos, 0.2f);
            Gizmos.DrawWireSphere(preview.EndPadPos, 0.2f);
        }

        private void DrawHandle(Vector3 point, Vector3 handle)
        {
            Gizmos.DrawLine(point, handle);
            Gizmos.DrawWireSphere(handle, 0.18f);
        }

        private void DrawHandle(Vector3 point, Vector3 inHandle, Vector3 outHandle)
        {
            DrawHandle(point, inHandle);
            DrawHandle(point, outHandle);
        }
#endif

        private Vector3 EvaluatePreviewPoint(PreviewData preview, float t)
        {
            t = Mathf.Clamp01(t);
            float scaledT = t * 4f;
            int segment = Mathf.Min(3, Mathf.FloorToInt(scaledT));
            float segmentT = scaledT - segment;

            switch (segment)
            {
                case 0:
                    return EvaluateCubicBezier(preview.P0, preview.P0 + preview.P0OutTangent, preview.P1 + preview.P1InTangent, preview.P1, segmentT);
                case 1:
                    return EvaluateCubicBezier(preview.P1, preview.P1 + preview.P1OutTangent, preview.P2 + preview.P2InTangent, preview.P2, segmentT);
                case 2:
                    return EvaluateCubicBezier(preview.P2, preview.P2 + preview.P2OutTangent, preview.P3 + preview.P3InTangent, preview.P3, segmentT);
                default:
                    return EvaluateCubicBezier(preview.P3, preview.P3 + preview.P3OutTangent, preview.P4 + preview.P4InTangent, preview.P4, segmentT);
            }
        }

        private Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0 +
                   3f * oneMinusT * oneMinusT * t * p1 +
                   3f * oneMinusT * t * t * p2 +
                   t * t * t * p3;
        }

        private struct PreviewData
        {
            public float TunnelLength;
            public Vector3 P0;
            public Vector3 P1;
            public Vector3 P2;
            public Vector3 P3;
            public Vector3 P4;
            public Vector3 StartPadPos;
            public Vector3 EndPadPos;
            public Vector3 P0InTangent;
            public Vector3 P0OutTangent;
            public Vector3 P1InTangent;
            public Vector3 P1OutTangent;
            public Vector3 P2InTangent;
            public Vector3 P2OutTangent;
            public Vector3 P3InTangent;
            public Vector3 P3OutTangent;
            public Vector3 P4InTangent;
            public Vector3 P4OutTangent;
        }
    }
}
