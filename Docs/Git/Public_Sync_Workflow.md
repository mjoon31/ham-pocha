# 원본 개발 결과의 공개 동기화

2026-11-06 KST 개발자 결정. 실제 수정·검증·후속 피드백은 [IR-20261106-003](../Issue_Records/Records/IR-20261106-003_public-sync.md)에 기록한다. 같은 날 후속 결정으로 별도 공개 소개 문서·커밋 메시지를 기획하던 초기안은 폐기했다.

## 2026-11-18 공개 자료 보완과 추가 승인

개발자가 비교 결과의 차이를 해결하도록 문서 수정을 지시하고 추가 승인이 필요한 부분을 재안내하도록 요청했다. 현행 기획·역할·검증 범위·실행 제한과 원본 Assets 경로를 문서에 정리한다. 이 문서 수정은 아래 신규 파일의 공개 등록이나 제한 코드 해제를 일괄 승인한 것으로 해석하지 않는다. 기존에 허용한 파일의 후속 변경은 별도 공개 범위 승인을 반복하지 않고 결과·커밋안 승인 절차를 따른다.

2026-11-18 조회 시 공개 dev는 문서 33개·C# 3개·빈 폴더 표식 84개와 README/.gitignore/동기화 상태 파일, 검수 이슈 14개를 제공했다. main은 초기 README/.gitignore 상태였다. 이는 조회 시점의 사실이며 영구 고정 수량이 아니다. 원본 README는 공개 허용 목록에 없어 공개 README에 최신 내용이 반영되지 않았다.

| 추가 결정 단위 | 후보 원본·범위 | 승인 후 필요한 작업과 남는 제한 |
|---|---|---|
| README — 보류 결정 | 공개 저장소 README는 개발자가 추후 작성할 예정 | 원본 README의 앞선 로컬 정비는 남기되 공개 목록에 등록하지 않는다. 추후 내용과 원본 관리 위치를 결정하며 이번에 공개 폴더를 직접 편집하지 않음 |
| 최근 개발 기록 — 추가 결정 대기 | `Docs/Architecture/Content_Flow.md`, `Docs/Issue_Records/Records/IR-20261117-004_business-rework.md`, `Docs/Issue_Records/Records/IR-20261113-003_poc-playtest-fixes.md`, `Docs/Troubleshooting/Cases/2026-11-13_poc-playtest-fixes.md`, `Docs/Test/Logs/2026-11-17_business-rework.md`, `Docs/Test/Logs/2026-11-16_save-profiler.md`, `Docs/Test/Logs/2026-11-15_save-migration.md` | 개별 파일·검수 시점을 확정하고 허용 목록에 등록. 최신 기록이 이미 허용됐는지도 다시 확인한다. 진행 중인 모션/소품 등 다른 변경은 섞지 않는다. 개인정보·원시 로그·미검수 링크를 검사한 승인 커밋의 스냅샷만 반영 |
| 저장 ID 도구 — 범위 선택 완료 | `Assets/02_Scripts/Editor/SaveIdSetup.cs`, `Assets/02_Scripts/Data/SaveId.cs`, `Docs/Editor_Tools/SaveIdSetup.md`의 3개만 | 2026-11-18 개발자가 현재 제작된 저장 ID 관련만 선택했다. 도구는 비공개 게임플레이 타입에 의존하여 공개본 단독 실행 불가. 다른 도구가 나열된 공통 목록과 저장 변환 검사 소스/전체 로그는 이번에 추가하지 않는다. 원본 경로의 SaveIdSetup 하나만 Editor 차단의 예외로 준비하며 commit/push·원격 확인 전 공개 완료 아님 |
| 대표 게임플레이 구현 — 보류 결정 | 완성도를 높인 뒤 개발자가 선별 | 기존 연결 코드3개는 유지한다. Inventory·TableOrder·CookingStation·RecipeData·Fatigue와 저장/세션 등 제한 코드는 추가 공개하지 않는다. 도구 의존성을 이유로 게임플레이 코드를 자동 해제하지 않음 |
| 환경·어셈블리·테스트 소스 — 보류 | `Packages/manifest.json`, `Packages/packages-lock.json`, 필요한 `.asmdef` 및 실제 작성된 테스트 파일 | ProjectVersion은 이번에 제외 결정. 테스트 규칙 문서만 아래 개별 승인됐으며 환경/테스트 코드 공개와 구분한다. Packages/ProjectSettings 전체나 Tests 경로를 포괄 허용하지 않는다. 테스트 존재와 실제 실행 결과는 구분 |
| AI 지침·공용 기록·자동화 구현 — 일부 문서 승인 | 아래 승인한 커밋 양식·개발 기록 규칙·공개 동기화 절차·테스트 규칙만 | AGENTS·공용 메모·Actions/도구 구현 등 나머지는 제외 유지. 채팅 전문·외부 공용 메모 전체·skills·MCP·인증 정보는 자동 포함하지 않는다. 현재 승인된 IR/커밋의 실제 기여 기록은 계속 유지 |
| 실행 샘플·미디어·빌드 | 개발자가 정한 작은 실행 범위, 씬·프리팹·데이터·필수 meta, 영상 또는 APK/Releases | 선택 사항. 새 개발 범위·재배포 권리·파일 목록·실행 환경·테스트·게시 위치를 별도 결정. meta와 lock은 재현에 필요한 파일로 검토하며 비밀/서명키/SDK 원본 배포는 허용하지 않음 |
| main 노출 | 비공개 dev → main PR과 대응 공개 PR/main | 기존 절차 유지. 문서 수정으로 main을 병합하거나 기본 브랜치를 변경하지 않는다. 개발자 PR 검토·병합 후 승인된 공개 자료가 자동 반영됨 |

