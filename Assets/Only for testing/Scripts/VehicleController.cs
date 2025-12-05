using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    // --- VEHICLE PRESET SYSTEM ---
    public enum VehiclePreset
    {
        HeavyVan,
        DeliveryTruck,
        FamilySedan,
        SportSedan,
        Sportscar,
        RaceCar,
        Custom
    }

    [Header("Vehicle Type")]
    [Tooltip("Select a preset or use Custom to manually tune parameters")]
    public VehiclePreset vehiclePreset = VehiclePreset.HeavyVan;

    // --- CONFIGURATION ---
    [Header("Drop your wheels here:")]
    public WheelCollider WheelCollider_FL;
    public Transform wheelModel_FL;
    public bool isSteer_FL;
    public bool isMotor_FL;

    public WheelCollider WheelCollider_FR;
    public Transform wheelModel_FR;
    public bool isSteer_FR;
    public bool isMotor_FR;

    public WheelCollider WheelCollider_RL;
    public Transform wheelModel_RL;
    public bool isSteer_RL;
    public bool isMotor_RL;

    public WheelCollider WheelCollider_RR;
    public Transform wheelModel_RR;
    public bool isSteer_RR;
    public bool isMotor_RR;

    Vector3 position;
    Quaternion rotation;

    [Header("Car Properties (Auto-configured by preset)")]
    public float mass = 2100f;
    public float motorTorque = 2500f;
    public float brakeTorque = 3500f;
    public float maxSpeed = 20f;
    public float steeringRange = 30f;
    public float steeringRangeAtMaxSpeed = 10f;
    public float centreOfGravityOffset = -0.5f;

    [Header("Suspension Settings")]
    public float suspensionDistance = 0.2f;

    [Header("Traction Control (Auto-configured)")]
    public bool enableTC = true;
    public float slipThreshold = 0.15f;
    public float tcAgression = 0.8f;

    // --- INTERNAL STATE ---
    private float targetMu;
    private float loadSensitivity;
    private float combinedSlipAlpha;
    private float staticCamber;

    private float baseSideExtremumSlip;
    private float baseSideExtremumValue;
    private float baseSideAsymptoteSlip;
    private float baseSideAsymptoteValue;

    private float baseFwdExtremumSlip;
    private float baseFwdExtremumValue;
    private float baseFwdAsymptoteSlip;
    private float baseFwdAsymptoteValue;

    private float baseStiffness;
    private float torqueLimitSafetyFactor;
    private float gripMultiplier;

    public Vector2 cachedInput { get; private set; }
    private Rigidbody _rb;
    private VehicleControls carControls;

    private float mass_front_corner;
    private float mass_rear_corner;
    private float nominalFz;

    enum springFrequency { sport, comfort }
    enum dampRatio { sport, comfort }
    enum frontRearBias { forty_sixty, sixty_forty, fifty_fifty }

    [SerializeField] private springFrequency _springFrequency = springFrequency.comfort;
    [SerializeField] private dampRatio _dampRatio = dampRatio.comfort;
    [SerializeField] private frontRearBias _frontRearBias = frontRearBias.sixty_forty;

    private float k;
    private float c;

    private class WheelData
    {
        public WheelCollider Collider;
        public Transform Model;
        public bool IsSteer;
        public bool IsMotor;

        public float currentExtremumValue;
        public float currentStiffness;
        public float currentExtremumSlip;
    }

    private WheelData[] allWheels;

    // --- PRESET CONFIGURATIONS ---
    [System.Serializable]
    private class VehiclePresetConfig
    {
        public float mass;
        public float motorTorque;
        public float brakeTorque;
        public float maxSpeed;
        public float steeringRange;
        public float steeringRangeAtMaxSpeed;
        public float centreOfGravityOffset;
        public float suspensionDistance;

        // Friction parameters
        public float targetMu;
        public float loadSensitivity;
        public float combinedSlipAlpha;
        public float staticCamber;
        public float gripMultiplier;
        public float torqueLimitSafetyFactor;

        // Forward friction
        public float fwdExtremumSlip;
        public float fwdExtremumValue;
        public float fwdAsymptoteSlip;
        public float fwdAsymptoteValue;

        // Sideways friction
        public float sideExtremumSlip;
        public float sideExtremumValue;
        public float sideAsymptoteSlip;
        public float sideAsymptoteValue;

        public float baseStiffness;

        // Suspension
        public springFrequency springFreq;
        public dampRatio dampingRatio;
        public frontRearBias weightBias;

        // Traction control
        public bool enableTC;
        public float slipThreshold;
        public float tcAgression;
    }

    public float WheelRadiusFront => WheelCollider_FL != null ? WheelCollider_FL.radius : 0.35f;
    public float WheelRadiusRear => WheelCollider_RL != null ? WheelCollider_RL.radius : 0.35f;

    public float AverageWheelRadius =>
        (WheelRadiusFront + WheelRadiusRear) * 0.5f;

    public float MotorTorqueMax => motorTorque;
    public float BrakeTorqueMax => brakeTorque;
    public float MaxVehicleSpeed => maxSpeed;



    private VehiclePresetConfig GetPresetConfig(VehiclePreset preset)
    {
        switch (preset)
        {
            case VehiclePreset.HeavyVan:
                return new VehiclePresetConfig
                {
                    mass = 2100f,
                    motorTorque = 1800f,
                    brakeTorque = 4000f,
                    maxSpeed = 25f,
                    steeringRange = 35f,
                    steeringRangeAtMaxSpeed = 12f,
                    centreOfGravityOffset = -0.3f,
                    suspensionDistance = 0.25f,

                    targetMu = 1.0f,
                    loadSensitivity = 0.03f,
                    combinedSlipAlpha = 0.3f,
                    staticCamber = -0.5f,
                    gripMultiplier = 1.2f,
                    torqueLimitSafetyFactor = 0.75f,

                    fwdExtremumSlip = 0.4f,
                    fwdExtremumValue = 1.1f,
                    fwdAsymptoteSlip = 1.0f,
                    fwdAsymptoteValue = 0.7f,

                    sideExtremumSlip = 0.15f,
                    sideExtremumValue = 1.0f,
                    sideAsymptoteSlip = 0.4f,
                    sideAsymptoteValue = 0.65f,

                    baseStiffness = 1.1f,

                    springFreq = springFrequency.comfort,
                    dampingRatio = dampRatio.comfort,
                    weightBias = frontRearBias.sixty_forty,

                    enableTC = true,
                    slipThreshold = 0.25f,
                    tcAgression = 0.6f
                };

            case VehiclePreset.DeliveryTruck:
                return new VehiclePresetConfig
                {
                    mass = 2800f,
                    motorTorque = 2200f,
                    brakeTorque = 5000f,
                    maxSpeed = 22f,
                    steeringRange = 40f,
                    steeringRangeAtMaxSpeed = 15f,
                    centreOfGravityOffset = -0.2f,
                    suspensionDistance = 0.3f,

                    targetMu = 0.95f,
                    loadSensitivity = 0.04f,
                    combinedSlipAlpha = 0.35f,
                    staticCamber = -0.3f,
                    gripMultiplier = 1.3f,
                    torqueLimitSafetyFactor = 0.7f,

                    fwdExtremumSlip = 0.45f,
                    fwdExtremumValue = 1.05f,
                    fwdAsymptoteSlip = 1.1f,
                    fwdAsymptoteValue = 0.65f,

                    sideExtremumSlip = 0.18f,
                    sideExtremumValue = 0.95f,
                    sideAsymptoteSlip = 0.45f,
                    sideAsymptoteValue = 0.6f,

                    baseStiffness = 1.15f,

                    springFreq = springFrequency.comfort,
                    dampingRatio = dampRatio.comfort,
                    weightBias = frontRearBias.fifty_fifty,

                    enableTC = true,
                    slipThreshold = 0.3f,
                    tcAgression = 0.5f
                };

            case VehiclePreset.FamilySedan:
                return new VehiclePresetConfig
                {
                    mass = 1500f,
                    motorTorque = 1500f,
                    brakeTorque = 2500f,
                    maxSpeed = 35f,
                    steeringRange = 32f,
                    steeringRangeAtMaxSpeed = 8f,
                    centreOfGravityOffset = -0.4f,
                    suspensionDistance = 0.18f,

                    targetMu = 0.95f,
                    loadSensitivity = 0.06f,
                    combinedSlipAlpha = 0.4f,
                    staticCamber = -1.0f,
                    gripMultiplier = 1.0f,
                    torqueLimitSafetyFactor = 0.85f,

                    fwdExtremumSlip = 0.35f,
                    fwdExtremumValue = 1.05f,
                    fwdAsymptoteSlip = 0.9f,
                    fwdAsymptoteValue = 0.65f,

                    sideExtremumSlip = 0.12f,
                    sideExtremumValue = 1.0f,
                    sideAsymptoteSlip = 0.35f,
                    sideAsymptoteValue = 0.6f,

                    baseStiffness = 1.0f,

                    springFreq = springFrequency.comfort,
                    dampingRatio = dampRatio.comfort,
                    weightBias = frontRearBias.sixty_forty,

                    enableTC = true,
                    slipThreshold = 0.2f,
                    tcAgression = 0.7f
                };

            case VehiclePreset.SportSedan:
                return new VehiclePresetConfig
                {
                    mass = 1600f,
                    motorTorque = 2000f,
                    brakeTorque = 3000f,
                    maxSpeed = 45f,
                    steeringRange = 30f,
                    steeringRangeAtMaxSpeed = 6f,
                    centreOfGravityOffset = -0.5f,
                    suspensionDistance = 0.15f,

                    targetMu = 1.0f,
                    loadSensitivity = 0.07f,
                    combinedSlipAlpha = 0.45f,
                    staticCamber = -1.5f,
                    gripMultiplier = 1.05f,
                    torqueLimitSafetyFactor = 0.88f,

                    fwdExtremumSlip = 0.3f,
                    fwdExtremumValue = 1.1f,
                    fwdAsymptoteSlip = 0.8f,
                    fwdAsymptoteValue = 0.7f,

                    sideExtremumSlip = 0.1f,
                    sideExtremumValue = 1.05f,
                    sideAsymptoteSlip = 0.3f,
                    sideAsymptoteValue = 0.65f,

                    baseStiffness = 1.05f,

                    springFreq = springFrequency.sport,
                    dampingRatio = dampRatio.sport,
                    weightBias = frontRearBias.fifty_fifty,

                    enableTC = true,
                    slipThreshold = 0.18f,
                    tcAgression = 0.75f
                };

            case VehiclePreset.Sportscar:
                return new VehiclePresetConfig
                {
                    mass = 1400f,
                    motorTorque = 2500f,
                    brakeTorque = 3500f,
                    maxSpeed = 55f,
                    steeringRange = 28f,
                    steeringRangeAtMaxSpeed = 5f,
                    centreOfGravityOffset = -0.55f,
                    suspensionDistance = 0.12f,

                    targetMu = 1.05f,
                    loadSensitivity = 0.08f,
                    combinedSlipAlpha = 0.5f,
                    staticCamber = -2.0f,
                    gripMultiplier = 1.1f,
                    torqueLimitSafetyFactor = 0.9f,

                    fwdExtremumSlip = 0.25f,
                    fwdExtremumValue = 1.15f,
                    fwdAsymptoteSlip = 0.7f,
                    fwdAsymptoteValue = 0.75f,

                    sideExtremumSlip = 0.08f,
                    sideExtremumValue = 1.1f,
                    sideAsymptoteSlip = 0.25f,
                    sideAsymptoteValue = 0.7f,

                    baseStiffness = 1.1f,

                    springFreq = springFrequency.sport,
                    dampingRatio = dampRatio.sport,
                    weightBias = frontRearBias.forty_sixty,

                    enableTC = true,
                    slipThreshold = 0.15f,
                    tcAgression = 0.8f
                };

            case VehiclePreset.RaceCar:
                return new VehiclePresetConfig
                {
                    mass = 1200f,
                    motorTorque = 3000f,
                    brakeTorque = 4000f,
                    maxSpeed = 70f,
                    steeringRange = 25f,
                    steeringRangeAtMaxSpeed = 4f,
                    centreOfGravityOffset = -0.6f,
                    suspensionDistance = 0.1f,

                    targetMu = 1.15f,
                    loadSensitivity = 0.1f,
                    combinedSlipAlpha = 0.6f,
                    staticCamber = -2.5f,
                    gripMultiplier = 1.2f,
                    torqueLimitSafetyFactor = 0.92f,

                    fwdExtremumSlip = 0.2f,
                    fwdExtremumValue = 1.2f,
                    fwdAsymptoteSlip = 0.6f,
                    fwdAsymptoteValue = 0.8f,

                    sideExtremumSlip = 0.06f,
                    sideExtremumValue = 1.15f,
                    sideAsymptoteSlip = 0.2f,
                    sideAsymptoteValue = 0.75f,

                    baseStiffness = 1.2f,

                    springFreq = springFrequency.sport,
                    dampingRatio = dampRatio.sport,
                    weightBias = frontRearBias.forty_sixty,

                    enableTC = true,
                    slipThreshold = 0.12f,
                    tcAgression = 0.85f
                };

            default: // Custom
                return null;
        }
    }

    private void ApplyPreset()
    {
        if (vehiclePreset == VehiclePreset.Custom) return;

        var config = GetPresetConfig(vehiclePreset);
        if (config == null) return;

        // Apply all settings
        mass = config.mass;
        motorTorque = config.motorTorque;
        brakeTorque = config.brakeTorque;
        maxSpeed = config.maxSpeed;
        steeringRange = config.steeringRange;
        steeringRangeAtMaxSpeed = config.steeringRangeAtMaxSpeed;
        centreOfGravityOffset = config.centreOfGravityOffset;
        suspensionDistance = config.suspensionDistance;

        targetMu = config.targetMu;
        loadSensitivity = config.loadSensitivity;
        combinedSlipAlpha = config.combinedSlipAlpha;
        staticCamber = config.staticCamber;
        gripMultiplier = config.gripMultiplier;
        torqueLimitSafetyFactor = config.torqueLimitSafetyFactor;

        baseFwdExtremumSlip = config.fwdExtremumSlip;
        baseFwdExtremumValue = config.fwdExtremumValue;
        baseFwdAsymptoteSlip = config.fwdAsymptoteSlip;
        baseFwdAsymptoteValue = config.fwdAsymptoteValue;

        baseSideExtremumSlip = config.sideExtremumSlip;
        baseSideExtremumValue = config.sideExtremumValue;
        baseSideAsymptoteSlip = config.sideAsymptoteSlip;
        baseSideAsymptoteValue = config.sideAsymptoteValue;

        baseStiffness = config.baseStiffness;

        _springFrequency = config.springFreq;
        _dampRatio = config.dampingRatio;
        _frontRearBias = config.weightBias;

        enableTC = config.enableTC;
        slipThreshold = config.slipThreshold;
        tcAgression = config.tcAgression;

        Debug.Log($"Applied {vehiclePreset} preset configuration");
    }

    // --- INITIALIZATION ---
    void Awake()
    {
        ApplyPreset();

        _rb = GetComponent<Rigidbody>();

        carControls = new VehicleControls();
        carControls.Vehicle.Move.performed += ctx => cachedInput = ctx.ReadValue<Vector2>();
        carControls.Vehicle.Move.canceled += ctx => cachedInput = Vector2.zero;

        allWheels = new WheelData[]
        {
            new WheelData { Collider = WheelCollider_FL, Model = wheelModel_FL, IsSteer = isSteer_FL, IsMotor = isMotor_FL },
            new WheelData { Collider = WheelCollider_FR, Model = wheelModel_FR, IsSteer = isSteer_FR, IsMotor = isMotor_FR },
            new WheelData { Collider = WheelCollider_RL, Model = wheelModel_RL, IsSteer = isSteer_RL, IsMotor = isMotor_RL },
            new WheelData { Collider = WheelCollider_RR, Model = wheelModel_RR, IsSteer = isSteer_RR, IsMotor = isMotor_RR }
        };

        foreach (var w in allWheels)
        {
            w.currentExtremumValue = baseSideExtremumValue;
            w.currentStiffness = baseStiffness;
            w.currentExtremumSlip = baseSideExtremumSlip;
        }
    }

    void OnEnable()
    {
        if (carControls != null) carControls.Enable();
    }

    void OnDisable()
    {
        if (carControls != null) carControls.Disable();
    }

    void Start()
    {
        ConfigureRigidbody();
        SetupSuspension();
    }

    void ConfigureRigidbody()
    {
        if (_rb != null)
        {
            _rb.mass = mass;
            _rb.centerOfMass = new Vector3(0, centreOfGravityOffset, 0);
        }

        switch (_frontRearBias)
        {
            case frontRearBias.forty_sixty:
                mass_front_corner = mass * 0.4f / 2;
                mass_rear_corner = mass * 0.6f / 2;
                break;
            case frontRearBias.sixty_forty:
                mass_front_corner = mass * 0.6f / 2;
                mass_rear_corner = mass * 0.4f / 2;
                break;
            case frontRearBias.fifty_fifty:
                mass_front_corner = mass * 0.5f / 2;
                mass_rear_corner = mass * 0.5f / 2;
                break;
        }

        nominalFz = (mass / 4.0f) * Physics.gravity.magnitude;
    }

    void SetupSuspension()
    {
        WheelFrictionCurve sideFriction = new WheelFrictionCurve
        {
            extremumSlip = baseSideExtremumSlip,
            extremumValue = baseSideExtremumValue,
            asymptoteSlip = baseSideAsymptoteSlip,
            asymptoteValue = baseSideAsymptoteValue,
            stiffness = baseStiffness
        };

        WheelFrictionCurve fwdFriction = new WheelFrictionCurve
        {
            extremumSlip = baseFwdExtremumSlip,
            extremumValue = baseFwdExtremumValue,
            asymptoteSlip = baseFwdAsymptoteSlip,
            asymptoteValue = baseFwdAsymptoteValue,
            stiffness = baseStiffness
        };

        foreach (var wheel in allWheels)
        {
            if (wheel.Collider == null) continue;

            var currentSpring = wheel.Collider.suspensionSpring;

            float springMass = wheel.Model.name.ToLower().Contains("f") ? mass_front_corner : mass_rear_corner;
            float freq = _springFrequency == springFrequency.sport ?
                (wheel.Model.name.ToLower().Contains("f") ? 2.3f : 1.9f) :
                (wheel.Model.name.ToLower().Contains("f") ? 1.8f : 1.5f);

            k = springMass * Mathf.Pow(2 * Mathf.PI * freq, 2);

            float dampingRatio = _dampRatio == dampRatio.sport ? 0.25f : 0.15f;
            c = 2f * dampingRatio * Mathf.Sqrt(k * springMass);

            currentSpring.spring = k;
            currentSpring.damper = c;
            currentSpring.targetPosition = 0.5f;

            wheel.Collider.suspensionSpring = currentSpring;
            wheel.Collider.suspensionDistance = suspensionDistance;
            wheel.Collider.wheelDampingRate = 0.5f;
            wheel.Collider.forceAppPointDistance = 0f;

            wheel.Collider.forwardFriction = fwdFriction;
            wheel.Collider.sidewaysFriction = sideFriction;
        }
    }

    void SyncWheel(WheelCollider collider, Transform model)
    {
        collider.GetWorldPose(out position, out rotation);
        model.transform.position = position;
        model.transform.rotation = rotation;
    }

    // --- PHYSICS LOOP ---
    void Update()
    {
        foreach (var wheel in allWheels)
        {
            SyncWheel(wheel.Collider, wheel.Model);
        }
    }

    void FixedUpdate()
    {
        float vInput = cachedInput.y;
        float hInput = cachedInput.x;

        float forwardSpeed = transform.InverseTransformDirection(_rb.linearVelocity).z;
        float speedFactor = Mathf.InverseLerp(0, maxSpeed, Mathf.Abs(forwardSpeed));

        float currentMotorTorque = Mathf.Lerp(motorTorque, motorTorque * 0.5f, speedFactor);
        float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);

        foreach (var wheel in allWheels)
        {
            UpdateWheelFriction(wheel);
            ApplyWheelForces(wheel, hInput, vInput, currentSteerRange, currentMotorTorque, brakeTorque, forwardSpeed);
        }
    }

    // --- FRICTION DYNAMICS ---
    void UpdateWheelFriction(WheelData wheel)
    {
        WheelHit hit;
        if (!wheel.Collider.GetGroundHit(out hit)) return;

        float currentFz = hit.force;
        float loadFactor = 1.0f - loadSensitivity * ((currentFz / nominalFz) - 1.0f);
        loadFactor = Mathf.Clamp(loadFactor, 0.7f, 1.05f);

        float compression = 1.0f - ((hit.point - wheel.Collider.transform.position).magnitude / wheel.Collider.suspensionDistance);
        float dynamicCamber = staticCamber - (compression * 2.0f);
        float camberFactor = 1.0f + (Mathf.Abs(dynamicCamber) * 0.015f);
        camberFactor = Mathf.Clamp(camberFactor, 0.95f, 1.1f);

        float s_long = Mathf.Clamp(Mathf.Abs(hit.forwardSlip), 0f, 1.5f);
        float combinedFactor = Mathf.Max(0.6f, 1.0f - combinedSlipAlpha * s_long);

        float finalSideExtremum = baseSideExtremumValue * loadFactor * camberFactor * combinedFactor * gripMultiplier;
        float finalFwdExtremum = baseFwdExtremumValue * loadFactor * gripMultiplier;

        // Update Sideways
        WheelFrictionCurve sideCurve = wheel.Collider.sidewaysFriction;
        sideCurve.extremumSlip = baseSideExtremumSlip;
        sideCurve.extremumValue = finalSideExtremum;
        sideCurve.stiffness = baseStiffness;
        sideCurve.asymptoteValue = finalSideExtremum * 0.65f;
        sideCurve.asymptoteSlip = baseSideExtremumSlip * 2.8f;
        wheel.Collider.sidewaysFriction = sideCurve;

        // Update Forward
        WheelFrictionCurve fwdCurve = wheel.Collider.forwardFriction;
        fwdCurve.extremumSlip = baseFwdExtremumSlip;
        fwdCurve.extremumValue = finalFwdExtremum;
        fwdCurve.asymptoteSlip = baseFwdAsymptoteSlip;
        fwdCurve.asymptoteValue = finalFwdExtremum * 0.65f;
        fwdCurve.stiffness = baseStiffness;
        wheel.Collider.forwardFriction = fwdCurve;
    }

    void ApplyWheelForces(WheelData wheelData, float hInput, float vInput, float currentSteerRange, float nominalMotorTorque, float brakeTorque, float forwardSpeed)
    {
        WheelCollider wheel = wheelData.Collider;

        if (wheelData.IsSteer)
        {
            wheel.steerAngle = hInput * currentSteerRange;
        }

        if (wheelData.IsMotor)
        {
            float targetTorque = 0f;
            float targetBrake = 0f;

            bool isAccelerating = Mathf.Abs(vInput) > 0.01f;
            bool sameDirection = Mathf.Sign(vInput) == Mathf.Sign(forwardSpeed) || Mathf.Abs(forwardSpeed) < 1f;

            if (isAccelerating && sameDirection)
            {
                WheelHit hit;
                if (wheel.GetGroundHit(out hit))
                {
                    float wheelRadius = wheel.radius;
                    float Fz = hit.force;

                    float muLong = baseFwdExtremumValue * gripMultiplier * 0.9f;

                    float maxForce = muLong * Fz;
                    float maxWheelTorque = maxForce * wheelRadius;
                    float allowedTorque = maxWheelTorque * torqueLimitSafetyFactor;

                    float tcFactor = 1.0f;
                    if (enableTC && hit.forwardSlip > slipThreshold)
                    {
                        tcFactor = Mathf.Clamp01(1.0f - tcAgression * (hit.forwardSlip - slipThreshold));
                    }

                    targetTorque = vInput * nominalMotorTorque;
                    targetTorque = Mathf.Clamp(targetTorque, -allowedTorque, allowedTorque);
                    targetTorque *= tcFactor;
                }
                else
                {
                    targetTorque = vInput * nominalMotorTorque * 0.1f;
                }
            }
            else if (isAccelerating && !sameDirection)
            {
                targetBrake = brakeTorque;
            }
            else
            {
                targetBrake = 10f;
            }

            wheel.motorTorque = targetTorque;
            wheel.brakeTorque = targetBrake;
        }
        else
        {
            wheel.brakeTorque = 0;
            wheel.motorTorque = 0;
        }
    }
}