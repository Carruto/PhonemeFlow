#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace PhonemeFlow
{
    public class WebGLPostBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                string buildFolder = report.summary.outputPath;
                string indexPath = Path.Combine(buildFolder, "index.html");

                if (File.Exists(indexPath))
                {
                    string indexContent = File.ReadAllText(indexPath);

                    // Avoid double injection
                    if (!indexContent.Contains("<!-- BEGIN PhonemeFlow Bridge -->"))
                    {
                        string snippet = @"
<!-- BEGIN PhonemeFlow Bridge -->

    <script src='StreamingAssets/PhonemeFlowResources/phonemeflow.js'></script>
    <script src='StreamingAssets/PhonemeFlowResources/phonemeflowInterface.js'></script>

    <script>
      function DownloadFile(path, filename) {
        fetch(path)
          .then(resp => resp.blob())
          .then(blob => {
            const a = document.createElement(""a"");
            a.href = URL.createObjectURL(blob);
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            a.remove();
          });
      }
    </script>

<!-- END PhonemeFlow Bridge -->
";
                        indexContent = snippet + "\n" + indexContent;
                        File.WriteAllText(indexPath, indexContent);

                        UnityEngine.Debug.Log("PhonemeFlow WebGLPostBuild: Injected PhonemeFlow bridge.");
                    }
                }
            }
        }
    }
}
#endif
