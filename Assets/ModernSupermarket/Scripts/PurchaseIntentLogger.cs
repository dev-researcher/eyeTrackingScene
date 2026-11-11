using System;
using System.IO;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PurchaseIntentLogger : MonoBehaviour
{
    private int purchaseCount = 0;
    private string filePath;

    void Start()
    {
        VarjoOpenXRGazeLogger gaze = FindAnyObjectByType<VarjoOpenXRGazeLogger>();
        string participant = gaze != null ? gaze.participantId : "unknown";
        string condition = gaze != null ? gaze.condition : "unknown";
        string folder = Path.Combine(Application.persistentDataPath, "EyeTrackingData");
        Directory.CreateDirectory(folder);
        string fileName = participant + "_" + condition + "_purchases_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        filePath = Path.Combine(folder, fileName);
        File.WriteAllText(filePath, "participantId,condition,Count,Timestamp,TimeSinceStart\n");

        Debug.Log("Purchase logger listo. Archivo: " + filePath);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            purchaseCount++;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string timeSinceStart = Time.time.ToString("F3");

            VarjoOpenXRGazeLogger gaze = FindAnyObjectByType<VarjoOpenXRGazeLogger>();
            string participant = gaze != null ? gaze.participantId : "unknown";
            string condition = gaze != null ? gaze.condition : "unknown";
            string line = participant + "," + condition + "," + purchaseCount + "," + timestamp + "," + timeSinceStart + "\n";
            File.AppendAllText(filePath, line);

            Debug.Log("Compra simulada #" + purchaseCount + " registrada en " + timestamp);
        }
    }
}