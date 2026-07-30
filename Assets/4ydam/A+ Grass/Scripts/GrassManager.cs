    using UnityEngine;
using System.Collections.Generic;

public class GrassManager : MonoBehaviour
{
#region Variables
    public static GrassManager Instance { get; private set; }

    [Header("Interaction Map")]
    [SerializeField] private float interactionMapWorldSize = 32f;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float recenterThresholdPercent = 0.25f;

    [Header("Map Centering")]
    [SerializeField] private Transform mapCenterTarget;
    [SerializeField] private bool useMainCameraAsCenter = true;

    [Header("Global Wind Weather")]
    public float globalWind1Multiplier = 1f;
    public float globalWind2Multiplier = 1f;
    [Range(0f, 360f)] public float globalWindDirection = 45f;
    [SerializeField] private float windTransitionSpeed = 2f;

    [Header("Grass Recovery")]
    [SerializeField] private float recoverySpeed = 0.9f;
    [SerializeField] private float recoveryDelay = 0.05f;

    [Header("Culling")]
    [SerializeField] private bool enableRendererDistanceCulling = true;
    [SerializeField] private float rendererCullUpdateInterval = 0.2f;
    [SerializeField] private float rendererCacheRefreshInterval = 1.5f;
    [SerializeField] private float rendererCullPadding = 0.5f;
    [SerializeField] private bool showGizmos = true;
    
    private const int BaseMapResolution = 128;
    private const float MinMapWorldSize = 8f;
    private const int MaxAdaptiveResolution = 1024;
    private const float ReferenceMapWorldSize = 32f;

    private static readonly int InteractionMapID = Shader.PropertyToID("_AGrassInteractionMap");
    private static readonly int MapCenterID = Shader.PropertyToID("_AGrassMapCenter");
    private static readonly int MapSizeID = Shader.PropertyToID("_AGrassMapSize");
    private static readonly int MapTexelSizeID = Shader.PropertyToID("_AGrassMapTexelSize");
    private static readonly int GlobalWind1MultiplierID = Shader.PropertyToID("_AGrassGlobalWind1Multiplier");
    private static readonly int GlobalWind2MultiplierID = Shader.PropertyToID("_AGrassGlobalWind2Multiplier");
    private static readonly int GlobalWindDirectionID = Shader.PropertyToID("_AGrassGlobalWindDirection");
    private static readonly int UseDistanceFadeID = Shader.PropertyToID("_UseDistanceFade");
    private static readonly int DistanceFadeEndID = Shader.PropertyToID("_DistanceFadeEnd");
    private static readonly int CullAtFadeEndID = Shader.PropertyToID("_CullAtFadeEnd");

    private const string GrassShaderName = "4ydam/A+Grass";

    private readonly List<GrassInteractor> interactors = new List<GrassInteractor>();

    private struct InteractorMotionState
    {
        public Vector3 lastPosition;
        public Vector3 previousPosition;
        public float smoothedSpeed;
    }

    private struct OverflowCellState
    {
        public float strength;
        public float lastPushTime;
    }

    private readonly Dictionary<GrassInteractor, InteractorMotionState> interactorMotionStates = new Dictionary<GrassInteractor, InteractorMotionState>();
    private readonly Dictionary<long, OverflowCellState> overflowCells = new Dictionary<long, OverflowCellState>();
    private readonly List<long> overflowRemoveKeys = new List<long>();
    private readonly List<long> overflowUpdateKeys = new List<long>();
    private readonly List<OverflowCellState> overflowUpdateStates = new List<OverflowCellState>();
    private readonly List<Renderer> grassRenderers = new List<Renderer>();
    private readonly List<int> activeCells = new List<int>();

    private int mapResolution = BaseMapResolution;
    private float[] strengthGrid;
    private float[] lastPushTime;
    private float[] tempStrength;
    private float[] tempTime;
    private bool[] activeCellFlags;
    private Texture2D interactionMap;
    private Vector2 mapCenter;
    private bool mapInitialized;
    private bool gridHasActiveData;

    private float lastAppliedWind1Multiplier = float.NaN;
    private float lastAppliedWind2Multiplier = float.NaN;
    private Vector2 lastAppliedWindDirection = new Vector2(float.NaN, float.NaN);

    private float smoothedWind1;
    private float smoothedWind2;
    private float smoothedWindAngle;
    private float wind1Start, wind2Start, windAngleStart;
    private float wind1Target, wind2Target, windAngleTarget;
    private float windTransitionElapsed;
    private float windTransitionDuration;
    private bool windInitialized;

    private Vector2 windPhase1;
    private Vector2 windPhase2;
    private float lastAppliedMapWorldSize = float.NaN;
    private float nextRendererCullUpdateTime;
    private float nextRendererCacheRefreshTime;
    private float interactionCpuUsageMs;
    private float interactionCpuUsagePercent;
    private float cullingCpuUsageMs;
    private float cullingCpuUsagePercent;
    private int lastTrackedRendererCount;
    private int lastCulledRendererCount;
    private bool rendererDistanceCullingWasEnabled;

    public float GlobalWind1Multiplier => globalWind1Multiplier;
    public float GlobalWind2Multiplier => globalWind2Multiplier;
    public float GlobalWindDirection => globalWindDirection;
    public float InteractionCpuUsageMs => interactionCpuUsageMs;
    public float InteractionCpuUsagePercent => interactionCpuUsagePercent;
    public float CullingCpuUsageMs => cullingCpuUsageMs;
    public float CullingCpuUsagePercent => cullingCpuUsagePercent;
    public int TrackedGrassRendererCount => lastTrackedRendererCount;
    public int CulledGrassRendererCount => lastCulledRendererCount;
    public float InteractionMapWorldSize
    {
        get => interactionMapWorldSize;
        set => interactionMapWorldSize = Mathf.Max(MinMapWorldSize, value);
    }

