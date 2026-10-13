using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskTrack.Repo.Repositories;

namespace TaskTrack.API.Filters;

public sealed class DeletionHistoryFilter(IDeletionRepository history) : IAsyncActionFilter
{
    private static readonly HashSet<string> Resources = ["departments","projects","tasks","tags","accounts"];
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var resource=context.RouteData.Values["controller"]?.ToString()?.ToLowerInvariant();
        if(context.HttpContext.Request.Method!="DELETE" || resource is null || !Resources.Contains(resource)
            || !context.ActionArguments.TryGetValue("id",out var rawId) || rawId is not int id || id<=0
            || !int.TryParse(context.HttpContext.User.FindFirst("AccountID")?.Value,out var actorId))
        {await next();return;}
        await history.RecordDeleteAsync(resource,id,actorId,()=>next(),
            result=>result.Exception is null && result.Result is NoContentResult);
    }
}
