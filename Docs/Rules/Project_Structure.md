# Unity 프로젝트 폴더 규칙

## 루트

| 경로 | 용도·Git 기준 |
|---|---|
| `Assets/` | 씬·스크립트·게임 리소스와 대응 `.meta` 추적 |
| `Packages/` | `manifest.json`, `packages-lock.json` 추적 |
| `ProjectSettings/` | 팀 공통 프로젝트 설정 추적. 비밀 값은 넣지 않음 |
| `Docs/` | 개발 지침·설계·검증 원본. Unity 에셋 폴더 밖에 유지 |
| `.github/` | Issue / PR 양식과 승인된 자동화 |
| `Public/` | 허용된 원본 파일에서 생성한 검토 사본. Git 제외·직접 편집 금지·동기화 입력 아님 |
| `Tools/PublicSync/` | 승인 계획·원본 C# 직접 추출·공개 커밋/PR 자동화와 개별 허용 목록 |
| `.public-sync/approvals/` | 실제 승인 뒤 생성하는 비공개 승인 기록. 공개 제외 |
| `AGENTS.md`, `README.md` | 작업 지침과 프로젝트 진입점 |
| `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/` | 생성물·개인 상태. 추적하지 않음 |
| `Build/`, `Builds/`, `MemoryCaptures/`, `Recordings/` | 빌드·측정 원본. Git에 넣지 않고 승인된 전달 경로 사용 |

## Assets 표준

기존 프로젝트의 번호 체계를 따른다. 아래는 **앞으로 적용할 구조**이며 현재 빈 폴더가 모두 생성되어 있다는 뜻은 아니다. 실제 에셋이 필요할 때 Unity Editor에서 생성한다.

```text
Assets/
  00_ThirdParty/       외부 패키지·에셋, 라이선스와 출처 유지
  01_Scenes/          씬
  02_Scripts/         직접 작성한 C#
    Core/             공통 실행 기반
    Gameplay/         게임 규칙, 아래 기능별 실제 구조 참고
    Input/            입력 변환
    UI/               화면·표현
    Data/             데이터와 저장
    Audio/            오디오 제어
    Editor/           에디터 전용 도구
    Tests/            필요 시 EditMode/PlayMode 테스트
  03_Prefabs/
  04_Models/
  05_Animations/
  06_Materials/
  07_Images/
  08_Audio/
  09_UI/
  10_Data/
  11_Localization/    실제 다국어 지원 채택 시
```

- 모듈·asmdef 도입은 ADR에 책임과 의존 방향을 정한 뒤 필요한 범위에만 적용한다. 빈 아키텍처부터 만들지 않는다.
- 패키지가 자동 관리하는 `Settings`, `TextMesh Pro` 등의 폴더는 근거 없이 이동하지 않는다. 렌더링은 URP 카툰풍을 채택했으며 설정은 Assets/Rendering에서 관리한다. Addressables·다국어 패키지 추가는 별도 결정이다.
- 현재 샘플씬은 `Assets/01_Scenes/SampleScene.unity`, 모델 확인씬은 `HamsterPosePreview.unity`다. 플레이어 프리팹·모델·재질·이미지는 각 분류의 `Characters/Player/` 아래에 둔다. 추가 이동 시 `.meta` GUID와 씬/프리팹 참조를 보존한다.

## Gameplay 실제 구조 — 2026-11-14

`Assets/02_Scripts/Gameplay/`의 18개 스크립트는 기능별로 다음과 같이 배치한다. 이 표는 실제 이동 결과이며, 위 Assets 표준의 미생성 폴더와 구분한다.

| 하위 폴더 | 책임 | 스크립트 |
|---|---|---|
| `Player/` | 플레이어 이동·월드 상호작용·피로 | `PlayerMotor`, `PlayerWorldInteraction`, `Fatigue` |
| `Npc/` | 공통 NPC 이동·마을 주민·마을 경로 | `NpcMotor`, `VillageNpc`, `VillageWalkArea` |
| `Exploration/` | 채집·숲 재배치·몬스터 행동 | `HarvestTarget`, `ForestRegrowth`, `Monster` |
| `Business/` | 영업 속도·조리·포차 상호작용·주문·손님 배정 | `BusinessPacing`, `CookingStation`, `MarketInteractable`, `TableOrder`, `VillageCustomerQueue` |
| `Inventory/` | 재료·완성 음식 보관 | `Inventory` |
| `Session/` | 활동 상태·하루 영업·탐험/수면 전환 판정 | `GameSessionState` |
| `World/` | 지역 출입구·수면 지점 상호작용 | `ForestPortal`, `SleepInteractable` |

- `NpcMotor`는 주민과 몬스터가 함께 사용하는 이동 코드다. 손님 배정은 영업 책임이므로 `VillageCustomerQueue`를 `Business/`에 둔다.
- 폴더 분류만 적용하며 기존 `AiMobileProject.Gameplay` 네임스페이스와 클래스·파일명·코드 내용·스크립트 GUID를 유지한다. 하위 폴더마다 asmdef를 만들지 않는다.
- 요청·이동 결과 (공개 제외), 검증 기록 (공개 제외).

## 씬 스크립트 실제 배치 — 2026-11-14

기존 `Scenes/`를 비우고 제거했으며, 실제 책임에 따라 기존 폴더에 배치한다. 후속 지시로 최초 지역 요청·페이드 타입은 현재 기능에 맞게 다시 명명하고, TitleSceneController·LoadingSceneController는 향후 타이틀·시작 로딩 UI용 이름으로 남겼다.

