using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    [ExecuteAlways]
    public class MapSubobject : MonoBehaviour
    {
        public string rotationParameterNameX;
        public string rotationParameterNameY;
        public string rotationParameterNameZ;

        [Tooltip("Authored local euler of the source subobject, used for unbound axes.")]
        public Vector3 baseLocalRotation;

        MapObject mapObject;

        void OnEnable()
        {
            mapObject = GetComponentInParent<MapObject>();
            Apply();
        }

#if UNITY_EDITOR
        void Update()
        {
            if(!Application.isPlaying)
                Apply();
        }
#endif

        public void Apply()
        {
            if(!mapObject)
                mapObject = GetComponentInParent<MapObject>();

            transform.localRotation = Quaternion.Euler(
                Resolve(rotationParameterNameX, baseLocalRotation.x),
                Resolve(rotationParameterNameY, baseLocalRotation.y),
                Resolve(rotationParameterNameZ, baseLocalRotation.z)
            );
        }

        float Resolve(string parameterName, float fallback)
        {
            if(string.IsNullOrEmpty(parameterName) || !mapObject || mapObject.parameters == null)
                return fallback;

            var parameter = mapObject.parameters.FirstOrDefault(p => p.name == parameterName);
            if(parameter == null || parameter.type != CustomParameterType.Float)
                return fallback;

            return parameter.floatValue;
        }
    }

}