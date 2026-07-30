using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private bool disableWhenIdle = true;
    [SerializeField] private float pushRate = 6f;
    [SerializeField] private float maxPushStrength = 2f;

    [Header("Motion Settings")]
    [SerializeField] private float movementThreshold = 0.5f;
    [SerializeField] private float speedSmoothing = 0.3f;
    [SerializeField] private float detectionRadius = 1f;

    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool showGizmos = true;

    private Vector3 lastPosition;
    private float currentSpeed;
    private float smoothedSpeed;
    private bool wasMoving;

    public float PushRate => pushRate;
    public float MaxPushStrength => maxPushStrength;
    public float SpeedSmoothing => speedSmoothing;
    public float DetectionRadius => detectionRadius;
    public bool DisableWhenIdle => disableWhenIdle;
    public bool IsInfluencing => IsMoving;

    public bool IsMoving
    {
        get
        {
            if (wasMoving)
                return smoothedSpeed > movementThreshold * 0.5f;
            else
                return smoothedSpeed > movementThreshold;
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        currentSpeed = 0f;
        smoothedSpeed = 0f;
        wasMoving = false;

        if (GrassManager.Instance != null)
            GrassManager.Instance.RegisterInteractor(this);
    }

    private void Start()
    {
        if (GrassManager.Instance == null)
            Debug.LogWarning("[GrassInteractor] No GrassManager found in scene! Add one to enable grass interaction.");
    }

    private void Update()
    {
        Vector3 currentPosition = transform.position;
        float distanceSqr = (currentPosition - lastPosition).sqrMagnitude;

        if (Time.deltaTime > 0.0001f)
            currentSpeed = Mathf.Min(Mathf.Sqrt(distanceSqr) / Time.deltaTime, 100f);

        float smoothAlpha = 1f - Mathf.Pow(Mathf.Clamp01(speedSmoothing), Time.deltaTime * 60f);
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, currentSpeed, smoothAlpha);
        wasMoving = IsMoving;
        lastPosition = currentPosition;
    }

    private void OnDisable()
    {
        if (GrassManager.Instance != null)
            GrassManager.Instance.UnregisterInteractor(this);
    }

    public Vector3 GetInteractionPosition()
    {
        return transform.position + positionOffset;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector3 center = transform.position + positionOffset;

        Gizmos.color = IsMoving ? new Color(1f, 1f, 0f, 0.15f) : new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(center, detectionRadius);

        float visibleRadius = detectionRadius * 0.7f;
        Gizmos.color = IsMoving ? new Color(1f, 1f, 0f, 0.35f) : new Color(0f, 1f, 0f, 0.35f);
        Gizmos.DrawSphere(center, visibleRadius);
    }
}
