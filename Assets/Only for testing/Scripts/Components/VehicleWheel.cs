using UnityEngine;

/// <summary>
/// Wheel component that syncs physics with visuals and provides smooth steering animation.
/// </summary>
public class VehicleWheel : MonoBehaviour
{
    [Header("References")]
    public WheelCollider wheelCollider;
    public Transform wheelVisual;

    [Header("Wheel Type")]
    public bool isSteer;
    public bool isMotor;

    [Header("Steering Animation")]
    [Tooltip("How fast the wheel turns to target angle (degrees/second)")]
    public float steerSpeed = 120f;

    // Internal state
    private float currentSteerAngle = 0f;
    private float targetSteerAngle = 0f;

    /// <summary>
    /// Syncs the visual wheel mesh with the WheelCollider physics.
    /// </summary>
    public void SyncVisuals()
    {
        if (wheelCollider == null || wheelVisual == null) return;

        // Get physics pose
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);

        // Apply position from physics
        wheelVisual.position = pos;

        // Apply rotation from physics (this includes wheel spin)
        wheelVisual.rotation = rot;
    }

    /// <summary>
    /// Applies motor torque to the wheel.
    /// </summary>
    public void ApplyTorque(float torque)
    {
        if (wheelCollider != null)
        {
            wheelCollider.motorTorque = torque;
        }
    }

    /// <summary>
    /// Applies brake torque to the wheel.
    /// </summary>
    public void ApplyBrake(float brakeTorque)
    {
        if (wheelCollider != null)
        {
            wheelCollider.brakeTorque = brakeTorque;
        }
    }

    /// <summary>
    /// Sets the target steer angle. The wheel will smoothly animate to this angle.
    /// </summary>
    public void ApplySteer(float angle)
    {
        targetSteerAngle = angle;
    }

    /// <summary>
    /// Returns the current wheel RPM.
    /// </summary>
    public float GetRPM()
    {
        return wheelCollider != null ? wheelCollider.rpm : 0f;
    }

    void Update()
    {
        // Smooth steering animation
        if (isSteer && wheelCollider != null)
        {
            // Smoothly interpolate towards target angle
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, steerSpeed * Time.deltaTime);
            wheelCollider.steerAngle = currentSteerAngle;
        }
    }
}