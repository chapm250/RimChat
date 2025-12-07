using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimChat.Access;

public static class Reflection
{
    public static readonly FieldInfo Verse_PlayLogEntry_Interaction_Initiator = AccessTools.Field(typeof(PlayLogEntry_Interaction), "initiator");
    public static readonly FieldInfo Verse_PlayLogEntry_Interaction_Recipient = AccessTools.Field(typeof(PlayLogEntry_Interaction), "recipient");
    public static readonly FieldInfo Verse_PlayLogEntry_Interaction_Type = AccessTools.Field(typeof(PlayLogEntry_Interaction), "intDef");
    public static readonly FieldInfo Verse_PlayLogEntry_InteractionSinglePawn_Initiator = AccessTools.Field(typeof(PlayLogEntry_InteractionSinglePawn), "initiator");

    public static readonly FieldInfo Verse_CameraDriver_RootSize = AccessTools.Field(typeof(CameraDriver), "rootSize");
    public static readonly FieldInfo Verse_PawnUIOverlay_Pawn = AccessTools.Field(typeof(PawnUIOverlay), "pawn");
}
