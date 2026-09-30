using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ArmaFit_API.Models;

public record Link(string Href, string Method);

public static class LinkExtensions
{
    /// <summary>Link to another action, e.g. <c>this.Link("Get", "Plans", new { id = 1 })</c>.</summary>
    public static Link Link(this ControllerBase c, string action, string controller, object values, string method = "GET") =>
        new(c.Url.Action(action, controller, values)!, method);

    /// <summary>self/first/last/prev/next links for a list, keeping the current query string (filters).</summary>
    public static Dictionary<string, Link> PageLinks(this ControllerBase c, int page, int totalPages)
    {
        Link To(int p)
        {
            var query = c.Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            query["page"] = p.ToString();
            return new(QueryHelpers.AddQueryString(c.Request.PathBase + c.Request.Path, query), "GET");
        }

        var last = Math.Max(totalPages, 1);
        var links = new Dictionary<string, Link> { ["self"] = To(page), ["first"] = To(1), ["last"] = To(last) };
        if (page > 1) links["prev"] = To(page - 1);
        if (page < last) links["next"] = To(page + 1);
        return links;
    }
}
