using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Data;
using TaskTrack.Repo.Repositories;
using SystemAccount = TaskTrack.Repo.Models.SystemAccount;

var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("TaskTrack.API/appsettings.json")
    .AddUserSecrets("bd82d864-be53-4110-b4f1-6e466bc1c045")
    .AddEnvironmentVariables().Build();
var uri = new Uri(config["DATABASE_URL"] ?? throw new Exception("Configure DATABASE_URL."));
var credentials = uri.UserInfo.Split(':', 2);
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
    Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
    Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
    SslMode = SslMode.Require
}.ConnectionString;
await using var db = new NpgsqlConnection(connectionString);
await db.OpenAsync();
using var http = new HttpClient { BaseAddress = new Uri(config["TEST_API_URL"] ?? "http://127.0.0.1:5127"), Timeout = TimeSpan.FromSeconds(60) };
var prefix = "as2check" + Guid.NewGuid().ToString("N");
var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
var checks = 0;
var staffId = 0;
var adminId = 0;
var taskId = 0;
var baseline = await Fingerprint();

void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    checks++;
}
int Id(JsonNode? node, string key) => node![key]!.GetValue<int>();
string Text(JsonNode? node, string key) => node![key]!.GetValue<string>();
async Task<JsonNode?> Send(string method, string path, int expected, object? body = null, string? token = null)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (body is not null) request.Content = JsonContent.Create(body);
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    using var response = await http.SendAsync(request);
    Check((int)response.StatusCode == expected, $"{method} {path}: expected {expected}, got {(int)response.StatusCode}.");
    if (response.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        return JsonNode.Parse(await response.Content.ReadAsStringAsync());
    return null;
}
async Task<string> Login(string email) => Text(await Send("POST", "/api/auth/login", 200, new { email, password }), "accessToken");
async Task<object?> Sql(string sql, params (string name, object value)[] parameters)
{
    await using var command = new NpgsqlCommand(sql, db);
    foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
    return await command.ExecuteScalarAsync();
}
async Task<string> Fingerprint()
{
    var hashes = new List<string>();
    foreach (var (table, orderBy) in new[]
    {
        ("Department", "\"DepartmentID\""), ("Project", "\"ProjectID\""),
        ("Task", "\"TaskID\""), ("Tag", "\"TagID\""), ("TaskTag", "\"TaskID\", \"TagID\"")
    })
        hashes.Add((string)(await Sql($"SELECT md5(COALESCE(jsonb_agg(r)::text, '[]')) FROM (SELECT * FROM public.\"{table}\" ORDER BY {orderBy}) r"))!);
    return string.Join("/", hashes);
}

