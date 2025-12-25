using UnityEngine;

public class VehicleAerodynamics : MonoBehaviour
{
    // Placeholder for now
    
    public float dragCoefficient = 0.3f;
    public float downforceCoefficient = 0.1f;
    public float frontalArea = 2.2f;
    
    public void ApplyAerodynamics(Rigidbody rb)
    {
        // Simple aero model
        // F_drag = 0.5 * rho * Cd * A * v^2
        // F_down = 0.5 * rho * Cl * A * v^2
        
        float rho = 1.225f; // Air density
        float speed = rb.linearVelocity.magnitude;
        float dynamicPressure = 0.5f * rho * speed * speed;
        
        Vector3 velocityDir = rb.linearVelocity.normalized;
        Vector3 dragForce = -velocityDir * dynamicPressure * dragCoefficient * frontalArea;
        Vector3 downforce = -transform.up * dynamicPressure * downforceCoefficient * frontalArea;
        
        rb.AddForce(dragForce);
        rb.AddForce(downforce);
    }
}
