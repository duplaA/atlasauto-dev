using UnityEngine;

/// <summary>
/// Transmission State Helper.
/// ICE: Automatic shifting, clutch slip, multiple gears.
/// EV: Single fixed ratio, always engaged, no clutch, no shifting.
/// </summary>
public class VehicleTransmission : MonoBehaviour
{
    public enum TransmissionMode { Automatic, Manual }

    [Header("Settings")]
    public TransmissionMode mode = TransmissionMode.Automatic;
    public bool isElectric = false;

    [Header("Ratios (ICE)")]
    public float[] gearRatios = { 3.5f, 2.2f, 1.5f, 1.1f, 0.9f, 0.75f };
    public float reverseRatio = 3.2f;
    public float finalDriveRatio = 3.7f;

    [Header("Ratios (EV)")]
    public float electricFixedRatio = 8.5f;

    [Header("Shift Logic (ICE Only)")]
    [Tooltip("RPM% to Shift UP")]
    public float upshiftRPM = 0.85f;
    [Tooltip("RPM% to Shift DOWN")]
    public float downshiftRPM = 0.4f;
    [Tooltip("Time the clutch is disengaged during shift")]
    public float shiftDuration = 0.3f;

    [Header("State (Read Only)")]
    public int currentGear = 0; // 0=Neutral, -1=Reverse, 1+=Forward
    public float clutchEngagement = 1f; // 0=Disengaged, 1=Fully Engaged
    
    // Internal State (ICE shifting)
    private float shiftTimer = 0f;
    private int targetGear = 0;
    private bool isShifting = false;

    void Start()
    {
        // Auto-Tune EV Gear Ratio
        // We need the VehicleController to know Top Speed
        VehicleController vc = GetComponent<VehicleController>();
        
        // Wait for Engine linkage
        if (vc != null && isElectric)
        {
            // Critical: VehicleController uses a HARDCODED 0.34f radius for RPM derivation.
            float normalizationRadius = 0.34f; 
            
            float circumference = 2f * Mathf.PI * normalizationRadius;
            float topSpeedMS = vc.topSpeedKMH / 3.6f;
            float maxRPM = vc.engine != null ? vc.engine.maxRPM : 7000f; // Engine should be linked by Start()
            
            float optimalRatio = (maxRPM * circumference) / (topSpeedMS * 60f);
            
            // Adjust so we hit Redline exactly at top speed
            electricFixedRatio = optimalRatio;
            
            Debug.Log($"[Transmission] Auto-Tuned EV Ratio: {electricFixedRatio:F2} (Target: {vc.topSpeedKMH} km/h @ {maxRPM} RPM)");
        }
    }

    /// Call this from Controller to handle automatic shifting logic.
    /// EVs bypass this entirely: they are always in Drive with full clutch.
    public void UpdateGearLogic(float engineRPM, float maxRPM, float throttle, float dt)
    {
        if (isElectric)
        {
            // EV: Always engaged, no shifting
            clutchEngagement = 1f;
            isShifting = false;
            
            // If in Neutral, auto-enter Drive. EVs should never stall in N.
            if (currentGear == 0)
            {
                currentGear = 1;
            }
            return; // Skip all ICE logic
        }

        // ICE LOGIC
        // Handle shift timer
        if (isShifting)
        {
            shiftTimer -= dt;
            clutchEngagement = 0f; // Clutch disengaged during shift
            if (shiftTimer <= 0f)
            {
                isShifting = false;
                currentGear = targetGear;
                clutchEngagement = 1f;
            }
            return;
        }

        if (mode == TransmissionMode.Automatic)
        {
            float rpmPercent = engineRPM / maxRPM;

            // Upshift
            if (currentGear > 0 && currentGear < gearRatios.Length && rpmPercent > upshiftRPM)
            {
                ShiftTo(currentGear + 1);
            }
            // Downshift
            else if (currentGear > 1 && rpmPercent < downshiftRPM && throttle < 0.5f)
            {
                ShiftTo(currentGear - 1);
            }
        }
    }

    public void ShiftTo(int gear)
    {
        // EVs: instant shift, no delay
        if (isElectric)
        {
            currentGear = Mathf.Clamp(gear, -1, 1); // EV only has R, N, D
            return;
        }

        // ICE: delayed shift
        if (isShifting) return;
        targetGear = Mathf.Clamp(gear, -1, gearRatios.Length);
        
        // Instant shift for N or R, delayed for gears
        if (targetGear == 0 || targetGear == -1 || currentGear == 0 || currentGear == -1)
        {
            currentGear = targetGear;
        }
        else
        {
            isShifting = true;
            shiftTimer = shiftDuration;
        }
    }

    public void SetReverse() => ShiftTo(-1);
    public void SetNeutral() => ShiftTo(0);
    public void SetDrive() => ShiftTo(1);

    /// Returns the ratio for a specific gear.
    /// FIX 4: EV returns correct signed ratio for Reverse.
    public float GetRatioForGear(int gear)
    {
        if (isElectric)
        {
            // EV: Single fixed ratio. Reverse just flips sign.
            if (gear == -1) return -electricFixedRatio;
            if (gear == 0) return 0f;
            return electricFixedRatio;
        }

        // ICE
        if (gear == 0) return 0f;
        if (gear == -1) return -reverseRatio;
        return gearRatios[Mathf.Clamp(gear - 1, 0, gearRatios.Length - 1)];
    }

    /// Returns the TOTAL ratio (Gear * Final).
    public float GetTotalRatio()
    {
        return GetRatioForGear(currentGear) * finalDriveRatio;
    }

    /// Calculates Engine RPM based on Wheel RPM.
    /// STRICT CAUSALITY: Wheels -> Engine.
    public float GetEngineRPM(float wheelRPM, float currentEngineRPM)
    {
        float totalRatio = GetTotalRatio();

        // If in Neutral or Clutch is disengaged, engine is disconnected from wheels.
        if (Mathf.Abs(totalRatio) < 0.01f || clutchEngagement < 0.1f)
        {
            return currentEngineRPM;
        }

        // Connected: Engine RPM = Wheel RPM * Ratio
        return wheelRPM * Mathf.Abs(totalRatio);
    }

    /// Calculates Torque at Wheels based on Engine Torque.
    /// CAUSALITY: Engine -> Wheels.
    public float GetDriveTorque(float engineTorque)
    {
        // EVs always have clutch engaged
        if (!isElectric && clutchEngagement < 0.1f) return 0f;
        return engineTorque * GetTotalRatio() * 0.9f; // 90% efficiency
    }

    public string GetGearDisplayString()
    {
        if (currentGear == -1) return "R";
        if (currentGear == 0) return "N";
        if (isElectric) return "D"; // EVs always show D
        return currentGear.ToString();
    }
}