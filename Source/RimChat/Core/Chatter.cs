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

namespace RimChat.Core;

public static class Chatter
{
    private const float LabelPositionOffset = -0.6f;
    private static bool CanRender() => WorldRendererUtility.CurrentWorldRenderMode is WorldRenderMode.None or WorldRenderMode.Background;
    private static Dictionary<Pawn, Chat> Dictionary = new();
    private static System.DateTime next_talk = DateTime.Now;

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



        if (chat.AIChat == null && DateTime.Now > next_talk)
        {
            // If the chat has not been talked about yet, start the talk
            if (chat.Entry is PlayLogEntry_Interaction interaction)
            {
                var initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                if (initiator != pawn) { return; }
            }

            // Start the talk
            chat.AIChat = chat.Talk(Settings.TextAPIKey.Value);
            is_up = pawn;
            next_talk = DateTime.Now.AddMinutes(1);
            chat.AlreadyPlayed = true;
            Log.Message($"chat: {chat.Entry}  pawn: {pawn} is_up: {is_up}");
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
            Log.Message($"chat: {chat.Entry}  pawn: {pawn} is_up: {is_up}");
            await chat.Vocalize(result);
            is_up = null;
        }
        else
        {
            // Message has been completed, remove it
            return;
        }
    }
    public static void Add(LogEntry entry)
    {
        if (!CanRender()) { return; }

        Pawn? initiator, recipient;

        switch (entry)
        {
            case PlayLogEntry_Interaction interaction:
                initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                recipient = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Recipient.GetValue(interaction);
                break;
            case PlayLogEntry_InteractionSinglePawn interaction:
                initiator = (Pawn?)Reflection.Verse_PlayLogEntry_InteractionSinglePawn_Initiator.GetValue(interaction);
                recipient = null;
                break;
            default:
                return;
        }

        if (initiator is null || initiator.Map != Find.CurrentMap) { return; }


        if (!Dictionary.ContainsKey(initiator)) { Dictionary[initiator] = new Chat(initiator, entry); }
        else
        {
            Dictionary[initiator].Entry = entry;
        }

    }
    private static float GetAltitude()
    {
        var altitude = Mathf.Max(1f, (float)Reflection.Verse_CameraDriver_RootSize.GetValue(Find.CameraDriver));
        Compatibility.Apply(ref altitude);

        return altitude;
    }
}
