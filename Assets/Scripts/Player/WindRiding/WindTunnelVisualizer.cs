using System.Collections.Generic;
using UnityEngine;

namespace Category5.Player.WindRiding
{
    // simple runtime tunnel visuals for testing
    // draws a center line and ring markers along the spline
    [RequireComponent(typeof(WindTunnel))]
    public class WindTunnelVisualizer : MonoBehaviour
    {
        [Header("Center Line")]
        [SerializeField] private int lineSamples = 48;
        [SerializeField] private float lineWidth = 0.35f;
        [SerializeField] private Color lineColor = new Color(0.3f, 0.85f, 1f, 0.75f);

        [Header("Ring Markers")]
        [SerializeField] private bool drawRings = true;
        [SerializeField] private float ringSpacing = 8f;
        [SerializeField] private int ringSegments = 20;
        [SerializeField] private float ringWidth = 0.08f;
        [SerializeField] private float ringRadiusMultiplier = 0.95f;
        [SerializeField] private Color ringColor = new Color(0.75f, 0.95f, 1f, 0.45f);

        private WindTunnel _windTunnel;
        private Transform _visualRoot;
        private LineRenderer _centerLine;
        private readonly List<LineRenderer> _ringRenderers = new List<LineRenderer>();
        private Material _sharedMaterial;

        private void Awake()
        {
            _windTunnel = GetComponent<WindTunnel>();
        }

        private void Start()
        {
            RefreshVisuals();
        }

        private void OnDisable()
        {
            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_sharedMaterial != null)
            {
                Destroy(_sharedMaterial);
            }
        }

        public void RefreshVisuals()
        {
            if (_windTunnel == null)
            {
                _windTunnel = GetComponent<WindTunnel>();
            }

            if (_windTunnel == null || _windTunnel.SplineLength <= 0f)
            {
                return;
            }

            EnsureVisualRoot();
            BuildCenterLine();
            BuildRings();
        }

        private void EnsureVisualRoot()
        {
            if (_visualRoot == null)
            {
                Transform existing = transform.Find("runtime tunnel visuals");
                if (existing != null)
                {
                    _visualRoot = existing;
                }
                else
                {
                    GameObject visualRoot = new GameObject("runtime tunnel visuals");
                    visualRoot.transform.SetParent(transform, false);
                    _visualRoot = visualRoot.transform;
                }
            }

            _visualRoot.gameObject.SetActive(true);

            if (_sharedMaterial == null)
            {
                _sharedMaterial = CreateLineMaterial();
            }

            if (_centerLine == null)
            {
                _centerLine = CreateLineRenderer(_visualRoot, "center line", lineWidth, lineColor);
            }
        }

        private void BuildCenterLine()
        {
            int sampleCount = Mathf.Max(2, lineSamples);
            _centerLine.positionCount = sampleCount;
            _centerLine.startWidth = lineWidth;
            _centerLine.endWidth = lineWidth;
            _centerLine.startColor = lineColor;
            _centerLine.endColor = lineColor;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                _centerLine.SetPosition(i, _windTunnel.EvaluatePosition(t));
            }
        }

        private void BuildRings()
        {
            if (!drawRings)
            {
                SetUnusedRingsInactive(0);
                return;
            }

            float spacing = Mathf.Max(0.5f, ringSpacing);
            int segmentCount = Mathf.Max(6, ringSegments);
            int ringCount = Mathf.Max(2, Mathf.CeilToInt(_windTunnel.SplineLength / spacing) + 1);
            float radius = Mathf.Max(0.1f, _windTunnel.TunnelRadius * ringRadiusMultiplier);

            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                LineRenderer ringRenderer = GetOrCreateRing(ringIndex);
                ringRenderer.gameObject.SetActive(true);
                ringRenderer.startWidth = ringWidth;
                ringRenderer.endWidth = ringWidth;
                ringRenderer.startColor = ringColor;
                ringRenderer.endColor = ringColor;
                ringRenderer.positionCount = segmentCount + 1;

                float t = ringCount == 1 ? 0f : ringIndex / (float)(ringCount - 1);
                Vector3 center = _windTunnel.EvaluatePosition(t);
                Vector3 up = _windTunnel.EvaluateUp(t);
                Vector3 right = _windTunnel.GetRightVector(t);

                if (up.sqrMagnitude < 0.001f) up = Vector3.up;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                up.Normalize();
                right.Normalize();

                for (int segmentIndex = 0; segmentIndex <= segmentCount; segmentIndex++)
                {
                    float angle = segmentIndex / (float)segmentCount * Mathf.PI * 2f;
                    Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                    ringRenderer.SetPosition(segmentIndex, center + offset);
                }
            }

            SetUnusedRingsInactive(ringCount);
        }

        private LineRenderer GetOrCreateRing(int index)
        {
            while (_ringRenderers.Count <= index)
            {
                LineRenderer ring = CreateLineRenderer(_visualRoot, $"ring {_ringRenderers.Count}", ringWidth, ringColor);
                ring.loop = true;
                _ringRenderers.Add(ring);
            }

            return _ringRenderers[index];
        }

        private void SetUnusedRingsInactive(int usedCount)
        {
            for (int i = usedCount; i < _ringRenderers.Count; i++)
            {
                if (_ringRenderers[i] != null)
                {
                    _ringRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        private LineRenderer CreateLineRenderer(Transform parent, string objectName, float width, Color color)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);

            LineRenderer lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            if (_sharedMaterial != null)
            {
                lineRenderer.material = _sharedMaterial;
            }

            return lineRenderer;
        }

        private Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("WindTunnelVisualizer: no suitable shader found for runtime tunnel visuals");
                return null;
            }

            Material material = new Material(shader);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            material.renderQueue = 3000;
            return material;
        }
    }
}