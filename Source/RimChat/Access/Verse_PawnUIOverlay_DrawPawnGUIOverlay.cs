using HarmonyLib;
using UnityEngine;
using Verse;
using RimChat.Core;

namespace RimChat.Access;

[HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
public static class Verse_PawnUIOverlay_DrawPawnGUIOverlay
{
    private static void Postfix(PawnUIOverlay __instance)
    {
        var pawn = (Pawn)Reflection.Verse_PawnUIOverlay_Pawn.GetValue(__instance);
        if (pawn == null || !pawn.Spawned) return;

        var chat = Chatter.GetChat(pawn);
        if (chat?.AudioSource != null && chat.AudioSource.isPlaying)
        {
            var drawLoc = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
            GenMapUI.DrawPawnLabel(pawn, drawLoc, 1f, 9999f, null, GameFont.Tiny, true, true);

            var labelRect = new Rect(drawLoc.x - 20f, drawLoc.y + 20f, 40f, 20f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(labelRect, "Talking");
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
