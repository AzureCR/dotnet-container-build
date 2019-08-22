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

namespace DotNet_Container_Build
{
    /// <summary>
    /// Sample application demonstrating the ability to download all layers for an image and use them to 
    /// create a new Repository.
    /// </summary>
    class Program
    {
        const string Username = "csharpsdkblobtest";
        static string Password = Environment.GetEnvironmentVariable("TEST_PASSWORD");
        const string Registry = "csharpsdkblobtest.azurecr.io";
        const string RepoOrigin = "jenkins";
        const string RepoOutput = "jenkins9";
        const string OutputTag = "latest";
        const int MaxParallel = 4;

        struct ImageRef
        {
            public string Registry { get; set; }
            public string Repository { get; set; }
            public string Tag { get; set; }
            public string Password { get; set; }
            public string Username { get; set; }
        }

        static void Main()
        {
            int timeoutInMilliseconds = 1500000;
            CancellationToken ct = new CancellationTokenSource(timeoutInMilliseconds).Token;
            AzureContainerRegistryClient client = LoginBasic(ct);
            var output = new ImageRef()
            {
                Registry = Registry,
                Password = Password,
                Repository = "idk",
                Tag = "Latest"
            };
            BuildDotNetImage(".",output).GetAwaiter().GetResult();
            //BuildImageInRepoAfterDownload(RepoOrigin, RepoOutput, OutputTag, client, ct).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Example Credentials provisioning (Using Basic Authentication)
        /// </summary>
        private static AzureContainerRegistryClient LoginBasic(CancellationToken ct)
        {
            AcrClientCredentials credentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, Registry, Username, Password, ct);
            AzureContainerRegistryClient client = new AzureContainerRegistryClient(credentials)
            {
                LoginUri = "https://csharpsdkblobtest.azurecr.io"
            };
            return client;
        }

        /// <summary>
        /// Uploads a specified image layer by layer from another local repository (Within the specific registry)
        /// </summary>
        private static async Task BuildImageInRepoAfterDownload(string origin, string output, string outputTag, AzureContainerRegistryClient client, CancellationToken ct)
        {
            V2Manifest manifest = (V2Manifest)await client.GetManifestAsync(origin, outputTag, "application/vnd.docker.distribution.manifest.v2+json", ct);

            var listOfActions = new List<Action>();

            // Acquire and upload all layers
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var cur = i;
                listOfActions.Add(() =>
                {
                    var progress = new ProgressBar(3);
                    progress.Refresh(0, "Starting");
                    var layer = client.GetBlobAsync(origin, manifest.Layers[cur].Digest).GetAwaiter().GetResult();
                    progress.Next("Downloading " + manifest.Layers[cur].Digest + " layer from " + origin);
                    string digestLayer = UploadLayer(layer, output, client).GetAwaiter().GetResult();
                    progress.Next("Uploading " + manifest.Layers[cur].Digest + " layer to " + output);
                    manifest.Layers[cur].Digest = digestLayer;
                    progress.Next("Uploaded " + manifest.Layers[cur].Digest + " layer to " + output);
                });
            }

            // Acquire config Blob
            listOfActions.Add(() =>
            {
                var progress = new ProgressBar(3);
                progress.Next("Downloading config blob from " + origin);
                var configBlob = client.GetBlobAsync(origin, manifest.Config.Digest).GetAwaiter().GetResult();
                progress.Next("Uploading config blob to " + output);
                string digestConfig = UploadLayer(configBlob, output, client).GetAwaiter().GetResult();
                progress.Next("Uploaded config blob to " + output);
                manifest.Config.Digest = digestConfig;
            });

            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel };
            Parallel.Invoke(options, listOfActions.ToArray());

            Console.WriteLine("Pushing new manifest to " + output + ":" + outputTag);
            await client.CreateManifestAsync(output, outputTag, manifest, ct);

