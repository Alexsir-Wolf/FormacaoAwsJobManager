using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Carter;
using JobManager.API;
using JobManager.API.Workers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSystemsManager(source =>
{
    source.AwsOptions = new AWSOptions
    {
        Region = RegionEndpoint.USEast2
    };

    source.Path = "/";
    source.ReloadAfter = TimeSpan.FromSeconds(30);
});

//Secret Manager
//builder.Configuration.AddSecretsManager(null, RegionEndpoint.USEast2, config =>
//{
//    config.KeyGenerator = (secret, name) => name.Replace("/", ":");
//    config.PollingInterval = TimeSpan.FromMinutes(30);
//});

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<JobApplicationNotificationWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "JobManager.API", Version = "v1" });
});

builder.Services.AddCarter();

// CORS
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobManager.API v1");
    });
}

app.UseHttpsRedirection();

app.MapCarter();

app.UseCors();

app.Run();