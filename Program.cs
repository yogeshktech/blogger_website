using Blogger_website.Areas.Identity.Data;
using Blogger_website.Data;
using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Services;
using CareerCracker.S3Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AppDbContextConnection")
    ?? throw new InvalidOperationException("Connection string 'AppDbContextConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IAdminRegistrationService, AdminRegistrationService>();

builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
});
builder.Services.AddScoped<IDatabaseLayer, DatabaseLayer>();
builder.Services.AddScoped<IBusinessLayer, BusinessLayer>();

S3StorageHelper.Initialize(builder.Configuration);

var app = builder.Build();

var smtpUser = app.Configuration["Smtp:Username"];
var smtpPwdLen = app.Configuration["Smtp:Password"]?.Where(c => !char.IsWhiteSpace(c)).Count() ?? 0;
app.Logger.LogInformation("SMTP loaded: User={User}, AppPasswordLength={Length} (expected 16 for Gmail)", smtpUser, smtpPwdLen);

await RoleSeeder.SeedAsync(app.Services);
await DatabaseInitializer.InitializeAsync(app.Configuration);

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
