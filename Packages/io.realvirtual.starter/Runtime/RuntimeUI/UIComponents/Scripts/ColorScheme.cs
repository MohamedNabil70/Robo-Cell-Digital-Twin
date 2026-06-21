using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    [CreateAssetMenu(fileName = "ColorScheme", menuName = "realvirtual/Planner/ColorScheme")]
    public class ColorScheme : ScriptableObject
    {
        
        [System.Serializable]
        public class ColorDefinition
        {
            public string name;
            public Color color = Color.white;
        }
        
        public Color defaultColor = Color.white;
        public List<ColorDefinition> colors = new List<ColorDefinition>();

        private Dictionary<string, Color> colorDict;


        void InitColorDict()
        {
            if (colorDict == null)
            {
                colorDict = new Dictionary<string, Color>();
                foreach (var colorDef in colors)
                {
                    colorDict[colorDef.name] = colorDef.color;
                }
            }
        }
        
        public Color GetColor(string name)
        {
            InitColorDict();
            
            if (colorDict.TryGetValue(name, out Color color))
            {
                return color;
            }
            else
            {
                Debug.LogWarning($"Color '{name}' not found in ColorScheme.");
                return defaultColor; // Default color if not found
            }
        }

    }
}
