using Amazon.S3.Model;
using Amazon.S3;
using Amazon.Runtime;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Tag = Amazon.S3.Model.Tag;

/*
 * Sean Fite
 * Hybrid Cloud Program
 * This project creates an S3 bucket file uploading exe, terminal call must include filepath and ticket info
 * Last Updated 6/16/23
 */

namespace UploadDataConsoleApp
{
    class Program
    {
        public static string bucketName = "project-003";       // s3 bucket name

        public static async Task Main(string[] args)
        {
            if (args.Length < 1)                                    // if exe is called without file path and tag
            {
                Console.WriteLine("Command input error, please edit and try again");
                return;
            }
            string filePath = args[0];                                        // split terminal call into filePath and tag
            int position = filePath.LastIndexOf("\\");                        // parse filename from filePath
            string key = filePath.Substring(position + 1);
            string tag = "";        
            for(int i = 1; i < args.Length; i++)
            {
                tag += " " + args[i];
            }
            AWSCredentials credentials = GetAWSCredentialsByName("default");                    // retrieve aws credentials
            AmazonS3Client s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);  // create aws instance
            await UploadFileToS3(s3Client, filePath, bucketName, key, tag);                     // call method to upload file to s3 bucket
            s3Client.Dispose();
        }

        static AWSCredentials GetAWSCredentialsByName(string profileName)               // retrieve aws credentials
        {
            if (String.IsNullOrEmpty(profileName))
            {
                throw new ArgumentException("profileName cannot be null or empty");
            }
            SharedCredentialsFile credFile = new SharedCredentialsFile();
            CredentialProfile profile = credFile.ListProfiles().Find(p => p.Name.Equals(profileName));
            if (profile == null)
            {
                throw new Exception(String.Format("Profile name {0} not found", profileName));
            }
            return AWSCredentialsFactory.GetAWSCredentials(profile, new SharedCredentialsFile());
        }
        // method to upload file to s3
        // passing in terminal command objects 
        static async Task UploadFileToS3(AmazonS3Client s3Client, string filePath, string bucketName, string key, string tag)
        {
            try
            {
               await s3Client.PutObjectAsync(new PutObjectRequest      // send request to upload file to s3 bucket
                //var putRequst = new PutObjectRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                    FilePath = filePath,
                    TagSet = new List<Tag>
                    {
                         new Tag { Key = tag, Value = "" },                 
                    }
               });

                Console.WriteLine($"File uploaded: {key} to bucket: {bucketName}");
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine($"Amazon S3 Error: {e.Message}");
                Console.WriteLine($"Error Code: {e.ErrorCode}");
                Console.WriteLine($"Request ID: {e.RequestId}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error uploading file: {e.Message}");
                Console.WriteLine(e.Message);
            }
        }
    }
}
