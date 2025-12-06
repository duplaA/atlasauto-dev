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

    [Header("Vehicle Type")] [Tooltip("Select a preset or use Custom to manually tune parameters")]
    public VehiclePreset vehiclePreset = VehiclePreset.HeavyVan;

    // --- WHEEL CONFIGURATION ---
    [Header("Drop your wheels here:")] public WheelCollider WheelCollider_FL;
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

    // --- ENGINE & TRANSMISSION ---
    [Header("Engine (Auto-configured by preset)")]
    public float engineRPM = 800f;

    public float engineInertia = 0.3f;
    public float idleRPM = 800f;
    public float maxRPM = 6000f;
    public float peakPowerHP = 147f;
    public float peakPowerRPM = 3500f;
    public float peakTorqueNm = 300f;
    public float peakTorqueRPM = 2500f;
    public float engineFrictionTorque = 15f;

    [Header("Transmission (Auto-configured)")]
    public float[] gearRatios = new float[] { 3.9f, 2.2f, 1.45f, 1.0f, 0.75f };

    public float finalDriveRatio = 3.9f;
    public float drivetrainEfficiency = 0.9f;
    public float clutchEfficiency = 0.95f;
    public int currentGear = 1;
    public float upshiftRPM = 5500f;
    public float downshiftRPM = 2000f;
    public bool autoShift = true;

    [Header("Vehicle Properties (Auto-configured)")]
    public float mass = 2100f;

    public float maxSpeed = 35f;
    public float steeringRange = 30f;
    public float steeringRangeAtMaxSpeed = 10f;
    public float centreOfGravityOffset = -0.5f;

    [Header("Suspension Settings")] public float suspensionDistance = 0.2f;

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

    private Vector2 cachedInput;
    private Rigidbody _rb;
    private VehicleControls carControls;

    private float mass_front_corner;
    private float mass_rear_corner;
    private float nominalFz;

    // Engine simulation state
    private AnimationCurve engineTorqueCurve;
    private float clutchSlip = 0f;
    private float shiftTimer = 0f;
    private const float shiftDuration = 0.6f; // Longer shift time for realism
    private float shiftCooldown = 0f; // Prevent rapid shifting
    private const float minShiftInterval = 1.2f; // Minimum time between shifts

    enum springFrequency
    {
        sport,
        comfort
    }

    enum dampRatio
    {
        sport,
        comfort
    }

    enum frontRearBias
    {
        forty_sixty,
        sixty_forty,
        fifty_fifty
    }

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
    }

    private WheelData[] allWheels;

    // --- PRESET CONFIGURATIONS ---
    [System.Serializable]
    private class VehiclePresetConfig
    {
        // Engine
        public float peakPowerHP;
        public float peakPowerRPM;
        public float peakTorqueNm;
        public float peakTorqueRPM;
        public float idleRPM;
        public float maxRPM;
        public float engineInertia;
        public float engineFrictionTorque;

        // Transmission
        public float[] gearRatios;
        public float finalDriveRatio;
        public float drivetrainEfficiency;
        public float upshiftRPM;
        public float downshiftRPM;

        // Vehicle
        public float mass;
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

    private VehiclePresetConfig GetPresetConfig(VehiclePreset preset)
    {
        switch (preset)
        {
            case VehiclePreset.HeavyVan:
                return new VehiclePresetConfig
                {
                    // Engine: Diesel van ~110 kW (147 HP)
                    peakPowerHP = 147f,
                    peakPowerRPM = 3500f,
                    peakTorqueNm = 300f,
                    peakTorqueRPM = 2000f,
                    idleRPM = 800f,
                    maxRPM = 4500f,
                    engineInertia = 0.35f,
                    engineFrictionTorque = 20f,

                    // Transmission: Tall gearing for fuel economy
                    gearRatios = new float[] { 3.9f, 2.2f, 1.45f, 1.0f, 0.75f },
                    finalDriveRatio = 3.9f,
                    drivetrainEfficiency = 0.88f,
                    upshiftRPM = 3800f,
                    downshiftRPM = 1500f,

                    mass = 2100f,
                    maxSpeed = 35f,
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
                    peakPowerHP = 180f,
                    peakPowerRPM = 3200f,
                    peakTorqueNm = 420f,
                    peakTorqueRPM = 1800f,
                    idleRPM = 700f,
                    maxRPM = 4000f,
                    engineInertia = 0.45f,
                    engineFrictionTorque = 25f,

                    gearRatios = new float[] { 4.2f, 2.5f, 1.6f, 1.15f, 0.85f },
                    finalDriveRatio = 4.1f,
                    drivetrainEfficiency = 0.85f,
                    upshiftRPM = 3500f,
                    downshiftRPM = 1400f,

                    mass = 2800f,
                    maxSpeed = 30f,
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
                    peakPowerHP = 160f,
                    peakPowerRPM = 5500f,
                    peakTorqueNm = 200f,
                    peakTorqueRPM = 4000f,
                    idleRPM = 850f,
                    maxRPM = 6500f,
                    engineInertia = 0.25f,
                    engineFrictionTorque = 12f,

                    gearRatios = new float[] { 3.5f, 2.0f, 1.3f, 0.95f, 0.75f },
                    finalDriveRatio = 3.7f,
                    drivetrainEfficiency = 0.92f,
                    upshiftRPM = 6000f,
                    downshiftRPM = 2500f,

                    mass = 1500f,
                    maxSpeed = 50f,
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
                    peakPowerHP = 300f,
                    peakPowerRPM = 6000f,
                    peakTorqueNm = 350f,
                    peakTorqueRPM = 4500f,
                    idleRPM = 900f,
                    maxRPM = 7000f,
                    engineInertia = 0.22f,
                    engineFrictionTorque = 10f,

                    gearRatios = new float[] { 3.2f, 2.1f, 1.5f, 1.1f, 0.85f, 0.7f },
                    finalDriveRatio = 3.5f,
                    drivetrainEfficiency = 0.93f,
                    upshiftRPM = 6500f,
                    downshiftRPM = 3000f,

                    mass = 1600f,
                    maxSpeed = 65f,
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
                    peakPowerHP = 450f,
                    peakPowerRPM = 7500f,
                    peakTorqueNm = 420f,
                    peakTorqueRPM = 5500f,
                    idleRPM = 1000f,
                    maxRPM = 8500f,
                    engineInertia = 0.18f,
                    engineFrictionTorque = 8f,

                    gearRatios = new float[] { 2.9f, 2.0f, 1.5f, 1.2f, 1.0f, 0.8f },
                    finalDriveRatio = 3.2f,
                    drivetrainEfficiency = 0.94f,
                    upshiftRPM = 8000f,
                    downshiftRPM = 4000f,

                    mass = 1400f,
                    maxSpeed = 80f,
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
                    peakPowerHP = 650f,
                    peakPowerRPM = 8500f,
                    peakTorqueNm = 550f,
                    peakTorqueRPM = 7000f,
                    idleRPM = 1200f,
                    maxRPM = 9500f,
                    engineInertia = 0.15f,
                    engineFrictionTorque = 6f,

                    gearRatios = new float[] { 2.6f, 1.9f, 1.5f, 1.25f, 1.05f, 0.9f },
                    finalDriveRatio = 3.0f,
                    drivetrainEfficiency = 0.95f,
                    upshiftRPM = 9000f,
                    downshiftRPM = 5000f,

                    mass = 1200f,
                    maxSpeed = 95f,
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

            default:
                return null;
        }
    }

    private void ApplyPreset()
    {
        if (vehiclePreset == VehiclePreset.Custom) return;

        var config = GetPresetConfig(vehiclePreset);
        if (config == null) return;

        // Engine
        peakPowerHP = config.peakPowerHP;
        peakPowerRPM = config.peakPowerRPM;
        peakTorqueNm = config.peakTorqueNm;
        peakTorqueRPM = config.peakTorqueRPM;
        idleRPM = config.idleRPM;
        maxRPM = config.maxRPM;
        engineInertia = config.engineInertia;
        engineFrictionTorque = config.engineFrictionTorque;

        // Transmission
        gearRatios = config.gearRatios;
        finalDriveRatio = config.finalDriveRatio;
        drivetrainEfficiency = config.drivetrainEfficiency;
        upshiftRPM = config.upshiftRPM;
        downshiftRPM = config.downshiftRPM;

        // Vehicle
        mass = config.mass;
        maxSpeed = config.maxSpeed;
        steeringRange = config.steeringRange;
        steeringRangeAtMaxSpeed = config.steeringRangeAtMaxSpeed;
        centreOfGravityOffset = config.centreOfGravityOffset;
        suspensionDistance = config.suspensionDistance;

        // Friction
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

        Debug.Log(
            $"Applied {vehiclePreset} preset - {peakPowerHP}HP @ {peakPowerRPM}RPM, {peakTorqueNm}Nm @ {peakTorqueRPM}RPM");
    }

    // --- BUILD ENGINE TORQUE CURVE ---
    private void BuildEngineTorqueCurve()
    {
        engineTorqueCurve = new AnimationCurve();

        // Idle torque (very low)
        engineTorqueCurve.AddKey(idleRPM, peakTorqueNm * 0.3f);

        // Build-up phase
        float rpmRange = peakTorqueRPM - idleRPM;
        engineTorqueCurve.AddKey(idleRPM + rpmRange * 0.3f, peakTorqueNm * 0.6f);
        engineTorqueCurve.AddKey(idleRPM + rpmRange * 0.7f, peakTorqueNm * 0.9f);

        // Peak torque plateau
        engineTorqueCurve.AddKey(peakTorqueRPM, peakTorqueNm);

        // Between peak torque and peak power
        float midRPM = (peakTorqueRPM + peakPowerRPM) / 2f;
        float midTorque = (peakTorqueNm + CalculateTorqueFromPower(peakPowerHP, peakPowerRPM)) / 2f;
        engineTorqueCurve.AddKey(midRPM, midTorque);

        // Peak power point
        float torqueAtPeakPower = CalculateTorqueFromPower(peakPowerHP, peakPowerRPM);
        engineTorqueCurve.AddKey(peakPowerRPM, torqueAtPeakPower);

        // Drop-off to redline
        float redlineRPM = maxRPM;
        engineTorqueCurve.AddKey(redlineRPM, torqueAtPeakPower * 0.7f);

        // Smooth the curve
        for (int i = 0; i < engineTorqueCurve.length; i++)
        {
            engineTorqueCurve.SmoothTangents(i, 0.3f);
        }
    }

    private float CalculateTorqueFromPower(float powerHP, float rpm)
    {
        // T = P * 60 / (2π * rpm)
        // Convert HP to Watts first: 1 HP = 745.699872 W
        float powerWatts = powerHP * 745.699872f;
        return powerWatts * 60f / (2f * Mathf.PI * rpm);
    }

    private float SampleEngineTorque(float rpm)
    {
        rpm = Mathf.Clamp(rpm, idleRPM, maxRPM);
        return engineTorqueCurve.Evaluate(rpm);
    }

    // --- INITIALIZATION ---
    void Awake()
    {
        ApplyPreset();
        BuildEngineTorqueCurve();

        _rb = GetComponent<Rigidbody>();

        carControls = new VehicleControls();
        carControls.Vehicle.Move.performed += ctx => cachedInput = ctx.ReadValue<Vector2>();
        carControls.Vehicle.Move.canceled += ctx => cachedInput = Vector2.zero;

        allWheels = new WheelData[]
        {
            new WheelData
                { Collider = WheelCollider_FL, Model = wheelModel_FL, IsSteer = isSteer_FL, IsMotor = isMotor_FL },
            new WheelData
                { Collider = WheelCollider_FR, Model = wheelModel_FR, IsSteer = isSteer_FR, IsMotor = isMotor_FR },
            new WheelData
                { Collider = WheelCollider_RL, Model = wheelModel_RL, IsSteer = isSteer_RL, IsMotor = isMotor_RL },
            new WheelData
                { Collider = WheelCollider_RR, Model = wheelModel_RR, IsSteer = isSteer_RR, IsMotor = isMotor_RR }
        };

        engineRPM = idleRPM;
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
            float freq = _springFrequency == springFrequency.sport
                ? (wheel.Model.name.ToLower().Contains("f") ? 2.3f : 1.9f)
                : (wheel.Model.name.ToLower().Contains("f") ? 1.8f : 1.5f);

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
        float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);

        // --- ENGINE & TRANSMISSION SIMULATION ---

        // 1. Calculate average driven wheel RPM
        float wheelRPM_driven = CalculateAverageDrivenWheelRPM();

        // 2. Handle shifting
        bool isShifting = shiftTimer > 0f;

        if (isShifting)
        {
            shiftTimer -= Time.fixedDeltaTime;

            // During shift: full clutch slip and let RPM drop naturally
            clutchSlip = 1f;

            // After shift completes, match engine RPM to new gear ratio
            if (shiftTimer <= 0f)
            {
                // Calculate what RPM should be in the new gear
                float newGearRatio = (currentGear > 0 && currentGear <= gearRatios.Length)
                    ? gearRatios[currentGear - 1]
                    : 1f;
                float targetRPMAfterShift = Mathf.Abs(wheelRPM_driven) * newGearRatio * finalDriveRatio;

                // Snap engine RPM to new gear (simulates clutch re-engagement)
                engineRPM = Mathf.Max(targetRPMAfterShift, idleRPM);
            }
        }
        else
        {
            clutchSlip = 0f;

            // Only auto-shift if moving and not already shifting
            if (autoShift && Mathf.Abs(forwardSpeed) > 1f)
            {
                HandleAutoShift();
            }
        }

        // 3. Detect reverse request
        bool wantsReverse = vInput < -0.1f;

        // Handle gear selection (forward vs reverse)
        if (wantsReverse && Mathf.Abs(forwardSpeed) < 0.5f)
        {
            // Switch to reverse when stopped
            if (currentGear > 0)
            {
                currentGear = -1; // Reverse gear
                shiftTimer = shiftDuration * 0.5f; // Shorter shift to reverse
            }
        }
        else if (vInput > 0.1f && currentGear < 0)
        {
            // Switch to forward when in reverse
            currentGear = 1;
            shiftTimer = shiftDuration * 0.5f;
        }
        else if (vInput > 0.1f && currentGear == 0)
        {
            // Start in first gear
            currentGear = 1;
        }

        // 4. Calculate target engine RPM from wheels (via current gear)
        float currentGearRatio;
        if (currentGear > 0 && currentGear <= gearRatios.Length)
        {
            currentGearRatio = gearRatios[currentGear - 1];
        }
        else if (currentGear == -1)
        {
            // Reverse uses first gear ratio
            currentGearRatio = gearRatios[0];
        }
        else
        {
            currentGearRatio = 1f;
        }

        float wheelDemandedRPM = Mathf.Abs(wheelRPM_driven) * currentGearRatio * finalDriveRatio;

        // 5. Determine clutch engagement based on conditions
        float rpmDifference = Mathf.Abs(engineRPM - wheelDemandedRPM);
        float autoClutchSlip = 0f;

        float throttle = Mathf.Abs(vInput);

        // Auto-clutch logic: slip at low speeds or big RPM mismatch
        if (Mathf.Abs(forwardSpeed) < 3f && throttle > 0.1f)
        {
            // At low speed with throttle: progressive clutch engagement
            // More throttle = less slip (more engagement)
            // But maintain some slip to allow engine to build RPM
            speedFactor = Mathf.Abs(forwardSpeed) / 3f; // 0 at standstill, 1 at 3 m/s
            autoClutchSlip = Mathf.Lerp(0.7f, 0f, speedFactor); // 70% slip at standstill, 0% at 3 m/s

            // Reduce slip as engine RPM builds (simulates clutch biting point)
            if (engineRPM > idleRPM + 500f)
            {
                float rpmBuildUp = Mathf.InverseLerp(idleRPM + 500f, idleRPM + 1500f, engineRPM);
                autoClutchSlip *= (1f - rpmBuildUp * 0.5f); // Reduce slip as RPM rises
            }
        }

        // Combine manual shift slip with auto-clutch slip
        float totalClutchSlip = Mathf.Max(clutchSlip, autoClutchSlip);

        // 6. Sample engine torque at current RPM
        float engineTorque_nominal = SampleEngineTorque(engineRPM);

        // 7. Engine RPM simulation
        if (throttle < 0.01f)
        {
            // No throttle - return to idle
            engineRPM = Mathf.Lerp(engineRPM, idleRPM, 0.1f);
        }
        else
        {
            // On throttle: calculate target RPM based on clutch state
            float targetRPM;

            if (totalClutchSlip > 0.3f)
            {
                // Significant clutch slip - engine can rev somewhat freely
                // But limit based on throttle position to prevent over-revving
                float maxThrottleRPM = Mathf.Lerp(idleRPM + 800f, peakPowerRPM, throttle);
                targetRPM = maxThrottleRPM;
            }
            else
            {
                // Clutch mostly engaged - follow wheel speed
                targetRPM = Mathf.Max(wheelDemandedRPM, idleRPM);

                // Add small throttle boost for acceleration
                targetRPM += throttle * 200f;
            }

            // Smoothly move engine RPM toward target
            float rpmChangeRate = 3000f * Time.fixedDeltaTime; // Max 3000 RPM/sec change
            engineRPM = Mathf.MoveTowards(engineRPM, targetRPM, rpmChangeRate);
        }

        // Hard clamp to prevent over-rev
        engineRPM = Mathf.Clamp(engineRPM, idleRPM, maxRPM);

        // 8. Calculate effective engine torque output (reduced by clutch slip)
        float effectiveClutch = clutchEfficiency * (1f - totalClutchSlip);
        float engineTorque_output = engineTorque_nominal * throttle * effectiveClutch;

        // 9. Calculate wheel torque from engine
        int numDrivenWheels = 0;
        foreach (var w in allWheels)
        {
            if (w.IsMotor) numDrivenWheels++;
        }

        float wheelTorque_fromEngine = 0f;
        if (numDrivenWheels > 0 && currentGear != 0)
        {
            wheelTorque_fromEngine = (engineTorque_output * currentGearRatio * finalDriveRatio * drivetrainEfficiency) /
                                     numDrivenWheels;

            // Apply reverse direction
            if (currentGear == -1)
            {
                wheelTorque_fromEngine *= -1f;
            }
        }

        // 10. Apply forces to wheels
        foreach (var wheel in allWheels)
        {
            UpdateWheelFriction(wheel);
            ApplyWheelForces(wheel, hInput, vInput, currentSteerRange, wheelTorque_fromEngine, forwardSpeed);
        }
    }

    // --- ENGINE HELPER FUNCTIONS ---

    private float CalculateAverageDrivenWheelRPM()
    {
        float totalRPM = 0f;
        int count = 0;

        foreach (var wheel in allWheels)
        {
            if (wheel.IsMotor && wheel.Collider != null)
            {
                totalRPM += Mathf.Abs(wheel.Collider.rpm);
                count++;
            }
        }

        return count > 0 ? totalRPM / count : 0f;
    }

    private void HandleAutoShift()
    {
        // Upshift
        if (engineRPM > upshiftRPM && currentGear < gearRatios.Length)
        {
            ShiftUp();
        }
        // Downshift
        else if (engineRPM < downshiftRPM && currentGear > 1)
        {
            ShiftDown();
        }
    }

    private void ShiftUp()
    {
        if (currentGear < gearRatios.Length)
        {
            currentGear++;
            shiftTimer = shiftDuration;
            Debug.Log($"Shifted up to gear {currentGear}");
        }
    }

    private void ShiftDown()
    {
        if (currentGear > 1)
        {
            currentGear--;
            shiftTimer = shiftDuration;
            Debug.Log($"Shifted down to gear {currentGear}");
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

        float compression = 1.0f - ((hit.point - wheel.Collider.transform.position).magnitude /
                                    wheel.Collider.suspensionDistance);
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

    float ApplyWheelForces(WheelData wheelData, float hInput, float vInput, float currentSteerRange,
        float nominalWheelTorque, float forwardSpeed)
    {
        WheelCollider wheel = wheelData.Collider;

        // Steering
        if (wheelData.IsSteer)
        {
            wheel.steerAngle = hInput * currentSteerRange;
        }

        // Drive Forces
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

                    // Calculate max allowed torque based on tyre grip
                    float muLong = baseFwdExtremumValue * gripMultiplier * 0.9f;
                    float maxForce = muLong * Fz;
                    float maxWheelTorque = maxForce * wheelRadius;
                    float allowedTorque = maxWheelTorque * torqueLimitSafetyFactor;

                    // Traction control
                    float tcFactor = 1.0f;
                    if (enableTC && hit.forwardSlip > slipThreshold)
                    {
                        tcFactor = Mathf.Clamp01(1.0f - tcAgression * (hit.forwardSlip - slipThreshold));
                    }

                    // Apply engine torque with limits
                    targetTorque = nominalWheelTorque;
                    targetTorque = Mathf.Clamp(targetTorque, -allowedTorque, allowedTorque);
                    targetTorque *= tcFactor;
                }
                else
                {
                    // Airborne - minimal torque
                    targetTorque = nominalWheelTorque * 0.1f;
                }
            }
            else if (isAccelerating && !sameDirection)
            {
                // Braking when reversing direction
                targetBrake = 3500f;
            }
            else
            {
                // Idle drag
                targetBrake = 10f;
            }

            wheel.motorTorque = targetTorque;
            wheel.brakeTorque = targetBrake;

            return targetTorque; // Return applied torque for engine load calculation
        }
        else
        {
            wheel.brakeTorque = 0;
            wheel.motorTorque = 0;
            return 0f;
        }
    }

    // --- TELEMETRY (optional - display in UI) ---
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float powerWatts = SampleEngineTorque(engineRPM) * 2f * Mathf.PI * engineRPM / 60f;
        float powerHP = powerWatts / 745.699872f;

        float speedMS = transform.InverseTransformDirection(_rb.linearVelocity).z;
        float speedKMH = speedMS * 3.6f;

        float wheelRPM = CalculateAverageDrivenWheelRPM();
        float currentGearRatio = 1f;
        if (currentGear > 0 && currentGear <= gearRatios.Length)
        {
            currentGearRatio = gearRatios[currentGear - 1];
        }
        else if (currentGear == -1)
        {
            currentGearRatio = gearRatios[0];
        }

        float wheelDemandRPM = Mathf.Abs(wheelRPM) * currentGearRatio * finalDriveRatio;

        string gearDisplay = currentGear == -1 ? "R" : currentGear == 0 ? "N" : currentGear.ToString();

        GUI.Label(new Rect(10, 10, 300, 25), $"RPM: {engineRPM:F0} / {maxRPM:F0}", style);
        GUI.Label(new Rect(10, 35, 300, 25), $"Gear: {gearDisplay} / {gearRatios.Length}", style);
        GUI.Label(new Rect(10, 60, 300, 25), $"Power: {powerHP:F0} HP", style);
        GUI.Label(new Rect(10, 85, 300, 25), $"Torque: {SampleEngineTorque(engineRPM):F0} Nm", style);
        GUI.Label(new Rect(10, 110, 300, 25), $"Speed: {speedKMH:F0} km/h", style);
        GUI.Label(new Rect(10, 135, 300, 25), $"Wheel RPM: {wheelRPM:F0} (demands {wheelDemandRPM:F0})", style);

        if (shiftTimer > 0f)
        {
            GUI.Label(new Rect(10, 160, 300, 25), "SHIFTING...", style);
        }

        // Show clutch state
        float throttle = Mathf.Abs(cachedInput.y);
        if (Mathf.Abs(speedMS) < 3f && throttle > 0.1f)
        {
            GUI.Label(new Rect(10, 185, 300, 25), "CLUTCH ENGAGING...", style);
        }
    }
}