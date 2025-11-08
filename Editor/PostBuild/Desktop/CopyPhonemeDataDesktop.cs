using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PhonemeFlow
{
    public class CopyPhonemeDataDesktop : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (IsStandalone(report.summary.platform))
            {
                PhonemeDataBuildUtility.TryPreparePhonemeData(report.summary.platform);
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (IsStandalone(report.summary.platform))
            {
                PhonemeDataBuildUtility.CleanupTemporaryData();
            }
        }

        private static bool IsStandalone(BuildTarget target)
        {
            return BuildPipeline.GetBuildTargetGroup(target) == BuildTargetGroup.Standalone;
        }
    }
}
