using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using System.Text.RegularExpressions;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaQueueTrigger;

public class Function
{

    public Function()
    {

    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach(var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        string queueMessage = message.Body.ToString();

        // split message body 
        int stringSplitIndex = queueMessage.IndexOf("|");
        string splitString = queueMessage.Substring(0, stringSplitIndex);

        // get license
        int splitLicenseIndexStart = splitString.IndexOf('"');
        int splitLicenseIndexEnd = splitString.LastIndexOf('"');
        string license = splitString.Substring(splitLicenseIndexStart + 1, splitLicenseIndexEnd - 1);

        // get address
        int splitAddressIndexStart = splitLicenseIndexEnd;
        int splitAddressIndexEnd = splitString.IndexOf("-") - splitAddressIndexStart;
        string address = splitString.Substring(splitAddressIndexStart + 1, splitAddressIndexEnd - 1);

        // get date/time
        int splitTimeIndexStart = splitAddressIndexEnd + splitAddressIndexStart;
        int splitTimeIndexEnd = splitString.LastIndexOf("-") - splitTimeIndexStart;
        string dateTime = splitString.Substring(splitTimeIndexStart + 1, splitTimeIndexEnd - 1);

        // get type
        int splitTypeIndexStart = splitTimeIndexEnd + splitTimeIndexStart;
        int splitTypeIndexEnd = stringSplitIndex - splitTypeIndexStart;
        string type = splitString.Substring(splitTypeIndexStart + 1, splitTypeIndexEnd - 1);

        // get vehicle info
        string make = Regex.Match(queueMessage, "<make>(.*?)</make>").Groups[1].Value;
        string model = Regex.Match(queueMessage, "<model>(.*?)</model>").Groups[1].Value;
        string color = Regex.Match(queueMessage, "<color>(.*?)</color>").Groups[1].Value;
        string vehicle = color + " " + make + " " + model;

        // get ticket amount
        string ticketAmount = "$0.00";
        string ticketAmountValidator = type.ToLower().TrimStart();
        if(ticketAmountValidator == "no stop")
        {
            ticketAmount = "$300.00";
        }
        if(ticketAmountValidator == "no full stop on right")
        {
            ticketAmount = "$75.00";
        }
        if(ticketAmountValidator == "no right on red")
        {
            ticketAmount = "125.00";
        }
        string heading = "Your vehicle was involved in a traffic violation. Please pay the specified ticket amount by 30 days:";
        string email = heading + "\n\n" + "Vehicle: " + vehicle + "\n" + "License Plate: " + license + "\n" + "Date: " + 
            dateTime + "\n" + "Violation address: " + address + "\n" + "Violation type: " + type + 
            "\n" + "Ticket amount: " + ticketAmount;

        Console.WriteLine(email);

        using (var snsClient = new AmazonSimpleNotificationServiceClient())
        {
            var request = new PublishRequest
            {
                TopicArn = "arn:aws:sns:us-east-1:958433759875:project-3-email-topic",
                Message = email,
                Subject = "Project 3"
            };
            var response = await snsClient.PublishAsync(request);
            // Log the response
            context.Logger.LogLine($"SNS Publish Response: {response.HttpStatusCode}");
        }

        // TODO: Do interesting work based on the new message
        await Task.CompletedTask;
    }
}