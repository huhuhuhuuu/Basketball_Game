using UnityEngine;

namespace BasketballCourt
{
    /// <summary>
    /// Free-look camera for walking around the court.
    /// <para>
    /// Desktop:  W A S D / arrow keys move, Q / E (or Ctrl / Space) move down / up,
    ///           hold the RIGHT mouse button and move the mouse to look around,
    ///           Shift = fast, mouse wheel = change base speed, R = reset to the start view.
    /// Touch:    one finger drag = look, two finger drag = move (forward/back and strafe).
    /// </para>
    /// Uses the classic Input API (the project has "Both" input handlers enabled).
    /// </summary>
    public class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float fastMultiplier = 3.5f;
        public float minSpeed = 1f;
        public float maxSpeed = 40f;

        [Header("Look")]
        public float lookSensitivity = 2.2f;
        public float touchLookSensitivity = 0.18f;
        public bool holdRightMouseToLook = true;
        [Range(0f, 89f)] public float maxPitch = 85f;

        [Header("Limits")]
        public float minHeight = 0.3f;
        public float maxHeight = 60f;

        float _yaw, _pitch;
        Vector3 _startPos;
        Quaternion _startRot;

        void Start()
        {
            _startPos = transform.position;
            _startRot = transform.rotation;
            SyncAnglesFromTransform();
        }

        void SyncAnglesFromTransform()
        {
            Vector3 e = transform.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                transform.position = _startPos;
                transform.rotation = _startRot;
                SyncAnglesFromTransform();
            }

            HandleLook();
            HandleMove();
        }

        void HandleLook()
        {
            float dx = 0f, dy = 0f;

            if (Input.touchSupported && Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    dx = t.deltaPosition.x * touchLookSensitivity;
                    dy = t.deltaPosition.y * touchLookSensitivity;
                }
            }
            else
            {
                bool looking = !holdRightMouseToLook || Input.GetMouseButton(1);
                if (looking)
                {
                    dx = Input.GetAxis("Mouse X") * lookSensitivity;
                    dy = Input.GetAxis("Mouse Y") * lookSensitivity;
                }
                if (holdRightMouseToLook)
                {
                    if (Input.GetMouseButtonDown(1)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                    if (Input.GetMouseButtonUp(1))   { Cursor.lockState = CursorLockMode.None;   Cursor.visible = true; }
                }
            }

            if (dx != 0f || dy != 0f)
            {
                _yaw += dx;
                _pitch = Mathf.Clamp(_pitch - dy, -maxPitch, maxPitch);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }

        void HandleMove()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
                moveSpeed = Mathf.Clamp(moveSpeed * (1f + scroll * 0.5f), minSpeed, maxSpeed);

            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  dir += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  dir += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))      dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) dir += Vector3.down;

            if (Input.touchSupported && Input.touchCount == 2)
            {
                Touch t = Input.GetTouch(1);
                if (t.phase == TouchPhase.Moved)
                {
                    dir += Vector3.forward * (t.deltaPosition.y * 0.02f);
                    dir += Vector3.right * (t.deltaPosition.x * 0.02f);
                }
            }

            if (dir == Vector3.zero) return;

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? fastMultiplier : 1f);
            // Move in the camera's yaw frame (forward stays level so W does not dive into the floor),
            // vertical moves stay world-up.
            Vector3 flatForward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 flatRight   = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 delta = (flatForward * dir.z + flatRight * dir.x + Vector3.up * dir.y) * speed * Time.deltaTime;

            Vector3 p = transform.position + delta;
            p.y = Mathf.Clamp(p.y, minHeight, maxHeight);
            transform.position = p;
        }
    }
}
