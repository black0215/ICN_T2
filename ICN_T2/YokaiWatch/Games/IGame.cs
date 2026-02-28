using ICN_T2.Logic.Level5.Archives.ARC0;
using ICN_T2.Logic.Level5.Text;
// ✅ Definitions 폴더의 클래스들을 사용하기 위해 추가
using ICN_T2.YokaiWatch.Definitions;
using System.Collections.Generic;
using ICN_T2.YokaiWatch.Games.YW2.Logic;

namespace ICN_T2.YokaiWatch.Games
{
    public interface IGame
    {
        string Name { get; }

        Dictionary<int, string> Tribes { get; }
        Dictionary<int, string> FoodsType { get; }
        Dictionary<int, string> ScoutablesType { get; }

        ARC0 Game { get; set; }
        ARC0 Language { get; set; }

        Dictionary<string, GameFile> Files { get; set; }

        void Save(System.Action<int, int, string>? progressCallback = null);

        // --- Logic Interfaces (변경된 클래스명 적용) ---

        // ICharabase -> CharaBase
        CharaBase[] GetCharacterbase(bool isYokai);
        void SaveCharaBase(CharaBase[] charabases);

        // ICharascale -> CharScale
        CharScale[] GetCharascale();
        void SaveCharascale(CharScale[] charascales);

        // ICharaparam -> YokaiStats (목록에 있는 이름으로 매핑)
        YokaiStats[] GetCharaparam();
        void SaveCharaparam(YokaiStats[] charaparams);

        // ICharaevolve -> Evolution
        Evolution[] GetCharaevolution();
        void SaveCharaevolution(Evolution[] charaevolves);

        // IItem -> ItemBase
        ItemBase[] GetItems(string itemType);

        // ICharaabilityConfig -> AbilityConfig
        AbilityConfig[] GetAbilities();

        // ISkillconfig -> SkillConfig
        SkillConfig[] GetSkills();

        // IBattleCharaparam -> YokaiStats (혹은 별도 파일이 없다면 Stats 공유)
        // 목록에 BattleCharaparam이 없어서 일단 YokaiStats로 추정했습니다.
        YokaiStats[] GetBattleCharaparam();
        void SaveBattleCharaparam(YokaiStats[] battleCharaparams);

        // IHackslashCharaparam -> BustersStats
        BustersStats[] GetHackslashCharaparam();
        void SaveHackslashCharaparam(BustersStats[] hackslashCharaparams);

        // IHackslashCharaabilityConfig -> BustersAbility
        BustersAbility[] GetHackslashAbilities();

        // IHackslashTechnic -> BustersSkill
        BustersSkill[] GetHackslashSkills();

        // IOrgetimeTechnic -> OniTimeSkill
        OniTimeSkill[] GetOrgetimeTechnics();

        // IBattleCommand -> BattleCommand
        BattleCommand[] GetBattleCommands();

        string[] GetMapWhoContainsEncounter();

        // IEncountTable -> EncountTable, IEncountChara -> EncountSlot
        (Definitions.EncountTable[], Definitions.EncountSlot[]) GetMapEncounter(string mapName);
        void SaveMapEncounter(string mapName, Definitions.EncountTable[] encountTables, Definitions.EncountSlot[] encountCharas);

        // IShopConfig -> ShopConfig
        // IShopValidCondition -> ShopValidCondition
        (ShopConfig[], ShopValidCondition[]) GetShop(string shopName);
        void SaveShop(string shopName, ShopConfig[] shopConfigs, ShopValidCondition[] shopValidConditions);

        ICombineConfig[] GetFusions();
        void SaveFusions(ICombineConfig[] combineConfigs);

        // IItableDataMore -> ItableDataMore
        string[] GetMapWhoContainsTreasureBoxes();
        ItableDataMore[] GetTreasureBox(string mapName);
        void SaveTreasureBox(string mapName, ItableDataMore[] itableDataMores);

        // ICapsuleConfig -> CapsuleConfig
        CapsuleConfig[] GetCapsuleConfigs();
        void SaveCapsuleConfigs(CapsuleConfig[] capsuleConfigs);
    }
}