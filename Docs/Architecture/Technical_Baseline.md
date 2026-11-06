# 기술 기준선과 미선정 항목

확인일: 2026-11-05. 로컬 파일 기준이며 호환·실행 테스트 결과가 아니다.

| 항목 | 확인한 사실 | 근거 |
|---|---|---|
| Unity | 2022.3.62f3 | `ProjectSettings/ProjectVersion.txt` |
| UI 패키지 | ugui 1.0.0 / TextMeshPro 3.0.7 설치 명세 | `Packages/manifest.json` |
| 기타 명세 | Timeline 1.7.7, Visual Scripting 1.9.4, Development feature 1.0.1 등 | `Packages/manifest.json` |
| 테스트 도구 | Test Framework 1.1.33 등이 의존성으로 기록됨 | `Packages/packages-lock.json` |
| 버전 관리 설정 | Visible Meta Files, Force Text | `ProjectSettings/EditorSettings.asset` |
| 초기 제품 정보 | DefaultCompany / Ham Pocha / 0.1 | `ProjectSettings/ProjectSettings.asset` |

## 결정 대기

- 렌더링·입력 API·UI 사용 범위·씬/초기화·상태 관리·데이터·저장·오디오.
- 싱글/멀티·서버/계정/DB·외부 SDK·광고/결제의 필요성.
- 패키지명·방향·서명·스크립팅 백엔드·CPU 아키텍처·그래픽 API·SDK 지원 범위.
- 목표 기기·프레임·메모리·발열 예산.

설정 파일에 기본 값이 있다는 이유로 지원 기기·배포 스펙·SDK 정책이 확정되었다고 해석하지 않는다. 최신 배포 요건은 실제 배포 결정 시 공식 자료로 확인한다.

## 아키텍처 산출물

GDD 요구표 → 구조 2안 비교 → [ADR](ADR_Template.md) → [PoC](PoC_Template.md) → 실제 결과 → 승인된 초기 구성 순으로 진행한다. 이번 문서 정비에서는 프로젝트 설정·패키지·코드를 변경하지 않는다.

2026-11-06 이름 변경: 표시명은 햄포차 (Ham Pocha), Unity productName은 Ham Pocha로 변경했다. 버전·회사·Android 식별자는 변경하지 않았으며 실행 검증은 하지 않았다.
