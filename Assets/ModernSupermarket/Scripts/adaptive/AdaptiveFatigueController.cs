using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Aplica las 3 reglas adaptativas de la tesis:
/// 1) PupilInstability  -> tono cálido
/// 2) HighBlinkRate     -> bajar brillo (-15%)
/// 3) LongFixation      -> subir contraste en productos/labels (+10%)
/// </summary>
public class AdaptiveFatigueController : MonoBehaviour
{
    [Header("Modo experimental")]
    [Tooltip("True = condición adaptativa..")]
    public bool adaptiveEnabled = true;
    public bool logAdaptations = true;
    public bool logSetupWarnings = true;
    [Tooltip("when a change starts and when it finishes")]
    public bool logVisualChanges = true;

    [Header("Referencias")]
    public OculometricFatigueMonitor fatigueMonitor;
    public Light[] sceneLights;
    public Light[] neonLights;
    [Tooltip("luces.")]
    public Material[] roofEmissiveMaterials;
    [Tooltip("productos o labels")]
    public Material[] targetEmissiveMaterials;
    public ReflectionProbe[] reflectionProbes;

    [Header("Pasos de la tesis")]
    [Range(0.5f, 1f)]
    public float brightnessStepMultiplier = 0.85f;
    [Range(1f, 1.5f)]
    public float targetContrastStepMultiplier = 1.10f;
    [Range(0f, 1f)]
    public float warmthStepAmount = 0.12f;

    [Header("Limites de seguridad")]
    [Range(0.2f, 1f)] public float minBrightnessFactor = 0.30f;
    [Range(1f, 1.5f)] public float maxTargetContrastFactor = 1.30f;
    [Range(0f, 1f)] public float maxWarmthFactor = 0.65f;
    [Range(0.4f, 1f)] public float minReflectionFactor = 0.50f;

    // ===== INICIO MODO 2 MINUTOS () =====
    [Header("Modo prueba: cambios a los 2 min sin fatiga")]
    [Tooltip("True = a los 120s aplica calor, brillo y contraste sin eye tracking. False = solo modo fatiga.")]
    public bool enableTwoMinuteForcedAdaptation = false;
    public float forcedAdaptationStartSeconds = 120f;
    // ===== FIN MODO 2 MINUTOS =====

    [Header("Timing")]
    public float transitionSeconds = 2.5f;
    public float minSecondsBetweenSteps = 2f;
    public int maxAdaptationStepsPerSession = 4;

    [Header("Estado en runtime (solo lectura)")]
    public float brightnessFactor = 1f;
    public float targetContrastFactor = 1f;
    public float warmthFactor = 0f;
    public float reflectionFactor = 1f;

    float targetBrightnessFactor = 1f;
    float targetTargetContrastFactor = 1f;
    float targetWarmthFactor = 0f;
    float targetReflectionFactor = 1f;

    float lastStepTime = -999f;
    int adaptationStepsApplied;
    bool loggedCalibrationReady;
    bool loggedTwoMinuteModeStarted;
    bool wasBlendingVisuals;
    int forcedTwoMinuteStepIndex;
    string lastAppliedTrigger;

    float baselineAmbientIntensity;
    float baselineReflectionIntensity;
    readonly List<LightBaseline> lightBaselines = new List<LightBaseline>();
    readonly List<MaterialBaseline> roofMaterialBaselines = new List<MaterialBaseline>();
    readonly List<MaterialBaseline> targetMaterialBaselines = new List<MaterialBaseline>();
    readonly List<ProbeBaseline> probeBaselines = new List<ProbeBaseline>();

    StreamWriter adaptationWriter;

    struct LightBaseline
    {
        public Light light;
        public float intensity;
        public Color color;
    }

    struct MaterialBaseline
    {
        public Material material;
        public Color emissionColor;
        public bool emissionEnabled;
    }

    struct ProbeBaseline
    {
        public ReflectionProbe probe;
        public float intensity;
    }

