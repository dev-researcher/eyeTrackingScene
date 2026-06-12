using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Lee eye tracking (OpenXR / Varjo) y calcula:
/// - parpadeos/min
/// - duración de fijación
/// - variabilidad pupilar (CV)
///
/// Expone flags para el controlador adaptativo:
/// 1) PupilInstability  -> tono cálido
/// 2) HighBlinkRate     -> bajar brillo
/// 3) LongFixation      -> subir contraste en targets
/// </summary>
public class OculometricFatigueMonitor : MonoBehaviour
{
    [Header("Referencias")]
    public Transform xrCamera;

    [Header("Muestreo")]
    public float sampleInterval = 0.02f;
    public float evaluationWindowSeconds = 15f;
    public float calibrationSeconds = 30f;

    [Header("Detección de fijación")]
    public float fixationAngularThresholdDegrees = 1.5f;
    public float minFixationDurationMs = 100f;

    [Header("Detección de parpadeo")]
    [Range(0f, 1f)]
    public float blinkEyeOpenThreshold = 0.35f;
    public float minBlinkDurationMs = 40f;

    [Header("Umbrales de la tesis")]
    public float baselineBlinkRatePerMin = 17f;
    public float fatigueBlinkRatePerMin = 25f;
    public float baselineFixationDurationMs = 300f;
    public float fatigueFixationDurationMs = 500f;
    public float baselinePupilCvPercent = 15f;
    public float fatiguePupilCvPercent = 20f;

    [Header("Persistencia del trigger")]
    public int windowsToTrigger = 2;
    public int windowsToRecover = 3;

    [Header("Debug sin VR (Editor)")]
    public bool enableKeyboardSimulation = false;

    public float BlinkRatePerMin { get; private set; }
    public float MeanFixationDurationMs { get; private set; }
    public float PupilDiameterCvPercent { get; private set; }
    public float VisualFatigueIndex { get; private set; }

    public bool HighBlinkRate { get; private set; }
    public bool LongFixation { get; private set; }
    public bool PupilInstability { get; private set; }

    public bool IsCalibrated => Time.time >= calibrationEndTime;

    readonly List<Sample> samples = new List<Sample>();
    readonly List<InputDevice> eyeDevices = new List<InputDevice>();
    readonly List<float> externalPupilSamples = new List<float>();

    float sampleTimer;
    float evaluationTimer;
    float calibrationEndTime;

    Vector3 lastGazeDirection = Vector3.forward;
    float currentFixationStartTime = -1f;
    float blinkCloseStartTime = -1f;
    bool eyesClosed;

    int highBlinkWindows;
    int longFixationWindows;
    int pupilInstabilityWindows;
    int recoveredBlinkWindows;
    int recoveredFixationWindows;
    int recoveredPupilWindows;

    struct Sample
    {
        public float time;
        public float fixationDurationMs;
        public bool blinkEnded;
        public float pupilDiameter;
        public bool pupilValid;
    }

    void OnEnable()
    {
        recoveredBlinkWindows = windowsToRecover;
        recoveredFixationWindows = windowsToRecover;
        recoveredPupilWindows = windowsToRecover;
    }

    void Start()
    {
        calibrationEndTime = Time.time + calibrationSeconds;
    }

    void Update()
    {
        HandleKeyboardSimulation();

        sampleTimer += Time.unscaledDeltaTime;
        if (sampleTimer < sampleInterval)
        {
            return;
        }

        sampleTimer -= sampleInterval;
        CaptureEyeSample();

        evaluationTimer += sampleInterval;
        if (evaluationTimer >= evaluationWindowSeconds)
        {
            evaluationTimer = 0f;
            EvaluateWindows();
        }
    }

    void HandleKeyboardSimulation()
    {
        if (!enableKeyboardSimulation)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            for (int i = 0; i < 8; i++)
            {
                ReportPupilDiameter(Random.Range(2.8f, 4.2f));
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            for (int i = 0; i < 8; i++)
            {
                AddSample(0f, true, 0f, false);
            }
            EvaluateWindows();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            AddSample(650f, false, 0f, false);
            EvaluateWindows();
        }
    }

    public void ReportPupilDiameter(float diameterMillimeters)
    {
        if (diameterMillimeters <= 0f)
        {
            return;
        }

        externalPupilSamples.Add(diameterMillimeters);
    }

    void CaptureEyeSample()
    {
        eyeDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);

        bool gazeValid = false;
        Vector3 gazeDirection = xrCamera != null ? xrCamera.forward : Vector3.forward;
        float leftOpen = 1f;
        float rightOpen = 1f;
        bool openAmountValid = false;

        foreach (InputDevice device in eyeDevices)
        {
            if (!device.isValid)
            {
                continue;
            }

            Eyes eyes;
            if (!device.TryGetFeatureValue(CommonUsages.eyesData, out eyes))
            {
                continue;
            }

            gazeValid = true;

            Vector3 fixation;
            if (eyes.TryGetFixationPoint(out fixation) && xrCamera != null)
            {
                gazeDirection = (fixation - xrCamera.position).normalized;
            }

            float left;
            float right;
            if (eyes.TryGetLeftEyeOpenAmount(out left) && eyes.TryGetRightEyeOpenAmount(out right))
            {
                leftOpen = left;
                rightOpen = right;
                openAmountValid = true;
            }

            break;
        }

        if (!gazeValid)
        {
            return;
        }

