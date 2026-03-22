using AIRagAPI.Services.Vector;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;

public class VectorService: IVectorService
{
    private readonly ILogger<VectorService> _logger;
    private readonly Kernel _kernel;
    private readonly QdrantClient _qdrant;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private const string CollectionName = "documents_nomic_768";
    
    public VectorService(Kernel kernel, IConfiguration config, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _kernel = kernel;
        string endpoint = config["Qdrant:ApiEndpoint"] 
                          ?? throw new InvalidOperationException("Qdrant:ApiEndpoint is required in configuration.");

        string apiKey = config["Qdrant:ApiKey"] 
                        ?? throw new InvalidOperationException("Qdrant:ApiKey is required for cloud.");

        // Extract just the hostname (strip scheme if accidentally included)
        string host = endpoint
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');  // clean any trailing slashes

        _qdrant = new QdrantClient(
            host: host,           // e.g., "55b6b428-b48c-4e04-a194-ec72036551cb.eu-west-2-0.aws.cloud.qdrant.io"
            apiKey: apiKey,
            port: 6334,           // gRPC port (standard for cloud clusters)
            https: true           // required for cloud (TLS)
            // Optional: grpcTimeout: TimeSpan.FromSeconds(30), loggerFactory: ...
        );
        
        EnsureCollectionExists().GetAwaiter().GetResult();
        _embeddingGenerator = embeddingGenerator;
    }

    /// <summary>
    /// Create a new text vector
    /// </summary>
    /// <param name="id"></param>
    /// <param name="text"></param>
    public async Task AddDocumentAsync(string text)
    {
        //Generate embedding using embedding Geneartor
        var result = await _embeddingGenerator.GenerateAsync(new[] { text });
        var vector = result[0].Vector.ToArray();

        // Qdrant record record consisting of a vector and optional payload
        var point = new PointStruct
        {
            Id = new PointId { Uuid = ComputeUuidFromText(text) },  // deterministic ID to override for duplicates
            Vectors = vector,
            Payload = { ["text"] = text}
        };
        
        await _qdrant.UpsertAsync(CollectionName, new List<PointStruct> { point });
    }

    /// <summary>
    /// Search for text vector similarities
    /// </summary>
    /// <param name="query"></param>
    /// <param name="top"></param>
    /// <returns></returns>
    public async Task<List<string>> SearchAsync(string query, ulong top = 3)
    {
        var result = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = result.First().Vector.ToArray();

        var searchResult = await _qdrant.QueryAsync(
            collectionName: CollectionName,
            query: vector, 
            limit: top // Top n similarities
        );

        return searchResult
            .Where(r => r.Payload.ContainsKey("text"))
            .Select(r => r.Payload["text"]?.ToString() ?? string.Empty).ToList();
    }

    public async Task<string> AskAsync(string question)
    {
        // Retrieve relevant docs
        var docs = await SearchAsync(question);
        var context = string.Join("\n", docs);
        
        // Build RAG prompt
        var prompt = $@"You are an AI assistant. Answer ONLY from the context below.
                        Context:
                        {context}
                        
                        Question:
                        {question}

                        Answer:
                        ";
        
        // Ask LLM (Ollama via Semantic Kernel)
        var result = await _kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }


    /// <summary>
    /// Check if vecctor collection exists, creates new if it doesn't
    /// </summary>
    private async Task EnsureCollectionExists()
    {
        const ulong ExpectedDimension = 768; // ← for nomic-embed-text
        var exists = await _qdrant.CollectionExistsAsync(CollectionName);
        if (!exists)
        {
            await _qdrant.CreateCollectionAsync(
                CollectionName, 
                new VectorParams
                {
                    Size = ExpectedDimension, // Embedding Size (MiniLM),
                    Distance = Distance.Cosine // Distance metric for comparing vectors - Cosine works well for semantic search
                });
        }
    }
    
    private string ComputeUuidFromText(string text)
    {
        // Simple: use SHA256 hash of the text, then take first 36 chars as UUID-like string
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        var guid = new Guid(hash.Take(16).ToArray());  // first 128 bits → valid GUID
        return guid.ToString();
    }
}