    void Awake()
    {
        CaptureBaselines();
    }

    void Start()
    {
        if (logAdaptations)
        {
            string folder = Path.Combine(Application.persistentDataPath, "EyeTrackingData");
            Directory.CreateDirectory(folder);

            string fileName = "adaptation_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            adaptationWriter = new StreamWriter(Path.Combine(folder, fileName), false);
            adaptationWriter.WriteLine(
                "systemTimestamp,timeSinceStart,trigger,brightnessFactor,targetContrastFactor,warmthFactor,reflectionFactor,vfi"
            );
            adaptationWriter.Flush();
            Debug.Log("[AdaptiveFatigue] adaptation_log en: " + Path.Combine(folder, fileName));
        }

        if (logSetupWarnings)
        {
            ValidateSetup();
        }

        ApplyImmediateTargets();
    }

    void ValidateSetup()
    {
        if (fatigueMonitor == null)
        {
            Debug.LogWarning("[AdaptiveFatigue] fatigueMonitor NO asignado. Arrastra OculometricFatigueMonitor al campo.");
        }

        if (!adaptiveEnabled)
        {
            Debug.Log("[AdaptiveFatigue] adaptiveEnabled=false (condición estática).");
        }

        if (enableTwoMinuteForcedAdaptation)
        {
            Debug.Log(
                "[AdaptiveFatigue] Modo 2 min ACTIVADO en Inspector. " +
                "Cambios forzados a los " + forcedAdaptationStartSeconds.ToString("F0") + "s (sin fatiga)."
            );
        }

        if (sceneLights == null || sceneLights.Length == 0)
        {
            Debug.LogWarning("[AdaptiveFatigue]El brillo global cambiará poco.");
        }

        if (targetEmissiveMaterials == null || targetEmissiveMaterials.Length == 0)
        {
            Debug.LogWarning("[AdaptiveFatigue] targetEmissiveMaterials contraste");
        }
    }

    void Update()
    {
        SmoothTowardsTargets();

        if (!adaptiveEnabled)
        {
            return;
        }

        if (Time.unscaledTime - lastStepTime < minSecondsBetweenSteps)
        {
            return;
        }

        // ===== INICIO MODO 2 MINUTOS () =====
        if (enableTwoMinuteForcedAdaptation)
        {
            if (Time.time < forcedAdaptationStartSeconds)
            {
                return;
            }

            if (!loggedTwoMinuteModeStarted)
            {
                loggedTwoMinuteModeStarted = true;
                if (logVisualChanges)
                {
                    Debug.Log(
                        "[AdaptiveFatigue] *** MODO 2 MINUTOS ACTIVO *** " +
                        "A los " + forcedAdaptationStartSeconds.ToString("F0") +
                        "s se aplican cambios SIN medir fatiga (calor, brillo, contraste)."
                    );
                }
            }

            ApplyForcedTwoMinuteStep();
            return;
        }
        // ===== FIN MODO 2 MINUTOS =====

        // ===== INICIO MODO FATIGA (comportamiento normal de la tesis) =====
        if (fatigueMonitor == null)
        {
            return;
        }

        if (!fatigueMonitor.IsCalibrated)
        {
            return;
        }

        if (!loggedCalibrationReady)
        {
            loggedCalibrationReady = true;
            if (logVisualChanges)
            {
                Debug.Log("[AdaptiveFatigue] Calibración lista (30s). Cambios solo si hay fatiga detectada.");
            }
        }

        if (adaptationStepsApplied >= maxAdaptationStepsPerSession)
        {
            return;
        }

        if (fatigueMonitor.PupilInstability)
        {
            ApplyWarmthStep("pupil_instability");
        }
        else if (fatigueMonitor.HighBlinkRate)
        {
            ApplyBrightnessStep("high_blink_rate");
        }
        else if (fatigueMonitor.LongFixation)
        {
            ApplyTargetContrastStep("long_fixation");
        }
        else
        {
            MaybeRecoverStep();
        }
        // ===== FIN MODO FATIGA =====
    }

