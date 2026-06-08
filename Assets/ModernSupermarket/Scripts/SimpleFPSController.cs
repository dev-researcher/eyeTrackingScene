using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    public float movementSpeed = 10f;
    public float fastMovementSpeed = 100f;
    public float freeLookSensitivity = 3f;
    public float zoomSensitivity = 10f;
    public float fastZoomSensitivity = 50f;
    private bool looking = false;
    private float verticalRotation = 0f;

    public Transform holdPoint;
    public float pickupDistance = 3f;
    private GameObject heldItem;

    void Update()
    {
        var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

        if (Input.GetKey(KeyCode.A))
        {
            transform.position = transform.position + (-transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.D))
        {

            transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.W))
        {
            transform.position = transform.position + (-transform.right * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.S))
        {
            transform.position = transform.position + (transform.right * movementSpeed * Time.deltaTime);
        }

        float arrowLookSpeed = freeLookSensitivity * 30f * Time.deltaTime;

        // Girar izquierda y derecha
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Rotate(0f, -arrowLookSpeed, 0f);
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Rotate(0f, arrowLookSpeed, 0f);
        }

        // Mirar arriba y abajo
        if (Input.GetKey(KeyCode.UpArrow))
        {
            // verticalRotation -= arrowLookSpeed;
            transform.position = transform.position + (Vector3.up * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            // verticalRotation += arrowLookSpeed;
            transform.position = transform.position + (-Vector3.up * movementSpeed * Time.deltaTime);
        }

        // Limitar cuánto puede mirar arriba y abajo
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);

        // Aplicar la rotación sin inclinar la cámara
        Vector3 angles = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(verticalRotation, angles.y, 0f);

        // if (Input.GetKeyDown(KeyCode.E))
        // {
        //     if (heldItem == null)
        //         TryPickup();
        //     else
        //         DropItem();
        // }

        if (looking)
        {
            float newRotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
            float newRotationY = transform.localEulerAngles.x - Input.GetAxis("Mouse Y") * freeLookSensitivity;
            transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }

        float axis = Input.GetAxis("Mouse ScrollWheel");
        if (axis > 0)
        {
            GetComponent<Camera>().fieldOfView--;
        }
        else if (axis < 0)
        {
            GetComponent<Camera>().fieldOfView++;
        }

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            StartLooking();
        }
        else if (Input.GetKeyUp(KeyCode.Mouse1))
        {
            StopLooking();
        }
    }

    void OnDisable()
    {
        StopLooking();
    }
    public void StartLooking()
    {
        looking = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void StopLooking()
    {
        looking = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ---
    void TryPickup()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupDistance))
        {
            if (hit.collider.CompareTag("Product"))
            {
                heldItem = hit.collider.gameObject;

                Rigidbody rb = heldItem.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;

                heldItem.transform.SetParent(holdPoint);
                heldItem.transform.localPosition = Vector3.zero;
                heldItem.transform.localRotation = Quaternion.identity;
            }
        }
    }

    void DropItem()
    {
        heldItem.transform.SetParent(null);

        Rigidbody rb = heldItem.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        heldItem = null;
    }

}