        bool blinkEnded = false;
        if (openAmountValid)
        {
            float eyeOpen = Mathf.Min(leftOpen, rightOpen);
            if (!eyesClosed && eyeOpen <= blinkEyeOpenThreshold)
            {
                eyesClosed = true;
                blinkCloseStartTime = Time.time;
            }
            else if (eyesClosed && eyeOpen > blinkEyeOpenThreshold)
            {
                float blinkDurationMs = (Time.time - blinkCloseStartTime) * 1000f;
                if (blinkDurationMs >= minBlinkDurationMs)
                {
                    blinkEnded = true;
                }

                eyesClosed = false;
            }
        }


        float angle = Vector3.Angle(lastGazeDirection, gazeDirection);
        if (angle <= fixationAngularThresholdDegrees)
        {
            if (currentFixationStartTime < 0f)
            {
                currentFixationStartTime = Time.time;
            }
        }
        else
        {
            if (currentFixationStartTime >= 0f)
            {
                float fixationDurationMs = (Time.time - currentFixationStartTime) * 1000f;
                if (fixationDurationMs >= minFixationDurationMs)
                {
                    AddSample(fixationDurationMs, false, 0f, false);
                }
            }

            currentFixationStartTime = Time.time;
            lastGazeDirection = gazeDirection;
        }

        if (blinkEnded)
        {
            AddSample(0f, true, 0f, false);
        }

        if (externalPupilSamples.Count > 0)
        {
            float pupil = externalPupilSamples[externalPupilSamples.Count - 1];
            AddSample(0f, false, pupil, true);
            externalPupilSamples.Clear();
        }
    }

    void AddSample(float fixationDurationMs, bool blinkEnded, float pupilDiameter, bool pupilValid)
    {
        samples.Add(new Sample
        {
            time = Time.time,
            fixationDurationMs = fixationDurationMs,
            blinkEnded = blinkEnded,
            pupilDiameter = pupilDiameter,
            pupilValid = pupilValid
        });

        TrimSamples(60f);
    }

    void EvaluateWindows()
    {
        TrimSamples(evaluationWindowSeconds);

        int blinkCount = 0;
        int fixationCount = 0;
        float fixationSumMs = 0f;
        List<float> pupilValues = new List<float>();

        for (int i = 0; i < samples.Count; i++)
        {
            Sample sample = samples[i];
            if (sample.blinkEnded)
            {
                blinkCount++;
            }

            if (sample.fixationDurationMs > 0f)
            {
                fixationCount++;
                fixationSumMs += sample.fixationDurationMs;
            }

            if (sample.pupilValid)
            {
                pupilValues.Add(sample.pupilDiameter);
            }
        }

        float windowMinutes = Mathf.Max(evaluationWindowSeconds / 60f, 0.01f);
        BlinkRatePerMin = blinkCount / windowMinutes;
        MeanFixationDurationMs = fixationCount > 0 ? fixationSumMs / fixationCount : 0f;
        PupilDiameterCvPercent = ComputeCoefficientOfVariationPercent(pupilValues);

        // FIX CS0206: usar variables locales, no propiedades con out
        bool highBlinkRate;
        UpdatePersistentFlag(
            BlinkRatePerMin >= fatigueBlinkRatePerMin,
            ref highBlinkWindows,
            ref recoveredBlinkWindows,
            out highBlinkRate);
        HighBlinkRate = highBlinkRate;

        bool longFixation;
        UpdatePersistentFlag(
            MeanFixationDurationMs >= fatigueFixationDurationMs,
            ref longFixationWindows,
            ref recoveredFixationWindows,
            out longFixation);
        LongFixation = longFixation;

        bool pupilInstability;
        UpdatePersistentFlag(
            pupilValues.Count >= 5 && PupilDiameterCvPercent >= fatiguePupilCvPercent,
            ref pupilInstabilityWindows,
            ref recoveredPupilWindows,
            out pupilInstability);
        PupilInstability = pupilInstability;

        VisualFatigueIndex = ComputeVisualFatigueIndex();
    }

    void UpdatePersistentFlag(
        bool thresholdMet,
        ref int activeWindows,
        ref int recoveredWindows,
        out bool activeFlag)
    {
        if (thresholdMet)
        {
            recoveredWindows = 0;
            activeWindows++;
            activeFlag = activeWindows >= windowsToTrigger;
        }
        else
        {
            activeWindows = 0;
            recoveredWindows++;
            activeFlag = recoveredWindows < windowsToRecover;
        }
    }

    float ComputeVisualFatigueIndex()
    {
        float blinkScore = NormalizeAbove(BlinkRatePerMin, baselineBlinkRatePerMin, fatigueBlinkRatePerMin);
        float fixationScore = NormalizeAbove(MeanFixationDurationMs, baselineFixationDurationMs, fatigueFixationDurationMs);
        float pupilScore = NormalizeAbove(PupilDiameterCvPercent, baselinePupilCvPercent, fatiguePupilCvPercent);

        return Mathf.Clamp01((blinkScore + fixationScore + pupilScore) / 3f);
    }

    static float NormalizeAbove(float value, float baseline, float fatigue)
    {
        if (fatigue <= baseline)
        {
            return 0f;
        }

        return Mathf.Clamp01((value - baseline) / (fatigue - baseline));
    }

    static float ComputeCoefficientOfVariationPercent(List<float> values)
    {
        if (values == null || values.Count < 2)
        {
            return 0f;
        }

        float mean = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            mean += values[i];
        }

        mean /= values.Count;
        if (mean <= 0.0001f)
        {
            return 0f;
        }

        float variance = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            float delta = values[i] - mean;
            variance += delta * delta;
        }

        variance /= values.Count;
        return (Mathf.Sqrt(variance) / mean) * 100f;
    }

    void TrimSamples(float maxAgeSeconds)
    {
        float minTime = Time.time - maxAgeSeconds;
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].time < minTime)
            {
                samples.RemoveAt(i);
            }
        }
    }
}