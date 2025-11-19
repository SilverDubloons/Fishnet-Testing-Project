using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Globalization;

[InitializeOnLoad]
public static class AutoBuildVersion
{
    static AutoBuildVersion()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildPlayerHandler);
    }

    private static void OnBuildPlayerHandler(BuildPlayerOptions options)
    {
        UpdateBuildVersion();

        // Now call the default build function
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

    private static void UpdateBuildVersion()
    {
        var data = AssetDatabase.LoadAssetAtPath<BuildVersionData>(
            "Assets/Build/BuildVersionData.asset"
        );

        if (data == null)
        {
            Debug.LogError("BuildVersionData asset not found!");
            return;
        }

        System.DateTime now = System.DateTime.Now;

        // Reset or increment iteration
        if (data.lastYear == now.Year &&
            data.lastMonth == now.Month &&
            data.lastDay == now.Day)
        {
            data.iteration++;
        }
        else
        {
            data.iteration = 0;
            data.lastYear = now.Year;
            data.lastMonth = now.Month;
            data.lastDay = now.Day;
        }

        string month = now.ToString("MMM", CultureInfo.InvariantCulture);
        data.version = $"{now.Day}.{month}.{now.Year}.{data.iteration:D2}";

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AutoBuildVersion] Version is now {data.version}");

        // Optional: update Project Settings > Player > Version
        PlayerSettings.bundleVersion = data.version;
    }
}