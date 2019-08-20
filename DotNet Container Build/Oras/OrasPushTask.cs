using Microsoft.Build.Framework;
using System.Diagnostics;

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

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, PublishDir);

            var psi = new ProcessStartInfo(fileName: OrasExe,
                                           arguments: $"-p {PublishDir} -h {Registry} -r {Repository} -t {Tag}");

            psi.RedirectStandardOutput = true;
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit();
                Log.LogMessage(MessageImportance.High, proc.StandardOutput.ReadToEnd());
            }
            return true;
        }
    }
}
