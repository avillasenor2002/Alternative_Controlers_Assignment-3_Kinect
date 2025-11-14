using UnityEngine;
using Windows.Kinect;

/// <summary>
/// 体感捕捉游戏控制器 - 将Kinect身体追踪数据转换为游戏控制
/// 这个脚本应该附加到玩家角色上
/// </summary>
public class KinectPlayerController : MonoBehaviour
{
    [Header("Kinect设置")]
    [Tooltip("BodySourceManager对象，用于获取Kinect数据")]
    public GameObject bodySourceManager;

    [Header("移动控制")]
    [Tooltip("移动速度")]
    public float moveSpeed = 5f;
    
    [Tooltip("左右移动的灵敏度（身体左右倾斜的阈值）")]
    public float leanSensitivity = 0.2f;
    
    [Tooltip("左右移动的最大距离")]
    public float maxHorizontalMove = 3f;

    [Header("跳跃控制")]
    [Tooltip("跳跃力度")]
    public float jumpForce = 10f;
    
    [Tooltip("检测跳跃的阈值（身体向上移动的距离）")]
    public float jumpThreshold = 0.3f;
    
    [Tooltip("检测下蹲的阈值（身体向下移动的距离）")]
    public float crouchThreshold = 0.2f;

    [Header("手势控制")]
    [Tooltip("是否启用手势控制")]
    public bool enableGestureControl = true;
    
    [Tooltip("左右手举起的阈值（Y坐标）")]
    public float handRaiseThreshold = 0.5f;

    [Header("调试选项")]
    [Tooltip("是否在控制台打印倾斜检测信息")]
    public bool enableLeanDebug = true;
    
    [Tooltip("打印倾斜信息的频率（秒），0表示每帧都打印")]
    public float debugPrintInterval = 0.5f;

    private BodySourceManager _bodyManager;
    private Rigidbody _rb;
    private CharacterController _controller;
    private bool _isGrounded = true;
    
    // 用于检测跳跃和下蹲
    private float _lastSpineBaseY = 0f;
    private bool _wasGrounded = true;
    
    // 用于平滑移动
    private float _currentHorizontalPosition = 0f;
    private float _targetHorizontalPosition = 0f;
    
    // 用于检测倾斜方向变化
    private enum LeanDirection { None, Left, Right }
    private LeanDirection _lastLeanDirection = LeanDirection.None;
    private float _lastDebugPrintTime = 0f;

