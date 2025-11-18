using UnityEngine;
using Windows.Kinect;

/// <summary>
/// 体感倾斜检测 + 控制玩家左右移动：
/// - 检测玩家身体相对“初始站姿”的左右倾斜程度
/// - 给出当前倾斜方向（左 / 右 / 无）
/// - 根据倾斜把挂着本脚本的 Player 在世界坐标的 X 轴上左右移动
/// </summary>
public class KinectLeanDetector : MonoBehaviour
{
    [Header("Kinect 设置")]
    [Tooltip("BodySourceManager 对象，用于获取 Kinect 数据")]
    public GameObject bodySourceManager;

    [Header("倾斜检测")]
    [Tooltip("左右倾斜的灵敏度阈值（小于这个值视为没倾斜）")]
    public float leanSensitivity = 0.1f;

    [Tooltip("玩家左右倾斜多少（米）就算“到达最左/最右”")]
    public float maxLeanForFullMove = 0.3f;  // 比如 0.3m 左右

    [Tooltip("是否在控制台打印倾斜检测信息")]
    public bool enableLeanDebug = true;

    [Tooltip("打印倾斜信息的频率（秒），0 表示每帧都打印")]
    public float debugPrintInterval = 0.5f;

    /// <summary>
    /// 当前相对中立姿势的 X 偏移（正数 = 向右倾，负数 = 向左倾）
    /// </summary>
    [Tooltip("当前相对中立姿势的 X 偏移（只读，用于观察）")]
    public float currentLeanValue = 0f;

    public enum LeanDirection { None, Left, Right }

    [Tooltip("当前检测到的倾斜方向")]
    public LeanDirection currentLeanDirection = LeanDirection.None;

    [Header("玩家左右移动")]
    [Tooltip("玩家在世界坐标 X 轴上最多能移动的距离（左右各这么多）")]
    public float horizontalMoveRange = 3f;

    [Tooltip("玩家位置插值的平滑速度")]
    public float moveSmoothSpeed = 5f;

    [Tooltip("是否使用 CharacterController 移动（如果存在）")]
    public bool useCharacterController = true;

    private BodySourceManager _bodyManager;
    private CharacterController _characterController;

    // 记录“中立站姿”的 Kinect X，用来当 0 点
    private float _neutralSpineX = 0f;
    private bool _hasNeutral = false;

    // 玩家初始的世界 X 坐标（中间位置）
    private float _initialPlayerX = 0f;

    // 用于检测方向变化和定时打印
    private LeanDirection _lastLeanDirection = LeanDirection.None;
    private float _lastDebugPrintTime = 0f;

    void Start()
    {
        // 记录玩家一开始的 X，当成"中间位置"
        _initialPlayerX = transform.position.x;

        // 检查是否有 CharacterController
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null && useCharacterController)
        {
            Debug.LogWarning("KinectLeanDetector: 未找到 CharacterController，将使用 transform.position 移动。");
            useCharacterController = false;
        }

        // 获取 BodySourceManager
        if (bodySourceManager != null)
        {
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
        }
        else
        {
            GameObject manager = GameObject.FindGameObjectWithTag("BodySourceManager");
            if (manager != null)
            {
                _bodyManager = manager.GetComponent<BodySourceManager>();
            }
        }

        if (_bodyManager == null)
        {
            Debug.LogWarning("KinectLeanDetector: 找不到 BodySourceManager，无法进行体感检测。");
        }
    }

    void Update()
    {
        if (_bodyManager == null) return;

        Body[] bodies = _bodyManager.GetData();
        if (bodies == null) return;

        // 找到第一个被追踪的身体
        Body trackedBody = null;
        foreach (Body body in bodies)
        {
            if (body != null && body.IsTracked)
            {
                trackedBody = body;
                break;
            }
        }

        if (trackedBody == null) return;

        HandleLeanDetectionAndMovement(trackedBody);
    }

    /// <summary>
    /// 检测倾斜 + 按倾斜移动玩家
    /// </summary>
    void HandleLeanDetectionAndMovement(Body body)
    {
        CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;

        // 第一次检测到身体时，把当前 Kinect X 当作“中立姿势”
        if (!_hasNeutral)
        {
            _neutralSpineX = spineBase.X;
            _hasNeutral = true;
            Debug.Log($"[体感检测] 记录中立站姿 X：{_neutralSpineX:F3}");
        }

        // 相对中立位置的偏移
        float offsetX = spineBase.X - _neutralSpineX;

        // 应用灵敏度阈值，避免微小抖动
        if (Mathf.Abs(offsetX) < leanSensitivity)
        {
            currentLeanValue = 0f;
            currentLeanDirection = LeanDirection.None;
        }
        else
        {
            currentLeanValue = offsetX;
            currentLeanDirection = offsetX < 0 ? LeanDirection.Left : LeanDirection.Right;
        }

        // === 用倾斜控制玩家水平移动 ===

        // 把倾斜值映射到 [-1, 1] 区间：-1 = 最左，1 = 最右
        float normalizedLean = 0f;
        if (maxLeanForFullMove > 0.0001f)
        {
            normalizedLean = Mathf.Clamp(currentLeanValue / maxLeanForFullMove, -1f, 1f);
        }

        // 计算目标 X 位置
        float targetX = _initialPlayerX + normalizedLean * horizontalMoveRange;
        float currentX = transform.position.x;
        float deltaX = targetX - currentX;

        // 使用 CharacterController 或直接设置位置
        if (useCharacterController && _characterController != null)
        {
            // 使用 CharacterController.Move() 来移动
            // 计算平滑的移动速度（基于到目标位置的距离）
            float moveSpeed = deltaX * moveSmoothSpeed;
            moveSpeed = Mathf.Clamp(moveSpeed, -horizontalMoveRange * moveSmoothSpeed, horizontalMoveRange * moveSmoothSpeed);
            
            // 使用 CharacterController 移动（只移动 X 轴，不影响其他轴的移动）
            Vector3 move = new Vector3(moveSpeed * Time.deltaTime, 0, 0);
            _characterController.Move(move);
        }
        else
        {
            // 直接设置位置（兼容没有 CharacterController 的情况）
            Vector3 pos = transform.position;
            pos.x = Mathf.Lerp(currentX, targetX, Time.deltaTime * moveSmoothSpeed);
            transform.position = pos;
        }

        // === 调试输出 ===
        if (enableLeanDebug && currentLeanDirection != _lastLeanDirection)
        {
            string directionText = currentLeanDirection switch
            {
                LeanDirection.Left => "向左倾斜",
                LeanDirection.Right => "向右倾斜",
                _ => "回到中间"
            };

            Debug.Log($"[体感检测] 身体倾斜方向变化: {directionText} | 相对偏移值: {currentLeanValue:F3} | Normalized: {normalizedLean:F3}");
            _lastLeanDirection = currentLeanDirection;
        }

        if (enableLeanDebug)
        {
            if (debugPrintInterval > 0f)
            {
                if (Time.time - _lastDebugPrintTime >= debugPrintInterval)
                {
                    PrintLeanDebug(normalizedLean);
                    _lastDebugPrintTime = Time.time;
                }
            }
            else
            {
                PrintLeanDebug(normalizedLean);
            }
        }
    }

    void PrintLeanDebug(float normalizedLean)
    {
        if (currentLeanDirection == LeanDirection.None) return;

        string dir = currentLeanDirection == LeanDirection.Left ? "左" : "右";
        Debug.Log($"[体感检测] 当前倾斜: {dir} | 偏移值: {currentLeanValue:F3} | Normalized: {normalizedLean:F3}");
    }
}
