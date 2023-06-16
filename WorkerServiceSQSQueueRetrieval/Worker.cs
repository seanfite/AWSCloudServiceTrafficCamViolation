using System.Xml.Linq;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

/*
 * Sean Fite
 * Hybrid Cloud Program
 * This project is a Worker Service, waiting for SQS queue messages, when one is recieved, a local DMV database is parsed for vehicle
 * info matching that of the queue message, a resulting upward queue message is sent with ticket info
 * Last Updated 6/16/23
 */

namespace WorkerServiceSQSQueueRetrieval
{
    public class Worker : BackgroundService
    {
        private const string loggerPath = "C:\\Users\\squir\\OneDrive\\Desktop\\Temp.txt";
        private string recieptHandler;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
            string xmlFilePath = "C:\\Users\\squir\\OneDrive\\Desktop\\DMVDatabase.xml";
            string myAccessKey = "ASIA56JY2SKBSWNAQTX4";
            string mySecretKey = "YpMN8PJeUcW0TMUGL6xAiQrLPbBHpOZ6usUPDWdE";
            string sessionToken = "FwoGZXIvYXdzEAIaDEoTG65NcgMQdRgDByLOAWhY2G4oRI8tAeKEGhg5Ae3fxV340ZpKEBdijSYuvS1q22dsZoYtzrULezt1yfqYNRLL+s62bjtL+7CKj/giVolYmtQaJ7t9bCkG5W/+ue8Pr1Gai5QR5GR1FMzuQkIdsAbonYVg5v2R0lJ9Z8nElfJQD8753eZWTb/y6foMxQuPTkXOiZFDzj9bUAveCNgJrgqwX5ne9J66+m+vL0p/tvuS97p4+99NYgYOPqntV75HkdL1iYdGO/vMA5vkZVG9sqVZGAIIAROkJMBVG/+rKLmnsqQGMi1PfDrWjXMdo0+Y+raAJuo74muCfJr2iXaeYwpZBdD8QFRa12UnNZ53j7HalQ8=";
            // get AWS credentials
            SessionAWSCredentials credentials = new SessionAWSCredentials(myAccessKey, mySecretKey, sessionToken);
            AmazonSQSConfig sqsConfig = new AmazonSQSConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.USEast1
            };
            // initialize object of SQS 
            AmazonSQSClient sQSClient = new AmazonSQSClient(credentials, sqsConfig);
            while (!cancellationToken.IsCancellationRequested)
            {
                ReceiveMessageRequest receiveMessageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/958433759875/Project3",
                    MaxNumberOfMessages = 1,
                    // wait time set to enable long polling
                    WaitTimeSeconds = 20
                };
                // recieve messages from sqs queue
                ReceiveMessageResponse receiveMessageResponse = await sQSClient.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);
                if (receiveMessageResponse.Messages.Count > 0)
                {
                    string vehicleXml;
                    string messageBody;
                    foreach (var message in receiveMessageResponse.Messages)
                    {
                        recieptHandler = message.ReceiptHandle;
                        messageBody = message.Body;
                        WriteLog("Read message: " + messageBody);
                        int startIndex = messageBody.IndexOf('"');
                        string licenseNum = messageBody.Substring(startIndex + 1, startIndex + 7);
                        WriteLog("LicenseNum: " + licenseNum);

                        XDocument document = XDocument.Load(xmlFilePath);
                        XElement matchingVehicle = document.Root.Elements("vehicle").FirstOrDefault(vehicle => vehicle.Attribute("plate")?.Value == licenseNum);
                        if (matchingVehicle != null)
                        {
                            vehicleXml = matchingVehicle.ToString();
                            WriteLog("Matching Vehicle: " + vehicleXml);
                            // sending message back to SQS queue
                            SendMessageRequest sendMessageRequest = new SendMessageRequest
                            {

                                QueueUrl = "https://sqs.us-east-1.amazonaws.com/958433759875/project-3-return",
                                MessageBody = messageBody + "|" + vehicleXml,
                            };
                            await sQSClient.SendMessageAsync(sendMessageRequest, cancellationToken);
                        }
                    }
                    
                    // delete message received from queue after read
                    DeleteMessageRequest deleteMessageRequest = new DeleteMessageRequest
                    {
                        QueueUrl = "https://sqs.us-east-1.amazonaws.com/958433759875/Project3",
                        ReceiptHandle = recieptHandler
                    };
                    await sQSClient.DeleteMessageAsync(deleteMessageRequest, cancellationToken);
                }
            }
        }
        public void WriteLog(string message)
        {
            string text = string.Format("{0}:\t{1}", DateTime.Now, message, Environment.NewLine);
            using (StreamWriter writer = new StreamWriter(loggerPath, append: true))
            {
                writer.WriteLine(text);
            }
        }
    }
}