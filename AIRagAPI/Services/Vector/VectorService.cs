using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AIRagAPI.Services.Vector;

public class VectorService : IVectorService
{
    private readonly ILogger<VectorService> _logger;
    private readonly QdrantClient _qdrant;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private const string CollectionName = "documents_gemini_3072";

    public VectorService(
        IConfiguration config,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<VectorService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;

        string endpoint = config["Qdrant:ApiEndpoint"] 
                          ?? throw new InvalidOperationException("Qdrant:ApiEndpoint is required in configuration.");
        string apiKey = config["Qdrant:ApiKey"] 
                        ?? throw new InvalidOperationException("Qdrant:ApiKey is required for cloud.");

        string host = endpoint
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        _qdrant = new QdrantClient(
            host: host,
            apiKey: apiKey,
            port: 6334,
            https: true
        );

        // Ensure the collection exists (Gemini embedding size 1024)
        EnsureCollectionExists().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Add a document by splitting into chunks and generating Gemini embeddings
    /// </summary>
    public async Task AddDocumentAsync(string text)
    {
        string docId = Guid.NewGuid().ToString();
        var chunks = ChunkText(text, chunkSize: 150); // smaller chunks for Gemini

        var embeddings = await _embeddingGenerator.GenerateAsync(chunks);

        var points = new List<PointStruct>();
        for (int i = 0; i < chunks.Count; i++)
        {
            points.Add(new PointStruct
            {
                Id = Guid.Parse(ComputeUuidFromText(chunks[i])),
                Vectors = embeddings[i].Vector.ToArray(),
                Payload =
                {
                    ["text"] = chunks[i],
                    ["docId"] = docId,
                    ["chunkIndex"] = i,
                    ["source"] = "input"
                }
            });
        }

        await _qdrant.UpsertAsync(CollectionName, points);
    }

    /// <summary>
    /// Search for relevant text using hybrid (vector + keyword) scoring
    /// </summary>
    public async Task<List<string>> SearchAsync(string query, ulong top = 5)
    {
        var embeddings = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = embeddings.First().Vector.ToArray();

        var vectorResults = await _qdrant.QueryAsync(
            collectionName: CollectionName,
            query: vector,
            limit: top * 2
        );

        var queryWords = query
            .ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = vectorResults
            .Where(r => r.Payload.ContainsKey("text"))
            .Select(r =>
            {
                var chunk = r.Payload["text"]?.ToString() ?? "";
                int keyWordScore = queryWords.Count(w => chunk.ToLower().Contains(w));
                double finalScore = r.Score + (keyWordScore * 0.1);
                return new { Text = chunk, Score = finalScore };
            });

        return scored
            .OrderByDescending(x => x.Score)
            .Take((int)top)
            .Select(x => x.Text)
            .ToList();
    }

    private async Task EnsureCollectionExists()
    {
        const ulong expectedDimension = 3072; // Gemini model embedding size
        bool exists = await _qdrant.CollectionExistsAsync(CollectionName);
        if (!exists)
        {
            await _qdrant.CreateCollectionAsync(CollectionName, new VectorParams
            {
                Size = expectedDimension,
                Distance = Distance.Cosine
            });
        }
    }

    private static string ComputeUuidFromText(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return new Guid(hash.Take(16).ToArray()).ToString();
    }

    private static List<string> ChunkText(string text, int chunkSize = 150)
    {
        var words = text.Split(' ');
        var chunks = new List<string>();
        for (int i = 0; i < words.Length; i += chunkSize)
            chunks.Add(string.Join(" ", words.Skip(i).Take(chunkSize)));
        return chunks;
    }
}