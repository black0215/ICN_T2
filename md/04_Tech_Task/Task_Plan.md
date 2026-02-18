### Legacy UI & Data Binding Report 1. Charabase (캐릭터 기본 정보)

- 파일 : CharabaseWindow.cs

- 데이터 구조 : List<ICharabase> 를 메인으로 사용하며, IGame.GetCharacterbase() 로 로드합니다.

- 바인딩 방식 :

  - 텍스트 : NameHash 를 chara_text 파일(T2bþ 포맷)에서 조회하여 표시합니다.

  - 이미지 : FileNamePrefix , Number , Variant 값을 조합해 face_icon.xi 에서 비트맵을 추출합니다.

  - 속성 연결 : UI 컨트롤(ComboBox 등) 변경 시 SelectedCharabase 객체의 속성( Tribe , Rank 등)에 직접 값을 할당합니다.

- 특이 사항 : 메달 위치( MedalPosX/Y )는 원본 이미지에서 좌표를 계산해 직접 Crop 하는 로직( CropMedal )을 포함합니다.

2. Charascale (캐릭터 비율/크기)

- 파일 : CharascaleWindow.cs

- 데이터 구조 : List<ICharascale>

- 바인딩 방식 :

  - Charabase 리스트와 BaseHash 를 기준으로 조인(Join)하여 캐릭터 이름과 아이콘을 가져옵니다.

  - Scale1 ~ Scale7 까지의 부동소수점 값을 NumericUpDown 컨트롤과 직접 매핑합니다.

- 버전 분기 : 게임 버전(YW1 vs YW2)에 따라 활성화되는 스케일 필드 개수가 다릅니다 (YW1은 일부 비활성).

3. Charaparam (요괴 능력치 및 스킬)

- 파일 : CharaparamWindow.cs

- 데이터 구조 : List<ICharaparam> 이 핵심이며, YW3/Blasters의 경우 BattleCharaparam , HackslashCharaparam 리스트를 별도로 관리합니다.

- 바인딩 방식 :

  - 스탯 : Min/Max HP, 힘, 정신, 방어, 속도 등 10종 이상의 스탯을 바인딩.

  - 스킬/공격 : AttackHash , TechniqueHash 등을 BattleCommand (YW1/2) 또는 Skill (YW3) 리스트와 매핑하여 ComboBox로 표시.

  - 진화(Evolution) : EvolveParam (대상 해시), Level , Cost 를 관리하며, 저장 시 List<ICharaevolve> 를 재구축합니다.

- 🚨 중요 저장 로직 (Critical Save Logic) :

  - YW2 버전 : 데이터 손상을 방지하기 위해 SaveCharaparamAndEvolution 통합 저장 메서드를 사용해야 합니다. 개별 저장 시 파일 구조가 깨질 수 있습니다.

  - YW3 버전 : BattleCharaparam , HackslashCharaparam 등 확장 데이터도 함께 저장해야 합니다.
  
  4. Encounter (인카운터/출현 정보)

- 파일 : EncounterWindow.cs

- 데이터 구조 : 맵별 IEncountTable 및 IEncountChara 리스트.

- UI 구조 : DataGridView 를 사용하여 테이블 형태의 편집 UI 제공.

- 바인딩 방식 :

  - Grid의 콤보박스 컬럼이 Charaparams 리스트와 바인딩되어 요괴를 선택.

  - CellValueChanged 이벤트에서 EncountOffsets 를 통해 실제 데이터( EncountChara )를 갱신.

### ⚠️ 현행 모던 UI(WPF)와의 갭 분석 (Gap Analysis)

현재 YokaiStatsViewModel.cs 등을 검토한 결과, 레거시의 안전장치들이 일부 누락되어 있습니다.



1. YW2 저장 위험 : 모던 코드는 단순히 _game.SaveCharaparam() 만 호출하고 있어, YW2 데이터 손상(Corruption) 위험이 큽니다. 레거시처럼 SaveCharaparamAndEvolution 을 사용하도록 수정이 시급합니다.

2. 진화 데이터 누락 : 현재 모던 UI는 진화( Evolution ) 정보를 로드하거나 편집하는 기능이 구현되어 있지 않습니다.

3. 확장 데이터 누락 : YW3의 Hackslash (블래스터 모드) 스탯 등이 모던 UI에는 반영되지 않았습니다.