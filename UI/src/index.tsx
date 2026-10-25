import { ModRegistrar } from "cs2/modding";
import { LevelControlSection } from "mods/LevelControlSection";

const register: ModRegistrar = moduleRegistry => {
  moduleRegistry.extend(
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
    "selectedInfoSectionComponents",
    LevelControlSection
  );
};

export default register;
