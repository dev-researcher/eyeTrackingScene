using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

public class VarjoOpenXRGazeLogger : MonoBehaviour
{
    [Header("Participant")]
    public string participantId = "P001";
    public string condition = "supermarket";

    [Header("References")]
    public Transform xrCamera;
    public Transform playerTransform;

    [Header("Sampling")]
    public float sampleInterval = 0.02f; // 0.02 = 50 Hz

    private StreamWriter writer;
    private string filePath;
    private float timer = 0f;
    private int purchaseCount = 0;
    private bool purchasePressedThisFrame = false;

    void Start()
    {
        string folder = Path.Combine(Application.persistentDataPath, "EyeTrackingData");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = participantId + "_" + condition + "_" + timestamp + ".csv";

        filePath = Path.Combine(folder, fileName);

        writer = new StreamWriter(filePath, false);

        writer.WriteLine(
            "participantId,condition,systemTimestamp,timeSinceStart," +
            "gazeValid,fixationValid," +
            "gazeOriginX,gazeOriginY,gazeOriginZ," +
            "gazeDirX,gazeDirY,gazeDirZ," +
            "fixationX,fixationY,fixationZ," +
            "playerX,playerY,playerZ," +
            "purchaseCount,purchasePressed"
        );

        Debug.Log("Guardando datos eye tracking en: " + filePath);
    }

    void Update()
    {
        purchasePressedThisFrame = false;

        if (Input.GetKeyDown(KeyCode.B))
        {
            purchaseCount++;
            purchasePressedThisFrame = true;
            Debug.Log("Compra simulada #" + purchaseCount);
        }

        timer += Time.deltaTime;

        if (timer >= sampleInterval)
        {
            timer = 0f;
            SaveSample();
        }
    }

    void SaveSample()
    {
        bool gazeValid = false;
        bool fixationValid = false;

        Vector3 gazeOrigin = xrCamera != null ? xrCamera.position : Vector3.zero;
        Vector3 gazeDirection = xrCamera != null ? xrCamera.forward : Vector3.forward; 
        Vector3 fixationPoint = Vector3.zero;

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, devices);

        foreach (InputDevice device in devices)
        {
            if (!device.isValid) continue;

            Eyes eyes;
            if (device.TryGetFeatureValue(CommonUsages.eyesData, out eyes))
            {
                Debug.Log("Eyes Data found!!!");
                gazeValid = true;

                Vector3 fixation;
                if (eyes.TryGetFixationPoint(out fixation))
                {
                    fixationValid = true;
                    fixationPoint = fixation;

                    if (xrCamera != null)
                    {
                        gazeOrigin = xrCamera.position;
                        gazeDirection = (fixationPoint - gazeOrigin).normalized;
                    }
                }

                break;
            }
        }

        Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        writer.WriteLine(
            participantId + "," +
            condition + "," +
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," +
            F(Time.time) + "," +
            gazeValid + "," +
            fixationValid + "," +
            F(gazeOrigin.x) + "," +
            F(gazeOrigin.y) + "," +
            F(gazeOrigin.z) + "," +
            F(gazeDirection.x) + "," +
            F(gazeDirection.y) + "," +
            F(gazeDirection.z) + "," +
            F(fixationPoint.x) + "," +
            F(fixationPoint.y) + "," +
            F(fixationPoint.z) + "," +
            F(playerPos.x) + "," +
            F(playerPos.y) + "," +
            F(playerPos.z) + "," +
            purchaseCount + "," +
            purchasePressedThisFrame
        );

        writer.Flush();
    }

    string F(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    void OnApplicationQuit()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
        }
    }
}