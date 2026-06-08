using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class PurchaseIntentLogger : MonoBehaviour
{
    private int purchaseCount = 0;
    private string filePath;

    void Start()
    {
        filePath = Path.Combine(Application.dataPath, "purchase_intent_log.csv");

        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "Count,Timestamp,TimeSinceStart\n");
        }

        Debug.Log("Purchase logger listo. Archivo: " + filePath);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            purchaseCount++;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string timeSinceStart = Time.time.ToString("F3");

            string line = purchaseCount + "," + timestamp + "," + timeSinceStart + "\n";
            File.AppendAllText(filePath, line);

            Debug.Log("Compra simulada #" + purchaseCount + " registrada en " + timestamp);
        }
    }
}