try
{
    foreach (var resource in new[] { "departments", "projects", "tasks", "tags" })
    {
        await Send("GET", $"/api/{resource}", 200);
        await Send("POST", $"/api/{resource}", 401, new { });
        await Send("PUT", $"/api/{resource}/0", 401, new { });
        await Send("DELETE", $"/api/{resource}/0", 401);
    }
    foreach (var (method, path) in new[] { ("GET", ""), ("GET", "/0"), ("PUT", "/0"), ("DELETE", "/0") })
        await Send(method, "/api/accounts" + path, 401, method == "PUT" ? new { } : null);
    await Send("GET", "/api/auth/me", 401);
    await Send("PUT", "/api/auth/profile", 401, new { fullName = prefix });
    await Send("PUT", "/api/auth/password", 401, new { currentPassword = password, newPassword = password + "new" });
    await Send("POST", "/api/auth/register", 400, new { });
    var staff = await Send("POST", "/api/auth/register", 201, new { fullName = prefix, email = prefix + "@example.test", password, role = 1 });
    staffId = Id(staff, "accountId");
    Check(Id(staff, "role") == 0 && staff!["passwordHash"] is null, "Registration must create Staff without exposing a hash.");
    await Send("POST", "/api/auth/register", 409, new { fullName = prefix, email = prefix.ToUpperInvariant() + "@EXAMPLE.TEST", password });
    await Send("POST", "/api/auth/login", 401, new { email = prefix + "@example.test", password = "wrong-password" });
    var staffToken = await Login(prefix + "@example.test");
    foreach (var (method, path) in new[] { ("GET", ""), ("GET", "/0"), ("PUT", "/0"), ("DELETE", "/0") })
        await Send(method, "/api/accounts" + path, 403, method == "PUT" ? new { } : null, staffToken);
    await Send("GET", "/api/auth/me", 200, token: staffToken);

    var admin = new SystemAccount { FullName = prefix + " Admin", Email = prefix + "-admin@example.test", Role = 1 };
    var hasher = new PasswordHasher<SystemAccount>(Options.Create(new PasswordHasherOptions { IterationCount = 220_000 }));
    var hash = hasher.HashPassword(admin, password);
    adminId = Convert.ToInt32(await Sql("INSERT INTO public.\"SystemAccount\" (\"FullName\", \"Email\", \"PasswordHash\", \"Role\") VALUES (@name,@email,@hash,1) RETURNING \"AccountID\"",
        ("name", admin.FullName), ("email", admin.Email), ("hash", hash)));
    var adminToken = await Login(admin.Email);
    var adminTag = Id(await Send("POST", "/api/tags", 201, new { tagName = prefix + " Admin", color = "#123456" }, adminToken), "tagId");
    await Send("PUT", $"/api/tags/{adminTag}", 204, new { tagName = prefix + " Admin U", color = "#654321" }, adminToken);
    await Send("DELETE", $"/api/tags/{adminTag}", 204, token: adminToken);
    var accounts = await Send("GET", "/api/accounts", 200, token: adminToken);
    Check(!accounts!.ToJsonString().Contains("passwordHash", StringComparison.OrdinalIgnoreCase), "Account list must not expose hashes.");
    await Send("GET", $"/api/accounts/{staffId}", 200, token: adminToken);
    await Send("GET", "/api/accounts/0", 404, token: adminToken);
    await Send("PUT", "/api/accounts/0", 404, new { role = 0 }, adminToken);
    await Send("DELETE", "/api/accounts/0", 404, token: adminToken);
    await Send("PUT", $"/api/accounts/{staffId}", 400, new { }, adminToken);
    await Send("PUT", $"/api/accounts/{staffId}", 400, new { role = 2 }, adminToken);
    await Send("PUT", $"/api/accounts/{staffId}", 400, new { fullName = " " }, adminToken);
    await Send("PUT", $"/api/accounts/{staffId}", 204, new { fullName = prefix + " Updated" }, adminToken);
    Check(Text(await Send("GET", "/api/auth/me", 200, token: staffToken), "fullName") == prefix + " Updated", "Name update must persist without invalidating an unchanged role.");
    await Send("PUT", $"/api/accounts/{staffId}", 204, new { role = 1 }, adminToken);
    await Send("GET", "/api/auth/me", 401, token: staffToken);
    var elevatedToken = await Login(prefix + "@example.test");
    await Send("GET", "/api/accounts", 200, token: elevatedToken);
    await Send("PUT", $"/api/accounts/{staffId}", 204, new { role = 0 }, adminToken);
    await Send("GET", "/api/accounts", 401, token: elevatedToken);
    staffToken = await Login(prefix + "@example.test");

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT_SECRET"]!));
    string Signed(IEnumerable<Claim> claims, bool expired = false, bool wrongKey = false) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        config["Jwt:Issuer"], config["Jwt:Audience"], claims, DateTime.UtcNow.AddMinutes(-2),
        expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(2),
        new SigningCredentials(wrongKey ? new SymmetricSecurityKey(new byte[32]) : key, SecurityAlgorithms.HmacSha256)));
    var session = new JwtSecurityTokenHandler().ReadJwtToken(staffToken).Claims.First(c => c.Type == "SessionID").Value;
    var claims = new[] { new Claim("AccountID", staffId.ToString()), new Claim("Role", "0"), new Claim("SessionID", session) };
    await Send("POST", "/api/tags", 401, new { }, Signed(claims, expired: true));
    await Send("POST", "/api/tags", 401, new { }, Signed(claims, wrongKey: true));
    await Send("POST", "/api/tags", 401, new { }, Signed(claims.Where(c => c.Type != "AccountID")));
    await Send("POST", "/api/tags", 401, new { }, Signed(claims.Select(c => c.Type == "SessionID" ? new Claim("SessionID", Guid.NewGuid().ToString()) : c)));

    await Send("POST", "/api/auth/refresh", 400, new { });
    await Send("POST", "/api/auth/logout", 400, new { });
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = "not-a-token" });
    await Send("POST", "/api/auth/logout", 204, new { refreshToken = "not-a-token" });
    var sessionLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    var originalRefresh = Text(sessionLogin, "refreshToken");
    var originalAccess = Text(sessionLogin, "accessToken");
    string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    Check(Convert.ToInt32(await Sql("SELECT COUNT(*) FROM public.\"RefreshToken\" WHERE \"TokenHash\"=@hash AND \"AccountID\"=@id", ("hash", Hash(originalRefresh)), ("id", staffId))) == 1,
        "Only the refresh token hash must be stored.");
    var rotated = await Send("POST", "/api/auth/refresh", 200, new { refreshToken = originalRefresh });
    Check(Text(rotated, "refreshToken") != originalRefresh, "Refresh must rotate the raw token.");
    Check(Math.Abs((DateTime.Parse(Text(rotated, "refreshTokenExpiresAt")) - DateTime.Parse(Text(sessionLogin, "refreshTokenExpiresAt"))).Ticks) <= 10,
        "Rotation must preserve the absolute session expiry, allowing PostgreSQL microsecond precision.");
    await Send("GET", "/api/auth/me", 200, token: Text(rotated, "accessToken"));
    await Send("GET", "/api/auth/me", 200, token: originalAccess);
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = originalRefresh });
    await Send("GET", "/api/auth/me", 401, token: Text(rotated, "accessToken"));
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = Text(rotated, "refreshToken") });

    sessionLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    var concurrentToken = Text(sessionLogin, "refreshToken");
    var concurrentResponses = await Task.WhenAll(
        http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = concurrentToken }),
        http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = concurrentToken }));
    Check(concurrentResponses.Count(r => (int)r.StatusCode == 200) == 1 && concurrentResponses.Count(r => (int)r.StatusCode == 401) == 1,
        "Concurrent refresh must have exactly one winner and one replay rejection.");
    var winner = JsonNode.Parse(await concurrentResponses.Single(r => (int)r.StatusCode == 200).Content.ReadAsStringAsync());
    foreach (var response in concurrentResponses) response.Dispose();
    await Send("GET", "/api/auth/me", 401, token: Text(winner, "accessToken"));

    sessionLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    await Send("POST", "/api/auth/logout", 204, new { refreshToken = Text(sessionLogin, "refreshToken") });
    await Send("POST", "/api/auth/logout", 204, new { refreshToken = Text(sessionLogin, "refreshToken") });
    await Send("GET", "/api/auth/me", 401, token: Text(sessionLogin, "accessToken"));
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = Text(sessionLogin, "refreshToken") });

    sessionLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    await Sql("UPDATE public.\"RefreshToken\" SET \"CreatedDate\"=CURRENT_TIMESTAMP - INTERVAL '2 hours', \"ExpiresDate\"=CURRENT_TIMESTAMP - INTERVAL '1 hour' WHERE \"TokenHash\"=@hash AND \"AccountID\"=@id",
        ("hash", Hash(Text(sessionLogin, "refreshToken"))), ("id", staffId));
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = Text(sessionLogin, "refreshToken") });
    await Send("GET", "/api/auth/me", 401, token: Text(sessionLogin, "accessToken"));

    sessionLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    await Send("PUT", $"/api/accounts/{staffId}", 204, new { role = 1 }, adminToken);
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = Text(sessionLogin, "refreshToken") });
    await Send("PUT", $"/api/accounts/{staffId}", 204, new { role = 0 }, adminToken);
    staffToken = await Login(prefix + "@example.test");

    await Send("PUT", "/api/auth/profile", 400, new { fullName = " " }, staffToken);
    await Send("PUT", "/api/auth/profile", 400, new { fullName = new string('x', 101) }, staffToken);
    var profile = await Send("PUT", "/api/auth/profile", 200,
        new { fullName = "  " + prefix + " Profile  ", role = 1, accountId = adminId }, staffToken);
    Check(Id(profile, "accountId") == staffId && Id(profile, "role") == 0 && Text(profile, "fullName") == prefix + " Profile",
        "Profile must update only the authenticated account's trimmed name, ignoring role and ID spoofing.");
    Check(Text(await Send("GET", $"/api/accounts/{adminId}", 200, token: adminToken), "fullName") == admin.FullName,
        "Self profile update must not affect another account.");
    await Send("PUT", "/api/auth/password", 400, new { }, staffToken);
    await Send("PUT", "/api/auth/password", 400, new { currentPassword = "wrong", newPassword = password + "new" }, staffToken);
    await Send("PUT", "/api/auth/password", 400, new { currentPassword = password, newPassword = "short" }, staffToken);
    await Send("PUT", "/api/auth/password", 400, new { currentPassword = password, newPassword = new string('x', 129) }, staffToken);
    await Send("GET", "/api/auth/me", 200, token: staffToken);
    var passwordSession = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password });
    var oldHash = (string)(await Sql("SELECT \"PasswordHash\" FROM public.\"SystemAccount\" WHERE \"AccountID\"=@id", ("id", staffId)))!;
    var changedPassword = password + "new";
    await Send("PUT", "/api/auth/password", 204, new { currentPassword = password, newPassword = changedPassword }, staffToken);
    await Send("GET", "/api/auth/me", 401, token: staffToken);
    await Send("GET", "/api/auth/me", 401, token: Text(passwordSession, "accessToken"));
    await Send("POST", "/api/auth/refresh", 401, new { refreshToken = Text(passwordSession, "refreshToken") });
    await Send("POST", "/api/auth/login", 401, new { email = prefix + "@example.test", password });
    var passwordLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password = changedPassword });
    var newHash = (string)(await Sql("SELECT \"PasswordHash\" FROM public.\"SystemAccount\" WHERE \"AccountID\"=@id", ("id", staffId)))!;
    Check(newHash != changedPassword && newHash != oldHash && hasher.VerifyHashedPassword(
        new SystemAccount(), newHash, changedPassword) != PasswordVerificationResult.Failed, "Changed passwords must be hashed with the application hasher.");
    await using (var raceContext = new TaskTrackDbContext(new DbContextOptionsBuilder<TaskTrackDbContext>().UseNpgsql(connectionString).Options))
    {
        var raceToken = new TaskTrack.Repo.Models.RefreshToken
        {
            AccountId = staffId, SessionId = Guid.NewGuid(), TokenHash = Hash(prefix + " stale-login"),
            CreatedDate = DateTime.UtcNow, ExpiresDate = DateTime.UtcNow.AddDays(7)
        };
        Check(await new RefreshTokenRepository(raceContext).StartSessionAsync(raceToken, oldHash) is null,
            "A login verified with an old password must not create a session after a password change.");
        Check(Convert.ToInt32(await Sql("SELECT count(*) FROM public.\"RefreshToken\" WHERE \"SessionID\"=@session", ("session", raceToken.SessionId))) == 0,
            "Rejected stale login must not save a token.");
    }
    var passwordRaceAccess = Text(passwordLogin, "accessToken");
    async Task<HttpResponseMessage> ChangeConcurrently(string nextPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/password")
        {
            Content = JsonContent.Create(new { currentPassword = changedPassword, newPassword = nextPassword })
        };
        request.Headers.Authorization = new("Bearer", passwordRaceAccess);
        using (request) return await http.SendAsync(request);
    }
    var passwordRace = await Task.WhenAll(ChangeConcurrently(password + "first"), ChangeConcurrently(password + "second"));
    Check(passwordRace.Count(r => (int)r.StatusCode == 204) == 1 && passwordRace.All(r => (int)r.StatusCode is 204 or 400 or 401),
        "Concurrent password changes must have exactly one winner.");
    var winnerPassword = (int)passwordRace[0].StatusCode == 204 ? password + "first" : password + "second";
    foreach (var response in passwordRace) response.Dispose();
    passwordLogin = await Send("POST", "/api/auth/login", 200, new { email = prefix + "@example.test", password = winnerPassword });
    await Send("PUT", "/api/auth/password", 204, new { currentPassword = winnerPassword, newPassword = password }, Text(passwordLogin, "accessToken"));
    staffToken = await Login(prefix + "@example.test");

    foreach (var resource in new[] { "departments", "projects", "tasks", "tags" })
    {
        await Send("POST", $"/api/{resource}", 400, new { }, staffToken);
        await Send("PUT", $"/api/{resource}/0", 400, new { }, staffToken);
        await Send("DELETE", $"/api/{resource}/0", 404, token: staffToken);
    }
    await Send("GET", "/api/departments/search", 400);
    await Send("GET", "/api/projects/search?status=4", 400);
    await Send("GET", "/api/projects/search?departmentId=0", 400);
    foreach (var query in new[] { "status=4", "priority=4", "projectId=0", "tagId=0" })
        await Send("GET", "/api/tasks/search?" + query, 400);
    foreach (var resource in new[] { "departments", "projects", "tasks" })
        await Send("GET", $"/api/{resource}/0", 404);

    var department = await Send("POST", "/api/departments", 201, new { departmentName = prefix, departmentDescription = "Fixture only" }, staffToken);
    var departmentId = Id(department, "departmentId");
    await Send("GET", $"/api/departments/{departmentId}", 200);
    Check((await Send("GET", "/api/departments/search?name=" + prefix, 200))!.AsArray().Count == 1, "Department search must find fixture.");
    await Send("PUT", $"/api/departments/{departmentId}", 204, new { departmentName = prefix + " U", departmentDescription = "Updated fixture", isActive = true }, staffToken);
    Check(Text(await Send("GET", $"/api/departments/{departmentId}", 200), "departmentDescription") == "Updated fixture", "Department update must persist.");
    await Send("PUT", "/api/departments/0", 404, new { departmentName = prefix, departmentDescription = "Fixture", isActive = true }, staffToken);

    var projectBody = new { projectName = prefix, description = "Fixture only", startDate = "2026-10-12", endDate = "2026-10-31", status = 0, departmentId };
    var projectId = Id(await Send("POST", "/api/projects", 201, projectBody, staffToken), "projectId");
    await Send("GET", $"/api/projects/{projectId}", 200);
    await Send("GET", $"/api/projects/department/{departmentId}", 200);
    Check((await Send("GET", "/api/projects/department/0", 200))!.AsArray().Count == 0, "Missing department must return an empty project list.");
    Check((await Send("GET", $"/api/projects/search?name={prefix}&status=0&departmentId={departmentId}", 200))!.AsArray().Count == 1, "Project search must find fixture.");
    Check((await Send("GET", $"/api/projects/search?name={prefix}&status=3", 200))!.AsArray().Count == 0, "Project filter must reject mismatched status.");
    await Send("POST", "/api/projects", 400, new { projectName = prefix, startDate = "2026-10-12", endDate = "2026-10-01", status = 0, departmentId = int.MaxValue }, staffToken);
    var projectUpdate = new { projectName = prefix + " U", description = "Updated fixture", startDate = "2026-10-12", endDate = "2026-11-15", status = 1, departmentId, isActive = true };
    await Send("PUT", $"/api/projects/{projectId}", 204, projectUpdate, staffToken);
    Check(Id(await Send("GET", $"/api/projects/{projectId}", 200), "status") == 1, "Project update must persist.");
    await Send("PUT", "/api/projects/0", 404, projectUpdate, staffToken);
    await Send("DELETE", $"/api/departments/{departmentId}", 400, token: staffToken);

    var tagId = Id(await Send("POST", "/api/tags", 201, new { tagName = prefix, color = "#abcdef" }, staffToken), "tagId");
    await Send("POST", "/api/tags", 400, new { tagName = prefix.ToUpperInvariant(), color = "#123456" }, staffToken);
    await Send("POST", "/api/tags", 400, new { tagName = prefix + " invalid", color = "blue" }, staffToken);
    await Send("PUT", $"/api/tags/{tagId}", 204, new { tagName = prefix + " U", color = "#fedcba" }, staffToken);
    var tag = (await Send("GET", "/api/tags", 200))!.AsArray().Single(n => Id(n, "tagId") == tagId);
    Check(Text(tag, "color") == "#FEDCBA", "Tag color normalization must persist.");
    await Send("PUT", "/api/tags/0", 404, new { tagName = prefix + " missing", color = "#123456" }, staffToken);

    await Send("POST", "/api/tasks", 400, new { title = " ", status = 4, priority = 4, projectId = 0 }, staffToken);
    var badTask = new { title = prefix, status = 0, priority = 1, projectId = int.MaxValue, tagIds = new[] { int.MaxValue } };
    await Send("POST", "/api/tasks", 400, badTask, staffToken);
    await Send("PUT", "/api/tasks/0", 400, badTask, staffToken);
    taskId = Id(await Send("POST", "/api/tasks", 201, new { title = prefix, status = 0, priority = 0, projectId, tagIds = new[] { tagId, tagId }, createdById = adminId }, staffToken), "taskId");
    var detail = await Send("GET", $"/api/tasks/{taskId}", 200);
    Check(Id(detail, "priority") == 0, "Task creation must preserve priority 0.");
    Check(detail!["tags"]!.AsArray().Count == 1, "Duplicate tags must not create duplicate links.");
    Check(Convert.ToInt32(await Sql("SELECT \"CreatedByID\" FROM public.\"Task\" WHERE \"TaskID\"=@id", ("id", taskId))) == staffId, "Task creator must be authenticated Staff, ignoring client input.");
    Check(Id(detail, "createdById") == staffId && detail!["updatedById"] is null, "A new task must expose its creator and have no updater yet.");
    await Send("GET", $"/api/tasks/project/{projectId}", 200);
    Check((await Send("GET", "/api/tasks/project/0", 200))!.AsArray().Count == 0, "Missing project must return an empty task list.");
    Check((await Send("GET", $"/api/tasks/search?title={prefix}&status=0&priority=0&projectId={projectId}&tagId={tagId}", 200))!.AsArray().Count == 1, "Task filters must find fixture.");
    await Send("DELETE", $"/api/accounts/{staffId}", 409, token: adminToken);
    await Send("DELETE", $"/api/projects/{projectId}", 400, token: staffToken);
    await Send("DELETE", $"/api/tags/{tagId}", 400, token: staffToken);
    var taskUpdate = new { title = prefix + " U", status = 2, priority = 3, projectId, tagIds = Array.Empty<int>(), updatedById = adminId };
    await Send("PUT", $"/api/tasks/{taskId}", 204, taskUpdate, staffToken);
    detail = await Send("GET", $"/api/tasks/{taskId}", 200);
    Check(Id(detail, "priority") == 3 && detail!["tags"]!.AsArray().Count == 0 && detail["modifiedDate"] is not null, "Task update must persist and clear tags.");
    Check(Id(detail, "updatedById") == staffId && Id(detail, "createdById") == staffId, "Update audit must use the authenticated updater and preserve creator, ignoring spoofed fields.");
    var editor = await Send("POST", "/api/auth/register", 201, new { fullName = prefix + " Editor", email = prefix + "-editor@example.test", password });
    var editorId = Id(editor, "accountId");
    await Send("PUT", $"/api/tasks/{taskId}", 204, taskUpdate, await Login(prefix + "-editor@example.test"));
    Check(Id(await Send("GET", $"/api/tasks/{taskId}", 200), "updatedById") == editorId, "Updates by a different account must replace the updater.");
    await Send("DELETE", $"/api/accounts/{editorId}", 204, token: adminToken);
    detail = await Send("GET", $"/api/tasks/{taskId}", 200);
    Check(detail!["updatedById"] is null && Id(detail, "createdById") == staffId, "Deleting a non-creator updater must clear its nullable link and preserve the task creator.");
    await Send("PUT", "/api/tasks/0", 404, taskUpdate, staffToken);
    await Send("DELETE", $"/api/tasks/{taskId}", 204, token: staffToken);
    await Send("GET", $"/api/tasks/{taskId}", 404);
    await Send("PUT", $"/api/tasks/{taskId}", 404, taskUpdate, staffToken);
    await Send("DELETE", $"/api/tasks/{taskId}", 404, token: staffToken);
    Check((await Send("GET", "/api/tasks/search?title=" + prefix, 200))!.AsArray().Count == 0, "Deleted task must not be searchable.");
    Check((await Send("GET", $"/api/tasks/project/{projectId}", 200))!.AsArray().Count == 0, "Deleted task must not appear in project list.");
    await Send("DELETE", $"/api/accounts/{staffId}", 409, token: adminToken);
    Check((bool)(await Sql("SELECT NOT \"IsActive\" AND \"CreatedByID\"=@creator FROM public.\"Task\" WHERE \"TaskID\"=@id", ("creator", staffId), ("id", taskId)))!, "Soft deletion must preserve row and creator.");
    Check(Convert.ToInt32(await Sql("SELECT \"UpdatedByID\" FROM public.\"Task\" WHERE \"TaskID\"=@id", ("id", taskId))) == staffId,
        "Soft delete must record the authenticated updater.");
    await Sql("DELETE FROM public.\"Task\" WHERE \"TaskID\"=@id AND \"CreatedByID\"=@creator", ("id", taskId), ("creator", staffId));
    await Send("DELETE", $"/api/tags/{tagId}", 204, token: staffToken);
    await Send("DELETE", $"/api/projects/{projectId}", 204, token: staffToken);
    await Send("GET", $"/api/projects/{projectId}", 404);
    await Send("DELETE", $"/api/departments/{departmentId}", 204, token: staffToken);
    await Send("GET", $"/api/departments/{departmentId}", 404);
    await Send("DELETE", $"/api/accounts/{staffId}", 204, token: adminToken);
    await Send("GET", "/api/auth/me", 401, token: staffToken);
    await Send("GET", $"/api/accounts/{staffId}", 404, token: adminToken);
}
finally
{
    await Sql("""
        DELETE FROM public."TaskTag" WHERE "TaskID" IN (SELECT "TaskID" FROM public."Task" WHERE "CreatedByID" IN (SELECT "AccountID" FROM public."SystemAccount" WHERE "Email" LIKE @prefix));
        DELETE FROM public."Task" WHERE "CreatedByID" IN (SELECT "AccountID" FROM public."SystemAccount" WHERE "Email" LIKE @prefix);
        DELETE FROM public."Tag" WHERE "TagName" LIKE @prefix;
        DELETE FROM public."Project" WHERE "ProjectName" LIKE @prefix;
        DELETE FROM public."Department" WHERE "DepartmentName" LIKE @prefix;
        DELETE FROM public."SystemAccount" WHERE "Email" LIKE @prefix;
        """, ("prefix", prefix + "%"));
    Check(await Fingerprint() == baseline, "Business data must match the original snapshot after fixture cleanup.");
}
Console.WriteLine($"PASS: {checks} assertions across all business routes, guest/Staff/Admin permissions, account lifecycle, JWT rejection, refresh rotation/replay/concurrency/logout, profile/password changes, login/password concurrency, creator/updater and soft-delete; fixtures cleaned and original business data unchanged.");
