# 공통 스케줄 엔진

계획 계산의 원본은 `mobile/offline-core.js` 한 파일이다. 이름은 기존 모바일 경로를 유지하지만 Android 전용 구현이 아니다. Android WebView와 Windows가 같은 파일을 실행한다.

## 실행 경로

- Android: APK의 `assets/offline-core.js`, 또는 서명 검증을 통과한 화면 업데이트에 포함된 같은 엔진을 WebView에서 실행한다.
- Windows: 빌드할 때 같은 파일을 `Aiary.Engine.js` 리소스로 실행 파일에 포함한다. C# `SharedEngine` 어댑터가 함께 배포한 `runtime/node.exe`에서 이를 실행한다.
- `engine/host.cjs`는 표준 입출력의 JSON 요청과 기존 DateTime 저장 형식을 변환하는 어댑터다. 서버를 열지 않으며 네트워크에 데이터를 전송하지 않는다.

## 변경할 위치

계획 생성, 식사·운동 기준 재배치, 버퍼 순서와 용량, 취침 목표, 계획 이력, 이행도, 만족도 집계, 알림에 사용할 계획 시각은 JS 엔진에서 변경한다. 기존 C# `Routine`, `Adaptive`, `PlanHistory`는 독립적인 계산 엔진이 아니라 이 구현을 호출하는 호환 어댑터다. 이전 저장 형식에 필요한 계산도 같은 JS 파일에 있다.

C#에는 WinForms 화면, 마우스 좌표와 선택 표시, 사용자 입력 처리, 파일 저장, 동기화 충돌 병합, Windows 알림 전달이 남는다. 운영체제 기능과 데이터 병합까지 JS로 옮긴 구조는 아니다.

## 실행과 실패 처리

Windows 배포는 실행 파일과 `runtime` 폴더를 함께 제공한다. 전역 Node 설치나 유료 API는 필요 없다. 실행 파일에 기록한 런타임 해시를 검사한다. 엔진과 전송 어댑터는 사용자 캐시에 내용별 경로로 추출하고 기존 파일을 무분별하게 다시 쓰지 않는다.

한 프로세스에서 계산 요청을 순서대로 처리한다. 날짜별 계산은 해당 날짜의 상태만 전달하고, 다른 날짜를 교체하지 않는다. 한국어와 이모지는 JSON 이스케이프로 전달한다. 응답 번호·대기 시간·전송 크기를 확인하며, 엔진 장애를 다른 C# 알고리즘으로 조용히 대체하지 않는다. 통신 실패 시 계산 오류를 알리고 저장 전 상태를 유지한다.

## 검증

`engine/fixtures.json`은 교체 전 C# 엔진으로 만든 가상 시나리오 63개다. 사용자 기록을 포함하지 않는다. `engine/tests.cjs`와 C# `SharedEngineTests`가 이 결과와 비교하고, C# 저장 형식·표준 입출력·원본 해시·오류 후 복구·다른 날짜와 유니코드 보존을 검사한다.

```powershell
node engine/tests.cjs
.\Build.ps1 -NodePath C:\tools\node.exe
& '.\ARA Daily OS.exe' --self-test --data "$PWD\test-output\state.json"
```

자동 계산 규칙은 기존 개인 루틴과 Preview 수준을 유지한다. 이번 변경 자체가 범용 규칙 편집기, 개인화 학습 또는 캘린더 연동을 구현하는 것은 아니다.
