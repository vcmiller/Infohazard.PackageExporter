using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Infohazard.Core.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Infohazard.PackageExporter.Editor {
    [CreateAssetMenu(menuName = "Infohazard/Package Exporter")]
    public class PackageExporter : ScriptableObject {
        [SerializeField] private DefaultAsset _removeFolder;
        [SerializeField] private DefaultAsset _prependFolder;
        [SerializeField] private Object[] _paths;
        [SerializeField] private PackageManifest _package;

        private string _removeFolderPath;
        private string _prependFolderPath;
        private string _tempPath;

        private const string ManifestJsonFolder = "packagemanagermanifest";
        private const string ManifestJsonPathFileContents = @"Packages/manifest.json
00";

        [ContextMenu("Export")]
        public void Export() {
            HashSet<string> addedPaths = new HashSet<string>();
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
            
            foreach (Object asset in _paths) {
                string path = AssetDatabase.GetAssetPath(asset);

                CreateTempDirectoriesForAsset(path, foldersToInclude, addedPaths);
                
                if (asset is DefaultAsset) {
                    ExploreFolder(path, foldersToInclude, addedPaths);
                }
            }
            
            if (_package) CreatePackageManagerManifestFolder(foldersToInclude);

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

        private void ExploreFolder(string path, List<string> foldersToInclude, HashSet<string> addedPaths) {
            if (!Directory.Exists(path)) return;

            foreach (string file in Directory.EnumerateFiles(path)) {
                if (file.EndsWith(".meta")) continue;
                CreateTempDirectoriesForAsset(file, foldersToInclude, addedPaths);
            }

            foreach (string directory in Directory.EnumerateDirectories(path)) {
                if (!Directory.EnumerateDirectories(directory).Any() &&
                    !Directory.EnumerateFiles(directory).Any()) continue;
                
                CreateTempDirectoriesForAsset(directory, foldersToInclude, addedPaths);
                
                ExploreFolder(directory, foldersToInclude, addedPaths);
            }
        }

        private void CreateTempDirectoriesForAsset(string path, List<string> outputPaths, HashSet<string> addedPaths) {
            string[] pathParts = path.Split('/', '\\');
            string pathSoFar = pathParts[0];
            for (int i = 1; i < pathParts.Length; i++) {
                string part = pathParts[i];
                pathSoFar = Path.Combine(pathSoFar, part).Replace('\\', '/');
                CreateTempDirectoryForAsset(pathSoFar, outputPaths, addedPaths);
            }
        }

        private void CreatePackageManagerManifestFolder(List<string> outputPaths) {
            string folder = Path.Combine(_tempPath, ManifestJsonFolder);
            Directory.CreateDirectory(folder);

            SimpleManifest manifest = JsonConvert.DeserializeObject<SimpleManifest>(_package.text);
            File.WriteAllText(Path.Combine(folder, "asset"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
            File.WriteAllText(Path.Combine(folder, "pathname"), ManifestJsonPathFileContents);
            
            outputPaths.Add(folder);
        }

        private void CreateTempDirectoryForAsset(string path, List<string> outputPaths, HashSet<string> addedPaths) {
            if (_removeFolderPath.StartsWith(path)) return;
            string guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);
            if (string.IsNullOrEmpty(guid)) return;
            
            string pathInPackage = path;
            if (pathInPackage.StartsWith(_removeFolderPath)) {
                pathInPackage = pathInPackage.Substring(_removeFolderPath.Length + 1);
                pathInPackage = Path.Combine(_prependFolderPath, pathInPackage).Replace('\\', '/');
            }
            
            if (!addedPaths.Add(pathInPackage)) return;

            string folder = Path.Combine(_tempPath, guid);
            Directory.CreateDirectory(folder);

            if (File.Exists(path)) {
                File.Copy(path, Path.Combine(folder, "asset"));
            }
            
            File.Copy(path + ".meta", Path.Combine(folder, "asset.meta"));
            File.WriteAllText(Path.Combine(folder, "pathname"), pathInPackage);
            outputPaths.Add(folder);
        }
        
        public class SimpleManifest {
            public Dictionary<string, string> dependencies { get; set; } = new Dictionary<string, string>();
        }
    }
}
