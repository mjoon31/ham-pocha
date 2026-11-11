# Android 검증 계획

상태: 실행계획. 2026-11-11 Galaxy S26 Ultra/Galaxy S22 지정,각30/60FPS모드비교. 빌드/측정치미확보·전항목NOT RUN.

- F-ID / 빌드 버전·코드·APK 해시 / commit·dirty:
- 기기·SoC·RAM·Android·해상도·주사율:
- 실행자·일시 KST / 설치·업데이트 조건:
- 목표 프레임·메모리·발열 기준 / 관련 ADR:

| T-ID | 시나리오 | 기대 결과·확정할 조건 | 결과 | 실제 기록/Issue |
|---|---|---|---|---|
| T-ANDROID-01 | 신규 설치·첫 실행 | 오류 없이 진입, 필수 초기화 정상 | NOT RUN | — |
| T-ANDROID-02 | 터치·UI 입력 | 승인 조작대로 동작, UI 관통·중복 처리 없음 | NOT RUN | — |
| T-ANDROID-03 | 한 판·재시작 | 시작→핵심 루프→성공/실패→재시작 정상 | NOT RUN | — |
| T-ANDROID-04 | 비율·안전영역 | 노치/제스처·텍스트·버튼 잘림 없음 | NOT RUN | — |
| T-ANDROID-05 | 홈·잠금·복귀 | 정한 상태/저장/오디오 정책과 일치 | NOT RUN | — |
| T-ANDROID-06 | 뒤로가기·회전 | 승인된 종료/일시정지·방향 정책과 일치 | NOT RUN | — |
| T-ANDROID-07 | 연속 플레이 | 승인된 프레임·메모리·발열 기준 충족 | NOT RUN | — |
| T-ANDROID-08 | 저장·재실행 | 저장 채택 시 정상·손상·호환·복구 정책 충족 | NOT RUN | — |
| T-ANDROID-09 | 오디오 포커스 | 외부 중단·복귀 후 볼륨/재생 중복 없음 | NOT RUN | — |
| T-ANDROID-10 | 오프라인·접속 변화 | 선택한 게임의 네트워크 정책과 일치 | NOT RUN | — |
| T-ANDROID-11 | 업데이트 | 배포 대상으로 채택 시 버전/데이터 유지 정상 | NOT RUN | — |

적용되지 않는 항목은 N/A와 이유를 기록한다. 미정인 기대 조건은 실제 GDD/ADR로 구체화한 뒤 판정한다.

성능 절차는 Mobile Performance Rules (공개 제외), 결과는 [Test Log](Test_Log_Template.md)·[성능 양식](Performance_Log_Template.md), 사용자 의견은 [Playtest](Playtest_Template.md)에 기록한다.

## 지정 실기기 측정 매트릭스 — 2026-11-11

| 기기 | 설정 | 검증 | 결과 |
|---|---|---|---|
| Galaxy S26 Ultra | 30FPS | 숲/세포차·최대손님·메뉴·야간·저장·30분연속플레이·발열·터치체감 | NOT RUN |
| Galaxy S26 Ultra | 60FPS | 동일조건·프레임변동/p95/p99·해상도·주사율기록 | NOT RUN |
| Galaxy S22 | 30FPS | 동일빌드/측정조건·후반프레임/발열 | NOT RUN |
| Galaxy S22 | 60FPS | 동일조건·체감/안정성비교 | NOT RUN |

30FPS안정유지와60FPS목표를검증한다. 성능설정의30/60표시는실제달성FPS가아니다. OS·기기상태·품질·충전·밝기·일반빌드/Profiler부하를기록한다.
T-ANDROID-12:두기기로그인전환·마지막업로드복원·미업로드롤백경고·이전세션늦은쓰기차단/오프라인복귀. NOT RUN.
