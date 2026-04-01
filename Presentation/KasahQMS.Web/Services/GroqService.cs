using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KasahQMS.Web.Services;

public interface IGroqService
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(IReadOnlyList<GroqChatMessage> messages, CancellationToken cancellationToken = default);
}

public class GroqChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class GroqService : IGroqService
{
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(new[]
        {
            new GroqChatMessage { Role = "user", Content = prompt }
        }, cancellationToken);
    }

    public async Task<string> GenerateAsync(IReadOnlyList<GroqChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Groq:ApiKey"] ?? _configuration["GROQ_API_KEY"];
        var model = _configuration["Groq:Model"] ?? _configuration["GROQ_MODEL"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "llama-3.3-70b-versatile";
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured. Set Groq:ApiKey.");
        }

        if (messages.Count == 0)
        {
            throw new InvalidOperationException("At least one message is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            temperature = 0.7
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq API failed: {StatusCode} {Body}", response.StatusCode, responseText);
            var providerMessage = TryExtractProviderErrorMessage(responseText);
            if (!string.IsNullOrWhiteSpace(providerMessage))
            {
                throw new InvalidOperationException($"AI request failed: {providerMessage}");
            }

            throw new InvalidOperationException("AI service request failed.");
        }

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI service returned an empty response.");
        }

        return content.Trim();
    }

    private static string? TryExtractProviderErrorMessage(string responseText)
    {
        try
        {
            using var errorDoc = JsonDocument.Parse(responseText);
            var root = errorDoc.RootElement;
            if (root.TryGetProperty("error", out var errorElement) &&
                errorElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }
        }
        catch
        {
            // Ignore parse failures and return null
        }

        return null;
    }
}
