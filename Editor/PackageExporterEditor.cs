using UnityEditor;
using UnityEngine;

namespace Infohazard.PackageExporter.Editor {
    [CustomEditor(typeof(PackageExporter))]
    public class PackageExporterEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            if (GUILayout.Button("Export")) {
                ((PackageExporter) target).Export();
            }
        }
    }
}