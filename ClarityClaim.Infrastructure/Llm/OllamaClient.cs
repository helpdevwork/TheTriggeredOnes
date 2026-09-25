using System.Text;
using System.Text.Json;
using ClarityClaim.Domain.Interfaces;

namespace ClarityClaim.Infrastructure.Llm;

// Calls an Ollama server's OpenAI-compatible API (POST {baseUrl}/v1/chat/completions).
// Base URL is configurable (Llm:BaseUrl) -- defaults to the confirmed server at
// http://172.50.50.83:11434. All model calls go through this one class.
public class OllamaClient : ILanguageModelClient
{
    private readonly HttpClient _http;

    // Model IDs confirmed available on the configured Ollama server (GET /v1/models).
    // No embedding model is available there, so RAG retrieval is lexical (TF-IDF),
    // not vector-based -- see PolicyLexicalSearchService.
    public static class Models
    {
        // SOAP generation -- the largest available model, for the most complex single-pass reasoning task
        public const string CLINICAL_REASONING = "gpt-oss:20b";
        // Validation -- two independent models for an ensemble second opinion (mirrors the original Qwen+Gemma design intent)
        public const string VALIDATION_PRIMARY = "granite3.3:latest";
        public const string VALIDATION_SECONDARY = "llama3:latest";
        // Patient summary -- lighter task, faster instruct model
        public const string SUMMARY = "mistral:7b-instruct";
        // qwen2.5-coder:7b is available on the server but unused here -- it's coding-specialized,
        // not a fit for clinical documentation or plain-language summaries.
    }

    public OllamaClient(IHttpClientFactory factory)
        => _http = factory.CreateClient("ollama");

    public async Task<string> CompleteAsync(
        string modelId, string systemPrompt, string userPrompt,
        float temperature = 0.1f, int maxTokens = 4096,
        CancellationToken ct = default)
    {
        var body = new
        {
            model = modelId,
            temperature = temperature,
            max_tokens = maxTokens,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync("v1/chat/completions", content, ct);
        resp.EnsureSuccessStatusCode();

        var result = await JsonSerializer.DeserializeAsync<OllamaChatResponse>(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return result?.Choices[0].Message.Content ?? string.Empty;
    }
}

// Response shape DTOs (internal to Infrastructure) -- OpenAI-compatible chat completion shape
internal record OllamaChatResponse(OllamaChoice[] Choices);
internal record OllamaChoice(OllamaMessage Message);
internal record OllamaMessage(string Role, string Content);
