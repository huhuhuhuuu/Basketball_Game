using UnityEngine;

public class ScoreDetector : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name != "Ball") return;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        // Only count if ball is moving downward — going through the hoop, not bouncing up
        if (rb != null && rb.linearVelocity.y < 0 && GameManager.Instance != null)
            GameManager.Instance.AddScore();
    }
}
