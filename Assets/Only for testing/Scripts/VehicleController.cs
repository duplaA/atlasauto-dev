using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main vehicle controller using realistic engine physics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Setup")]
    public float vehicleMass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.4f, 0.1f);

    [Header("Drive Mode")]
    public bool isElectricVehicle = false;

    [Header("Engine (synced to VehicleEngine)")]
    [Tooltip("Peak torque in Nm")]
    public float torqueNm = 450f;
    [Tooltip("Maximum engine RPM")]
    public float maxRPM = 7000f;
    [Tooltip("Top speed limiter km/h (0 = no limit)")]
    public float topSpeedKMH = 250f;

    [Header("Braking")]
    public float maxBrakeTorque = 5000f;

    [Header("Steering")]
    [Range(20f, 45f)]
    public float maxSteerAngle = 35f;
    public bool speedSensitiveSteering = true;

    [Header("Tire Friction")]
    [Range(0.5f, 3f)]
    public float forwardStiffness = 1.2f;
    [Range(0.5f, 3f)]
    public float sidewaysStiffness = 1.5f;

    [Header("Components")]
    public VehicleEngine engine;
    public VehicleTransmission transmission;

    [Header("Debug")]
    public float speedKMH;
    public float speedMS;
    public float wheelTorqueApplied;

    // Private
    private Vector2 moveInput;
    private Rigidbody rb;
    private VehicleWheel[] wheels;
    private PlayerInput playerInput;

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

    void CheckPlayerInput()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[VehicleController] MISSING PlayerInput component!");
        }
    }

    void AutoLinkComponents()
    {
        if (engine == null) engine = GetComponent<VehicleEngine>();
        if (engine == null) engine = gameObject.AddComponent<VehicleEngine>();

        if (transmission == null) transmission = GetComponent<VehicleTransmission>();
        if (transmission == null) transmission = gameObject.AddComponent<VehicleTransmission>();

        SyncSettings();
    }

    void SyncSettings()
    {
        if (engine != null)
        {
            engine.engineType = isElectricVehicle ? VehicleEngine.EngineType.Electric : VehicleEngine.EngineType.InternalCombustion;
            engine.peakTorqueNm = torqueNm;
            engine.maxRPM = maxRPM;
        }
        if (transmission != null)
        {
            transmission.isElectric = isElectricVehicle;
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void Update()
    {
        if (wheels != null)
        {
            foreach (var w in wheels) w.SyncVisuals();
        }
        SyncSettings();
    }

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0) return;

        float dt = Time.fixedDeltaTime;
        
        // Calculate speed
        speedMS = rb.linearVelocity.magnitude;
        speedKMH = speedMS * 3.6f;
        
        float localVelZ = transform.InverseTransformDirection(rb.linearVelocity).z;
        float inputY = moveInput.y;

        // --- BRAKE & DIRECTION ---
        float brakeInput = 0f;
        if (localVelZ > 0.5f && inputY < -0.1f) brakeInput = Mathf.Abs(inputY);
        else if (localVelZ < -0.5f && inputY > 0.1f) brakeInput = Mathf.Abs(inputY);

        // Auto gear selection when stopped
        if (speedKMH < 2f && brakeInput < 0.1f)
        {
            if (inputY > 0.1f) transmission.SetDriveMode(1);
            else if (inputY < -0.1f) transmission.SetDriveMode(-1);
            else transmission.SetDriveMode(0);
        }

        float throttle = (brakeInput > 0.1f) ? 0f : Mathf.Abs(inputY);

        // Top speed limiter
        if (topSpeedKMH > 0 && speedKMH >= topSpeedKMH)
        {
            throttle *= Mathf.Clamp01(1f - (speedKMH - topSpeedKMH) / 10f);
        }

        // --- ENGINE & TRANSMISSION UPDATE ---
        float totalRatio = transmission.GetTotalRatio();
        
        // Update engine with vehicle speed (proper physics-based RPM calculation)
        engine.UpdateEngine(throttle, speedMS, totalRatio, transmission.clutchPosition, dt);
        
        // Update transmission
        transmission.UpdateTransmission(speedKMH, engine.currentRPM, engine.maxRPM, throttle, dt);

        // --- TORQUE TO WHEELS ---
        // WheelTorque = EngineTorque × GearRatio × FinalDrive
        float wheelTorque = engine.GetWheelTorque(throttle, totalRatio);
        
        // Divide among driven wheels
        int motorCount = GetMotorWheelCount();
        float torquePerWheel = wheelTorque / Mathf.Max(1, motorCount);
        
        // No torque during shifting
        if (transmission.IsShifting())
        {
            torquePerWheel = 0f;
        }

        wheelTorqueApplied = torquePerWheel; // Debug

        // --- APPLY TO WHEELS ---
        float steerAngle = CalculateSteerAngle(moveInput.x);

        foreach (var w in wheels)
        {
            UpdateWheelFriction(w.wheelCollider);

            if (w.isMotor)
            {
                float appliedTorque = brakeInput > 0.1f ? 0f : torquePerWheel;
                // Reverse direction for reverse gear
                if (transmission.currentGear == -1)
                {
                    appliedTorque = -Mathf.Abs(appliedTorque);
                }
                w.ApplyTorque(appliedTorque);
            }

            w.ApplyBrake(brakeInput * maxBrakeTorque);

            if (w.isSteer)
            {
                w.ApplySteer(steerAngle);
            }
        }

        // Minimal drag
        rb.linearDamping = 0.01f;
    }

    float CalculateSteerAngle(float input)
    {
        if (!speedSensitiveSteering) return input * maxSteerAngle;
        float speedFactor = Mathf.InverseLerp(0f, 120f, speedKMH);
        float reducedAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.3f, speedFactor);
        return input * reducedAngle;
    }

    int GetMotorWheelCount()
    {
        int count = 0;
        foreach (var w in wheels) if (w.isMotor) count++;
        return count;
    }

    void UpdateWheelFriction(WheelCollider wc)
    {
        WheelFrictionCurve fwd = wc.forwardFriction;
        fwd.stiffness = forwardStiffness;
        wc.forwardFriction = fwd;

        WheelFrictionCurve side = wc.sidewaysFriction;
        side.stiffness = sidewaysStiffness;
        wc.sidewaysFriction = side;
    }

    void DiscoverWheels()
    {
        wheels = GetComponentsInChildren<VehicleWheel>();
        if (wheels.Length == 0)
        {
            Debug.LogWarning("[VehicleController] No VehicleWheel components found.");
        }
    }

    void OnGUI()
    {
        if (playerInput == null)
        {
            GUI.color = Color.red;
            GUI.Box(new Rect(Screen.width / 2 - 180, 50, 360, 50), "");
            GUI.Label(new Rect(Screen.width / 2 - 170, 60, 340, 30), "MISSING PlayerInput component!");
            GUI.color = Color.white;
        }

        string typeLabel = isElectricVehicle ? "EV" : "ICE";
        GUI.Box(new Rect(10, 10, 280, 160), "VEHICLE TELEMETRY");
        GUI.Label(new Rect(20, 35, 260, 20), $"Speed: {speedKMH:F1} km/h");
        GUI.Label(new Rect(20, 55, 260, 20), $"RPM: {engine.currentRPM:F0} / {engine.maxRPM:F0}");
        GUI.Label(new Rect(20, 75, 260, 20), $"Gear: {transmission.GetGearDisplayString()} | {typeLabel}");
        GUI.Label(new Rect(20, 95, 260, 20), $"Torque: {engine.outputTorque:F0} Nm");
        GUI.Label(new Rect(20, 115, 260, 20), $"Power: {engine.GetCurrentPowerHP():F0} HP");
        GUI.Label(new Rect(20, 135, 260, 20), $"Wheel Torque: {wheelTorqueApplied:F0} Nm");
    }
}