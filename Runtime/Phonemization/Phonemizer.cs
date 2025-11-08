using UnityEngine;

namespace PhonemeFlow
{
    public static class Phonemizer
    {

        public static string GetPhonemes(string text)
        {

            string phonemes = PhonemeFlowAdapter.GetPhonemes(text);

            if (string.IsNullOrEmpty(phonemes))
            {
                Debug.Log("[Phonemizer] eSpeak-ng returned empty phoneme string!");
                return "";
            }

            //Debug.Log($"[Phonemizer] Phonemes: {phonemes}");
            return phonemes;
        }

        public static string NormalizePhonemes(string phonemes)
        {
            return phonemes
                .Replace("'", "")  // Remove stress markers
                .Replace("@", "ə") // Convert schwa if needed
                .Replace("#", "")  // Remove special symbols
                .Replace("U", "ʊ") // Match vowel notation
                .Replace("E", "ɛ") // Match vowel notation
                .Replace("D", "ð") // Match vowel notation
                .Replace("I", "ɪ") // Match vowel notation
                .Trim();
        }
    }
}