위 표는 후보와 후속 결정을 구분한 목록이며 공개 안전성 검수 완료를 뜻하지 않는다. 최초 문서 정비에서는 허용 목록을 바꾸지 않았고, 후속 선택에 따라 저장 ID3개와 운영 문서4개를 신규 등록한다. 나머지는 보류·제외 상태다. 실제 diff·의존성·권리를 확인하고 비밀정보·제외 파일·브랜치 대응·삭제·중복 실행을 검증한다. 커밋안 승인 뒤 원본 dev에 push하고 Actions와 공개 원격 파일을 확인한다. 게임 원본 코드·Secrets·공개 폴더는 직접 수정하지 않는다.

### AI 지침·자동화 및 환경·테스트 공개 제안

아래는 기존 제외 자료를 읽고 좁힌 추천안과 후속 결정이다. 2026-11-18 개발자가 Unity 버전 파일을 제외한 문서4개를 수용했다. 해당4개만 개별 허용 목록과 경로 예외에 추가하고, 나머지 보류 파일은 공개하지 않는다. 이는 공개 범위 승인으로, 실제 커밋/push와 원격 반영 완료는 별도로 확인한다.

| 우선순위 | 파일 | 보여주는 내용·판단 |
|---|---|---|
| 공개 범위 승인 | `Docs/Git/Commit_Template.md` | 개발자 판단/직접 작업·AI 작업·검증 주체를 실제 커밋에 구분하는 기준. AGENTS 전체보다 공개 목적에 맞는 좁은 원본 |
| 공개 범위 승인 | `Docs/Rules/Development_Records_Rules.md` | 요청·결정·피드백·수정·검증을 연결하고 실제 기여를 기록하는 기준. 실제 사례는 기존 공개 IR/이슈와 연결 |
| 공개 범위 승인 | `Docs/Git/Public_Sync_Workflow.md` | 파일별 승인·제외·동일 작업 단위 동기화·PR 반영 절차. 개인 경로/비공개 링크는 기존 추출 규칙으로 가리고 다른 내부 지침은 계속 제외 |
| 이번 공개에서 제외 | `ProjectSettings/ProjectVersion.txt` | 개발자가 버전 확인 파일을 이번 수용 범위에서 제외했다. 허용 목록·추출 예외를 추가하지 않음 |
| 공개 범위 승인 | `Docs/Rules/Test_Rules.md` | PASS/FAIL/NOT RUN·실행자·환경·검증 책임 구분. 이 원본 파일만 개별 허용 |
| 검수 후 추천 | `Docs/Test/Logs/2026-11-15_save-migration.md` | 저장 ID 실제 검증 근거가 있으나 전체 저장 변환/클라우드 검증도 포함. 저장 ID 도구만이라는 이번 선택에 포함하지 않고 전체 본문 범위 검수 후 별도 결정 |
| 보류 추천 | `Packages/manifest.json`, `Packages/packages-lock.json` | 현재 MCP 패키지의 Git URL·의존성·고정 hash가 포함됨. 현재 코드 열람용 공개본에는 필수 아님. 목록을 임의 삭제/변형하지 않고 추후 원본 공개 여부를 결정. 다른 작업의 패키지 미커밋 변경도 섞지 않음 |
| 보류 추천 | `.asmdef`·Unity 자동화 테스트 코드 | 현재 `Assets/02_Scripts`에서 해당 어셈블리 정의와 정식 테스트 소스를 확인하지 못함. TestSimulationSpeed는 테스트 스위트가 아닌 개발용 시뮬레이션 도구. 없는 테스트를 새로 만들거나 검사 도구를 자동 공개하지 않음 |
| 추후 선택 | `Tools/PublicSync/core.mjs`, `cli.mjs`, `config.json`, `sync.test.mjs`, `allowlist.json` | 자동화 구현까지 보여주려는 경우의 상호 의존 묶음. cli가 config를 읽고 테스트가 cli를 가져오므로 단일 파일로 실행 검증을 약속하지 않음. 고정 저장소 정보·승인 이력 의존성과 동작 목적을 먼저 검수. 현행 추출 경계 밖이므로 이번에는 구현 공개를 보류 |

