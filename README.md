# ICN_T2 — 요괴워치 2 모딩 툴

> **Albatross** 프로젝트의 완전한 재탄생.  
> WPF 기반의 현대적인 UI와 체계적인 코드 구조로 새롭게 태어났습니다.

---

## 📌 프로젝트 소개

**ICN_T2**는 요괴워치 2 (Yokai Watch 2) 게임 데이터를 편집하기 위한 모딩 툴입니다.  
구버전 **Albatross** (v2.0)에서 프로젝트를 완전히 재구성하여, 더 빠르고 안정적이며 아름다운 툴로 발전했습니다.

---

## 🚀 v2.0 → 현재: 무엇이 달라졌나?

### 🏗️ 프로젝트 구조 리브랜딩

| 구분 | v2.0 (Albatross) | 현재 (ICN_T2) |
|:---|:---|:---|
| 📁 프로젝트명 | Albatross | **ICN_T2** |
| 🖥️ UI 프레임워크 | WinForms 중심 (혼합) | **WPF (Modern UI)** 전면 전환 |
| 🧩 솔루션 구조 | `Albatross.sln` | `ICN_T2.sln` |

단순한 이름 변경이 아닌, **프로젝트의 완전한 재탄생**입니다.

---

### 🎨 UI/UX 현대화

| 구분 | v2.0 | 현재 |
|:---|:---|:---|
| 🔤 폰트 | 사용자 PC에 폰트 설치 필요 | **앱 내 내장** (`GmarketSans`, `NexonLv2Gothic`, `ONEMobilePOP`) |
| 🖼️ 테마 | 기본 시스템 스타일 | **커스텀 WPF 테마** (`PuniPuniTheme.xaml`) |

> 💡 폰트가 설치되어 있지 않아도 **어디서나 동일한 디자인**으로 실행됩니다.

---

### 🧹 코드베이스 정리

| 구분 | v2.0 | 현재 |
|:---|:---|:---|
| 💾 레거시 코드 | `Albatross` 구형 코드 혼재 | **제거됨** — 현재 코드만 유지 |
| 📦 백업 파일 | `.bak` 파일 다수 | **`.gitignore`로 자동 제외** |
| 🐘 대용량 파일 | `sample/*.fa` 등 포함 | **업로드 차단** |
| ⚙️ IDE 설정 | `.cursor`, `.trae`, `.claude` 등 노출 | **`.gitignore`로 완전 차단** |

---

## 🛠️ 빌드 환경

- **Framework**: .NET 8.0 (Windows)
- **UI**: WPF + WinForms (혼합)
- **Target OS**: Windows 10 (10.0.22621.0) 이상

---

## 📂 프로젝트 구조

```
ICN_T2/
├── ICN_T2/
│   ├── UI/
│   │   └── WPF/              # WPF 기반 메인 UI
│   │       ├── Views/        # XAML 뷰
│   │       ├── ViewModels/   # ViewModel (MVVM)
│   │       ├── Themes/       # 커스텀 테마
│   │       └── Animations/   # 애니메이션 설정
│   ├── YokaiWatch/           # 게임 데이터 파싱 로직
│   ├── Logic/                # 핵심 비즈니스 로직
│   ├── Tools/                # 유틸리티 도구
│   └── Resources/
│       └── Fonts/            # 내장 폰트
└── README.md
```

---

## ✨ 주요 기능

- 요괴 능력치 (HP, 공격, 방어, 속도 등) 편집
- 기술 / 필살기 / 특성 편집
- 대사 및 메달리움 데이터 편집
- 인카운터 (출현 데이터) 편집
- 커스텀 WPF UI로 쾌적한 편집 환경 제공

---

*ICN_T2 — Albatross의 정신을 이어받아, 더 나은 모습으로.*
