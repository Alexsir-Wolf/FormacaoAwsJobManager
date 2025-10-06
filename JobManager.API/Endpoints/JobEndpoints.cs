using Amazon.S3.Model;
using Carter;
using JobManager.API.Entities;
using JobManager.API.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace JobManager.API.Endpoints;

public class JobEndpoints : CarterModule
{
    public JobEndpoints() : base("/api/jobs")
    {
        WithTags("Jobs");
    }

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/", CreateJob)
            .Produces<Job>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        app.MapGet("/{id}", GetJobById)
            .Produces<Job>()
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        app.MapGet("/", GetJobs)
            .Produces<List<Job>>()
            .WithOpenApi();

        app.MapPost("/{id}/job-applications", CreateJobApplication)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        app.MapPut("/job-applications/{id}/upload-cv", UploadCv)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi()
            .DisableAntiforgery();

        app.MapGet("/{id}/job-applications/cv", GetCv)
            .Produces<List<Job>>()
            .WithOpenApi();
    }

    private async Task<IResult> CreateJob(Job job, AppDbContext db)
    {
        await db.Jobs.AddAsync(job);
        await db.SaveChangesAsync();

        return Results.Created($"/api/jobs/{job.Id}", job);
    }

    private async Task<IResult> GetJobById(int id, AppDbContext db)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id);

        if (job is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(job);
    }

    private async Task<IResult> GetJobs(AppDbContext db)
    {
        var jobs = await db.Jobs.ToListAsync();

        return Results.Ok(jobs);
    }

    private async Task<IResult> CreateJobApplication(
        int id, 
        JobApplication application, 
        [FromServices] AppDbContext db,
        [FromServices] IConfiguration configuration)
    {
        var exists = await db.Jobs.AnyAsync(j => j.Id == id);

        if (!exists)
        {
            return Results.NotFound();
        }

        application.JobId = id;

        await db.JobApplications.AddAsync(application);
        await db.SaveChangesAsync();

        var client = new Amazon.S3.AmazonS3Client(Amazon.RegionEndpoint.USEast2);
        var sqs = new Amazon.SQS.AmazonSQSClient(Amazon.RegionEndpoint.USEast2);

        var message = $"New job application for job {id} from {application.CandidateName} | {application.CandidateEmail}";
        var queue = await sqs.GetQueueUrlAsync("formacao-aws-alex-fs-dev");

        if (string.IsNullOrEmpty(queue.QueueUrl))
            return Results.NotFound("Queue not Found;");

        var request = new Amazon.SQS.Model.SendMessageRequest
        {
            QueueUrl = queue.QueueUrl,
            MessageBody = message
        };

        var result = await sqs.SendMessageAsync(request);

        return Results.NoContent();
    }

    private async Task<IResult> UploadCv(
        int id, 
        IFormFile file, 
        [FromServices] AppDbContext db, 
        [FromServices] IConfiguration configuration)
    {
        if (file == null || file.Length == 0)        
            return Results.BadRequest();        

        var extension = Path.GetExtension(file.FileName);

        var validExtensions = new List<string> { ".pdf", ".docx" };

        if (!validExtensions.Contains(extension))        
            return Results.BadRequest();        

        var key = $"job-applications/{id}-{file.FileName}";

        var client = new Amazon.S3.AmazonS3Client(Amazon.RegionEndpoint.USEast2);
        using var stream = file.OpenReadStream();

        var bucketName = configuration.GetValue<string>("Buckets:alex-fs-dev-dotnet") ?? string.Empty;
        if (string.IsNullOrEmpty(bucketName))
            return Results.NotFound("Bucket not Found;");

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream
        };

        var response = await client.PutObjectAsync(putRequest);

        var application = await db.JobApplications.SingleOrDefaultAsync(ja => ja.Id == id);

        if (application is null)        
            return Results.NotFound();        

        application.CVUrl = key;

        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private async Task<IResult> GetCv(
        int jobId, 
        string email,
        [FromServices] AppDbContext db,
        [FromServices] IConfiguration configuration)
    {
        var bucketName = configuration.GetValue<string>("Buckets:alex-fs-dev-dotnet") ?? string.Empty;
        if (string.IsNullOrEmpty(bucketName))
            return Results.NotFound("Bucket not Found;");

        var application = await db.JobApplications
            .SingleOrDefaultAsync(ja => ja.JobId == jobId && ja.CandidateEmail == email);

        if (application is null)        
            return Results.NotFound();   

        var getRequest = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = application.CVUrl
        };

        var client = new Amazon.S3.AmazonS3Client(Amazon.RegionEndpoint.USEast2);
        var response = await client.GetObjectAsync(getRequest);

        if (response.HttpStatusCode != HttpStatusCode.OK)        
            return Results.NotFound();        

        return Results.File(response.ResponseStream, response.Headers.ContentType);
    }
}