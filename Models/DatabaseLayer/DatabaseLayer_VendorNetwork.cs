using Blogger_website.Models.Entities;
using Blogger_website.Models.ViewModels;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<VendorProfileDto>> GetVendorProfilesAsync(string currentUserId);
    Task<VendorConnectionRequest?> GetConnectionBetweenUsersAsync(string userA, string userB);
    Task<VendorConnectionRequest?> GetConnectionRequestByIdAsync(int id);
    Task<int> CreateConnectionRequestAsync(string fromUserId, string toUserId, string? introMessage);
    Task<bool> UpdateConnectionRequestStatusAsync(int id, string status);
    Task<List<VendorConnectionRequest>> GetIncomingConnectionRequestsAsync(string userId);
    Task<List<VendorConnectionRequest>> GetOutgoingConnectionRequestsAsync(string userId);
    Task<int> CreateChatThreadAsync(int connectionRequestId, string user1Id, string user2Id);
    Task<VendorChatThread?> GetChatThreadByIdAsync(int threadId);
    Task<VendorChatThread?> GetChatThreadByUsersAsync(string userA, string userB);
    Task<List<VendorChatThread>> GetChatThreadsForUserAsync(string userId);
    Task<List<VendorChatMessage>> GetChatMessagesAsync(int threadId, int? afterId = null, string? viewerUserId = null);
    Task<VendorChatMessage?> GetChatMessageByIdAsync(int messageId);
    Task<int> CreateChatMessageAsync(int threadId, string senderUserId, string content, int? replyToMessageId = null);
    Task<bool> UpdateChatMessageContentAsync(int messageId, string content);
    Task<bool> HideChatMessageForUserAsync(int messageId, string userId);
    Task<bool> DeleteChatMessageForEveryoneAsync(int messageId);
    Task<bool> IsUserInThreadAsync(int threadId, string userId);
}

public partial class DatabaseLayer
{
    public async Task<List<VendorProfileDto>> GetVendorProfilesAsync(string currentUserId)
    {
        var vendors = new List<VendorProfileDto>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT u."Id", COALESCE(u."FullName", u."Email", u."UserName") AS "FullName", u."Email", u."ProfileImageUrl"
            FROM "AspNetUsers" u
            INNER JOIN "AspNetUserRoles" ur ON u."Id" = ur."UserId"
            INNER JOIN "AspNetRoles" r ON ur."RoleId" = r."Id"
            WHERE r."Name" = 'Blogger' AND u."IsActive" = TRUE AND u."Id" <> @currentUserId
            ORDER BY "FullName"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("currentUserId", currentUserId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vendors.Add(new VendorProfileDto
            {
                UserId = reader.GetString(0),
                FullName = reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1),
                Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ProfileImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        foreach (var vendor in vendors)
        {
            var connection = await GetConnectionBetweenUsersAsync(currentUserId, vendor.UserId);
            if (connection == null)
            {
                vendor.ConnectionStatus = "None";
                continue;
            }

            vendor.RequestId = connection.Id;
            if (connection.Status == VendorConnectionStatus.Accepted)
            {
                vendor.ConnectionStatus = "Connected";
                var thread = await GetChatThreadByUsersAsync(currentUserId, vendor.UserId);
                vendor.ThreadId = thread?.Id;
            }
            else if (connection.Status == VendorConnectionStatus.Rejected)
            {
                vendor.ConnectionStatus = "Rejected";
            }
            else if (connection.FromUserId == currentUserId)
            {
                vendor.ConnectionStatus = "PendingOutgoing";
            }
            else
            {
                vendor.ConnectionStatus = "PendingIncoming";
            }
        }

        return vendors;
    }

    public async Task<VendorConnectionRequest?> GetConnectionBetweenUsersAsync(string userA, string userB)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT r."Id", r."FromUserId", r."ToUserId", r."Status", r."IntroMessage", r."CreatedAt", r."RespondedAt",
                   fu."FullName", fu."Email", tu."FullName", tu."Email"
            FROM "VendorConnectionRequests" r
            INNER JOIN "AspNetUsers" fu ON r."FromUserId" = fu."Id"
            INNER JOIN "AspNetUsers" tu ON r."ToUserId" = tu."Id"
            WHERE (r."FromUserId" = @userA AND r."ToUserId" = @userB)
               OR (r."FromUserId" = @userB AND r."ToUserId" = @userA)
            ORDER BY r."CreatedAt" DESC
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userA", userA);
        cmd.Parameters.AddWithValue("userB", userB);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapConnectionRequest(reader) : null;
    }

    public async Task<VendorConnectionRequest?> GetConnectionRequestByIdAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT r."Id", r."FromUserId", r."ToUserId", r."Status", r."IntroMessage", r."CreatedAt", r."RespondedAt",
                   fu."FullName", fu."Email", tu."FullName", tu."Email"
            FROM "VendorConnectionRequests" r
            INNER JOIN "AspNetUsers" fu ON r."FromUserId" = fu."Id"
            INNER JOIN "AspNetUsers" tu ON r."ToUserId" = tu."Id"
            WHERE r."Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapConnectionRequest(reader) : null;
    }

