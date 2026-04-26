using AIRagAPI.Domain.Persistence;
using AIRagAPI.Exceptions;
using AIRagAPI.Extensions;
using AIRagAPI.Generators;
using AIRagAPI.Services.Auth;
using AIRagAPI.Services.ChatService;
using System.Security.Claims;
using AIRagAPI.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//For only production
// if (!builder.Environment.IsDevelopment())
//     builder.WebHost.UseUrls("http://+:8080");

builder.Services.AddOpenApi();

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;
});

builder.Services.Configure<ChatSettings>(builder.Configuration.GetSection("ChatSettings"));

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


// Configure header for Render deployment
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure data protection
var dpBuilder = builder.Services.AddDataProtection().SetApplicationName("LittlePhoenix");

if (builder.Environment.IsDevelopment())
{
    // Local dev: use file system to avoid DB init issues
    var keysDir = Path.Combine(Directory.GetCurrentDirectory(), "keys");
    Directory.CreateDirectory(keysDir);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDir))
        .SetDefaultKeyLifetime(TimeSpan.FromDays(30));
}
else
{
    // Production: store encrypted keys in database
    dpBuilder.PersistKeysToDbContext<AppDbContext>()
        .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
}

// Configure Auth
builder.Services.AddAuthentication(options =>
    {
        // Check app cookie to know if user is authenticated
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // Store authenticated user in cookie after external login
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // Redirect to Google for authentication if not authenticated
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Cookie.Name = "littlephoenix_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;

        // Using None to allow cross-origin requests 
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"] ?? throw new InvalidOperationException("Google:ClientId not configured");
        options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? throw new InvalidOperationException("Google:ClientSecret not configured");
        
        // Set explicit callback path
        options.CallbackPath = new PathString("/api/auth/google/callback");
        
        // Store authenticated user in app's cookie (DefaultSignInScheme)
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        
        // Capture profile picture
        options.ClaimActions.MapJsonKey("picture", "picture", "url");
        options.SaveTokens = true;
        
        // Using None for cross-origin OAuth flow
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        
        // Log OAuth redirects for debugging
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("🔐 Redirecting to Google OAuth - State will be stored in correlation cookie");
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        
        // Handle OAuth failures
        options.Events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("❌ Google OAuth failed: {Error}", context.Failure?.Message);
            
            // Log available cookies
            var cookies = context.Request.Cookies.Keys.ToList();
            // logger.LogError("📍 Available cookies: {Cookies}", string.Join(", ", cookies));
            
            context.HandleResponse();
            var frontendUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            context.Response.Redirect($"{frontendUrl}?error=oauth_failed");
            return Task.CompletedTask;
        };
        
        // Create/validate user in database after successful authentication - Also oauth take claims principal and serializes into cookie
        options.Events.OnTicketReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            try
            {
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                var name = context.Principal?.FindFirstValue(ClaimTypes.Name);
                var picture = context.Principal?.FindFirstValue("picture");
                
                if (string.IsNullOrEmpty(email))
                {
                    logger.LogError("Email claim missing from Google response");
                    context.HandleResponse();
                    var frontendUrl = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Frontend:BaseUrl"] ?? "http://localhost:5173";
                    context.Response.Redirect($"{frontendUrl}?error=missing_email");
                    return Task.CompletedTask;
                }
                
                logger.LogInformation("Creating or validating user: {Email}", email);
                
                // Create/validate user in database
                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                var claimsIdentity = authService.ValidateUserAsync(email, name, picture, CancellationToken.None).GetAwaiter().GetResult();
                
                context.Principal = claimsIdentity;
                logger.LogInformation("✅ User validated/created: {Email}", email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating user during OAuth");
                context.HandleResponse();
                var frontendUrl = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Frontend:BaseUrl"] ?? "http://localhost:5173";
                context.Response.Redirect($"{frontendUrl}?error=server_error");
            }
            
            return Task.CompletedTask;
        };
    });

//Register Application Services
builder.Services.AddApplicationServices();

// Register Real-time Chat Services
builder.Services.AddScoped<IChatManagementService, ChatManagementService>();
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();
builder.Services.AddSingleton<ChatPresenceTracker>();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

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

// Should be first middelware
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Scheme: {Scheme}, RemoteIp: {Ip}",
        context.Request.Scheme,
        context.Connection.RemoteIpAddress);
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        await next();
    });

    app.UseHttpsRedirection();
}

// if (!app.Environment.IsEnvironment("Docker"))
// {
//     app.UseHttpsRedirection();
// }
// app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);

    if (context.Request.Headers.TryGetValue("Cookie", out var cookie))
        logger.LogInformation("Cookies: Cookie available");
        // logger.LogInformation("Cookies: {Cookies}", cookie.ToString());
    else
        logger.LogInformation("No cookies sent");

    await next();
});

// Enable WebSocket support
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/api/chat-hub");
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