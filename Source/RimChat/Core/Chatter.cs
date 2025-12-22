using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using RimChat.Access;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using System.Configuration;
using Unity.Burst.Intrinsics;
using System.EnterpriseServices;
using UnityEngine.PlayerLoop;
using RimWorld.BaseGen;
using Verse.Sound;

namespace RimChat.Core;

public static class Chatter
{
    private const float LabelPositionOffset = -0.6f;
    private static bool CanRender() => WorldRendererUtility.CurrentWorldRenderMode is WorldRenderMode.None or WorldRenderMode.Background;
    private static Dictionary<Pawn, Chat> Dictionary = new();

    public static Chat? GetChat(Pawn pawn)
    {
        return Dictionary.TryGetValue(pawn, out var chat) ? chat : null;
    }

    // private static Dictionary<Pawn, string> VoiceDict = new();
    private static System.DateTime next_talk = DateTime.Now;

    private static Pawn? talked_to;

    private static Pawn? is_up;
    // eUd, XjL, jqc

    public static void Talk()
    {
        var altitude = GetAltitude();
        if (altitude <= 0 || altitude > 40) { return; }

        var selected = Find.Selector!.SingleSelectedObject as Pawn;
        StartTalk();
    }

    private static async Task StartTalk()
    {
        var candidates = Dictionary.Where(kvp => !kvp.Value.AlreadyPlayed && kvp.Value.Entry != null).ToList();
        if (candidates.Count == 0) return;
        var random = new System.Random();
        var randomEntry = candidates[random.Next(candidates.Count)];
        var pawn = randomEntry.Key;
        var chat = randomEntry.Value;

        if (is_up != null)
        {
            pawn = randomEntry.Key;
            chat = Dictionary[is_up];
        }
        if (!CanRender() || !pawn.Spawned || pawn.Map != Find.CurrentMap || pawn.Map!.fogGrid!.IsFogged(pawn.Position)) { return; }

        if (chat.AIChat == null && DateTime.Now > next_talk && talked_to != null && talked_to != pawn)
        {
            // If the chat has not been talked about yet, start the talk
            if (chat.Entry is PlayLogEntry_Interaction interaction)
            {
                var initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                if (initiator != pawn) { return; }
            }

            // Update next_talk IMMEDIATELY to prevent multiple conversations starting at once
            next_talk = DateTime.Now.AddMinutes(Settings.MinTimeBetweenTalkInMinutes.Value);

            // Start the talk
            chat.AIChat = chat.Talk(Settings.TextAPIKey.Value, talked_to, Find.History.archive.ArchivablesListForReading);
            is_up = pawn;

            chat.AlreadyPlayed = true;
        }
        else if (chat.AIChat is not null && !chat.AIChat.IsCompleted)
        {
            // Message is still being waited on, do nothing
            return;
        }
        else if (chat.AIChat is not null && chat.AIChat.IsCompleted)
        {
            var result = chat.AIChat.Result;
            chat.AIChat = null;
            talked_to = null;
                
            var db = VoiceWorldComp.Get();
            chat.AlreadyPlayed = false;
            var voice = db.GetVoice(chat.pawn);

            Log.Message($"[Chatter] About to vocalize. Provider: {Settings.TTSProviderSetting.Value}, Voice: {voice}, Text: {result}");

            if (Settings.TTSProviderSetting.Value == TTSProvider.OpenAI)
            {
                await chat.VocalizeOpenAI(result, voice);
            }
            else if (Settings.TTSProviderSetting.Value == TTSProvider.Resemble)
            {
                await chat.VocalizeResemble(result, voice);
            }
            else if (Settings.TTSProviderSetting.Value == TTSProvider.Player2)
            {
                await chat.VocalizePlayer2(result, voice);
            }
            else
            {
                await chat.Vocalize(result, voice);
            }

            is_up = null;
        }
        else if (!chat.AudioSource.isPlaying && !chat.MusicReset)
        {
            Prefs.VolumeMusic = chat.MusicVol;
            Prefs.Apply();
            Prefs.Save();
            chat.MusicReset = true;
        }
        else
        {
            return;
        }
    }
    public static void Add(LogEntry entry)
    {
        if (!CanRender()) { return; }
        
        Pawn? initiator, recipient;
        

        InteractionDef kind_of_talk;

        switch (entry)
        {
            case PlayLogEntry_Interaction interaction:
                initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                recipient = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Recipient.GetValue(interaction);
                kind_of_talk = (InteractionDef?)Reflection.Verse_PlayLogEntry_Interaction_Type.GetValue(interaction);
                talked_to = recipient;
                break;
            default:
                return;
        }

        if (!initiator.IsColonistPlayerControlled)
        {
            return;
        }

        if (initiator is null || initiator.Map != Find.CurrentMap) { return; }

        if (talked_to == null || recipient == initiator ) return;

        var choosenTalk = ChanceUtil.IsSelected(kind_of_talk.defName);

        if (choosenTalk)
        {
            if (!Dictionary.ContainsKey(initiator))
            {
                Dictionary[initiator] = new Chat(initiator, entry);
                Dictionary[initiator].KindOfTalk = kind_of_talk.defName;
            }
            else
            {
                Dictionary[initiator].Entry = entry;
                Dictionary[initiator].KindOfTalk = kind_of_talk.defName;
            }

            var db = VoiceWorldComp.Get();


            if (db.GetVoice(initiator) == "" || db.GetVoice(initiator) == null)
            {
                db.TryAssignRandomVoice(initiator);
            }
        }


    }
    private static float GetAltitude()
    {
        var altitude = Mathf.Max(1f, (float)Reflection.Verse_CameraDriver_RootSize.GetValue(Find.CameraDriver));
        Compatibility.Apply(ref altitude);

        return altitude;
    }
}


