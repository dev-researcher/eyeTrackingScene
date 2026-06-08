using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

public class QuestProGazeLogger : MonoBehaviour
{
    public Transform centerEyeAnchor;
    public Transform playerTransform;

    private string filePath;

    void Start()
    {
        string fileName = "quest_gaze_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(filePath,
            "timestamp,timeSinceStart,eyeX,eyeY,eyeZ,gazeDirX,gazeDirY,gazeDirZ,playerX,playerY,playerZ\n");

        Debug.Log("Guardando datos de mirada en: " + filePath);
    }

    void Update()
    {
        if (centerEyeAnchor == null) return;

        Vector3 eyePos = centerEyeAnchor.position;
        Vector3 gazeDir = centerEyeAnchor.forward;
        Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        string line =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," +
            Time.time.ToString("F3") + "," +
            eyePos.x.ToString("F4") + "," +
            eyePos.y.ToString("F4") + "," +
            eyePos.z.ToString("F4") + "," +
            gazeDir.x.ToString("F4") + "," +
            gazeDir.y.ToString("F4") + "," +
            gazeDir.z.ToString("F4") + "," +
            playerPos.x.ToString("F4") + "," +
            playerPos.y.ToString("F4") + "," +
            playerPos.z.ToString("F4") + "\n";

        File.AppendAllText(filePath, line);
    }
}