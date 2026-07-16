using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Home;

[AllowAnonymous]
public class FaqModel : PageModel
{
    public IReadOnlyList<FaqItem> Items { get; } = new[]
    {
        new FaqItem("Faq_Q1", "Faq_A1"),
        new FaqItem("Faq_Q2", "Faq_A2"),
        new FaqItem("Faq_Q3", "Faq_A3"),
        new FaqItem("Faq_Q4", "Faq_A4"),
        new FaqItem("Faq_Q5", "Faq_A5"),
        new FaqItem("Faq_Q6", "Faq_A6"),
        new FaqItem("Faq_Q7", "Faq_A7"),
        new FaqItem("Faq_Q8", "Faq_A8"),
        new FaqItem("Faq_Q9", "Faq_A9"),
        new FaqItem("Faq_Q10", "Faq_A10"),
    };

    public void OnGet() { }
}