    public async Task<int> CreateConnectionRequestAsync(string fromUserId, string toUserId, string? introMessage)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "VendorConnectionRequests"
            ("FromUserId", "ToUserId", "Status", "IntroMessage", "CreatedAt")
            VALUES (@fromUserId, @toUserId, 'Pending', @introMessage, @createdAt)
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("fromUserId", fromUserId);
        cmd.Parameters.AddWithValue("toUserId", toUserId);
        cmd.Parameters.AddWithValue("introMessage", (object?)introMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpdateConnectionRequestStatusAsync(int id, string status)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "VendorConnectionRequests"
            SET "Status" = @status, "RespondedAt" = @respondedAt
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("respondedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<VendorConnectionRequest>> GetIncomingConnectionRequestsAsync(string userId)
    {
        return await GetConnectionRequestsAsync(userId, incoming: true);
    }

    public async Task<List<VendorConnectionRequest>> GetOutgoingConnectionRequestsAsync(string userId)
    {
        return await GetConnectionRequestsAsync(userId, incoming: false);
    }

    private async Task<List<VendorConnectionRequest>> GetConnectionRequestsAsync(string userId, bool incoming)
    {
        var list = new List<VendorConnectionRequest>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT r."Id", r."FromUserId", r."ToUserId", r."Status", r."IntroMessage", r."CreatedAt", r."RespondedAt",
                   fu."FullName", fu."Email", tu."FullName", tu."Email"
            FROM "VendorConnectionRequests" r
            INNER JOIN "AspNetUsers" fu ON r."FromUserId" = fu."Id"
            INNER JOIN "AspNetUsers" tu ON r."ToUserId" = tu."Id"
            WHERE r."Status" = 'Pending' AND
            """;

        sql += incoming ? " r.\"ToUserId\" = @userId " : " r.\"FromUserId\" = @userId ";
        sql += " ORDER BY r.\"CreatedAt\" DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapConnectionRequest(reader));

        return list;
    }

    public async Task<int> CreateChatThreadAsync(int connectionRequestId, string user1Id, string user2Id)
    {
        var (u1, u2) = NormalizeUserPair(user1Id, user2Id);

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "VendorChatThreads" ("ConnectionRequestId", "User1Id", "User2Id", "CreatedAt")
            VALUES (@connectionRequestId, @user1Id, @user2Id, @createdAt)
            ON CONFLICT ("User1Id", "User2Id") DO UPDATE SET "ConnectionRequestId" = EXCLUDED."ConnectionRequestId"
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("connectionRequestId", connectionRequestId);
        cmd.Parameters.AddWithValue("user1Id", u1);
        cmd.Parameters.AddWithValue("user2Id", u2);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<VendorChatThread?> GetChatThreadByIdAsync(int threadId)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT "Id", "ConnectionRequestId", "User1Id", "User2Id", "CreatedAt"
            FROM "VendorChatThreads"
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", threadId);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapChatThread(reader) : null;
    }

    public async Task<VendorChatThread?> GetChatThreadByUsersAsync(string userA, string userB)
    {
        var (u1, u2) = NormalizeUserPair(userA, userB);

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT "Id", "ConnectionRequestId", "User1Id", "User2Id", "CreatedAt"
            FROM "VendorChatThreads"
            WHERE "User1Id" = @user1Id AND "User2Id" = @user2Id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user1Id", u1);
        cmd.Parameters.AddWithValue("user2Id", u2);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapChatThread(reader) : null;
    }

    public async Task<List<VendorChatThread>> GetChatThreadsForUserAsync(string userId)
    {
        var threads = new List<VendorChatThread>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT t."Id", t."ConnectionRequestId", t."User1Id", t."User2Id", t."CreatedAt",
                   CASE WHEN t."User1Id" = @userId THEN u2."FullName" ELSE u1."FullName" END AS "OtherName",
                   CASE WHEN t."User1Id" = @userId THEN t."User2Id" ELSE t."User1Id" END AS "OtherUserId",
                   lm."Content", lm."CreatedAt"
            FROM "VendorChatThreads" t
            INNER JOIN "AspNetUsers" u1 ON t."User1Id" = u1."Id"
            INNER JOIN "AspNetUsers" u2 ON t."User2Id" = u2."Id"
            LEFT JOIN LATERAL (
                SELECT m."Content", m."CreatedAt"
                FROM "VendorChatMessages" m
                WHERE m."ThreadId" = t."Id"
                ORDER BY m."CreatedAt" DESC
                LIMIT 1
            ) lm ON TRUE
            WHERE t."User1Id" = @userId OR t."User2Id" = @userId
            ORDER BY COALESCE(lm."CreatedAt", t."CreatedAt") DESC
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            threads.Add(new VendorChatThread
            {
                Id = reader.GetInt32(0),
                ConnectionRequestId = reader.GetInt32(1),
                User1Id = reader.GetString(2),
                User2Id = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                OtherUserName = reader.IsDBNull(5) ? "Vendor" : reader.GetString(5),
                OtherUserId = reader.GetString(6),
                LastMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastMessageAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }

        return threads;
    }

    public async Task<List<VendorChatMessage>> GetChatMessagesAsync(int threadId, int? afterId = null, string? viewerUserId = null)
    {
        var messages = new List<VendorChatMessage>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT m."Id", m."ThreadId", m."SenderUserId", m."Content", m."CreatedAt", m."EditedAt", m."DeletedForEveryone",
                   COALESCE(NULLIF(TRIM(u."FullName"), ''), u."Email", u."UserName", 'Vendor') AS "SenderName",
                   m."ReplyToMessageId",
                   COALESCE(rm."DeletedForEveryone", FALSE) AS "ReplyDeleted",
                   CASE WHEN rm."Id" IS NULL THEN NULL
                        WHEN rm."DeletedForEveryone" THEN ''
                        ELSE rm."Content" END AS "ReplyContent",
                   COALESCE(NULLIF(TRIM(ru."FullName"), ''), ru."Email", ru."UserName", 'Vendor') AS "ReplySenderName"
            FROM "VendorChatMessages" m
            INNER JOIN "AspNetUsers" u ON m."SenderUserId" = u."Id"
            LEFT JOIN "VendorChatMessages" rm ON m."ReplyToMessageId" = rm."Id"
            LEFT JOIN "AspNetUsers" ru ON rm."SenderUserId" = ru."Id"
            """;

        if (!string.IsNullOrEmpty(viewerUserId))
            sql += """
                 LEFT JOIN "VendorChatMessageHidden" h ON h."MessageId" = m."Id" AND h."UserId" = @viewerUserId
                """;

        sql += " WHERE m.\"ThreadId\" = @threadId";

        if (!string.IsNullOrEmpty(viewerUserId))
            sql += " AND h.\"MessageId\" IS NULL";

        if (afterId.HasValue)
            sql += " AND m.\"Id\" > @afterId";

        sql += " ORDER BY m.\"CreatedAt\" ASC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("threadId", threadId);
        if (afterId.HasValue)
            cmd.Parameters.AddWithValue("afterId", afterId.Value);
        if (!string.IsNullOrEmpty(viewerUserId))
            cmd.Parameters.AddWithValue("viewerUserId", viewerUserId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(MapChatMessage(reader));
        }

        return messages;
    }

    public async Task<VendorChatMessage?> GetChatMessageByIdAsync(int messageId)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT m."Id", m."ThreadId", m."SenderUserId", m."Content", m."CreatedAt", m."EditedAt", m."DeletedForEveryone",
                   COALESCE(NULLIF(TRIM(u."FullName"), ''), u."Email", u."UserName", 'Vendor') AS "SenderName",
                   m."ReplyToMessageId",
                   COALESCE(rm."DeletedForEveryone", FALSE) AS "ReplyDeleted",
                   CASE WHEN rm."Id" IS NULL THEN NULL
                        WHEN rm."DeletedForEveryone" THEN ''
                        ELSE rm."Content" END AS "ReplyContent",
                   COALESCE(NULLIF(TRIM(ru."FullName"), ''), ru."Email", ru."UserName", 'Vendor') AS "ReplySenderName"
            FROM "VendorChatMessages" m
            INNER JOIN "AspNetUsers" u ON m."SenderUserId" = u."Id"
            LEFT JOIN "VendorChatMessages" rm ON m."ReplyToMessageId" = rm."Id"
            LEFT JOIN "AspNetUsers" ru ON rm."SenderUserId" = ru."Id"
            WHERE m."Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", messageId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return MapChatMessage(reader);
    }

    public async Task<int> CreateChatMessageAsync(int threadId, string senderUserId, string content, int? replyToMessageId = null)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "VendorChatMessages" ("ThreadId", "SenderUserId", "Content", "CreatedAt", "ReplyToMessageId")
            VALUES (@threadId, @senderUserId, @content, @createdAt, @replyToMessageId)
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("threadId", threadId);
        cmd.Parameters.AddWithValue("senderUserId", senderUserId);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("replyToMessageId", (object?)replyToMessageId ?? DBNull.Value);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpdateChatMessageContentAsync(int messageId, string content)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "VendorChatMessages"
            SET "Content" = @content, "EditedAt" = @editedAt, "DeletedForEveryone" = FALSE
            WHERE "Id" = @id AND "DeletedForEveryone" = FALSE
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("editedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", messageId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> HideChatMessageForUserAsync(int messageId, string userId)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "VendorChatMessageHidden" ("MessageId", "UserId", "HiddenAt")
            VALUES (@messageId, @userId, @hiddenAt)
            ON CONFLICT ("MessageId", "UserId") DO NOTHING
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("messageId", messageId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hiddenAt", DateTime.UtcNow);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteChatMessageForEveryoneAsync(int messageId)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "VendorChatMessages"
            SET "DeletedForEveryone" = TRUE, "Content" = '', "EditedAt" = NULL
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", messageId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> IsUserInThreadAsync(int threadId, string userId)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT 1 FROM "VendorChatThreads"
            WHERE "Id" = @threadId AND ("User1Id" = @userId OR "User2Id" = @userId)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("threadId", threadId);
        cmd.Parameters.AddWithValue("userId", userId);

        return await cmd.ExecuteScalarAsync() != null;
    }

    private static VendorChatMessage MapChatMessage(NpgsqlDataReader reader)
    {
        var deleted = reader.GetBoolean(6);
        return new VendorChatMessage
        {
            Id = reader.GetInt32(0),
            ThreadId = reader.GetInt32(1),
            SenderUserId = reader.GetString(2),
            Content = deleted ? string.Empty : reader.GetString(3),
            CreatedAt = reader.GetDateTime(4),
            EditedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            DeletedForEveryone = deleted,
            SenderName = reader.IsDBNull(7) ? "Vendor" : reader.GetString(7),
            ReplyToMessageId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            ReplyToDeleted = !reader.IsDBNull(9) && reader.GetBoolean(9),
            ReplyToContent = reader.IsDBNull(10) ? null : reader.GetString(10),
            ReplyToSenderName = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    private static (string U1, string U2) NormalizeUserPair(string userA, string userB)
        => string.CompareOrdinal(userA, userB) <= 0 ? (userA, userB) : (userB, userA);

    private static VendorConnectionRequest MapConnectionRequest(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        FromUserId = reader.GetString(1),
        ToUserId = reader.GetString(2),
        Status = reader.GetString(3),
        IntroMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
        CreatedAt = reader.GetDateTime(5),
        RespondedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        FromUserName = reader.IsDBNull(7) ? null : reader.GetString(7),
        FromUserEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
        ToUserName = reader.IsDBNull(9) ? null : reader.GetString(9),
        ToUserEmail = reader.IsDBNull(10) ? null : reader.GetString(10)
    };

    private static VendorChatThread MapChatThread(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ConnectionRequestId = reader.GetInt32(1),
        User1Id = reader.GetString(2),
        User2Id = reader.GetString(3),
        CreatedAt = reader.GetDateTime(4)
    };
}
