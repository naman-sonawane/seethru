using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class FirebaseReader : MonoBehaviour
{
    public TextMeshProUGUI displayText;
    private string firebaseURL = "https://seethru-e860f-default-rtdb.firebaseio.com/writtenWord.json";

    public float refreshInterval = 2f; // seconds between fetches

    void Start()
    {
        // StartCoroutine(PollFirebase());
    }

    IEnumerator PollFirebase()
    {
        while (true)
        {
            yield return StartCoroutine(GetFirebaseData());
            yield return new WaitForSeconds(refreshInterval);
        }
    }

    public IEnumerator GetFirebaseData()
    {
        UnityWebRequest request = UnityWebRequest.Get(firebaseURL);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            string value = jsonResponse.Trim('"');

            displayText.text = value;
            Debug.Log("Firebase value: " + value);
        }
        else
        {
            displayText.text = "Error: " + request.error;
            Debug.LogError("Firebase error: " + request.error);
        }
    }
}