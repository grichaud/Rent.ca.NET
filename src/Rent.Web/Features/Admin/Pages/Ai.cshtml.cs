using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Admin.Pages;

[Authorize(Roles = Roles.Admin)]
public class AiModel : PageModel
{
    // Heuristic: 4 chars per token (close enough for English/French content for portfolio metrics).
    private const int CharsPerToken = 4;

    // Claude Haiku 4.5 input cost per million tokens (USD). Output cost differs but for a high-level
    // dashboard we approximate using a single rate. Documented in the dashboard copy.
    private const decimal CostPerMillionTokens = 800m / 1000m; // $0.0008 per 1K tokens => $0.80 per 1M tokens

    private readonly AppDbContext _db;

    public AiModel(AppDbContext db)
    {
        _db = db;
    }

    public int TotalConversations { get; private set; }
    public int TotalMessages { get; private set; }
    public long EstimatedTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public IReadOnlyList<ToolUsage> ToolBreakdown { get; private set; } = [];
    public IReadOnlyList<DailyBucket> Last7Days { get; private set; } = [];
    public IReadOnlyList<RecentConversation> Recent { get; private set; } = [];
    public string? UserEmailFilter { get; private set; }

    public async Task OnGetAsync(string? userEmail, CancellationToken ct)
    {
        UserEmailFilter = userEmail;

        TotalConversations = await _db.AiConversations.CountAsync(ct);
        TotalMessages = await _db.AiMessages.CountAsync(ct);

        // Estimate tokens by summing content lengths in memory. SQLite supports SUM(LEN()) but the
        // per-message overhead is negligible at portfolio scale (a few hundred messages tops).
        var lengths = await _db.AiMessages
            .AsNoTracking()
            .Select(m => m.Content.Length)
            .ToListAsync(ct);
        var totalChars = lengths.Sum(l => (long)l);
        EstimatedTokens = totalChars / CharsPerToken;
        EstimatedCostUsd = EstimatedTokens / 1_000_000m * CostPerMillionTokens;

        // Tool call breakdown — group by ToolName for messages whose Role is Tool.
        ToolBreakdown = await _db.AiMessages
            .AsNoTracking()
            .Where(m => m.Role == AiMessageRole.Tool && m.ToolName != null)
            .GroupBy(m => m.ToolName!)
            .Select(g => new ToolUsage { Name = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        ToolBreakdown = ToolBreakdown.OrderByDescending(t => t.Count).ToList();

        // 7-day chart — pull all messages from last 7 days then bucket in memory (SQLite-safe).
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var recentMsgs = await _db.AiMessages
            .AsNoTracking()
            .Select(m => new { m.CreatedAt })
            .ToListAsync(ct);
        var grouped = recentMsgs
            .Where(m => m.CreatedAt >= sevenDaysAgo)
            .GroupBy(m => m.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var buckets = new List<DailyBucket>();
        for (int i = 6; i >= 0; i--)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            buckets.Add(new DailyBucket
            {
                Date = date,
                Count = grouped.TryGetValue(date, out var c) ? c : 0
            });
        }
        Last7Days = buckets;

        // Recent conversations (top 25 by UpdatedAt). Pull rows then sort + join in memory
        // because SQLite (test fixture) cannot translate ORDER BY DateTimeOffset in subqueries.
        var allConvosQuery = _db.AiConversations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var f = userEmail.Trim();
            allConvosQuery = allConvosQuery.Where(c => c.User != null && c.User.Email != null && c.User.Email.Contains(f));
        }
        var convos = await allConvosQuery
            .Select(c => new
            {
                c.Id,
                c.UpdatedAt,
                c.Title,
                UserEmail = c.User != null ? c.User.Email : null,
                c.SessionId
            })
            .ToListAsync(ct);

        var topConvoIds = convos
            .OrderByDescending(c => c.UpdatedAt)
            .Take(25)
            .Select(c => c.Id)
            .ToHashSet();

        var msgRows = await _db.AiMessages
            .AsNoTracking()
            .Where(m => topConvoIds.Contains(m.ConversationId))
            .Select(m => new { m.ConversationId, m.Content, m.CreatedAt })
            .ToListAsync(ct);

        var msgsByConvo = msgRows
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Recent = convos
            .OrderByDescending(c => c.UpdatedAt)
            .Take(25)
            .Select(c =>
            {
                msgsByConvo.TryGetValue(c.Id, out var msgs);
                var lastMsg = msgs?.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
                return new RecentConversation
                {
                    Id = c.Id,
                    UpdatedAt = c.UpdatedAt,
                    Title = c.Title,
                    UserEmail = c.UserEmail,
                    SessionId = c.SessionId,
                    MessageCount = msgs?.Count ?? 0,
                    LastMessage = Truncate(lastMsg?.Content, 80)
                };
            })
            .ToList();
    }

    private static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    public class ToolUsage
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyBucket
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class RecentConversation
    {
        public Guid Id { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? Title { get; set; }
        public string? UserEmail { get; set; }
        public Guid? SessionId { get; set; }
        public int MessageCount { get; set; }
        public string? LastMessage { get; set; }
    }
}
