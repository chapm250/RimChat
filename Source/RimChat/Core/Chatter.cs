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
    private static readonly string[] male_voices = { "NYkjXRso4QIcgWakN1Cr", "XjLkpWUlnhS8i7gGz3lZ", "zNsotODqUhvbJ5wMG7Ei", "MFZUKuGQUsGJPQjTS4wC", "4dZr8J4CBeokyRkTRpoN" };
    private static readonly string[] female_voices = { "4tRn1lSkEn13EVTuqb0g", "eUdJpUEN3EslrgE24PKx", "kNie5n4lYl7TrvqBZ4iG", "g6xIsTj2HwM6VR4iXFCw", "jqcCZkN6Knx8BJ5TBdYR" };
    private const float LabelPositionOffset = -0.6f;
    private static bool CanRender() => WorldRendererUtility.CurrentWorldRenderMode is WorldRenderMode.None or WorldRenderMode.Background;
    private static Dictionary<Pawn, Chat> Dictionary = new();

    private static Dictionary<Pawn, string> VoiceDict = new();
    private static System.DateTime next_talk = DateTime.Now;

    private static Pawn? talked_to;

    private static Pawn? is_up;

    public static void Talk()
    {
        var altitude = GetAltitude();
        if (altitude <= 0 || altitude > 40) { return; }

        var selected = Find.Selector!.SingleSelectedObject as Pawn;

        DrawBubble();
    }

    private static async Task DrawBubble()
    {
        var candidates = Dictionary.Where(kvp => !kvp.Value.AlreadyPlayed).ToList();
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
            Log.Message($"chat: {chat.Entry} voice dict {VoiceDict[pawn]}");
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
            Log.Message($"chat: {chat.Entry}  pawn: {pawn} is_up: {is_up}");
            chat.AlreadyPlayed = false;
            await chat.Vocalize(result, VoiceDict[]);
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
            case PlayLogEntry_InteractionSinglePawn interaction:
                initiator = (Pawn?)Reflection.Verse_PlayLogEntry_InteractionSinglePawn_Initiator.GetValue(interaction);
                kind_of_talk = (InteractionDef?)Reflection.Verse_PlayLogEntry_Interaction_Type.GetValue(interaction);
                recipient = null;
                talked_to = null;
                break;
            default:
                return;
        }


        if (initiator is null || initiator.Map != Find.CurrentMap) { return; }


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

        string pawn_sex = initiator.gender.ToString();

        if (!VoiceDict.ContainsKey(initiator))
        {
            if (pawn_sex == "Female")
            {
                var random = new System.Random();
                string voice;
                var assignedVoices = VoiceDict.Values.ToHashSet();
                var availableVoices = female_voices.Where(v => !assignedVoices.Contains(v)).ToList();
                if (availableVoices.Count > 0)
                {
                    voice = availableVoices[random.Next(availableVoices.Count)];
                }
                else
                {
                    voice = female_voices[random.Next(female_voices.Length)];
                }
                Log.Message($"Setting female voice for {initiator} who is a {pawn_sex} to {voice}");
                VoiceDict[initiator] = voice;
            }
            else
            {

                var random = new System.Random();
                string voice;
                var assignedVoices = VoiceDict.Values.ToHashSet();
                var availableVoices = male_voices.Where(v => !assignedVoices.Contains(v)).ToList();
                if (availableVoices.Count > 0)
                {
                    voice = availableVoices[random.Next(availableVoices.Count)];
                }
                else
                {
                    voice = male_voices[random.Next(male_voices.Length)];
                }
                Log.Message($"Setting male voice for {initiator} who is a {pawn_sex} to {voice}");
                VoiceDict[initiator] = voice;

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
