using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI.InGame;

namespace Level_Control
{
    public sealed class Mod : IMod
    {
        public const string kVersion = "1.0.0";
        public static Setting setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            setting = new Setting(this);
            AssetDatabase.global.LoadSettings("Level_Control_Settings", setting, new Setting(this));
            string[] locales = { "en-US", "en-GB", "fr-FR", "de-DE", "es-ES", "it-IT", "pl-PL", "pt-BR", "ru-RU", "ko-KR", "zh-HANS", "zh-HANT", "ja-JP" };
            foreach (string locale in locales)
                GameManager.instance.localizationManager.AddSource(locale, new Locale(setting, locale == "fr-FR"));
            setting.RegisterInOptionsUI();

            updateSystem.UpdateAt<LevelControlSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAfter<ExistingBuildingSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateBefore<LevelControlSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAfter<PlacementLockSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAfter<LevelControlSelectionSection, SelectedInfoUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            setting?.UnregisterInOptionsUI();
            setting = null;
        }
    }
}
