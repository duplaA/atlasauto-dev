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
        
    }
}
