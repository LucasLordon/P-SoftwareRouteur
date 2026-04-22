using Microsoft.EntityFrameworkCore;
using SoftwareRouteur.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SoftwareRouteur.Models;
using SoftwareRouteur.Services;

var builder = WebApplication.CreateBuilder(args);

var password = Environment.GetEnvironmentVariable("DB_PASSWORD")
               ?? builder.Configuration["DB_PASSWORD"];

var apiKey = Environment.GetEnvironmentVariable("API_KEY")
              ?? builder.Configuration["API_KEY"];

var apiSecret = Environment.GetEnvironmentVariable("API_SECRET")
               ?? builder.Configuration["API_SECRET"];

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!
    .Replace("${DB_PASSWORD}", password);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddViewLocalization();

builder.Services.Configure<OPNsenseSettings>(
    builder.Configuration.GetSection("OPNsense"));

builder.Services.AddSingleton<OPNsenseService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Index");
    app.UseHsts();
}

//app.UseHttpsRedirection();

var supportedCultures = new[] { "fr", "en", "de" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("fr")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();