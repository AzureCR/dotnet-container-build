using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using MSBuildTasks;
using Microsoft.Azure.ContainerRegistry;
using Microsoft.Azure.ContainerRegistry.Models;
using Newtonsoft.Json;
using QuickType;
using CommandLine;
using ShellProgressBar;
//System.Commandline.Experimental
namespace DotNet_Container_Build
{

    class Options
    {
        [Option('u', "username", Required = true, HelpText = "Username for registry")]
        public string Username { get; set; }

        [Option('p', "password", Required = true, HelpText = "Password for registry")]
        public string Password { get; set; }

        [Option('r', "Registry", Required = true, HelpText = "Output registry")]
        public string Registry { get; set; }

        [Option('e', "repository", Required = true, HelpText = "Output repository")]
        public string Repository { get; set; }

        [Option('t', "tag", Required = false, HelpText = "Tag for repository")]
        public string Tag { get; set; }

        [Option('f', "foldersrc", Required = true, HelpText = "Input folder with produced .Net dll's")]
        public string Source { get; set; }

    }

    /// <summary>
    /// Sample application demonstrating the ability to download all layers for an image and use them to 
    /// create a new Repository.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(options =>
                   {
                       int timeoutInMilliseconds = 1500000;
                       CancellationToken ct = new CancellationTokenSource(timeoutInMilliseconds).Token;
                       var output = new ImageRef()
                       {
                           Registry = options.Registry,
                           Username = options.Username,
                           Password = options.Password,
                           Repository = options.Repository,
                           Tag = options.Tag == null ? "latest" : options.Tag
                       };
                       try
                       {
                           BuildDotNetImage(options.Source, output).GetAwaiter().GetResult();
                       }
                       catch (Exception e)
                       {
                           Console.WriteLine(e);
                       }
                   });
        }

        private static async Task BuildDotNetImage(string fileOrigin, ImageRef outputRepo)
        {
            // 0. Identify project files
            string[] files = System.IO.Directory.GetFiles(fileOrigin, "*.runtimeconfig.json");

           // using (StreamReader r = new StreamReader(files[0]))
            //{
              //  string json = r.ReadToEnd();
               // List<Item> items = JsonConvert.DeserializeObject<List<Item>>(json);
            //}

            int delay = 50;
            // 1. Upload the .Net files to the specified repository

            var clientCredentials = new AcrClientCredentials(AcrClientCredentials.LoginMode.Basic, outputRepo.Registry, outputRepo.Username, outputRepo.Password);
            var client = new AzureContainerRegistryClient(clientCredentials)
            {
                LoginUri = "https://" + outputRepo.Registry
            };

            var oras = new OrasPush()
            {
                OrasExe = "C:/ProgramData/Fish/Barrel/oras/0.6.0/oras.exe",
                Registry = outputRepo.Registry,
                Tag = outputRepo.Tag,
                Repository = outputRepo.Repository,
                PublishDir = fileOrigin,
                Username = outputRepo.Username,
                Password = outputRepo.Password

            };

            if (!oras.Execute())
                throw new Exception("Could not upload " + fileOrigin);

            string orasDigest = oras.digest;
            ManifestWrapper manifest = await client.Manifests.GetAsync(outputRepo.Repository, orasDigest, "application/vnd.oci.image.manifest.v1+json");

            long app_size = (long)manifest.Layers[0].Size;
            string appDiffId = (string)manifest.Layers[0].Annotations.AdditionalProperties["io.deis.oras.content.digest"];
            string app_digest = manifest.Layers[0].Digest;

            // 3. Acquire base for .Net image
            var baseLayers = new ImageRef()
            {
                Registry = "mcr.microsoft.com"
            };

            var dotnetVersion = "3.0";
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

            var baseClient = new AzureContainerRegistryClient(new TokenCredentials())
            {
                LoginUri = "https://" + baseLayers.Registry
            };

            ManifestWrapper baseManifest = await baseClient.Manifests.GetAsync(baseLayers.Repository, baseLayers.Tag, "application/vnd.docker.distribution.manifest.v2+json");

            ProgressBarOptions options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkYellow,
                BackgroundCharacter = '\u25A0',
                ProgressCharacter = '\u25A0'
            };

            ProgressBarOptions childOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                BackgroundCharacter = '\u25A0',
                ProgressCharacter = '\u25A0',
                CollapseWhenFinished = false
            };

            using (var pbar = new ProgressBar(baseManifest.Layers.Count + 1, "Constructing image " + outputRepo.Repository + ":" + outputRepo.Tag, options))
            {
                var baseLayerManager = new LayerManager(baseLayers);
                var outputLayerManager = new LayerManager(outputRepo);
                await baseLayerManager.CopyLayersTo(outputLayerManager, false, true, pbar);

                using (var child = pbar.Spawn(10, "Modifying Config Blob", childOptions))
                {
                    child.Tick("Download Config Blob");
                    Thread.Sleep(delay);
                    var configBlob = await baseClient.Blob.GetAsync(baseLayers.Repository, baseManifest.Config.Digest);
                    long appConfigSize;
                    string appConfigDigest;

                    // 6. Add layer to config blob 
                    using (StreamReader reader = new StreamReader(configBlob, Encoding.UTF8))
                    {
                        child.Tick("Parsing Config Blob");
                        Thread.Sleep(delay);

                        string originalBlob = reader.ReadToEnd();
                        var config = JsonConvert.DeserializeObject<ConfigBlob>(originalBlob);
                        child.Tick("Add cmd run configuration to config blob");
                        Thread.Sleep(delay);

                        config.Config.Cmd = new[] { "dotnet", "newEmpty.dll" };
                        child.Tick("Add new layer info to Rootfs");
                        Thread.Sleep(delay);

                        config.Rootfs.DiffIds.Add(appDiffId);
                        child.Tick("Serialize config blob");
                        Thread.Sleep(delay);

                        string serialized = JsonConvert.SerializeObject(config, Formatting.None);
                        appConfigSize = Encoding.UTF8.GetByteCount(serialized);
                        appConfigDigest = ComputeDigest(serialized);
                        child.Tick("Uploading config blob");
                        Thread.Sleep(delay);

                        await outputLayerManager.UploadLayer(GenerateStreamFromString(serialized), appConfigDigest);
                        // Upload config blob
                    }

                    // 7. Modify manifest file for the new layer
                    child.Tick("Building new Manifest");
                    Thread.Sleep(delay);
                    var newManifest = baseManifest;
                    newManifest.Config.Size = appConfigSize;
                    newManifest.Config.Digest = appConfigDigest;
                    child.Tick("Adding new layer information");
                    Thread.Sleep(delay);

                    var newLayer = new Descriptor()
                    {
                        MediaType = "application/vnd.docker.image.rootfs.diff.tar.gzip",
                        Size = app_size,
                        Digest = app_digest
                    };
                    newManifest.Layers.Add(newLayer);
                    child.Tick("Uploading new manifest for " + outputRepo.Repository +":"+ outputRepo.Tag);
                    Thread.Sleep(delay);

                    await client.Manifests.CreateAsync(outputRepo.Repository, outputRepo.Tag, newManifest);
                    child.Tick("Manifest upload complete for " + outputRepo.Repository + ":" + outputRepo.Tag);
                    Thread.Sleep(delay);

                    pbar.Tick("Added new Manifest");
                    Thread.Sleep(delay);
                }
            }
                Console.WriteLine(outputRepo.Repository + ":" + outputRepo.Tag + " Has been created succesfully");
                Thread.Sleep(delay);

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
                Byte[] result = hash.ComputeHash(s);

                foreach (Byte b in result)
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
                Byte[] result = hash.ComputeHash(enc.GetBytes(s));

                foreach (Byte b in result)
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
