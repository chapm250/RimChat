using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using LudeonTK;
using UnityEngine;
using Verse;
using System.EnterpriseServices;
using System.Net;

namespace RimChat.Core;

public class Chat(Pawn pawn, LogEntry entry)
{
    private static readonly Regex RemoveColorTag = new("<\\/?color[^>]*>");
    public LogEntry Entry { get; } = entry;

    public async Task<bool> Talk(bool isSelected, string chatgpt_api_key)
    {
        var text = Entry.ToGameStringFromPOV(pawn);
        var response = await GetOpenAIResponseAsync(chatgpt_api_key, text);
        // Parse the response JSON and extract output->content->text
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
                        Log.Message(textElement.GetString());
                        return response != null;
                    }
                }
            }
        }
        Log.Message("No output text found in response.");
        return response != null;
    }

    public async Task<string?> GetOpenAIResponseAsync(string apiKey, string input)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        string instructions = @"You are a pawn in Rimworld named Sancho talking to his wife Mando in english.
Respond to Mando in 1 - 3 sentences.
Do not reference objects as if they are nearby, just talk about them in the abstract or as memories.
Do not speak for Mando";

        var requestBody = new
        {
            model = "gpt-5",
            input = input,
            instructions = instructions,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/responses", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseBody = await response.Content.ReadAsStringAsync();
        return responseBody;
    }
}