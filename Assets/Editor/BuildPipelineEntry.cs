using UnityEditor;
using UnityEngine;

namespace TanLuZhe.EditorTools
{
    /// <summary>
    /// Single entry point for the whole content pipeline. Used by the menu and by
    /// <c>-executeMethod TanLuZhe.EditorTools.BuildPipelineEntry.BuildAll</c> in batch mode.
    /// </summary>
    public static class BuildPipelineEntry
    {
        [MenuItem("TanLuZhe/Build Everything (art + project + scene)", false, 20)]
        public static void BuildAll()
        {
            Debug.Log("[TanLuZhe] === pipeline start ===");
            ProceduralArtGenerator.GenerateAll();
            ProjectSetup.Run();
            WeaponLibraryGenerator.GenerateAll();
            DemoSceneBuilder.BuildFromCommandLine();
            Debug.Log("[TanLuZhe] === pipeline done ===");
        }

        /// <summary>Full pipeline plus static scene verification. Used by CI / batch mode.</summary>
        public static void BuildAndValidate()
        {
            BuildAll();
            Debug.Log("[TanLuZhe] === validation start ===");
            SceneValidator.ValidateFromCommandLine();
        }
    }
}
