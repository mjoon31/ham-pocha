# SaveIdSetup — 저장 고정 ID 부여

소스: [SaveIdSetup.cs](../../Assets/02_Scripts/Editor/SaveIdSetup.cs). 2026-11-17 소스 확인. 공통 규칙 (공개 제외)을 먼저 읽는다.

## 공개 범위와 의존성

2026-11-18 개발자 선택으로 현재 저장 ID 부여 도구와 데이터 컴포넌트 [SaveId.cs](../../Assets/02_Scripts/Data/SaveId.cs), 이 사용 안내만 추가 공개 대상으로 정했다. 다른 에디터 도구·저장/세션 구현·게임플레이 코드는 포함하지 않는다. 승인 커밋·동기화 전에는 공개 반영 완료가 아니다.

원본 도구의 대상 탐색은 TableOrder·VillageNpc·HarvestTarget·ForestRegrowth에 의존한다. 해당 게임플레이 타입은 공개 보류이므로 이 파일만으로 공개본을 컴파일하거나 메뉴를 실행할 수 없다. 원본 코드를 변형하거나 공개용 대체 구현을 만들지 않으며, 아래 사용 절차는 필요한 의존성을 갖춘 개발 원본 기준이다. 과거 저장 변환 검증 기록 전체의 신규 공개는 이번 도구 선택에 포함하지 않는다.

## 목적

저장 대상 오브젝트의 SaveId 누락을 채우고 복제한 새 오브젝트만 별도 ID로 분리한다. 기존 저장 대상의 ID를 재발급하는 도구나 중복 ID 자동 복구기는 아니다.

## 메뉴 위치·진입점

- `Tools > Ham Pocha > Save > Assign Missing IDs in Active Scene`
- `Tools > Ham Pocha > Save > Assign New IDs to Selected Copies`
- 코드 호출: `HamPocha.Editor.SaveIdSetup.AssignMissingIds(Scene)`·대상 조회 `GetTargets(Scene)`. 메뉴는 Play/Play 전환 중 실행하지 않는다. 코드 직접 호출에는 같은 Play 차단이 없으므로 호출자가 먼저 확인한다.

## 사용 순서

1. 실제 프로젝트·Edit Mode·활성 씬을 확인하고 현재 변경/저장 호환 작업을 확인한다. 여러 씬이 열려 있어도 누락 부여 메뉴는 활성 씬 하나가 기준이다.
2. 누락분만 채울 때 첫 메뉴를 실행한다. 활성 씬의 비활성 자식을 포함한 TableOrder·VillageNpc·HarvestTarget, ForestRegrowth의 randomProps/decorations 참조 대상에서 SaveId가 없으면 컴포넌트를 추가하고 빈 ID만 채운다.
3. 복제본은 **새로 만든 복제 오브젝트 자체**를 선택하고 두 번째 메뉴를 사용한다. 선택 GameObject에 SaveId가 있어야 하며 부모만 선택해도 자식까지 재귀 재발급되지는 않는다.
4. Console `[Save] event=save_id_setup`의 assigned/replaced_selected·count와 Inspector/diff를 확인한다. 원본 ID 불변·복제본 ID 구분·구버전 키 영향을 별도 확인한다.
5. 변경 씬은 dirty로 표시될 뿐 자동 저장되지 않는다. 대상 씬 diff를 검토하고 저장한다. 필요 시 기존 ID/저장 검증 절차로 확인하되 검증 도구 실행 권한은 별도다.

## 변경 대상

- 누락 대상에 SaveId 컴포넌트(Undo.AddComponent), `_id`에32자리 GUID(N), 새 ID를 준 대상의 `_schema2Key`는 빈 문자열로 초기화한다.
- 선택 재발급은 유효 씬 안의 선택 오브젝트 중 SaveId가 있는 것만 처리하고 **기존 ID도 교체**한다. 각 씬을 dirty 표시한다. 실제 저장파일/클라우드 데이터를 변환하지 않는다.

## 주의사항

- 원본/기존 저장에 연결된 오브젝트를 복제본과 함께 선택하지 않는다. 재발급은 기존 참조를 끊을 수 있으며 구버전 대응 키도 지운다.
- 이미 ID가 있는 대상은 누락 메뉴에서 건너뛰므로 복제 중복 ID·빈 구버전 키를 자동 해결하지 않는다. 새 ID 생성은 전체 프로젝트 중복 검사의 대체가 아니다.
- 누락 소품 참조는 `missing_forest_prop` 예외가 난다. 앞에서 처리된 일부 대상이 이미 바뀌었을 수 있으므로 diff/Undo 기록을 확인하고 참조를 수정한다. 일괄 원자 롤백을 보장하지 않는다.
- ID 편집은 Undo.RecordObject/컴포넌트 Undo를 사용한다. 실제 선택/씬의 Undo 범위와 저장 후 복구는 확인해야 하며 기존 ID를 일괄 재생성해 오류를 숨기지 않는다.

## 검증 결과

- 과거2026-11-15 AI의 씬 ID 조회:105대상·고유ID/대응키 각105·재할당0·씬 dirty 없음 PASS. 원본 SCENE-ID/M20~23 (공개 제외). 당시 기준이며 이후 추가 오브젝트/콘텐츠 전체를 검증한 결과가 아니다.
- 이번2026-11-17: 메뉴2개·선택 범위·키 초기화·Undo/dirty·자동 저장 없음은 소스 정적 확인. 메뉴 실제 실행·복제/Undo·최신 씬/기존 저장 복원·Play/Android는 NOT RUN. 기존 두 메뉴의 모든 동작이 실검증됐다고 확대하지 않는다.
