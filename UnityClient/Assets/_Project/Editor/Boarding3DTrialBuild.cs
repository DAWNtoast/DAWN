using System;
using System.IO;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static class Boarding3DTrialBuild
    {
        const string ScenePath="Assets/_Project/Scenes/UIPreviews/UI_Boarding3DTrial.unity";
        [MenuItem("Tools/Shattered Sail/Open 3D Boarding Trial")]
        public static void OpenTrial()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            CreateScene();
            Selection.activeGameObject=UnityEngine.Object.FindFirstObjectByType<Boarding3DTrial>().gameObject;
        }
        static void CreateScene()
        {
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Boarding 3D Trial").AddComponent<Boarding3DTrial>();
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.52f,.56f);
            RenderSettings.fog=false;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        public static void BuildFromCommandLine()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            if(!project.Replace('\\','/').EndsWith("/tmp/boarding-3d-trial-20261102",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build this trial only in its dedicated isolated project.");
            string destination=null;string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-trialBuildOutput")destination=Path.GetFullPath(args[i+1]);
            if(string.IsNullOrEmpty(destination))throw new ArgumentException("Missing -trialBuildOutput");
            PlayerSettings.companyName="ShatteredSail.Validation";PlayerSettings.productName="Boarding3DTrial";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.antiAliasing=4;QualitySettings.vSyncCount=0;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;
            QualitySettings.shadowDistance=40;
            // All prototype materials are created at runtime; explicitly retain their shaders.
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=settings.FindProperty("m_AlwaysIncludedShaders");
            foreach(string name in new[]{"Standard","Unlit/Color"})
            {
                Shader shader=Shader.Find(name);if(shader==null)throw new InvalidOperationException("Missing shader "+name);
                bool exists=false;for(int i=0;i<shaders.arraySize;i++)exists|=shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader;
                if(!exists){int index=shaders.arraySize;shaders.InsertArrayElementAtIndex(index);shaders.GetArrayElementAtIndex(index).objectReferenceValue=shader;}
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            CreateScene();AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=destination,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("3D trial build failed: "+report.summary.result);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(destination),"build-summary.json"),
                "{\"result\":\"PASS\",\"bytes\":"+report.summary.totalSize+",\"errors\":"+report.summary.totalErrors+"}");
        }
    }
}
