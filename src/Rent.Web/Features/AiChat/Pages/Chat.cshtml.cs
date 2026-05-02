using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Rent.Web.Domain;
using Rent.Web.Features.AiChat.Services;

namespace Rent.Web.Features.AiChat.Pages;

[AllowAnonymous]
public class ChatModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAiChatService _chat;
    private readonly IValidator<ChatRequest> _validator;
    private readonly IRateLimiter _rateLimiter;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AiOptions _options;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(
        IAiChatService chat,
        IValidator<ChatRequest> validator,
        IRateLimiter rateLimiter,
        UserManager<ApplicationUser> userManager,
        IOptions<AiOptions> options,
        ILogger<ChatModel> logger)
    {
        _chat = chat;
        _validator = validator;
        _rateLimiter = rateLimiter;
        _userManager = userManager;
        _options = options.Value;
        _logger = logger;
    }

    public IActionResult OnGet() => StatusCode(405);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ChatRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ChatRequest>(
                Request.Body, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in /api/ai/chat request");
            return BadRequest(new { error = "Invalid JSON body." });
        }

        if (request is null)
            return BadRequest(new { error = "Empty request body." });

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error = "Validation failed.",
                details = validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }

        var sessionId = AiSessionCookie.EnsureSessionId(HttpContext);
        Guid? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var raw = _userManager.GetUserId(User);
            if (Guid.TryParse(raw, out var uid)) userId = uid;
        }

        var rateKey = userId is { } u ? $"user:{u}" : $"sess:{sessionId}";
        if (!_rateLimiter.TryAcquire(rateKey, _options.RateLimitPerHour, TimeSpan.FromHours(1)))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return new JsonResult(new
            {
                error = "Slow down — too many requests. Try again in a few minutes."
            });
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache, no-transform");
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            var result = await _chat.ProcessAsync(
                request, userId, sessionId,
                emitChunkAsync: async (chunk, innerCt) =>
                {
                    await WriteEventAsync("message", new { content = chunk }, innerCt);
                },
                ct);

            await WriteEventAsync("done", new { conversationId = result.ConversationId }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat stream canceled by client.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat processing failed for user {UserId}", userId);
            try
            {
                await WriteEventAsync("error", new
                {
                    message = "Sorry, I couldn't reach the assistant. Please try again."
                }, ct);
            }
            catch { /* response may already be closed */ }
        }

        return new EmptyResult();
    }

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var line = $"event: {eventName}\ndata: {json}\n\n";
        await Response.WriteAsync(line, ct);
        await Response.Body.FlushAsync(ct);
    }
}
