using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using TaskTrack.Repo.Data;

namespace TaskTrack.Repo.Repositories;

public sealed class DeletionRepository(TaskTrackDbContext context) : IDeletionRepository
{
    private static readonly Dictionary<string,(string Table,string Id,string Label)> Tables = new()
    {
        ["departments"] = ("Department","DepartmentID","DepartmentName"),
        ["projects"] = ("Project","ProjectID","ProjectName"),
        ["tasks"] = ("Task","TaskID","Title"),
        ["tags"] = ("Tag","TagID","TagName"),
        ["accounts"] = ("SystemAccount","AccountID","FullName")
    };
    private NpgsqlCommand Command(string sql) => new(sql,
        (NpgsqlConnection)context.Database.GetDbConnection(),
        (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());

    public async Task<T> RecordDeleteAsync<T>(string resource, int id, int actorId,
        Func<Task<T>> action, Func<T,bool> succeeded)
    {
        var table=Tables[resource];
        await using var transaction=await context.Database.BeginTransactionAsync();
        string? snapshot;
        // Only the fixed allowlist supplies SQL identifiers; values are parameters.
        await using(var read=Command($"SELECT to_jsonb(r)::text FROM public.\"{table.Table}\" r WHERE r.\"{table.Id}\"=@id FOR UPDATE"))
        {
            read.Parameters.AddWithValue("id",id);
            snapshot=await read.ExecuteScalarAsync() as string;
        }
        var result=await action();
        if(succeeded(result) && snapshot is not null)
        {
            using var json=JsonDocument.Parse(snapshot);
            await using var insert=Command("""
                INSERT INTO public."DeletionHistory"
                ("EntityType","EntityID","EntityName","DeletedByID","DeletedAt","UndoUntil","Snapshot")
                SELECT @type,@id,@name,@actor,t.at,t.at+INTERVAL '24 hours',@snapshot
                FROM (SELECT clock_timestamp() AS at) t
                """);
            insert.Parameters.AddWithValue("type",resource);
            insert.Parameters.AddWithValue("id",id);
            insert.Parameters.AddWithValue("name",json.RootElement.GetProperty(table.Label).GetString()!);
            insert.Parameters.AddWithValue("actor",actorId);
            insert.Parameters.AddWithValue("snapshot",NpgsqlDbType.Jsonb,snapshot);
            await insert.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        else await transaction.RollbackAsync();
        return result;
    }

    public async Task<DeletionPage> GetPageAsync(bool includeAccounts,string resource,string state,string search,int page,int pageSize)
    {
        const string where="""
            WHERE (@admin OR "EntityType"<>'accounts') AND (@type='' OR "EntityType"=@type)
            AND "EntityName" ILIKE '%' || @search || '%'
            AND (@state='all' OR (@state='restored' AND "RestoredAt" IS NOT NULL)
                OR (@state='available' AND "RestoredAt" IS NULL AND "UndoUntil">clock_timestamp())
                OR (@state='expired' AND "RestoredAt" IS NULL AND "UndoUntil"<=clock_timestamp()))
            """;
        await context.Database.OpenConnectionAsync();
        try
        {
            void Parameters(NpgsqlCommand c)
            {
                c.Parameters.AddWithValue("admin",includeAccounts);c.Parameters.AddWithValue("type",resource);
                c.Parameters.AddWithValue("state",state);c.Parameters.AddWithValue("search",search);
            }
            await using var count=Command("SELECT count(*) FROM public.\"DeletionHistory\" "+where);
            Parameters(count);var total=checked((int)(long)(await count.ExecuteScalarAsync())!);
            // Snapshot is deliberately absent from every public projection, including account hashes.
            await using var list=Command("""
                SELECT "DeletionID","EntityType","EntityID","EntityName","DeletedByID","DeletedAt","UndoUntil",
                    "RestoredAt","RestoredByID",("RestoredAt" IS NULL AND "UndoUntil">clock_timestamp())
                FROM public."DeletionHistory"
                """+where+" ORDER BY \"DeletedAt\" DESC,\"DeletionID\" DESC LIMIT @limit OFFSET @offset");
            Parameters(list);list.Parameters.AddWithValue("limit",pageSize);list.Parameters.AddWithValue("offset",(page-1)*pageSize);
            var items=new List<DeletionItem>();
            await using var reader=await list.ExecuteReaderAsync();
            while(await reader.ReadAsync())items.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetInt32(2),reader.GetString(3),reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5),reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7)?null:reader.GetFieldValue<DateTimeOffset>(7),reader.IsDBNull(8)?null:reader.GetInt32(8),reader.GetBoolean(9)));
            return new(items,page,pageSize,total,DateTimeOffset.UtcNow);
        }
        finally{await context.Database.CloseConnectionAsync();}
    }

    public async Task<RestoreDeletionResult> RestoreAsync(long id,int actorId,bool isAdmin)
    {
        await using var transaction=await context.Database.BeginTransactionAsync();
        string resource,snapshot;int entityId;DateTimeOffset deadline;bool restored;
        await using(var read=Command("SELECT \"EntityType\",\"EntityID\",\"Snapshot\"::text,\"UndoUntil\",\"RestoredAt\" IS NOT NULL FROM public.\"DeletionHistory\" WHERE \"DeletionID\"=@id FOR UPDATE"))
        {
            read.Parameters.AddWithValue("id",id);await using var reader=await read.ExecuteReaderAsync();
            if(!await reader.ReadAsync())return RestoreDeletionResult.NotFound;
            resource=reader.GetString(0);entityId=reader.GetInt32(1);snapshot=reader.GetString(2);
            deadline=reader.GetFieldValue<DateTimeOffset>(3);restored=reader.GetBoolean(4);
        }
        if(resource=="accounts"&&!isAdmin)return RestoreDeletionResult.Forbidden;
        if(restored)return RestoreDeletionResult.AlreadyRestored;
        if(deadline<=DateTimeOffset.UtcNow)return RestoreDeletionResult.Expired;
        var table=Tables[resource];using var json=JsonDocument.Parse(snapshot);
        try
        {
            if(resource is "projects" or "tasks")
            {
                var parent=resource=="projects"?(Table:"Department",Id:"DepartmentID"):(Table:"Project",Id:"ProjectID");
                await using var exists=Command($"SELECT 1 FROM public.\"{parent.Table}\" WHERE \"{parent.Id}\"=@id AND \"IsActive\" FOR SHARE");
                exists.Parameters.AddWithValue("id",json.RootElement.GetProperty(parent.Id).GetInt32());
                if(await exists.ExecuteScalarAsync() is null)return RestoreDeletionResult.Conflict;
            }
            if(resource=="tasks")
            {
                await using var update=Command("UPDATE public.\"Task\" SET \"IsActive\"=TRUE,\"UpdatedByID\"=@actor,\"ModifiedDate\"=clock_timestamp() AT TIME ZONE 'UTC' WHERE \"TaskID\"=@id AND NOT \"IsActive\"");
                update.Parameters.AddWithValue("id",entityId);update.Parameters.AddWithValue("actor",actorId);
                if(await update.ExecuteNonQueryAsync()!=1)return RestoreDeletionResult.Conflict;
            }
            else
            {
                await using var insert=Command($"INSERT INTO public.\"{table.Table}\" OVERRIDING SYSTEM VALUE SELECT (jsonb_populate_record(NULL::public.\"{table.Table}\",@snapshot)).*");
                insert.Parameters.AddWithValue("snapshot",NpgsqlDbType.Jsonb,snapshot);
                await insert.ExecuteNonQueryAsync();
            }
            // Recheck real DB time after any lock wait. A transaction started before expiry
            // cannot commit an undo after its 24-hour deadline.
            await using var mark=Command("UPDATE public.\"DeletionHistory\" SET \"RestoredAt\"=clock_timestamp(),\"RestoredByID\"=@actor,\"Snapshot\"='{}'::jsonb WHERE \"DeletionID\"=@id AND \"RestoredAt\" IS NULL AND \"UndoUntil\">clock_timestamp()");
            mark.Parameters.AddWithValue("id",id);mark.Parameters.AddWithValue("actor",actorId);
            if(await mark.ExecuteNonQueryAsync()!=1){await transaction.RollbackAsync();return RestoreDeletionResult.Expired;}
            await transaction.CommitAsync();return RestoreDeletionResult.Restored;
        }
        catch(PostgresException e) when(e.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            await transaction.RollbackAsync();return RestoreDeletionResult.Conflict;
        }
    }
}
