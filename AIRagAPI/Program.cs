using AIRagAPI.Exceptions;
using AIRagAPI.Generators;
using AIRagAPI.Services.Vector;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//For only production
builder.WebHost.UseUrls("http://+:8080");

builder.Services.AddOpenApi();

// Add Semantic Kernel
builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Groq (LLM)
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: "llama-3.3-70b-versatile",
        apiKey: builder.Configuration["Groq:APiKey"],
        endpoint: new Uri(builder.Configuration["Groq:ApiEndpoint"] ?? "")
    );
    
    return kernelBuilder.Build();
});

// Register Gemini Embedding Generator and it's base address
// Add HttpClient for Gemini API
builder.Services.AddHttpClient<GeminiEmbeddingGenerator>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gemini:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("x-goog-api-key", builder.Configuration["Gemini:ApiKey"]);
    client.Timeout = TimeSpan.FromMinutes(30); // Prevent request from hanging when Gemini req hangs
});

// Use as IEmbeddingGenerator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<GeminiEmbeddingGenerator>());

builder.Services.AddSingleton<IVectorService, VectorService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:BaseUrl"]!);
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.AllowCredentials(); // For Cookies/auth
    });
});
builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();

if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();