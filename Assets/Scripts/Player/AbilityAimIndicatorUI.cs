using UnityEngine;

namespace Category5.Player
{
    // world-space aim indicator that renders only for the local caster
    // attaches to the player prefab so the indicator follows the player
    //
    // - for Fire/Ice/R: a line from the projectile spawn point to the aim target
    // - for Thunder: a ground ring at the player's feet (radius = arcRadius)
    // auto-hides on release/cancel
    //
    // caster-only rendering: the owner filter in each event handler is the primary guard
    // (remote clients' AbilityAimIndicatorUI instances ignore local players' aim events because
    // their manager.IsOwner is false). the LineRenderer renderingLayerMask is a secondary
    // defense so remote cameras can also exclude the indicator at the URP level if desired.
    [DisallowMultipleComponent]
    public class AbilityAimIndicatorUI : MonoBehaviour
    {
        // dedicated rendering layer bit for caster-only world-space UI
        // bit 8 (value 0x100) — pick any unused bit; must match the camera config
        public const uint LocalCasterLayerMask = 1u << 8;

        [Header("Line Indicator")]
        [SerializeField] private LineRenderer aimLine;
        [Tooltip("distance from the spawn point that the line extends to when there's no hit")]
        [SerializeField] private float defaultLineLength = 30f;

        [Header("Ground Ring Indicator")]
        [SerializeField] private LineRenderer groundRing;
        [SerializeField] private int groundRingSegments = 48;
        [SerializeField] private float groundRingHeightOffset = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color fireColor = new Color(1f, 0.5f, 0.1f, 0.8f);
        [SerializeField] private Color iceColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        [SerializeField] private Color thunderColor = new Color(0.9f, 0.85f, 0.2f, 0.8f);
        [SerializeField] private Color blackHoleColor = new Color(0.6f, 0.2f, 0.9f, 0.8f);

        private PlayerAbilityManager _ownerManager;
        private Category5.AbilitySlot _trackedSlot;
        private bool _isVisible;

        private void Awake()
        {
            // force world space so SetPosition coordinates are treated as world positions
            // (otherwise the line draws relative to the player transform and appears "stuck" in the scene)
            if (aimLine != null)
            {
                aimLine.useWorldSpace = true;
                aimLine.renderingLayerMask = LocalCasterLayerMask;
            }
            if (groundRing != null)
            {
                groundRing.useWorldSpace = true;
                groundRing.renderingLayerMask = LocalCasterLayerMask;
            }

            // hide both indicators by default
            SetLineVisible(false);
            SetRingVisible(false);
        }

        private void OnEnable()
        {
            Category5.PlayerAbilityManager.OnAbilityAimStarted += OnAimStarted;
            Category5.PlayerAbilityManager.OnAbilityAimProgress += OnAimProgress;
            Category5.PlayerAbilityManager.OnAbilityAimReleased += OnAimReleased;
            Category5.PlayerAbilityManager.OnAbilityAimCanceled += OnAimCanceled;
        }

        private void OnDisable()
        {
            Category5.PlayerAbilityManager.OnAbilityAimStarted -= OnAimStarted;
            Category5.PlayerAbilityManager.OnAbilityAimProgress -= OnAimProgress;
            Category5.PlayerAbilityManager.OnAbilityAimReleased -= OnAimReleased;
            Category5.PlayerAbilityManager.OnAbilityAimCanceled -= OnAimCanceled;
        }

        private void OnAimStarted(Category5.PlayerAbilityManager manager, Category5.AbilitySlot slot)
        {
            // only render for the local caster
            if (!manager.IsOwner) return;

            _ownerManager = manager;
            _trackedSlot = slot;
            _isVisible = true;

            // E slot: pick shape + color from the current element right away (avoids a 1-frame fire-colored flash)
            if (slot == Category5.AbilitySlot.Ability2)
            {
                Category5.ElementMode element = GetCurrentElement(manager);
                if (element == Category5.ElementMode.Thunder)
                {
                    SetLineVisible(false);
                    SetRingVisible(true);
                    ApplyColorForElement(Category5.ElementMode.Thunder);
                }
                else
                {
                    SetRingVisible(false);
                    SetLineVisible(true);
                    ApplyColorForElement(element);
                }
                return;
            }

            // R slot: direction line in black hole color
            SetRingVisible(false);
            SetLineVisible(true);
            ApplyColorForSlot(slot);
        }

