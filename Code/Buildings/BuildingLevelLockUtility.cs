using Colossal.Entities;
using Game.Buildings;
using Game.Economy;
using Game.Notifications;
using Game.Prefabs;
using Unity.Entities;

namespace Level_Control
{
    internal static class BuildingLevelLockUtility
    {
        internal static bool HasPendingUpgrade(EntityManager entityManager, Entity building)
        {
            return entityManager.HasComponent<ResourceNeeding>(building)
                || entityManager.HasComponent<BuildingUpgradeElement>(building);
        }

        internal static void ClearPendingUpgrade(EntityManager entityManager, Entity building, IconCommandBuffer iconCommandBuffer, Entity notificationPrefab, bool resetCondition)
        {
            if (resetCondition && entityManager.TryGetComponent(building, out BuildingCondition condition))
            {
                condition.m_Condition = 0;
                entityManager.SetComponentData(building, condition);
            }

            if (entityManager.TryGetBuffer<ResourceNeeding>(building, false, out DynamicBuffer<ResourceNeeding> resourceNeeding))
                entityManager.RemoveComponent<ResourceNeeding>(building);

            if (entityManager.TryGetBuffer<BuildingUpgradeElement>(building, false, out DynamicBuffer<BuildingUpgradeElement> buildingUpgrades))
                entityManager.RemoveComponent<BuildingUpgradeElement>(building);

            if (notificationPrefab != Entity.Null)
                iconCommandBuffer.Remove(building, notificationPrefab);
        }
    }
}
