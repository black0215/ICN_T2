# UI 개선 & 고급 필터 구현 계획

## 개요
캐릭터 기본정보 창(CharacterInfoV3)에 3가지 기능 추가:
1. 즉시 리스트 선택 (MouseDown 즉시 반응)
2. Rank/Tribe 아이콘 표시 (리스트 + 상세 패널)
3. 슬라이드 고급 검색 필터 패널

---

## 1. 즉시 리스트 선택

### 파일: `CharacterInfoV3.xaml`
- ListBoxItem 스타일에 `EventSetter`로 `PreviewMouseLeftButtonDown` 추가
- 또는: code-behind에서 `LstCharacters.PreviewMouseLeftButtonDown` 핸들러로 즉시 선택

### 파일: `CharacterInfoV3.xaml.cs`
- `ListBoxItem_PreviewMouseDown` 핸들러 추가
- `VisualTreeHelper`로 클릭된 `ListBoxItem` 찾아 즉시 `IsSelected = true` 설정

---

## 2. Rank/Tribe 아이콘 표시

### 리소스 접근 방식
- Rank Icon: `Resources/Rank Icon/Rank_E~S.png` → CopyToOutput → 런타임 파일 로드
- Tribe Icon: `Resources/Tribe Icon/all_icon_kind01_00~11.png` → CopyToOutput → 런타임 파일 로드
- WPF BitmapImage로 변환하여 바인딩

### 파일: `CharacterWrapper` (CharacterViewModel.cs)
- `RankIconSource` 프로퍼티 추가 (BitmapImage)
- `TribeIconSource` 프로퍼티 추가 (BitmapImage)
- 아이콘 캐시: `static Dictionary<int, BitmapImage>` 사용 (파일 반복 로드 방지)

### 파일: `CharacterInfoV3.xaml`
**리스트 ItemTemplate 변경:**
```
[이름]  [Rank아이콘 18x18] [Tribe아이콘 18x18]
```
- 기존 텍스트 `Rank`, `Tribe` → `Image` 컨트롤로 교체
- `Source="{Binding RankIconSource}"`, `Source="{Binding TribeIconSource}"`

**상세 패널 (Panel 1 - 캐릭터 아이콘):**
- 종족 TextBlock 옆에 Tribe 아이콘 이미지 추가
- Panel 4의 Rank/Tribe 텍스트를 아이콘+텍스트 조합으로 변경

---

## 3. 고급 검색 필터 슬라이드 패널

### 디자인
- 검색 패널(Panel 0) 오른쪽에서 왼쪽→오른쪽으로 슬라이드
- 슬라이드 트리거: 검색 패널 상단에 "필터" 토글 버튼
- 슬라이드 패널은 검색 패널 위에 오버레이 (Z-Index)
- 패널 너비: ~280px, 배경: `#70FFFFFF`, CornerRadius: 15

### 필터 옵션 (위→아래 배치)
1. **카테고리 체크박스**: ☑ 요괴 ☑ NPC (기본 둘 다 체크)
2. **Rank 콤보박스**: 전체 / E / D / C / B / A / S (아이콘 표시)
3. **Tribe 콤보박스**: 전체 / 용맹 / 불가사의 / ... (아이콘 표시)
4. **좋아하는 음식 콤보박스**: 전체 / Rice Balls / Bread / ... (아이콘 표시)
5. **싫어하는 음식 콤보박스**: 전체 / Rice Balls / Bread / ...
6. **해시 검색 TextBox**: BaseHash/NameHash 검색

### 정렬 옵션
- **정렬 콤보박스**: 파일명순(기본) / 이름순 / 해시순 / Tribe순

### 파일: `CharacterViewModel.cs`
- `RankList` 프로퍼티: 게임의 Rank 목록 (IGame에서 가져오기 or 하드코딩 E~S)
- `TribeList` 프로퍼티: `IGame.Tribes` 딕셔너리에서
- `FoodList` 프로퍼티: `IGame.FoodsType` 딕셔너리에서
- `FilterFavoriteFood` (int?) 프로퍼티
- `FilterHatedFood` (int?) 프로퍼티
- `SortOption` enum + 프로퍼티
- `AllowFilter()` 업데이트: FavoriteFood/HatedFood 필터, 정렬 적용

### 파일: `CharacterInfoV3.xaml`
- RootGrid에 필터 오버레이 Border 추가 (Grid.Column="0", 높은 ZIndex)
- 필터 토글 버튼 (검색 헤더 오른쪽)
- 슬라이드 애니메이션: TranslateTransform X → 0 (보이기) / -280 (숨기기)

### 파일: `CharacterInfoV3.xaml.cs`
- `ToggleFilterPanel()` 메서드
- `DoubleAnimation`으로 TranslateTransform.X 애니메이션 (300ms, EaseOut)

---

## 구현 순서
1. **CharacterWrapper에 아이콘 프로퍼티 + 캐시** (ViewModel 수준)
2. **리스트 즉시 선택** (XAML + code-behind)
3. **리스트/상세 아이콘 표시** (XAML ItemTemplate 수정)
4. **ViewModel 필터/정렬 확장** (FilterFood, SortOption)
5. **슬라이드 패널 XAML 구조** (오버레이, 컨트롤 배치)
6. **슬라이드 애니메이션** (code-behind)

## 수정 파일 목록
- `CharacterInfoV3.xaml` — 리스트 아이콘, 필터 패널 UI
- `CharacterInfoV3.xaml.cs` — 즉시 선택, 슬라이드 애니메이션
- `CharacterViewModel.cs` — 아이콘 프로퍼티, 필터/정렬 확장
- `ICN_T2.csproj` — Food Icon을 CopyToOutput에 추가 (현재 누락)
