using System;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEditor;
using UnityEngine;

namespace Unity.VisualScripting.Community 
{
    public class CSharpPreviewSettingsManager
    {
        private CSharpPreviewSettings settings;
    
        public void InitializeSettings()
        {
            var path = "Assets/Unity.VisualScripting.Community.Generated/";
            HUMIO.Ensure(path).Path();
            CSharpPreviewSettings settings = AssetDatabase.LoadAssetAtPath<CSharpPreviewSettings>(path + "CSharpPreviewSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CSharpPreviewSettings>();
                settings.name = "CSharpPreviewSettings";
                AssetDatabase.CreateAsset(settings, path + "CSharpPreviewSettings.asset");
                settings.Initalize();
            }
            else if (!settings.isInitalized)
            {
                settings.Initalize();
            }
            this.settings = settings;
        }
    
        public void SaveSettings()
        {
            settings.SaveAndDirty();
        }
    
        public void UpdateSettings(Action<CSharpPreviewSettings> action)
        {
            action?.Invoke(settings);
        }
    } 
}