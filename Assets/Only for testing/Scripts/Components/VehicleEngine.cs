using UnityEngine;

/// <summary>
/// Realistic engine simulation based on proper physics formulas.
/// RPM is derived from wheel speed through gear ratios.
/// </summary>
public class VehicleEngine : MonoBehaviour
{
    public enum EngineType { InternalCombustion, Electric }

    [Header("Engine Type")]
    public EngineType engineType = EngineType.InternalCombustion;

    [Header("Engine Specs")]
    [Tooltip("Peak torque in Nm")]
    public float peakTorqueNm = 450f;
    [Tooltip("Maximum engine RPM (redline)")]
    public float maxRPM = 7000f;
    [Tooltip("Idle RPM (ICE only)")]
    public float idleRPM = 850f;
    [Tooltip("Tire radius in meters (for RPM calculation)")]
    public float tireRadius = 0.35f;

    [Header("Engine Inertia")]
    [Tooltip("How fast engine RPM can change (higher = more responsive)")]
    public float engineInertia = 0.15f;
    [Tooltip("Drivetrain efficiency (0.8-0.9 typical)")]
    [Range(0.7f, 1f)]
    public float drivetrainEfficiency = 0.85f;

    [Header("Torque Curve (ICE)")]
    [Tooltip("Normalized torque vs RPM. X = RPM/maxRPM, Y = torque multiplier")]
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0.0f, 0.5f),   // Idle: 50% torque
        new Keyframe(0.15f, 0.7f),  // Low RPM
        new Keyframe(0.4f, 0.95f),  // Building
        new Keyframe(0.6f, 1.0f),   // Peak torque
        new Keyframe(0.8f, 0.9f),   // Past peak
        new Keyframe(1.0f, 0.75f)   // Redline
    );

    [Header("EV Torque Curve")]
    [Tooltip("EV torque vs RPM. Typically flat then decreasing")]
    public AnimationCurve evTorqueCurve = new AnimationCurve(
        new Keyframe(0.0f, 1.0f),   // Instant torque
        new Keyframe(0.3f, 1.0f),   // Flat peak
        new Keyframe(0.6f, 0.85f),  // Decreasing
        new Keyframe(1.0f, 0.5f)    // High speed falloff
    );

    [Header("Engine State")]
    public float currentRPM;
    public float targetRPM;
    public float outputTorque;

    /// <summary>
    /// Calculate the RPM the engine should be at based on vehicle speed and gearing.
    /// Formula: RPM = (VehicleSpeed / TireCircumference) × GearRatio × FinalDrive × 60
    /// </summary>
    public float CalculateRPMFromSpeed(float speedMS, float totalGearRatio)
    {
        if (Mathf.Abs(totalGearRatio) < 0.01f) return idleRPM;
        
        float tireCircumference = 2f * Mathf.PI * tireRadius;
        float wheelRPS = speedMS / tireCircumference; // Rotations per second
        float wheelRPM = wheelRPS * 60f;
        float engineRPM = wheelRPM * Mathf.Abs(totalGearRatio);
        
        return engineRPM;
    }

    /// <summary>
    /// Update engine RPM based on wheel-derived RPM and throttle.
    /// </summary>
    public void UpdateEngine(float throttle, float vehicleSpeedMS, float totalGearRatio, float clutchEngagement, float dt)
    {
        if (engineType == EngineType.Electric)
        {
            UpdateElectricMotor(throttle, vehicleSpeedMS, totalGearRatio, dt);
        }
        else
        {
            UpdateICEngine(throttle, vehicleSpeedMS, totalGearRatio, clutchEngagement, dt);
        }
    }

    void UpdateICEngine(float throttle, float vehicleSpeedMS, float totalGearRatio, float clutchEngagement, float dt)
    {
        // Calculate what RPM the engine SHOULD be at based on wheel speed
        float wheelDerivedRPM = CalculateRPMFromSpeed(vehicleSpeedMS, totalGearRatio);
        
        // Minimum RPM is idle
        float minRPM = idleRPM;
        
        if (clutchEngagement > 0.5f)
        {
            // Clutch engaged: RPM is locked to wheel speed
            targetRPM = Mathf.Max(wheelDerivedRPM, minRPM);
        }
        else
        {
            // Clutch slipping/disengaged: can rev freely
            float freeRevTarget = Mathf.Lerp(minRPM, maxRPM * 0.9f, throttle);
            targetRPM = Mathf.Max(wheelDerivedRPM * clutchEngagement, freeRevTarget);
        }

        // Rev limiter
        targetRPM = Mathf.Min(targetRPM, maxRPM);

        // Engine inertia: RPM doesn't change instantly
        float rpmChangeRate = (maxRPM - idleRPM) / engineInertia;
        currentRPM = Mathf.MoveTowards(currentRPM, targetRPM, rpmChangeRate * dt);
        currentRPM = Mathf.Clamp(currentRPM, minRPM, maxRPM);

        // Calculate output torque
        outputTorque = GetTorque(throttle);
    }

    void UpdateElectricMotor(float throttle, float vehicleSpeedMS, float totalGearRatio, float dt)
    {
        // EVs: RPM is directly tied to wheel speed (single gear, no clutch)
        float wheelDerivedRPM = CalculateRPMFromSpeed(vehicleSpeedMS, totalGearRatio);
        
        // EV can "spin" motor when stationary with throttle
        if (vehicleSpeedMS < 1f && throttle > 0.1f)
        {
            targetRPM = maxRPM * throttle * 0.3f;
        }
        else
        {
            targetRPM = wheelDerivedRPM;
        }

        targetRPM = Mathf.Min(targetRPM, maxRPM);

        // EVs have very fast response
        float rpmChangeRate = maxRPM * 5f;
        currentRPM = Mathf.MoveTowards(currentRPM, targetRPM, rpmChangeRate * dt);
        currentRPM = Mathf.Clamp(currentRPM, 0f, maxRPM);

        outputTorque = GetTorque(throttle);
    }

    /// <summary>
    /// Get torque output at current RPM and throttle.
    /// </summary>
    public float GetTorque(float throttle)
    {
        // Rev limiter cuts power
        if (currentRPM >= maxRPM - 100f)
        {
            return 0f;
        }

        float normalizedRPM = currentRPM / maxRPM;
        float curveFactor;

        if (engineType == EngineType.Electric)
        {
            curveFactor = evTorqueCurve.Evaluate(normalizedRPM);
        }
        else
        {
            curveFactor = torqueCurve.Evaluate(normalizedRPM);
        }

        return peakTorqueNm * throttle * curveFactor * drivetrainEfficiency;
    }

    /// <summary>
    /// Calculate wheel torque from engine torque.
    /// WheelTorque = EngineTorque × GearRatio × FinalDrive
    /// </summary>
    public float GetWheelTorque(float throttle, float totalGearRatio)
    {
        return GetTorque(throttle) * Mathf.Abs(totalGearRatio);
    }

    /// <summary>
    /// Calculate current power output in kW.
    /// Power = Torque × RPM / 9549
    /// </summary>
    public float GetCurrentPowerKW()
    {
        return (outputTorque * currentRPM) / 9549f;
    }

    /// <summary>
    /// Get current power in HP.
    /// </summary>
    public float GetCurrentPowerHP()
    {
        return GetCurrentPowerKW() * 1.341f;
    }
}