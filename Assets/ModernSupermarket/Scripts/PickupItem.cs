using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public Transform holdPoint;
    public float pickupDistance = 3f;

    private GameObject heldItem;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null)
                TryPickup();
            else
                DropItem();
        }
    }

    void TryPickup()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupDistance))
        {
            if (hit.collider.CompareTag("Product"))
            {
                heldItem = hit.collider.gameObject;

                Rigidbody rb = heldItem.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

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
        if (rb != null) rb.isKinematic = false;

        heldItem = null;
    }

    public bool HasItem()
    {
        return heldItem != null;
    }

    public void RemoveHeldItem()
    {
        if (heldItem != null)
        {
            Destroy(heldItem);
            heldItem = null;
        }
    }
}