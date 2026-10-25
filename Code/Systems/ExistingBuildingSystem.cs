using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Economy;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using RuntimeBuildingFlags = Game.Buildings.BuildingFlags;

namespace Level_Control
{
    public sealed partial class ExistingBuildingSystem : GameSystemBase
    {
        private EntityQuery m_UnprocessedBuildings;
        private EntityQuery m_PendingUpgradeBuildings;
        private EntityQuery m_BuildingConfigurationQuery;
        private IconCommandSystem m_IconCommandSystem;

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
                ComponentType.Exclude<Signature>());
            m_PendingUpgradeBuildings = SystemAPI.QueryBuilder()
                .WithAll<Building>()
                .WithAny<BuildingUpgradeElement, ResourceNeeding>()
                .WithNone<Temp, Deleted, Destroyed, Signature>()
                .Build();
            m_BuildingConfigurationQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            RequireAnyForUpdate(m_UnprocessedBuildings, m_PendingUpgradeBuildings);
        }

        protected override void OnUpdate()
        {
            if (!m_UnprocessedBuildings.IsEmpty)
                EntityManager.AddComponent<LevelControlProcessed>(m_UnprocessedBuildings);

            if (m_PendingUpgradeBuildings.IsEmpty)
                return;

            Entity notificationPrefab = Entity.Null;
            IconCommandBuffer iconCommandBuffer = default;
            if (!m_BuildingConfigurationQuery.IsEmpty)
            {
                notificationPrefab = m_BuildingConfigurationQuery.GetSingleton<BuildingConfigurationData>().m_LevelingBuildingNotificationPrefab;
                if (notificationPrefab != Entity.Null)
                    iconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer();
            }

            NativeArray<Entity> buildings = m_PendingUpgradeBuildings.ToEntityArray(Allocator.Temp);
            foreach (Entity buildingEntity in buildings)
            {
                if (!EntityManager.TryGetComponent(buildingEntity, out Building building)
                    || (building.m_Flags & RuntimeBuildingFlags.Historical) == 0)
                    continue;

                BuildingLevelLockUtility.ClearPendingUpgrade(EntityManager, buildingEntity, iconCommandBuffer, notificationPrefab, false);
            }
            buildings.Dispose();
        }
    }
}
