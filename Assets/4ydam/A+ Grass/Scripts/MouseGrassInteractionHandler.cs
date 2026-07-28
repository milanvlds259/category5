using UnityEngine;
using UnityEngine.InputSystem;

public class MouseGrassInteractionHandler : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject mouseInteractor;
    [SerializeField] private LayerMask interactableLayerMask;
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private bool showGizmos = true;

    private Camera mainCamera;
    private Transform mouseInteractorTransform;

    private readonly RaycastHit[] raycastHits = new RaycastHit[1];

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mouseInteractor != null)
            mouseInteractorTransform = mouseInteractor.transform;
    }

    private void OnValidate()
    {
        rayDistance = Mathf.Max(0.01f, rayDistance);
    }

    private void Update()
    {
        if (mainCamera == null || Mouse.current == null)
            return;

        if (mouseInteractorTransform == null)
        {
            if (mouseInteractor == null)
                return;

            mouseInteractorTransform = mouseInteractor.transform;
            if (mouseInteractorTransform == null)
                return;
        }

        if (!mainCamera.isActiveAndEnabled)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        bool isInView = mousePosition.x >= 0f && mousePosition.x <= Screen.width &&
                        mousePosition.y >= 0f && mousePosition.y <= Screen.height;

        if (!isInView)
        {
            if (mouseInteractor.activeSelf)
                mouseInteractor.SetActive(false);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastHits,
            rayDistance,
            interactableLayerMask,
            triggerInteraction);

        if (hitCount > 0)
        {
            if (!mouseInteractor.activeSelf)
                mouseInteractor.SetActive(true);
            mouseInteractorTransform.position = raycastHits[0].point;
        }
        else
        {
            if (mouseInteractor.activeSelf)
                mouseInteractor.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Camera cam = mainCamera;
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawRay(origin, direction * rayDistance);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
        Gizmos.DrawSphere(origin + direction * rayDistance, 0.2f);
    }
}
