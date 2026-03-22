using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace AIRagAPI.Generators;

public class OllamaEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName; // e.g. "nomic-embed-text"

    public OllamaEmbeddingGenerator(HttpClient httpClient, string modelName = "nomic-embed-text")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _modelName = modelName;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<Embedding<float>>();

        // Ollama supports batch input as array — send all at once for efficiency
        var requestBody = new
        {
            model = _modelName,
            input = values.ToArray() // array for batch
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embed", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        // Console.WriteLine($"Ollama raw response: {rawJson}"); // ← keep for debugging, then use ILogger

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: cancellationToken);

        if (result?.Embeddings == null || !result.Embeddings.Any())
        {
            throw new InvalidOperationException($"Ollama returned no embeddings. Raw: {rawJson}");
        }

        // Ollama returns list of arrays for batch input
        foreach (var vector in result.Embeddings)
        {
            embeddings.Add(new Embedding<float>(vector));
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    // For single-string convenience if needed elsewhere
    public async Task<Embedding<float>> GenerateAsync(string text, CancellationToken ct = default)
    {
        var results = await GenerateAsync(new[] { text }, cancellationToken: ct);
        return results.First();
    }

    private class OllamaEmbedResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; set; }
    }

    // Required stubs
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}