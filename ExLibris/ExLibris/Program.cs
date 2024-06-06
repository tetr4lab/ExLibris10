using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using MudBlazor;
using MudBlazor.Services;
using PetaPoco;
using ExLibris.Components;
using ExLibris.Services;

var builder = WebApplication.CreateBuilder (args);
var connectionString = $"database=exlibris;{builder.Configuration.GetConnectionString ("Host")}{builder.Configuration.GetConnectionString ("Account")}";

// Add services to the container.
builder.Services.AddRazorComponents ()
    .AddInteractiveServerComponents ()
    .AddInteractiveWebAssemblyComponents ();

// MudBlazor
builder.Services.AddMudServices (config => {
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
    config.SnackbarConfiguration.MaximumOpacity = 80;
});

// クッキーとグーグルの認証を構成
builder.Services.AddAuthentication (options => {
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
    .AddCookie ()
    .AddGoogle (options => {
        options.ClientId = builder.Configuration ["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration ["Authentication:Google:ClientSecret"]!;
    });

// メールアドレスを保持するクレームを要求する認可用のポリシーを構成
builder.Services.AddAuthorization (options => {
    // 管理者
    options.AddPolicy ("Admin", policyBuilder => {
        policyBuilder.RequireClaim (ClaimTypes.Email, builder.Configuration ["Identity:Claims:EmailAddress:Admin:0"]!);
    });
    // 一般ユーザ (管理者を含む)
    options.AddPolicy ("Users", policyBuilder => {
        policyBuilder.RequireClaim (ClaimTypes.Email, builder.Configuration ["Identity:Claims:EmailAddress:Admin:0"]!, builder.Configuration ["Identity:Claims:EmailAddress:User:0"]!, builder.Configuration ["Identity:Claims:EmailAddress:User:1"]!);
    });
});

// PetaPoco with MySqlConnector
builder.Services.AddTransient (_ => (Database) new MySqlDatabase (connectionString, "MySqlConnector"));

// HTTP Client
builder.Services.AddHttpClient ();

// ExLibrisDataSet
builder.Services.AddTransient<ExLibrisDataSet> ();

var app = builder.Build ();

// Application level Culture
app.UseRequestLocalization (new RequestLocalizationOptions ()
    .AddSupportedCultures (["ja-JP",])
    .AddSupportedUICultures (["ja-JP",]));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment ()) {
    app.UseWebAssemblyDebugging ();
} else {
    app.UseExceptionHandler ("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts ();
}

app.UseHttpsRedirection ();

app.UseStaticFiles ();
app.UseAntiforgery ();
app.UseAuthentication ();
app.UseAuthorization ();

app.MapRazorComponents<App> ()
    .AddInteractiveServerRenderMode ()
    .AddInteractiveWebAssemblyRenderMode ()
    .AddAdditionalAssemblies (typeof (ExLibris.Client._Imports).Assembly);

System.Diagnostics.Debug.WriteLine ("Initialized");
app.Run ();
