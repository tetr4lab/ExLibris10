using MySql.Data.MySqlClient;
using System.Data;
using ExLibris.Client.Pages;
using ExLibris.Components;

var builder = WebApplication.CreateBuilder (args);
var connectionString = $"Database=exlibris;{builder.Configuration.GetConnectionString ("Host")}{builder.Configuration.GetConnectionString ("Account")}";

// Add services to the container.
builder.Services.AddRazorComponents ()
    .AddInteractiveServerComponents ()
    .AddInteractiveWebAssemblyComponents ();

// MySqlConnector
builder.Services.AddTransient<IDbConnection> (db => new MySqlConnection (connectionString));

var app = builder.Build ();

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

app.MapRazorComponents<App> ()
    .AddInteractiveServerRenderMode ()
    .AddInteractiveWebAssemblyRenderMode ()
    .AddAdditionalAssemblies (typeof (ExLibris.Client._Imports).Assembly);

app.Run ();
