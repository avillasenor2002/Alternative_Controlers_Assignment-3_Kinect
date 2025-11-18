using UnityEngine;

public class CameraShake : MonoBehaviour
{
    Vector3 originalPos;
    float shakeDuration = 0f;
    float shakeIntensity = 0.5f;

    private void Start()
    {
        originalPos = transform.localPosition;
    }

    public void Shake(float duration, float intensity)
    {
        shakeDuration = duration;
        shakeIntensity = intensity;
    }

    private void Update()
    {
        if (shakeDuration > 0)
        {
            transform.localPosition = originalPos + (Random.insideUnitSphere * shakeIntensity);
            shakeDuration -= Time.deltaTime;
        }
        else
        {
            transform.localPosition = originalPos;
        }
    }
}
