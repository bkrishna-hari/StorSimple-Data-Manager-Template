#load "QueueData.cs"

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Threading;

private static CloudMediaContext cloudMediaContext = null;

public static void Run(QueueItem myQueueItem, TraceWriter log)
{
    string assetId = myQueueItem.TargetLocation.Substring(myQueueItem.TargetLocation.LastIndexOf("/") + 1).Replace("asset-", "nb:cid:UUID:");

    // Read Asset details & initiate Media Analytics
    ReadMediaAssetAndRunEncoding(assetId, log);
}

public static void ReadMediaAssetAndRunEncoding(string assetId, TraceWriter log)
{
    string keyIdentifier = ConfigurationManager.AppSettings["MEDIA_ACCOUNT_NAME"];
    string keyValue = ConfigurationManager.AppSettings["MEDIA_ACCOUNT_KEY"];

    MediaServicesCredentials _cachedCredentials = new MediaServicesCredentials(keyIdentifier, keyValue);
    cloudMediaContext = new CloudMediaContext(_cachedCredentials);

    var assetInstance = from a in cloudMediaContext.Assets where a.Id == assetId select a;
    IAsset asset = assetInstance.FirstOrDefault();

    log.Info($"Asset {asset}");
    log.Info($"Asset Id: {asset.Id}");
    log.Info($"Asset name: {asset.Name}");
    log.Info($"Asset files: ");

    foreach (IAssetFile fileItem in asset.AssetFiles)
    {
        log.Info($"    Name: {fileItem.Name}");
        log.Info($"    Size: {fileItem.ContentFileSize}");
    }

    //submit job
    RunIndexingJob(asset, log);

    log.Info($"Encoding launched - function done");
}

public static bool RunIndexingJob(IAsset asset, TraceWriter log, string configurationFile = "")
{
    // Declare a new job.
    var jobName = string.Concat("Media Indexing of ", asset.Name);
    IJob job = cloudMediaContext.Jobs.Create(jobName);

    // Get a reference to the Azure Media Indexer.
    string MediaProcessorName = "Azure Media Indexer";
    IMediaProcessor processor = GetLatestMediaProcessorByName(MediaProcessorName);

    // Read configuration from file if specified.
    string configuration = string.IsNullOrEmpty(configurationFile) ? "" : File.ReadAllText(configurationFile);

    // Create a task with the encoding details, using a string preset.
    ITask task = job.Tasks.AddNew(jobName, processor, configuration, TaskOptions.None);

    // Specify the input asset to be indexed.
    task.InputAssets.Add(asset);

    // Add an output asset to contain the results of the job.
    task.OutputAssets.AddNew(string.Format("{0} - Indexed", asset.Name), AssetCreationOptions.None);

    // Launch the job.
    job.Submit();

    //// Check job execution and wait for job to finish.
    //Task progressJobTask = job.GetExecutionProgressTask(CancellationToken.None);

    //progressJobTask.Wait();
    log.Info($"Media Indexer submitted (Job name: {jobName})");

    return true;
}

public static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
{
    var processor = cloudMediaContext.MediaProcessors
                 .Where(p => p.Name == mediaProcessorName)
                 .ToList()
                 .OrderBy(p => new Version(p.Version))
                 .LastOrDefault();

    if (processor == null)
        throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

    return processor;
}