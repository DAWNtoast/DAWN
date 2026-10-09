using System;
using System.Collections.Generic;
using System.IO;
using ShatteredSail.Content;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class LegacyContentValidator
    {
        [MenuItem("Shattered Sail/Content/Validate Legacy Sources")]
        public static void ValidateFromMenu()
        {
            if (TryValidate(out string report))
            {
                Debug.Log(report);
                return;
            }

            Debug.LogError(report);
        }

        public static void ValidateFromCommandLine()
        {
            if (!TryValidate(out string report))
            {
                throw new InvalidOperationException(report);
            }

            Debug.Log(report);
        }

        public static bool TryValidate(out string report)
        {
            string repositoryRoot = GetRepositoryRoot();
            var missingPaths = new List<string>();

            foreach (string relativePath in LegacyContentCatalog.RequiredRelativePaths)
            {
                string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                string absolutePath = Path.GetFullPath(Path.Combine(repositoryRoot, platformPath));
                if (!File.Exists(absolutePath))
                {
                    missingPaths.Add(relativePath);
                }
            }

            if (missingPaths.Count > 0)
            {
                report = "Legacy content validation failed. Missing files:" + Environment.NewLine
                    + string.Join(Environment.NewLine, missingPaths);
                return false;
            }

            report = $"Legacy content validation passed: {LegacyContentCatalog.RequiredRelativePaths.Count} required CSV files found under {repositoryRoot}.";
            return true;
        }

        private static string GetRepositoryRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, ".."));
        }
    }
}

