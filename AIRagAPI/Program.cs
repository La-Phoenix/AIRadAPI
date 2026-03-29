using AIRagAPI.Domain.Persistence;
using AIRagAPI.Exceptions;
using AIRagAPI.Extensions;
using AIRagAPI.Generators;
using AIRagAPI.Services.Vector;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//For only production
// if (!builder.Environment.IsDevelopment())
//     builder.WebHost.UseUrls("http://+:8080");

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

// Configure DBContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Configure Auth
// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Google";
})
.AddCookie("Cookies", options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Cookie.Name = "littlephoenix_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // var isDev = builder.Environment.IsDevelopment();
    // options.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
    // options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
})
.AddGoogle("Google", options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    options.CallbackPath = "/api/auth/google/callback";
    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey("picture", "picture", "url");

    // var isDev = builder.Environment.IsDevelopment();
    // options.CorrelationCookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
    // options.CorrelationCookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
});

//Register Application Services
builder.Services.AddApplicationServices();

builder.Services.AddApplicationServices();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

//Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        
    } catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}
app.Run();