using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddAuthentication("PriserCookies")
    .AddCookie("PriserCookies", options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdmin", p => p.RequireRole("TenantAdmin", "SystemAdmin"));
    options.AddPolicy("Manager", p => p.RequireRole("Manager", "TenantAdmin", "SystemAdmin"));
    options.AddPolicy("Employee", p => p.RequireRole("Employee", "Manager", "TenantAdmin", "SystemAdmin"));
    options.AddPolicy("Viewer", p => p.RequireRole("Viewer", "TenantAdmin", "SystemAdmin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EnterpriseSchemaService>();
builder.Services.AddScoped<PlatformBootstrapService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Auto-migrate, self-heal enterprise schema, and seed the masked platform admin.
using (var scope = app.Services.CreateScope())
{
    var bootstrap = scope.ServiceProvider.GetRequiredService<PlatformBootstrapService>();
    await bootstrap.EnsureAsync();
}

app.Run();
