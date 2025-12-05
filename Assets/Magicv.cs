using UnityEngine;

public class Magicv : VehicleBehaviour
{
    protected override void OnStart()
    {

    }

    void Update()
    {
        Debug.Log(vehicle.EngineRPM);
        Debug.Log(vehicle.AverageWheelRPM);
    }
}