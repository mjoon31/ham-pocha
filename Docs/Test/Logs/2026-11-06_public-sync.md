# 공개 동기화 자동화 검사

- 날짜: 2026-11-06 KST. 실행자: AI.
- 기준: dev `5542d4afa97a69c1d4d468cfcc37cc6e11d11712` 이후 작업 트리. 이번 변경 commit 없음.
- 연결: [IR-20261106-003](../../Issue_Records/Records/IR-20261106-003_public-sync.md), 운영 흐름 (공개 제외).
- 환경: Windows PowerShell, Git, Node.js v26.13.0. 원격 메타데이터는 연결 GitHub 도구로 조회.

## 회차 1 실행 결과 — 이전 공개 전용 구성

이 절은 당시 결과다. 개발자 후속 지적으로 별도 공개 문서·메시지 설계는 회차 2에서 폐기했다.

| 검사 | 기대·실제 | 결과 |
|---|---|---|
| 비공개/공개·브랜치 메타데이터 | mjoon31/ham-pocha-dev private, ham-pocha public, 양쪽 main/dev 확인 | PASS |
| 첫 로컬 테스트 14개 | 순수 로직 13 PASS. 임시 Git 1개는 sandbox `.git/config` 쓰기 거부 | FAIL |
| 격리 fixture 권한 검토 후 재실행 | 당시 14개 모두 통과. 실제 프로젝트 Git 변경 없음 | PASS |
| 추가 지침 보완 후 테스트 | 당시 16개 전부 통과. API 요청 검사 추가 후 최종 **17/17 PASS**, 실패 0 | PASS |
| 문법·승인 검사 | Node 문법 검사, 현재 HEAD 승인 0개 확인, diff 공백 검사 | PASS |
| 허용 자료·문서 링크 | 공개 5개 경로/내용 검사 통과, 신규 깨진 상대 링크 0개. 기존 보류 문서의 깨진 링크 14개는 이전 HEAD에도 존재해 범위 밖으로 보존 | PASS |
| 실제 커밋 계획 최초 생성 | Docs/Test/Logs를 Unity 생성 로그로 오인해 중단. commit/push 전 차단 | FAIL |
| 경로 검사 수정 후 재검증 | 17/17 재통과, 실제 prepare로 비공개 26개 파일·3개 그룹·공개 5개 추출 계획 생성 | PASS |
| GitHub Actions 실제 전송·실제 공개 PR | 전용 secret·최종 commit/public 승인 후 실행 | NOT RUN |
| Unity Editor·Android 빌드·실기기 | 게임 구현/설정 변경 없는 저장소 자동화 작업 | NOT RUN |

실행 명령: `node --test Tools/PublicSync/sync.test.mjs`.

## 회차 1 검사 범위

1. 기능별 공개 commit, 공개 부모만 사용, 재실행 중복 없음.
2. 승인 hash 불일치·누락 blob·실제 승인 없는 기록 거부.
3. 공개 수동 수정 충돌·미관리 파일 덮어쓰기 거부.
4. 초기 README는 알려진 기존 blob일 때만 승인된 교체 허용.
5. 승인 파일 삭제·다른 공개 파일 보존.
6. 내부 전용 변경은 공개 commit 생략.
7. 원본 PR 범위보다 앞선 공개 dev는 과거 PR에 병합하지 않음.
8. 중간 전송 실패 후 성공분 중복 없이 재개.
9. PR 열기·초안 변경·병합·동일 작업 재실행.
10. PR 미병합 종료·재개·공개 변경 없는 PR 생략.
11. 보호 규칙 거부를 성공으로 표시하지 않음.
12. 경로 이탈·인증 패턴·개인/내부 경로·LFS·바이너리 거부.
13. 승인 중복·상태 파일 오류 거부.
14. 실제 임시 Git의 CRLF·원본 C# 직접 추출·승인 기록·main merge·미허용 신규 제외·무승인 변경·기록 변조 거부.
15. 개별 허용 목록·C# source/target·원본 삭제·범위 제외·AGENTS/skills/씬/중복 C# 사본 거부.
16. 이미지 signature·확장자·메타데이터 패턴·LFS 거부·binary blob 전달.
17. GitHub API 요청의 공개 부모·비강제 dev 갱신·예상 public PR head·merge 방식·공개 저장소 한정.

PR·보호 규칙 검사는 격리된 포트 모형이며 GitHub의 실제 계정 설정을 검증한 것이 아니다. 이미지 검사는 파일 서명/기본 메타데이터 검사이며 전체 디코딩·권리·개인정보 검수는 별도다. 실제 원격 commit/push/PR/merge는 실행 전까지 NOT RUN으로 유지한다.

