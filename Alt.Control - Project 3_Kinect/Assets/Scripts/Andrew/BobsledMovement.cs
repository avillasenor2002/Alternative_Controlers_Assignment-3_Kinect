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
    private KinectLeanDetector kinectLeanDetector;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // 查找 KinectLeanDetector 组件（可能在同一个 GameObject 或其他地方）
        kinectLeanDetector = GetComponent<KinectLeanDetector>();
        if (kinectLeanDetector == null)
        {
            kinectLeanDetector = FindObjectOfType<KinectLeanDetector>();
        }
    }

    void Update()
    {
        float input = 0;

        // 优先检查 Kinect 倾斜检测
        bool kinectLeft = false;
        bool kinectRight = false;
        
        if (kinectLeanDetector != null)
        {
            if (kinectLeanDetector.currentLeanDirection == KinectLeanDetector.LeanDirection.Left)
            {
                kinectLeft = true;  // 向左倾斜 = 按 A 键
            }
            else if (kinectLeanDetector.currentLeanDirection == KinectLeanDetector.LeanDirection.Right)
            {
                kinectRight = true;  // 向右倾斜 = 按 D 键
            }
        }

        // 键盘输入（如果 Kinect 没有输入，则使用键盘）
        if (kinectLeft || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            input = -1;
        else if (kinectRight || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
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
