//------------------------------------------------------------------------------
//MIT License

//Copyright(c) 2017 Microsoft Corporation. All rights reserved.

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor; 
using System.Text;
using System.Threading;

namespace Storage.Blob.Dotnet.Quickstart.V12
{
    /// <summary>
    /// Azure Storage QuickStart Sample - Demonstrate how to upload, list, download, and delete blobs. 
    ///
    /// Note: This sample uses the .NET asynchronous programming model to demonstrate how to call Blob storage using the 
    /// azure storage client library's asynchronous API's. When used in production applications, this approach enables you to improve the 
    /// responsiveness of your application.  
    ///
    /// </summary>

    public static class Program
    {
        private static string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=epsostorage;AccountKey=lN75nzpFQ0Hl3Fb38ckUL0qJDyntlgSu69XOkRIQsvHSvkaucuUAfAACjAYYlQOkO+UrKPNz25f8+AStfDR9LA==;EndpointSuffix=core.windows.net";
        private static string dataContainer = "contoso-data";
        private static string eventContainer = "contoso-event";

        private static string eventHubconnectString = "Endpoint=sb://contosoaitrainingevnt.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=yVWpCc232CCtLAVZpbwEfNGFllkwxMQ0Y+AEhMMTO7Q=";
        private static string eventHub = "aitrainingtrig";

        private static bool AiTraigDelivered = false; 

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Azure Blob Storage - Downloader");
            AiTraigDelivered = false;

            EventWaitHandle _manualResetEvent = new EventWaitHandle(false, EventResetMode.ManualReset);


            if (args.Length < 1)
            {
                Console.WriteLine("Usage: storage-blobs-dotnet-quickstart.exe [download path]");
                return;
            }

            if(!Directory.Exists(args[0]))
            {
                Console.WriteLine("Folder {0} doesn't exist.", args[0]);
                Console.WriteLine("Usage: storage-blobs-dotnet-quickstart.exe [download path]");
                return;
            }


            //Wait until AI training event triggered. 
            // Create a blob container client that the event processor will use 
            BlobContainerClient storageClient = new BlobContainerClient(storageConnectionString,eventContainer);

            // Create an event processor client to process events in the event hub
            var processor = new EventProcessorClient(
                storageClient,
                EventHubConsumerClient.DefaultConsumerGroupName,
                eventHubconnectString, eventHub);


            // Register handlers for processing events and handling errors
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;


            // Start the processing
            await  processor.StartProcessingAsync();

            // Wait for 30 seconds for the events to be processed
            //await Task.Delay(TimeSpan.FromDays(1));
            //Wait until AITrig event delivdered. 
            _manualResetEvent.WaitOne();


            Console.WriteLine("Wait 5 secs. Event Hub consumer processed....");
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Stop the processing to finish wait. 
            await processor.StopProcessingAsync();

            Task ProcessEventHandler(ProcessEventArgs eventArgs)
            {
                // Write the body of the event to the console window
                Console.WriteLine("Received event: {0}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));

                var EnqueuedTime = eventArgs.Data.EnqueuedTime.UtcDateTime;//.DateTime.ToUniversalTime();
                TimeSpan timeDiff = DateTime.UtcNow.Subtract(EnqueuedTime); 

                if( timeDiff.TotalSeconds > 20)
                {
                    Console.WriteLine("Skipping it's too aged event...");
                    return Task.CompletedTask;
                }

                if (Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()).Contains($"AItrig"))
                {
                    AiTraigDelivered = true;
                    Console.WriteLine("AI Retraining Event triggered");
                    _manualResetEvent.Set(); 
                }

                return Task.CompletedTask;

            }

            Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
            {
                // Write details about the error to the console window
                Console.WriteLine($"\tPartition '{eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
                Console.WriteLine(eventArgs.Exception.Message);
                Console.ReadLine();

                return Task.CompletedTask;
            }



            if (AiTraigDelivered == true)
            {
                //Copying Blob Storage. 
                Console.WriteLine("Started downloading from Blob storage.......");
                await OperateBlobAsync(args[0]);

            }
            else
            {
                Console.WriteLine("Skipped downloading from Blob storage.......");
            }

            Console.WriteLine("Completed downloading.......");
            //Environment.Exit(0);
        }

        private static async Task OperateBlobAsync(string downloadpath)
        {
            string tempDirectory = null;
            string destinationPath = null;
            string sourcePath = null;
            BlobContainerClient blobContainerClient = null;

            // Retrieve the connection string for use with the application. The storage connection string is stored
            // in an environment variable on the machine running the application called AZURE_STORAGE_CONNECTIONSTRING.
            // If the environment variable is created after the application is launched in a console or with Visual
            // Studio, the shell needs to be closed and reloaded to take the environment variable into account.
            //string storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTIONSTRING");

            //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=epsostorage;AccountKey=lN75nzpFQ0Hl3Fb38ckUL0qJDyntlgSu69XOkRIQsvHSvkaucuUAfAACjAYYlQOkO+UrKPNz25f8+AStfDR9LA==;EndpointSuffix=core.windows.net";

            if (storageConnectionString == null)
            {
                Console.WriteLine("A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'AZURE_STORAGE_CONNECTIONSTRING' with your storage " +
                    "connection string as a value.");

                return;
            }

            try
            {
                // Create a container called 'quickstartblob' and append a GUID value to it to make the name unique. 
                //string containerName = "quickstartblob" + Guid.NewGuid().ToString();
                //string containerName = "contoso-data";

                blobContainerClient = new BlobContainerClient(storageConnectionString, dataContainer);
                await blobContainerClient.CreateIfNotExistsAsync();

                Console.WriteLine($"Created container '{blobContainerClient.Uri}'");
                Console.WriteLine();

                // Set the permissions so the blobs are public. 
                await blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);
                Console.WriteLine("Setting the Blob access policy to public.");
                Console.WriteLine();

                // List the blobs in the container.
                Console.WriteLine("Listing blobs in container.");
                await foreach (BlobItem item in blobContainerClient.GetBlobsAsync())
                {
                    Console.WriteLine($"The blob name is '{item.Name}'");

                    //Save all files. 
                    BlobClient _blob = blobContainerClient.GetBlobClient(item.Name);

                    // Append the string "_DOWNLOADED" before the .txt extension so that you can see both files in the temp directory.
                    destinationPath = Path.Combine(downloadpath, item.Name).ToString();

                    // Download the blob to a file in same directory, using the reference created earlier. 
                    Console.WriteLine($"Downloading blob to file in the temp directory {destinationPath}");
                    BlobDownloadInfo blobDownload = await _blob.DownloadAsync();

                    using (FileStream fileStream = File.OpenWrite(destinationPath))
                    {
                        await blobDownload.Content.CopyToAsync(fileStream);
                    }

                    Console.WriteLine("{0} Downloaded successfully.", item.Name);

                    if (_blob.DeleteIfExists())
                    {
                        Console.WriteLine("{0} Deleted from Blob Storage successfully.", item.Name);
                    }
                    else
                    {
                        Console.WriteLine("{0} Deleted from Blob Storage failed.", item.Name);
                    }

                }
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Error returned from the service: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Download completed");
                //Environment.Exit(0);
            }
        }
    }
}