            Console.WriteLine("Successfully created " + output + ":" + outputTag);
        }

        private static async Task CopyBaseImageLayers(ImageRef origin, ImageRef output, bool includeConfig)
        {
            AzureContainerRegistryClient originClient;
            if (string.IsNullOrEmpty(origin.Username))
            {
                var originCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.TokenAuth, origin.Registry, origin.Username, origin.Password);
                originClient = new AzureContainerRegistryClient(originCredentials)
                {
                    LoginUri = origin.Registry
                };
            }
            else {
                originClient = new AzureContainerRegistryClient(new TokenCredentials())
                {
                    LoginUri = "https://" + origin.Registry
                };
            }
 

            var outputCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.TokenAuth, output.Registry, output.Username, output.Password);
            var outputClient = new AzureContainerRegistryClient(outputCredentials) {
                LoginUri = "https://" + output.Registry
            };

            V2Manifest manifest = (V2Manifest) await outputClient.GetManifestAsync(origin.Repository, origin.Tag, "application/vnd.docker.distribution.manifest.v2+json");
            var listOfActions = new List<Action>();

            // Acquire and upload all layers
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var cur = i;
                listOfActions.Add(() =>
                {
                    var progress = new ProgressBar(3);
                    progress.Refresh(0, "Starting");
                    var layer = originClient.GetBlobAsync(origin.Repository, manifest.Layers[cur].Digest).GetAwaiter().GetResult();
                    progress.Next("Downloading " + manifest.Layers[cur].Digest + " layer from " + origin);
                    string digestLayer = UploadLayer(layer, output.Repository, outputClient).GetAwaiter().GetResult();
                    progress.Next("Uploading " + manifest.Layers[cur].Digest + " layer to " + output.Repository);
                    manifest.Layers[cur].Digest = digestLayer;
                    progress.Next("Uploaded " + manifest.Layers[cur].Digest + " layer to " + output.Repository);
                });
            }

            if (includeConfig) {
                // Acquire config Blob
                listOfActions.Add(() =>
                {
                    var progress = new ProgressBar(3);
                    progress.Next("Downloading config blob from " + origin.Repository);
                    var configBlob = originClient.GetBlobAsync(origin.Repository, manifest.Config.Digest).GetAwaiter().GetResult();
                    progress.Next("Uploading config blob to " + output.Repository);
                    string digestConfig = UploadLayer(configBlob, output.Repository, outputClient).GetAwaiter().GetResult();
                    progress.Next("Uploaded config blob to " + output);
                    manifest.Config.Digest = digestConfig;
                });
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel };
            Parallel.Invoke(options, listOfActions.ToArray());

            Console.WriteLine("Pushing new manifest to " + output + ":" + output.Tag);
            await outputClient.CreateManifestAsync(output.Repository, output.Tag, manifest);

            Console.WriteLine("Successfully created " + output + ":" + output.Tag);
        }

        private static async Task BuildDotNetImage (string fileOrigin, ImageRef outputRepo)
        {

            // 1. Upload the .Net files to the specified repository
            var oras = new OrasPush() {
                OrasExe = "C:/ProgramData/Fish/Barrel/oras/0.6.0/oras.exe",
                Registry = outputRepo.Registry,
                Tag = outputRepo.Tag,
                Repository = outputRepo.Repository,
                PublishDir = fileOrigin
            };

            if (!oras.Execute())
                throw new Exception("Could not upload " + fileOrigin);

            var clientCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.TokenAuth, outputRepo.Registry, outputRepo.Username, outputRepo.Password);
            var client = new AzureContainerRegistryClient(clientCredentials)
            {
                LoginUri = "https://" + outputRepo.Registry
            };

            // 2. Acquire the resulting OCI manifest
            string orasDigest = oras.digest;
            OCIManifest manifest = (OCIManifest)await client.GetManifestAsync(outputRepo.Repository, orasDigest, "application/vnd.oci.image.manifest.v1+json");
            long app_size = (long)manifest.Layers[0].Size;
            string app_diff_id = (string) manifest.Annotations.AdditionalProperties["Digest"];
            string app_digest = manifest.Layers[0].Digest;

            // 3. Acquire base for .Net image

            var baseLayers = new ImageRef(){
                Registry = "https://mcr.microsoft.com"
            };

            var dotnetVersion = "2.2";
            switch (dotnetVersion)
            {
                case "2.1":
                    baseLayers.Repository = "dotnet/core/runtime";
                    baseLayers.Tag = "2.1";
                    break;
                case "2.2":
                    baseLayers.Repository = "dotnet/core/runtime";
                    baseLayers.Tag = "2.2";
                    break;
                case "3.0":
                    baseLayers.Repository = "dotnet/core-nightly/runtime";
                    baseLayers.Tag = "3.0";
                    break;
                default:
                    baseLayers.Repository = "dotnet/core-nightly/runtime-deps";
                    baseLayers.Tag = "latest";
                    break;
            }

            // 4. Move base layers to repo
            await CopyBaseImageLayers(baseLayers, outputRepo, false);

            // 5. Acquire config blob from base
            var baseClient = new AzureContainerRegistryClient(new TokenCredentials())
            {
                LoginUri = "https://" + baseLayers.Registry
            };

            V2Manifest baseManifest = (V2Manifest) await baseClient.GetManifestAsync(baseLayers.Repository, baseLayers.Tag, "application/vnd.docker.distribution.manifest.v2+json");
            var configBlob = await baseClient.GetBlobAsync(baseLayers.Repository, manifest.Config.Digest);

            using (StreamReader reader = new StreamReader(configBlob, Encoding.UTF8))
            {
                dynamic configJson = JsonConvert.DeserializeObject(reader.ReadToEnd());
                // 6. Add layer to config blob 

            }

            // 7. Modify manifest file for the new layer

            // 8. Upload config blob

            // 9. Push new manifest

            // Image can now be run!
        }

        /// <summary>
        /// Upload a layer using the nextLink properties internally. Very clean and simple to use overall.
        /// </summary>
        private static async Task<string> UploadLayer(Stream blob, string repo, AzureContainerRegistryClient client)
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

        struct BlobData
        {
            public bool noUpload;
            public string state;
        }

        /// <summary>
        /// Computes a digest for a particular layer from its stream
        /// </summary>
        private static string ComputeDigest(Stream s)
        {
            s.Position = 0;
            StringBuilder sb = new StringBuilder();

            using (var hash = SHA256.Create())
            {
                Encoding enc = Encoding.Unicode;
                Byte[] result = hash.ComputeHash(s);

                foreach (Byte b in result)
                    sb.Append(b.ToString("x2"));
            }

            return "sha256:" + sb.ToString();

        }

    }
}
