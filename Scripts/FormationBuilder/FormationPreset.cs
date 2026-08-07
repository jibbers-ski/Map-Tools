using UnityEngine;

namespace Jibbers.MapTools
{
    [CreateAssetMenu(fileName = "FormationPreset", menuName = "Jibbers/Formation Preset")]
    public class FormationPreset : ScriptableObject
    {
        public Formation formation = new Formation();
    }
}
