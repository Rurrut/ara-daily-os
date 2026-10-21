# 빌드와 테스트

현재 개발·빌드 환경은 Windows입니다. 설치 파일과 개인 기록은 저장소에 포함하지 않습니다. 아래 명령은 저장소 최상위 폴더에서 실행합니다.

## 모바일 화면·일정 엔진 테스트

Node.js가 필요하며 별도 npm 패키지를 설치하지 않습니다.

```powershell
node mobile/tests.cjs
node mobile/fixed-sleep-tests.cjs
node mobile/prep-record-tests.cjs
node mobile/satisfaction-tests.cjs
node mobile/yogurt-gap-tests.cjs
node mobile/checklist-tests.cjs
```

첫 번째는 작은 DOM 대역으로 입력 흐름을 검증합니다. 실제 브라우저나 Android 터치를 검증하는 테스트와는 구분합니다. 두 번째는 96가지 기상·지연·식사 길이 조합으로 고정 취침, 실제 기록 보존, 요거트 종료 제한, 수면 알림, 만족도 집계 범위를 확인합니다.

## Windows 앱

.NET Framework의 C# 컴파일러와 Windows Forms를 사용합니다. `Build.ps1`은 Windows의 Framework64 또는 Framework 경로에서 `csc.exe`를 찾습니다. QR 생성용 QRCoder DLL과 라이선스는 저장소에 포함했습니다.

```powershell
.\Build.ps1
& '.\ARA Daily OS.exe'
```

전체 자체 테스트는 별도의 데이터 경로에서 실행하세요. 실제 사용하는 `data/state.json`을 넘기지 않습니다. GUI 검사도 포함하므로 대화형 Windows 세션에서 실행합니다.

```powershell
& '.\ARA Daily OS.exe' --self-test --data "$PWD\test-output\state.json"
Get-Content .\test-output\test-results.txt
```

Windows 기본 제공 PowerShell을 사용하거나 실행 중인 콘솔의 정책에 맞게 스크립트를 실행합니다. 빌드 자체가 방화벽이나 자동 시작을 등록하지는 않습니다. 앱의 Windows 알림은 사용자 범위의 앱 식별자·프로토콜 등록을 사용할 수 있습니다.

## Android 앱

Python 3, JDK 17, Android SDK Build Tools 35, Android API 35의 `android.jar`가 필요합니다. ZIP/JAR로 포함한 ZXing의 라이선스는 `android/lib/ZXing-LICENSE.txt`에 있습니다. 다음 경로는 예시이므로 자신의 설치 경로로 바꿉니다.

```powershell
python android/build_android.py `
  --jdk C:/tools/jdk-17 `
  --tools C:/Android/sdk/build-tools/35.0.0 `
  --android-jar C:/Android/sdk/platforms/android-35/android.jar `
  --work "$PWD/build/android" `
  --keystore "$PWD/work/local-build.jks" `
  --out "$PWD/build/ARA-Daily-OS.apk"
```

빌더는 키가 없으면 로컬 개발용 키를 생성합니다. 공개된 개발용 비밀번호를 사용하므로 배포용 서명 관리는 별도로 해야 합니다. 이 저장소에는 현재 배포 앱의 서명 개인 키를 포함하지 않았습니다. 각자 만든 키로 빌드한 APK는 기존 배포 앱을 업데이트하는 용도로 사용할 수 없습니다.

`android/update-public.der`는 화면 업데이트 검증용 **공개 키**입니다. 서명된 화면 업데이트를 별도로 운영하려면 자신의 서명 키와 대응하는 공개 키를 사용해야 합니다. APK 빌드에 공개 업데이트의 서명 개인 키는 필요하지 않습니다.

## Android 노션 통신 회귀 테스트

`tests/NotionFailureTests.java`는 실제 통신 클래스에 모의 HTTP 응답을 주입합니다. Android 빌드 후 생성되는 `build/android/classes`, Android SDK의 `android.jar`, JVM에서 실행 가능한 `org.json` JAR가 필요합니다. Android SDK의 JSON 클래스는 실행용 대역이므로 JVM용 구현을 클래스패스에서 먼저 둡니다.

```powershell
javac -encoding UTF-8 `
  -cp "C:/tools/json.jar;C:/Android/sdk/platforms/android-35/android.jar;build/android/classes" `
  -d build/test-classes tests/NotionFailureTests.java
java -cp "build/test-classes;C:/tools/json.jar;build/android/classes;C:/Android/sdk/platforms/android-35/android.jar" ara.daily.NotionFailureTests
```

이 테스트는 테스트 객체 생성에 JDK 내부 API를 사용하므로 JDK 17에서 경고가 출력됩니다. 앱 코드의 의존성은 아닙니다. 실제 휴대폰 로그인·인터넷·백그라운드 실행 검증을 대체하지 않습니다.

## 외부 업데이트 경로 검사

Android 빌드 뒤 앞서 사용한 JVM JSON JAR와 Android classes를 클래스패스에 두고 `tests/UpdateFeedTests.java`를 컴파일해 `ara.daily.UpdateFeedTests`를 실행한다. 공개 카탈로그 선택, 같은 저장소의 자산 연결, 인증 헤더 분리, HTTPS·리디렉션·크기 제한을 대역 HTTP로 검사한다. 네트워크나 PC 인증은 필요하지 않다.

[앱 업데이트 사용·배포 방법](UPDATES.md)
