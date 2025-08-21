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

namespace RimChat.Core;

public static class Chatter
{
    private const float LabelPositionOffset = -0.6f;
    private static bool CanRender() => WorldRendererUtility.CurrentWorldRenderMode is WorldRenderMode.None or WorldRenderMode.Background;
    private static Dictionary<Pawn, Chat> Dictionary = new();
    private static System.DateTime next_talk = DateTime.Now;
    public static void Talk()
    {
        var altitude = GetAltitude();
        if (altitude <= 0 || altitude > 40) { return; }

        var selected = Find.Selector!.SingleSelectedObject as Pawn;

        foreach (var pawn in Dictionary.Keys.OrderBy(pawn => pawn == selected).ThenBy(static pawn => pawn.Position.y).ToArray()) { DrawBubble(pawn, pawn == selected); }
    }

    private static void DrawBubble(Pawn pawn, bool isSelected)
    {
        if (!CanRender() || !pawn.Spawned || pawn.Map != Find.CurrentMap || pawn.Map!.fogGrid!.IsFogged(pawn.Position)) { return; }

        var random = new System.Random();
        var randomEntry = Dictionary.ElementAt(random.Next(Dictionary.Count));
        var chat = randomEntry.Value;
        if (chat.AIChat == null && DateTime.Now > next_talk)
        {
            // If the chat has not been talked about yet, start the talk
            if (chat.Entry is PlayLogEntry_Interaction interaction)
            {
                var initiator = (Pawn?)Reflection.Verse_PlayLogEntry_Interaction_Initiator.GetValue(interaction);
                if (initiator != pawn) { return; }
            }

            // Start the talk
            chat.AIChat = chat.Talk(isSelected, Settings.TextAPIKey.Value);
            next_talk = DateTime.Now.AddMinutes(1);
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
            Log.Message($"Started chat for {pawn.Name} with entry {chat.Entry.Tick} at {chat.LastTalked}");
            chat.AIChat = null;
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