    private float MapWorldSize => Mathf.Max(MinMapWorldSize, interactionMapWorldSize);
    private int GridSize => mapResolution * mapResolution;
    private float CellSize => MapWorldSize / mapResolution;
    private float RecenterThreshold => MapWorldSize * Mathf.Clamp(recenterThresholdPercent, 0.05f, 0.5f);
    private float TargetCellSize => ReferenceMapWorldSize / BaseMapResolution;

#endregion

#region Helpers
    private static float ExpSmoothing(float speed, float deltaTime)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * deltaTime);
    }
#endregion

#region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeMap();
        ApplyInteractionMapSettings(force: true, clearData: false);
        smoothedWind1 = globalWind1Multiplier;
        smoothedWind2 = globalWind2Multiplier;
        smoothedWindAngle = NormalizeWindAngle(globalWindDirection);
        wind1Start = smoothedWind1;
        wind2Start = smoothedWind2;
        windAngleStart = smoothedWindAngle;
        wind1Target = smoothedWind1;
        wind2Target = smoothedWind2;
        windAngleTarget = smoothedWindAngle;
        windTransitionElapsed = 0f;
        windTransitionDuration = 1f / Mathf.Max(0.01f, windTransitionSpeed);
        windInitialized = true;
        Vector2 initDir = GetWindDirectionFromAngle(smoothedWindAngle);
        float initTime = Mathf.Max(Time.time, 0.001f);
        windPhase1 = smoothedWind1 * initTime * initDir;
        windPhase2 = smoothedWind2 * initTime * initDir;
        ApplyGlobalWindSettings(force: true);
        RegisterExistingInteractors();
    }

    private void InitializeMap()
    {
        strengthGrid = new float[GridSize];
        lastPushTime = new float[GridSize];
        tempStrength = new float[GridSize];
        tempTime = new float[GridSize];
        activeCellFlags = new bool[GridSize];
        activeCells.Clear();

        if (interactionMap != null)
        {
            Destroy(interactionMap);
            interactionMap = null;
        }

        interactionMap = new Texture2D(mapResolution, mapResolution, TextureFormat.RFloat, false, true);
        interactionMap.filterMode = FilterMode.Bilinear;
        interactionMap.wrapMode = TextureWrapMode.Clamp;
        interactionMap.SetPixelData(strengthGrid, 0);
        interactionMap.Apply(false, false);

        Shader.SetGlobalTexture(InteractionMapID, interactionMap);
        Shader.SetGlobalVector(MapCenterID, Vector4.zero);
        Shader.SetGlobalFloat(MapTexelSizeID, 1f / mapResolution);
        Shader.SetGlobalFloat(MapSizeID, MapWorldSize);

        mapCenter = Vector2.zero;
        mapInitialized = false;
        gridHasActiveData = false;
        lastAppliedMapWorldSize = MapWorldSize;
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            ApplyGlobalWindSettings(force: true);
            RegisterExistingInteractors();
            nextRendererCullUpdateTime = 0f;
            nextRendererCacheRefreshTime = 0f;
            rendererDistanceCullingWasEnabled = enableRendererDistanceCulling;
        }
    }

    private void OnDisable()
    {
        SetAllGrassRenderersEnabled(true);
    }

    private void OnValidate()
    {
        interactionMapWorldSize = Mathf.Max(MinMapWorldSize, interactionMapWorldSize);
        recenterThresholdPercent = Mathf.Clamp(recenterThresholdPercent, 0.05f, 0.5f);
        globalWind1Multiplier = Mathf.Max(0f, globalWind1Multiplier);
        globalWind2Multiplier = Mathf.Max(0f, globalWind2Multiplier);
        globalWindDirection = NormalizeWindAngle(globalWindDirection);
        rendererCullUpdateInterval = Mathf.Max(0.05f, rendererCullUpdateInterval);
        rendererCacheRefreshInterval = Mathf.Max(0.1f, rendererCacheRefreshInterval);
        rendererCullPadding = Mathf.Max(0f, rendererCullPadding);
        ApplyInteractionMapSettings(force: true, clearData: Application.isPlaying);
        if (!Application.isPlaying)
            ApplyGlobalWindSettings(force: true);
    }
    
    private void Start()
    {
        RegisterExistingInteractors();
    }

    private void LateUpdate()
    {
        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (enableRendererDistanceCulling)
        {
            float cullingStartTime = Time.realtimeSinceStartup;
            UpdateRendererDistanceCulling();
            float cullingElapsedMs = (Time.realtimeSinceStartup - cullingStartTime) * 1000f;
            UpdateCullingCpuUsageStats(cullingElapsedMs, deltaTime);
        }
        else
        {
            UpdateRendererDistanceCulling();
        }

        float currentTime = Time.time;
        float interactionStartTime = Time.realtimeSinceStartup;

        ApplyInteractionMapSettings(force: false, clearData: true);

        if (Application.isPlaying)
        {
            if (!Mathf.Approximately(globalWind1Multiplier, wind1Target) ||
                !Mathf.Approximately(globalWind2Multiplier, wind2Target) ||
                Mathf.Abs(Mathf.DeltaAngle(globalWindDirection, windAngleTarget)) > 0.01f ||
                !Mathf.Approximately(windTransitionSpeed, 1f / windTransitionDuration))
            {
                ApplyGlobalWindSettings(force: false);
            }
        }

        SmoothWindTransition(deltaTime);

        bool hasRecoverableData = gridHasActiveData || overflowCells.Count > 0;
        bool hasInfluencingInteractors = HasAnyInfluencingInteractors();

        if (!hasRecoverableData && !hasInfluencingInteractors)
        {
            ResetInteractionUsageStats();
            return;
        }

        bool anyActive = false;
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        bool hasCenterAnchor = TryGetMapCenterAnchor(out Vector2 anchorCenterXZ);
        if (!mapInitialized && hasCenterAnchor)
        {
            mapCenter = anchorCenterXZ;
            mapInitialized = true;
        }

        for (int i = 0; i < interactors.Count; i++)
        {
            var interactor = interactors[i];
            if (interactor == null || !interactor.isActiveAndEnabled) continue;

            Vector3 pos = interactor.GetInteractionPosition();
            float radius = interactor.DetectionRadius;

            if (!interactorMotionStates.TryGetValue(interactor, out var motionState))
            {
                motionState = new InteractorMotionState { lastPosition = pos, previousPosition = pos };
            }
            else
            {
                float speed = 0f;
                if (deltaTime > 0.0001f)
                {
                    speed = Mathf.Min(Vector3.Distance(pos, motionState.lastPosition) / deltaTime, 100f);
                }
                float smoothAlpha = 1f - Mathf.Pow(Mathf.Clamp01(interactor.SpeedSmoothing), deltaTime * 60f);
                motionState.smoothedSpeed = Mathf.Lerp(motionState.smoothedSpeed, speed, smoothAlpha);
            }

            motionState.previousPosition = motionState.lastPosition;
            motionState.lastPosition = pos;
            interactorMotionStates[interactor] = motionState;

            bool shouldAffectMap = interactor.IsInfluencing || (!interactor.DisableWhenIdle && hasRecoverableData);
            if (!shouldAffectMap)
                continue;

            anyActive = true;

            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;

            if (interactor.IsInfluencing)
            {
                float speedBoost = 1f + motionState.smoothedSpeed * 0.4f;
                float rate = interactor.PushRate * speedBoost;
                float maxStr = interactor.MaxPushStrength;

                float moveDist = Vector3.Distance(motionState.previousPosition, pos);
                float stepSize = radius * 0.4f;
                int steps = Mathf.Max(1, Mathf.CeilToInt(moveDist / stepSize));
                float dtPerStep = deltaTime / steps;

                for (int s = 0; s < steps; s++)
                {
                    float t = (s + 1f) / steps;
                    Vector3 stampPos = Vector3.Lerp(motionState.previousPosition, pos, t);
                    StampInteractor(stampPos, radius, rate, maxStr, dtPerStep, currentTime);
                }
            }
            else if (!interactor.DisableWhenIdle)
            {
                SuppressRecovery(pos, radius, currentTime);
            }
        }

        if (!anyActive && !gridHasActiveData && overflowCells.Count == 0)
        {
            UpdateInteractionUsageStats((Time.realtimeSinceStartup - interactionStartTime) * 1000f, deltaTime);
            return;
        }

        Vector2 recenterXZ = mapCenter;
        if (hasCenterAnchor)
        {
            recenterXZ = anchorCenterXZ;
        }
        else if (anyActive)
        {
            recenterXZ = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        if (!mapInitialized && anyActive)
        {
            mapCenter = recenterXZ;
            mapInitialized = true;
        }

        if (mapInitialized)
        {
            if ((recenterXZ - mapCenter).sqrMagnitude > RecenterThreshold * RecenterThreshold)
            {
                RecenterMap(recenterXZ);
            }
        }

        RecoverGrid(deltaTime, currentTime);
        RecoverOverflowCells(deltaTime, currentTime);

        interactionMap.SetPixelData(strengthGrid, 0);
        interactionMap.Apply(false, false);

        Shader.SetGlobalTexture(InteractionMapID, interactionMap);
        Shader.SetGlobalVector(MapCenterID, new Vector4(mapCenter.x, mapCenter.y, 0, 0));

        float interactionElapsedMs = (Time.realtimeSinceStartup - interactionStartTime) * 1000f;
        UpdateInteractionUsageStats(interactionElapsedMs, deltaTime);
    }

    private bool HasAnyInfluencingInteractors()
    {
        for (int i = 0; i < interactors.Count; i++)
        {
            var interactor = interactors[i];
            if (interactor == null || !interactor.isActiveAndEnabled)
                continue;

            if (interactor.IsInfluencing)
                return true;
        }

        return false;
    }

#endregion

#region Interaction Map
    private void ApplyInteractionMapSettings(bool force, bool clearData)
    {
        float mapSize = MapWorldSize;
        int targetResolution = GetAdaptiveResolutionForMapSize(mapSize);
        bool mapSizeChanged = !Mathf.Approximately(lastAppliedMapWorldSize, mapSize);
        bool resolutionChanged = targetResolution != mapResolution || interactionMap == null;

        if (!force && !mapSizeChanged && !resolutionChanged)
            return;

        if (resolutionChanged)
        {
            mapResolution = targetResolution;
            InitializeMap();
            return;
        }

        Shader.SetGlobalFloat(MapSizeID, mapSize);
        Shader.SetGlobalFloat(MapTexelSizeID, 1f / mapResolution);
        lastAppliedMapWorldSize = mapSize;

        if (clearData && mapSizeChanged && strengthGrid != null && lastPushTime != null)
        {
            ResetGrass();
        }
    }

    private int GetAdaptiveResolutionForMapSize(float mapSize)
    {
        int desired = Mathf.CeilToInt(mapSize / TargetCellSize);
        desired = Mathf.Max(BaseMapResolution, desired);
        int pow2 = Mathf.NextPowerOfTwo(desired);
        return Mathf.Clamp(pow2, BaseMapResolution, MaxAdaptiveResolution);
    }

    private bool TryGetMapCenterAnchor(out Vector2 centerXZ)
    {
        if (mapCenterTarget != null)
        {
            Vector3 pos = mapCenterTarget.position;
            centerXZ = new Vector2(pos.x, pos.z);
            return true;
        }

        if (useMainCameraAsCenter)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 camPos = mainCamera.transform.position;
                centerXZ = new Vector2(camPos.x, camPos.z);
                return true;
            }
        }

        centerXZ = Vector2.zero;
        return false;
    }

    private void StampInteractor(Vector3 worldPos, float radius, float pushRate, float maxStrength, float dt, float time)
    {
        float rampAlpha = ExpSmoothing(pushRate, dt);
        float gridCenterX = (worldPos.x - mapCenter.x + MapWorldSize * 0.5f) / CellSize;
        float gridCenterZ = (worldPos.z - mapCenter.y + MapWorldSize * 0.5f) / CellSize;
        float cellHalf = CellSize * 0.5f;
        float effectiveRadius = Mathf.Max(0.001f, radius - cellHalf);
        float gridRadius = (effectiveRadius + CellSize) / CellSize;

        int minX = Mathf.Max(0, Mathf.FloorToInt(gridCenterX - gridRadius));
        int maxX = Mathf.Min(mapResolution - 1, Mathf.CeilToInt(gridCenterX + gridRadius));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(gridCenterZ - gridRadius));
        int maxZ = Mathf.Min(mapResolution - 1, Mathf.CeilToInt(gridCenterZ + gridRadius));

        float radiusSqr = effectiveRadius * effectiveRadius;
        float halfSize = MapWorldSize * 0.5f;

        for (int z = minZ; z <= maxZ; z++)
        {
            float worldCellZ = mapCenter.y - halfSize + (z + 0.5f) * CellSize;

            for (int x = minX; x <= maxX; x++)
            {
                float worldCellX = mapCenter.x - halfSize + (x + 0.5f) * CellSize;
            float closestX = Mathf.Clamp(worldPos.x, worldCellX - cellHalf, worldCellX + cellHalf);
            float closestZ = Mathf.Clamp(worldPos.z, worldCellZ - cellHalf, worldCellZ + cellHalf);
            float dx = closestX - worldPos.x;
            float dz = closestZ - worldPos.z;
            float distSqr = dx * dx + dz * dz;

                if (distSqr < radiusSqr)
                {
                    float t = 1f - Mathf.Sqrt(distSqr) / effectiveRadius;
                    float falloff = t * t * (3f - 2f * t);
                    float target = maxStrength * falloff;

                    int idx = z * mapResolution + x;
                    bool wasInactive = strengthGrid[idx] <= 0f;
                    if (target > strengthGrid[idx])
                    {
                        strengthGrid[idx] = Mathf.Lerp(strengthGrid[idx], target, rampAlpha);
                    }

                    if (wasInactive && strengthGrid[idx] > 0f)
                    {
                        MarkCellActive(idx);
                    }
                    lastPushTime[idx] = time;
                }
            }
        }
    }

    private void SuppressRecovery(Vector3 worldPos, float radius, float time)
    {
        float extRadius = radius + CellSize * 2f;
        float gridCenterX = (worldPos.x - mapCenter.x + MapWorldSize * 0.5f) / CellSize;
        float gridCenterZ = (worldPos.z - mapCenter.y + MapWorldSize * 0.5f) / CellSize;
        float cellHalf = CellSize * 0.5f;
        float effectiveExtRadius = Mathf.Max(0.001f, extRadius - cellHalf);
        float gridRadius = effectiveExtRadius / CellSize;

        int minX = Mathf.Max(0, Mathf.FloorToInt(gridCenterX - gridRadius));
        int maxX = Mathf.Min(mapResolution - 1, Mathf.CeilToInt(gridCenterX + gridRadius));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(gridCenterZ - gridRadius));
        int maxZ = Mathf.Min(mapResolution - 1, Mathf.CeilToInt(gridCenterZ + gridRadius));

        float extRadiusSqr = effectiveExtRadius * effectiveExtRadius;
        float halfSize = MapWorldSize * 0.5f;

        for (int z = minZ; z <= maxZ; z++)
        {
            float worldCellZ = mapCenter.y - halfSize + (z + 0.5f) * CellSize;

            for (int x = minX; x <= maxX; x++)
            {
                float worldCellX = mapCenter.x - halfSize + (x + 0.5f) * CellSize;
            float closestX = Mathf.Clamp(worldPos.x, worldCellX - cellHalf, worldCellX + cellHalf);
            float closestZ = Mathf.Clamp(worldPos.z, worldCellZ - cellHalf, worldCellZ + cellHalf);
            float dx = closestX - worldPos.x;
            float dz = closestZ - worldPos.z;
            float distSqr = dx * dx + dz * dz;

                if (distSqr < extRadiusSqr)
                {
                    int idx = z * mapResolution + x;
                    if (strengthGrid[idx] > 0f)
                        lastPushTime[idx] = time;
                }
            }
        }
    }

    private void RecoverGrid(float dt, float time)
    {
        float recoveryLerp = ExpSmoothing(recoverySpeed, dt);
        const float fadeInDuration = 0.5f;

        for (int i = activeCells.Count - 1; i >= 0; i--)
        {
            int idx = activeCells[i];
            float s = strengthGrid[idx];
            if (s <= 0f)
            {
                UnmarkCellActiveAt(i, idx);
                continue;
            }

            float elapsed = time - lastPushTime[idx] - recoveryDelay;
            if (elapsed > 0f)
            {
                float fadeIn = Mathf.Clamp01(elapsed / fadeInDuration);
                s = Mathf.Lerp(s, 0f, recoveryLerp * fadeIn);
                if (s < 0.001f) s = 0f;
                strengthGrid[idx] = s;
            }

            if (s <= 0f)
            {
                UnmarkCellActiveAt(i, idx);
            }
        }

        gridHasActiveData = activeCells.Count > 0;
    }

    private void RecenterMap(Vector2 newCenter)
    {
        Vector2 oldCenter = mapCenter;
        float halfSize = MapWorldSize * 0.5f;

        float newMinX = newCenter.x - halfSize;
        float newMaxX = newCenter.x + halfSize;
        float newMinZ = newCenter.y - halfSize;
        float newMaxZ = newCenter.y + halfSize;

        for (int z = 0; z < mapResolution; z++)
        {
            float worldCellZ = oldCenter.y - halfSize + (z + 0.5f) * CellSize;

            for (int x = 0; x < mapResolution; x++)
            {
                int srcIdx = z * mapResolution + x;
                float srcStrength = strengthGrid[srcIdx];
                if (srcStrength <= 0f)
                    continue;

                float worldCellX = oldCenter.x - halfSize + (x + 0.5f) * CellSize;
                if (worldCellX >= newMinX && worldCellX <= newMaxX && worldCellZ >= newMinZ && worldCellZ <= newMaxZ)
                    continue;

                long key = MakeCellKey(worldCellX, worldCellZ);
                if (overflowCells.TryGetValue(key, out var existing))
                {
                    existing.strength = Mathf.Max(existing.strength, srcStrength);
                    existing.lastPushTime = Mathf.Max(existing.lastPushTime, lastPushTime[srcIdx]);
                    overflowCells[key] = existing;
                }
                else
                {
                    overflowCells[key] = new OverflowCellState
                    {
                        strength = srcStrength,
                        lastPushTime = lastPushTime[srcIdx]
                    };
                }
            }
        }

        float offsetX = (newCenter.x - mapCenter.x) / CellSize;
        float offsetZ = (newCenter.y - mapCenter.y) / CellSize;

        mapCenter = newCenter;

        System.Array.Clear(tempStrength, 0, GridSize);
        System.Array.Clear(tempTime, 0, GridSize);

        for (int z = 0; z < mapResolution; z++)
        {
            for (int x = 0; x < mapResolution; x++)
            {
                int dstIdx = z * mapResolution + x;

                float srcX = x + offsetX;
                float srcZ = z + offsetZ;

                float sampledStrength = SampleGridBilinear(strengthGrid, srcX, srcZ);
                float sampledTime = SampleGridMax(lastPushTime, srcX, srcZ);

                if (sampledStrength <= 0f)
                {
                    float worldCellX = mapCenter.x - halfSize + (x + 0.5f) * CellSize;
                    float worldCellZ = mapCenter.y - halfSize + (z + 0.5f) * CellSize;
                    long key = MakeCellKey(worldCellX, worldCellZ);
                    if (overflowCells.TryGetValue(key, out var overflow))
                    {
                        sampledStrength = overflow.strength;
                        sampledTime = overflow.lastPushTime;
                        overflowCells.Remove(key);
                    }
                }

                tempStrength[dstIdx] = sampledStrength;
                tempTime[dstIdx] = sampledTime;
            }
        }

        var swapS = strengthGrid;
        strengthGrid = tempStrength;
        tempStrength = swapS;

        var swapT = lastPushTime;
        lastPushTime = tempTime;
        tempTime = swapT;

        RebuildActiveCellsFromGrid();
    }

    private void RecoverOverflowCells(float dt, float time)
    {
        if (overflowCells.Count == 0)
            return;

        float recoveryLerp = ExpSmoothing(recoverySpeed, dt);
        const float fadeInDuration = 0.5f;

        overflowRemoveKeys.Clear();
        overflowUpdateKeys.Clear();
        overflowUpdateStates.Clear();

        foreach (var pair in overflowCells)
        {
            var state = pair.Value;
            if (state.strength <= 0f)
            {
                overflowRemoveKeys.Add(pair.Key);
                continue;
            }

            float elapsed = time - state.lastPushTime - recoveryDelay;
            if (elapsed > 0f)
            {
                float fadeIn = Mathf.Clamp01(elapsed / fadeInDuration);
                state.strength = Mathf.Lerp(state.strength, 0f, recoveryLerp * fadeIn);
                if (state.strength < 0.001f)
                    state.strength = 0f;
            }

            if (state.strength <= 0f)
            {
                overflowRemoveKeys.Add(pair.Key);
            }
            else
            {
                overflowUpdateKeys.Add(pair.Key);
                overflowUpdateStates.Add(state);
            }
        }

        for (int i = 0; i < overflowRemoveKeys.Count; i++)
            overflowCells.Remove(overflowRemoveKeys[i]);

        for (int i = 0; i < overflowUpdateKeys.Count; i++)
            overflowCells[overflowUpdateKeys[i]] = overflowUpdateStates[i];
    }

    private long MakeCellKey(float worldX, float worldZ)
    {
        int gx = Mathf.FloorToInt(worldX / CellSize);
        int gz = Mathf.FloorToInt(worldZ / CellSize);
        return ((long)gx << 32) ^ (uint)gz;
    }

    private float SampleGridBilinear(float[] grid, float x, float z)
    {
        if (x < 0f || z < 0f || x > mapResolution - 1 || z > mapResolution - 1)
            return 0f;

        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        int x1 = Mathf.Min(x0 + 1, mapResolution - 1);
        int z1 = Mathf.Min(z0 + 1, mapResolution - 1);

        float tx = x - x0;
        float tz = z - z0;

        float s00 = grid[z0 * mapResolution + x0];
        float s10 = grid[z0 * mapResolution + x1];
        float s01 = grid[z1 * mapResolution + x0];
        float s11 = grid[z1 * mapResolution + x1];

        float sx0 = Mathf.Lerp(s00, s10, tx);
        float sx1 = Mathf.Lerp(s01, s11, tx);
        return Mathf.Lerp(sx0, sx1, tz);
    }

    private float SampleGridMax(float[] grid, float x, float z)
    {
        if (x < 0f || z < 0f || x > mapResolution - 1 || z > mapResolution - 1)
            return 0f;

        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        int x1 = Mathf.Min(x0 + 1, mapResolution - 1);
        int z1 = Mathf.Min(z0 + 1, mapResolution - 1);

        float t00 = grid[z0 * mapResolution + x0];
        float t10 = grid[z0 * mapResolution + x1];
        float t01 = grid[z1 * mapResolution + x0];
        float t11 = grid[z1 * mapResolution + x1];

        return Mathf.Max(Mathf.Max(t00, t10), Mathf.Max(t01, t11));
    }
