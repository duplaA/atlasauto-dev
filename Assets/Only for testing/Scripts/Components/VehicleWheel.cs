using UnityEngine;

/// <summary>
/// Wheel component that syncs physics with visuals and provides smooth steering.
/// </summary>
public class VehicleWheel : MonoBehaviour
{
    [Header("References")]
    public WheelCollider wheelCollider;
    public Transform wheelVisual;

    [Header("Wheel Type")]
    public bool isFront;
    public bool isSteer;
    public bool isMotor;

    [Header("Steering Animation")]
    [Tooltip("How fast the wheel turns to target angle (degrees/second)")]
    public float steerSpeed = 120f;

    // Internal state
    private float currentSteerAngle = 0f;
    private float visualRotation = 0f; 

    // Removed internal LateUpdate - VehicleController will drive this now for perfect sync
    
    void Awake()
    {
        // Auto-discover WheelCollider if not assigned
        if (wheelCollider == null)
        {
            // Try to find on this GameObject
            wheelCollider = GetComponent<WheelCollider>();
            
            // Try to find in children
            if (wheelCollider == null)
            {
                wheelCollider = GetComponentInChildren<WheelCollider>();
            }
            
            // Try to find in parent (if VehicleWheel script is on visual mesh)
            if (wheelCollider == null)
            {
                wheelCollider = GetComponentInParent<WheelCollider>();
            }
        }
        
        // Validation warnings
        if (wheelCollider == null)
        {
            Debug.LogError($"[VehicleWheel] {gameObject.name}: No WheelCollider found! Assign it in Inspector or ensure a WheelCollider is on this GameObject.");
        }
        else
        {
            // Ensure WheelCollider is configured correctly
            if (wheelCollider.suspensionDistance <= 0f)
            {
                Debug.LogWarning($"[VehicleWheel] {gameObject.name}: WheelCollider.suspensionDistance is {wheelCollider.suspensionDistance}. This may cause grounding issues.");
            }
        }
        
        if (wheelVisual == null)
        {
            Debug.LogWarning($"[VehicleWheel] {gameObject.name}: No wheelVisual assigned. Visual sync will be skipped.");
        }
    }

    /// Updates the visual wheel state (Steering and Spin)
    /// Called by VehicleController.
    /// <param name="targetSteer">Target steering angle in degrees</param>
    /// <param name="driveSpeedMS">Vehicle speed in m/s (controls spin speed)</param>
    /// <param name="wheelRadius">Radius to calculate spin from speed</param>
    public void UpdateVisuals(float targetSteer, float driveSpeedMS, float wheelRadius)
    {
        if (wheelCollider == null || wheelVisual == null) return;

        // 1. Position from Physics (Suspension travel)
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelVisual.position = pos;

        // 2. Smooth Steering
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpeed * Time.deltaTime);

        // 3. Spin
        // Calculate RPM from Speed: RPM = (Speed / Circumference) * 60
        // Spin Speed (Deg/Sec) = RPM * 6
        float circumference = 2f * Mathf.PI * wheelRadius;
        float arcadeRPM = (driveSpeedMS * 60f) / circumference;
        
        // "Slower" visual style (0.1f multiplier)
        float spinDegreesPerSec = arcadeRPM * 6f * 0.1f; 
        
        // Apply direction based on speed sign (roughly).
        // In a real game we'd pass signed speed.
        visualRotation += spinDegreesPerSec * Time.deltaTime;

        // 4. Reconstruct Rotation
        // Base: WheelCollider parent rotation (Car Body + Local Offset)
        // wheelCollider.transform.rotation gives us the mounting point's rotation.
        
        Quaternion mountingRot = wheelCollider.transform.rotation;
        Quaternion steerRot = Quaternion.Euler(0, currentSteerAngle, 0);
        Quaternion spinRot = Quaternion.Euler(visualRotation, 0, 0);

        wheelVisual.rotation = mountingRot * steerRot * spinRot;
    }

    /// Applies motor torque to the wheel.
    /// Only applies torque if wheel is grounded.
    public void ApplyTorque(float torque)
    {
        if (wheelCollider == null) return;
        
        // Check if wheel is grounded - WheelCollider only applies force when touching ground
        WheelHit hit;
        bool grounded = wheelCollider.GetGroundHit(out hit);
        
        if (grounded)
        {
            wheelCollider.motorTorque = torque;
        }
        else
        {
            // Wheel in air - no resistance, but also no traction
            wheelCollider.motorTorque = 0f;
        }
    }

    /// Returns true if the wheel is touching the ground.
    public bool IsGrounded()
    {
        if (wheelCollider == null) return false;
        WheelHit hit;
        return wheelCollider.GetGroundHit(out hit);
    }

    /// Applies brake torque to the wheel.
    public void ApplyBrake(float brakeTorque)
    {
        if (wheelCollider != null)
        {
            wheelCollider.brakeTorque = brakeTorque;
        }
    }

    /// Returns the current wheel RPM.
    public float GetRPM()
    {
        return wheelCollider != null ? wheelCollider.rpm : 0f;
    }
}