using UnityEngine;
using TMPro;
using Google.XR.Cardboard;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputController : MonoBehaviour
{
    public GameObject overlay; // Assign in Inspector
    private int counter = 0;
    private bool pressed = false;

    void Start() {
        overlay.gameObject.SetActive(false);
    }

    void Update()
    {
        bool isTriggered = false;

        // Check Cardboard Trigger
        if (Api.IsTriggerPressed) isTriggered = true;

        // Check Mouse (Editor)
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) isTriggered = true;

        // Check Touch (Mobile)
        if (Input.touchCount > 0) isTriggered = true;

        if (isTriggered && !pressed)
        {
            OnScreenPressed();
            pressed = true;
        }
        else if (!isTriggered)
        {
            pressed = false;
        }
    }

    void OnScreenPressed()
    {
        counter++;
        // Handle screen press logic here
        Debug.Log("Screen press detected!");

        OCR ocr = GetComponent<OCR>();
        if (ocr != null)
        {
            ocr.CaptureAndRecognize();
        }

        // Activate the text object
        Api.Recenter();
        overlay.gameObject.SetActive(true);
    }
}