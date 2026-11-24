# 햄포차 (Ham Pocha)

<img src="https://mjoon31.github.io/mjoon31/assets/media/ham-pocha/app-icon.png" alt="햄포차 앱 아이콘" width="140" align="left" />

**3D 탐험 | 요리 | 포장마차 경영**

**플랫폼** Android | 가로 화면 | 싱글 플레이

**기술 스택** Unity 2022.3.62f3 | C# | URP | Firebase

**개발 인원** 김명준 | 개인 개발 | AI 협업

**담당 역할** 게임 설계 | AI 작업 지시와 검수 | Android 실기기 테스트

**개발 기간** 2026년 11/5&#126;11/6, 11/11&#126;11/23 | 출시 준비 중

<br clear="left" />

햄포차는 햄스터가 숲에서 재료를 모아, 마을의 포차를 운영하는 캐주얼 경영 시뮬레이션 게임입니다. 손님의 주문을 받아 직접 조리하고 서빙합니다. 장사로 얻은 골드와 경험치로 레시피를 강화하고 새 숲과 야시장을 엽니다.

[프로젝트 상세 소개](https://mjoon31.github.io/mjoon31/projects/ham-pocha/) | [프로젝트 자료](https://mjoon31.github.io/mjoon31/projects/ham-pocha/#resources) | [AI 협업 과정과 주요 사례](https://github.com/mjoon31/ham-pocha/wiki/AI-Collaboration) | [개발 위키](https://github.com/mjoon31/ham-pocha/wiki)

## 숲에서 포차까지, 햄스터의 하루

- **계절 숲**에서 미니맵으로 출입구와 수집 대상을 찾습니다. 밤에 몬스터가 추격하면 재료를 더 모을지, 마을로 돌아갈지 판단합니다.
- **실내 포차**에서는 냉장고에 입고한 재료를 주문에 맞춰 조리대에 하나씩 넣고, 완성한 음식을 손님에게 직접 전달합니다.
- **야시장**에서는 화로에 꼬치를 예약합니다. 완성된 꼬치는 자동으로 진열되고, 손님은 구매한 뒤 자리에 앉아 식사합니다.
- **마감과 휴식**은 이미 받은 주문을 마무리한 뒤 시작합니다. 결산에서 판매 결과를 보고, 집에서 가구를 배치하거나 침대에서 쉬며 다음 날을 준비합니다.

## 플레이를 받치는 구조

- **씬 전환과 영업 상태.** 공통 시스템은 Main 씬에 두고 집, 마을, 숲을 필요에 따라 불러옵니다. 실내와 야외 사이에서 카메라와 조작 대상을 바꿔도 조리와 손님 행동이 이어집니다.
- **레시피와 성장.** 메뉴의 재료, 조리 시간, 가격과 아이콘을 ScriptableObject로 관리합니다. 조리, 메뉴 선택, 강화 화면에서 같은 데이터를 사용합니다.
- **탐험과 미니맵.** 숲을 다시 방문하면 채집물과 몬스터를 재배치합니다. 같은 방문을 이어서 플레이하면 저장한 수집 상태를 복원하고 미니맵에도 반영합니다.
- **가구 배치와 계정별 저장.** 가구의 격자 점유와 입구 및 침대까지의 접근 경로를 검사합니다. 로컬 JSON과 Firebase Authentication, Cloud Firestore를 연결해 계정별 진행을 저장합니다.

## AI 협업과 검수

- **기획과 결정** — 제가 게임 방향과 우선순위, 기능별 완료 기준을 정했습니다. AI가 대안과 영향 범위를 정리하면, 적용할 방식과 범위를 직접 선택했습니다.
- **구현과 도구화** — AI는 확정한 범위에 맞춰 기존 Unity 코드와 에셋을 확인하고 구현·수정·검사를 진행했습니다. 프리팹 격자 등록처럼 반복되는 작업은 에디터 도구로 만들고 사용 절차와 검증 결과를 남겼습니다.
- **검수와 재수정** — Android 실기기에서 플레이 흐름과 조작을 직접 확인하고, 관찰한 문제를 재현 조건과 함께 전달했습니다. AI는 수정과 재검사를 진행했고, 이슈와 테스트 기록에는 개발자 판단, 실제 변경, 검수 주체와 결과를 구분해 남겼습니다.

[역할과 작업 흐름](https://github.com/mjoon31/ham-pocha/wiki/AI-Workflow) · [AI 협업 과정과 주요 사례](https://github.com/mjoon31/ham-pocha/wiki/AI-Collaboration)

Google Play 비공개 테스트와 출시를 준비하고 있습니다. 빌드별 확인 범위는 [검증 현황](https://github.com/mjoon31/ham-pocha/wiki/Validation-and-Status)에 정리했습니다.

[영업 흐름](https://github.com/mjoon31/ham-pocha/wiki/Core-Loop-and-Business) | [시스템 아키텍처](https://github.com/mjoon31/ham-pocha/wiki/System-Architecture) | [AI 구현과 변경 관리](https://github.com/mjoon31/ham-pocha/wiki/AI-Implementation-Management)
