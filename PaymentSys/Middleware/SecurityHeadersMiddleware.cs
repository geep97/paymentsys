namespace PaymentSys.Middleware
{
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";

                if (context.Request.Path.StartsWithSegments("/api")
                    || context.Request.Path.StartsWithSegments("/health"))
                {
                    headers.CacheControl = "no-store";
                    headers.Pragma = "no-cache";
                    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                }

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
