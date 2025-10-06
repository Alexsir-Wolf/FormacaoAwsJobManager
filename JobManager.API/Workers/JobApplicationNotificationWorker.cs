
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Net;

namespace JobManager.API.Workers;

public class JobApplicationNotificationWorker : BackgroundService
{
    readonly IConfiguration _configuration;

    public JobApplicationNotificationWorker(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new AmazonSQSClient(RegionEndpoint.USEast2);

        var queue = await client.GetQueueUrlAsync("formacao-aws-alex-fs-dev");

        while (!stoppingToken.IsCancellationRequested)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queue.QueueUrl,
                MessageAttributeNames = ["All"],
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20
            };

            var response = await client.ReceiveMessageAsync(request, stoppingToken);
            
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                foreach (var message in response.Messages)
                {
                    Console.WriteLine($"Received message: {message.Body}");

                    await client.DeleteMessageAsync(queue.QueueUrl, message.ReceiptHandle);
                }
            }
        }
    }
}
