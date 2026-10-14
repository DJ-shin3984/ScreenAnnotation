# Screen Annotation

Windows 11 화면 판서 앱. 전체 기획·설계는 계획서(https://claude.ai/code/artifact/86ca1610-52e1-45e1-a819-912fb2e77d96, 접근 불가 시 docs/PLAN.md). 태스크는 계획서의 "태스크 트래커"와 상세 섹션을 따른다.

## 기술 스택
- .NET 8 (net8.0-windows10.0.19041.0), C#, WPF, MVVM(CommunityToolkit.Mvvm)
- DI: Microsoft.Extensions.DependencyInjection
- Win32: CsWin32 (NativeMethods.txt에 API 이름 추가). 직접 DllImport 작성 금지
- 캡처: Vortice.Direct3D11/DXGI (Desktop Duplication), 대체 Windows.Graphics.Capture
- UIA: Interop.UIAutomationClient (COM)
- 테스트: xUnit + FluentAssertions

## 아키텍처 규칙
- Core는 WPF·Win32·System.Drawing을 참조하지 않는다
- Win32 호출은 ScreenAnnotation.Windows 프로젝트에만 둔다
- 판서 좌표는 항상 "모니터 로컬 물리 픽셀"로 저장한다. DIP 변환은 렌더 직전에만
- 판서 데이터 변경은 IAnnotationCommand를 통해서만 한다
- UI 스레드에서 50ms 이상 걸릴 수 있는 작업(캡처 인코딩, UIA 질의, OCR)은 백그라운드로

## 작업 방식
- 한 번에 태스크 하나. 시작 전 계획서에서 해당 태스크의 범위·완료 조건을 다시 읽는다
- 완료 조건 중 자동 테스트 가능한 것은 테스트로 작성한다
- 사람이 눈으로 확인해야 하는 항목은 docs/checks/<태스크ID>.md에 확인 절차를 적고 결과 칸을 비워 둔다
- 태스크 범위 밖 변경이 필요하면 구현하지 말고 보고한다
- 끝나면: dotnet build, dotnet test 통과 확인 → 변경 요약, 남은 이슈 보고 → 커밋 메시지 "T<ID>: <요약>"

## 빌드·실행
- dotnet build ScreenAnnotation.sln
- dotnet test
- dotnet run --project src/ScreenAnnotation.App