    void Start()
    {
        // 尝试获取BodySourceManager
        if (bodySourceManager != null)
        {
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
        }
        else
        {
            // 尝试自动查找
            GameObject manager = GameObject.FindGameObjectWithTag("BodySourceManager");
            if (manager != null)
            {
                _bodyManager = manager.GetComponent<BodySourceManager>();
            }
        }

        // 获取Rigidbody或CharacterController
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<CharacterController>();
        
        if (_rb == null && _controller == null)
        {
            Debug.LogWarning("KinectPlayerController: 需要Rigidbody或CharacterController组件！");
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

        // 处理移动控制
        HandleMovement(trackedBody);
        
        // 处理跳跃和下蹲
        HandleJumpAndCrouch(trackedBody);
        
        // 处理手势控制
        if (enableGestureControl)
        {
            HandleGestures(trackedBody);
        }
    }

    /// <summary>
    /// 处理左右移动（基于身体左右倾斜）
    /// </summary>
    void HandleMovement(Body body)
    {
        // 获取身体中心点（SpineBase）
        CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;
        
        // 计算相对于身体中心的左右偏移
        // Kinect坐标系：X轴正方向是右，负方向是左
        float horizontalOffset = spineBase.X;
        
        // 检测当前倾斜方向
        LeanDirection currentLeanDirection = LeanDirection.None;
        if (horizontalOffset < -leanSensitivity)
        {
            currentLeanDirection = LeanDirection.Left;
        }
        else if (horizontalOffset > leanSensitivity)
        {
            currentLeanDirection = LeanDirection.Right;
        }
        
        // 打印倾斜检测信息
        if (enableLeanDebug)
        {
            // 检测倾斜方向变化
            if (currentLeanDirection != _lastLeanDirection)
            {
                string directionText = "";
                switch (currentLeanDirection)
                {
                    case LeanDirection.Left:
                        directionText = "向左倾斜";
                        break;
                    case LeanDirection.Right:
                        directionText = "向右倾斜";
                        break;
                    default:
                        directionText = "回到中间";
                        break;
                }
                Debug.Log($"[体感检测] 身体倾斜方向: {directionText} | X偏移值: {horizontalOffset:F3}");
                _lastLeanDirection = currentLeanDirection;
            }
            
            // 按间隔打印实时倾斜值
            if (debugPrintInterval > 0 && Time.time - _lastDebugPrintTime >= debugPrintInterval)
            {
                if (currentLeanDirection != LeanDirection.None)
                {
                    Debug.Log($"[体感检测] 当前倾斜: X={horizontalOffset:F3} | 方向={(currentLeanDirection == LeanDirection.Left ? "左" : "右")}");
                }
                _lastDebugPrintTime = Time.time;
            }
            else if (debugPrintInterval <= 0)
            {
                // 每帧都打印（可能很频繁）
                if (currentLeanDirection != LeanDirection.None)
                {
                    Debug.Log($"[体感检测] 倾斜: X={horizontalOffset:F3} | {(currentLeanDirection == LeanDirection.Left ? "左" : "右")}");
                }
            }
        }
        
        // 应用灵敏度阈值
        if (Mathf.Abs(horizontalOffset) < leanSensitivity)
        {
            horizontalOffset = 0f;
        }
        
        // 计算目标水平位置（限制在最大范围内）
        _targetHorizontalPosition = Mathf.Clamp(horizontalOffset * maxHorizontalMove, -maxHorizontalMove, maxHorizontalMove);
        
        // 平滑移动到目标位置
        _currentHorizontalPosition = Mathf.Lerp(_currentHorizontalPosition, _targetHorizontalPosition, Time.deltaTime * moveSpeed);
        
        // 应用移动
        Vector3 newPosition = transform.position;
        newPosition.x = _currentHorizontalPosition;
        
        if (_controller != null)
        {
            _controller.Move(new Vector3(_currentHorizontalPosition - transform.position.x, 0, 0));
        }
        else if (_rb != null)
        {
            Vector3 moveDirection = new Vector3(_currentHorizontalPosition - transform.position.x, 0, 0);
            _rb.MovePosition(transform.position + moveDirection * Time.deltaTime * moveSpeed);
        }
        else
        {
            transform.position = newPosition;
        }
    }

    /// <summary>
    /// 处理跳跃和下蹲（基于身体上下移动）
    /// </summary>
    void HandleJumpAndCrouch(Body body)
    {
        CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;
        float currentY = spineBase.Y;
        
        // 检测跳跃（身体快速向上移动）
        if (_lastSpineBaseY > 0f)
        {
            float yDelta = currentY - _lastSpineBaseY;
            
            if (yDelta > jumpThreshold && _isGrounded)
            {
                Jump();
            }
        }
        
        _lastSpineBaseY = currentY;
        
        // 检测是否在地面上（可以通过脚部位置判断）
        CameraSpacePoint leftFoot = body.Joints[JointType.FootLeft].Position;
        CameraSpacePoint rightFoot = body.Joints[JointType.FootRight].Position;
        float averageFootY = (leftFoot.Y + rightFoot.Y) / 2f;
        
        _isGrounded = averageFootY < spineBase.Y + 0.1f; // 脚部在身体下方
    }

    /// <summary>
    /// 执行跳跃
    /// </summary>
    void Jump()
    {
        if (_rb != null)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        else if (_controller != null)
        {
            // CharacterController需要手动处理跳跃
            // 这里可以添加一个垂直速度变量
        }
        
        _isGrounded = false;
    }

    /// <summary>
    /// 处理手势控制（举手等）
    /// </summary>
    void HandleGestures(Body body)
    {
        CameraSpacePoint leftHand = body.Joints[JointType.HandLeft].Position;
        CameraSpacePoint rightHand = body.Joints[JointType.HandRight].Position;
        CameraSpacePoint head = body.Joints[JointType.Head].Position;
        
        // 检测左手举起
        bool leftHandRaised = leftHand.Y > head.Y + handRaiseThreshold;
        // 检测右手举起
        bool rightHandRaised = rightHand.Y > head.Y + handRaiseThreshold;
        
        // 检测手的状态（握拳/张开）
        HandState leftHandState = body.HandLeftState;
        HandState rightHandState = body.HandRightState;
        
        // 可以在这里添加手势触发的游戏逻辑
        // 例如：举手触发攻击、握拳触发抓取等
        
        if (leftHandRaised && leftHandState == HandState.Closed)
        {
            OnLeftHandFist();
        }
        
        if (rightHandRaised && rightHandState == HandState.Closed)
        {
            OnRightHandFist();
        }
    }

    /// <summary>
    /// 左手握拳事件（可以重写此方法添加自定义逻辑）
    /// </summary>
    protected virtual void OnLeftHandFist()
    {
        Debug.Log("左手握拳");
    }

    /// <summary>
    /// 右手握拳事件（可以重写此方法添加自定义逻辑）
    /// </summary>
    protected virtual void OnRightHandFist()
    {
        Debug.Log("右手握拳");
    }

    void OnDrawGizmos()
    {
        // 在Scene视图中显示移动范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position + Vector3.left * maxHorizontalMove,
            transform.position + Vector3.right * maxHorizontalMove
        );
    }
}

