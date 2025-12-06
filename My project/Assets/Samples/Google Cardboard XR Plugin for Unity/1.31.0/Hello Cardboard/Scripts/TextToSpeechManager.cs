using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class TextToSpeechManager : MonoBehaviour
{
    public static TextToSpeechManager Instance { get; private set; }

    public enum TTSMode
    {
        Native,
        Web
    }

    [Header("General Settings")]
    [SerializeField] private TTSMode _mode = TTSMode.Native;

    [Header("Web API Settings")]
    [SerializeField] private string _apiUrl = "https://api.example.com/tts";
    [SerializeField] private string _apiKey = "YOUR_API_KEY";

    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;

    // Events
    public event Action<string, int> OnSpeakLine;
    public event Action OnSpeakComplete;

    private Queue<string> _linesQueue = new Queue<string>();
    private bool _isSpeaking = false;
    private int _currentLineIndex = 0;

    // Native Android Variables
    private AndroidJavaObject _ttsObject;
    private AndroidJavaObject _context;

    // Native iOS Imports
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _TTS_Speak(string text);
    [DllImport("__Internal")]
    private static extern void _TTS_Stop();
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (_mode == TTSMode.Native)
        {
            InitializeNativeTTS();
        }
    }

    private void InitializeNativeTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                _context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            _ttsObject = new AndroidJavaObject("android.speech.tts.TextToSpeech", _context, new TTSOnInitListener(this));
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTS] Failed to initialize Android TTS: {e.Message}");
        }
#endif
    }

    public void Speak(string text)
    {
        Stop();
        _currentLineIndex = 0;

        string[] lines = text.Split(new[] { '\n', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _linesQueue.Enqueue(line.Trim());
            }
        }

        if (_linesQueue.Count > 0)
        {
            ProcessNextLine();
        }
    }

    public void Stop()
    {
        StopAllCoroutines();
        _linesQueue.Clear();
        _isSpeaking = false;
        _audioSource.Stop();

        if (_mode == TTSMode.Native)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _ttsObject?.Call("stop");
#elif UNITY_IOS && !UNITY_EDITOR
            _TTS_Stop();
#endif
        }
    }

    private void ProcessNextLine()
    {
        if (_linesQueue.Count == 0)
        {
            _isSpeaking = false;
            OnSpeakComplete?.Invoke();
            return;
        }

        _isSpeaking = true;
        string currentLine = _linesQueue.Dequeue();
        OnSpeakLine?.Invoke(currentLine, _currentLineIndex);

        if (_mode == TTSMode.Web)
        {
            StartCoroutine(FetchAndPlayAudioWeb(currentLine));
        }
        else
        {
            SpeakNative(currentLine);
        }
        
        _currentLineIndex++;
    }

    private void SpeakNative(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android TTS Speak
        // Queue mode: 0 = QUEUE_FLUSH (interrupt), 1 = QUEUE_ADD
        // We handle queuing manually, so we can use FLUSH for immediate response or ADD if we wanted to dump it all.
        // But since we want line-by-line events, we speak one line, wait for callback, then next.
        
        var paramsMap = new AndroidJavaObject("java.util.HashMap");
        paramsMap.Call<string>("put", "utteranceId", "id_" + _currentLineIndex);
        
        _ttsObject.Call<int>("speak", text, 0, paramsMap); 

#elif UNITY_IOS && !UNITY_EDITOR
        _TTS_Speak(text);
#else
        Debug.LogWarning($"[TTS] Native Mode selected but not on device. Mocking speech for: {text}");
        // Mock delay for editor testing
        StartCoroutine(MockSpeechDelay(text));
#endif
    }

    private IEnumerator MockSpeechDelay(string text)
    {
        yield return new WaitForSeconds(1.0f); // Fake speaking time
        OnNativeSpeakComplete("");
    }

    // --- Web Implementation ---
    private IEnumerator FetchAndPlayAudioWeb(string text)
    {
        // ... (Existing Web Implementation Logic) ...
        // Re-using the logic from previous step but simplified for this context
        string jsonBody = $"{{\"text\":\"{text}\", \"voice\":\"en-US-Standard-A\"}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest www = new UnityWebRequest(_apiUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(_apiUrl, AudioType.MPEG);
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                _audioSource.clip = clip;
                _audioSource.Play();
                yield return new WaitForSeconds(clip.length);
                ProcessNextLine(); // Move to next line after web audio finishes
            }
            else
            {
                Debug.LogError($"[TTS] Web Error: {www.error}");
                ProcessNextLine(); // Skip on error
            }
        }
    }

    // --- Native Callbacks ---

    // Called by iOS (UnitySendMessage) and Android Listener
    public void OnNativeSpeakStart(string msg)
    {
        // Optional: verify msg matches current text
    }

    public void OnNativeSpeakComplete(string msg)
    {
        // Trigger next line
        ProcessNextLine();
    }

    // --- Android Classes ---
    
    // Listener for TTS Initialization
    class TTSOnInitListener : AndroidJavaProxy
    {
        private TextToSpeechManager _manager;
        public TTSOnInitListener(TextToSpeechManager manager) : base("android.speech.tts.TextToSpeech$OnInitListener") 
        {
            _manager = manager;
        }

        public void onInit(int status)
        {
            if (status == 0) // SUCCESS
            {
                Debug.Log("[TTS] Android TTS Initialized Successfully");
                _manager.SetAndroidUtteranceListener();
            }
            else
            {
                Debug.LogError($"[TTS] Android TTS Initialization Failed: {status}");
            }
        }
    }

    public void SetAndroidUtteranceListener()
    {
        if (_ttsObject != null)
        {
            _ttsObject.Call<int>("setOnUtteranceProgressListener", new TTSUtteranceListener(this));
        }
    }

    // Listener for Utterance Progress (Start, Done, Error)
    class TTSUtteranceListener : AndroidJavaProxy
    {
        private TextToSpeechManager _manager;
        public TTSUtteranceListener(TextToSpeechManager manager) : base("android.speech.tts.UtteranceProgressListener") 
        {
            _manager = manager;
        }

        public void onStart(string utteranceId)
        {
            // Run on main thread if needed, though events usually safe
             _manager.OnNativeSpeakStart(utteranceId);
        }

        public void onDone(string utteranceId)
        {
            // Unity API calls must be on main thread
            MainThreadDispatcher.Enqueue(() => _manager.OnNativeSpeakComplete(utteranceId));
        }

        public void onError(string utteranceId)
        {
            Debug.LogError($"[TTS] Android Error on utterance: {utteranceId}");
            MainThreadDispatcher.Enqueue(() => _manager.OnNativeSpeakComplete(utteranceId)); // Skip on error
        }
    }
}

// Simple MainThreadDispatcher to handle Android background threads calling back into Unity
public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    [RuntimeInitializeOnLoadMethod]
    static void Initialize()
    {
        GameObject go = new GameObject("MainThreadDispatcher");
        go.AddComponent<MainThreadDispatcher>();
        DontDestroyOnLoad(go);
    }
}
