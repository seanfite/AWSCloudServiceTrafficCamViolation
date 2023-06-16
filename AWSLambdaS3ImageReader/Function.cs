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

/*
 * Sean Fite
 * Hybrid Cloud Program
 * This project is a Lambda S3 trigger, processing an S3 bucket image and pushing the contents into an SQS queue
 * Last Updated 6/16/23
 */

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaS3ImageReader;

public class Function
{
    // get set functionality object instances
    IAmazonS3 S3Client { get; set; }
    IAmazonRekognition AmazonRekognition { get; set; }

    IAmazonSQS SQSClient { get; set; }

    // initialize instances
    public Function()
    {
        S3Client = new AmazonS3Client();
        AmazonRekognition = new AmazonRekognitionClient();
        SQSClient = new AmazonSQSClient();
    }

    // assign instance objects
    public Function(IAmazonS3 s3Client, IAmazonRekognition amazonRekognition, IAmazonSQS sqsClient)
    {
        this.S3Client = s3Client;
        this.AmazonRekognition= amazonRekognition; 
        this.SQSClient = sqsClient;
        
    }

    // this function processes S3 image and sends results to SQS queue
    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        // find event records
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
                // new instance for tagging request
                GetObjectTaggingRequest getTagsRequest = new GetObjectTaggingRequest           
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key,

                };
                GetObjectTaggingResponse objectTags = await this.S3Client.GetObjectTaggingAsync(getTagsRequest);
                string tags = objectTags.GetType().Name;
                // get tag from s3 object
                string tag = objectTags.Tagging.Count > 0 ? objectTags.Tagging[0].Key : string.Empty;  
                // grab text from image
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
                // format image text for processing ease in queue trigger function
                string licensePlateString = string.Join("-", detectedTexts);
                Console.WriteLine(licensePlateString);              
                // if license plate is not from California, send to event bus
                if(!licensePlateString.Contains("California"))
                {
                    // event bus message
                    var eventDetail = new
                    {                      
                        Message = "This plate is not from California",
                        UtcTime = DateTime.UtcNow.ToString("g")
                    };
                    // sending to event bus
                    var client = new AmazonEventBridgeClient(); 
                    try
                    {
                        await client.PutEventsAsync(new PutEventsRequest
                        {
                            Entries = new List<PutEventsRequestEntry>()
                            {
                                new PutEventsRequestEntry
                                {
                                    Source = "Lambda Project 3",
                                    Detail = JsonSerializer.Serialize(eventDetail),
                                    DetailType = "Return Message",
                                    EventBusName = "default"
                                }
                            }
                        });
                        Console.WriteLine("Message sent to EventBridge");                      
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send message to EventBridge: {ex.Message}");
                    }
                    return;
                }
                // pull out license number from image string
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
                // send message back into SQS queue
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
            // if we are unable to get s3bucket
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