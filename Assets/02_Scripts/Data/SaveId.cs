using UnityEngine;

namespace HamPocha.Data
{
    // 씬에 확정한 ID다. 이름/부모/형제 순서를 바꿔도 값은 유지한다.
    // 실행 중에는 생성하지 않는다. 복제한 오브젝트의 중복 ID는 편집 단계에서 해결한다.
    [DisallowMultipleComponent]
    public class SaveId : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField, HideInInspector] private string _schema2Key;

        public string Id => _id;
        public string Schema2Key => _schema2Key;
    }
}