`AGENTS.md` 전체·skills·MCP 설정·외부 공용 메모·`.public-sync/approvals`·인증정보는 계속 비공개를 권장한다. Actions workflow 자체는 공개 저장소에서 같은 동작을 실행할 필요가 없어 우선 제외한다. 이미 허용한 작업 흐름/협업 기록·테스트 양식과 계획은 재승인 대상으로 중복 제안하지 않는다. 실행용 환경 공개로 전환할 때에는 meta·패키지 lock이 필요한 재현 파일임을 별도로 검토한다.

## 원본과 공개본

- 유일한 Unity 개발·테스트·기획·기록 원본은 [비공개 로컬 경로] / mjoon31/ham-pocha-dev다. 모든 기존 개발 규칙을 그대로 적용한다.
- 공개 ham-pocha는 같은 개발 결과에서 제한된 파일과 민감 위치를 뺀 결과다. 별도 게임/AI 협업 소개 프로젝트, 별도 작업 단위나 공개 커밋 메시지를 만들지 않는다.
- 문서·IR·테스트·문제 해결 기록은 원본 경로를 유지한다. 승인된 C#은 기존 결정에 따라 원본 Assets/02_Scripts에서 원본 Assets 경로로 직접 추출한다. C# 내용은 바꾸지 않고 사본을 별도 개발하지 않는다.
- Public/는 허용 원본에서 자동 생성하는 Git 제외 검토 사본이다. 편집 원본이나 Actions 입력으로 사용하지 않는다. Actions는 승인된 Git commit의 실제 원본 blob을 읽는다.
- 공개 로컬 폴더에서 일상 개발·직접 수정·커밋하지 않는다. 양쪽 main/dev·기존 이력과 다른 소스 폴더를 보존한다.

## 한 번의 커밋 계획과 자동 실행

1. 원본에서 개발·검증하고 요청 → AI 제안 → 개발자 선택 → 실제 수정 → 검증 → 피드백/재수정 → 최종 판단을 같은 IR에 회차별로 기록한다.
2. 기능별 원본 커밋 제목·필수 본문·파일을 한 번만 제시한다. 공개에는 같은 단위·제목·본문·ai/directed/manual/mixed/review 표시를 사용한다. 별도 공개 제목·본문을 입력받지 않는다.
3. 함께 제시하는 공개 검사는 제한 파일 제외 목록과 민감 위치 가림 결과다. 기존 승인 범위는 재승인받지 않고 새 파일/범위 변경만 먼저 결정받는다.
4. 개발자가 최종 결과·커밋 계획을 승인하면 AI가 비공개 dev에 기능별 commit/push한다. Actions가 허용·제한 목록, 원본 메시지/해시, 비밀 검사를 대조해 공개 dev에 같은 순서로 별도 SHA의 commit을 만든다.
5. 공개 가능한 파일 변화가 없는 내부 전용 commit은 공개에서 생략한다. 가짜 중간 이력·빈 작업 commit을 만들지 않는다. 최초 허용 목록 등록에는 승인된 기존 원본의 현재 스냅샷도 포함하며 과거 작업을 재구성하지 않는다.
6. AI는 실제 Actions·공개 commit·파일을 확인한다. 로컬 검사 통과를 원격 반영 완료라고 표시하지 않는다.
7. 피드백 수정도 같은 원본 IR의 다음 회차·실제 수정 commit으로 남긴다. 수정 결과·커밋안이 승인되면 같은 절차를 이어서 수행한다. 과거 실패·반려·미수락 상태를 지우지 않는다.

