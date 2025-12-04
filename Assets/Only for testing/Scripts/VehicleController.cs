using System;
using UnityEngine;
using UnityEngine.InputSystem; // Added namespace for cleanliness

public class VehicleController : MonoBehaviour
{
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
    
    [Header("Car Properties")] 
    public float mass = 1500f;
    public float motorTorque = 1500f;
    public float brakeTorque = 2000f;
    public float maxSpeed = 20f;
    public float steeringRange = 30f;
    public float steeringRangeAtMaxSpeed = 10f;
    public float centreOfGravityOffset = -0.5f;
    
    [Header("Suspension Settings")]
    [Tooltip("Length of the suspension travel in meters. Increase if car body drags on ground.")]
    public float suspensionDistance = 0.2f;
    
    [Header("Wheel Friction Settings")]
    [Tooltip("Forward friction curve settings")]
    public float frictionExtremumSlip = 3f;
    public float frictionExtremumValue = 2.0f;
    public float frictionAsymptoteSlip = 0.8f;
    public float frictionAsymptoteValue = 1.0f;
    
    [Tooltip("The multiplier for side friction. Higher value means more lateral grip and less sliding.")]
    public float frictionStiffness = 5.0f;
    
    private Vector2 cachedInput;
    private Rigidbody _rb;
    private VehicleControls carControls;

    private float calculatedSpring;
    private float calculatedDamp;
    
    private struct WheelData
    {
        public WheelCollider Collider;
        public Transform Model;
        public bool IsSteer;
        public bool IsMotor;
    }
    
    private WheelData[] allWheels;

    void Awake()
    {
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
    }

    void OnEnable()
    {
        if(carControls != null) carControls.Enable();
    }

    void OnDisable()
    {
        if(carControls != null) carControls.Disable();
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
    }

    void SetupSuspension()
    {
        calculatedSpring = mass * 20f; 

        calculatedDamp = mass * 4.0f; 
        
        WheelFrictionCurve frictionCurve = new WheelFrictionCurve
        {
            extremumSlip = frictionExtremumSlip,
            extremumValue = frictionExtremumValue,
            asymptoteSlip = frictionAsymptoteSlip,
            asymptoteValue = frictionAsymptoteValue,
            stiffness = frictionStiffness
        };
        
        WheelFrictionCurve sidewaysFrictionCurve = new WheelFrictionCurve
        {
            extremumSlip = frictionExtremumSlip,
            extremumValue = frictionExtremumValue,
            asymptoteSlip = frictionAsymptoteSlip,
            asymptoteValue = frictionAsymptoteValue,
            stiffness = frictionStiffness
        };

        foreach (var wheel in allWheels)
        {
            if (wheel.Collider == null) continue;

            JointSpring currentSpring = wheel.Collider.suspensionSpring;
            
            // Apply calculated values to the spring struct
            currentSpring.spring = calculatedSpring;
            currentSpring.damper = calculatedDamp;
            currentSpring.targetPosition = 0.5f;
            
            wheel.Collider.suspensionSpring = currentSpring;

            wheel.Collider.suspensionDistance = suspensionDistance;

            wheel.Collider.wheelDampingRate = 0.5f; 

            wheel.Collider.forceAppPointDistance = 0f;

            wheel.Collider.forwardFriction = frictionCurve;
            wheel.Collider.sidewaysFriction = sidewaysFrictionCurve;
        }
    }

    void SyncWheel(WheelCollider collider, Transform model)
    {
        collider.GetWorldPose(out position, out rotation);
        model.transform.position = position;
        model.transform.rotation = rotation;
    }

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
            ApplyWheelForces(wheel.Collider, wheel.IsSteer, wheel.IsMotor, hInput, vInput, currentSteerRange, currentMotorTorque, brakeTorque, forwardSpeed);
        }
    }

    void ApplyWheelForces(WheelCollider wheel, bool isSteer, bool isMotor, float hInput, float vInput, float currentSteerRange, float currentMotorTorque, float brakeTorque, float forwardSpeed)
    {
        // Steering
        if (isSteer)
        {
            wheel.steerAngle = hInput * currentSteerRange;
        }
        
        // Acceleration / Braking Logic
        if (isMotor)
        {
            bool isAccelerating = Mathf.Abs(vInput) > 0.01f;
            bool sameDirection = Mathf.Sign(vInput) == Mathf.Sign(forwardSpeed) || Mathf.Abs(forwardSpeed) < 1f;

            if (isAccelerating && sameDirection)
            {
                wheel.motorTorque = vInput * currentMotorTorque;
                wheel.brakeTorque = 0f;
            }
            else if (isAccelerating && !sameDirection)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = brakeTorque;
            }
            else
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = 10f; 
            }
        }
    }
}