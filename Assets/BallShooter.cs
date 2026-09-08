using UnityEngine;

public enum TimingZone { Bad, OK, Good }

public class BallShooter : MonoBehaviour
{
    public float forwardForce = 5f;
    public float upwardForce  = 6.2f;

    Rigidbody  rb;
    Vector3    startPosition;
    Quaternion startRotation;
    bool  hasShot    = false;
    float resetTimer = 0f;

    Camera mainCam;
    bool   isFirstPerson = false;

    static readonly Vector3 tpCamPos = new Vector3(0f, 3f, 5f);
    static readonly Vector3 fpCamPos = new Vector3(0f, 2.2f, 7.8f);
    static readonly Vector3 hoopPos  = new Vector3(0f, 3.05f, 12.6f);

    public bool HasShot => hasShot;

    void Start()
    {
        rb             = GetComponent<Rigidbody>();
        rb.isKinematic = true;          // hold ball in place until shot
        startPosition  = transform.position;
        startRotation  = transform.rotation;
        mainCam        = Camera.main;
        SetCamera(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) ResetBall();
        if (Input.GetKeyDown(KeyCode.C)) ToggleCamera();

        if (hasShot)
        {
            resetTimer += Time.deltaTime;
            if (resetTimer > 4f && rb.linearVelocity.magnitude < 0.5f)
                ResetBall();
        }
    }

    public void ShootWithTiming(TimingZone zone)
    {
        if (hasShot) return;

        float fwd, up;
        switch (zone)
        {
            case TimingZone.Good:
                fwd = forwardForce;
                up  = upwardForce;
                break;
            case TimingZone.OK:
                float off = Random.Range(-1.2f, 1.2f);
                fwd = forwardForce - 1.5f + off;
                up  = upwardForce  - 0.8f + off * 0.4f;
                break;
            default: // Bad — airball or brick
                bool airball = Random.value > 0.5f;
                fwd = airball ? forwardForce - 4f : forwardForce + 3f;
                up  = airball ? upwardForce  - 2f : upwardForce  + 1.5f;
                break;
        }

        rb.isKinematic    = false;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(new Vector3(0f, up, fwd), ForceMode.Impulse);
        hasShot    = true;
        resetTimer = 0f;
    }

    public void UpdateStartPosition(Vector3 pos)
    {
        startPosition = pos;
    }

    public void ResetBall()
    {
        rb.isKinematic    = true;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = startRotation;
        hasShot    = false;
        resetTimer = 0f;
    }

    void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        SetCamera(isFirstPerson);
    }

    void SetCamera(bool fp)
    {
        if (mainCam == null) return;
        Vector3 pos = fp ? fpCamPos : tpCamPos;
        mainCam.transform.position = pos;
        mainCam.transform.rotation = Quaternion.LookRotation(hoopPos - pos);
    }
}
