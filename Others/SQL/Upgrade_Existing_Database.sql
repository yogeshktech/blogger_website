-- =============================================================================
-- BlogHub — UPGRADE SCRIPT (Existing Database ke liye)
-- Aapke paas pehle se ye tables hain:
--   AspNetUsers, AspNetRoles, AspNetUserRoles, Blogs, BlogCategories, Comments
--
-- Ye script SIRF nayi cheezein add karega — purani tables delete NAHI karega
-- pgAdmin mein poora script select karke Execute karo
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. AspNetUsers — admin profile columns (agar nahi hain)
-- -----------------------------------------------------------------------------
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "FullName" TEXT NULL;
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "IsActive" BOOLEAN NOT NULL DEFAULT TRUE;


-- -----------------------------------------------------------------------------
-- 2. BlogCategories — Category / Sub / Child hierarchy columns
-- -----------------------------------------------------------------------------
ALTER TABLE "BlogCategories" ADD COLUMN IF NOT EXISTS "ParentId"  INT NULL;
ALTER TABLE "BlogCategories" ADD COLUMN IF NOT EXISTS "Slug"      VARCHAR(120) NULL;
ALTER TABLE "BlogCategories" ADD COLUMN IF NOT EXISTS "SortOrder" INT NOT NULL DEFAULT 0;

