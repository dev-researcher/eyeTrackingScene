using UnityEngine;
using UnityEngine.XR;

public class OpenXRGazeTest : MonoBehaviour
{
    void Update()
    {
        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);

        if (head.isValid)
        {
            Debug.Log("XR headset detectado");
        }
        else
        {
            Debug.Log("No XR headset");
        }
    }
}