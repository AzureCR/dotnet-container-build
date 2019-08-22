using Microsoft.Build.Framework;
using System.Diagnostics;
using System.IO;

namespace MSBuildTasks
{
    public class OrasPush : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string OrasExe { get; set; }

        [Required]
        public string PublishDir { get; set; }

        [Required]
        public string Registry { get; set; }

        [Required]
        public string Repository { get; set; }

        [Required]
        public string Tag { get; set; }

        public string digest { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, PublishDir);

            var psi = new ProcessStartInfo(fileName: OrasExe,
                                           arguments: $" push {Registry}.azurecr.io/{Repository}:{Tag} {PublishDir}");

            psi.RedirectStandardOutput = true;
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit();
                string output = proc.StandardOutput.ReadToEnd();
                using (StringReader sr = new StringReader(output))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("Digest")) {
                            digest = line.Substring(line.IndexOf("sha256"));
                        }

                    }
                }
            }
            return true;
        }
    }
}