#endregion

#region Wind
    private static float NormalizeWindAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }

    private static Vector2 GetWindDirectionFromAngle(float angle)
    {
        float normalizedAngle = NormalizeWindAngle(angle);
        float radians = normalizedAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private void ApplyGlobalWindSettings(bool force = false)
    {
        float clampedWind1 = Mathf.Max(0f, globalWind1Multiplier);
        float clampedWind2 = Mathf.Max(0f, globalWind2Multiplier);
        float normalizedAngle = NormalizeWindAngle(globalWindDirection);
        Vector2 normalizedDirection = GetWindDirectionFromAngle(normalizedAngle);
        globalWindDirection = normalizedAngle;

        if (force)
        {
            smoothedWind1 = clampedWind1;
            smoothedWind2 = clampedWind2;
            smoothedWindAngle = normalizedAngle;
            wind1Start = smoothedWind1;
            wind2Start = smoothedWind2;
            windAngleStart = smoothedWindAngle;
            wind1Target = clampedWind1;
            wind2Target = clampedWind2;
            windAngleTarget = normalizedAngle;
            windTransitionElapsed = 0f;
            windTransitionDuration = 1f / Mathf.Max(0.01f, windTransitionSpeed);
            windInitialized = true;
            Vector2 forceDir = GetWindDirectionFromAngle(normalizedAngle);
            float forceTime = Mathf.Max(Time.time, 0.001f);
            windPhase1 = clampedWind1 * forceTime * forceDir;
            windPhase2 = clampedWind2 * forceTime * forceDir;
        }
        else
        {
            float newDuration = windTransitionSpeed > 0.01f ? 1f / windTransitionSpeed : 1f;
            if (!Mathf.Approximately(windTransitionDuration, newDuration) && windTransitionElapsed < windTransitionDuration && windTransitionDuration > 0f)
            {
                float progress = Mathf.Clamp01(windTransitionElapsed / windTransitionDuration);
                windTransitionDuration = newDuration;
                windTransitionElapsed = progress * windTransitionDuration;
            }
            else
            {
                windTransitionDuration = newDuration;
            }
            if (!Mathf.Approximately(wind1Target, clampedWind1) || !Mathf.Approximately(wind2Target, clampedWind2) || Mathf.Abs(Mathf.DeltaAngle(windAngleTarget, normalizedAngle)) > 0.01f)
            {
                wind1Start = smoothedWind1;
                wind2Start = smoothedWind2;
                windAngleStart = smoothedWindAngle;
                wind1Target = clampedWind1;
                wind2Target = clampedWind2;
                windAngleTarget = normalizedAngle;
                windTransitionElapsed = 0f;
            }
            return;
        }

        Shader.SetGlobalFloat(GlobalWind1MultiplierID, clampedWind1);
        Shader.SetGlobalFloat(GlobalWind2MultiplierID, clampedWind2);
        Shader.SetGlobalVector(GlobalWindDirectionID, new Vector4(normalizedDirection.x, normalizedDirection.y, 0f, 0f));

        lastAppliedWind1Multiplier = clampedWind1;
        lastAppliedWind2Multiplier = clampedWind2;
        lastAppliedWindDirection = normalizedDirection;
    }

    private void SmoothWindTransition(float dt)
    {
        if (!windInitialized) return;

        if (windTransitionDuration <= 0f)
        {
            smoothedWind1 = wind1Target;
            smoothedWind2 = wind2Target;
            smoothedWindAngle = windAngleTarget;
        }
        else if (smoothedWind1 != wind1Target || smoothedWind2 != wind2Target || Mathf.Abs(Mathf.DeltaAngle(smoothedWindAngle, windAngleTarget)) > 0.01f)
        {
            windTransitionElapsed += dt;
            float t = Mathf.Clamp01(windTransitionElapsed / windTransitionDuration);
            t = t * t * (3f - 2f * t);
            smoothedWind1 = Mathf.Lerp(wind1Start, wind1Target, t);
            smoothedWind2 = Mathf.Lerp(wind2Start, wind2Target, t);
            float angleDelta = Mathf.DeltaAngle(windAngleStart, windAngleTarget);
            smoothedWindAngle = NormalizeWindAngle(windAngleStart + angleDelta * t);
        }

        Vector2 currentDir = GetWindDirectionFromAngle(smoothedWindAngle);
        windPhase1 += smoothedWind1 * dt * currentDir;
        windPhase2 += smoothedWind2 * dt * currentDir;

        float currentTime = Mathf.Max(Time.time, 0.001f);
        float phase1Mag = windPhase1.magnitude;
        float phase2Mag = windPhase2.magnitude;

        Vector2 effectiveDir;
        if (phase1Mag >= phase2Mag && phase1Mag > 0.0001f)
            effectiveDir = windPhase1 / phase1Mag;
        else if (phase2Mag > 0.0001f)
            effectiveDir = windPhase2 / phase2Mag;
        else
            effectiveDir = currentDir;

        float effectiveWind1 = phase1Mag / currentTime;
        float effectiveWind2 = phase2Mag / currentTime;

        bool hasChanged =
            !Mathf.Approximately(lastAppliedWind1Multiplier, effectiveWind1) ||
            !Mathf.Approximately(lastAppliedWind2Multiplier, effectiveWind2) ||
            (lastAppliedWindDirection - effectiveDir).sqrMagnitude > 0.000001f;

        if (!hasChanged) return;

        Shader.SetGlobalFloat(GlobalWind1MultiplierID, effectiveWind1);
        Shader.SetGlobalFloat(GlobalWind2MultiplierID, effectiveWind2);
        Shader.SetGlobalVector(GlobalWindDirectionID, new Vector4(effectiveDir.x, effectiveDir.y, 0f, 0f));

        lastAppliedWind1Multiplier = effectiveWind1;
        lastAppliedWind2Multiplier = effectiveWind2;
        lastAppliedWindDirection = effectiveDir;
    }

    public void SetWindMultipliers(float firstWindMultiplier, float secondWindMultiplier, bool immediate = false)
    {
        globalWind1Multiplier = Mathf.Max(0f, firstWindMultiplier);
        globalWind2Multiplier = Mathf.Max(0f, secondWindMultiplier);
        if (immediate) ApplyGlobalWindSettings(force: true);
    }

    public void SetWindDirection(float yRotationDegrees, bool immediate = false)
    {
        globalWindDirection = NormalizeWindAngle(yRotationDegrees);
        if (immediate) ApplyGlobalWindSettings(force: true);
    }

    public void SetGlobalWeather(float firstWindMultiplier, float secondWindMultiplier, float yRotationDegrees, bool immediate = false)
    {
        globalWind1Multiplier = Mathf.Max(0f, firstWindMultiplier);
        globalWind2Multiplier = Mathf.Max(0f, secondWindMultiplier);
        globalWindDirection = NormalizeWindAngle(yRotationDegrees);
        if (immediate) ApplyGlobalWindSettings(force: true);
    }

    public static void SetWeather(float firstWindMultiplier, float secondWindMultiplier, float yRotationDegrees, bool immediate = false)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.SetGlobalWeather(firstWindMultiplier, secondWindMultiplier, yRotationDegrees, immediate);
    }
