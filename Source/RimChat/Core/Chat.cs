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

        return true;
    }

    public async Task<bool> VocalizeResemble(string whatWasSaid, string voiceUuid)
    {
        using var client = new HttpClient();
        var apiKey = Settings.ResembleAPIKey.Value;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var requestBody = new
        {
            voice_uuid = voiceUuid,
            data = whatWasSaid,
            precision = "PCM_16"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(
            "https://f.cluster.resemble.ai/synthesize",
            content);

        if (!response.IsSuccessStatusCode)
        {
            Log.Message("Failed to vocalize text with Resemble.");
            Log.Message($"Status Code: {response.StatusCode}");
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"Error Body: {errorBody}");
            return false;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        // Parse the response JSON and extract audio_content and sample_rate
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("audio_content", out var audioContentElement))
        {
            Log.Message("Failed to find audio_content in Resemble response.");
            return false;
        }

        if (!doc.RootElement.TryGetProperty("sample_rate", out var sampleRateElement))
        {
            Log.Message("Failed to find sample_rate in Resemble response.");
            return false;
        }

        var audioBase64 = audioContentElement.GetString();
        var audioBytes = System.Convert.FromBase64String(audioBase64);
        var sampleRate = sampleRateElement.GetInt32();

        // Convert PCM16 bytes to AudioClip using the sample rate from the response
        var audioClip = WavUtility.ToAudioClipWithSampleRate(audioBytes, "VocalizedText", sampleRate);
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

        Log.Message($"Received {audioBytes.Length} bytes of audio data from Resemble.");
        return true;
    }

    public async Task<bool> VocalizePlayer2(string whatWasSaid, string voiceId)
    {
        Log.Message($"[Player2 TTS] Starting vocalization with voice ID: {voiceId}");
        Log.Message($"[Player2 TTS] Text to speak: {whatWasSaid}");

        using var client = new HttpClient();
        client.Timeout = System.TimeSpan.FromSeconds(30); // Set timeout early
        var gameKey = Settings.Player2GameKey.Value;
        Log.Message($"[Player2 TTS] Using game key: {(string.IsNullOrEmpty(gameKey) ? "EMPTY" : "SET")}");
        client.DefaultRequestHeaders.Add("player2-game-key", gameKey);

        var requestBody = new
        {
            text = whatWasSaid,
            voice_ids = new[] { voiceId },
            play_in_app = false,
            speed = 1.0,
            audio_format = "pcm"
        };

        var json = JsonSerializer.Serialize(requestBody);
        Log.Message($"[Player2 TTS] Request body: {json}");
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        Log.Message("[Player2 TTS] Sending request to http://127.0.0.1:4315/v1/tts/speak");

        System.Net.Http.HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("http://127.0.0.1:4315/v1/tts/speak", content);
            Log.Message($"[Player2 TTS] Received response with status: {response.StatusCode}");
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Player2 TTS] Exception during POST request: {ex.Message}");
            Log.Error($"[Player2 TTS] Stack trace: {ex.StackTrace}");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            Log.Message("[Player2 TTS] Failed to vocalize text with Player2.");
            Log.Message($"[Player2 TTS] Status Code: {response.StatusCode}");
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"[Player2 TTS] Error Body: {errorBody}");
            return false;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        Log.Message($"[Player2 TTS] Response body length: {responseBody.Length} characters");
        Log.Message($"[Player2 TTS] Response preview: {responseBody.Substring(0, System.Math.Min(200, responseBody.Length))}...");

        try
        {
            // Parse the response JSON and extract the base64 audio data
            Log.Message("[Player2 TTS] Parsing JSON response...");
            using var doc = JsonDocument.Parse(responseBody);

            Log.Message("[Player2 TTS] Looking for 'data' property...");
            if (!doc.RootElement.TryGetProperty("data", out var audioDataElement))
            {
                Log.Error("[Player2 TTS] Failed to find 'data' property in Player2 response.");
                Log.Error($"[Player2 TTS] Available properties: {string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name))}");
                return false;
            }

            Log.Message("[Player2 TTS] Extracting base64 string...");
            var audioBase64 = audioDataElement.GetString();
            if (string.IsNullOrEmpty(audioBase64))
            {
                Log.Error("[Player2 TTS] Audio base64 string is null or empty!");
                return false;
            }
            Log.Message($"[Player2 TTS] Raw data string length: {audioBase64.Length}");

            // Player2 returns a data URI like: data:audio/pcm;rate=24000;base64,ACTUALBASE64DATA
            // We need to strip the prefix and extract just the base64 part
            if (audioBase64.StartsWith("data:"))
            {
                Log.Message("[Player2 TTS] Detected data URI format, extracting base64 portion...");
                var commaIndex = audioBase64.IndexOf(',');
                if (commaIndex >= 0)
                {
                    audioBase64 = audioBase64.Substring(commaIndex + 1);
                    Log.Message($"[Player2 TTS] Extracted base64 string length: {audioBase64.Length}");
                }
                else
                {
                    Log.Error("[Player2 TTS] Data URI format detected but no comma found!");
                    return false;
                }
            }

            Log.Message("[Player2 TTS] Decoding base64 to bytes...");
            var audioBytes = System.Convert.FromBase64String(audioBase64);
            Log.Message($"[Player2 TTS] Decoded {audioBytes.Length} bytes of audio data");

            // Assume 24kHz sample rate for Player2 (adjust if needed based on their actual output)
            Log.Message("[Player2 TTS] Creating AudioClip from PCM data...");
            var audioClip = WavUtility.ToAudioClipWithSampleRate(audioBytes, "VocalizedText", 24000);
            if (audioClip == null)
            {
                Log.Error("[Player2 TTS] AudioClip creation returned null!");
                return false;
            }
            Log.Message($"[Player2 TTS] AudioClip created: length={audioClip.length}s, samples={audioClip.samples}");

            Log.Message("[Player2 TTS] Creating AudioSource...");
            var audioSource = new GameObject("VocalizedAudioSource").AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.volume = 1f;
            AudioSource = audioSource;

            Log.Message("[Player2 TTS] Adjusting music volume and starting playback...");
            MusicVol = Prefs.VolumeMusic;
            Prefs.VolumeMusic = 0.05f;
            Prefs.Apply();
            Prefs.Save();
            AudioSource.Play();
            entry = null;
            MusicReset = false;

            Log.Message($"[Player2 TTS] Successfully started playback!");
            return true;
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Player2 TTS] Exception during audio processing: {ex.Message}");
            Log.Error($"[Player2 TTS] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public async Task<string> Talk(string chatgpt_api_key, Pawn? talked_to, List<IArchivable> history)
    {
        var text = RemoveColorTag.Replace(Entry.ToGameStringFromPOV(pawn), string.Empty);
        var all_history = string.Join("\n", history.Select(item => item.ArchivedLabel));

        string response;

        // Route to the correct LLM provider
        if (Settings.LLMProviderSetting.Value == LLMProvider.Claude)
        {
            response = await GetClaudeResponseAsync(Settings.ClaudeAPIKey.Value, talked_to, all_history);
            // Claude response is already parsed and returns the text directly
            var claudeText = response ?? "Error: No response from Claude";
            Log.Message($"[Claude] Captured text: {claudeText}");
            return claudeText;
        }
        else if (Settings.LLMProviderSetting.Value == LLMProvider.Gemini)
        {
            response = await GetGeminiResponseAsync(Settings.GeminiAPIKey.Value, talked_to, all_history);
            // Gemini response is already parsed and returns the text directly
            var geminiText = response ?? "Error: No response from Gemini";
            Log.Message($"[Gemini] Captured text: {geminiText}");
            return geminiText;
        }
        else if (Settings.LLMProviderSetting.Value == LLMProvider.Player2)
        {
            response = await GetPlayer2ResponseAsync(Settings.Player2GameKey.Value, talked_to, all_history);
            // Player2 response is already parsed and returns the text directly
            var player2Text = response ?? "Error: No response from Player2";
            Log.Message($"[Player2] Captured text: {player2Text}");
            return player2Text;
        }
        else
        {
            response = await GetOpenAIResponseAsync(chatgpt_api_key, talked_to, all_history);

            // Parse the OpenAI response JSON and extract output->content->text
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
                            var openaiText = textElement.GetString()!;
                            Log.Message($"[OpenAI] Captured text: {openaiText}");
                            return openaiText;
                        }
                    }
                }
            }
            Log.Message("No output text found in response.");
            return response;
        }
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
        
        // Get subjects
        var subjects = new string[] { "recent events", "your adulthood", "your childhood", "what you're currently doing" };
        var subject = subjects[new System.Random().Next(subjects.Length)];

        // Get current thoughts separately
        var thoughts = new List<Thought>();
        if (pawn.needs?.mood?.thoughts != null)
        {
            List<Thought> allThoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(allThoughts);

            thoughts = allThoughts
                .Where(t => !string.IsNullOrEmpty(t.Description))
                .ToList();
        }

        Thought? selectedThought = thoughts.Count > 0 ? thoughts.RandomElement() : null;


        if (talked_to != null)
        {
            instructions = SubstituteKeywords(Settings.InstructionsTemplate.Value, pawn, talked_to, all_history);

            switch (KindOfTalk)
            {
                case "Chitchat":
                    if (selectedThought != null)
                    {
                        var thoughtDesc = selectedThought.Description;
                        var isPositive = selectedThought.MoodOffset() > 0;

                        if (isPositive)
                        {
                            // Prompt for positive thoughts
                            input = $"You ({pawn.Name}) just had the following thought {thoughtDesc}, you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                        }
                        else
                        {
                            // Check if this is a pain-related thought
                            var thoughtDefName = selectedThought.def?.defName?.ToLower() ?? "";
                            var thoughtLabel = selectedThought.def?.label?.ToLower() ?? "";
                            var isPainRelated = thoughtDefName.Contains("pain") || thoughtLabel.Contains("pain");

                            if (isPainRelated)
                            {
                                // Try to find what's causing the pain
                                var painSource = "";
                                if (pawn.health?.hediffSet?.hediffs != null)
                                {
                                    var painfulHediff = pawn.health.hediffSet.hediffs
                                        .Where(h => h.PainOffset > 0)
                                        .OrderByDescending(h => h.PainOffset)
                                        .FirstOrDefault();

                                    if (painfulHediff != null && painfulHediff.Part != null)
                                    {
                                        painSource = $" in your {painfulHediff.Part.Label}";
                                    }
                                }

                                input = $"{thoughtDesc}. This pain{painSource} has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                            else
                            {
                                // Prompt for negative thoughts (same for now, can customize later)
                                input = $"{thoughtDesc}. This has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                        }
                    }
                    else
                    {
                        input = $"you ({pawn.Name}) make some casual conversation about {subject} with you're fellow crewmate {talked_to.Name}";
                    }
                    break;
                case "DeepTalk":
                    input = $"you ({pawn.Name}) talk about a deep subject with you're fellow crewmate {talked_to.Name}";
                    break;
                case "Slight":
                    input = $"you ({pawn.Name}) say something to slight you're fellow crewmate {talked_to.Name}";
                    break;
                case "Insult":
                    input = $"you ({pawn.Name}) say something to insult you're fellow crewmate {talked_to.Name}";
                    break;
                case "KindWords":
                    input = $"you ({pawn.Name}) say kind words to you're fellow crewmate {talked_to.Name}";
                    break;
                case "AnimalChat":
                    input = $"you ({pawn.Name}) chat with the animal {talked_to.Name}";
                    break;
                case "TameAttempt":
                    input = $"you ({pawn.Name}) say something to try and tame the animal {talked_to.Name}";
                    break;
                case "TrainAttempt":
                    input = $"you ({pawn.Name}) say something to try and train the animal {talked_to.Name}";
                    break;
                case "Nuzzle":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who is nuzzling you";
                    break;
                case "ReleaseToWild":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who you are releasing";
                    break;
                case "BuildRapport":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and build rapport";
                    break;
                case "RecruitAttempt":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and recruit them";
                    break;
                case "SparkJailbreak":
                    input = $"you ({pawn.Name}) are a prisoner talking with you're fellow prisoner {talked_to.Name} to get them to rebel";
                    break;
                case "RomanceAttempt":
                    input = $"you ({pawn.Name}) say something to try to romance {talked_to.Name}";
                    break;
                case "MarriageProposal":
                    input = $"you ({pawn.Name}) propose to {talked_to.Name}";
                    break;
                case "Breakup":
                    input = $"you ({pawn.Name}) are breaking up with {talked_to.Name}";
                    break;
            }
        }
        var requestBody = new
        {
            model = "gpt-5.1-chat-latest",
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

    public async Task<string?> GetClaudeResponseAsync(string apiKey, Pawn? talked_to, string all_history)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var instructions = "";
        var input = "";

        // Get subjects
        var subjects = new string[] { "recent events", "your adulthood", "your childhood", "what you're currently doing" };
        var subject = subjects[new System.Random().Next(subjects.Length)];

        // Get current thoughts separately
        var thoughts = new List<Thought>();
        if (pawn.needs?.mood?.thoughts != null)
        {
            List<Thought> allThoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(allThoughts);

            thoughts = allThoughts
                .Where(t => !string.IsNullOrEmpty(t.Description))
                .ToList();
        }

        Thought? selectedThought = thoughts.Count > 0 ? thoughts.RandomElement() : null;

        if (talked_to != null)
        {
            instructions = SubstituteKeywords(Settings.InstructionsTemplate.Value, pawn, talked_to, all_history);

            switch (KindOfTalk)
            {
                case "Chitchat":
                    if (selectedThought != null)
                    {
                        var thoughtDesc = selectedThought.Description;
                        var isPositive = selectedThought.MoodOffset() > 0;

                        if (isPositive)
                        {
                            // Prompt for positive thoughts
                            input = $"You ({pawn.Name}) just had the following thought {thoughtDesc}, you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                        }
                        else
                        {
                            // Check if this is a pain-related thought
                            var thoughtDefName = selectedThought.def?.defName?.ToLower() ?? "";
                            var thoughtLabel = selectedThought.def?.label?.ToLower() ?? "";
                            var isPainRelated = thoughtDefName.Contains("pain") || thoughtLabel.Contains("pain");

                            if (isPainRelated)
                            {
                                // Try to find what's causing the pain
                                var painSource = "";
                                if (pawn.health?.hediffSet?.hediffs != null)
                                {
                                    var painfulHediff = pawn.health.hediffSet.hediffs
                                        .Where(h => h.PainOffset > 0)
                                        .OrderByDescending(h => h.PainOffset)
                                        .FirstOrDefault();

                                    if (painfulHediff != null && painfulHediff.Part != null)
                                    {
                                        painSource = $" in your {painfulHediff.Part.Label}";
                                    }
                                }

                                input = $"{thoughtDesc}. This pain{painSource} has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                            else
                            {
                                // Prompt for negative thoughts (same for now, can customize later)
                                input = $"{thoughtDesc}. This has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                        }
                    }
                    else
                    {
                        input = $"you ({pawn.Name}) make some casual conversation about {subject} with you're fellow crewmate {talked_to.Name}";
                    }
                    break;
                case "DeepTalk":
                    input = $"you ({pawn.Name}) talk about a deep subject with you're fellow crewmate {talked_to.Name}";
                    break;
                case "Slight":
                    input = $"you ({pawn.Name}) say something to slight you're fellow crewmate {talked_to.Name}";
                    break;
                case "Insult":
                    input = $"you ({pawn.Name}) say something to insult you're fellow crewmate {talked_to.Name}";
                    break;
                case "KindWords":
                    input = $"you ({pawn.Name}) say kind words to you're fellow crewmate {talked_to.Name}";
                    break;
                case "AnimalChat":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name}";
                    break;
                case "TameAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and tame them";
                    break;
                case "TrainAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and train them";
                    break;
                case "Nuzzle":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who is nuzzling you";
                    break;
                case "ReleaseToWild":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who you are releasing";
                    break;
                case "BuildRapport":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and build rapport";
                    break;
                case "RecruitAttempt":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and recruit them";
                    break;
                case "SparkJailbreak":
                    input = $"you ({pawn.Name}) are a prisoner talking with you're fellow prisoner {talked_to.Name} to get them to rebel";
                    break;
                case "RomanceAttempt":
                    input = $"you ({pawn.Name}) say something to try to romance {talked_to.Name}";
                    break;
                case "MarriageProposal":
                    input = $"you ({pawn.Name}) propose to {talked_to.Name}";
                    break;
                case "Breakup":
                    input = $"you ({pawn.Name}) are breaking up with {talked_to.Name}";
                    break;
            }
        }

        var requestBody = new
        {
            model = "claude-haiku-4-5",
            max_tokens = 1024,
            system = instructions,
            messages = new[]
            {
                new { role = "user", content = input }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"Claude API Error: {response.StatusCode} - {errorBody}");
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        // Parse the Claude response JSON
        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("content", out var contentArray))
        {
            foreach (var contentItem in contentArray.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString();
                }
            }
        }

        Log.Message("No text found in Claude response.");
        return responseBody;
    }

    public async Task<string?> GetGeminiResponseAsync(string apiKey, Pawn? talked_to, string all_history)
    {
        using var client = new HttpClient();
        var instructions = "";
        var input = "";

        // Get subjects
        var subjects = new string[] { "recent events", "your adulthood", "your childhood", "what you're currently doing" };
        var subject = subjects[new System.Random().Next(subjects.Length)];

        // Get current thoughts separately
        var thoughts = new List<Thought>();
        if (pawn.needs?.mood?.thoughts != null)
        {
            List<Thought> allThoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(allThoughts);

            thoughts = allThoughts
                .Where(t => !string.IsNullOrEmpty(t.Description))
                .ToList();
        }

        Thought? selectedThought = thoughts.Count > 0 ? thoughts.RandomElement() : null;


        if (talked_to != null)
        {
            instructions = SubstituteKeywords(Settings.InstructionsTemplate.Value, pawn, talked_to, all_history);

            switch (KindOfTalk)
            {
                case "Chitchat":
                    if (selectedThought != null)
                    {
                        var thoughtDesc = selectedThought.Description;
                        var isPositive = selectedThought.MoodOffset() > 0;

                        if (isPositive)
                        {
                            // Prompt for positive thoughts
                            input = $"You ({pawn.Name}) just had the following thought {thoughtDesc}, you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                        }
                        else
                        {
                            // Check if this is a pain-related thought
                            var thoughtDefName = selectedThought.def?.defName?.ToLower() ?? "";
                            var thoughtLabel = selectedThought.def?.label?.ToLower() ?? "";
                            var isPainRelated = thoughtDefName.Contains("pain") || thoughtLabel.Contains("pain");

                            if (isPainRelated)
                            {
                                // Try to find what's causing the pain
                                var painSource = "";
                                if (pawn.health?.hediffSet?.hediffs != null)
                                {
                                    var painfulHediff = pawn.health.hediffSet.hediffs
                                        .Where(h => h.PainOffset > 0)
                                        .OrderByDescending(h => h.PainOffset)
                                        .FirstOrDefault();

                                    if (painfulHediff != null && painfulHediff.Part != null)
                                    {
                                        painSource = $" in your {painfulHediff.Part.Label}";
                                    }
                                }

                                input = $"{thoughtDesc}. This pain{painSource} has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                            else
                            {
                                // Prompt for negative thoughts (same for now, can customize later)
                                input = $"{thoughtDesc}. This has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                        }
                    }
                    else
                    {
                        input = $"you ({pawn.Name}) make some casual conversation about {subject} with you're fellow crewmate {talked_to.Name}";
                    }
                    break;
                case "DeepTalk":
                    input = $"you ({pawn.Name}) talk about a deep subject with you're fellow crewmate {talked_to.Name}";
                    break;
                case "Slight":
                    input = $"you ({pawn.Name}) say something to slight you're fellow crewmate {talked_to.Name}";
                    break;
                case "Insult":
                    input = $"you ({pawn.Name}) say something to insult you're fellow crewmate {talked_to.Name}";
                    break;
                case "KindWords":
                    input = $"you ({pawn.Name}) say kind words to you're fellow crewmate {talked_to.Name}";
                    break;
                case "AnimalChat":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name}";
                    break;
                case "TameAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and tame them";
                    break;
                case "TrainAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and train them";
                    break;
                case "Nuzzle":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who is nuzzling you";
                    break;
                case "ReleaseToWild":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who you are releasing";
                    break;
                case "BuildRapport":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and build rapport";
                    break;
                case "RecruitAttempt":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and recruit them";
                    break;
                case "SparkJailbreak":
                    input = $"you ({pawn.Name}) are a prisoner talking with you're fellow prisoner {talked_to.Name} to get them to rebel";
                    break;
                case "RomanceAttempt":
                    input = $"you ({pawn.Name}) say something to try to romance {talked_to.Name}";
                    break;
                case "MarriageProposal":
                    input = $"you ({pawn.Name}) propose to {talked_to.Name}";
                    break;
                case "Breakup":
                    input = $"you ({pawn.Name}) are breaking up with {talked_to.Name}";
                    break;
            }
        }

        var combinedPrompt = $"{instructions}\n\n{input}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = combinedPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 1.5,
                thinkingConfig = new
                {
                    includeThoughts = false,
                    thinkingLevel = "LOW"
                }
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-preview:generateContent?key={apiKey}", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"Gemini API Error: {response.StatusCode} - {errorBody}");
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        // Parse the Gemini response JSON - just grab the first text value
        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content_obj) &&
                content_obj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var firstPart = parts[0];
                if (firstPart.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString();
                }
            }
        }

        Log.Message("No text found in Gemini response.");
        return responseBody;
    }

    public async Task<string?> GetPlayer2ResponseAsync(string gameKey, Pawn? talked_to, string all_history)
    {
        Log.Message($"[Player2 LLM] Starting chat completion for {pawn.Name}");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("player2-game-key", gameKey);
        var instructions = "";
        var input = "";

        // Get subjects
        var subjects = new string[] { "recent events", "your adulthood", "your childhood", "what you're currently doing" };
        var subject = subjects[new System.Random().Next(subjects.Length)];

        // Get current thoughts separately
        var thoughts = new List<Thought>();
        if (pawn.needs?.mood?.thoughts != null)
        {
            List<Thought> allThoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(allThoughts);

            thoughts = allThoughts
                .Where(t => !string.IsNullOrEmpty(t.Description))
                .ToList();
        }

        Thought? selectedThought = thoughts.Count > 0 ? thoughts.RandomElement() : null;

        if (talked_to != null)
        {
            instructions = SubstituteKeywords(Settings.InstructionsTemplate.Value, pawn, talked_to, all_history);

            switch (KindOfTalk)
            {
                case "Chitchat":
                    if (selectedThought != null)
                    {
                        var thoughtDesc = selectedThought.Description;
                        var isPositive = selectedThought.MoodOffset() > 0;

                        if (isPositive)
                        {
                            // Prompt for positive thoughts
                            input = $"You ({pawn.Name}) just had the following thought {thoughtDesc}, you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                        }
                        else
                        {
                            // Check if this is a pain-related thought
                            var thoughtDefName = selectedThought.def?.defName?.ToLower() ?? "";
                            var thoughtLabel = selectedThought.def?.label?.ToLower() ?? "";
                            var isPainRelated = thoughtDefName.Contains("pain") || thoughtLabel.Contains("pain");

                            if (isPainRelated)
                            {
                                // Try to find what's causing the pain
                                var painSource = "";
                                if (pawn.health?.hediffSet?.hediffs != null)
                                {
                                    var painfulHediff = pawn.health.hediffSet.hediffs
                                        .Where(h => h.PainOffset > 0)
                                        .OrderByDescending(h => h.PainOffset)
                                        .FirstOrDefault();

                                    if (painfulHediff != null && painfulHediff.Part != null)
                                    {
                                        painSource = $" in your {painfulHediff.Part.Label}";
                                    }
                                }

                                input = $"{thoughtDesc}. This pain{painSource} has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                            else
                            {
                                // Prompt for negative thoughts (same for now, can customize later)
                                input = $"{thoughtDesc}. This has been affecting you ({pawn.Name}), and you want to discuss it with your crewmate {talked_to.Name}. Express how you're feeling about this";
                            }
                        }
                    }
                    else
                    {
                        input = $"you ({pawn.Name}) make some casual conversation about {subject} with you're fellow crewmate {talked_to.Name}";
                    }
                    break;
                case "DeepTalk":
                    input = $"you ({pawn.Name}) talk about a deep subject with you're fellow crewmate {talked_to.Name}";
                    break;
                case "Slight":
                    input = $"you ({pawn.Name}) say something to slight you're fellow crewmate {talked_to.Name}";
                    break;
                case "Insult":
                    input = $"you ({pawn.Name}) say something to insult you're fellow crewmate {talked_to.Name}";
                    break;
                case "KindWords":
                    input = $"you ({pawn.Name}) say kind words to you're fellow crewmate {talked_to.Name}";
                    break;
                case "AnimalChat":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name}";
                    break;
                case "TameAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and tame them";
                    break;
                case "TrainAttempt":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} to try and train them";
                    break;
                case "Nuzzle":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who is nuzzling you";
                    break;
                case "ReleaseToWild":
                    input = $"you ({pawn.Name}) say something to the animal {talked_to.Name} who you are releasing";
                    break;
                case "BuildRapport":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and build rapport";
                    break;
                case "RecruitAttempt":
                    input = $"you ({pawn.Name}) say something to the prisoner {talked_to.Name} to try and recruit them";
                    break;
                case "SparkJailbreak":
                    input = $"you ({pawn.Name}) are a prisoner talking with you're fellow prisoner {talked_to.Name} to get them to rebel";
                    break;
                case "RomanceAttempt":
                    input = $"you ({pawn.Name}) say something to try to romance {talked_to.Name}";
                    break;
                case "MarriageProposal":
                    input = $"you ({pawn.Name}) propose to {talked_to.Name}";
                    break;
                case "Breakup":
                    input = $"you ({pawn.Name}) are breaking up with {talked_to.Name}";
                    break;
            }
        }

        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = instructions },
                new { role = "user", content = input }
            },
            stream = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        Log.Message($"[Player2 LLM] Request: {json.Substring(0, System.Math.Min(300, json.Length))}...");
        var response = await client.PostAsync("http://127.0.0.1:4315/v1/chat/completions", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Message($"[Player2 LLM] Error: {response.StatusCode} - {errorBody}");
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        Log.Message($"[Player2 LLM] Response: {responseBody.Substring(0, System.Math.Min(300, responseBody.Length))}...");

        // Parse the Player2 response JSON (follows OpenAI format)
        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var textElement))
            {
                var result = textElement.GetString();
                Log.Message($"[Player2 LLM] Extracted content: {result}");
                return result;
            }
        }

        Log.Message("[Player2 LLM] No text found in Player2 response.");
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