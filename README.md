# Hiworks Email Notifier

Hiworks 이메일 계정을 실시간으로 감시하여 새로운 메일이 도착하면 알림을 제공하는 데스크톱 애플리케이션입니다.

## 주요 기능
- **IMAP 기반 실시간 모니터링**: MailKit을 사용하여 실시간으로 새로운 메일을 감시합니다.
- **트레이 아이콘 및 알림**: 메일 도착 시 바탕화면에 팝업 알림을 표시하며, 시스템 트레이에서 동작합니다.
- **보안 로그인**: 사용자 계정 정보를 `ProtectedData` (DPAPI)를 사용하여 안전하게 암호화하여 저장합니다.
- **자동 시작 설정**: 윈도우 시작 시 자동으로 앱이 실행되도록 설정할 수 있습니다.
- **커스터마이징 가능한 UI**: WPF 기반의 깔끔한 로그인 및 설정 인터페이스를 제공합니다.

## 프로젝트 구조
- **HiworksNotifier/**: 메인 애플리케이션 프로젝트
  - `HiworksWatcher.cs`: IMAP 서버 접속 및 메일 감시 핵심 로직
  - `ConfigManager.cs`: 암호화된 계정 정보 및 설정 관리
  - `AutoStartupManager.cs`: 윈도우 레지스트리를 통한 자동 시작 관리
  - `LoginWindow.xaml`: 사용자 로그인 인터페이스
  - `MainWindow.xaml`: 메인 트레이 앱 및 설정 UI
- **HiworksNotifier_v1_Source/**: 다른 버전의 소스 코드 가 포함되어 있습니다. (MVVM 패턴 구현 등)

## 빌드 방법
### 요구 사항
- **.NET 10.0 SDK** (또는 호환되는 개발 환경)
- **Visual Studio 2022** (Preview 권장)

### 빌드 및 실행
```powershell
# 프로젝트 폴더로 이동
cd HiworksNotifier

# 의존성 복구
dotnet restore

# 프로젝트 빌드
dotnet build

# 애플리케이션 실행
dotnet run
```

### 배포(Publish) 빌드
단일 파일(Single File) 및 자체 포함(Self-contained) 실행 파일을 생성하려면 다음 명령어를 사용합니다:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
배포 파일은 `HiworksNotifier/bin/Release/net10.0-windows/win-x64/publish/` 폴더에 생성됩니다.

## 라이선스
이 프로젝트의 저작권은 각 원작 소유자에게 있습니다.
