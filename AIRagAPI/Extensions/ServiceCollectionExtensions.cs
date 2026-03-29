using AIRagAPI.Generators;
using AIRagAPI.Services.Auth;
using AIRagAPI.Services.Vector;
using Microsoft.Extensions.AI;

namespace AIRagAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<GeminiEmbeddingGenerator>());
        services.AddSingleton<IVectorService, VectorService>();
        services.AddScoped<IAuthService,  AuthService>(); // Since AuthService has a scope dependency - DbContext
        return services;
    }
}