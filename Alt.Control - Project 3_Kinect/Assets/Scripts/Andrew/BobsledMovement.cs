using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RunnerLateralMovement : MonoBehaviour
{
    [Header("Side Movement")]
    public float maxSideSpeed = 8f;
    public float acceleration = 12f;
    public float deceleration = 10f;

    [Header("Forward Movement")]
    public float forwardSpeed = 12f;

    [Header("Tilt Settings")]
    public float maxTiltAngle = 30f;           // How far to tilt left/right
    public float tiltSpeed = 6f;               // How fast to tilt

    private CharacterController controller;
    private float currentSideSpeed = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float input = 0;

        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            input = -1;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            input = 1;

        // Accelerating sideways
        if (input != 0)
        {
            currentSideSpeed += input * acceleration * Time.deltaTime;
            currentSideSpeed = Mathf.Clamp(currentSideSpeed, -maxSideSpeed, maxSideSpeed);
        }
        else
        {
            // Decelerate to zero when no key is held
            currentSideSpeed = Mathf.MoveTowards(
                currentSideSpeed,
                0,
                deceleration * Time.deltaTime
            );
        }

        // Move Player
        Vector3 move = new Vector3(currentSideSpeed, 0, forwardSpeed);
        controller.Move(move * Time.deltaTime);

        // TILT LOGIC
        float sidePercent = currentSideSpeed / maxSideSpeed;
        float targetTilt = -sidePercent * maxTiltAngle;        // tilt left is positive visually

        Quaternion targetRot = Quaternion.Euler(0, 0, targetTilt);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            tiltSpeed * Time.deltaTime
        );
    }
}
