using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Repo.Repositories;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/deletions")]
[Authorize(Roles="0,1")]
public sealed class DeletionsController(IDeletionRepository history) : ControllerBase
{
    private static readonly HashSet<string> Resources=["departments","projects","tasks","tags","accounts"];
    [HttpGet]
    public async Task<ActionResult<DeletionPage>> Get([FromQuery]string resource="",[FromQuery]string state="all",
        [FromQuery,MaxLength(300)]string search="",[FromQuery,Range(1,10000)]int page=1,[FromQuery,Range(1,100)]int pageSize=20)
    {
        Response.Headers.CacheControl="no-store";
        if(resource!=""&&!Resources.Contains(resource))return BadRequest(new{message="Unknown resource."});
        if(state is not ("all" or "available" or "expired" or "restored"))return BadRequest(new{message="Unknown history state."});
        if(resource=="accounts"&&!User.IsInRole("1"))return Forbid();
        return Ok(await history.GetPageAsync(User.IsInRole("1"),resource,state,search,page,pageSize));
    }
    [HttpPost("{id:long}/restore")]
    public async Task<IActionResult> Restore(long id)
    {
        Response.Headers.CacheControl="no-store";
        if(!int.TryParse(User.FindFirst("AccountID")?.Value,out var actor)||actor<=0)return Unauthorized();
        return (await history.RestoreAsync(id,actor,User.IsInRole("1"))) switch
        {
            RestoreDeletionResult.Restored=>Ok(new{message="Restored successfully."}),
            RestoreDeletionResult.Forbidden=>Forbid(),
            RestoreDeletionResult.NotFound=>NotFound(),
            RestoreDeletionResult.Expired=>Conflict(new{message="The 24-hour undo period has expired."}),
            RestoreDeletionResult.AlreadyRestored=>Conflict(new{message="This deletion has already been restored."}),
            _=>Conflict(new{message="Restoration conflicts with a current record or missing parent. Restore required parents first and check duplicate names/emails."})
        };
    }
}
