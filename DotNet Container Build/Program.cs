using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Azure.ContainerRegistry;
using Microsoft.Azure.ContainerRegistry.Models;
using System.IO;
using System.Threading.Tasks;

namespace DotNet_Container_Build
{
    class Program
    {
        const string USERNAME = "csharpsdkblobtest";
        const string PASSWORD = "NPqQjbdYHPkA9ho/67oPv5voMJvhzD97";
        const string REGISTRY = "csharpsdkblobtest.azurecr.io";
        const string REPO_ORIGIN = "jenkins";
        const string REPO_OUTPUT = "jenkins2";
        const string OUTPUT_TAG = "latest";

        static void Main()
        {
            int timeoutInMilliseconds = 1500000;
            CancellationToken ct = new CancellationTokenSource(timeoutInMilliseconds).Token;
            AzureContainerRegistryClient client = LoginBasic(ct);
            BuildImageInRepoAfterDownload(REPO_ORIGIN, REPO_OUTPUT, OUTPUT_TAG, client, ct).GetAwaiter().GetResult();
        }

        /* Example Credentials provisioning: */
        private static AzureContainerRegistryClient LoginBasic(CancellationToken ct)
        {
            AcrClientCredentials credentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, REGISTRY, USERNAME, PASSWORD, ct);
            AzureContainerRegistryClient client = new AzureContainerRegistryClient(credentials) {
                LoginUri = "https://csharpsdkblobtest.azurecr.io"

            };
            return client;
        }

        //Uploads a Hello world image part by part using stream downloaded and then upload for example purposes
        private static async Task BuildImageInRepoAfterDownload(string origin, string output, string outputTag, AzureContainerRegistryClient client, CancellationToken ct)
        {
            V2Manifest manifest = (V2Manifest)await client.GetManifestAsync(origin, outputTag, "application/vnd.docker.distribution.manifest.v2+json", ct);

            // Acquire and upload all layers
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var layer = client.GetBlobAsync(origin, manifest.Layers[i].Digest).GetAwaiter().GetResult();
                string digestLayer = await UploadLayer(layer, output, client);
                manifest.Layers[i].Digest = digestLayer;
            }

            // Acquire config Blob
            var configBlob = client.GetBlobAsync(origin, manifest.Config.Digest).GetAwaiter().GetResult();
            string digestConfig = await UploadLayer(configBlob, output, client);
            manifest.Config.Digest = digestConfig;

            //Piece image together
            string tag = DateTime.Now.Millisecond.ToString();
            await client.CreateManifestAsync(output, tag, manifest, ct);

            Console.WriteLine("Successfully " + output + ":" + tag);
        }

        // Upload a layer using the nextLink properties internally. Very clean and simple to use overall.
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
