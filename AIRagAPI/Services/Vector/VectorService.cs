using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AIRagAPI.Services.Vector;

public class VectorService: IVectorService
{
    private readonly ILogger<VectorService> _logger;
    private readonly Kernel _kernel;
    private readonly QdrantClient _qdrant;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private const string CollectionName = "documents_nomic_768";
    
    public VectorService(Kernel kernel, IConfiguration config, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, ILogger<VectorService> logger)
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
        _logger = logger;
    }

    /// <summary>
    /// Create a new text vector with chunking (i.e document - structured container)
    /// </summary>
    /// <param name="text"></param>
    public async Task AddDocumentAsync(string text)
    {
        var docId = Guid.NewGuid().ToString();
        //Create Chunks for text input
        var chunks = ChunkText(text);
        var result = await _embeddingGenerator.GenerateAsync(chunks);
        
        // Create points for each chunks
        var points = new List<PointStruct>();
        
        for (int i = 0 ; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var vector = result[i].Vector.ToArray();
            
            points.Add(new PointStruct() // Qdrant record consisting of a vector and optional payload
            {
                Id = Guid.NewGuid(),
                Vectors = vector, // Vector - A one dimensional resizable array
                Payload =
                {
                    ["text"] = chunk,
                    ["docId"] = docId,
                    ["chunkIndex"] = i,
                    ["source"] = "input"
                }
            });
        }
        
        await _qdrant.UpsertAsync(CollectionName, points);
    }

    /// <summary>
    /// Search for text vector similarities - Hybrid Search (Vector + Keyword)
    /// </summary>
    /// <param name="query"></param>
    /// <param name="top"></param>
    /// <returns></returns>
    public async Task<List<string>> SearchAsync(string query, ulong top = 5)
    {
        // Vector Search (existing)
        var result = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = result.First().Vector.ToArray();

        var vectorResults = await _qdrant.QueryAsync(
            collectionName: CollectionName,
            query: vector, 
            limit: top * 2 // Get more candidates
        );
        
        // Keyword Scoring
        var queryWords = query
            .ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = vectorResults
            .Where(r => r.Payload.ContainsKey("text"))
            .Select(r =>
            {
                var chunk = r.Payload["text"]?.ToString() ?? "";
                var chunkLower = chunk.ToLower();
            
                // Count how many query words are contained in a chunk for ranking
                int keyWordScore = queryWords.Count(w => chunkLower.Contains(w)); 
            
                //Combine Scores
                double finalScore = r.Score + (keyWordScore * 0.1);
                return new
                {
                    Text = chunk,
                    Score = finalScore
                };
            });

        // Re-rank
        return scored
            .OrderByDescending(x => x.Score)
            .Take((int)top)
            .Select(x => x.Text)
            .ToList();
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
    /// Check if a vector collection exists, creates new if it doesn't
    /// </summary>
    private async Task EnsureCollectionExists()
    {
        const ulong expectedDimension = 768; // ← for nomic-embed-text
        var exists = await _qdrant.CollectionExistsAsync(CollectionName);
        if (!exists)
        {
            await _qdrant.CreateCollectionAsync(
                CollectionName, 
                new VectorParams
                {
                    Size = expectedDimension, // Embedding Size (MiniLM),
                    Distance = Distance.Cosine // Distance metric for comparing vectors - Cosine works well for semantic search
                });
        }
    }
    
    /// <summary>
    /// Create a Uuid from text - To avoid duplicate text embedding
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static string ComputeUuidFromText(string text)
    {
        // Simple: use SHA256 hash of the text, then take first 36 chars as UUID-like string
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        var guid = new Guid(hash.Take(16).ToArray());  // first 128 bits → valid GUID
        return guid.ToString();
    }

    /// <summary>
    /// Create text chunks to reduce retrieval quality drops and reduce inaccuracy when text/document gets long
    /// </summary>
    /// <param name="text"></param>
    /// <param name="chunkSize"></param>
    /// <returns></returns>
    private static List<string> ChunkText(string text, int chunkSize = 300)
    {
        var words = text.Split(' ');
        var chunks = new List<string>();

        for (int i = 0; i < words.Length; i += chunkSize)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize));
            chunks.Add(chunk);
        }
        return chunks;
    }
}