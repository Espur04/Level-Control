using Colossal;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace Level_Control
{
    [FileLocation("Level_Control_Settings")]
    [SettingsUITabOrder(kMainTab)]
    [SettingsUIGroupOrder(kLockGroup, kPlacementGroup, kInformationGroup)]
    [SettingsUIShowGroupName(kLockGroup, kPlacementGroup, kInformationGroup)]
    public sealed class Setting : ModSetting
    {
        public const string kMainTab = "Buildings";
        public const string kLockGroup = "Locking";
        public const string kPlacementGroup = "Placement";
        public const string kInformationGroup = "Information";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUIButton]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsNotInGame))]
        [SettingsUISection(kMainTab, kLockGroup)]
        public bool LockAllBuildings
        {
            set => LevelControlSystem.Instance?.RequestLockAllBuildings();
        }

        [SettingsUISection(kMainTab, kPlacementGroup)]
        public bool LockAtPlacement { get; set; }

        [SettingsUISection(kMainTab, kInformationGroup)]
        public string Version => Mod.kVersion;

        public bool IsNotInGame() => GameManager.instance.gameMode != GameMode.Game;

        public override void SetDefaults()
        {
            LockAtPlacement = false;
        }
    }

    public sealed class Locale : IDictionarySource
    {
        private readonly Setting _setting;
        private readonly bool _french;

        public Locale(Setting setting, bool french)
        {
            _setting = setting;
            _french = french;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            string settings = "LEVEL CONTROL";
            string tab = _french ? "Bâtiments" : "Buildings";
            string lockGroup = _french ? "Verrouillage" : "Locking";
            string placementGroup = _french ? "Placement" : "Placement";
            string informationGroup = _french ? "Informations" : "Information";
            string lockAllLabel = _french ? "Verrouiller tous les bâtiments" : "Lock all buildings";
            string lockAllDescription = _french ? "Empêche les bâtiments existants de changer automatiquement de niveau." : "Prevents existing buildings from automatically changing level.";
            string lockLabel = _french ? "Verrouiller le niveau à la pose" : "Lock level when placed";
            string lockDescription = _french ? "Verrouille les nouveaux bâtiments zonables à leur niveau au moment de leur pose." : "Locks newly placed growable buildings at their current level.";
            string versionLabel = "Version";
            string versionDescription = _french ? "Version installée de Level Control." : "Installed version of Level Control.";
            string currentLevel = _french ? "Niveau actuel" : "Current level";
            string targetLevel = _french ? "Choisir le niveau" : "Choose level";
            string lockBuilding = _french ? "Verrouiller le niveau" : "Lock level";
            string unlockBuilding = _french ? "Déverrouiller le niveau" : "Unlock level";
            string openLevelControl = _french ? "Ouvrir le contrôle du niveau" : "Open level control";
            string panelTitle = _french ? "Niveau du bâtiment" : "Building level";
            string panelLevelGroup = _french ? "NIVEAU DU BÂTIMENT" : "BUILDING LEVEL";
            string levelLabel = _french ? "Niveau" : "Level";

            return new Dictionary<string, string>
            {
                { "Options.SECTION[Level_Control]", settings },
                { _setting.GetSettingsLocaleID(), settings },
                { _setting.GetOptionTabLocaleID(Setting.kMainTab), tab },
                { _setting.GetOptionGroupLocaleID(Setting.kLockGroup), lockGroup },
                { _setting.GetOptionGroupLocaleID(Setting.kPlacementGroup), placementGroup },
                { _setting.GetOptionGroupLocaleID(Setting.kInformationGroup), informationGroup },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.LockAllBuildings)), lockAllLabel },
                { _setting.GetOptionDescLocaleID(nameof(Setting.LockAllBuildings)), lockAllDescription },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.LockAtPlacement)), lockLabel },
                { _setting.GetOptionDescLocaleID(nameof(Setting.LockAtPlacement)), lockDescription },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.Version)), versionLabel },
                { _setting.GetOptionDescLocaleID(nameof(Setting.Version)), versionDescription },
                { "Level_Control.UI.CurrentLevel", currentLevel },
                { "Level_Control.UI.ChooseLevel", targetLevel },
                { "Level_Control.UI.LockLevel", lockBuilding },
                { "Level_Control.UI.UnlockLevel", unlockBuilding },
                { "Level_Control.UI.OpenLevelControl", openLevelControl },
                { "Level_Control.UI.PanelTitle", panelTitle },
                { "Level_Control.UI.LevelGroup", panelLevelGroup },
                { "Level_Control.UI.LevelLabel", levelLabel }
            };
        }

        public void Unload()
        {
        }
    }
}
