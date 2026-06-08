using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float mouseSensitivity = 2f;
    public float gravity = -9.81f;

    public Transform cameraTransform;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        // float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Flechas izquierda/derecha para girar
        float keyboardLookSpeed = 0.7f;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            mouseX = -keyboardLookSpeed;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            mouseX = keyboardLookSpeed;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            mouseY = -keyboardLookSpeed;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            mouseY = keyboardLookSpeed;
        }

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        // h
        // float x = Input.GetAxis("Horizontal");
        // float z = Input.GetAxis("Vertical");
        // h

        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.D)) x = 1f;
        if (Input.GetKey(KeyCode.W)) z = 1f;
        if (Input.GetKey(KeyCode.S)) z = -1f;

        // Vector3 move = transform.right * x + transform.forward * z;
        // controller.Move(move * walkSpeed * Time.deltaTime);

        // if (controller.isGrounded && verticalVelocity < 0)
        // {
        //     verticalVelocity = -2f;
        // }

        Vector3 move = transform.right * x + transform.forward * z;

        if (move.magnitude > 1f)
        {
            move.Normalize();
        }

        controller.Move(move * walkSpeed * Time.deltaTime);

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}


