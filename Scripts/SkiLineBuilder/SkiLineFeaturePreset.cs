using UnityEngine;

namespace Jibbers.MapTools
{
    [CreateAssetMenu(fileName = "SkiLineFeaturePreset", menuName = "Jibbers/Ski Line Feature Preset")]
    public class SkiLineFeaturePreset : ScriptableObject
    {
        public SkiLineFeature feature = new SkiLineFeature();
    }
}
