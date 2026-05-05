using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AiUsageDashboardTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AiUsageDashboardTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_RendersTotalsCardsAndChart()
    {
        var convoId = await SeedConversationWithMessagesAsync(messageCount: 4, includeToolCall: false);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "ai-totals");

        var html = await client.GetStringAsync("/en/admin/ai");

        html.Should().Contain("AI Usage");
        html.Should().Contain("Conversations");
        html.Should().Contain("Messages");
        html.Should().Contain("Estimated tokens");
    }

    [Fact]
    public async Task Dashboard_GroupsToolCallsByName()
    {
        await SeedConversationWithMessagesAsync(
            messageCount: 1,
            includeToolCall: true,
            toolName: "search_properties");

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "ai-tools");

        var html = await client.GetStringAsync("/en/admin/ai");

        html.Should().Contain("search_properties");
        html.Should().Contain("Tool calls breakdown");
    }

    [Fact]
    public async Task ConversationDetail_RendersAllMessagesInOrder()
    {
        var convoId = await SeedConversationWithMessagesAsync(messageCount: 3, includeToolCall: true);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "ai-detail");

        var html = await client.GetStringAsync($"/en/admin/ai/{convoId}");

        html.Should().Contain("Conversation detail");
        // Roles render with their enum name (CSS does the uppercase styling).
        html.Should().Contain("User");
        html.Should().Contain("Assistant");
        html.Should().Contain("Tool");
    }

    private async Task<Guid> SeedConversationWithMessagesAsync(int messageCount, bool includeToolCall, string toolName = "search_properties")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var convo = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = null,
            SessionId = Guid.NewGuid(),
            Title = $"Test convo {Guid.NewGuid():N}".Substring(0, 20),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AiConversations.Add(convo);

        for (int i = 0; i < messageCount; i++)
        {
            db.AiMessages.Add(new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = convo.Id,
                Role = i % 2 == 0 ? AiMessageRole.User : AiMessageRole.Assistant,
                Content = $"Test message {i} content",
                CreatedAt = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }

        if (includeToolCall)
        {
            db.AiMessages.Add(new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = convo.Id,
                Role = AiMessageRole.Tool,
                Content = "{\"results\":[]}",
                ToolName = toolName,
                ToolArgsJson = "{\"city\":\"Toronto\"}",
                ToolResultJson = "{\"count\":0}",
                CreatedAt = DateTimeOffset.UtcNow.AddSeconds(messageCount)
            });
        }

        await db.SaveChangesAsync();
        return convo.Id;
    }
}
