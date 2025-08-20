using System.Text.RegularExpressions;
using LudeonTK;
using UnityEngine;
using Verse;

namespace RimChat.Core;

public class Chat(Pawn pawn, LogEntry entry)
{
    private static readonly Regex RemoveColorTag = new("<\\/?color[^>]*>");
    public LogEntry Entry { get; } = entry;

    private string? _text;
    private string Text => _text ??= GetText();


    private string GetText()
    {
        var text = Entry.ToGameStringFromPOV(pawn);
        Log.Message(text);
        return RemoveColorTag.Replace(text, string.Empty);
    }
    public bool Talk(bool isSelected)
    {
        GetText();
        return true;
    }


    public void Rebuild() => _text = null;
}