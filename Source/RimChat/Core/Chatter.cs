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

        if (chat.AIChat == null && DateTime.Now > next_talk && talked_to != null)
        {
            // If the chat has not been talked about yet, start the talk
            if (chat.Entry is PlayLogEntry_Interaction interaction)
            {
                var initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                if (initiator != pawn) { return; }
            }

            // Start the talk
            chat.AIChat = chat.Talk(Settings.TextAPIKey.Value, talked_to, Find.History.archive.ArchivablesListForReading);
            is_up = pawn;
            next_talk = DateTime.Now.AddMinutes(1);

            chat.AlreadyPlayed = true;
            Log.Message($"Next talk: {next_talk}");
        }
        else if (chat.AIChat is not null && !chat.AIChat.IsCompleted)
        {
            // Message is still being waited on, do nothing
            return;
        }
        else if (chat.AIChat is not null && chat.AIChat.IsCompleted)
        {
            var result = chat.AIChat.Result;
            Log.Message($"Returned text: {result}");
            chat.AIChat = null;
            talked_to = null;
                
            var db = VoiceWorldComp.Get();
            Log.Message($"chat: {chat.Entry}  pawn: {chat.pawn.Name} is_up: {is_up}");
            chat.AlreadyPlayed = false;
            var voice = db.GetVoice(chat.pawn);
            Log.Message($"chat: {result} voice dict {voice}");

            if (Settings.TTSProviderSetting.Value == TTSProvider.OpenAI)
            {
                await chat.VocalizeOpenAI(result, voice);
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
        Log.Message($"kind of talk {kind_of_talk.defName} choosen: {choosenTalk }");

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

            Log.Message($"get voice value {db.GetVoice((initiator))}");

            if (db.GetVoice(initiator) == "" || db.GetVoice(initiator) == null)
            {
                db.TryAssignRandomVoice(initiator);
            }
            Log.Message($"get voice value after {db.GetVoice((initiator))}");
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


    // --- Main storage: pawn -> voice ---
    private Dictionary<Pawn, string> pawnVoices = new Dictionary<Pawn, string>();
    // Reverse index ensures uniqueness: voice -> owner
    private Dictionary<string, Pawn> voiceIndex = new Dictionary<string, Pawn>(StringComparer.OrdinalIgnoreCase);
    
    // Scribe helpers
    private List<Pawn> _keys;
    private List<string> _vals;

    public VoiceWorldComp(World world) : base(world) { }

    public override void ExposeData()
    {
        base.ExposeData();

        // Save the assignments
        Scribe_Collections.Look(ref pawnVoices, "pawnVoices",
            LookMode.Reference, LookMode.Value, ref _keys, ref _vals);
        Log.Message("pawnVoices");
        Log.Message(pawnVoices);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {

            Log.Message("Pruning voices?");
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

    private IEnumerable<string> PoolFor(Pawn p)
    {
        var useOpenAI = Settings.TTSProviderSetting.Value == TTSProvider.OpenAI;

        if (useOpenAI)
        {
            if (p?.gender == Gender.Male) return openAIMalePool;
            if (p?.gender == Gender.Female) return openAIFemalePool;
            return openAIMalePool.Concat(openAIFemalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            if (p?.gender == Gender.Male) return malePool;
            if (p?.gender == Gender.Female) return femalePool;
            return malePool.Concat(femalePool).Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ---------- Read API ----------
    public string GetVoice(Pawn p) =>
        p != null && pawnVoices.TryGetValue(p, out var v) ? v : null;

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
        voiceIndex[voice] = p;
        return true;
    }

    public void UnassignVoice(Pawn p)
    {
        if (p == null) return;
        if (pawnVoices.TryGetValue(p, out var old))
        {
            pawnVoices.Remove(p);
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
    // values: ["a","b","c"], percents: [10,20,30]
    public static bool IsSelected(string value)
    {
         // possible_talks = { "Chitchat", "DeepTalk"};
         string[] values = new string[]
         {
             "Chitchat",
             "DeepTalk",
             "Slight",
             "Insult",
             "KindWords",
             "AnimalChat",
             "TameAttempt",
             "TrainAttempt",
             "Nuzzle",
             "ReleaseToWild",
             "BuildRapport",
             "RecruitAttempt",
             "SparkJailbreak",
             "RomanceAttempt",
             "MarriageProposal",
             "Breakup"
         };
         int[] percents =  new int[]
         {
             100,
             10,
             50,
             75,
             50,
             50,
             10,
             10,
             10,
             100,
             90,
             90,
             100,
             100,
             100,
             100
         };
            
        if (values == null || percents == null) return false;
        int n = Math.Min(values.Length, percents.Length);

        for (int i = 0; i < n; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                float p = Clamp01(percents[i] / 100f);
                return Rand.Value < p; // or Rand.Chance(p)
            }
        }
        return false; // value not found → treat as 0%
    }

    private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
}