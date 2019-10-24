using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace StorageAccountSync
{
    public static class StorageAccountExtensions
    {
        /// <summary>
        /// Gets the list of the contains 
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="prefix">A string containing the container name prefix.</param>
        /// <returns>List of container names in the storage account</returns>
        public static async Task<List<string>> ListContainersAsync(this CloudStorageAccount storageAccount, string prefix = null)
        {
            if (storageAccount == null) throw new ArgumentNullException(nameof(storageAccount));

            // -----------------------------------------------------------------
            // Get list of all the blob containers in this storage account
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            List<string> containers = new List<string>();
            BlobContinuationToken continuationToken = null;

            do
            {
                var segment = await blobClient.ListContainersSegmentedAsync(prefix, continuationToken);
                continuationToken = segment.ContinuationToken;
            
                foreach (var result in segment.Results)
                {
                    containers.Add(result.Name);
                }
            }
            while (continuationToken != null);

            return containers;
        }
    }
}
