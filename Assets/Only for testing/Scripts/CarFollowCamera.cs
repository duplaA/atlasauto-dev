using UnityEngine;
using UnityEngine.InputSystem;

public class CarFollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 10.0f;
    public float minDistance = 2f;
    public float maxDistance = 20f;
    public float sensitivityX = 5f;
    public float sensitivityY = 5f;

    public float minVerticalAngle = -30f; 
    public float maxVerticalAngle = 60f;

    private float _currentX = 0f;
    private float _currentY = 20f;

    void LateUpdate()
    {
        if (target == null) return;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            _currentX += mouseDelta.x * sensitivityX * Time.deltaTime;
            _currentY -= mouseDelta.y * sensitivityY * Time.deltaTime;
            _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        distance = Mathf.Clamp(distance - scroll * 1f, minDistance, maxDistance);

        Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);
        transform.position = target.position + (rotation * new Vector3(0, 0, -distance));
        transform.LookAt(target.position + Vector3.up * 1.5f); // Look slightly above the car
    }
}