using UnityEngine;

public class InputController : MonoBehaviour
{
    void Update()
    {
        // Detect touch input for mobile
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Began)
            {
                Debug.Log("Screen pressed at: " + touch.position);
                OnScreenPressed();
            }
        }
    }

    void OnScreenPressed()
    {
        // Handle screen press logic here
        Debug.Log("Screen press detected!");
    }
}