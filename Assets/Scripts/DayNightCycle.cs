using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light light;
    public float RotationSpeed = 1f;

    private void Update()
    {
        light.transform.Rotate(Vector3.right, RotationSpeed * Time.deltaTime);
    }
}
