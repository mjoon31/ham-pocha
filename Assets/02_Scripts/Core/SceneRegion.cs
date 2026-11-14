using UnityEngine;

namespace HamPocha.Core
{
    // 지역 씬의 유일한 루트에 붙인다. 지형·소품·지역 객체는 모두 이 아래에 둔다.
    public class SceneRegion : MonoBehaviour
    {
        [SerializeField] private SceneType _sceneType;
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private Transform[] _arrivalPoints;

        public SceneType SceneType
        {
            get { return _sceneType; }
        }

        public Transform EntryPoint
        {
            get { return _entryPoint; }
        }

        public Transform FindArrivalPoint(string pointId)
        {
            if (string.IsNullOrEmpty(pointId))
            {
                return _entryPoint;
            }

            if (_arrivalPoints == null)
            {
                return null;
            }

            foreach (Transform point in _arrivalPoints)
            {
                if (point != null && point.name == pointId)
                {
                    return point;
                }
            }

            return null;
        }
    }
}