        private void OnAimProgress(Category5.PlayerAbilityManager manager, Category5.AbilitySlot slot, Vector3 spawnPos, Vector3 direction)
        {
            if (!_isVisible || manager != _ownerManager) return;

            // E slot: shape + color depend on the current element
            if (slot == Category5.AbilitySlot.Ability2)
            {
                Category5.ElementMode element = GetCurrentElement(manager);

                if (element == Category5.ElementMode.Thunder)
                {
                    // thunder: show ground ring instead of a line
                    SetLineVisible(false);
                    DrawGroundRing(spawnPos, manager);
                    ApplyColorForElement(Category5.ElementMode.Thunder);
                    return;
                }

                // fire/ice: direction line tinted by the current element
                SetRingVisible(false);
                SetLineVisible(true);
                ApplyColorForElement(element);
                DrawAimLine(spawnPos, direction);
                return;
            }

            // R slot: direction line in black hole color
            SetRingVisible(false);
            SetLineVisible(true);
            ApplyColorForSlot(slot);
            DrawAimLine(spawnPos, direction);
        }

        // reads the local elementalist's current element from the Q ability
        private Category5.ElementMode GetCurrentElement(Category5.PlayerAbilityManager manager)
        {
            var q = manager != null ? manager.GetComponentInChildren<Category5.ElementalistQ>() : null;
            return q != null ? q.CurrentElement : Category5.ElementMode.Fire;
        }

        private void OnAimReleased(Category5.PlayerAbilityManager manager, Category5.AbilitySlot slot, Vector3 spawnPos, Vector3 direction)
        {
            if (manager != _ownerManager) return;
            Hide();
        }

        private void OnAimCanceled(Category5.PlayerAbilityManager manager, Category5.AbilitySlot slot)
        {
            if (manager != _ownerManager) return;
            Hide();
        }

        private void Hide()
        {
            _isVisible = false;
            _ownerManager = null;
            SetLineVisible(false);
            SetRingVisible(false);
        }

        private void DrawAimLine(Vector3 spawnPos, Vector3 direction)
        {
            if (aimLine == null) return;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, spawnPos);
            aimLine.SetPosition(1, spawnPos + direction.normalized * defaultLineLength);
        }

        private void DrawGroundRing(Vector3 center, Category5.PlayerAbilityManager manager)
        {
            if (groundRing == null) return;

            // read the radius from the thunder ability so the ring matches the actual damage radius
            float radius = 5f;
            if (manager != null)
            {
                var thunder = manager.GetComponentInChildren<Category5.ElementalistE_Thunder>();
                if (thunder != null) radius = thunder.ArcRadius;
            }

            SetRingVisible(true);
            groundRing.positionCount = groundRingSegments + 1;
            Vector3 ringCenter = center;
            ringCenter.y = groundRingHeightOffset;
            for (int i = 0; i <= groundRingSegments; i++)
            {
                float angle = (float)i / groundRingSegments * 360f;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
                groundRing.SetPosition(i, ringCenter + offset);
            }
        }

        private void SetLineVisible(bool visible)
        {
            if (aimLine != null && aimLine.gameObject.activeSelf != visible)
            {
                aimLine.gameObject.SetActive(visible);
            }
        }

        private void SetRingVisible(bool visible)
        {
            if (groundRing != null && groundRing.gameObject.activeSelf != visible)
            {
                groundRing.gameObject.SetActive(visible);
            }
        }

        private void ApplyColorForSlot(Category5.AbilitySlot slot)
        {
            Color color = slot switch
            {
                Category5.AbilitySlot.Ability2 => fireColor,
                Category5.AbilitySlot.Ability3 => blackHoleColor,
                _ => Color.white
            };

            if (aimLine != null)
            {
                aimLine.startColor = color;
                aimLine.endColor = color;
            }
        }

        private void ApplyColorForElement(Category5.ElementMode element)
        {
            Color color = element switch
            {
                Category5.ElementMode.Fire => fireColor,
                Category5.ElementMode.Ice => iceColor,
                Category5.ElementMode.Thunder => thunderColor,
                _ => Color.white
            };

            // apply to BOTH indicators so whichever is visible gets the element color
            // (fire/ice show the line, thunder shows the ring)
            if (aimLine != null)
            {
                aimLine.startColor = color;
                aimLine.endColor = color;
            }
            if (groundRing != null)
            {
                groundRing.startColor = color;
                groundRing.endColor = color;
            }
        }
    }
}
