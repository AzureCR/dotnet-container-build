using System;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using Konsole;
using System.Collections.Generic;
using MSBuildTasks;
using Microsoft.Azure.ContainerRegistry;
using Microsoft.Azure.ContainerRegistry.Models;
using Newtonsoft.Json;
using QuickType;
using System.Text.Json;
using System.Configuration;

namespace DotNet_Container_Build
{
    public class LayerManager
    {
        private AzureContainerRegistryClient _client;
        private string _repository;
        private string _tag;

        public LayerManager(ImageRef cur) {
            ServiceClient clientCredentials;
            if (cur.Username == null)
            {
                clientCredentials = new TokenCredentials();
            }
            else {
                clientCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, cur.Registry, cur.Username, cur.Password);
            }
            var client = new AzureContainerRegistryClient(clientCredentials)
            {
                LoginUri = "https://" + cur.Registry
            };
        }

        /// <summary>
        /// Upload a layer using the nextLink properties internally. Very clean and simple to use overall.
        /// </summary>
        private async Task<string> UploadLayer(Stream blob, string repository = default(_repository), string tag = default(_tag))
        {
            // Make copy to obtain the ability to rewind the stream
            Stream cpy = new MemoryStream();
            blob.CopyTo(cpy);
            cpy.Position = 0;

            string digest = ComputeDigest(cpy);
            cpy.Position = 0;

            var uploadInfo = await client.StartEmptyResumableBlobUploadAsync(repo);
            var uploadedLayer = await client.UploadBlobContentFromNextAsync(cpy, uploadInfo.Location.Substring(1));
            var uploadedLayerEnd = await client.EndBlobUploadFromNextAsync(digest, uploadedLayer.Location.Substring(1));
            return digest;
        }

        private async Task CopyLayersTo(LayerManager output, bool includeConfig, bool consoleOutput)
        {
            var outputCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, output.Registry, output.Username, output.Password);

            V2Manifest manifest = (V2Manifest)await _client.GetManifestAsync(origin.Repository, origin.Tag, "application/vnd.docker.distribution.manifest.v2+json");
            var listOfActions = new List<Action>();

            // Acquire and upload all layers
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var cur = i;
                listOfActions.Add(() =>
                {
                    DownloadAndUpload(manifest.Layers[cur].Digest, output._repository, consoleOutput);
                });
            }

            if (includeConfig)
            {
                // Acquire config Blob
                listOfActions.Add(() =>
                {
                    DownloadAndUpload(manifest.Layers[cur].Digest, output._repository, consoleOutput);
                });
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel };
            Parallel.Invoke(options, listOfActions.ToArray());
        }

        private void DownloadAndUpload(string digest, string origin, bool consoleOutput)
        {
            var progress = new ProgressBar(2);

            progress.Refresh(0, "Downloading " + digest + " layer from " + origin);
            var layer = _client.GetBlobAsync(_repository, digest).GetAwaiter().GetResult();

            progress.Next("Uploading " + digest + " layer to " + _repository);
            string digestLayer = UploadLayer(layer, _repository).GetAwaiter().GetResult();

            progress.Next("Uploaded " + digest + " layer to " + _repository);
        }

    }
}