-- Parent self-reference (safe — pehle se nahi hai to hi add hoga)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_BlogCategories_Parent'
    ) THEN
        ALTER TABLE "BlogCategories"
            ADD CONSTRAINT "FK_BlogCategories_Parent"
            FOREIGN KEY ("ParentId") REFERENCES "BlogCategories"("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_BlogCategories_ParentId" ON "BlogCategories" ("ParentId");
CREATE INDEX IF NOT EXISTS "IX_BlogCategories_Slug"     ON "BlogCategories" ("Slug");


-- -----------------------------------------------------------------------------
-- 3. BlogLabels — NAYA TABLE (Tags: Jokes, Study, Comedy, News, PHP, etc.)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "BlogLabels"
(
    "Id"        SERIAL PRIMARY KEY,
    "Name"      VARCHAR(100) NOT NULL UNIQUE,
    "Slug"      VARCHAR(120) NULL,
    "IsActive"  BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_BlogLabels_Slug" ON "BlogLabels" ("Slug");


-- -----------------------------------------------------------------------------
-- 4. BlogLabelMappings — NAYA TABLE (Blog ↔ multiple Labels)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "BlogLabelMappings"
(
    "BlogId"  INT NOT NULL,
    "LabelId" INT NOT NULL,
    CONSTRAINT "PK_BlogLabelMappings" PRIMARY KEY ("BlogId", "LabelId"),
    CONSTRAINT "FK_BlogLabelMappings_Blog"
        FOREIGN KEY ("BlogId") REFERENCES "Blogs"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BlogLabelMappings_Label"
        FOREIGN KEY ("LabelId") REFERENCES "BlogLabels"("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_BlogLabelMappings_LabelId" ON "BlogLabelMappings" ("LabelId");


-- -----------------------------------------------------------------------------
-- 5. Blogs — extra indexes (optional, performance)
-- -----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS "IX_Blogs_CategoryId"      ON "Blogs" ("CategoryId");
CREATE INDEX IF NOT EXISTS "IX_Blogs_CreatedByUserId" ON "Blogs" ("CreatedByUserId");
CREATE INDEX IF NOT EXISTS "IX_Blogs_IsPublished"     ON "Blogs" ("IsPublished");
CREATE INDEX IF NOT EXISTS "IX_Blogs_Slug"            ON "Blogs" ("Slug");

CREATE INDEX IF NOT EXISTS "IX_Comments_BlogId" ON "Comments" ("BlogId");
CREATE INDEX IF NOT EXISTS "IX_Comments_UserId" ON "Comments" ("UserId");


-- -----------------------------------------------------------------------------
-- 6. Comments — reply support (ParentId)
-- -----------------------------------------------------------------------------
ALTER TABLE "Comments" ADD COLUMN IF NOT EXISTS "ParentId" INT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Comments_Parent'
    ) THEN
        ALTER TABLE "Comments"
            ADD CONSTRAINT "FK_Comments_Parent"
            FOREIGN KEY ("ParentId") REFERENCES "Comments"("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Comments_ParentId" ON "Comments" ("ParentId");


-- -----------------------------------------------------------------------------
-- 7. Vendor Network — connection requests + chat
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "VendorConnectionRequests"
(
    "Id"           SERIAL PRIMARY KEY,
    "FromUserId"   TEXT NOT NULL,
    "ToUserId"     TEXT NOT NULL,
    "Status"       VARCHAR(20) NOT NULL DEFAULT 'Pending',
    "IntroMessage" TEXT NULL,
    "CreatedAt"    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "RespondedAt"  TIMESTAMP NULL,
    CONSTRAINT "FK_VendorConnectionRequests_FromUser"
        FOREIGN KEY ("FromUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VendorConnectionRequests_ToUser"
        FOREIGN KEY ("ToUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CHK_VendorConnectionRequests_Status"
        CHECK ("Status" IN ('Pending', 'Accepted', 'Rejected'))
);

CREATE INDEX IF NOT EXISTS "IX_VendorConnectionRequests_FromUserId" ON "VendorConnectionRequests" ("FromUserId");
CREATE INDEX IF NOT EXISTS "IX_VendorConnectionRequests_ToUserId" ON "VendorConnectionRequests" ("ToUserId");
CREATE INDEX IF NOT EXISTS "IX_VendorConnectionRequests_Status" ON "VendorConnectionRequests" ("Status");

CREATE TABLE IF NOT EXISTS "VendorChatThreads"
(
    "Id"                  SERIAL PRIMARY KEY,
    "ConnectionRequestId" INT NOT NULL,
    "User1Id"             TEXT NOT NULL,
    "User2Id"             TEXT NOT NULL,
    "CreatedAt"           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_VendorChatThreads_ConnectionRequest"
        FOREIGN KEY ("ConnectionRequestId") REFERENCES "VendorConnectionRequests" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VendorChatThreads_User1"
        FOREIGN KEY ("User1Id") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VendorChatThreads_User2"
        FOREIGN KEY ("User2Id") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_VendorChatThreads_Pair" UNIQUE ("User1Id", "User2Id")
);

CREATE TABLE IF NOT EXISTS "VendorChatMessages"
(
    "Id"           SERIAL PRIMARY KEY,
    "ThreadId"     INT NOT NULL,
    "SenderUserId" TEXT NOT NULL,
    "Content"      TEXT NOT NULL,
    "CreatedAt"    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_VendorChatMessages_Thread"
        FOREIGN KEY ("ThreadId") REFERENCES "VendorChatThreads" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VendorChatMessages_Sender"
        FOREIGN KEY ("SenderUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_VendorChatMessages_ThreadId" ON "VendorChatMessages" ("ThreadId");


-- -----------------------------------------------------------------------------
-- 8. Blog Likes
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "BlogLikes"
(
    "Id"         SERIAL PRIMARY KEY,
    "BlogId"     INT NOT NULL,
    "LikedByKey" VARCHAR(100) NOT NULL,
    "CreatedAt"  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_BlogLikes_Blog"
        FOREIGN KEY ("BlogId") REFERENCES "Blogs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_BlogLikes_Blog_LikedByKey" UNIQUE ("BlogId", "LikedByKey")
);

CREATE INDEX IF NOT EXISTS "IX_BlogLikes_BlogId" ON "BlogLikes" ("BlogId");


-- -----------------------------------------------------------------------------
-- 10. Profile image on users
-- -----------------------------------------------------------------------------
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "ProfileImageUrl" TEXT NULL;


-- -----------------------------------------------------------------------------
-- 12. Chat message edit / delete
-- -----------------------------------------------------------------------------
ALTER TABLE "VendorChatMessages" ADD COLUMN IF NOT EXISTS "EditedAt" TIMESTAMP NULL;
ALTER TABLE "VendorChatMessages" ADD COLUMN IF NOT EXISTS "DeletedForEveryone" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "VendorChatMessageHidden"
(
    "MessageId" INT NOT NULL,
    "UserId"    TEXT NOT NULL,
    "HiddenAt"  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_VendorChatMessageHidden" PRIMARY KEY ("MessageId", "UserId"),
    CONSTRAINT "FK_VendorChatMessageHidden_Message"
        FOREIGN KEY ("MessageId") REFERENCES "VendorChatMessages" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VendorChatMessageHidden_User"
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


-- -----------------------------------------------------------------------------
-- 13. Chat reply-to message reference
-- -----------------------------------------------------------------------------
ALTER TABLE "VendorChatMessages" ADD COLUMN IF NOT EXISTS "ReplyToMessageId" INT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_VendorChatMessages_ReplyTo'
    ) THEN
        ALTER TABLE "VendorChatMessages"
            ADD CONSTRAINT "FK_VendorChatMessages_ReplyTo"
            FOREIGN KEY ("ReplyToMessageId") REFERENCES "VendorChatMessages" ("Id") ON DELETE SET NULL;
    END IF;
END $$;


-- -----------------------------------------------------------------------------
-- 11. Default Roles (agar nahi hain)
-- -----------------------------------------------------------------------------
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'SuperAdmin', 'SUPERADMIN', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = 'SUPERADMIN');

INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Blogger', 'BLOGGER', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = 'BLOGGER');


-- =============================================================================
-- DONE! Ab pgAdmin mein Refresh karo — ye 2 nayi tables dikhengi:
--   ✅ BlogLabels
--   ✅ BlogLabelMappings
-- =============================================================================
