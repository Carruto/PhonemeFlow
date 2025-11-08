using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PhonemeFlow
{
    public class CopyPhonemeData : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android || report.summary.platform == BuildTarget.iOS)
            {
                PhonemeDataBuildUtility.TryPreparePhonemeData(report.summary.platform);
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android || report.summary.platform == BuildTarget.iOS)
            {
                PhonemeDataBuildUtility.CleanupTemporaryData();
            }
        }
    }
}
