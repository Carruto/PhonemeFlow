using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PhonemeFlow; // comes from PhonemeFlow assembly

public class PhonemeTest : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI outputText;
    [SerializeField] Slider progressBar;
    [SerializeField] TextMeshProUGUI progressLabel;
    [SerializeField] string sampleText = "Hello";
    [SerializeField] string voice = "en-us";  // any voice available in phoneme-data/voices

    void Start()
    {
        ReportProgress(0f, "Preparing phonemizer...");
        StartCoroutine(InitializePhonemizer());
    }

    IEnumerator InitializePhonemizer()
    {
        const float copyProgressStart = 0.1f;
        const float copyProgressEnd = 0.7f;

        string dataPath = null;
        System.Action<float, string> copyProgress = (progress, message) =>
        {
            float mapped = Mathf.Lerp(copyProgressStart, copyProgressEnd, Mathf.Clamp01(progress));
            ReportProgress(mapped, message);
        };

        ReportProgress(0.05f, "Checking phoneme cache...");

#if UNITY_ANDROID && !UNITY_EDITOR
        var phonemeFlowAndroid = new PhonemeFlow_Android();
        PhonemeFlowAdapter.Phonemizer = phonemeFlowAndroid;
        yield return phonemeFlowAndroid.CopyPhonemeDataToPersistentPath(path => dataPath = path, copyProgress);
#elif UNITY_IOS && !UNITY_EDITOR
        var phonemeFlowiOS = new PhonemeFlow_iOS();
        PhonemeFlowAdapter.Phonemizer = phonemeFlowiOS;
        yield return phonemeFlowiOS.CopyPhonemeDataToPersistentPath(path => dataPath = path, copyProgress);
#else
        dataPath = "/phoneme-data"; // Editor/WebGL/other platforms use embedded data
        ReportProgress(copyProgressEnd, "Using embedded phoneme data");
#endif

        if (string.IsNullOrEmpty(dataPath))
        {
            ReportProgress(1f, "Failed to resolve phoneme-data path");
            Debug.LogError("[PhonemeTest] Failed to resolve phoneme-data path.");
            yield break;
        }

        ReportProgress(0.8f, "Initializing phoneme engine...");
        PhonemeFlowAdapter.Initialize(dataPath, voice);

        ReportProgress(0.9f, "Generating sample phonemes...");
        string phonemes = PhonemeFlowAdapter.GetPhonemes(sampleText);
        Debug.Log($"[PhonemeTest] {sampleText} => {phonemes}");
        if (outputText != null)
        {
            outputText.text = phonemes;
        }

        ReportProgress(1f, "PhonemeFlow ready");
        yield break;
    }

    void ReportProgress(float value, string message)
    {
        float clamped = Mathf.Clamp01(value);

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = clamped;
        }

        if (progressLabel != null && !string.IsNullOrEmpty(message))
        {
            progressLabel.gameObject.SetActive(true);
            progressLabel.text = message;
        }

        Debug.Log($"[PhonemeTest] {message} ({clamped:P0})");
    }
}