## 회차 2 — 원본 커밋·개발 기록 보존

- 개발자 지적: 공개를 별도 프로젝트/소개 문서/커밋 계획으로 구성하지 않고 원본에서 제한 파일과 민감 정보만 제외한다.
- 원본 message 한 개, 같은 커밋 단위·제목·본문·경위 태그·IR 경로/회차를 유지하도록 기존 구현을 수정했다.
- 격리 테스트: **20/20 PASS**, 실패 0. 실행 명령은 동일하다.
- 추가 검사 18~20: 원본과 동일한 커밋 메시지·다섯 기여 태그, 개발자 지시/AI 작업/직접 수정/피드백/실패/해결의 내용 보존 및 위치만 가림, 원본 기록 경로 유지·제한 코드 제외·공개 C# 변형 차단.
- Public은 Git 제외 자동 생성 검토본이고 Actions의 입력은 실제 원본 commit이다. 별도 공개 메시지 필드는 거부한다.
- 실제 원격 전송·Secrets·PR/merge·게임 실행: **NOT RUN**. 기존 저장소 이력/브랜치에 변경 없음.
- 실제 31개 원본 추출 최초 검사: **FAIL**. 일반 HTTPS의 프로토콜 구분자를 Windows 드라이브로 오인해 중단했다. 드라이브 문자 앞 경계 검사를 추가하고 일반/코드 표기 외부 URL 보존을 회귀 검사에 반영했다. 재실행 결과를 아래에 추가한다.
- 수정 후 **20/20 PASS** 재확인. 실제 원본 **31개 추출 검사 PASS**, 민감 위치/제외 링크 처리가 필요한 21개를 기계적으로 처리하고 오류 0개를 확인했다. 이는 별도 서사 재작성이나 기여/검증 결과 변경이 아니다.
- 실제 prepare: **PASS**. 변경 22개 파일·원본 커밋 2개 그룹과 원본 31개 초기 허용 후보를 검사하고 Public 검토 사본을 자동 생성했다. Public은 gitignore로 Git 추적 대상에서 빠진다. 별도 공개 메시지 입력 없음.

## 회차 3 — 승인된 실제 commit·Push

| 검사 | 실제 결과 | 상태 |
|---|---|---|
| 승인 계획 commit | 로컬 dev에 e77108d·76aa9f8 생성. 현재 dev, staging 비어 있음 | PASS |
| 원본 승인 이력 | cli check로 승인 JSON 1개와 실제 원본 메시지/추출 일치 확인 | PASS |
| 비공개 dev Push | 현재 Git PAT에 workflow scope가 없어 GitHub 원격이 workflow 파일 업로드 거부 | FAIL |
| 원격 dev 확인 | 연결 GitHub 도구로 기존 5542d4a 유지 확인. 로컬 두 커밋은 보존됨 | PASS |
| Actions·공개 dev·공개 PR/main | Push 거부로 실행되지 않음. Git 권한 및 PUBLIC_SYNC_TOKEN 설정 후 재개 | NOT RUN |

인증값은 출력·기록하지 않았다. Git용 workflow 업로드 권한과 공개 자동화용 Secret은 서로 다른 용도다. 같은 두 커밋을 재생성하지 않고 인증 보완 후 Push만 재시도한다.

## 회차 4 — 실제 Actions와 주소 오류 회귀

- 개발자 Push: PASS, dev 76aa9f8. 실제 실행은 Public Sync #1 (비공개 링크).
- GitHub-hosted Ubuntu: 격리 테스트 20/20 PASS, Mirror 단계 FAIL(저장소 메타데이터 GET 404). PUBLIC_SYNC_TOKEN과 GITHUB_TOKEN은 마스킹된 값으로 전달됐다.
- 연결 도구로 저장소 끝 슬래시 주소의 HTTP 404 재현. 빈 route 주소 생성 결함을 AI가 수정하고 metadata() 정확한 URL 회귀 검사를 추가했다.
- 수정 후 Windows 로컬 격리 테스트 **21/21 PASS**, 실패 0. 실제 수정 버전의 Actions·공개 dev 반영·PR/main은 **NOT RUN**.
- 변경 대상: Tools/PublicSync/cli.mjs·sync.test.mjs, 같은 IR/테스트 로그. 새 공개 파일·범위·권한 확대 없음. 원본 후속 커밋 승인 뒤 Push 및 원격 재검증한다.