    void ApplyForcedTwoMinuteStep()
    {
        if (forcedTwoMinuteStepIndex >= 3)
        {
            return;
        }

        switch (forcedTwoMinuteStepIndex)
        {
            case 0:
                ApplyWarmthStep("forced_2min_warmth");
                break;
            case 1:
                ApplyBrightnessStep("forced_2min_brightness");
                break;
            case 2:
                ApplyTargetContrastStep("forced_2min_contrast");
                break;
        }

        forcedTwoMinuteStepIndex++;
    }

    void CaptureBaselines()
    {
        baselineAmbientIntensity = RenderSettings.ambientIntensity;
        baselineReflectionIntensity = RenderSettings.reflectionIntensity;

        CacheLightBaselines(sceneLights);
        CacheLightBaselines(neonLights);
        CacheMaterialBaselines(roofEmissiveMaterials, roofMaterialBaselines);
        CacheMaterialBaselines(targetEmissiveMaterials, targetMaterialBaselines);
        CacheProbeBaselines(reflectionProbes);
    }

    void CacheLightBaselines(Light[] lights)
    {
        if (lights == null)
        {
            return;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            lightBaselines.Add(new LightBaseline
            {
                light = light,
                intensity = light.intensity,
                color = light.color
            });
        }
    }

    void CacheMaterialBaselines(Material[] materials, List<MaterialBaseline> output)
    {
        if (materials == null)
        {
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material source = materials[i];
            if (source == null)
            {
                continue;
            }

            Material runtimeMaterial = Instantiate(source);
            materials[i] = runtimeMaterial;

            output.Add(new MaterialBaseline
            {
                material = runtimeMaterial,
                emissionColor = runtimeMaterial.IsKeywordEnabled("_EMISSION")
                    ? runtimeMaterial.GetColor("_EmissionColor")
                    : Color.black,
                emissionEnabled = runtimeMaterial.IsKeywordEnabled("_EMISSION")
            });
        }
    }

    void CacheProbeBaselines(ReflectionProbe[] probes)
    {
        if (probes == null)
        {
            return;
        }

        for (int i = 0; i < probes.Length; i++)
        {
            ReflectionProbe probe = probes[i];
            if (probe == null)
            {
                continue;
            }

            probeBaselines.Add(new ProbeBaseline
            {
                probe = probe,
                intensity = probe.intensity
            });
        }
    }

    void ApplyBrightnessStep(string trigger)
    {
        targetBrightnessFactor = Mathf.Clamp(
            targetBrightnessFactor * brightnessStepMultiplier,
            minBrightnessFactor,
            1f);

        targetReflectionFactor = Mathf.Clamp(
            targetReflectionFactor * 0.90f,
            minReflectionFactor,
            1f);

        RegisterAdaptationStep(trigger);
    }

    void ApplyTargetContrastStep(string trigger)
    {
        targetTargetContrastFactor = Mathf.Clamp(
            targetTargetContrastFactor * targetContrastStepMultiplier,
            1f,
            maxTargetContrastFactor);

        RegisterAdaptationStep(trigger);
    }

    void ApplyWarmthStep(string trigger)
    {
        targetWarmthFactor = Mathf.Clamp(
            targetWarmthFactor + warmthStepAmount,
            0f,
            maxWarmthFactor);

        RegisterAdaptationStep(trigger);
    }

    void MaybeRecoverStep()
    {
        bool needsRecovery =
            targetBrightnessFactor < 0.999f ||
            targetReflectionFactor < 0.999f ||
            targetTargetContrastFactor > 1.001f ||
            targetWarmthFactor > 0.001f;

        if (!needsRecovery)
        {
            return;
        }

        targetBrightnessFactor = Mathf.Min(1f, targetBrightnessFactor + 0.05f);
        targetReflectionFactor = Mathf.Min(1f, targetReflectionFactor + 0.05f);
        targetTargetContrastFactor = Mathf.Max(1f, targetTargetContrastFactor - 0.05f);
        targetWarmthFactor = Mathf.Max(0f, targetWarmthFactor - (warmthStepAmount * 0.5f));

        RegisterAdaptationStep("recovery", false);
    }

