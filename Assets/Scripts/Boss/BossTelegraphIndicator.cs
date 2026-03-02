using UnityEngine;

namespace Category5.Boss
{
    // procedural ground indicator shown during boss attack telegraph phases
    // no prefab or shader assets required :) builds its own mesh and material at runtime
    // circle attacks fill like a pie chart outward; sweep attacks fill symmetrically from the center forward angle
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class BossTelegraphIndicator : MonoBehaviour
    {
        public enum IndicatorShape { Circle, Arc }

        // config

        public IndicatorShape shape = IndicatorShape.Circle;
        public float radius = 3f;         // disc radius for circle, beam length for arc
        public float arcAngle = 90f;      // total fan angle in degrees (arc mode only)
        public Color baseColor = new Color(1f, 0.3f, 0.2f, 0.45f);
        public float telegraphDuration = 1.5f;

        [Header("pulse settings")]
        public float pulseStartFill = 0.75f;     // fill% above which pulsing begins
        public float pulseMinAlpha = 0.15f;
        public float pulseMaxAlpha = 0.75f;
        public float pulseBaseFrequency = 2f;    // hz at pulse start
        public float pulseMaxFrequency = 7f;     // hz at fill = 1

        // mesh resolution
        private const int CIRCLE_SEGMENTS = 48;
        private const int ARC_SEGMENTS_HALF = 20; // segments on each side of arc center

        // runtime state

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private float _elapsed;

        // optional transform to follow each frame (boss transform)
        // offset is in the boss's local horizontal plane (XZ only, Y ignored)
        private Transform _followTarget;
        private Vector3 _followLocalOffsetXZ;

        // factory

        // creates and initializes an indicator at world position, not parented to anything
        public static BossTelegraphIndicator Create(
            IndicatorShape shape, float radius, float arcAngle,
            Color color, float duration, Vector3 worldPosition)
        {
            var go = new GameObject("BossTelegraphIndicator");
            go.transform.position = worldPosition;

            var indicator = go.AddComponent<BossTelegraphIndicator>();
            indicator.shape = shape;
            indicator.radius = radius;
            indicator.arcAngle = arcAngle;
            indicator.baseColor = color;
            indicator.telegraphDuration = duration;

            return indicator;
        }

        // lifecycle
        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh();
            _mesh.name = "TelegraphMesh";
            _meshFilter.mesh = _mesh;

            _material = CreateMaterial();
            _meshRenderer.material = _material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        private void Update()
        {
            // track boss position/rotation on both server and clients
            if (_followTarget != null)
            {
                float groundY = _followTarget.position.y + 0.02f;
                // rotate the local offset by the boss's current Y rotation
                Vector3 worldOffset = Quaternion.Euler(0f, _followTarget.eulerAngles.y, 0f) * _followLocalOffsetXZ;
                transform.position = new Vector3(
                    _followTarget.position.x + worldOffset.x,
                    groundY,
                    _followTarget.position.z + worldOffset.z);
                transform.rotation = Quaternion.Euler(0f, _followTarget.eulerAngles.y, 0f);
            }
            _elapsed += Time.deltaTime;

            float fillProgress = Mathf.Clamp01(_elapsed / telegraphDuration);
            float alpha = ComputeAlpha(fillProgress);
            SetMaterialColor(new Color(baseColor.r, baseColor.g, baseColor.b, alpha));

            if (shape == IndicatorShape.Circle)
                BuildDiscMesh(radius, fillProgress * 360f);
            else
                BuildArcMesh(radius, arcAngle, fillProgress * arcAngle);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        // alpha pulse
        // returns base alpha until threshold, then oscillates between min/max with rising frequency
        private float ComputeAlpha(float fillProgress)
        {
            if (fillProgress < pulseStartFill)
                return baseColor.a;

            float t = (fillProgress - pulseStartFill) / (1f - pulseStartFill);
            float frequency = Mathf.Lerp(pulseBaseFrequency, pulseMaxFrequency, t);
            float pulse = Mathf.Sin(_elapsed * frequency * Mathf.PI * 2f) * 0.5f + 0.5f; // 0..1
            return Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse);
        }

        // call after Create() to make the indicator track a transform each frame
        // localOffsetXZ is the XZ offset in the boss's local horizontal space
        public void SetFollowTarget(Transform target, Vector3 localOffsetXZ)
        {
            _followTarget = target;
            _followLocalOffsetXZ = new Vector3(localOffsetXZ.x, 0f, localOffsetXZ.z);
        }

        // mesh builders

        // pie chart fill starting at local forward (Z+), rotating clockwise
        private void BuildDiscMesh(float discRadius, float fillAngleDeg)
        {
            int filled = Mathf.Clamp(
                Mathf.FloorToInt(fillAngleDeg / 360f * CIRCLE_SEGMENTS), 0, CIRCLE_SEGMENTS);

            if (filled == 0)
            {
                _mesh.Clear();
                return;
            }

            var verts = new Vector3[filled + 2];
            var tris = new int[filled * 3];

            verts[0] = Vector3.zero; // center

            for (int i = 0; i <= filled; i++)
            {
                float a = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Sin(a) * discRadius, 0f, Mathf.Cos(a) * discRadius);
            }

            for (int i = 0; i < filled; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
        }

        // fan expanding symmetrically from local forward (Z+), matching the centered sweep arc
        private void BuildArcMesh(float arcRadius, float totalAngleDeg, float filledAngleDeg)
        {
            // clamp filled half-angle to totalAngle half so we never exceed the intended sweep area
            float halfFilled = Mathf.Clamp(filledAngleDeg * 0.5f, 0f, totalAngleDeg * 0.5f);
            float halfTotal = totalAngleDeg * 0.5f;

            // how many outer verts per side
            int filled = Mathf.Clamp(
                Mathf.FloorToInt((halfFilled / halfTotal) * ARC_SEGMENTS_HALF), 0, ARC_SEGMENTS_HALF);

            if (filled == 0)
            {
                _mesh.Clear();
                return;
            }

            // total outer verts: filled on left + center + filled on right = filled*2+1
            var verts = new Vector3[filled * 2 + 2]; // +1 for center
            var tris = new int[filled * 2 * 3];

            verts[0] = Vector3.zero; // center

            for (int i = 0; i <= filled * 2; i++)
            {
                float t = (float)i / (float)(ARC_SEGMENTS_HALF * 2);
                float angleDeg = Mathf.Lerp(-halfTotal, halfTotal, t);
                float rad = angleDeg * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(rad) * arcRadius, 0f, Mathf.Cos(rad) * arcRadius);
            }

            for (int i = 0; i < filled * 2; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
        }

        // material helper
        private Material CreateMaterial()
        {
            // search for shaders
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[BossTelegraphIndicator] no suitable unlit shader found");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var mat = new Material(shader);

            // URP unlit transparent
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            SetMaterialColor(baseColor);
            return mat;
        }

		// sets the material color
        private void SetMaterialColor(Color c)
        {
            if (_material == null) return;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", c);
        }
    }
}
