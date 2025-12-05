using UnityEngine;

public class VehicleData : MonoBehaviour
{
    public class VehicleDataReference
    {
        private VehicleController controller;
        private Rigidbody rb;

        public VehicleDataReference(VehicleController controller, Rigidbody rb)
        {
            this.controller = controller;
            this.rb = rb;
        }

        public float SpeedKPH => rb.linearVelocity.magnitude * 3.6f;
        public float SpeedMPH => rb.linearVelocity.magnitude * 2.23694f;

        public float EngineRPM { get; private set; }
        public float AverageWheelRPM { get; private set; }
        public float GroundedWheelRPM { get; private set; }

        public float NormalisedEngineRPM => Mathf.InverseLerp(800f, MaxEngineRPM, EngineRPM);

        public float MaxEngineRPM { get; private set; } = 7500f;
        public float Horsepower => controller.motorTorque * 0.0013f; // I lowk asked the clanker to make me shit up
        public float CurrentHorsepower => NormalisedEngineRPM * Horsepower;
        public float Torque => controller.motorTorque;
        public float CurrentTorque => Mathf.Lerp(0f, Torque, NormalisedEngineRPM);

        public bool IsGrounded => controller.WheelCollider_FL.isGrounded
                                || controller.WheelCollider_FR.isGrounded
                                || controller.WheelCollider_RL.isGrounded
                                || controller.WheelCollider_RR.isGrounded;

        public bool IsAllWheelsGrounded => controller.WheelCollider_FL.isGrounded
                                        && controller.WheelCollider_FR.isGrounded
                                        && controller.WheelCollider_RL.isGrounded
                                        && controller.WheelCollider_RR.isGrounded;

        public float WheelRPM(int index) => index switch
        {
            0 => controller.WheelCollider_FL.rpm,
            1 => controller.WheelCollider_FR.rpm,
            2 => controller.WheelCollider_RL.rpm,
            3 => controller.WheelCollider_RR.rpm,
            _ => 0f
        };

        public float LongitudinalSlip { get; private set; }
        public float LateralSlip { get; private set; }
        public float SlipRatio => Mathf.Abs(LongitudinalSlip);

        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteeringInput { get; private set; }

        public float NormalisedThrottle => Mathf.Clamp01(ThrottleInput);
        public float NormalisedSteering => Mathf.Abs(SteeringInput);
        public float NormalisedBrake => Mathf.Clamp01(BrakeInput);

        public bool IsBraking => BrakeInput > 0.1f;
        public bool IsAccelerating => ThrottleInput > 0.1f;
        public bool IsCoasting => !IsBraking && !IsAccelerating;

        public void UpdateTelemetry(Vector2 input)
        {
            ThrottleInput = input.y;
            BrakeInput = input.y < 0 ? -input.y : 0;
            SteeringInput = input.x;

            ReadWheels();
            ComputeEngineRPM();
            EstimateSlip();
        }

        private void ReadWheels()
        {
            var wheels = new[]
            {
            controller.WheelCollider_FL, controller.WheelCollider_FR,
            controller.WheelCollider_RL, controller.WheelCollider_RR
        };

            float sum = 0f;
            float groundedSum = 0f;
            int groundedCount = 0;

            foreach (var wheel in wheels)
            {
                float rpm = Mathf.Abs(wheel.rpm);
                sum += rpm;

                WheelHit hit;
                if (wheel.GetGroundHit(out hit))
                {
                    groundedSum += rpm;
                    groundedCount++;
                }
            }

            AverageWheelRPM = sum / wheels.Length;
            GroundedWheelRPM = groundedCount > 0 ? groundedSum / groundedCount : 0;
        }

        private void ComputeEngineRPM()
        {
            float wheelSpeedLinear = AverageWheelRPM * controller.AverageWheelRadius * 0.10472f;

            float engineRedline = 4500f;
            float engineIdle = 800f;

            float rpmFactor = Mathf.Clamp01(wheelSpeedLinear / controller.MaxVehicleSpeed);

            float rawRPM = Mathf.Lerp(engineIdle, engineRedline, rpmFactor);

            // EngineRPM = Mathf.Lerp(EngineRPM, rawRPM, 0.2f);
            EngineRPM = rawRPM;
        }

        private void EstimateSlip()
        {
            float wheelSpeed = AverageWheelRPM * (2f * Mathf.PI * controller.WheelCollider_FL.radius);
            float carSpeed = rb.linearVelocity.magnitude;

            LongitudinalSlip = (wheelSpeed - carSpeed) / Mathf.Max(carSpeed, 1f);
            LateralSlip = Mathf.Clamp(rb.angularVelocity.y * 0.1f, -1f, 1f);
        }
    }

    VehicleDataReference data;
    public VehicleDataReference vehicleDataReference => data;

    VehicleController controller;

    void Start()
    {
        controller = GetComponent<VehicleController>();
        data = new VehicleDataReference(controller, GetComponent<Rigidbody>());
    }

    void FixedUpdate()
    {
        data.UpdateTelemetry(controller.cachedInput);
    }
}
