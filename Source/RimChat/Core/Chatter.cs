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
    private static readonly Dictionary<Pawn, List<Chat>> Dictionary = new();
    public static void Talk()
    {
        var altitude = GetAltitude();
        if (altitude <= 0 || altitude > 40) { return; }

        var selected = Find.Selector!.SingleSelectedObject as Pawn;

        foreach (var pawn in Dictionary.Keys.OrderBy(pawn => pawn == selected).ThenBy(static pawn => pawn.Position.y).ToArray()) { DrawBubble(pawn, pawn == selected); }
    }
    private static void Remove(Pawn pawn, Chat chat)
    {
        Dictionary[pawn]!.Remove(chat);
        if (Dictionary[pawn]!.Count is 0) { Dictionary.Remove(pawn); }
    }

    private static void DrawBubble(Pawn pawn, bool isSelected)
    {
        if (!CanRender() || !pawn.Spawned || pawn.Map != Find.CurrentMap || pawn.Map!.fogGrid!.IsFogged(pawn.Position)) { return; }

        var count = 0;

        foreach (var chat in Dictionary[pawn].OrderByDescending(static chat => chat.Entry.Tick).ToArray())
        {
            if (count > 1) { return; }
            chat.Talk(isSelected, Settings.TextAPIKey.Value);
            if (chat.AudioSource is not null && !chat.AudioSource.isPlaying)
            {
                chat.AudioSource.Play();
            }
            Remove(pawn, chat);
            count++;
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


        if (!Dictionary.ContainsKey(initiator)) { Dictionary[initiator] = []; }

        Dictionary[initiator]!.Add(new Chat(initiator, entry));
    }
    private static float GetAltitude()
    {
        var altitude = Mathf.Max(1f, (float)Reflection.Verse_CameraDriver_RootSize.GetValue(Find.CameraDriver));
        Compatibility.Apply(ref altitude);

        return altitude;
    }
}
