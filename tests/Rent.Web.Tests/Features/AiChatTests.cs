using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AiChatTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AiChatTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Chat_ValidatesMessageNotEmpty_And_MaxLength()
    {
        _factory.OpenRouter.Reset();
        using var client = CreateClient();

        var emptyResp = await client.PostAsync("/api/ai/chat", JsonBody(new { message = "" }));
        emptyResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var longMsg = new string('x', 2001);
        var longResp = await client.PostAsync("/api/ai/chat", JsonBody(new { message = longMsg }));
        longResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_ReturnsAssistantResponse_AfterToolCall()
    {
        _factory.OpenRouter.Reset();
        _factory.OpenRouter.EnqueueToolCall(
            "get_city_info",
            JsonSerializer.Serialize(new { city = "Toronto" }));
        _factory.OpenRouter.EnqueueText("Toronto is in Ontario and we have listings there.");

        using var client = CreateClient();
        var resp = await client.PostAsync("/api/ai/chat",
            JsonBody(new { message = "tell me about toronto" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("event: message");
        body.Should().Contain("Toronto is in Ontario");
        body.Should().Contain("event: done");

        _factory.OpenRouter.Calls.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Chat_PersistsUserAndAssistantMessages()
    {
        _factory.OpenRouter.Reset();
        _factory.OpenRouter.EnqueueText("Sure, here's what I found.");

        using var client = CreateClient();
        var resp = await client.PostAsync("/api/ai/chat",
            JsonBody(new { message = "hello there" }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        await resp.Content.ReadAsStringAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var convos = await db.AiConversations.AsNoTracking()
            .Where(c => c.Title == "hello there")
            .ToListAsync();
        var convo = convos.OrderByDescending(c => c.CreatedAt).First();

        var rawMessages = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == convo.Id)
            .ToListAsync();
        var messages = rawMessages.OrderBy(m => m.CreatedAt).ToList();
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(AiMessageRole.User);
        messages[0].Content.Should().Be("hello there");
        messages[1].Role.Should().Be(AiMessageRole.Assistant);
        messages[1].Content.Should().Be("Sure, here's what I found.");
    }

    [Fact]
    public async Task Chat_RateLimits_After20RequestsPerHour()
    {
        _factory.OpenRouter.Reset();
        for (int i = 0; i < 25; i++)
            _factory.OpenRouter.EnqueueText($"reply {i}");

        using var client = CreateClient();

        HttpResponseMessage? last = null;
        for (int i = 0; i < 21; i++)
        {
            last = await client.PostAsync("/api/ai/chat",
                JsonBody(new { message = $"hi {i}" }));
            if (last.StatusCode == HttpStatusCode.TooManyRequests) break;
            await last.Content.ReadAsStringAsync();
        }

        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await last.Content.ReadAsStringAsync();
        body.Should().Contain("Slow down");
    }

    [Fact]
    public async Task Conversations_Active_ReturnsLastConversationLessThan24h()
    {
        _factory.OpenRouter.Reset();
        _factory.OpenRouter.EnqueueText("got it");

        using var client = CreateClient();
        var post = await client.PostAsync("/api/ai/chat",
            JsonBody(new { message = "remember me" }));
        post.StatusCode.Should().Be(HttpStatusCode.OK);
        await post.Content.ReadAsStringAsync();

        var get = await client.GetAsync("/api/ai/conversations/active");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await get.Content.ReadAsStringAsync();
        json.Should().Contain("remember me");
        json.Should().Contain("got it");
    }

    [Fact]
    public async Task Chat_HandlesOpenRouterError_GracefullyWithSseError()
    {
        _factory.OpenRouter.Reset();
        _factory.OpenRouter.EnqueueFailure(new HttpRequestException("simulated"));

        using var client = CreateClient();
        var resp = await client.PostAsync("/api/ai/chat",
            JsonBody(new { message = "this will fail" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("event: error");
        body.Should().Contain("Sorry");
    }

    [Fact]
    public async Task Chat_AuthenticatedUser_PersistsConversationWithUserId()
    {
        _factory.OpenRouter.Reset();
        _factory.OpenRouter.EnqueueText("welcome back");

        var email = $"ai-chat-user+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        var resp = await client.PostAsync("/api/ai/chat",
            JsonBody(new { message = "hi as logged in user" }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        await resp.Content.ReadAsStringAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var convos = await db.AiConversations.AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .ToListAsync();
        var convo = convos.OrderByDescending(c => c.CreatedAt).First();
        convo.UserId.Should().Be(user.Id);
        convo.SessionId.Should().BeNull();
    }
}
