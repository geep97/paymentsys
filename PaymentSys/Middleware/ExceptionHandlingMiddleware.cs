using Microsoft.AspNetCore.Mvc;
using PaymentSys.Services.Paystack;

namespace PaymentSys.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request was canceled by the client");
            }
            catch (KeyNotFoundException ex)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Resource not found", ex.Message);
            }
            catch (ArgumentException ex)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Invalid request", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Request could not be processed", ex.Message);
            }
            catch (PaystackException ex)
            {
                _logger.LogWarning(ex, "Paystack request failed");
                await WriteProblemAsync(context, StatusCodes.Status502BadGateway, "Payment provider error", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred",
                    "The request failed. Please try again later.");
            }
        }

        private static async Task WriteProblemAsync(
            HttpContext context,
            int statusCode,
            string title,
            string detail)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            });
        }
    }
}
