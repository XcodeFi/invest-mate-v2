using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using InvestmentApp.Application.Common.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class ClaudeApiService : IAiChatService
{
    private readonly HttpClient _httpClient;

    public ClaudeApiService(HttpClient httpClient)
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
        var requestBody = new
        {
            model,
            max_tokens = 4096,
            stream = true,
            system = systemPrompt,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException)
        {
            // Cannot yield in catch — set error and handle below
            response = null!;
        }
        catch (TaskCanceledException)
        {
            yield break;
        }

        if (response == null)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Lỗi kết nối đến Claude API." };
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var errorMsg = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "API key không hợp lệ. Vui lòng kiểm tra lại trong Cài đặt AI.",
                System.Net.HttpStatusCode.TooManyRequests => $"Vượt giới hạn tốc độ API. {ParseErrorMessage(errorBody)}",
                System.Net.HttpStatusCode.BadRequest => $"Yêu cầu không hợp lệ: {ParseErrorMessage(errorBody)}",
                _ => $"Lỗi API ({(int)response.StatusCode}): {ParseErrorMessage(errorBody)}"
            };
            yield return new AiStreamChunk { Type = "error", ErrorMessage = errorMsg };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

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
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "message_start":
                        if (doc.RootElement.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("usage", out var startUsage) &&
                            startUsage.TryGetProperty("input_tokens", out var inputTok))
                        {
                            yield return new AiStreamChunk
                            {
                                Type = "usage",
                                InputTokens = inputTok.GetInt32()
                            };
                        }
                        break;

                    case "content_block_delta":
                        if (doc.RootElement.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("type", out var deltaType) &&
                            deltaType.GetString() == "text_delta" &&
                            delta.TryGetProperty("text", out var textVal))
                        {
                            yield return new AiStreamChunk
                            {
                                Type = "text",
                                Text = textVal.GetString()
                            };
                        }
                        break;

                    case "message_delta":
                        if (doc.RootElement.TryGetProperty("usage", out var endUsage) &&
                            endUsage.TryGetProperty("output_tokens", out var outputTok))
                        {
                            yield return new AiStreamChunk
                            {
                                Type = "usage",
                                OutputTokens = outputTok.GetInt32()
                            };
                        }
                        break;

                    case "error":
                        var errMsg = "Lỗi từ Claude API.";
                        if (doc.RootElement.TryGetProperty("error", out var errObj) &&
                            errObj.TryGetProperty("message", out var errText))
                        {
                            errMsg = errText.GetString() ?? errMsg;
                        }
                        yield return new AiStreamChunk { Type = "error", ErrorMessage = errMsg };
                        yield break;
                }
            }
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
