using System.Xml.Linq;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

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
            string myAccessKey = "ASIA56JY2SKBYGVLE7X6";
            string mySecretKey = "jjFRoTTOkO9agWHqxgzibFkQfp6HQ82puEBJqMLi";
            string sessionToken = "FwoGZXIvYXdzEPX//////////wEaDNfsFlIi9PTODmKHpiLOAQLI+OFYoQKTJNu8b4IMlSmJylBua8X2XXMObiYSLAxGCobeKQLgGmjPwq+Ibq3aVxGc88Yfy8GoWgC0tTPlOQsSWNd/zKCDDtV5YzDx7UF8w0aPVY8+whl2uwy5EG0doKZOzYolUDGunbLRwqD5kkPveQRM1/Gdia4bF1PB72K4Fbk+m5u9KZa8wY+TnM3Y+cLhnqdyk2wyD8u+74etjtqcAfztom+iThYxHUVrvBBUicvTMfm+4sz8lOmVRATtsnDgbQ1zeGKEudtycNExKNy4r6QGMi1hEo1JmRdcaxugr+YQKP0PHFW0IDyPHaCijcKuxIOSTmYL5ZhX0tY+Mtrh2RA=";
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