    void RegisterAdaptationStep(string trigger, bool countsTowardLimit = true)
    {
        lastStepTime = Time.unscaledTime;
        lastAppliedTrigger = trigger;

        if (countsTowardLimit)
        {
            adaptationStepsApplied++;
        }

        if (logVisualChanges)
        {
            Debug.Log("[AdaptiveFatigue] *** APLICANDO CAMBIO *** " + DescribeTrigger(trigger));
            Debug.Log("[AdaptiveFatigue] Que se modifica -> " + DescribeWhatChanges(trigger));
            Debug.Log(
                "[AdaptiveFatigue] Valores objetivo -> brillo=" + targetBrightnessFactor.ToString("F2") +
                " contraste=" + targetTargetContrastFactor.ToString("F2") +
                " calor=" + targetWarmthFactor.ToString("F2") +
                " (transición " + transitionSeconds.ToString("F1") + "s)"
            );
        }
        else
        {
            Debug.Log(
                "[AdaptiveFatigue] " + trigger +
                " | brightness=" + targetBrightnessFactor.ToString("F2") +
                " contrast=" + targetTargetContrastFactor.ToString("F2") +
                " warmth=" + targetWarmthFactor.ToString("F2")
            );
        }

        LogAdaptation(trigger);
    }

    string DescribeTrigger(string trigger)
    {
        switch (trigger)
        {
            case "pupil_instability":
                return "Tono cálido (inestabilidad pupilar)";
            case "high_blink_rate":
                return "Bajar brillo (muchos parpadeos)";
            case "long_fixation":
                return "Subir contraste en labels (fijación larga)";
            case "forced_2min_warmth":
                return "Tono cálido [modo 2 min, sin fatiga]";
            case "forced_2min_brightness":
                return "Bajar brillo [modo 2 min, sin fatiga]";
            case "forced_2min_contrast":
                return "Subir contraste en labels [modo 2 min, sin fatiga]";
            case "recovery":
                return "Recuperación lenta hacia iluminación normal";
            default:
                return trigger;
        }
    }

    string DescribeWhatChanges(string trigger)
    {
        switch (trigger)
        {
            case "pupil_instability":
            case "forced_2min_warmth":
                return "Color de luces más cálido + emisión del techo más cálida";
            case "high_blink_rate":
            case "forced_2min_brightness":
                return "Brillo ambiental, intensidad de luces y reflejos del suelo (-15%)";
            case "long_fixation":
            case "forced_2min_contrast":
                return "Emisión de materiales target (precios/labels de productos) +10%";
            case "recovery":
                return "Vuelve gradualmente a valores base de brillo, contraste y calor";
            default:
                return "Parámetros de iluminación de la escena";
        }
    }

    void SmoothTowardsTargets()
    {
        float step = transitionSeconds <= 0f
            ? 1f
            : Time.unscaledDeltaTime / transitionSeconds;

        brightnessFactor = Mathf.MoveTowards(brightnessFactor, targetBrightnessFactor, step);
        targetContrastFactor = Mathf.MoveTowards(targetContrastFactor, targetTargetContrastFactor, step);
        warmthFactor = Mathf.MoveTowards(warmthFactor, targetWarmthFactor, step);
        reflectionFactor = Mathf.MoveTowards(reflectionFactor, targetReflectionFactor, step);

        ApplyImmediateTargets();

        bool isBlendingVisuals = IsBlendingVisuals();
        if (logVisualChanges && wasBlendingVisuals && !isBlendingVisuals)
        {
            string changeLabel = string.IsNullOrEmpty(lastAppliedTrigger)
                ? "adaptación"
                : DescribeTrigger(lastAppliedTrigger);

            Debug.Log(
                "[AdaptiveFatigue] *** CAMBIO VISIBLE EN PANTALLA *** " + changeLabel +
                " | brillo=" + brightnessFactor.ToString("F2") +
                " contraste=" + targetContrastFactor.ToString("F2") +
                " calor=" + warmthFactor.ToString("F2") +
                " | t=" + Time.time.ToString("F1") + "s"
            );
        }

        wasBlendingVisuals = isBlendingVisuals;
    }

