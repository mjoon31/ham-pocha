# 햄포차 (Ham Pocha)

<img src="https://mjoon31.github.io/mjoon31/assets/media/ham-pocha/app-icon.png" alt="햄포차 앱 아이콘" width="140" align="left" />

**3D 탐험·요리·포장마차 경영 게임**

`Unity 2022.3 LTS` `C#` `Android` `AI` `URP` `Firebase`

**김명준 · 개인 개발 · AI 협업 · Android 테스트 빌드**

**개발 기간** · 2026.11.05 — 2026.11.22 (계획)

<br clear="left" />

숲과 마을을 오가며 포차 영업에 필요한 재료를 수집하고, 손님의 주문에 맞춰 조리하고 서빙합니다.  
자정이 되면 영업을 마감하고, 집에서 피로를 회복하며 다음 날을 준비합니다.  
영업으로 얻은 골드와 경험치는 음식 강화와 새로운 탐험 지역·레시피 해금에 쓰입니다.

**[프로젝트 상세 소개](https://mjoon31.github.io/mjoon31/projects/ham-pocha/)** · **[프로젝트 자료](https://mjoon31.github.io/mjoon31/projects/ham-pocha/#resources)** · **[개발 위키](https://github.com/mjoon31/ham-pocha/wiki)**

## 숲에서 포차까지, 햄스터의 하루

**숲 탐험·재료 수집 → 재료 입고 → 조리·서빙 → 판매·성장 → 마감·수면**

- **계절 숲 탐험**에서는 미니맵으로 출입구와 수집 대상을 확인합니다.  
  밤에 몬스터가 공격해 오면 채집을 더 이어갈지, 모은 재료를 가지고 돌아갈지 판단합니다.
- **실내 조리와 서빙**은 공유 냉장고에서 재료를 꺼내 조리대에 넣는 것부터 완성된 음식을 손님에게 직접 건네는 것까지 이어집니다.
- **야시장 영업**은 화로에서 꼬치를 구워 진열하고 자동으로 판매하는 방식입니다. 판매 수익과 경험치로 음식을 강화하고, 새 메뉴와 화로, 탐험 지역을 엽니다.
- **집 꾸미기**에서는 창고에 보관한 가구의 위치와 방향을 바꾸고, 벽 마감을 바꿔 공간을 꾸밉니다.

## 플레이를 받치는 구조

- **씬 전환과 영업 진행.** 공통 시스템이 있는 Main 씬을 유지하고 집·마을·숲을 필요에 따라 불러옵니다.  
  실내외 영업 중에는 마을과 포차 씬을 함께 두고 카메라와 조작 대상을 전환해, 다른 공간의 조리와 손님 행동도 이어지도록 구성했습니다.
- **레시피·성장 데이터.** 레시피에 재료와 아이콘을 연결하고 콘텐츠 카탈로그에서 메뉴 해금과 강화를 관리합니다.  
  조리·메뉴 선택·강화 화면이 같은 데이터를 참조합니다.
- **탐험과 미니맵.** 이동과 상호작용은 Unity Input System으로 처리합니다.  
  숲에 다시 들어오면 채집 대상과 몬스터를 재배치하고, 같은 방문의 저장을 이어갈 때는 수집 상태를 복원해 미니맵에도 반영합니다.
- **가구 배치와 계정별 저장.** 배치 전 격자의 점유와 입구·침대 접근 경로를 확인합니다.  
  로컬 JSON과 Firebase Authentication·Cloud Firestore를 연결해 계정별 진행을 저장하고, 저장 ID로 주문 상태와 가구 배치를 복원합니다.

## AI 협업과 검수

게임의 방향과 검수 기준은 제가 정하고, AI에는 구현과 검사를 맡겼습니다.  
Codex 대화를 역할별로 나누고, `AGENTS.md`와 작업별 지침에 참고할 문서와 수정 범위를 정리했습니다.  
공용 메모와 Git 변경 내역을 확인해 중복 작업을 줄였으며, 직접 플레이한 결과와 후속 수정은 이슈와 커밋에 남겼습니다.

[영업 흐름](https://github.com/mjoon31/ham-pocha/wiki/Core-Loop-and-Business) · [시스템 아키텍처](https://github.com/mjoon31/ham-pocha/wiki/System-Architecture) · [AI 구현과 변경 관리](https://github.com/mjoon31/ham-pocha/wiki/AI-Implementation-Management) · [검증 현황](https://github.com/mjoon31/ham-pocha/wiki/Validation-and-Status)