## 공개 허용과 제한

- Tools/PublicSync/allowlist.json의 files가 공개 가능한 원본 파일을 개별 지정한다. 원본 기록을 다른 공개 요약문으로 다시 작성하지 않는다. 문서와 승인된 C# 모두 원본 상대 경로를 유지하며 C#은 Assets 아래에 둔다. 과거 Source 경로는 승인 이력 검증용이다.
- restrictedFiles에 지정된 개별 코드는 files에 있더라도 공개할 수 없다. 기본 제외는 AGENTS, .agents/skills, MCP/인증/개인 설정, Tools/자동화 내부 설정, 상세 프로젝트 지침 Docs/Rules·Git·Publishing, 미공개 Planning·GDD, Unity 원본 에셋·씬·프리팹·meta, 외부/유료 에셋, 생성물·빌드다.
- 목록 밖 신규 파일은 자동 공개하지 않는다. 공개 가능한 전체 구성요소를 원본 기준으로 분류하고 제한 사유가 없는 원본 파일을 허용 후보에 포함한다. 별도 공개 프로젝트로 축약/재설계하거나 기록 몇 개만 골라 다시 작성하지 않는다. 현재 정확한 후보와 제한 사유는 allowlist에 명시하며 실제 원격 반영 전 범위 결정을 받는다.
- 개발자 지시/선택, AI 작업 결과, 피드백에 따른 재수정, 이슈 관찰/해결, 개발자 직접 수정, 검증 주체와 PASS/FAIL/NOT RUN, 실제 수락 여부를 모두 보존한다. 직접 수정 사실이 없으면 만들지 않는다.
- 텍스트의 개인 로컬 경로·비공개 저장소 URL은 지정 표기로 가린다. 제외된 파일의 상대 링크는 공개 제외 표기로 바꾼다. 나머지 내용·기여·판단·회차는 유지한다. 가림 실패나 토큰/키 패턴은 전송을 차단한다.
- C#에 민감정보가 있으면 공개용으로 코드 내용을 변형하지 않고 전송을 차단한다. 원본에서 해결하거나 제한 목록에 넣는다. 이미지는 승인된 원본의 PNG/JPEG/WebP·5 MiB 이하·파일 서명을 검사하고 권리/EXIF/개인정보를 검수한다. LFS 포인터는 거부하며 원본 Git/LFS 정책 변경은 별도 확인한다.
- 목록에서 제외하거나 원본을 삭제하면 승인 뒤 해당 공개 파일을 삭제한다. 다른 공개 파일과 공개 과거 이력은 보존한다. 기본 패턴 검사만으로 수동 공개 검수 (공개 제외)를 대신하지 않는다.

## 도구

Git·Node.js 20 이상을 사용하며 Unity 패키지는 추가하지 않는다. 임시 계획은 저장소 밖에 둔다.

1. 외부 draft.json의 groups에 message와 명시적 변경 paths만 작성한다. publicMessage 입력은 거부한다. 제목·본문은 [원본 커밋 규칙](Commit_Template.md)을 따른다.
2. node Tools/PublicSync/cli.mjs prepare <draft.json> <plan.json>으로 원본 HEAD·해시·파일 제외·자동 가림 결과와 Public 검토 사본을 생성한다. 계획 version 2만 사용하며 이전 공개 전용 계획은 사용할 수 없다.
3. 최종 결정 뒤 node Tools/PublicSync/cli.mjs commit <plan.json> <실제 결정 요약> [공개 범위 변경 승인 요약]을 실행한다. 새 허용/제한 범위 변경 시 마지막 인자가 필요하다. 기존 staging·HEAD/원격 차이·내용 변경은 중단한다.
4. 지정 파일과 비공개 .public-sync/approvals/UUID.json만 commit/push한다. 원본 commit 메시지가 승인 메시지와 다른 경우 공개를 차단한다. 같은 파일의 hunk를 자동으로 여러 그룹에 나누지는 않는다.
5. node Tools/PublicSync/cli.mjs check [ref]는 읽기 전용 승인 검사, node --test Tools/PublicSync/sync.test.mjs는 격리 검사다.

