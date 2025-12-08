using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using LudeonTK;
using UnityEngine;
using Verse;
using System.EnterpriseServices;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace RimChat.Core;

public class Chat(Pawn pawn, LogEntry entry)
{
    private static readonly Regex RemoveColorTag = new("<\\/?color[^>]*>");
    public LogEntry? Entry { get; set; } = entry;

    public AudioSource? AudioSource { get; private set; }

    public Task<string>? AIChat { get; set; }

    public string? KindOfTalk { get; set; }

    public Pawn? pawn { get; set; } = pawn;

    public float MusicVol { get; set; }

    public bool MusicReset { get; set; } = true;

    public bool AlreadyPlayed { get; set; } = false;

    public bool current_up { get; set; } = false;

    public async Task<bool> Vocalize(string whatWasSaid, string voiceID)
    {
        using var client = new HttpClient();
        var xiApiKey = Settings.VoiceAPIKey.Value;
        client.DefaultRequestHeaders.Add("xi-api-key", xiApiKey);

        var requestBody = new
        {
            text = whatWasSaid,
            model_id = "eleven_turbo_v2_5"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        Log.Message($"{whatWasSaid} for the {entry} for the pawn {pawn} using the voiceid {voiceID}");
        // Request WAV output for easier Unity playback
        var response = await client.PostAsync(
            $"https://api.elevenlabs.io/v1/text-to-speech/{voiceID}?output_format=pcm_16000",
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
        audioSource.volume = 1f;
        AudioSource = audioSource;
        MusicVol = Prefs.VolumeMusic;
        Prefs.VolumeMusic = 0.05f;
        Prefs.Apply();
        Prefs.Save();
        AudioSource.Play();
        entry = null;
        MusicReset = false;

        Log.Message($"Received {audioBytes.Length} bytes of audio data.");
        return true;
    }

    public async Task<bool> VocalizeOpenAI(string whatWasSaid, string voice)
    {
        using var client = new HttpClient();
        var apiKey = Settings.TextAPIKey.Value;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var requestBody = new
        {
            model = "gpt-4o-mini-tts",
            input = whatWasSaid,
            voice = voice,
            response_format = "pcm"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        Log.Message($"{whatWasSaid} for the {entry} for the pawn {pawn} using the voice {voice}");

        var response = await client.PostAsync(
            "https://api.openai.com/v1/audio/speech",
            content);

        if (!response.IsSuccessStatusCode)
        {
            Log.Message("Failed to vocalize text with OpenAI.");
            Log.Message($"Status Code: {response.StatusCode}");
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"Error Body: {errorBody}");
            return false;
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync();

        // Convert PCM bytes to AudioClip - OpenAI returns 24kHz PCM
        var audioClip = WavUtility.ToAudioClipWithSampleRate(audioBytes, "VocalizedText", 24000);
        var audioSource = new GameObject("VocalizedAudioSource").AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = 1f;
        AudioSource = audioSource;
        MusicVol = Prefs.VolumeMusic;
        Prefs.VolumeMusic = 0.05f;
        Prefs.Apply();
        Prefs.Save();
        AudioSource.Play();
        entry = null;
        MusicReset = false;

        Log.Message($"Received {audioBytes.Length} bytes of audio data.");
        return true;
    }

    public async Task<string> Talk(string chatgpt_api_key, Pawn? talked_to, List<IArchivable> history)
    {
        var text = RemoveColorTag.Replace(Entry.ToGameStringFromPOV(pawn), string.Empty);
        var all_history = string.Join("\n", history.Select(item => item.ArchivedLabel));

        var response = await GetOpenAIResponseAsync(chatgpt_api_key, talked_to, all_history);

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
                        return textElement.GetString()!;
                    }
                }
            }
        }
        Log.Message("No output text found in response.");
        return response;
    }

    private string SubstituteKeywords(string template, Pawn pawn, Pawn talked_to, string all_history)
    {
        return template
            .Replace("{PAWN_NAME}", pawn.Name?.ToString() ?? "Unknown")
            .Replace("{RECIPIENT_NAME}", talked_to.Name?.ToString() ?? "Unknown")
            .Replace("{DAYS_PASSED}", RimWorld.GenDate.DaysPassedSinceSettle.ToString())
            .Replace("{JOB}", pawn.CurJob?.def?.ToString() ?? "nothing")
            .Replace("{CHILDHOOD}", pawn.story.Childhood?.untranslatedDesc ?? "colonist")
            .Replace("{ADULTHOOD}", pawn.story.Adulthood?.untranslatedDesc ?? "colonist")
            .Replace("{HISTORY}", all_history);
    }

    public async Task<string?> GetOpenAIResponseAsync(string apiKey, Pawn? talked_to, string all_history)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        var instructions = "";
        var input = "";

        var job = pawn.CurJob.def;
        Log.Message("related pawns");
        Log.Message(pawn.relations.RelatedPawns);


        if (talked_to != null)
        {
            instructions = SubstituteKeywords(Settings.InstructionsTemplate.Value, pawn, talked_to, all_history);

            switch (KindOfTalk)
            {
                case "Chitchat":
                    input = $"you make some casual conversation with you're fellow crewmate {talked_to.Name}";
                    break;
                case "DeepTalk":
                    input = $"you talk about a deep subject with you're fellow crewmate {talked_to.Name}";
                    break;
                case "Slight":
                    input = $"you say something to slight you're fellow crewmate {talked_to.Name}";
                    break;
                case "Insult":
                    input = $"you say something to insult you're fellow crewmate {talked_to.Name}";
                    break;
                case "KindWords":
                    input = $"you say kind words to you're fellow crewmate {talked_to.Name}";
                    break;
                case "AnimalChat":
                    input = $"you chat with the animal {talked_to.Name}";
                    break;
                case "TameAttempt":
                    input = $"you say something to try and tame the animal {talked_to.Name}";
                    break;
                case "TrainAttempt":
                    input = $"you say something to try and train the animal {talked_to.Name}";
                    break;
                case "Nuzzle":
                    input = $"you say something to the animal {talked_to.Name} who is nuzzling you";
                    break;
                case "ReleaseToWild":
                    input = $"you say something to the animal {talked_to.Name} who you are releasing";
                    break;
                case "BuildRapport":
                    input = $"you say something to the prisoner {talked_to.Name} to try and build rapport";
                    break;
                case "RecruitAttempt":
                    input = $"you say something to the prisoner {talked_to.Name} to try and recruit them";
                    break;
                case "SparkJailbreak":
                    input = $"you are a prisoner talking with you're fellow prisoner {talked_to.Name} to get them to rebel";
                    break;
                case "RomanceAttempt":
                    input = $"you say something to try to romance {talked_to.Name}";
                    break;
                case "MarriageProposal":
                    input = $"you say something to try to get {talked_to.Name} to marry you";
                    break;
                case "Breakup":
                    input = $"you are breaking up with {talked_to.Name}";
                    break;
            }
        }
        var requestBody = new
        {
            model = "gpt-5.1-2025-11-13",
            input,
            instructions,
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

    public static AudioClip ToAudioClipWithSampleRate(byte[] pcmData, string clipName, int sampleRate)
    {
        // Parse raw PCM data (16-bit PCM, mono, no header)
        int channels = 1;
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            samples[i] = sample / 32768f;
        }

        AudioClip audioClip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
        audioClip.SetData(samples, 0);
        return audioClip;
    }
}