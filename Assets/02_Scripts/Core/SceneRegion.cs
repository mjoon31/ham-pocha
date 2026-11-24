using UnityEngine;

namespace HamPocha.Core
{
    // 지역 씬의 유일한 루트에 붙인다. 지형·소품·지역 객체는 모두 이 아래에 둔다.
    public class SceneRegion : MonoBehaviour
    {
        [SerializeField] private SceneType _sceneType;
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private Transform[] _arrivalPoints;
        [SerializeField] private bool _saveInRegionSpace;

        public SceneType SceneType
        {
            get { return _sceneType; }
        }

        public Transform EntryPoint
        {
            get { return _entryPoint; }
        }

        // 회전된 지역도 기존 저장과 같은 지역 내부 좌표와 방향을 기록한다.
        internal Vector3 ToSavePosition(Vector3 position)
        {
            return _saveInRegionSpace ? transform.InverseTransformPoint(position) : position;
        }

        internal Vector3 FromSavePosition(Vector3 position)
        {
            return _saveInRegionSpace ? transform.TransformPoint(position) : position;
        }

        internal Quaternion ToSaveRotation(Quaternion rotation)
        {
            return _saveInRegionSpace ? Quaternion.Inverse(transform.rotation) * rotation : rotation;
        }

        internal Quaternion FromSaveRotation(Quaternion rotation)
        {
            return _saveInRegionSpace ? transform.rotation * rotation : rotation;
        }

        internal static Vector3 FindRoomRestorePosition(Vector3 saved, Vector3 fallback, BoxCollider floor,
            CapsuleCollider player)
        {
            if (floor == null || player == null) return saved;

            Physics.SyncTransforms();
            var hits = new Collider[32];
            if (IsRoomPositionClear(saved, floor, player, hits)) return saved;

            for (int ring = 1; ring <= 8; ring++)
            {
                float distance = ring * .25f;
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * Mathf.PI / 8f;
                    Vector3 point = saved + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (IsRoomPositionClear(point, floor, player, hits)) return point;
                }
            }

            return fallback;
        }

        private static bool IsRoomPositionClear(Vector3 position, BoxCollider floor, CapsuleCollider player, Collider[] hits)
        {
            var floorRay = new Ray(position + Vector3.up * 2f, Vector3.down);
            if (!floor.Raycast(floorRay, out _, 3f)) return false;

            Vector3 scale = player.transform.lossyScale;
            float radius = player.radius * Mathf.Max(scale.x, scale.z);
            float offset = Mathf.Max(0f, player.height * scale.y * .5f - radius);
            Vector3 center = position + player.transform.TransformVector(player.center);
            int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * offset, center + Vector3.up * offset,
                radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (hit == player || hit == floor || hit.gameObject.scene != floor.gameObject.scene) continue;
                if (!Physics.GetIgnoreLayerCollision(player.gameObject.layer, hit.gameObject.layer)) return false;
            }

            return count < hits.Length;
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
