using System.IO;
using Hunt.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hunt.EditorTools
{
    /// <summary>
    /// 유니티 위쪽 메뉴 [Hunt] — 클릭 한 번으로 안드로이드 앱(APK)을 만든다.
    /// 필요: Unity Hub → Installs → (설치된 유니티) 톱니 → Add modules → "Android Build Support" (+ OpenJDK, SDK & NDK)
    /// </summary>
    public static class HuntBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string KeepDir = "Assets/Resources/ShaderKeep";
        const string ApkPath = "Builds/ProjectHunt.apk";

        [MenuItem("Hunt/안드로이드 APK 만들기", priority = 1)]
        public static void BuildApk() => Build(false);

        [MenuItem("Hunt/휴대폰에 바로 설치하고 실행 (USB 연결)", priority = 2)]
        public static void BuildAndRun() => Build(true);

        [MenuItem("Hunt/모바일 조작 미리보기 켜기·끄기", priority = 20)]
        public static void TogglePreview()
        {
            GameSettings.ForceMobileControls = !GameSettings.ForceMobileControls;
            EditorUtility.DisplayDialog("모바일 조작 미리보기",
                GameSettings.ForceMobileControls
                    ? "켜졌습니다. ▶ 플레이하면 터치 버튼이 나오고, 마우스로 한 손가락처럼 눌러 볼 수 있어요."
                    : "꺼졌습니다. 키보드·마우스 조작으로 돌아갑니다.", "확인");
        }

        static void Build(bool run)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("안드로이드 모듈 없음",
                    "Unity Hub → Installs → 지금 쓰는 유니티 옆 톱니(⚙) → Add modules →\n" +
                    "'Android Build Support'와 그 아래 OpenJDK, Android SDK & NDK Tools를 체크해 설치한 뒤\n유니티를 다시 열고 이 메뉴를 다시 눌러 주세요.", "확인");
                return;
            }

            EnsureScene();
            ConfigurePlayer();
            KeepShaders();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
            {
                double mb = sum.totalSize / (1024.0 * 1024.0);
                EditorUtility.DisplayDialog("완성!",
                    $"앱을 만들었어요 ({mb:0.0}MB).\n{Path.GetFullPath(ApkPath)}\n\n" +
                    (run ? "연결된 휴대폰에서 바로 실행됩니다." : "이 파일을 휴대폰으로 옮겨 설치하세요 (출처를 알 수 없는 앱 설치 허용 필요)."), "확인");
                EditorUtility.RevealInFinder(ApkPath);
            }
            else
            {
                EditorUtility.DisplayDialog("빌드 실패",
                    $"결과: {sum.result}, 오류 {sum.totalErrors}개\nConsole 창의 빨간 글씨를 복사해서 보내 주세요.", "확인");
            }
        }

        /// <summary>앱에는 씬이 하나 필요하다. 빈 씬을 만들고 등록 (게임은 GameBootstrap이 코드로 구성).</summary>
        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "JeungHoSub";
            PlayerSettings.productName = "역병의 마을";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jeunghosub.projecthunt");
            PlayerSettings.bundleVersion = "0.1.0";

            // 가로 화면만
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 64비트 (구글 플레이 필수) = IL2CPP + ARM64
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        }

        /// <summary>
        /// 코드에서 Shader.Find로 만드는 재질이 앱에서 분홍색으로 깨지지 않게:
        /// 1) 셰이더를 '항상 포함'에 넣고 2) 쓰는 키워드 조합의 재질을 Resources에 하나씩 둔다 (변형이 빌드에서 빠지지 않게).
        /// </summary>
        static void KeepShaders()
        {
            string[] names = { "Standard", "Sprites/Default", "GUI/Text Shader", "Hidden/Internal-Colored" };
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs != null)
            {
                var so = new SerializedObject(gs);
                var arr = so.FindProperty("m_AlwaysIncludedShaders");
                foreach (var n in names)
                {
                    var sh = Shader.Find(n);
                    if (sh == null) continue;
                    bool found = false;
                    for (int i = 0; i < arr.arraySize; i++)
                        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) found = true;
                    if (found) continue;
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                }
                so.ApplyModifiedProperties();
            }

            Directory.CreateDirectory(KeepDir);
            var std = Shader.Find("Standard");
            Keep("Standard_Plain", std, null, 0f);
            Keep("Standard_Normal", std, new[] { "_NORMALMAP" }, 0f);
            Keep("Standard_Emission", std, new[] { "_EMISSION" }, 0f);
            Keep("Standard_NormalEmission", std, new[] { "_NORMALMAP", "_EMISSION" }, 0f);
            Keep("Standard_Cutout", std, new[] { "_ALPHATEST_ON", "_NORMALMAP" }, 1f);
            Keep("Standard_Transparent", std, new[] { "_ALPHAPREMULTIPLY_ON" }, 3f);
            Keep("Sprites_Default", Shader.Find("Sprites/Default"), null, -1f);
            AssetDatabase.SaveAssets();
        }

        static void Keep(string name, Shader shader, string[] keywords, float mode)
        {
            if (shader == null) return;
            string path = $"{KeepDir}/{name}.mat";
            if (File.Exists(path)) return;
            var m = new Material(shader);
            if (mode >= 0f) m.SetFloat("_Mode", mode);
            if (mode >= 3f) { m.SetInt("_ZWrite", 0); m.renderQueue = 3000; }
            else if (mode >= 1f) m.renderQueue = 2450;
            if (keywords != null) foreach (var k in keywords) m.EnableKeyword(k);
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
