using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SQS;
using JobManager.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobManager.API;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppDb");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonDynamoDB>();

        return services;
    }
}