using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis.Scripting;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Extensions;
using YBotEngine.Runners.Abstractions;
using YBotEngine.Runners.Roslyn;
using YBotEngine.Services;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
builder.Services.AddDiscordGateway(opt =>
{
    opt.Token = config["Discord:Token"];
});
builder.Services.AddApplicationCommands();

builder.Services.AddDefaultScriptOptions();

builder.Services.AddKeyedSingleton<ICompiler, RoslynCompiler>(CompilerType.Roslyn);

builder.Services.AddSingleton<DiscordEventRegistry>();
builder.Services.AddSingleton<UserScriptManager>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
}

app.UseRouting();
//app.UseAuthorization();

app.MapFallbackToFile("index.html");

app.Run();