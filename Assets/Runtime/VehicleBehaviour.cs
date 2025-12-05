using UnityEngine;

public abstract class VehicleBehaviour : MonoBehaviour
{
    VehicleData vehicleData;
    public VehicleData.VehicleDataReference vehicle => vehicleData.vehicleDataReference;

    void Start()
    {
        vehicleData = GetComponentInParent<VehicleData>(true);
        Debug.Log(vehicleData);
        OnStart();
    }

    void Awake()
    {
        OnAwake();
    }

    protected virtual void OnStart() { }

    protected virtual void OnAwake() { }
}
