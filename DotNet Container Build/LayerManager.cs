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
using System.Runtime.InteropServices;

namespace DotNet_Container_Build
{
    public class LayerManager
    {
        public int MaxParallel { get; set; } = 4;

        private AzureContainerRegistryClient _client;
        private string _repository;
        private string _tag;

        public LayerManager(ImageRef cur) {
            Microsoft.Rest.ServiceClientCredentials clientCredentials;
            if (cur.Username == null)
            {
                clientCredentials = new TokenCredentials();
            }
            else {
                clientCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, cur.Registry, cur.Username, cur.Password);
            }
            _client = new AzureContainerRegistryClient(clientCredentials)
            {
                LoginUri = "https://" + cur.Registry
            };
            _repository = cur.Repository;
            _tag = cur.Tag;
        }

        /// <summary>
        /// Upload a layer using the nextLink properties internally. Very clean and simple to use overall.
        /// <paramref name="blob"/> Stream containing the blob to be uploaded
        /// the registry. If none is specified the internal default will be used. Included for flexibility.
        /// </summary>
        private async Task<string> UploadLayer(Stream blob)
        {
            // Make copy to obtain the ability to rewind the stream
            Stream cpy = new MemoryStream();
            blob.CopyTo(cpy);
            cpy.Position = 0;

            string digest = ComputeDigest(cpy);
            cpy.Position = 0;

            var uploadInfo = await _client.StartEmptyResumableBlobUploadAsync(_repository);
            var uploadedLayer = await _client.UploadBlobContentFromNextAsync(cpy, uploadInfo.Location.Substring(1));
            var uploadedLayerEnd = await _client.EndBlobUploadFromNextAsync(digest, uploadedLayer.Location.Substring(1));
            return uploadedLayerEnd.DockerContentDigest;
        }

        /// <summary>
        /// Upload a layer using the nextLink properties internally. Very clean and simple to use overall.
        /// <paramref name="output"/> Layer manager for output repository. Will be used as the upload location
        /// of the copied output.
        /// <paramref name="includeConfig"/> Describes if the config blob should be copuied along with the rest
        /// of the layers.
        /// <paramref name="consoleOutput"/> True indicates progress should be printed to console in the form of
        /// progress bars.
        /// </summary>
        public async Task CopyLayersTo(LayerManager output, bool includeConfig, bool consoleOutput)
        {
            V2Manifest manifest = (V2Manifest)await _client.GetManifestAsync(_repository, _tag, "application/vnd.docker.distribution.manifest.v2+json");
            var listOfActions = new List<Action>();

            // Acquire and upload all layers
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var cur = i;
                listOfActions.Add(() =>
                {
                    DownloadAndUpload(manifest.Layers[cur].Digest, output, consoleOutput);
                });
            }

            if (includeConfig)
            {
                // Acquire config Blob
                listOfActions.Add(() =>
                {
                    DownloadAndUpload(manifest.Config.Digest, output, consoleOutput);
                });
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel };
            Parallel.Invoke(options, listOfActions.ToArray());
        }

        /// <summary>
        /// Downloads a layer from the local repository and uploads it to the output layer Manager's repo.
        /// <paramref name="output"/> Layer manager for output repository. Will be used as the upload location
        /// of the copied output.
        /// <paramref name="includeConfig"/> Describes if the config blob should be copuied along with the rest
        /// of the layers.
        /// <paramref name="consoleOutput"/> True indicates progress should be printed to console in the form of
        /// progress bars.
        /// </summary>
        private void DownloadAndUpload(string digest, LayerManager output , bool consoleOutput)
        {
            var progress = new ProgressBar(2);

            progress.Refresh(0, "Downloading " + digest + " layer from " + _repository);
            var layer = _client.GetBlobAsync(_repository, digest).GetAwaiter().GetResult();

            progress.Next("Uploading " + digest + " layer to " + output._repository);
            string digestLayer = output.UploadLayer(layer).GetAwaiter().GetResult();

            progress.Next("Uploaded " + digestLayer + " layer to " + output._repository);
        }

        private static string ComputeDigest(Stream s)
        {
            s.Position = 0;
            StringBuilder sb = new StringBuilder();

            using (var hash = SHA256.Create())
            {
                byte[] result = hash.ComputeHash(s);

                foreach (byte b in result)
                    sb.Append(b.ToString("x2"));
            }

            return "sha256:" + sb.ToString();

        }

        private static string ComputeDigest(string s)
        {
            StringBuilder sb = new StringBuilder();

            using (var hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                byte[] result = hash.ComputeHash(enc.GetBytes(s));

                foreach (byte b in result)
                    sb.Append(b.ToString("x2"));
            }

            return "sha256:" + sb.ToString();

        }

        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }


    }
}