승인 대화와 내부 설정은 비공개에 둔다. 공개 .public-sync/state.json에는 중복 방지 UUID와 실제 공개 blob만 기록한다. 공개 commit은 기존 공개 부모에서 생성하므로 비공개 Git 객체/조상 이력을 복제하지 않는다. 같은 논리적 commit이어도 제외 파일·부모가 다르므로 SHA는 달라진다.

## 브랜치·PR

- 비공개 dev push → 제한·민감 정보만 제외한 같은 commit → 공개 dev.
- 비공개 dev → main PR 생성/수정/초안/재개 → 같은 제목·본문에서 민감 위치만 가린 공개 PR. 미병합 종료도 연동한다. 댓글을 실행 지시로 해석하거나 임의로 외부 전송하지 않는다. 해결 과정은 승인된 원본 IR에 기록한다.
- 개발자가 비공개 PR을 검토·병합하면 실제 main 병합 commit의 자료로 공개 PR/main도 반영한다. 비공개 main을 자동 병합하지 않는다. 원본의 검토·병합을 근거로 한 공개 연동은 상시 승인 범위다.
- PR closed와 main push 양쪽 이벤트를 처리하고 재실행 중복을 막는다. main 직접 push는 공개 병합 근거로 쓰지 않는다.
- 공개 보호/필수 검사·예상 head SHA를 지킨다. dev가 원본 PR 범위보다 앞서면 나중 작업을 과거 PR에 끼워 넣지 않고 중단한다. 릴리스 PR 검토 중 새로운 범위를 넣기 전에 최신 dev까지 검토 범위를 확정한다.
- 개별 commit을 보존하는 merge 방식을 사용한다. squash/rebase·이력 재작성·강제 push·브랜치 삭제를 자동 수행하지 않는다. 태그·빌드 배포·사이트 공개·비용 발생은 상시 승인에 포함하지 않는다.

## 최초 인증과 복구

공개 ham-pocha 하나에만 Contents/PR/Issues Read and write를 가진 Fine-grained Token을 만들고 비공개 저장소 Actions Secrets (비공개 링크)의 PUBLIC_SYNC_TOKEN에 보관한다. 비공개 조회는 기본 GITHUB_TOKEN read 권한만 사용한다. App을 사용해도 같은 repository·권한으로 제한한다. 토큰을 채팅·파일·로그에 넣지 않는다. 연결 도구에는 token 발급/Secrets 등록 기능이 없고 확인한 IAB는 비로그인 상태다.

commit/push 또는 공개 전송 중간 실패 시 성공 이력을 보존하고 재실행한다. 공개 수동 수정은 충돌로 보고하며 덮어쓰지 않는다. secret·권한·보호·필수 검사 문제를 해결한 뒤 Re-run jobs를 사용한다. default main에 workflow가 배치된 이후 Run workflow의 pr_number로 대응 PR을 재조정할 수 있다. 최초에는 dev push/PR 이벤트로 시작한다.

## 공식 근거

