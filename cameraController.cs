//using System.Collections;
//using System.Collections.Generic;
//using System.Numerics;
using UnityEngine;

public class cameraController : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 10.0f;
    [SerializeField] private float sprintMuntiplier = 2.0f;
    [SerializeField] private float smoothMoveTime = 0.2f;

    [Header("回転設定")]
    [SerializeField] private float rotationSpeed = 3.0f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float smoothRotationTime = 0.1f;

    [Header("ズーム設定")]
    [SerializeField] private float zoomSpeed = 10.0f;
    [SerializeField] private float minZoomFOV = 20.0f;
    [SerializeField] private float maxZoomFOV = 90.0f;
    [SerializeField] private float defaultFOV = 60.0f;

    [SerializeField] private bool lookAtCenter = false;
    [SerializeField] private Transform centerPoint;  // 中心

    [SerializeField] private float sphereRadius = 100f;  // 天球の半径
    [SerializeField] private bool stayInsideSphere = true;  // 天球内に留まる

    // 内部変数
    private Vector3 targetMoveAmount;
    private Vector3 moveAmount;
    private Vector2 targerRotation;
    private Vector2 rotation;
    private float targetFOV;
    private float initialFOV;
    private Camera cam;
    private bool cursorLocked = false;


    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("カメラコンポーネントが見つかりません。このスクリプトはカメラにアタッチしてください。");
            enabled = false;
            return;
        }

        initialFOV = cam.fieldOfView;
        this.targetFOV = initialFOV;

        // 初期回転値を最新カメラの向きに設定
        rotation = new Vector2(transform.eulerAngles.y, transform.eulerAngles.x);
        targerRotation = rotation;
    }

    // Update is called once per frame
    void Update()
    {
        // 右クリックを押している間だけロック
        if (Input.GetMouseButtonDown(1))
        {
            SetCursorLock(true);
        }
        else
        {
            SetCursorLock(false);
        }

        // ESCキーでアラームロックを解除
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(false);
        }

        // カメラの回転
        if (cursorLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * (invertY ? 1 : -1);

            // 上下の回転を制限
            targerRotation.y = Mathf.Clamp(targerRotation.y + mouseY, -89.9f, 89.9f);
        }

        // ゆっくりな回転
        rotation = Vector2.Lerp(rotation, targerRotation, 1 - Mathf.Exp(-smoothRotationTime * 30f * Time.deltaTime));
        transform.eulerAngles = new Vector3(rotation.y, rotation.x, 0);

        // 移動入力
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        float y = 0;

        // 上下移動
        if (Input.GetKey(KeyCode.Q))
        {
            y = -1;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            y = 1;
        }

        // スプリント
        float currentMoveSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentMoveSpeed *= sprintMuntiplier;
        }

        // 移動方向の計算
        Vector3 moveDir = new Vector3(x, y, z).normalized;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        // カメラの向きに合わせた移動
        targetMoveAmount = (forward * z + right * x + up * y) * currentMoveSpeed;

        // 慎重な移動
        moveAmount = Vector3.Lerp(moveAmount, targetMoveAmount, 1 - Mathf.Exp(-smoothMoveTime * 30f * Time.deltaTime));
        transform.position += moveAmount * Time.deltaTime;

        // ズーム
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput == 0)
        {
            targetFOV = Mathf.Clamp(targetFOV - scrollInput * zoomSpeed, minZoomFOV, maxZoomFOV);
        }

        // FOVリセット
        if (Input.GetKeyDown(KeyCode.R))
        {
            targetFOV = defaultFOV;
        }

        // ズーム（容易）
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
    }

    private void SetCursorLock(bool lookCursor)
    {
        this.cursorLocked = lookCursor;
        Cursor.lockState = lookCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lookCursor;
    }

    // 現在位置のリセット
    public void ResetPosition(Vector3 position)
    {
        transform.position = position;
        this.moveAmount = Vector3.zero;
        this.targetMoveAmount = Vector3.zero;
    }

    // 現在の回転をリセット
    public void Resetrotation(Vector3 eulerAngels)
    {
        transform.eulerAngles = eulerAngels;
        this.rotation = new Vector2(eulerAngels.y, eulerAngels.x);
        this.targerRotation = rotation;
    }

}
