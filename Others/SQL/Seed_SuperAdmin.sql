-- =============================================================================
-- SuperAdmin — Roles seed (user app start par auto-create hota hai)
-- =============================================================================
--
-- LOGIN CREDENTIALS (appsettings.json):
--   Email:    superadmin@blogger.com
--   Password: SuperAdmin@123
--
-- User + password ASP.NET Identity hash se app banata hai.
-- SQL se plain password insert NAHI ho sakta easily.
--
-- SuperAdmin user banane ke liye:
--   1. dotnet run  (RoleSeeder automatic chalega)  ← RECOMMENDED
--   2. Ya pgAdmin se ye roles run karo, phir app ek baar start karo
-- =============================================================================

INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'SuperAdmin', 'SUPERADMIN', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = 'SUPERADMIN');

INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Blogger', 'BLOGGER', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = 'BLOGGER');

-- Verify roles:
-- SELECT * FROM "AspNetRoles";
-- SELECT u."Email", u."FullName", u."IsActive", r."Name" AS "Role"
-- FROM "AspNetUsers" u
-- LEFT JOIN "AspNetUserRoles" ur ON u."Id" = ur."UserId"
-- LEFT JOIN "AspNetRoles" r ON ur."RoleId" = r."Id"
-- WHERE u."Email" = 'superadmin@blogger.com';
