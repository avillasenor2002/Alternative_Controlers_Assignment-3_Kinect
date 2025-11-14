using UnityEngine;
using Windows.Kinect;

/// <summary>
/// 只做体感倾斜检测的控制器：
/// - 检测玩家身体相对“初始站姿”的左右倾斜程度
/// - 给出当前倾斜方向（左 / 右 / 无）
/// - 可选择在控制台打印调试信息
/// </summary>
public class KinectLeanDetector : MonoBehaviour
{
    [Header("Kinect 设置")]
    [Tooltip("BodySourceManager 对象，用于获取 Kinect 数据")]
    public GameObject bodySourceManager;

    [Header("倾斜检测")]
    [Tooltip("左右倾斜的灵敏度阈值（小于这个值视为没倾斜）")]
    public float leanSensitivity = 0.1f;

    [Tooltip("是否在控制台打印倾斜检测信息")]
    public bool enableLeanDebug = true;

    [Tooltip("打印倾斜信息的频率（秒），0 表示每帧都打印")]
    public float debugPrintInterval = 0.5f;

    /// <summary>
    /// 当前相对中立姿势的 X 偏移（正数 = 向右倾，负数 = 向左倾）
    /// </summary>
    [Tooltip("当前相对中立姿势的 X 偏移（只读，用于观察）")]
    public float currentLeanValue = 0f;

    /// <summary>
    /// 当前倾斜方向
    /// </summary>
    public enum LeanDirection { None, Left, Right }

    [Tooltip("当前检测到的倾斜方向")]
    public LeanDirection currentLeanDirection = LeanDirection.None;

    private BodySourceManager _bodyManager;

    // 记录“中立站姿”的 X 位置，用来当 0 点
    private float _neutralSpineX = 0f;
    private bool _hasNeutral = false;

    // 用于检测方向变化和定时打印
    private LeanDirection _lastLeanDirection = LeanDirection.None;
    private float _lastDebugPrintTime = 0f;

    void Start()
    {
        // 获取 BodySourceManager
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

        HandleLeanDetection(trackedBody);
    }

    /// <summary>
    /// 只检测左右倾斜程度和方向
    /// </summary>
    void HandleLeanDetection(Body body)
    {
        // 使用 SpineBase 作为身体中心点
        CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;

        // 第一次检测到身体时，把当前 X 当作“中立站姿”
        if (!_hasNeutral)
        {
            _neutralSpineX = spineBase.X;
            _hasNeutral = true;
            Debug.Log($"[体感检测] 记录中立站姿 X：{_neutralSpineX:F3}");
        }

        // 相对中立位置的偏移
        float offsetX = spineBase.X - _neutralSpineX;

        // 应用灵敏度阈值
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

        // 方向变化时打印一次
        if (enableLeanDebug && currentLeanDirection != _lastLeanDirection)
        {
            string directionText = currentLeanDirection switch
            {
                LeanDirection.Left => "向左倾斜",
                LeanDirection.Right => "向右倾斜",
                _ => "回到中间"
            };

            Debug.Log($"[体感检测] 身体倾斜方向变化: {directionText} | 相对偏移值: {currentLeanValue:F3}");
            _lastLeanDirection = currentLeanDirection;
        }

        // 定时打印
        if (enableLeanDebug)
        {
            if (debugPrintInterval > 0f)
            {
                if (Time.time - _lastDebugPrintTime >= debugPrintInterval)
                {
                    PrintLeanDebug();
                    _lastDebugPrintTime = Time.time;
                }
            }
            else
            {
                // 每帧都打印（可能较多）
                PrintLeanDebug();
            }
        }
    }

    void PrintLeanDebug()
    {
        if (currentLeanDirection == LeanDirection.None) return;

        string dir = currentLeanDirection == LeanDirection.Left ? "左" : "右";
        Debug.Log($"[体感检测] 当前倾斜: {dir} | 偏移值: {currentLeanValue:F3}");
    }
}
