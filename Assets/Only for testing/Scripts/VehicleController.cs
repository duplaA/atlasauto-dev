using UnityEngine;
using UnityEngine.InputSystem;

// Wheel Speed -> Engine RPM -> Engine Torque -> Transmission -> Wheel Force.
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Components")]
    public VehicleEngine engine;
    public VehicleTransmission transmission;

    [Header("Powertrain")]
    [Tooltip("Master EV toggle. Syncs engine type and transmission behavior.")]
    public bool isElectricVehicle = false;
    
    public enum DrivetrainType { RWD, FWD, AWD }
    [Tooltip("Which wheels receive power: RWD (rear), FWD (front), AWD (all).")]
    public DrivetrainType drivetrain = DrivetrainType.RWD;
    
    [Tooltip("Maximum speed the engine can push the car (km/h). Can be exceeded going downhill.")]
    public float topSpeedKMH = 250f;
    
    [Tooltip("Override wheel radius for physics calculations (meters). Use if your model has oversized wheels. 0 = use actual WheelCollider radius.")]
    public float physicsWheelRadius = 0.34f;

    [Header("Handling")]
    public float vehicleMass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -1.5f, 0.1f); 
    public float maxBrakeTorque = 5000f; 
    [Range(0f, 1f)] public float frontBrakeBias = 0.7f;
    [Tooltip("Multiplier for sideways friction. Higher = more grip in corners.")]
    public float corneringStiffness = 3.0f; 
    [Tooltip("Artificial gravity to keep car glued to road. Multiplies by speed.")]
    public float downforceFactor = 5.0f;
    
    [Header("Steering")]
    public float maxSteerAngle = 40f; 
    public bool speedSensitiveSteering = true;

    [Header("Debug")]
    public float speedKMH;
    public float speedMS;
    public float engineTorque;
    public float driveTorque;
    
    // Physics causality debug
    [Header("Physics Validation")]
    public float expectedSpeedKMH;  
    public float slipRatio;         
    public float debugWheelRadius;  
    private bool isBraking;
    
    // Private
    private Vector2 moveInput;
    private float lastInputY = 0f;
    private Rigidbody rb;
    private VehicleWheel[] wheels;
    private PlayerInput playerInput;
    
    // Gear Logic
    private bool isInputReleaseRequired = false; // For stop-and-switch logic

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = vehicleMass;
        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        DiscoverWheels();
        AutoLinkComponents();
        CheckPlayerInput();
    }

    void AutoLinkComponents()
    {
        if (engine == null) engine = GetComponent<VehicleEngine>();
        if (engine == null) engine = gameObject.AddComponent<VehicleEngine>();

        if (transmission == null) transmission = GetComponent<VehicleTransmission>();
        if (transmission == null) transmission = gameObject.AddComponent<VehicleTransmission>();

        SyncPowertrainSettings();
    }

    void SyncPowertrainSettings()
    {
        if (engine != null)
        {
            engine.engineType = isElectricVehicle
                ? VehicleEngine.EngineType.Electric
                : VehicleEngine.EngineType.InternalCombustion;
        }
        if (transmission != null)
        {
            transmission.isElectric = isElectricVehicle;
        }
    }

    void OnValidate()
    {
        SyncPowertrainSettings();
        
        if (wheels != null && wheels.Length > 0)
        {
            ApplyDrivetrainConfig();
        }
    }

    void CheckPlayerInput()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) Debug.LogError("[VehicleController] MISSING PlayerInput component!");
    }

    void DiscoverWheels()
    {
        VehicleWheel[] allWheels = GetComponentsInChildren<VehicleWheel>();
        
        System.Collections.Generic.List<VehicleWheel> uniqueWheelList = new System.Collections.Generic.List<VehicleWheel>();
        System.Collections.Generic.HashSet<WheelCollider> seenColliders = new System.Collections.Generic.HashSet<WheelCollider>();
        
        Debug.Log($"[VehicleController] Discovered {allWheels.Length} VehicleWheel components...");
        
        foreach (var w in allWheels)
        {
            if (w.wheelCollider == null)
            {
                Debug.LogWarning($"[VehicleController] Skipping {w.gameObject.name} - no WheelCollider assigned");
                continue;
            }
            
            if (seenColliders.Contains(w.wheelCollider))
            {
                Debug.LogWarning($"[VehicleController] Skipping duplicate: {w.gameObject.name} (shares WheelCollider with another VehicleWheel)");
                continue;
            }
            
            seenColliders.Add(w.wheelCollider);
            uniqueWheelList.Add(w);
        }
        
        wheels = uniqueWheelList.ToArray();
        
        Debug.Log($"[VehicleController] Using {wheels.Length} unique wheels:");
        foreach (var w in wheels)
        {
            if (w.isFront && !w.isSteer)
            {
                w.isSteer = true;
                Debug.Log($"  ✓ {w.gameObject.name} | Radius: {w.wheelCollider.radius:F3}m | isFront: {w.isFront} | [Auto-set isSteer=true]");
            }
            else
            {
                Debug.Log($"  ✓ {w.gameObject.name} | Radius: {w.wheelCollider.radius:F3}m | isFront: {w.isFront} | isSteer: {w.isSteer}");
            }
        }
        
        if (wheels.Length != 4)
        {
            Debug.LogError($"[VehicleController] WARNING: Expected 4 wheels, have {wheels.Length}. Vehicle may not drive correctly!");
        }
        
        if (wheels.Length > 0 && wheels[0].wheelCollider != null)
        {
            debugWheelRadius = wheels[0].wheelCollider.radius;
            float speedAt1000RPM = (1000f * 2f * Mathf.PI * debugWheelRadius / 60f) * 3.6f;
            Debug.Log($"[VehicleController] Wheel radius: {debugWheelRadius:F3}m | At 1000 RPM = {speedAt1000RPM:F1} km/h");
        }
        
        ApplyDrivetrainConfig();
    }
    
    void ApplyDrivetrainConfig()
    {
        if (wheels == null) return;
        
        foreach (var w in wheels)
        {
            switch (drivetrain)
            {
                case DrivetrainType.RWD:
                    w.isMotor = !w.isFront;
                    break;
                case DrivetrainType.FWD:
                    w.isMotor = w.isFront;
                    break;
                case DrivetrainType.AWD:
                    w.isMotor = true;
                    break;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void LateUpdate()
    {
        if (wheels != null)
        {
            float steerAngle = CalculateSteerAngle(moveInput.x);
            float visualCalcRadius = physicsWheelRadius > 0.01f ? physicsWheelRadius : 0.34f;

            foreach (var w in wheels) 
            {
               float wheelSteer = 0f;
               if (w.isSteer) wheelSteer = steerAngle;
               
               w.UpdateVisuals(wheelSteer, speedMS, visualCalcRadius);
            }
        }
    }

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0) return;
        float dt = Time.fixedDeltaTime;

        speedMS = rb.linearVelocity.magnitude;
        speedKMH = speedMS * 3.6f;
        
        if (speedKMH > topSpeedKMH)
        {
            float maxMS = topSpeedKMH / 3.6f;
            rb.linearVelocity = rb.linearVelocity.normalized * maxMS;
            speedMS = maxMS;
            speedKMH = topSpeedKMH;
        }

        float localVelZ = transform.InverseTransformDirection(rb.linearVelocity).z;

        float inputThrottle = 0f;
        float inputBrake = 0f;
        float rawY = moveInput.y;
        
        bool isFreshInput = Mathf.Abs(rawY) > 0.05f && Mathf.Abs(lastInputY) < 0.05f;
        
        if (isInputReleaseRequired)
        {
            if (Mathf.Abs(rawY) < 0.1f)
            {
                isInputReleaseRequired = false;
            }
        }
        else
        {
            if (transmission.currentGear == 0)
            {
                if (rawY > 0.1f) { transmission.SetDrive(); inputThrottle = rawY; }
                else if (rawY < -0.1f) { transmission.SetReverse(); inputThrottle = Mathf.Abs(rawY); }
            }
            else if (transmission.currentGear > 0)
            {
                if (rawY > 0) inputThrottle = rawY; // Accelerate
                else if (rawY < 0) // Brake/Reverse?
                {
                    if (speedKMH > 1.0f) 
                    {
                        inputBrake = Mathf.Abs(rawY); // Standard braking
                    }
                    else 
                    {
                        if (isFreshInput)
                        {
                            transmission.SetReverse();
                            // Do NOT throttle immediately to be safe? Or valid?
                            inputThrottle = Mathf.Abs(rawY);
                        }
                        else
                        {
                            // Continuous Hold -> Lock
                            isInputReleaseRequired = true;
                        }
                    }
                }
            }
            else if (transmission.currentGear == -1)
            {
                if (rawY < 0) inputThrottle = Mathf.Abs(rawY);
                else if (rawY > 0) // Brake/Forward?
                {
                    if (speedKMH > 1.0f) 
                    {
                        inputBrake = Mathf.Abs(rawY);
                    }
                    else 
                    {
                        if (isFreshInput)
                        {
                            transmission.SetDrive();
                            inputThrottle = rawY;
                        }
                        else
                        {
                            // Continuous Hold -> Lock
                            isInputReleaseRequired = true;
                        }
                    }
                }
            }
        }
        
        lastInputY = rawY;
        
        if (isInputReleaseRequired)
        {
            inputThrottle = 0f;
            inputBrake = 1f; // Max brake
            if (speedKMH < 0.5f) 
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        transmission.UpdateGearLogic(engine.currentRPM, engine.maxRPM, inputThrottle, dt);

        if (isGroundedAny())
        {
            float downforce = downforceFactor * vehicleMass * (speedKMH / 100f); 
            rb.AddForce(-transform.up * downforce, ForceMode.Force);
        }
        
        float drivenWheelRPM = GetAverageDrivenRPM();
        float totalRatio = transmission.GetTotalRatio();
        
        // Determine if drivetrain is connected
        bool inNeutral = transmission.currentGear == 0;
        bool clutchDisengaged = transmission.clutchEngagement < 0.5f;
        bool isConnected = !inNeutral && !clutchDisengaged && Mathf.Abs(totalRatio) > 0.01f;
        
        if (isConnected)
        {
            
            // Calculate what wheel RPM SHOULD be for current vehicle speed
            float normalizedWheelRadius = 0.34f; // Standard car wheel
            float normalizedWheelRPM = (speedMS * 60f) / (2f * Mathf.PI * normalizedWheelRadius);
            
            // Apply gear ratio to get engine RPM
            float speedDerivedEngineRPM = normalizedWheelRPM * Mathf.Abs(totalRatio);
            
            // Smooth transition to target RPM
            float targetRPM = Mathf.Max(speedDerivedEngineRPM, isElectricVehicle ? 0f : engine.idleRPM);
            engine.currentRPM = Mathf.Lerp(engine.currentRPM, targetRPM, 25f * dt);
        }
        else
        {
            // FREE REVVING (Neutral or Clutch Out)
            float freeTorque = engine.CalculateTorque(engine.currentRPM, inputThrottle);
            float alpha = freeTorque / Mathf.Max(engine.inertia, 0.1f);
            engine.currentRPM += alpha * dt;
            
            // Decay to idle if no throttle
            if (inputThrottle < 0.05f)
            {
                float targetIdle = isElectricVehicle ? 0f : engine.idleRPM;
                engine.currentRPM = Mathf.Lerp(engine.currentRPM, targetIdle, 3f * dt);
            }
        }
        
        // Clamp RPM
        float minRPM = isElectricVehicle ? 0f : engine.idleRPM;
        engine.currentRPM = Mathf.Clamp(engine.currentRPM, minRPM, engine.maxRPM);


        // B. Engine RPM + Throttle -> Engine Torque
        engineTorque = engine.CalculateTorque(engine.currentRPM, inputThrottle);
        engine.currentLoad = inputThrottle; // Simplified load for now


        // C. Transmission -> Drive Torque
        driveTorque = transmission.GetDriveTorque(engineTorque);


        // D. Power Limiting (Critical!)
        // WheelForce = min(Torque/Radius, Power/Speed)
        float targetPhysicsRadius = physicsWheelRadius > 0.01f ? physicsWheelRadius : 0.34f;
        
        // Calculate scale multiplier: Actual / Target
        float actualRadius = (wheels.Length > 0 && wheels[0].wheelCollider != null) ? wheels[0].wheelCollider.radius : 0.34f;
        float scaleMultiplier = actualRadius / targetPhysicsRadius;
        
        float torqueBasedMaxForce = driveTorque / targetPhysicsRadius;
        
        // Power Limit: F = P / v
        float powerLimitedMaxForce = (engine.maxPowerKW * 1000f) / Mathf.Max(speedMS, 1f);
        
        // The FINAL force we are allowed to apply
        float effectiveForce = Mathf.Min(torqueBasedMaxForce, powerLimitedMaxForce);
        
        // Convert back to Torque for WheelCollider
        // We want effectiveForce at the contact point.
        // Torque needed = Force * ActualRadius (because Unity uses ActualRadius)
        float finalWheelTorque = effectiveForce * actualRadius; 

        int driveCount = GetMotorWheelCount();
        
        // If brakes are applied, cut drive torque
        bool isBraking = inputBrake > 0.1f;
        float torquePerWheel = 0f;
        
        if (!isBraking && driveCount > 0)
        {
            float speedRatio = speedKMH / topSpeedKMH;
            float topSpeedMultiplier = 1f;
            
            if (speedRatio > 0.8f)
            {
                topSpeedMultiplier = Mathf.Clamp01(1f - ((speedRatio - 0.8f) / 0.2f));
            }
            
            // Hard Cutoff if exceeding top speed
            if (speedKMH > topSpeedKMH) topSpeedMultiplier = 0f;
            
            // Distribute torque
            torquePerWheel = (finalWheelTorque / driveCount) * topSpeedMultiplier;
        }

        // Apply to wheels
        float steerAngle = CalculateSteerAngle(moveInput.x);

        float totalSlip = 0f;
        int slipCount = 0;
        
        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;
            WheelFrictionCurve sideFriction = w.wheelCollider.sidewaysFriction;
            sideFriction.stiffness = corneringStiffness;
            w.wheelCollider.sidewaysFriction = sideFriction;
            
            if (w.isSteer)
            {
                w.wheelCollider.steerAngle = steerAngle;
            }
            
            // Get wheel contact info
            WheelHit hit;
            bool isGrounded = w.wheelCollider.GetGroundHit(out hit);
            
            if (w.isMotor && !isBraking)
            {
                w.wheelCollider.motorTorque = torquePerWheel;
            }
            else
            {
                w.wheelCollider.motorTorque = 0f;
            }
            
            // If we are strictly waiting for release, we are braking
            // If no input and stopped, we apply PARK BRAKE
            if (isBraking)
            {
                float brakeTorque;
                if (w.isFront)
                    brakeTorque = inputBrake * maxBrakeTorque * frontBrakeBias * scaleMultiplier;
                else
                    brakeTorque = inputBrake * maxBrakeTorque * (1f - frontBrakeBias) * scaleMultiplier;
                
                w.wheelCollider.brakeTorque = brakeTorque;
            }
            else if (Mathf.Abs(moveInput.y) < 0.1f && speedKMH < 2f && !isInputReleaseRequired)
            {
                // Auto-Park: Aggressive hold
                // Also kill RB angular velocity to stop
                w.wheelCollider.brakeTorque = maxBrakeTorque * scaleMultiplier; 
                rb.angularDamping = 2.0f; 
            }
            else
            {
                w.wheelCollider.brakeTorque = 0f;
                rb.angularDamping = 0.05f; // Return to normal drag
            }
            
            // Calculate slip for grounded wheels
            if (isGrounded)
            {
                Vector3 velocityAtWheel = rb.GetPointVelocity(hit.point);
                
                float arcadeRPM = (velocityAtWheel.magnitude * 60f) / (2f * Mathf.PI * targetPhysicsRadius);
                
                // "perfect" RPM for slip calculation
                float wheelSpeed = arcadeRPM * 2f * Mathf.PI * targetPhysicsRadius / 60f;
                // Note: wheelSpeed == velocityAtWheel.magnitude technically
                
                float groundSpeed = Vector3.Dot(velocityAtWheel, w.wheelCollider.transform.forward);
                float slipDenominator = Mathf.Max(Mathf.Abs(wheelSpeed), Mathf.Abs(groundSpeed), 0.1f);
                float wheelSlip = (wheelSpeed - groundSpeed) / slipDenominator;
                totalSlip += Mathf.Abs(wheelSlip);
                slipCount++;
            }
        }
        
        this.isBraking = isBraking; 
        
        // Force expected speed to be actual speed
        expectedSpeedKMH = speedKMH;
        float expectedSpeedMS = speedMS;
        
        // Fix slip ratio display
        if (slipCount == 0) slipRatio = 0f;
        else slipRatio = totalSlip / slipCount;
    }

    float GetAverageDrivenRPM()
    {
        float total = 0;
        int count = 0;
        foreach(var w in wheels)
        {
            if (w.isMotor)
            {
                total += w.wheelCollider.rpm;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    int GetMotorWheelCount()
    {
        int c = 0;
        foreach (var w in wheels) if (w.isMotor) c++;
        return c;
    }
    
    bool isGroundedAny()
    {
        if (wheels == null) return false;
        foreach (var w in wheels) if (w.IsGrounded()) return true;
        return false;
    }

    float CalculateSteerAngle(float input)
    {
        if (!speedSensitiveSteering) return input * maxSteerAngle;
        float speedFactor = Mathf.InverseLerp(10f, 120f, speedKMH);
        // less steering reduction at speed
        return input * Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.7f, speedFactor);
    }
    
    void OnGUI()
    {
        if (engine == null || transmission == null) return;
        
        // Count grounded wheels
        int groundedCount = 0;
        float avgWheelRPM = 0f;
        int motorCount = 0;
        foreach (var w in wheels)
        {
            if (w.IsGrounded()) groundedCount++;
            if (w.isMotor)
            {
                avgWheelRPM += Mathf.Abs(w.wheelCollider.rpm);
                motorCount++;
            }
        }
        if (motorCount > 0) avgWheelRPM /= motorCount;
        
        // Main debug box
        GUI.Box(new Rect(10, 10, 380, 280), "PHYSICS DEBUG");
        
        // Speed and drivetrain
        GUI.Label(new Rect(20, 35, 360, 20), $"Actual Speed: {speedKMH:F1} km/h | {drivetrain}");
        
        // PHYSICS VALIDATION: Expected vs Actual speed
        bool speedMismatch = Mathf.Abs(expectedSpeedKMH - speedKMH) > 5f && speedKMH > 5f;
        if (speedMismatch) GUI.color = Color.red;
        GUI.Label(new Rect(20, 55, 360, 20), $"Wheel-Derived Speed: {expectedSpeedKMH:F1} km/h | Slip: {slipRatio * 100f:F0}%");
        if (speedMismatch) GUI.color = Color.white;
        
        // RPM
        GUI.Label(new Rect(20, 75, 360, 20), $"Engine RPM: {engine.currentRPM:F0} | Wheel RPM: {avgWheelRPM:F0}");
        GUI.Label(new Rect(20, 95, 360, 20), $"Gear: {transmission.GetGearDisplayString()} | Grounded: {groundedCount}/{wheels.Length}");
        
        // Torque
        GUI.Label(new Rect(20, 120, 360, 20), $"Eng Torque: {engineTorque:F0} Nm | Drive: {driveTorque:F0} Nm");
        
        // Power
        float currentPower = engine.GetCurrentPowerKW(engineTorque, engine.currentRPM);
        float currentHP = currentPower * 1.341f;
        GUI.Label(new Rect(20, 140, 360, 20), $"Power: {currentHP:F0} / {engine.horsepowerHP:F0} HP");
        GUI.Label(new Rect(20, 160, 360, 20), $"Weight: {vehicleMass:F0} kg | Wheel Radius: {debugWheelRadius:F3}m");
        
        // Status indicators
        string status = "";
        if (transmission.clutchEngagement < 0.9f) status += "[CLUTCH] ";
        if (groundedCount == 0) status += "[AIRBORNE] ";
        if (transmission.currentGear == 0) status += "[NEUTRAL] ";
        if (speedMismatch) status += "[SLIP!] ";
        
        // BRAKE INDICATOR
        if (isBraking)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(20, 185, 360, 20), ">>> BRAKING <<<");
            GUI.color = Color.white;
        }
        else if (!string.IsNullOrEmpty(status))
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(20, 185, 360, 20), status);
            GUI.color = Color.white;
        }
        
        // Causality violation warning
        if (speedMismatch && speedKMH > 10f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(20, 210, 360, 40), $"⚠ CAUSALITY VIOLATION\nSpeed {speedKMH:F0} km/h but wheels show {expectedSpeedKMH:F0} km/h");
            GUI.color = Color.white;
        }
    }
}