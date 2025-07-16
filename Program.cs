using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(option =>
    {
        option.LoginPath = "/Access/Index";
        option.ExpireTimeSpan = TimeSpan.FromHours(10);
        option.AccessDeniedPath = "/Home/Privacy";
    });

// REMOVER esta línea para IIS - IIS maneja las URLs
// builder.WebHost.UseUrls("http://0.0.0.0:5187", "https://0.0.0.0:7190");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // REMOVER UseHsts() también para evitar problemas de HTTPS
    // app.UseHsts();
}

// REMOVER si existe - IIS maneja la redirección HTTPS
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Stock}/{action=Index}/{id?}");

app.Run();