public class VoiceWorldComp : WorldComponent
{
    // --- ElevenLabs voice pools ---
    private HashSet<string> malePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "NYkjXRso4QIcgWakN1Cr", "XjLkpWUlnhS8i7gGz3lZ", "zNsotODqUhvbJ5wMG7Ei", "MFZUKuGQUsGJPQjTS4wC", "4dZr8J4CBeokyRkTRpoN" };

    private HashSet<string> femalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {  "4tRn1lSkEn13EVTuqb0g", "eUdJpUEN3EslrgE24PKx", "kNie5n4lYl7TrvqBZ4iG", "g6xIsTj2HwM6VR4iXFCw", "jqcCZkN6Knx8BJ5TBdYR"  };

    // --- OpenAI voice pools ---
    private HashSet<string> openAIMalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "echo", "fable", "onyx", "ash", "ballad", "cedar" };

    private HashSet<string> openAIFemalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "alloy", "nova", "shimmer", "coral",  "marin", "sage" };

    // --- Resemble voice pools ---
    private HashSet<string> resembleMalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "38a0b764", "68e421ed", "bee581c1", "018dc07a", "a3b3f1df", "e8883d33", "ac7df359" };

    private HashSet<string> resembleFemalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "fb2d2858", "55f5b8dc", "96d225a3", "4e972f71", "8d516bf5", "1cf47426", "1ff0045f", "f453b918" };

    // --- Player2 voice pools (fetched on-demand from API) ---
    private HashSet<string> player2MalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> player2FemalePool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool player2VoicesFetched = false;

    // --- Main storage: pawn -> voice ---
    private Dictionary<Pawn, string> pawnVoices = new Dictionary<Pawn, string>();
    private Dictionary<Pawn, TTSProvider> pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();

    // Reverse index ensures uniqueness: voice -> owner
    private Dictionary<string, Pawn> voiceIndex = new Dictionary<string, Pawn>(StringComparer.OrdinalIgnoreCase);

    // Scribe helpers
    private List<Pawn> _keys;
    private List<string> _vals;
    private List<Pawn> _keys2;
    private List<TTSProvider> _providers;

    public VoiceWorldComp(World world) : base(world) { }

    public override void ExposeData()
    {
        base.ExposeData();

        // Save the assignments
        Scribe_Collections.Look(ref pawnVoices, "pawnVoices",
            LookMode.Reference, LookMode.Value, ref _keys, ref _vals);

        Scribe_Collections.Look(ref pawnVoiceProviders, "pawnVoiceProviders",
            LookMode.Reference, LookMode.Value, ref _keys2, ref _providers);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // Initialize dictionaries if they're null (loading old saves)
            if (pawnVoices == null) pawnVoices = new Dictionary<Pawn, string>();
            if (pawnVoiceProviders == null) pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();

            RebuildIndexAndPrune();
        }
    }

    private void RebuildIndexAndPrune()
    {
        voiceIndex.Clear();
        var toRemove = new List<Pawn>();

        foreach (var kv in pawnVoices)
        {
            var p = kv.Key;
            var v = kv.Value;

            if (!IsValidHumanlike(p) || string.IsNullOrWhiteSpace(v))
            {
                toRemove.Add(p);
                continue;
            }

            // First owner wins; if two pawns were serialized with the same voice, keep one.
            if (!voiceIndex.ContainsKey(v))
                voiceIndex[v] = p;
        }

        foreach (var p in toRemove)
            pawnVoices.Remove(p);
    }

    private static bool IsValidHumanlike(Pawn p) =>
        p != null && p.RaceProps?.Humanlike == true && !p.DestroyedOrNull();

    private void FetchPlayer2VoicesSync()
    {
        if (player2VoicesFetched) return;

        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("player2-game-key", Settings.Player2GameKey.Value);

            var task = client.GetAsync("http://127.0.0.1:4315/v1/tts/voices");
            task.Wait(5000); // Wait up to 5 seconds

            if (!task.IsCompleted)
            {
                Log.Warning("Player2 voice fetch timed out after 5 seconds!");
                return;
            }

            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning($"Failed to fetch Player2 voices: {response.StatusCode}");
                return;
            }

            var responseTask = response.Content.ReadAsStringAsync();
            responseTask.Wait();
            var responseBody = responseTask.Result;

            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("voices", out var voicesArray))
            {
                return;
            }

            player2MalePool.Clear();
            player2FemalePool.Clear();

            foreach (var voice in voicesArray.EnumerateArray())
            {
                if (!voice.TryGetProperty("id", out var idElement) ||
                    !voice.TryGetProperty("language", out var langElement) ||
                    !voice.TryGetProperty("gender", out var genderElement))
                {
                    continue;
                }

                var id = idElement.GetString();
                var language = langElement.GetString();
                var gender = genderElement.GetString();

                // Only include English voices (american_english and british_english)
                if (language != "american_english" && language != "british_english")
                {
                    continue;
                }

                if (gender == "male")
                {
                    player2MalePool.Add(id);
                }
                else if (gender == "female")
                {
                    player2FemalePool.Add(id);
                }
            }

            player2VoicesFetched = true;
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Error fetching Player2 voices: {ex.Message}");
        }
    }

    private IEnumerable<string> PoolFor(Pawn p)
    {
        var provider = Settings.TTSProviderSetting.Value;

        if (provider == TTSProvider.OpenAI)
        {
            if (p?.gender == Gender.Male) return openAIMalePool;
            if (p?.gender == Gender.Female) return openAIFemalePool;
            return openAIMalePool.Concat(openAIFemalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        else if (provider == TTSProvider.Resemble)
        {
            if (p?.gender == Gender.Male) return resembleMalePool;
            if (p?.gender == Gender.Female) return resembleFemalePool;
            return resembleMalePool.Concat(resembleFemalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        else if (provider == TTSProvider.Player2)
        {
            // Fetch voices from API if not already fetched
            if (!player2VoicesFetched)
            {
                FetchPlayer2VoicesSync();
            }

            if (p?.gender == Gender.Male) return player2MalePool;
            if (p?.gender == Gender.Female) return player2FemalePool;
            return player2MalePool.Concat(player2FemalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        else // ElevenLabs
        {
            if (p?.gender == Gender.Male) return malePool;
            if (p?.gender == Gender.Female) return femalePool;
            return malePool.Concat(femalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ---------- Read API ----------
    public string GetVoice(Pawn p)
    {
        if (p == null) return null;

        if (pawnVoices.TryGetValue(p, out var voice))
        {
            // Check if the voice is valid for the current provider
            if (!IsVoiceValidForCurrentProvider(p, voice))
            {
                // Voice is from a different provider, reassign
                Log.Message($"Voice {voice} for pawn {p.Name} is invalid for current TTS provider, reassigning...");
                TryAssignRandomVoice(p);
                pawnVoices.TryGetValue(p, out voice);
            }
            return voice;
        }

        return null;
    }

    private bool IsVoiceValidForCurrentProvider(Pawn p, string voice)
    {
        if (string.IsNullOrWhiteSpace(voice)) return false;

        var currentProvider = Settings.TTSProviderSetting.Value;

        // Initialize pawnVoiceProviders if null (shouldn't happen, but safety first)
        if (pawnVoiceProviders == null)
        {
            pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();
        }

        // Check if we have a stored provider for this pawn
        if (pawnVoiceProviders.TryGetValue(p, out var storedProvider))
        {
            // If stored provider matches current, voice is valid
            if (storedProvider == currentProvider)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // No stored provider, validate against pools
        if (currentProvider == TTSProvider.OpenAI)
        {
            return openAIMalePool.Contains(voice) || openAIFemalePool.Contains(voice);
        }
        else if (currentProvider == TTSProvider.Resemble)
        {
            return resembleMalePool.Contains(voice) || resembleFemalePool.Contains(voice);
        }
        else if (currentProvider == TTSProvider.Player2)
        {
            // Fetch voices if needed to validate
            if (!player2VoicesFetched)
            {
                FetchPlayer2VoicesSync();
            }
            return player2MalePool.Contains(voice) || player2FemalePool.Contains(voice);
        }
        else // ElevenLabs
        {
            return malePool.Contains(voice) || femalePool.Contains(voice);
        }
    }

    public bool IsVoiceFree(string voice) => !voiceIndex.ContainsKey(voice);

    public Pawn OwnerOfVoice(string voice) =>
        voiceIndex.TryGetValue(voice, out var owner) ? owner : null;

    // Snapshot for UI (read-only copy)
    public IReadOnlyDictionary<Pawn, string> Snapshot() =>
        new Dictionary<Pawn, string>(pawnVoices);

    // ---------- Write API (enforces uniqueness) ----------
    public bool TryAssignVoice(Pawn p, string voice, bool stealIfTaken = false)
    {
        if (!IsValidHumanlike(p)) return false;
        if (string.IsNullOrWhiteSpace(voice))
        {
            UnassignVoice(p);
            return true;
        }

        // Initialize dictionaries if null
        if (pawnVoices == null) pawnVoices = new Dictionary<Pawn, string>();
        if (pawnVoiceProviders == null) pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();

        // already has it?
        if (pawnVoices.TryGetValue(p, out var current) &&
            string.Equals(current, voice, StringComparison.OrdinalIgnoreCase))
            return true;

        if (voiceIndex.TryGetValue(voice, out var other) && other != p)
        {
            if (!stealIfTaken) return false; // uniqueness violation
            // Steal: remove from previous owner
            UnassignVoice(other);
        }

        // update indices
        if (current != null) voiceIndex.Remove(current);
        pawnVoices[p] = voice;
        pawnVoiceProviders[p] = Settings.TTSProviderSetting.Value; // Store current provider
        voiceIndex[voice] = p;
        return true;
    }

    public void UnassignVoice(Pawn p)
    {
        if (p == null) return;
        if (pawnVoices == null) return;
        if (pawnVoiceProviders == null) pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();

        if (pawnVoices.TryGetValue(p, out var old))
        {
            pawnVoices.Remove(p);
            pawnVoiceProviders.Remove(p);
            if (old != null) voiceIndex.Remove(old);
        }
    }

    /// Assigns a random voice from the appropriate pool.
    /// - Prefers unused voices.
    /// - If none are free, will "steal" a random one from someone else to keep uniqueness.
    ///   (If you prefer duplicates when exhausted, see the commented branch below.)
    public bool TryAssignRandomVoice(Pawn p)
    {
        if (!IsValidHumanlike(p)) return false;

        // Initialize dictionaries if null
        if (pawnVoices == null) pawnVoices = new Dictionary<Pawn, string>();
        if (pawnVoiceProviders == null) pawnVoiceProviders = new Dictionary<Pawn, TTSProvider>();

        var pool = PoolFor(p).ToList();
        if (pool.Count == 0) return false;

        // free voices from the pool
        var free = pool.Where(v => !voiceIndex.ContainsKey(v)).ToList();

        string pick;
        if (free.Count > 0)
        {
            pick = free.RandomElement();
            return TryAssignVoice(p, pick, stealIfTaken: false);
        }
        else
        {
            // Exhausted: pick any voice from the pool
            pick = pool.RandomElement();

            // Option A (default): STEAL to preserve uniqueness
            // return TryAssignVoice(p, pick, stealIfTaken: true);

            // Option B: ALLOW DUPLICATE (comment above line, uncomment below)
            pawnVoices[p] = pick; // duplicate allowed
            pawnVoiceProviders[p] = Settings.TTSProviderSetting.Value; // Store current provider
            return true;
        }
    }

    // Optional: call this periodically or when pawns die/leave to free voices
    public void PruneDeadOrInvalidOwners()
    {
        var toUnassign = pawnVoices.Keys.Where(p => !IsValidHumanlike(p)).ToList();
        foreach (var p in toUnassign) UnassignVoice(p);
    }

    public static VoiceWorldComp Get() => Find.World.GetComponent<VoiceWorldComp>();
}

public static class ChanceUtil
{
    public static bool IsSelected(string value)
    {
        int percent = value switch
        {
            "Chitchat" => Settings.ChitchatTalkChance.Value,
            "DeepTalk" => Settings.DeepTalkTalkChance.Value,
            "Slight" => Settings.SlightTalkChance.Value,
            "Insult" => Settings.InsultTalkChance.Value,
            "KindWords" => Settings.KindWordsTalkChance.Value,
            "AnimalChat" => Settings.AnimalChatTalkChance.Value,
            "TameAttempt" => Settings.TameAttemptTalkChance.Value,
            "TrainAttempt" => Settings.TrainAttemptTalkChance.Value,
            "Nuzzle" => Settings.NuzzleTalkChance.Value,
            "ReleaseToWild" => Settings.ReleaseToWildTalkChance.Value,
            "BuildRapport" => Settings.BuildRapportTalkChance.Value,
            "RecruitAttempt" => Settings.RecruitAttemptTalkChance.Value,
            "SparkJailbreak" => Settings.SparkJailbreakTalkChance.Value,
            "RomanceAttempt" => Settings.RomanceAttemptTalkChance.Value,
            "MarriageProposal" => Settings.MarriageProposalTalkChance.Value,
            "Breakup" => Settings.BreakupTalkChance.Value,
            _ => 0 // Unknown interaction type → 0% chance
        };

        float p = Clamp01(percent / 100f);
        return Rand.Value < p;
    }

    private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
}