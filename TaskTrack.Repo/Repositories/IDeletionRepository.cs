using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaskTrack.Repo.Repositories;

public sealed record DeletionItem(long DeletionId, string EntityType, int EntityId, string EntityName,
    int DeletedById, DateTimeOffset DeletedAt, DateTimeOffset UndoUntil,
    DateTimeOffset? RestoredAt, int? RestoredById, bool CanRestore);
public sealed record DeletionPage(List<DeletionItem> Items, int Page, int PageSize, int TotalItems, DateTimeOffset ServerTime);
public enum RestoreDeletionResult { Restored, NotFound, Forbidden, Expired, AlreadyRestored, Conflict }

public interface IDeletionRepository
{
    Task<T> RecordDeleteAsync<T>(string resource, int id, int actorId, Func<Task<T>> action, Func<T,bool> succeeded);
    Task<DeletionPage> GetPageAsync(bool includeAccounts, string resource, string state, string search, int page, int pageSize);
    Task<RestoreDeletionResult> RestoreAsync(long id, int actorId, bool isAdmin);
}
