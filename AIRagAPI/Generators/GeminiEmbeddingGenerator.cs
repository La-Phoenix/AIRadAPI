using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AIRagAPI.Generators;

public class GeminiEmbeddingGenerator(
    HttpClient httpClient,
    ILogger<GeminiEmbeddingGenerator> logger,
    string modelName = "gemini-embedding-001")
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private string BuildUrl() => $"v1beta/models/{modelName}:embedContent";

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> inputs,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var semaphore = new SemaphoreSlim(5); // control concurrency

        var tasks = inputs
            .Select(input => ProcessInputAsync(input, semaphore, cancellationToken))
            .ToList(); // prevents semaphore disposal warning

        var embeddings = await Task.WhenAll(tasks);

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }
    private async Task<Embedding<float>> ProcessInputAsync(
        string input,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var payload = new
            {
                content = new
                {
                    parts = new[]
                    {
                        new { text = input }
                    }
                }
            };

            var response = await httpClient.PostAsJsonAsync(BuildUrl(), payload, cancellationToken);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogDebug("Gemini response received. Length: {Length}", raw.Length);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Gemini API failed: {Status} - {Raw}",
                    response.StatusCode, raw);

                throw new HttpRequestException(
                    $"Gemini API failed with status {response.StatusCode}");
            }

            GeminiEmbedResponse? result;

            try
            {
                result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Gemini response: {Raw}", raw);
                throw;
            }

            if (result?.Embedding?.Values == null || result.Embedding.Values.Length == 0)
                throw new Exception($"Gemini returned no embeddings. Raw: {raw}");

            return new Embedding<float>(result.Embedding.Values);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // public async Task<Embedding<float>> GenerateAsync(string text, CancellationToken ct = default)
    // {
    //     var results = await GenerateAsync(new[] { text }, cancellationToken: ct);
    //     return results.First();
    // }

    private class GeminiEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public EmbeddingValues? Embedding { get; set; }
    }

    private class EmbeddingValues
    {
        [JsonPropertyName("values")]
        public float[]? Values { get; set; }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}