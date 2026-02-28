# ICN_T2

ICN_T2는 요괴워치 2 데이터 편집을 위한 WPF 기반 모딩 툴입니다.

## UI 미리보기

<img width="1206" height="675" alt="스크린샷 2026-02-19 111044" src="https://github.com/user-attachments/assets/0cdeae89-8e62-4512-ab26-56d20a781f02" />
<img width="1199" height="673" alt="스크린샷 2026-02-19 111304" src="https://github.com/user-attachments/assets/39958b67-5e4b-4ff7-a774-a19d50cf364d" />

## 이전 버전 대비 변경점 (Latest)

이번 버전은 기존 UI/편집 기능 정비를 넘어서, 도구 범위를 크게 확장했습니다.

1. 신규 편집 도구 추가
- Crank-a-kai(뽑기) 편집기 추가
- 진화(Evolution) 편집기 추가
- 합성(Fusion) 편집기 추가
- 상점(Shop) 편집기 추가
- 보물상자(Treasure Box) 편집기 추가

2. 뽑기 에디터 표시/매핑 개선
- `hash_mapping_user.csv`를 프로그램 내부 리소스로 포함하여 기본 매핑을 항상 사용 가능하도록 변경
- 외부 CSV가 있으면 외부 파일을 우선 적용하고, 없으면 내장 매핑으로 자동 fallback
- CSV 파서를 분리(`HashMappingCsvParser`)하여 안정성 향상
- 코인/보상명 표시를 해시 중심에서 이름 중심으로 개선

3. 게임 데이터 모델 확장
- Capsule, Combine, Treasure/Shop 관련 정의 및 로직 추가
- YW2 게임 로직 인터페이스(`IGame`)와 구현(`YW2`) 확장

4. 인카운터/맵 처리 보강
- 인카운터 로드/저장 흐름과 맵 리스트 파싱 개선
- 도구 전환 시 레이아웃/초기화 안정성 보강

5. 배포/빌드 설정 강화
- Release 기준 Single-file + Self-contained(`win-x64`) 설정
- 배포 시 실행 파일 중심으로 관리하기 쉽게 정리

6. 리버스 엔지니어링 도구 추가
- `ICN_T2/Tools/ReverseEngineering/`에 StreetPass/VIP 분석 스크립트군 추가
- 내부 분석 및 검증 자동화용 파이썬/파워셸 도구 포함

## 설치 및 실행

1. 릴리스 압축을 해제합니다.
2. `ICN_T2.exe`, `Resources` 폴더, `sample` 폴더가 같은 경로에 있는지 확인합니다.
3. `ICN_T2.exe`를 실행합니다.

## 개발 빌드

```powershell
dotnet build .\ICN_T2\ICN_T2.csproj -c Release
```

```powershell
dotnet publish .\ICN_T2\ICN_T2.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

## 프로젝트 구조

```text
ICN_T2/
├─ ICN_T2/
│  ├─ UI/WPF/                  # WPF UI (Views/ViewModels/Themes)
│  ├─ YokaiWatch/              # 게임 데이터 정의/로직
│  ├─ Logic/                   # 공통 파싱/처리 로직
│  ├─ Resources/               # 리소스 및 해시 매핑 CSV
│  └─ Tools/ReverseEngineering # 분석 스크립트
└─ README.md
```