| 실제 경로 (`Assets/02_Scripts/` 기준) | 이전 타입명 | 현재 책임·배치 이유 |
|---|---|---|
| `Core/MainSceneLoader.cs` | `SceneTitle` → `TitleSceneController` → `WorldSceneBootstrap` | Main 진입 후 최초 지역 로드 요청 |
| `Core/SceneLoadManager.cs` | 동일 | 씬 로드·지역 전환·공통 진행 제어 |
| `UI/VillageSceneController.cs` | `SceneVillage` | 지역 공통 HUD·버튼 표시/입력 |
| `UI/SceneTransitionFade.cs` | `SceneLoading` → `LoadingSceneController` | 지역 이동·수면의 검은 화면 페이드·입력 차단 |

`Session/`은 활동·하루 영업 판정, `World/`는 월드 출입·수면 상호작용이므로 이 4개를 넣지 않는다. 기존 Core/UI 네임스페이스·GUID·직렬화 연결·게임 동작을 유지한다. SceneLoadManager의 `_loadingView`는 FormerlySerializedAs로 연결을 보존하며 `_transitionFade`로 변경했고, 동작이 없던 SetProgress와 호출은 제거했다. 기존 Main 씬 버튼 이벤트의 타입 문자열 변경은 유지한다. Inspector 수동 재연결은 필요하지 않다.

개발자가 설명한 타이틀씬·마을씬(로비)·시작 전환용 로딩씬의 최종 역할과 현재 구현은 구분한다. 이름 변경으로 해당 씬 기능을 새로 구현한 것은 아니다. `HarvestTarget`의 `ResourceTarget`, `SceneLoadManager`의 `SceneTransitionManager`는 이전 AI 추천이며 이번 변경에 적용하지 않았다. 요청·수정 이력 (공개 제외), 검증 (공개 제외).

## 파일·참조 안전

2026-11-14 타이틀 후속: `UI/TitleSceneController`는 초기 SDK 준비·로그인 선택/결과, `UI/LoadingSceneController`는 Title의 별도 배경에서 Main·최초 지역 준비를 담당한다. Build Settings 첫 씬은 Title이다. `MainSceneLoader`는 여전히 Main 자체를 로드하지 않고 최초 지역을 요청하며, Title 진입 시에만 시작 준비 차단을 연결한다. 구현·검증 범위 (공개 제외).

2026-11-14 후속 개발자 지시로 직접 작성 코드의 기본 네임스페이스를 `HamPocha.<Module>`로 통일했다. 위 폴더·씬 스크립트 이동 당시 네임스페이스 유지 설명은 당시 작업 범위이며, 현재 기준은 코딩 규칙 (공개 제외)과 변경 기록 (공개 제외)을 따른다. 과거 `MovedFrom` 정보와 스크립트 GUID는 보존한다.

1. 새 이름은 영어로 명확히 작성한다. 클래스와 C# 파일 이름을 맞춘다. 설명·주석·UI 문구는 한국어 사용 가능하다.
2. 이동·이름 변경은 Unity Editor를 우선한다. 외부 도구를 쓰면 대상과 `.meta`를 쌍으로 이동하고 GUID를 유지한다.
3. `.meta`를 재생성하거나 다른 에셋의 GUID를 복사하지 않는다. 씬·프리팹·머티리얼의 누락 참조를 확인한다.
4. Visible Meta Files / Force Text를 유지한다. 씬·프리팹 대량 변경은 작은 작업으로 나눈다.
5. 외부 원본은 `00_ThirdParty`에서 보존하고 프로젝트용 변형은 자체 폴더에서 관리한다. 재배포 권한을 확인한다.
6. 참고 프로젝트 원본·임시 변환 파일·자동 생성 보고서는 프로젝트에 쌓지 않는다.


## 서드파티 저장소 구분 — 2026-11-12

Assets/00_ThirdParty와 폴더 meta는 비공개 원본 저장소에서 추적할 수 있다. 공개 동기화에는 외부 원본을 포함하지 않으며 개별 허용 목록·경로 검증을 유지한다. 추적 허용과 실제 커밋 완료, 라이선스상 재배포 허용은 각각 구분한다. 정책 변경 이력 (공개 제외).

## 구조·연결의 공개 범위 — 2026-11-14

개발자는 이 폴더 구조와 [씬·공통 시스템 연결](../Architecture/Scene_Flow.md), MainSceneLoader·SceneRegion·SceneTransitionFade 원본 C#의 공개 추출을 승인했다. 이 문서만 Docs/Rules 경로의 개별 공개 예외이며 다른 내부 규칙은 계속 제외한다. 실제 전체 Unity 프로젝트의 에셋·씬·프리팹·설정·SDK를 공개한다는 뜻이 아니다.

현재 Core에는 GameSave/CloudSave, Data에는 SaveData/SaveKey/LocalSave/CloudSession, UI에는 GoogleLogin/CloudSaveHud가 추가돼 있다. 이 이름과 연결 책임의 설명은 공개하되 저장·인증·세션 내부 구현은 공개 범위 밖이다. 상세 연결과 원본 코드 링크는 위 구조 문서에서 관리한다.


2026-11-15 개발자 지시: 공개 파일은 별도 Source 디렉터리로 재배치하지 않고 원본 Assets 상대 경로에 둔다. Assets 전체 실제 하위 디렉터리는 공개하며, 공개 파일이 없는 말단 폴더는 내용이 빈 .gitkeep으로 보존한다. 이는 디렉터리 표시만 허용하며 핵심 코드·인증 설정·SDK·외부 에셋·씬/프리팹·meta 내용의 공개를 허용하지 않는다. .public-sync는 동기화 상태 관리에 필요해 유지한다. 과거 Source 경로는 승인 이력 검증만 지원하고 현재 공개 파일은 원본 경로로 이동한다.
