using UnityEngine;
using UnityEngine.Android;
using System.Collections;

public class RearCameraPass : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private Renderer quadRenderer;

    public WebCamTexture GetWebCamTexture()
    {
        return webcamTexture;
    }

    void Start()
    {
        quadRenderer = GetComponent<Renderer>();
        
        // 1. Request Camera Permission (Android specific)
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            StartCoroutine(WaitForPermission());
        }
        else
        {
            InitializeCamera();
        }
    }

    IEnumerator WaitForPermission()
    {
        // Wait until user accepts permission
        while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            yield return null;
        }
        InitializeCamera();
    }

    void InitializeCamera()
    {
        // 2. Find the Rear Camera
        WebCamDevice[] devices = WebCamTexture.devices;
        string backCamName = "";
        
        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                backCamName = devices[i].name;
                break;
            }
        }

        // 3. Start the Camera
        if (webcamTexture == null)
        {
            // Use 1280x720 or 1920x1080 for balance between quality and performance
            webcamTexture = new WebCamTexture(backCamName, 1280, 720);
        }

        quadRenderer.material.mainTexture = webcamTexture;
        webcamTexture.Play();
    }

    void Update()
    {
        // 4. Continuously fix orientation (Crucial for Mobile VR)
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            // Match the rotation of the camera feed
            // -webcamTexture.videoRotationAngle corrects the rotation
            transform.localRotation = Quaternion.Euler(0, 0, -webcamTexture.videoRotationAngle);

            // Fix mirroring if necessary (Front cams are usually mirrored, back cams usually aren't)
            // But sometimes Unity defaults weirdly, so we check explicitly.
            float scaleY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
            
            // Adjust scale based on the actual texture aspect ratio to prevent stretching
            float aspectRatio = (float)webcamTexture.width / (float)webcamTexture.height;
            
            // If rotation is 90 or 270 (portrait feed), we swap aspect ratio logic
            if (Mathf.Abs(webcamTexture.videoRotationAngle) == 90)
            {
                // In portrait, width/height are swapped visually
                transform.localScale = new Vector3(9f * (1f/aspectRatio), 9f * scaleY, 1f); 
            }
            else
            {
                transform.localScale = new Vector3(9f * aspectRatio, 9f * scaleY, 1f);
            }
        }
    }
}