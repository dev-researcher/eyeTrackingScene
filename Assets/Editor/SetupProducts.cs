using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class SetupProducts : EditorWindow
{
    [MenuItem("Tools/Setup Products")]
    public static void Setup()
    {
        GameObject products = GameObject.Find("Products");

        if (products == null)
        {
            Debug.LogError("No encontré un objeto llamado Products en la escena.");
            return;
        }

        foreach (Transform child in products.GetComponentsInChildren<Transform>())
        {
            if (child == products.transform) continue;

            GameObject obj = child.gameObject;
            obj.tag = "Product";

            if (obj.GetComponent<Collider>() == null)
            {
                obj.AddComponent<BoxCollider>();
            }

            if (obj.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = obj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }
        }

        Debug.Log("Todos los productos fueron configurados.");
    }
}