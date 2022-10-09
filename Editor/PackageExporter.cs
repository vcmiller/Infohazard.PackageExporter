using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Infohazard.Core.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Infohazard.PackageExporter.Editor {
    [CreateAssetMenu(menuName = "Infohazard/Package Exporter")]
    public class PackageExporter : ScriptableObject {
        [SerializeField] private DefaultAsset _removeFolder;
        [SerializeField] private DefaultAsset _prependFolder;
        [SerializeField] private Object[] _paths;

        private string _removeFolderPath;
        private string _prependFolderPath;
        private string _tempPath;

        [ContextMenu("Export")]
        public void Export() {
            string outputPath = EditorUtility.SaveFilePanel("Export Package",
                                                            string.Empty,
                                                            name + ".unitypackage", 
                                                            "unitypackage");
            if (string.IsNullOrEmpty(outputPath)) return;
            
            _tempPath = Path.Combine(Application.temporaryCachePath, name);
            if (Directory.Exists(_tempPath)) {
                Directory.Delete(_tempPath, true);
            }
            Debug.Log(_tempPath);
            
            _prependFolderPath = _prependFolder ? AssetDatabase.GetAssetPath(_prependFolder) : string.Empty;
            _removeFolderPath = _removeFolder ? AssetDatabase.GetAssetPath(_removeFolder) : string.Empty;
            
            List<string> foldersToInclude = new List<string>();
            
            string[] pathParts = _prependFolderPath.Split('/', '\\');
            string pathSoFar = pathParts[0];
            for (int i = 1; i < pathParts.Length; i++) {
                string part = pathParts[i];
                pathSoFar = Path.Combine(pathSoFar, part).Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(pathSoFar);
                foldersToInclude.Add(CreateTempDirectoryForAsset(pathSoFar, guid, false));
            }
            
            foreach (Object asset in _paths) {
                string path = AssetDatabase.GetAssetPath(asset);
                string guid = AssetDatabase.AssetPathToGUID(path);

                foldersToInclude.Add(CreateTempDirectoryForAsset(path, guid, true));
                
                if (asset is DefaultAsset) {
                    ExploreFolder(path, foldersToInclude);
                }
            }

            string tarName = Path.Combine(_tempPath, "archtemp.tar").Replace('/', '\\');
            StringBuilder sb = new StringBuilder();
            sb.Append($"a -ttar \"{tarName}\" ");
            foreach (string folder in foldersToInclude) {
                sb.Append($" \"{folder.Replace('/', '\\')}\"");
            }

            CoreEditorUtility.ExecuteProcess("7z.exe", sb.ToString(), true);

            CoreEditorUtility.ExecuteProcess("7z.exe", $"a -tgzip \"{outputPath}\" \"{tarName}\"", true);
            
            Directory.Delete(_tempPath, true);
        }

        private void ExploreFolder(string path, List<string> foldersToInclude) {
            if (!Directory.Exists(path)) return;

            foreach (string file in Directory.EnumerateFiles(path)) {
                if (file.EndsWith(".meta")) continue;
                string guid = AssetDatabase.AssetPathToGUID(file, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (string.IsNullOrEmpty(guid)) continue;
                foldersToInclude.Add(CreateTempDirectoryForAsset(file, guid, true));
            }

            foreach (string directory in Directory.EnumerateDirectories(path)) {
                if (!Directory.EnumerateDirectories(directory).Any() &&
                    !Directory.EnumerateFiles(directory).Any()) continue;
                
                string guid = AssetDatabase.AssetPathToGUID(directory, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (string.IsNullOrEmpty(guid)) continue;
                foldersToInclude.Add(CreateTempDirectoryForAsset(directory, guid, true));
                
                ExploreFolder(directory, foldersToInclude);
            }
        }

        private string CreateTempDirectoryForAsset(string path, string guid, bool replacePrefix) {
            string pathInPackage = path;
            if (replacePrefix && pathInPackage.StartsWith(_removeFolderPath)) {
                pathInPackage = pathInPackage.Substring(_removeFolderPath.Length + 1);
                pathInPackage = Path.Combine(_prependFolderPath, pathInPackage).Replace('\\', '/');
            }

            string folder = Path.Combine(_tempPath, guid);
            Directory.CreateDirectory(folder);

            if (File.Exists(path)) {
                File.Copy(path, Path.Combine(folder, "asset"));
            }
            
            File.Copy(path + ".meta", Path.Combine(folder, "asset.meta"));
            File.WriteAllText(Path.Combine(folder, "pathname"), pathInPackage);
            return folder;
        }
    }
}