    bool IsBlendingVisuals()
    {
        return !Mathf.Approximately(brightnessFactor, targetBrightnessFactor)
            || !Mathf.Approximately(targetContrastFactor, targetTargetContrastFactor)
            || !Mathf.Approximately(warmthFactor, targetWarmthFactor)
            || !Mathf.Approximately(reflectionFactor, targetReflectionFactor);
    }

    void ApplyImmediateTargets()
    {
        RenderSettings.ambientIntensity = baselineAmbientIntensity * brightnessFactor;
        RenderSettings.reflectionIntensity = baselineReflectionIntensity * reflectionFactor;

        Color warmWhite = Color.Lerp(Color.white, new Color(1f, 0.88f, 0.72f), warmthFactor);

        for (int i = 0; i < lightBaselines.Count; i++)
        {
            LightBaseline baseline = lightBaselines[i];
            if (baseline.light == null)
            {
                continue;
            }

            baseline.light.intensity = baseline.intensity * brightnessFactor;
            baseline.light.color = Color.Lerp(baseline.color, warmWhite, warmthFactor);
        }

        for (int i = 0; i < roofMaterialBaselines.Count; i++)
        {
            MaterialBaseline baseline = roofMaterialBaselines[i];
            if (baseline.material == null || !baseline.emissionEnabled)
            {
                continue;
            }

            Color emission = baseline.emissionColor * brightnessFactor;
            emission = Color.Lerp(
                emission,
                new Color(emission.r, emission.g * 0.92f, emission.b * 0.78f),
                warmthFactor * 0.35f);
            baseline.material.SetColor("_EmissionColor", emission);
        }

        for (int i = 0; i < targetMaterialBaselines.Count; i++)
        {
            MaterialBaseline baseline = targetMaterialBaselines[i];
            if (baseline.material == null || !baseline.emissionEnabled)
            {
                continue;
            }

            baseline.material.SetColor("_EmissionColor", baseline.emissionColor * targetContrastFactor);
        }

        for (int i = 0; i < probeBaselines.Count; i++)
        {
            ProbeBaseline baseline = probeBaselines[i];
            if (baseline.probe == null)
            {
                continue;
            }

            baseline.probe.intensity = baseline.intensity * reflectionFactor;
        }
    }

    void LogAdaptation(string trigger)
    {
        if (adaptationWriter == null)
        {
            return;
        }

        float vfi = fatigueMonitor != null ? fatigueMonitor.VisualFatigueIndex : 0f;
        adaptationWriter.WriteLine(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," +
            Time.time.ToString("F3", CultureInfo.InvariantCulture) + "," +
            trigger + "," +
            brightnessFactor.ToString("F3", CultureInfo.InvariantCulture) + "," +
            targetContrastFactor.ToString("F3", CultureInfo.InvariantCulture) + "," +
            warmthFactor.ToString("F3", CultureInfo.InvariantCulture) + "," +
            reflectionFactor.ToString("F3", CultureInfo.InvariantCulture) + "," +
            vfi.ToString("F3", CultureInfo.InvariantCulture)
        );
        adaptationWriter.Flush();
    }

    void OnDestroy()
    {
        if (adaptationWriter != null)
        {
            adaptationWriter.Flush();
            adaptationWriter.Close();
            adaptationWriter = null;
        }
    }
}
