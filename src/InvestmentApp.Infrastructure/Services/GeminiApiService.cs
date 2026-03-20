using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using InvestmentApp.Application.Common.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class GeminiApiService : IAiChatService
{
    private readonly HttpClient _httpClient;

    public GeminiApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<AiStreamChunk> StreamChatAsync(
        string apiKey,
        string model,
        string systemPrompt,
        List<AiChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Build Gemini request body
        var contents = messages.Select(m => new
        {
            role = m.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = m.Content } }
        }).ToArray();

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { maxOutputTokens = 4096 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"v1beta/models/{model}:streamGenerateContent?alt=sse&key={apiKey}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException)
        {
            response = null!;
        }
        catch (TaskCanceledException)
        {
            yield break;
        }

        if (response == null)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Lỗi kết nối đến Gemini API." };
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var errorMsg = response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => $"Yêu cầu không hợp lệ: {ParseErrorMessage(errorBody)}",
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    "API key không hợp lệ. Vui lòng kiểm tra lại trong Cài đặt AI.",
                System.Net.HttpStatusCode.TooManyRequests =>
                    $"Vượt giới hạn tốc độ API. {ParseErrorMessage(errorBody)}",
                _ => $"Lỗi Gemini API ({(int)response.StatusCode}): {ParseErrorMessage(errorBody)}"
            };
            yield return new AiStreamChunk { Type = "error", ErrorMessage = errorMsg };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int totalInputTokens = 0, totalOutputTokens = 0;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            using (doc)
            {
                // Extract text from candidates[0].content.parts[0].text
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var part = parts[0];
                        if (part.TryGetProperty("text", out var textVal))
                        {
                            yield return new AiStreamChunk
                            {
                                Type = "text",
                                Text = textVal.GetString()
                            };
                        }
                    }
                }

                // Extract usage from usageMetadata
                if (doc.RootElement.TryGetProperty("usageMetadata", out var usage))
                {
                    if (usage.TryGetProperty("promptTokenCount", out var promptTok))
                        totalInputTokens = promptTok.GetInt32();
                    if (usage.TryGetProperty("candidatesTokenCount", out var candTok))
                        totalOutputTokens = candTok.GetInt32();
                }

                // Check for errors
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var errMsg = "Lỗi từ Gemini API.";
                    if (error.TryGetProperty("message", out var errText))
                        errMsg = errText.GetString() ?? errMsg;
                    yield return new AiStreamChunk { Type = "error", ErrorMessage = errMsg };
                    yield break;
                }
            }
        }

        // Gemini sends usage in chunks; yield final usage after stream completes
        if (totalInputTokens > 0 || totalOutputTokens > 0)
        {
            yield return new AiStreamChunk
            {
                Type = "usage",
                InputTokens = totalInputTokens,
                OutputTokens = totalOutputTokens
            };
        }
    }

    private static string ParseErrorMessage(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { }
        return body.Length > 200 ? body[..200] : body;
    }
}
