using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GuiUpdateBehaviour : VehicleBehaviour
{
    public TMP_Text engineRPM;
    public TMP_Text avgRPM;

    // Custom start logic since regular Start cannot be used (seriously you'll get an error!)
    protected override void OnStart()
    {

    }

    void Update()
    {
        engineRPM.text = $"Engine RPM: {vehicle.EngineRPM}";
        avgRPM.text = $"Avarage RPM: {vehicle.AverageWheelRPM}";
    }
}