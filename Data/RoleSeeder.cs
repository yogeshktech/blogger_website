using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Identity;

namespace Blogger_website.Data;

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RoleSeeder");

        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Blogger })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var superAdminEmail = config["Seed:SuperAdminEmail"] ?? "superadmin@blogger.com";
        var superAdminPassword = config["Seed:SuperAdminPassword"] ?? "SuperAdmin@123";

        var user = await userManager.FindByEmailAsync(superAdminEmail);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                FullName = "Super Admin",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, superAdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);
                logger.LogInformation("SuperAdmin created: {Email}", superAdminEmail);
            }
            else
            {
                logger.LogError("SuperAdmin create failed: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            return;
        }

        // User exists — ensure SuperAdmin role + active + email confirmed
        var changed = false;

        if (string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = "Super Admin";
            changed = true;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            changed = true;
        }

        if (changed)
            await userManager.UpdateAsync(user);

        if (!await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
            await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);

        // Password reset sirf tab jab config mein explicitly enable ho
        if (config.GetValue<bool>("Seed:ResetSuperAdminPassword"))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, token, superAdminPassword);
            logger.LogWarning("SuperAdmin password reset to configured default.");
        }

        logger.LogInformation("SuperAdmin ready: {Email}", superAdminEmail);
    }
}
