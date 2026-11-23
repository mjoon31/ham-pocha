# 하루 시간·영업·수면 검증

2026-11-20 KST · AI 실행 · 작업 기준 `dev/0639c133` + 미커밋 변경 · 변경 커밋 없음. IR 회차 4 (공개 제외)

Unity 2022.3.62f3의 기존 컴파일 응답 파일과 참조를 사용했다. 산출물은 저장소 밖에 생성했다. Android APK 빌드·설치, 실제 계정 및 클라우드 저장 검증은 `NOT RUN`이다. 이번 작업의 설정 변경은 Main의 WorldClock과 GameplayTiming이다.

| 검증 절차 | 결과 | 관찰 |
|---|---|---|
| Unity 런타임·Editor 소스 별도 컴파일 | PASS | 두 어셈블리 컴파일 종료 코드 0. 이 결과는 실제 Unity Editor 실행과 구분한다. |
| 격리한 월드 시간과 세션 코드에 시각 및 가상 상점·피로 데이터를 주입 | PASS | 16개 검사. 06:00 시작, 150초 뒤 정오 개점, 개점일 저장, 300초 뒤 자정의 날짜만 증가, 정상 마감과 저장 복원 후 중복 퇴장 방지, 자정 신규 접수·재개점 거부, 수면 1회 회복과 같은 날짜 06:00, 다음 영업일 재개점, 수동 마감 후 재개점 거부, 안내 정지·재개, 구버전 영업 중/아닐 때 전환, 백그라운드 정지. |
| Main·GameplayTiming 정적 검사 | PASS | 참조 GUID와 실제 600초·25초·35초를 대조했다. |
| Unity `BusinessChecks.RunRestoreChecks` | NOT RUN | 배치 실행이 라이선스 클라이언트 연결 단계에서 종료됐으며 복원 검사 결과가 생성되지 않았다. |
| Unity Play Mode·Android | NOT RUN | 실제 UI/세이프 에어리어, 퇴장·야외 식사, 숲 추격·피해·HP 0 강제 귀환, 수면 도중 이탈, 첫 서빙과 결산 안내, 실제 업데이트 저장은 미실행이다. |

로그 확인 태그는 `business_day`, `business_state`, `business_summary`, `daily_schedule_migration`, `tutorial_pause`, `order_warning`, `sleep_completed`, `forest_defeat`다. 정오와 늦은 개점의 판매량 및 10,000골드·Lv.6 도달은 경제 추정 (공개 제외)과 구분해 실측한다.

## 회차 5 — 해 아이콘 경계 확인

AI가 `WorldClock.LightingProgress`, `DayShadows.PhaseAt`, `VillageSceneController.UpdateDayPhase`를 대조하고 코드의 단정밀도 산식으로 하루 86,400초를 검사했다. PASS. 06:00·12:00·18:00·24:00 경계 전후에 밤 또는 각 낮 단계가 적용되며, 각 단계는 게임 시간 21,600초다. 해의 회전은 첫 단계부터 −14°, 0°, +14°다. 실제 화면과 Android는 NOT RUN이다.

## 회차 6 — 상단 HUD 검증

AI가 Unity 2022.3.62f3 참조로 런타임·Editor 소스를 별도 컴파일했다. PASS. 05:59, 06:00, 06:09, 06:10, 11:59, 12:00, 23:59, 다음 날 00:00의 표시식을 검사했다. PASS. 16:9와 20:9 전체 화면 안전영역을 가정한 배치 좌표에서 씬 이름·아이콘·시계·위험 안내·자원 표시 사이의 간격과 HP·닉네임·레벨 배지의 간격을 확인했다. PASS. 실제 글자 외곽선과 기기 안전영역의 화면 검수는 Unity Play·Android NOT RUN이다.

## 회차 7 — 테스트 배속 검증

AI가 Unity 2022.3.62f3 참조로 런타임·Editor 소스를 별도 컴파일했다. PASS. 월드 시간과 세션의 기존 16개 검사와 ×5에서 30초 만에 06:00→12:00, ×5 중 백그라운드 정지, ×5 중 튜토리얼 정지, ×1 복귀 후 실제 25초에 한 시간 진행의 4개 검사를 수행했다. 총 20개 PASS. 실제 버튼 터치와 기기 배속 표시는 Unity Play·Android NOT RUN이다.

## 회차 8 — 테스트 시간 이동

AI가 Unity 2022.3.62f3 참조로 런타임·Editor 소스를 별도 컴파일했다. PASS. 기존 20개 검사에 릴리스 시간 이동 거부, 이동 전 개점, 자정으로 이동 후 날짜 증가, 중복 자정 이동 방지, 수락 주문 보존 상태로 자정 마감 진입, 기존 주문 처리 뒤 결산, 같은 달력 날짜의 정오 이동, 다음 영업일 개점 허용을 더했다. 총 28개 PASS. 실제 버튼 배치·터치와 기기 표시는 Unity Play·Android NOT RUN이다.

## 회차 9 — 음식 정리와 재료 차감

AI가 Unity 2022.3.62f3 참조로 런타임·Editor 소스를 별도 컴파일했다. PASS.

- 실제 컴파일한 `KitchenStock`과 `ForestCombatRules`를 격리해 실행했다. 운반 음식 우선, 보관 칸 초과 폐기, 진행 중·재료 투입 음식 폐기, 야외 재고·기존 보관품 유지, 조리 선택 유지, 반복 정리 시 추가 변경 없음, 20% 내림 경계 등 13개 PASS.
- 월드 시간·세션 코드의 합성 상점으로 수락 주문 처리 후 자정 정리 한 번 호출과 수동 마감 구분을 포함한 29개 검사 PASS. 실제 결산 전후 로그는 `business_state`, `business_summary`의 보관·폐기 개수로 확인할 수 있다.
- 자정 야외 식사·숲 추격 전환 및 HP 0 귀가, 수면 피로 +20 수치와 화면 표시의 실제 플레이 및 Android 검증은 NOT RUN이다.

## 회차 11 · 실내 서빙 제한 50초

2026-11-21 KST · AI 실행 · Windows C# 독립 컴파일, Unity 2022.3.62f3 격리 Edit Mode.

- PASS · 컴파일 오류 0. Main의 타이밍 GUID가 `GameplayTiming`을 가리키고, 주문 수락 시 실내 코드가 `FoodWaitSeconds`를 사용함을 정적으로 확인했다.
- PASS · Unity에서 원본 타이밍 에셋과 새 기본값을 로드해 수락 25초, 서빙 50초, 하루 600초를 확인했다. 로그는 `UnityBuilds/HamPocha/Checks/TutorialNavigation20261121/serving-wait-editmode.log`에 있다.
- NOT RUN · 실제 주문의 시간 경과·자정 마감, Play Mode 및 Android 실기기. 변경 기록 (공개 제외)
