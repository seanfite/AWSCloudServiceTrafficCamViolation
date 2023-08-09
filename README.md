# TrafficCamViolationCloudService

This is a hybrid cloud solution using AWS cloud services. This program includes an executable that uploads a license plate photo to an AWS S3 bucket. 
A Lambda function is triggered by the S3 bucket upload and parses the license plate data. Assesses if it is from California or not. If it is not, 
the parsed data is sent to an AWS Event Bus for later processing. If it is a California plate, the licenese plate number is sent via a downward queue to a 
Worker Service that compares it with a local database to validate and if matched, retrieve driver info (name, address, email).
This data is sent via an Upward Queue by the Worker Service back to AWS services. A Lambda function is triggered by the Upward Queue and emails this data 
along with ticket information (price, location, time) as well as confirmation to an AWS Event Bus.
