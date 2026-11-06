# Android 검증 계획

상태: 계획 양식. 게임 기능·테스트 기기·빌드가 미정이므로 모두 NOT RUN이다.

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
