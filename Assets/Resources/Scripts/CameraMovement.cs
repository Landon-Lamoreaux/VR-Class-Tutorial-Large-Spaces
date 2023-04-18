using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{

    [SerializeField]
    private float horizontalAngle = 100;
    [SerializeField]
    private float verticalAngle = 40;
    private float thetaY = 0;
    private float thetaX = 0;

    [SerializeField]
    private float speed = 0.2f;

    public void Start()
    {
        //get actions
        PlayerInput filter = FindObjectOfType<PlayerInput>();
        filter.actions["CameraMove"].performed += OnCameraMove;
    }
    public void OnCameraMove(InputAction.CallbackContext context)
    {
        Vector2 delta = context.ReadValue<Vector2>();
        thetaY += delta.x * speed;
        thetaX -= delta.y * speed;
        thetaY = Mathf.Clamp(thetaY, -horizontalAngle / 2, horizontalAngle / 2);
        thetaX = Mathf.Clamp(thetaX, -verticalAngle / 2, verticalAngle / 2);
        transform.localRotation = Quaternion.Euler(thetaX, thetaY, 0);

    }


}
