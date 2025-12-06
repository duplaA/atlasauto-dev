using UnityEngine;
using UnityEngine.InputSystem;

public class Camera : MonoBehaviour
{
    [Header("Targets")]
    public Transform target;

    [Header("Settings")]
    public float distance = 10.0f;
    public float minDistance = 2f;
    public float maxDistance = 20f;
    
    public float sensitivityX = 0.2f;
    public float sensitivityY = 0.2f;
    public float scrollSensitivity = 0.5f;

    [Header("Limits")]
    public float minVerticalAngle = 90;
    public float maxVerticalAngle = -30;

    // track angles
    private float _currentX = 0f;
    private float _currentY = 20f;
    private Mouse _mouseDevice;

    void Start()
    {
        _mouseDevice = Mouse.current;
        _currentX = 0f;
        _currentY = 20f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        // Handle Input
        if (_mouseDevice != null && _mouseDevice.rightButton.isPressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 mouseDelta = _mouseDevice.delta.ReadValue();

            if (!float.IsNaN(mouseDelta.x) && !float.IsNaN(mouseDelta.y))
            {
                _currentX += mouseDelta.x * sensitivityX * 0.5f;
                _currentY -= mouseDelta.y * sensitivityY * 0.5f;
                
                // clamp vertical
                _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (_mouseDevice != null)
        {
            float scrollValue = _mouseDevice.scroll.ReadValue().y;
            // scale down scroll
            if (scrollValue != 0)
            {
                distance -= scrollValue * scrollSensitivity * 0.5f; 
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);
        Vector3 position = target.position + (rotation * new Vector3(0, 0, -distance));
        transform.rotation = rotation;
        transform.position = position;
    }
}