#endregion

#region Interactors
    public void RegisterInteractor(GrassInteractor interactor)
    {
        if (interactor != null && !interactors.Contains(interactor))
        {
            interactors.Add(interactor);
        }
    }

    public void UnregisterInteractor(GrassInteractor interactor)
    {
        interactors.Remove(interactor);
        interactorMotionStates.Remove(interactor);
    }

    public void ResetGrass()
    {
        interactorMotionStates.Clear();
        overflowCells.Clear();
        System.Array.Clear(strengthGrid, 0, GridSize);
        System.Array.Clear(lastPushTime, 0, GridSize);
        if (activeCellFlags != null)
            System.Array.Clear(activeCellFlags, 0, activeCellFlags.Length);
        activeCells.Clear();
        mapInitialized = false;
        gridHasActiveData = false;

        if (interactionMap != null)
        {
            interactionMap.SetPixelData(strengthGrid, 0);
            interactionMap.Apply(false, false);
        }
    }
#endregion

#region Culling And Usage
    private void UpdateRendererDistanceCulling()
    {
        if (!Application.isPlaying)
            return;

        if (!enableRendererDistanceCulling)
        {
            if (rendererDistanceCullingWasEnabled)
            {
                SetAllGrassRenderersEnabled(true);
                ResetCullingCpuUsageStats();
            }

            rendererDistanceCullingWasEnabled = false;
            lastTrackedRendererCount = grassRenderers.Count;
            lastCulledRendererCount = 0;
            return;
        }

        rendererDistanceCullingWasEnabled = true;

        if (Time.time < nextRendererCullUpdateTime)
            return;

        nextRendererCullUpdateTime = Time.time + rendererCullUpdateInterval;

        if (grassRenderers.Count == 0 || Time.time >= nextRendererCacheRefreshTime)
        {
            RefreshGrassRendererCache();
            nextRendererCacheRefreshTime = Time.time + rendererCacheRefreshInterval;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            SetAllGrassRenderersEnabled(true);
            return;
        }

        Vector3 camPos = mainCamera.transform.position;
        int culledCount = 0;

        for (int i = grassRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = grassRenderers[i];
            if (renderer == null)
            {
                grassRenderers.RemoveAt(i);
                continue;
            }

            bool shouldRender = ShouldRenderGrassRenderer(renderer, camPos, rendererCullPadding);
            if (renderer.enabled != shouldRender)
            {
                renderer.enabled = shouldRender;
            }

            if (!shouldRender)
            {
                culledCount++;
            }
        }

        lastTrackedRendererCount = grassRenderers.Count;
        lastCulledRendererCount = culledCount;
    }

    private void UpdateCullingCpuUsageStats(float elapsedMs, float deltaTime)
    {
        const float targetFrameMs = 16.6667f;
        float smoothing = 1f - Mathf.Exp(-6f * deltaTime);

        cullingCpuUsageMs = Mathf.Lerp(cullingCpuUsageMs, Mathf.Max(0f, elapsedMs), smoothing);
        float usagePercent = (cullingCpuUsageMs / targetFrameMs) * 100f;
        cullingCpuUsagePercent = Mathf.Lerp(cullingCpuUsagePercent, usagePercent, smoothing);
    }

    private void UpdateInteractionUsageStats(float elapsedMs, float deltaTime)
    {
        const float targetFrameMs = 16.6667f;
        interactionCpuUsageMs = Mathf.Max(0f, elapsedMs);
        interactionCpuUsagePercent = (interactionCpuUsageMs / targetFrameMs) * 100f;
    }

    private void ResetInteractionUsageStats()
    {
        interactionCpuUsageMs = 0f;
        interactionCpuUsagePercent = 0f;
    }

    private void MarkCellActive(int idx)
    {
        if (activeCellFlags[idx])
            return;

        activeCellFlags[idx] = true;
        activeCells.Add(idx);
        gridHasActiveData = true;
    }

    private void UnmarkCellActiveAt(int listIndex, int idx)
    {
        if (activeCellFlags[idx])
            activeCellFlags[idx] = false;

        int last = activeCells.Count - 1;
        if (listIndex != last)
            activeCells[listIndex] = activeCells[last];

        activeCells.RemoveAt(last);
    }

    private void RebuildActiveCellsFromGrid()
    {
        if (activeCellFlags == null || activeCellFlags.Length != GridSize)
            activeCellFlags = new bool[GridSize];
        else
            System.Array.Clear(activeCellFlags, 0, activeCellFlags.Length);

        activeCells.Clear();
        for (int i = 0; i < GridSize; i++)
        {
            if (strengthGrid[i] > 0f)
            {
                activeCellFlags[i] = true;
                activeCells.Add(i);
            }
        }

        gridHasActiveData = activeCells.Count > 0;
    }

    private void ResetCullingCpuUsageStats()
    {
        cullingCpuUsageMs = 0f;
        cullingCpuUsagePercent = 0f;
    }

    private void RefreshGrassRendererCache()
    {
        grassRenderers.Clear();
        Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneRenderers.Length; i++)
        {
            Renderer renderer = sceneRenderers[i];
            if (renderer == null)
                continue;

            if (HasGrassMaterial(renderer))
            {
                grassRenderers.Add(renderer);
            }
        }
    }

    private static bool HasGrassMaterial(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null)
                continue;

            if (material.shader.name == GrassShaderName)
                return true;
        }

        return false;
    }

    private static bool ShouldRenderGrassRenderer(Renderer renderer, Vector3 cameraPosition, float cullPadding)
    {
        Material[] materials = renderer.sharedMaterials;
        bool hasGrassMaterial = false;
        bool allGrassMaterialsAllowDistanceCull = true;
        float maxFadeEnd = 0f;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null || material.shader.name != GrassShaderName)
                continue;

            hasGrassMaterial = true;

            bool usesDistanceFade = material.HasProperty(UseDistanceFadeID) && material.GetFloat(UseDistanceFadeID) > 0.5f;
            bool cullAtFadeEnd = material.HasProperty(CullAtFadeEndID) && material.GetFloat(CullAtFadeEndID) > 0.5f;

            if (!usesDistanceFade || !cullAtFadeEnd || !material.HasProperty(DistanceFadeEndID))
            {
                allGrassMaterialsAllowDistanceCull = false;
                break;
            }

            maxFadeEnd = Mathf.Max(maxFadeEnd, material.GetFloat(DistanceFadeEndID));
        }

        if (!hasGrassMaterial || !allGrassMaterialsAllowDistanceCull)
            return true;

        float cullDistance = Mathf.Max(0f, maxFadeEnd + cullPadding);
        float cullDistanceSqr = cullDistance * cullDistance;
        float distanceToRendererSqr = (renderer.bounds.center - cameraPosition).sqrMagnitude;
        return distanceToRendererSqr <= cullDistanceSqr;
    }

    private void SetAllGrassRenderersEnabled(bool isEnabled)
    {
        if (grassRenderers.Count == 0)
            return;

        for (int i = grassRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = grassRenderers[i];
            if (renderer == null)
            {
                grassRenderers.RemoveAt(i);
                continue;
            }

            if (renderer.enabled != isEnabled)
            {
                renderer.enabled = isEnabled;
            }
        }
    }
