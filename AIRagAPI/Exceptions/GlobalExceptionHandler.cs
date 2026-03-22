using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AIRagAPI.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;  // optional but recommended for ProblemDetails

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log the exception (always – even in production)
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        // Decide response based on exception type (extend as needed)
        var statusCode = StatusCodes.Status500InternalServerError;
        var detail = "An unexpected error occurred.";
        var title = "Server Error";

        if (exception is ArgumentException or ArgumentNullException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            title = "Bad Request";
            detail = exception.Message;  // safe to expose for argument errors
        }
        else if (exception is UnauthorizedAccessException)
        {
            statusCode = StatusCodes.Status401Unauthorized;
            title = "Unauthorized";
            detail = "Authentication required.";
        }
        // Add more specific cases (e.g., ValidationException, NotFoundException, etc.)

        // In production: never expose stack trace or inner details
        if (!httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            detail = "An error occurred while processing your request.";
        }

        // Build ProblemDetails
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = $"https://tools.ietf.org/html/rfc7231#section-{statusCode}"  // or custom URI
        };

        // Add extensions if useful (e.g., trace ID, correlation ID)
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Write the response using ProblemDetailsService (handles content negotiation)
        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new()
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception  // optional
        });
    }
}