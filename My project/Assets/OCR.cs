using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using TMPro;

public class OCR : MonoBehaviour
{
    public RearCameraPass rearCameraPass; // Assign in Inspector
    public TextMeshProUGUI resultText; // Assign in Inspector
    
    [SerializeField] private string openAIKey = "api-key"; 
    private string openAIUrl = "https://api.openai.com/v1/chat/completions";

    public void CaptureAndRecognize()
    {
        StartCoroutine(ProcessImage());
    }

    string bolded(string input)
    {
        // first word of every sentence is bolded
        StringBuilder sb = new StringBuilder();
        bool newSentence = true;
        foreach (char c in input)
        {
            if (newSentence && !char.IsWhiteSpace(c))
            {
                sb.Append("<b>");
                sb.Append(c);
                sb.Append("</b>");
                newSentence = false;
            }
            else
            {
                sb.Append(c);
            }

            if (c == '.' || c == '!' || c == '?')
            {
                newSentence = true;
            }
        }

        return sb.ToString();
    }

    IEnumerator ProcessImage()
    {
        if (rearCameraPass == null)
        {
            Debug.LogError("RearCameraPass reference not set in OCR script.");
            yield break;
        }

        WebCamTexture webcamTexture = rearCameraPass.GetWebCamTexture();
        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            Debug.LogError("Camera not ready");
            yield break;
        }

        // Create a Texture2D from the WebCamTexture
        Texture2D snap = new Texture2D(webcamTexture.width, webcamTexture.height);
        snap.SetPixels(webcamTexture.GetPixels());
        snap.Apply();

        // Encode to JPG
        byte[] bytes = snap.EncodeToJPG();
        Destroy(snap); // Cleanup

        string base64Image = System.Convert.ToBase64String(bytes);

        // Create JSON payload
        // Note: JsonUtility has limitations with nested arrays of different types, 
        // so we construct the JSON manually for the content array or use a specific structure.
        // Here we use a simplified object structure that JsonUtility can handle.
        
        OpenAIRequest req = new OpenAIRequest();
        req.model = "gpt-4.1";
        req.max_tokens = 300;
        
        req.messages = new Message[] {
            new Message {
                role = "user",
                content = new Content[] {
                    new Content { type = "text", text = "Read the text in this image. Return ONLY the text found, no conversational filler nor formatting (no ```plaintext). Only the text." },
                    new Content { type = "image_url", image_url = new ImageUrl { url = $"data:image/jpeg;base64,{base64Image}" } }
                }
            }
        };

        string jsonPayload = JsonUtility.ToJson(req);
        
        // JsonUtility might not serialize the nested 'content' array correctly if it thinks it's polymorphic.
        // However, since we defined a concrete 'Content' class with all fields, it should work, 
        // but fields that are null/empty will still be serialized. 
        // OpenAI might complain about null fields. 
        // Let's clean up the JSON or use a simpler manual string construction for safety if JsonUtility fails.
        // Actually, let's try to be safe and construct the body manually to avoid null field issues with OpenAI API.
        
        string manualJson = $@"{{
            ""model"": ""gpt-4o"",
            ""messages"": [
                {{
                    ""role"": ""user"",
                    ""content"": [
                        {{
                            ""type"": ""text"",
                            ""text"": ""Read the text in this image. Return ONLY the text found, no conversational filler.""
                        }},
                        {{
                            ""type"": ""image_url"",
                            ""image_url"": {{
                                ""url"": ""data:image/jpeg;base64,{base64Image}""
                            }}
                        }}
                    ]
                }}
            ],
            ""max_tokens"": 300
        }}";

        if (resultText) resultText.text = "Analyzing...";

        using (UnityWebRequest www = new UnityWebRequest(openAIUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(manualJson);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + openAIKey);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error + ": " + www.downloadHandler.text);
                if (resultText) resultText.text = "Error: " + www.error;
            }
            else
            {
                string jsonResponse = www.downloadHandler.text;
                Debug.Log("OpenAI Response: " + jsonResponse);
                
                OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(jsonResponse);
                
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    string parsedText = response.choices[0].message.content;
                    if (resultText) resultText.text = bolded(parsedText);
                }
                else
                {
                    if (resultText) resultText.text = "No text found.";
                }
            }
        }
    }

    // Helper classes for JsonUtility (Response only, since Request is manual)
    [System.Serializable]
    public class OpenAIRequest
    {
        public string model;
        public Message[] messages;
        public int max_tokens;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public Content[] content;
    }

    [System.Serializable]
    public class Content
    {
        public string type;
        public string text;
        public ImageUrl image_url;
    }

    [System.Serializable]
    public class ImageUrl
    {
        public string url;
    }

    [System.Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    public class Choice
    {
        public MessageResponse message;
    }

    [System.Serializable]
    public class MessageResponse
    {
        public string content;
    }
}