#endregion

#region Cleanup And Gizmos
    private void OnDestroy()
    {
        SetAllGrassRenderersEnabled(true);

        if (interactionMap != null)
        {
            Shader.SetGlobalTexture(InteractionMapID, Texture2D.blackTexture);
            Destroy(interactionMap);
            interactionMap = null;
        }
        if (Instance == this)
            Instance = null;
    }

    private void RegisterExistingInteractors()
    {
        var sceneInteractors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneInteractors.Length; i++)
        {
            RegisterInteractor(sceneInteractors[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        float angle = windAngleTarget * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(-Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
        float wind1Strength = wind1Target;
        float wind2Strength = wind2Target;

        float headAngle = 25f;

        float arrow1Length = Mathf.Max(0.3f, wind1Strength);
        float head1Length = Mathf.Clamp(arrow1Length * 0.3f, 0.15f, 0.6f);
        Vector3 tip1 = origin + dir * arrow1Length;
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(origin, tip1);
        Vector3 right1 = Quaternion.Euler(0, headAngle, 0) * -dir;
        Vector3 left1 = Quaternion.Euler(0, -headAngle, 0) * -dir;
        Gizmos.DrawLine(tip1, tip1 + right1 * head1Length);
        Gizmos.DrawLine(tip1, tip1 + left1 * head1Length);

        Vector3 origin2 = origin + Vector3.up * 0.3f;
        float arrow2Length = Mathf.Max(0.3f, wind2Strength);
        float head2Length = Mathf.Clamp(arrow2Length * 0.3f, 0.15f, 0.6f);
        Vector3 tip2 = origin2 + dir * arrow2Length;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawLine(origin2, tip2);
        Vector3 right2 = Quaternion.Euler(0, headAngle, 0) * -dir;
        Vector3 left2 = Quaternion.Euler(0, -headAngle, 0) * -dir;
        Gizmos.DrawLine(tip2, tip2 + right2 * head2Length);
        Gizmos.DrawLine(tip2, tip2 + left2 * head2Length);
    }
#endregion
}
