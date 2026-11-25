<h1 align="center">🐹 햄포차 <small>| Ham Pocha</small></h1>

<p align="center"><strong>낮에는 숲에서 재료를 모으고, 저녁에는 햄스터 포차를 엽니다.</strong></p>

<p align="center">
  <kbd>Android</kbd> &nbsp; <kbd>싱글 플레이</kbd> &nbsp; <kbd>출시 준비 중</kbd>
</p>

<p align="center"><strong>개발 기간</strong> 2026년 11/5&#126;11/6, 11/11&#126;11/23</p>

<p align="center">
  <a href="https://mjoon31.github.io/mjoon31/projects/ham-pocha/">햄포차 프로젝트 소개</a>
  &nbsp;|&nbsp;
  <a href="https://github.com/mjoon31/ham-pocha/wiki/AI-Collaboration">AI 협업 과정과 주요 사례</a>
  &nbsp;|&nbsp;
  <a href="https://github.com/mjoon31/ham-pocha/tree/dev">개발 브랜치</a>
  &nbsp;|&nbsp;
  <a href="https://github.com/mjoon31/ham-pocha/blob/dev/Docs/Issue_Records/README.md">작업 기록</a>
  &nbsp;|&nbsp;
  <a href="https://github.com/mjoon31/ham-pocha/issues">개발 이슈</a>
</p>

---

햄포차는 햄스터가 숲에서 재료를 모아, 마을의 포차를 운영하는 캐주얼 경영 시뮬레이션 게임입니다. 당근 수프와 바삭 토스트로 장사를 시작하고, 판매로 얻은 골드와 경험치로 레시피를 강화해 새 메뉴와 숲, 야시장을 엽니다.

## 🌿 숲에서 포차까지

| 숲에서 | 포차 안에서 | 야시장에서 |
| --- | --- | --- |
| 숲마다 다른 재료를 채집하고 몬스터를 상대합니다. 밤에는 도망가던 몬스터가 플레이어를 쫓아옵니다. | 재료를 공유 냉장고에 넣고, 손님의 주문을 받아 직접 조리하고 서빙합니다. | 야시장을 열면 화로에서 꼬치를 조리합니다. 완성된 꼬치는 자동으로 진열되고 손님이 사서 테이블에서 먹습니다. |

게임 속 하루는 실제 **10분**입니다. 정오부터 하루에 한 번 문을 열 수 있고, 자정에는 이미 받은 실내 주문을 마무리한 뒤 결산합니다. 햄스터가 **5레벨에 도달하고 5,000골드**를 모으면 야시장을 열 수 있습니다. 장사를 마친 뒤에는 집에서 가구를 배치하거나 침대에서 쉬며 다음 날을 준비합니다.

## 🛠️ 개발 기록

**Unity 2022.3.62f3, C#, URP**로 제작했습니다. 공통 시스템은 Main 씬에 두고 마을과 포차를 오갈 때도 조리와 손님 행동이 이어지도록 구성했습니다. 레시피는 ScriptableObject로 관리하고, 로컬 JSON 저장과 Firebase를 연결해 계정별 진행을 복원합니다.

### AI 협업과 검수

- 개발자가 게임 규칙과 완료 기준을 정하고 AI에 조사, 구현, 검사를 맡겼습니다. 개발자는 Android에서 첫날 안내와 숲 탐험을 직접 확인했고, AI가 실행한 검사는 환경과 범위를 구분해 기록했습니다. [작업 기록](Docs/Issue_Records/README.md)
- AI와 함께 기존 프리팹 등록 도구를 확장했습니다. Unity 격리 검사에서 새 규칙 3개 저장, 재실행 시 추가 0개, Undo 한 번으로 이번 등록 취소를 확인했습니다. [격자 일괄 등록 검증](Docs/Test/Logs/2026-11-21_grid-registration.md)
- 공개 범위를 파일별로 정하고, 원본의 커밋 단위와 메시지를 유지하도록 자동화했습니다. 초기 공개 dev 동기화에서 승인 파일 31개가 가림 처리한 원본과 일치했습니다. [공개 동기화 과정](Docs/Issue_Records/Records/IR-20261106-003_public-sync.md)

| 자료 | 확인할 내용 |
| --- | --- |
| [기술 기준선](Docs/Architecture/Technical_Baseline.md) | 개발 환경과 기술 선택 |
| [씬 연결](Docs/Architecture/Scene_Flow.md) &#124; [첫 영업일 검증](Docs/Test/Logs/2026-11-23_first-day-flow.md) | 게임 흐름과 첫 영업일에서 확인한 범위 |
| [격자 일괄 등록 검증](Docs/Test/Logs/2026-11-21_grid-registration.md) | 신규 프리팹 등록, 재실행과 한 번에 되돌리기 |
| [작업 과정](Docs/Issue_Records/README.md) | 요청, 결정, 수정과 재검증 |
| [테스트 기록](Docs/Test/Logs/README.md) | Unity와 Android에서 확인한 범위 |
| [문서 목차](Docs/README.md) | 기획, 기술, 검증 자료의 진입점 |

### 출시 준비

Google Play 대한민국 비공개 테스트를 준비하고 있습니다. Unity와 Android 기기에서 게임을 검수했으며, Google Play 설치본의 로그인·저장·보상 광고는 비공개 테스트 배포 후 확인합니다.

공개 저장소에서 검수한 개발 문서와 공개 범위에 포함된 원본 코드를 볼 수 있습니다. 플레이 영상, 스크린샷과 프로젝트 자료는 [햄포차 프로젝트 소개](https://mjoon31.github.io/mjoon31/projects/ham-pocha/)에 모았습니다.
