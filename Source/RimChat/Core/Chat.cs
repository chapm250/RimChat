using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using LudeonTK;
using UnityEngine;
using Verse;
using System.EnterpriseServices;
using System.Net;

namespace RimChat.Core;

public class Chat(Pawn pawn, LogEntry entry)
{
    private static readonly Regex RemoveColorTag = new("<\\/?color[^>]*>");
    public LogEntry Entry { get; } = entry;

    public async Task<bool> Vocalize(string whatWasSaid)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("xi-api-key", xiApiKey);

        var requestBody = new
        {
            text = whatWasSaid,
            model_id = "eleven_flash_v2_5"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Request WAV output for easier Unity playback
        var response = await client.PostAsync(
            "https://api.elevenlabs.io/v1/text-to-speech/JBFqnCBsd6RMkjVDRZzb?output_format=pcm_16000",
            content);

        if (!response.IsSuccessStatusCode)
        {
            Log.Message("Failed to vocalize text.");
            Log.Message($"Status Code: {response.StatusCode}");
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"Error Body: {errorBody}");
            return false;
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync();

        // Convert WAV bytes to AudioClip using WavUtility
        var audioClip = WavUtility.ToAudioClip(audioBytes, "VocalizedText");
        var audioSource = new GameObject("VocalizedAudioSource").AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.Play();

        Log.Message($"Received {audioBytes.Length} bytes of audio data.");
        return true;
    }

    public async Task<bool> Talk(bool isSelected, string chatgpt_api_key)
    {
        var text = Entry.ToGameStringFromPOV(pawn);
        var response = await GetOpenAIResponseAsync(chatgpt_api_key, text);
        // Parse the response JSON and extract output->content->text
        using var doc = JsonDocument.Parse(response);
        var outputArray = doc.RootElement.GetProperty("output");
        foreach (var outputItem in outputArray.EnumerateArray())
        {
            if (outputItem.GetProperty("type").GetString() == "message" &&
                outputItem.TryGetProperty("content", out var contentArray))
            {
                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.GetProperty("type").GetString() == "output_text" &&
                        contentItem.TryGetProperty("text", out var textElement))
                    {
                        Log.Message(textElement.GetString());
                        await Vocalize(textElement.GetString());
                        return response != null;
                    }
                }
            }
        }
        Log.Message("No output text found in response.");
        return response != null;
    }

    public async Task<string?> GetOpenAIResponseAsync(string apiKey, string input)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        string instructions = @"You are a pawn in Rimworld named Sancho talking to his wife Mando in english.
Respond to Mando in 1 - 3 sentences.
Do not reference objects as if they are nearby, just talk about them in the abstract or as memories.
Do not speak for Mando";

        var requestBody = new
        {
            model = "gpt-5",
            input = input,
            instructions = instructions,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/responses", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseBody = await response.Content.ReadAsStringAsync();
        return responseBody;
    }
}


public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavFile, string clipName = "AudioClip")
    {
        // Minimal WAV PCM parser for Unity (assumes 16-bit PCM, mono)
        int channels = 1;
        int sampleRate = 16000;
        int headerOffset = 44; // Standard WAV header size
        int sampleCount = (wavFile.Length - headerOffset) / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(wavFile[headerOffset + i * 2] | (wavFile[headerOffset + i * 2 + 1] << 8));
            samples[i] = sample / 32768f;
        }

        AudioClip audioClip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
        audioClip.SetData(samples, 0);
        return audioClip;
    }
}