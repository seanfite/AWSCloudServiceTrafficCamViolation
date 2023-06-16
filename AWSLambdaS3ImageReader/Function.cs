using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaS3ImageReader;

public class Function
{
    IAmazonS3 S3Client { get; set; }
    IAmazonRekognition AmazonRekognition { get; set; }

    IAmazonSQS SQSClient { get; set; }

    public Function()
    {
        S3Client = new AmazonS3Client();
        AmazonRekognition = new AmazonRekognitionClient();
        SQSClient = new AmazonSQSClient();
    }

    public Function(IAmazonS3 s3Client, IAmazonRekognition amazonRekognition, IAmazonSQS sqsClient)
    {
        this.S3Client = s3Client;
        this.AmazonRekognition= amazonRekognition; 
        this.SQSClient = sqsClient;
        
    }

    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }

            try
            {
                var response = await this.S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);

                GetObjectTaggingRequest getTagsRequest = new GetObjectTaggingRequest            // new instance for tagging request
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key,

                };
                GetObjectTaggingResponse objectTags = await this.S3Client.GetObjectTaggingAsync(getTagsRequest);
                string tags = objectTags.GetType().Name;
                string tag = objectTags.Tagging.Count > 0 ? objectTags.Tagging[0].Key : string.Empty;  // get tag from s3 object

                var detectTextRequest = new DetectTextRequest
                {
                    Image = new Image { S3Object = new Amazon.Rekognition.Model.S3Object { Bucket = s3Event.Bucket.Name, Name = s3Event.Object.Key } }
                };

                var rekognitionClient = new AmazonRekognitionClient();
                var detectTextResponse = await rekognitionClient.DetectTextAsync(detectTextRequest);
                var detectedTexts = new List<string>();

                // Process the detected text
                foreach (var textDetection in detectTextResponse.TextDetections)
                {
                    string detectedText = textDetection.DetectedText;
                    if (!detectedTexts.Contains(detectedText))
                    {
                        detectedTexts.Add(detectedText);
                    }
                }

                string licensePlateString = string.Join("-", detectedTexts);

                Console.WriteLine(licensePlateString);              

                if(!licensePlateString.Contains("California"))
                {
                    Console.WriteLine("We are inside the start of the if statement");
                    var eventDetail = new
                    {                      
                        Message = "This plate is not from California",
                        UtcTime = DateTime.UtcNow.ToString("g")
                    };
                    AmazonEventBridgeClient _amazonEventBridge = new AmazonEventBridgeClient();
                    var response3 = await _amazonEventBridge.PutEventsAsync(new PutEventsRequest()
                    {
                        Entries = new List<PutEventsRequestEntry>()
                        {
                            new PutEventsRequestEntry()
                            {
                                Source = "Lambda Project 3",
                                Detail = JsonSerializer.Serialize(eventDetail),
                                DetailType = "Return Message",
                                EventBusName = "arn:aws:events:us-east-1:958433759875:event-bus/default"
                            }
                        }
                    });

                    // Handle the response
                    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine("Message sent to EventBridge successfully");
                    }
                    else
                    {
                        Console.WriteLine("Failed to send message to EventBridge");
                    }
                }

                var count = 0;
                var licenseNumString = "\"";
                for(int i = 0; i < licensePlateString.Length; i++)
                {
                    if (licensePlateString[i] == '-')
                    {
                        count++;
                    }
                    if(count == 4 && licensePlateString[i] != '-')
                    {
                        licenseNumString += licensePlateString[i];
                    }
                    if(count == 5)
                    {
                        licenseNumString += "\"";
                        break;
                    }
                }
                Console.WriteLine(licenseNumString);
                Console.WriteLine(tag);
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/958433759875/Project3",
                    MessageBody = licenseNumString + tag,
                };
                try
                {
                    var response2 = await SQSClient.SendMessageAsync(sendMessageRequest);
                    Console.WriteLine($"Message sent successfully. Message ID: {response2.MessageId}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error sending message to queue. {e.Message}");
                }

            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogError(e.Message);
                context.Logger.LogError(e.StackTrace);
                throw;
            }
        }
    }
}