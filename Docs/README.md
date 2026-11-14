# 개발 문서

이 폴더는 프로젝트 운영·기획·설계·검증의 원본이다. 규칙, 빈 양식, 실행 결과를 구분한다. `Template` 파일은 작성 예시이며 실제 결정·실행 기록이 아니다.

## 읽는 순서

1. 루트 지침 (공개 제외) → 현재 상태 (공개 제외)
2. [개발 흐름](Workflow/Development_Flow.md) → 일정 (공개 제외)
3. 작업별 규칙 → 해당 기획·설계 → 실제 기록

| 폴더 | 원본과 역할 |
|---|---|
| `Rules/` | 조사 (공개 제외), 기획 (공개 제외), 기술 선택 (공개 제외), [폴더](Rules/Project_Structure.md), 코드 (공개 제외), 로그 (공개 제외), 테스트 (공개 제외), 모바일 성능 (공개 제외), 개발 기록 (공개 제외) |
| `Research/` | 접근 진단 (공개 제외), [시장조사](Research/Market_Research.md), 한국 리뷰 20건 (공개 제외), [협동 3개·솔로 3개](Research/Concept_Comparison.md) |
| `Planning/` | GDD (공개 제외), 콘텐츠·해금 (공개 제외), Cuisineer 참고 (공개 제외), MVP (공개 제외), 기본 햄스터 모델 (공개 제외) |
| `Decisions/` | 게임 선정 (공개 제외), 결정 목록 (공개 제외) |
| `Architecture/` | [기술 현황](Architecture/Technical_Baseline.md), [ADR 양식](Architecture/ADR_Template.md), [PoC 양식](Architecture/PoC_Template.md) |
| `Git/` | GitHub 운영 (공개 제외), 커밋 (공개 제외), 비공개·공개 자동화 (공개 제외) |
| `Issue_Records/` | [과정 기록 운영](Issue_Records/README.md), [요청·제안·결정·검수·재수정 양식](Issue_Records/Issue_Record_Template.md), `Records/` 실제 작업 과정 |
| `Test/` | [Android 계획](Test/Android_Test_Plan.md), 게임 기능 검증 계획 (공개 제외), [기록 양식](Test/Test_Log_Template.md), [성능 기록](Test/Performance_Log_Template.md), [플레이테스트](Test/Playtest_Template.md), `Logs/` 실제 결과 |
| `Troubleshooting/` | [문제 해결 양식](Troubleshooting/Issue_Resolution_Template.md), `Cases/` 실제 사례 |
| `Workflow/` | [업무 흐름](Workflow/Development_Flow.md), 효율적인 작업 지시 (공개 제외), 작업 지시 모음 (공개 제외), [협업 기록](Workflow/Collaboration_Log.md), [마감 보고](Workflow/Daily_Report_Template.md) |
| `Schedule/` | 일정 (공개 제외), 범위·위험 (공개 제외) |
| `Publishing/` | 공개·배포 (공개 제외), GitHub Pages (공개 제외), 공개 검수 (공개 제외), 게임 소개 양식 (공개 제외), 빌드 릴리스 (공개 제외) |

## 관리 규칙

- `Docs`의 대소문자를 링크·명령·CI에서도 일치시킨다. `docs` 사본을 만들지 않는다.
- 원본 규칙을 여러 파일에 복사하지 않고 링크한다. 루트 `AGENTS.md`는 작업 진입점이다.
- 규칙 수정 시 관련 양식과 링크를 함께 확인한다. 결정 변경은 이전 이유를 지우지 않고 대체 결정으로 연결한다.
- 게임 설계·수치가 미결정이면 그대로 남긴다. 문서 양식이 채워졌다는 이유로 개발·검증 완료 처리하지 않는다.
- 이전 자료에서 옮긴 범위와 제외 사항은 통합 내역 (공개 제외)을 참고한다. 참조 폴더 없이 이 문서들로 다음 작업을 시작할 수 있어야 한다.
- 이번 문서 구성의 실제 정적 검사 결과는 [검사 기록](Test/Logs/2026-11-05_documentation.md)에 있다. 게임 실행 테스트와 구분한다.

## 씬·시스템 구조 확인

[실제 폴더 구조](Rules/Project_Structure.md)와 [씬·공통 시스템 연결](Architecture/Scene_Flow.md)에서 책임과 호출 관계를 확인한다. 승인한 MainSceneLoader·SceneRegion·SceneTransitionFade 원본 링크를 포함한다. 공개 개발 변경은 dev 브랜치에서 확인하며, 선택한 연결 코드와 구조 설명을 실행 가능한 Unity 프로젝트 전체와 구분한다.
