using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class EquipmentStationBuild
{
    private const string ScenePath = "Assets/Station/EquipmentStation.unity";

    [MenuItem("Overdose/Build equipment station for Windows")]
    public static void BuildWindows()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    throw new InvalidDataException("Missing component on " + child.name);
            }
        }
        if (!UnityEngine.Object.FindAnyObjectByType<AndyWeaponStation>() ||
            !UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() ||
            !UnityEngine.Object.FindAnyObjectByType<EventSystem>())
            throw new InvalidDataException("The station or its pointer input is missing.");

        var directory = Environment.GetEnvironmentVariable("OVERDOSE_STATION_BUILD");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.GetFullPath("Build/Windows");
        Directory.CreateDirectory(directory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(directory, "Overdose-DynamicUI.exe"),
            target = BuildTarget.StandaloneWindows64
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new IOException(report.summary.result.ToString());
        Debug.Log("Equipment station built successfully.");
    }
}