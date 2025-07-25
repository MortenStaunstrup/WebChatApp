using HubServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();

    });
});

var app = builder.Build();

app.UseCors();

app.MapHub<UserHub>("/chatMe");

app.Run();