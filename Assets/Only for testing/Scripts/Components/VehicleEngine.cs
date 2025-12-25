using UnityEngine;

/// <summary>
/// Stateless engine torque calculator.
/// Follows strict causality: Input RPM + Throttle -> Output Torque.
/// Supports both ICE and EV.
/// </summary>
public class VehicleEngine : MonoBehaviour
{
    public enum EngineType { InternalCombustion, Electric }

    [Header("Engine Type")]
    public EngineType engineType = EngineType.InternalCombustion;

    [Header("Power Output")]
    [Tooltip("Peak power in Horsepower (HP). Synced with kW.")]
    public float horsepowerHP = 400f;
    [Tooltip("Peak power in Kilowatts (kW). Auto-calculated from HP.")]
    public float maxPowerKW = 300f;

    [Header("Torque")]
    [Tooltip("Peak torque in Nm (ICE peak or EV constant torque region)")]
    public float peakTorqueNm = 450f;
    [Tooltip("Maximum mechanical RPM")]
    public float maxRPM = 7500f;
    [Tooltip("Engine inertia (kg*m^2). Only affects free-revving response.")]
    public float inertia = 0.2f;

    [Header("Internal Combustion")]
    [Tooltip("RPM where peak torque is reached")]
    public float peakTorqueRPM = 4500f;
    [Tooltip("RPM where peak power is reached (Torque begins to drop significantly)")]
    public float peakPowerRPM = 6000f;
    [Tooltip("Idle RPM")]
    public float idleRPM = 850f;
    
    // Auto-generated curve based on peaks
    private AnimationCurve proceduralTorqueCurve;

    [Header("Electric Vehicle")]
    [Tooltip("RPM where motor transitions from Constant Torque to Constant Power (Base Speed)")]
    public float evBaseRPM = 3000f;

    [Header("Friction")]
    public float frictionTorque = 15f; // Constant drag
    public float brakingTorque = 60f; // Engine braking at 0 throttle

    [Header("State")]
    public float currentRPM;
    public float currentLoad; // 0..1, for UI/Sound

    // Conversion constants
    private const float HP_TO_KW = 0.7457f;
    private const float KW_TO_HP = 1.341f;

    void OnValidate()
    {
        // Sync HP <-> kW (HP is the primary input, kW is derived)
        maxPowerKW = horsepowerHP * HP_TO_KW;
    }
    
    void Awake()
    {
        // Ensure sync
        maxPowerKW = horsepowerHP * HP_TO_KW;
        GenerateTorqueCurve();
    }
    
    void GenerateTorqueCurve()
    {
        // generate a realistic torque curve
        proceduralTorqueCurve = new AnimationCurve();
        
        // Idle: 60% torque
        proceduralTorqueCurve.AddKey(new Keyframe(0f, 0.7f)); 
        
        // Peak Torque RPM: 100% torque
        float peakTorqueNorm = peakTorqueRPM / maxRPM;
        proceduralTorqueCurve.AddKey(new Keyframe(peakTorqueNorm, 1.0f));
        
        // We WANT to hit exactly 'horsepowerHP' at 'peakPowerRPM'.
        // RequiredTorque = (PowerKW * 9549) / RPM
        // CurveFactor = RequiredTorque / peakTorqueNm 
        float requiredTorqueForPeakHP = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(peakPowerRPM, 1f);
        float powerPointFactor = requiredTorqueForPeakHP / Mathf.Max(peakTorqueNm, 1f);
        
        // Clamp it reasonably (can't produce MORE than mechanical peak torque implies)
        // For realism, we should stick to 1.0 peak, implying the user's config is impossible
        // Let's cap at 1.0f (User needs to raise PeakTorque if they want more HP at low RPM)
        powerPointFactor = Mathf.Min(powerPointFactor, 1.0f);
        float peakPowerNorm = peakPowerRPM / maxRPM;
        proceduralTorqueCurve.AddKey(new Keyframe(peakPowerNorm, powerPointFactor)); 
        
        // Redline: Maintain more power
        // Don't drop to 0.5. Drop to maybe 0.7 or maintain power?
        proceduralTorqueCurve.AddKey(new Keyframe(1.0f, 0.7f));
        
        // Linearize tangents for smooth curve
        for (int i=0; i < proceduralTorqueCurve.length; i++) 
            proceduralTorqueCurve.SmoothTangents(i, 0f);            
        Debug.Log($"[VehicleEngine] Generated Torque Curve. Peak Torque @ {peakTorqueRPM}, Peak Power Factor {powerPointFactor:F2} @ {peakPowerRPM}");
    }


    /// Calculates the instantaneous torque available at the flywheel.
    /// Pure function: depends only on current state, does not modify state.
    public float CalculateTorque(float currentRPM, float throttle)
    {
        currentRPM = Mathf.Abs(currentRPM); // Handle reverse RPM naturally
        float availableTorque = 0f;

        if (engineType == EngineType.InternalCombustion)
        {
            // ICE Calculation: Procedural Curve based
            if (proceduralTorqueCurve == null) GenerateTorqueCurve();
            
            float effectiveRPM = Mathf.Max(currentRPM, idleRPM);
            float normalizedRPM = Mathf.Clamp01(effectiveRPM / maxRPM);
            
            float curveFactor = proceduralTorqueCurve.Evaluate(normalizedRPM);
            availableTorque = peakTorqueNm * curveFactor * throttle;
        }
        else
        {
            // EV Calculation: Dynamic Crossover
            // We calculate the natural RPM where Peak Torque intersects Peak Power.
            // CrossoverRPM = (PowerKW * 9549) / TorqueNm
            float crossoverRPM = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(peakTorqueNm, 1f);
            
            // Update the debug value if needed (optional, or just ignore the field)
            // evBaseRPM = crossoverRPM; 

            if (currentRPM < crossoverRPM)
            {
                // Constant Torque Region
                availableTorque = peakTorqueNm * throttle;
            }
            else
            {
                // Constant Power Region
                // Torque = Power / RPM
                float powerLimitTorque = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(currentRPM, 1f);
                availableTorque = powerLimitTorque * throttle;
            }
        }

        // Apply friction/pumping losses
        float drag = frictionTorque + (brakingTorque * (1f - throttle) * (currentRPM / maxRPM));
        float netTorque = availableTorque - drag;
        
        if (throttle > 0.1f) netTorque = Mathf.Max(netTorque, 0f);
        
        return netTorque;
    }

    /// Returns the maximum power (kW) currently being produced.
    /// Used for UI/Telemetry.
    public float GetCurrentPowerKW(float torqueNm, float currentRPM)
    {
        return (torqueNm * currentRPM) / 9549f;
    }
}