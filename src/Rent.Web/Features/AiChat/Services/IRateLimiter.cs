namespace Rent.Web.Features.AiChat.Services;

public interface IRateLimiter
{
    bool TryAcquire(string key, int limit, TimeSpan window);
}
