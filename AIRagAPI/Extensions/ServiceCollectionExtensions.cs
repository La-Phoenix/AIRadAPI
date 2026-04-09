using AIRagAPI.Agents;
using AIRagAPI.Common.UserContext;
using AIRagAPI.Generators;
using AIRagAPI.Services.Agents;
using AIRagAPI.Services.Auth;
using AIRagAPI.Services.ChatService;
using AIRagAPI.Services.Vector;
using Microsoft.Extensions.AI;

namespace AIRagAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContextService, UserContextService>(); // Scoped to request to prevent accidental user data leakage between requests
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<GeminiEmbeddingGenerator>());
        services.AddSingleton<IVectorService, VectorService>();
        services.AddScoped<AgentCoordinator>();
        services.AddScoped<IAuthService, AuthService>(); // Since AuthService has a scope dependency - DbContext
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAgent, RetrieverAgent>();
        services.AddScoped<IAgent, SummarizeAgent>();
        return services;
    }
}