[GITHUB_TOKEN 범위](https://docs.github.com/en/actions/concepts/security/github_token), [Secrets·외부 권한](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token), [이벤트](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows), [예상 SHA로 PR 병합](https://docs.github.com/en/rest/pulls/pulls#merge-a-pull-request).

## 2026-11-14 커밋 본문 작업 주체 구분

원본 커밋의 개발자 판단·개발자 작업내용·실제 수정·AI 작업내용·검증은 [필수 양식](Commit_Template.md)을 따른다. 공개에도 동일한 단위와 본문 구분을 유지하고 승인된 민감 위치만 가린다. 공개용으로 실제 판단자·편집자·테스트 실행자를 다시 각색하지 않는다. 기존 이력 재작성·강제 push는 이 지침 변경 범위가 아니다.

## 2026-11-14 구조·연결 코드·GitHub 이슈 공개 결정

- 개발자가 폴더 구조·연결 설명, MainSceneLoader·SceneRegion·SceneTransitionFade 원본 코드와 검수된 개발 이슈 공개를 승인하고 이 기준의 규칙 반영을 지시했다.
- 원본 Docs/Rules/Project_Structure.md만 내부 Rules 문서의 개별 예외로 허용한다. Docs/Architecture/Scene_Flow.md는 실제 연결 책임을 설명한다. 승인 C# 3개는 수정 없이 Assets/02_Scripts/Core 및 Assets/02_Scripts/UI로 추출한다. 새 파일 최초 등록에는 현재 원본 스냅샷을 사용하며 과거 커밋을 재구성하지 않는다.
- 저장·활성 세션 핵심 코드, 인증 설정, SDK·외부 에셋·씬/프리팹·meta·빌드와 나머지 내부 지침은 제외한다. 폴더 구조·호출 관계의 공개와 실행 가능한 Unity 프로젝트 전체 공개를 구분한다.
- GitHub Issues는 Git 파일/IR 문서·PR과 별도다. Tools/PublicSync/issues.json에 개발자가 승인하고 검수한 제목·본문·상태 스냅샷만 등록한다. 최초 등록은 원본 #1~#5였으며 현재 대상은 승인된 issues.json을 기준으로 확인한다. 댓글·첨부·실제 UID/sessionId/토큰·미검수 변경은 자동 복제하지 않는다.
- dev push 또는 issues/수동 실행 이벤트에서 검수 스냅샷을 공개 이슈에 반영한다. 원본 이슈 편집만으로 새 본문이 공개 승인되지 않는다. 새 내용·상태를 반영하려면 스냅샷을 다시 검수·갱신하고 승인한 dev 커밋으로 반영한다.
- 제목·개발자 판단/작업·AI 작업·PASS/FAIL/NOT RUN·종료/재개를 보존한다. 비공개 URL·로컬 경로는 가리고 공개 가능한 파일/이슈 링크만 실제 공개 경로로 연결한다. 공개 번호는 독립 배정되며 내부 번호를 그대로 가정하지 않는다.
- 비공개 저장소 정보를 포함하지 않는 해시 표식으로 이슈를 연결해 중복 생성을 막는다. 표식 없는 기존 수동 이슈·댓글은 건드리지 않는다. 관리 이슈의 제목/본문 수동 변경이 감지되면 덮어쓰지 않고 중단한다.
- 공개 토큰은 공개 저장소 하나의 Contents/Pull requests/Issues Read and write가 필요하다. 토큰 값·Secrets를 조회/노출하거나 권한을 임의 변경하지 않는다. 실제 이슈 쓰기 성공과 원격 파일/상태 확인 전에는 완료로 보고하지 않는다.
- 기존 원인은 문서 31개만 지정된 허용 목록(C#0개), Rules 일괄 제한, 이슈 복제 구현 부재였다. 이 설정을 보완하며 비공개·공개 main 병합·태그·배포 권한은 확대하지 않는다.


GitHub의 issues 이벤트는 기본 브랜치에 해당 이벤트 설정이 있어야 실행된다. 현재 main은 승인 전 유지하므로 이번 공개 반영은 dev push 경로로 수행한다. issues 이벤트 자체 활성화는 이후 승인된 dev→main 반영 뒤 확인한다. [GitHub 공식 이벤트 조건](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#issues). 검수 스냅샷의 자동 반영과 미검수 원본 이슈 이벤트를 구분한다.


2026-11-15 개발자 지시: 공개 파일은 별도 Source 디렉터리로 재배치하지 않고 원본 Assets 상대 경로에 둔다. Assets 전체 실제 하위 디렉터리는 공개하며, 공개 파일이 없는 말단 폴더는 내용이 빈 .gitkeep으로 보존한다. 이는 디렉터리 표시만 허용하며 핵심 코드·인증 설정·SDK·외부 에셋·씬/프리팹·meta 내용의 공개를 허용하지 않는다. .public-sync는 동기화 상태 관리에 필요해 유지한다. 과거 Source 경로는 승인 이력 검증만 지원하고 현재 공개 파일은 원본 경로로 이동한다.
