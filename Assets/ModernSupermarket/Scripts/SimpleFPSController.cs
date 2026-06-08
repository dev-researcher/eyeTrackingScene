// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class SimpleFPSController : MonoBehaviour
// {
//     public float walkSpeed = 4f;
//     public float mouseSensitivity = 2f;
//     public float gravity = -9.81f;

//     public Transform cameraTransform;

//     private CharacterController controller;
//     private float verticalVelocity;
//     private float cameraPitch;

//     void Start()
//     {
//         controller = GetComponent<CharacterController>();

//         if (cameraTransform == null)
//         {
//             cameraTransform = Camera.main.transform;
//         }

//         Cursor.lockState = CursorLockMode.Locked;
//         Cursor.visible = false;
//     }

//     void Update()
//     {
//         // float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
//         // float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
//         float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
//         float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

//         // Flechas izquierda/derecha para girar
//         float keyboardLookSpeed = 0.7f;

//         if (Input.GetKey(KeyCode.LeftArrow))
//         {
//             mouseX = -keyboardLookSpeed;
//         }

//         if (Input.GetKey(KeyCode.RightArrow))
//         {
//             mouseX = keyboardLookSpeed;
//         }

//         if (Input.GetKey(KeyCode.UpArrow))
//         {
//             mouseY = -keyboardLookSpeed;
//         }

//         if (Input.GetKey(KeyCode.DownArrow))
//         {
//             mouseY = keyboardLookSpeed;
//         }

//         transform.Rotate(Vector3.up * mouseX);

//         cameraPitch -= mouseY;
//         cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
//         cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
//         // h
//         // float x = Input.GetAxis("Horizontal");
//         // float z = Input.GetAxis("Vertical");
//         // h

//         float x = 0f;
//         float z = 0f;

//         if (Input.GetKey(KeyCode.A)) x = -1f;
//         if (Input.GetKey(KeyCode.D)) x = 1f;
//         if (Input.GetKey(KeyCode.W)) z = 1f;
//         if (Input.GetKey(KeyCode.S)) z = -1f;

//         // Vector3 move = transform.right * x + transform.forward * z;
//         // controller.Move(move * walkSpeed * Time.deltaTime);

//         // if (controller.isGrounded && verticalVelocity < 0)
//         // {
//         //     verticalVelocity = -2f;
//         // }

//         Vector3 move = transform.right * x + transform.forward * z;

//         if (move.magnitude > 1f)
//         {
//             move.Normalize();
//         }

//         controller.Move(move * walkSpeed * Time.deltaTime);

//         verticalVelocity += gravity * Time.deltaTime;
//         controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
//     }
// }

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    public float movementSpeed = 10f;
    public float fastMovementSpeed = 100f;
    public float freeLookSensitivity = 3f;
    public float zoomSensitivity = 10f;
    public float fastZoomSensitivity = 50f;
    private bool looking = false;



    public Transform holdPoint;
    public float pickupDistance = 3f;
    private GameObject heldItem;

    void Update()
    {
        var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            transform.position = transform.position + (-transform.right * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            transform.position = transform.position + (transform.right * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            transform.position = transform.position + (-transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            transform.position = transform.position + (transform.up * movementSpeed * Time.deltaTime);
        }

        // if (Input.GetKey(KeyCode.E))
        // {
        //     transform.position = transform.position + (-transform.up * movementSpeed * Time.deltaTime);
        // }
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null)
                TryPickup();
            else
                DropItem();
        }

        if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp))
        {
            transform.position = transform.position + (Vector3.up * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown))
        {
            transform.position = transform.position + (-Vector3.up * movementSpeed * Time.deltaTime);
        }

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
}
