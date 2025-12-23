using UnityEngine;

/// <summary>
/// Realistic transmission simulation with proper gear physics.
/// Handles gear ratios, shift timing, and RPM calculations during shifts.
/// </summary>
public class VehicleTransmission : MonoBehaviour
{
    public enum TransmissionMode { Automatic, Manual }

    [Header("Transmission Settings")]
    public TransmissionMode mode = TransmissionMode.Automatic;
    public bool isElectric = false;

    [Header("Gear Ratios")]
    [Tooltip("Gear ratios from 1st to top gear")]
    public float[] gearRatios = { 3.5f, 2.1f, 1.4f, 1.0f, 0.8f, 0.65f };
    public float reverseRatio = 3.2f;
    public float electricFixedRatio = 9.0f;
    public float finalDriveRatio = 3.7f;

    [Header("Shift Tuning")]
    [Tooltip("RPM percentage of max to upshift (0.85 = 85% of redline)")]
    [Range(0.7f, 0.95f)]
    public float upshiftPoint = 0.85f;
    [Tooltip("RPM percentage of max to downshift")]
    [Range(0.2f, 0.4f)]
    public float downshiftPoint = 0.3f;
    [Tooltip("Time for gear change (clutch disengaged)")]
    public float shiftTime = 0.25f;
    [Tooltip("Minimum time between consecutive shifts")]
    public float shiftCooldown = 1.0f;

    [Header("Clutch")]
    [Tooltip("Speed below which clutch slips (km/h)")]
    public float clutchSlipSpeed = 15f;

    [Header("State")]
    public int currentGear = 0; // -1=R, 0=N, 1+=D
    public float clutchPosition = 1f; // 0=disengaged, 1=engaged

    private float shiftTimer = 0f;
    private float cooldownTimer = 0f;
    private bool isShifting = false;
    private int previousGear = 0;

    /// <summary>
    /// Update transmission state. Call every FixedUpdate.
    /// </summary>
    public void UpdateTransmission(float vehicleSpeedKMH, float engineRPM, float maxRPM, float throttle, float dt)
    {
        // Update timers
        if (cooldownTimer > 0f) cooldownTimer -= dt;

        // Handle active shift
        if (isShifting)
        {
            shiftTimer -= dt;
            clutchPosition = 0.1f; // Almost fully disengaged during shift

            if (shiftTimer <= 0f)
            {
                isShifting = false;
                shiftTimer = 0f;
            }
            return;
        }

        // Electric: always in "gear 1", no clutch
        if (isElectric)
        {
            currentGear = 1;
            clutchPosition = 1f;
            return;
        }

        // Clutch slip at low speeds
        if (vehicleSpeedKMH < clutchSlipSpeed && currentGear != 0)
        {
            clutchPosition = Mathf.Lerp(0.2f, 1f, vehicleSpeedKMH / clutchSlipSpeed);
        }
        else
        {
            clutchPosition = 1f;
        }

        // Automatic shifting logic
        if (mode == TransmissionMode.Automatic && currentGear > 0 && cooldownTimer <= 0f)
        {
            float rpmRatio = engineRPM / maxRPM;

            // Upshift condition
            if (rpmRatio >= upshiftPoint && currentGear < gearRatios.Length)
            {
                ExecuteShift(currentGear + 1);
            }
            // Downshift condition (only when not accelerating hard)
            else if (rpmRatio <= downshiftPoint && currentGear > 1 && throttle < 0.3f)
            {
                ExecuteShift(currentGear - 1);
            }
            // Kickdown: aggressive throttle at mid-low RPM
            else if (throttle > 0.9f && rpmRatio < 0.5f && currentGear > 1)
            {
                ExecuteShift(currentGear - 1);
            }
        }
    }

    /// <summary>
    /// Calculate what RPM the engine will drop/jump to after a shift.
    /// NewRPM = OldRPM × (NewGearRatio / OldGearRatio)
    /// </summary>
    public float CalculateRPMAfterShift(float currentRPM, int fromGear, int toGear)
    {
        float fromRatio = GetGearRatio(fromGear);
        float toRatio = GetGearRatio(toGear);

        if (Mathf.Abs(fromRatio) < 0.01f) return currentRPM;

        return currentRPM * (toRatio / fromRatio);
    }

    void ExecuteShift(int targetGear)
    {
        previousGear = currentGear;
        currentGear = Mathf.Clamp(targetGear, 1, gearRatios.Length);
        isShifting = true;
        shiftTimer = shiftTime;
        cooldownTimer = shiftCooldown;
    }

    public void ShiftUp()
    {
        if (!isShifting && currentGear > 0 && currentGear < gearRatios.Length && cooldownTimer <= 0f)
        {
            ExecuteShift(currentGear + 1);
        }
    }

    public void ShiftDown()
    {
        if (!isShifting && currentGear > 1 && cooldownTimer <= 0f)
        {
            ExecuteShift(currentGear - 1);
        }
    }

    public void SetDriveMode(int gear)
    {
        if (!isShifting && gear >= -1 && gear <= 1)
        {
            currentGear = gear;
        }
    }

    float GetGearRatio(int gear)
    {
        if (gear == 0) return 0f;
        if (gear == -1) return reverseRatio;
        if (isElectric) return electricFixedRatio;
        int index = Mathf.Clamp(gear - 1, 0, gearRatios.Length - 1);
        return gearRatios[index];
    }

    /// <summary>
    /// Get total gear ratio (gear × final drive).
    /// </summary>
    public float GetTotalRatio()
    {
        if (currentGear == 0) return 0f;

        float gearRatio = GetGearRatio(currentGear);
        float total = gearRatio * finalDriveRatio;

        // Negative for reverse
        if (currentGear == -1) total = -Mathf.Abs(total);

        return total;
    }

    public string GetGearDisplayString()
    {
        if (currentGear == -1) return "R";
        if (currentGear == 0) return "N";
        return currentGear.ToString();
    }

    public bool IsShifting() => isShifting;
}