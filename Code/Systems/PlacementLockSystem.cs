using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;

namespace Level_Control
{
    public sealed partial class PlacementLockSystem : GameSystemBase
    {
        private EntityQuery m_UnprocessedBuildings;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_UnprocessedBuildings = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<LevelControlProcessed>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Signature>(),
                ComponentType.Exclude<UnderConstruction>());
            RequireForUpdate(m_UnprocessedBuildings);
        }

        protected override void OnUpdate()
        {
            NativeArray<Entity> buildings = m_UnprocessedBuildings.ToEntityArray(Allocator.Temp);
            LevelControlSystem levelControl = LevelControlSystem.Instance;
            bool refreshSelection = false;

            foreach (Entity building in buildings)
            {
                if (Mod.setting != null && Mod.setting.LockAtPlacement && levelControl != null && levelControl.IsLevelable(building))
                    refreshSelection |= levelControl.SetLocked(building, true);

                EntityManager.AddComponent<LevelControlProcessed>(building);
            }

            buildings.Dispose();

            if (refreshSelection)
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>().SetDirty();
        }
    }
}
