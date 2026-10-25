using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Economy;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI.InGame;
using Game.Zones;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using PrefabBuildingFlags = Game.Prefabs.BuildingFlags;
using RuntimeBuildingFlags = Game.Buildings.BuildingFlags;

namespace Level_Control
{
    public sealed partial class LevelControlSystem : GameSystemBase
    {
        private enum RequestType
        {
            SetLock,
            LockAll
        }

        private readonly struct Request
        {
            public readonly RequestType Type;
            public readonly Entity Building;
            public readonly bool Locked;
            public readonly long ReadyAtTicks;

            public Request(RequestType type, Entity building, bool locked = false)
            {
                Type = type;
                Building = building;
                Locked = locked;
                ReadyAtTicks = DateTime.UtcNow.Ticks;
            }
        }

        private readonly struct PendingLevel
        {
            public readonly int Level;
            public readonly long ReadyAtTicks;

            public PendingLevel(int level, long readyAtTicks)
            {
                Level = level;
                ReadyAtTicks = readyAtTicks;
            }
        }

        private readonly struct BuildingProfile
        {
            public readonly PrefabKey Key;
            public readonly byte CurrentLevel;
            public readonly int ResidentialProperties;

            public BuildingProfile(PrefabKey key, byte currentLevel, int residentialProperties)
            {
                Key = key;
                CurrentLevel = currentLevel;
                ResidentialProperties = residentialProperties;
            }
        }

        private readonly struct PrefabCandidate
        {
            public readonly Entity Prefab;
            public readonly int ResidentialProperties;

            public PrefabCandidate(Entity prefab, int residentialProperties)
            {
                Prefab = prefab;
                ResidentialProperties = residentialProperties;
            }
        }

        private readonly struct PrefabKey : IEquatable<PrefabKey>
        {
            public readonly ZoneType ZoneType;
            public readonly int LotWidth;
            public readonly int LotDepth;
            public readonly byte AccessFlags;
            public readonly Resource AllowedSold;
            public readonly Resource AllowedInput;
            public readonly Resource AllowedManufactured;
            public readonly Resource AllowedStored;

            public PrefabKey(ZoneType zoneType, int lotWidth, int lotDepth, byte accessFlags, BuildingPropertyData properties)
            {
                ZoneType = zoneType;
                LotWidth = lotWidth;
                LotDepth = lotDepth;
                AccessFlags = accessFlags;
                AllowedSold = properties.m_AllowedSold;
                AllowedInput = properties.m_AllowedInput;
                AllowedManufactured = properties.m_AllowedManufactured;
                AllowedStored = properties.m_AllowedStored;
            }

            private PrefabKey(ZoneType zoneType, int lotWidth, int lotDepth, byte accessFlags, Resource allowedSold, Resource allowedInput, Resource allowedManufactured, Resource allowedStored)
            {
                ZoneType = zoneType;
                LotWidth = lotWidth;
                LotDepth = lotDepth;
                AccessFlags = accessFlags;
                AllowedSold = allowedSold;
                AllowedInput = allowedInput;
                AllowedManufactured = allowedManufactured;
                AllowedStored = allowedStored;
            }

            public bool Equals(PrefabKey other)
            {
                return ZoneType.Equals(other.ZoneType)
                    && LotWidth == other.LotWidth
                    && LotDepth == other.LotDepth
                    && AccessFlags == other.AccessFlags
                    && AllowedSold == other.AllowedSold
                    && AllowedInput == other.AllowedInput
                    && AllowedManufactured == other.AllowedManufactured
                    && AllowedStored == other.AllowedStored;
            }

            public override bool Equals(object obj) => obj is PrefabKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ZoneType.GetHashCode();
                    hash = (hash * 397) ^ LotWidth;
                    hash = (hash * 397) ^ LotDepth;
                    hash = (hash * 397) ^ AccessFlags;
                    hash = (hash * 397) ^ (int)AllowedSold;
                    hash = (hash * 397) ^ (int)AllowedInput;
                    hash = (hash * 397) ^ (int)AllowedManufactured;
                    hash = (hash * 397) ^ (int)AllowedStored;
                    return hash;
                }
            }
        }

        private static readonly object s_RequestLock = new object();
        private static readonly List<Request> s_Requests = new List<Request>();
        private const int kPrefabBatchSize = 1024;
        private const int kBuildingBatchSize = 128;
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_PrefabQuery;
        private EntityQuery m_BuildingConfigurationQuery;
        private IconCommandSystem m_IconCommandSystem;
        private NativeArray<Entity> m_PrefabsBeingIndexed;
        private Dictionary<PrefabKey, List<PrefabCandidate>> m_PrefabCatalog;
        private Dictionary<PrefabKey, byte> m_AvailableLevelMasks;
        private Dictionary<Entity, PendingLevel> m_PendingLevels;
        private Dictionary<Entity, bool> m_PendingLocks;
        private List<Entity> m_PendingLevelEntities;
        private NativeArray<Entity> m_BulkBuildings;
        private int m_PrefabIndex;
        private int m_BulkIndex;
        private int m_BulkChanged;
        private bool m_LockAllPending;
        private bool m_CatalogReady;

        public static LevelControlSystem Instance { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_BuildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Signature>(),
                ComponentType.Exclude<UnderConstruction>());
            m_PrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<PrefabData>(),
                ComponentType.ReadOnly<BuildingSpawnGroupData>(),
                ComponentType.ReadOnly<SpawnableBuildingData>(),
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<BuildingPropertyData>());
            m_BuildingConfigurationQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            m_PrefabCatalog = new Dictionary<PrefabKey, List<PrefabCandidate>>();
            m_AvailableLevelMasks = new Dictionary<PrefabKey, byte>();
            m_PendingLevels = new Dictionary<Entity, PendingLevel>();
            m_PendingLocks = new Dictionary<Entity, bool>();
            m_PendingLevelEntities = new List<Entity>();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode != GameMode.Game && mode != GameMode.Editor)
                return;

            lock (s_RequestLock)
            {
                s_Requests.Clear();
                m_PendingLevels.Clear();
                m_PendingLocks.Clear();
                m_PendingLevelEntities.Clear();
                m_LockAllPending = false;
            }
            if (m_BulkBuildings.IsCreated)
            {
                m_BulkBuildings.Dispose();
                m_BulkBuildings = default;
            }

            StartCatalogBuild();
            while (!m_CatalogReady)
                BuildCatalogSlice();
        }

        protected override void OnUpdate()
        {
            if (!m_CatalogReady)
            {
                if (!m_PrefabsBeingIndexed.IsCreated)
                    StartCatalogBuild();

                BuildCatalogSlice();
                return;
            }

            if (m_BulkBuildings.IsCreated)
            {
                ProcessLockAllBatch();
                return;
            }

            if (TryGetReadyPendingLevel(out Entity pendingBuilding, out int pendingLevel))
            {
                ProcessPendingLevel(pendingBuilding, pendingLevel);
                return;
            }

            if (!TryTakeReadyRequest(out Request request))
                return;

            if (request.Type == RequestType.SetLock)
            {
                SetLocked(request.Building, request.Locked);
                lock (s_RequestLock)
                {
                    if (m_PendingLocks.TryGetValue(request.Building, out bool pendingLocked) && pendingLocked == request.Locked)
                        m_PendingLocks.Remove(request.Building);
                }
                RefreshSelectedInfo();
                return;
            }

            StartLockAll();
        }

        private bool TryTakeReadyRequest(out Request request)
        {
            request = default;
            long now = DateTime.UtcNow.Ticks;

            lock (s_RequestLock)
            {
                for (int i = 0; i < s_Requests.Count; i++)
                {
                    if (s_Requests[i].ReadyAtTicks > now)
                        continue;

                    request = s_Requests[i];
                    s_Requests.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private bool TryGetReadyPendingLevel(out Entity building, out int level)
        {
            building = Entity.Null;
            level = 0;
            long now = DateTime.UtcNow.Ticks;

            lock (s_RequestLock)
            {
                for (int i = 0; i < m_PendingLevelEntities.Count; i++)
                {
                    Entity candidate = m_PendingLevelEntities[i];
                    if (!m_PendingLevels.TryGetValue(candidate, out PendingLevel pending))
                    {
                        m_PendingLevelEntities.RemoveAt(i--);
                        continue;
                    }

                    if (pending.ReadyAtTicks > now)
                        continue;

                    if (!EntityManager.Exists(candidate))
                    {
                        m_PendingLevels.Remove(candidate);
                        m_PendingLevelEntities.RemoveAt(i--);
                        continue;
                    }

                    if (EntityManager.HasComponent<UnderConstruction>(candidate))
                        continue;

                    building = candidate;
                    level = pending.Level;
                    return true;
                }
            }

            return false;
        }

        private void ProcessPendingLevel(Entity building, int targetLevel)
        {
            if (!TryGetCurrentLevel(building, out int currentLevel))
            {
                RemovePendingLevel(building);
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>().SetDirty();
                return;
            }

            if (currentLevel == targetLevel)
            {
                RemovePendingLevel(building);
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>().SetDirty();
                return;
            }

            if (!ApplyLevel(building, targetLevel))
            {
                RemovePendingLevel(building);
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>().SetDirty();
            }
        }

        private void RemovePendingLevel(Entity building)
        {
            lock (s_RequestLock)
            {
                m_PendingLevels.Remove(building);
                m_PendingLevelEntities.Remove(building);
            }
        }

        private void StartLockAll()
        {
            m_BulkBuildings = m_BuildingQuery.ToEntityArray(Allocator.Persistent);
            m_BulkIndex = 0;
            m_BulkChanged = 0;
            if (m_BulkBuildings.Length == 0)
                FinishLockAll();
        }

        private void ProcessLockAllBatch()
        {
            int end = Math.Min(m_BulkIndex + kBuildingBatchSize, m_BulkBuildings.Length);
            for (; m_BulkIndex < end; m_BulkIndex++)
            {
                if (SetLocked(m_BulkBuildings[m_BulkIndex], true))
                    m_BulkChanged++;
            }

            if (m_BulkIndex < m_BulkBuildings.Length)
                return;

            FinishLockAll();
        }

        private void FinishLockAll()
        {
            m_BulkBuildings.Dispose();
            m_BulkBuildings = default;
            m_LockAllPending = false;
            Mod.log.Info($"Locked {m_BulkChanged} buildings.");
            RefreshSelectedInfo();
        }

        protected override void OnDestroy()
        {
            if (m_PrefabsBeingIndexed.IsCreated)
                m_PrefabsBeingIndexed.Dispose();
            if (m_BulkBuildings.IsCreated)
                m_BulkBuildings.Dispose();
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
        }

        public void RequestLevel(Entity building, int level)
        {
            if (level < 1 || level > 5 || building == Entity.Null)
                return;

            lock (s_RequestLock)
            {
                if (!m_PendingLevels.ContainsKey(building))
                    m_PendingLevelEntities.Add(building);

                m_PendingLevels[building] = new PendingLevel(
                    level,
                    DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond / 5);
            }

            RefreshSelectedInfo();
        }

        public int GetTargetLevel(Entity building, int currentLevel)
        {
            lock (s_RequestLock)
                return m_PendingLevels.TryGetValue(building, out PendingLevel pending) ? pending.Level : currentLevel;
        }

        public void RequestToggleLock(Entity building)
        {
            if (building == Entity.Null)
                return;

            lock (s_RequestLock)
            {
                bool current = m_PendingLocks.TryGetValue(building, out bool pendingLocked)
                    ? pendingLocked
                    : IsLockedNative(building);
                bool requestedLocked = !current;
                m_PendingLocks[building] = requestedLocked;
                s_Requests.RemoveAll(request => request.Type == RequestType.SetLock && request.Building == building);
                s_Requests.Add(new Request(RequestType.SetLock, building, requestedLocked));
            }

            RefreshSelectedInfo();
        }

        public void RequestLockAllBuildings()
        {
            lock (s_RequestLock)
            {
                s_Requests.RemoveAll(request => request.Type == RequestType.SetLock);
                m_PendingLocks.Clear();
                m_LockAllPending = true;
                if (!s_Requests.Exists(request => request.Type == RequestType.LockAll))
                    s_Requests.Add(new Request(RequestType.LockAll, Entity.Null));
            }

            RefreshSelectedInfo();
        }

        public bool TryGetCurrentLevel(Entity building, out int level)
        {
            level = 0;
            if (!EntityManager.Exists(building)
                || !EntityManager.TryGetComponent(building, out PrefabRef prefabRef)
                || !EntityManager.Exists(prefabRef.m_Prefab)
                || !EntityManager.TryGetComponent(prefabRef.m_Prefab, out SpawnableBuildingData spawnableBuildingData))
                return false;

            level = spawnableBuildingData.m_Level;
            return true;
        }

        public int[] GetAvailableLevels(Entity building)
        {
            if (!TryCreateProfile(building, out BuildingProfile profile))
                return Array.Empty<int>();

            if (!m_AvailableLevelMasks.TryGetValue(profile.Key, out byte levelMask))
                return new[] { (int)profile.CurrentLevel };

            int count = 0;
            for (int level = 1; level <= 5; level++)
                if ((levelMask & (1 << level)) != 0)
                    count++;

            if ((levelMask & (1 << profile.CurrentLevel)) == 0)
                count++;

            int[] result = new int[count];
            int index = 0;
            for (int level = 1; level <= 5; level++)
                if ((levelMask & (1 << level)) != 0)
                    result[index++] = level;

            if ((levelMask & (1 << profile.CurrentLevel)) == 0)
                result[index] = profile.CurrentLevel;

            return result;
        }

        public bool IsLocked(Entity building)
        {
            lock (s_RequestLock)
            {
                if (m_PendingLocks.TryGetValue(building, out bool pendingLocked))
                    return pendingLocked;
                if (m_LockAllPending)
                    return true;
            }

            return IsLockedNative(building);
        }

        private bool IsLockedNative(Entity building)
        {
            return EntityManager.Exists(building)
                && EntityManager.TryGetComponent(building, out Building data)
                && (data.m_Flags & RuntimeBuildingFlags.Historical) != 0;
        }

        private void RefreshSelectedInfo()
        {
            SelectedInfoUISystem selectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            selectedInfo.SetDirty();
            selectedInfo.RequestUpdate();
        }

        internal bool IsLevelable(Entity building)
        {
            return EntityManager.Exists(building)
                && EntityManager.HasComponent<Building>(building)
                && EntityManager.TryGetComponent(building, out PrefabRef prefabRef)
                && EntityManager.Exists(prefabRef.m_Prefab)
                && EntityManager.HasComponent<SpawnableBuildingData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingPropertyData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingSpawnGroupData>(prefabRef.m_Prefab);
        }

        internal bool SetLocked(Entity entity, bool locked)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.TryGetComponent(entity, out Building building))
                return false;

            bool currentlyLocked = (building.m_Flags & RuntimeBuildingFlags.Historical) != 0;
            if (currentlyLocked == locked)
            {
                if (locked)
                    ClearPendingUpgrade(entity, true);
                return false;
            }

            if (locked)
                building.m_Flags |= RuntimeBuildingFlags.Historical;
            else
                building.m_Flags &= ~RuntimeBuildingFlags.Historical;

            EntityManager.SetComponentData(entity, building);
            if (locked)
                ClearPendingUpgrade(entity, true);
            return true;
        }

        private void ClearPendingUpgrade(Entity building, bool resetCondition)
        {
            Entity notificationPrefab = Entity.Null;
            IconCommandBuffer iconCommandBuffer = default;

            if (BuildingLevelLockUtility.HasPendingUpgrade(EntityManager, building) && !m_BuildingConfigurationQuery.IsEmpty)
            {
                notificationPrefab = m_BuildingConfigurationQuery.GetSingleton<BuildingConfigurationData>().m_LevelingBuildingNotificationPrefab;
                if (notificationPrefab != Entity.Null)
                    iconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer();
            }

            BuildingLevelLockUtility.ClearPendingUpgrade(EntityManager, building, iconCommandBuffer, notificationPrefab, resetCondition);
        }

        private bool ApplyLevel(Entity building, int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > 5
                || !EntityManager.Exists(building)
                || EntityManager.HasComponent<UnderConstruction>(building)
                || EntityManager.HasBuffer<ResourceNeeding>(building)
                || !TryCreateProfile(building, out BuildingProfile profile)
                || profile.CurrentLevel == targetLevel
                || !TryChoosePrefab(building, profile, (byte)targetLevel, out Entity prefab))
                return false;

            EntityManager.AddComponentData(building, new UnderConstruction
            {
                m_NewPrefab = prefab,
                m_Progress = byte.MaxValue
            });
            return true;
        }

        private bool TryCreateProfile(Entity building, out BuildingProfile profile)
        {
            profile = default;
            if (!EntityManager.Exists(building)
                || !EntityManager.TryGetComponent(building, out PrefabRef prefabRef)
                || !EntityManager.Exists(prefabRef.m_Prefab)
                || !EntityManager.TryGetComponent(prefabRef.m_Prefab, out SpawnableBuildingData spawnable)
                || !EntityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingData buildingData)
                || !EntityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingPropertyData propertyData)
                || !EntityManager.HasComponent<BuildingSpawnGroupData>(prefabRef.m_Prefab))
                return false;

            BuildingSpawnGroupData spawnGroup = EntityManager.GetSharedComponent<BuildingSpawnGroupData>(prefabRef.m_Prefab);
            byte accessFlags = (byte)(buildingData.m_Flags & (PrefabBuildingFlags.LeftAccess | PrefabBuildingFlags.RightAccess));
            PrefabKey key = new PrefabKey(spawnGroup.m_ZoneType, buildingData.m_LotSize.x, buildingData.m_LotSize.y, accessFlags, propertyData);
            profile = new BuildingProfile(key, spawnable.m_Level, propertyData.m_ResidentialProperties);
            return true;
        }

        private void StartCatalogBuild()
        {
            if (m_PrefabsBeingIndexed.IsCreated)
                m_PrefabsBeingIndexed.Dispose();

            m_PrefabCatalog.Clear();
            m_AvailableLevelMasks.Clear();
            m_PrefabIndex = 0;
            m_CatalogReady = false;
            m_PrefabsBeingIndexed = m_PrefabQuery.ToEntityArray(Allocator.Persistent);
        }

        private void BuildCatalogSlice()
        {
            int end = Math.Min(m_PrefabIndex + kPrefabBatchSize, m_PrefabsBeingIndexed.Length);
            for (; m_PrefabIndex < end; m_PrefabIndex++)
            {
                Entity prefab = m_PrefabsBeingIndexed[m_PrefabIndex];
                if (!EntityManager.Exists(prefab)
                    || !EntityManager.TryGetComponent(prefab, out SpawnableBuildingData spawnable)
                    || spawnable.m_Level < 1 || spawnable.m_Level > 5
                    || !EntityManager.TryGetComponent(prefab, out BuildingData buildingData)
                    || !EntityManager.TryGetComponent(prefab, out BuildingPropertyData propertyData)
                    || !EntityManager.HasComponent<BuildingSpawnGroupData>(prefab))
                    continue;

                BuildingSpawnGroupData spawnGroup = EntityManager.GetSharedComponent<BuildingSpawnGroupData>(prefab);
                byte accessFlags = (byte)(buildingData.m_Flags & (PrefabBuildingFlags.LeftAccess | PrefabBuildingFlags.RightAccess));
                PrefabKey key = new PrefabKey(spawnGroup.m_ZoneType, buildingData.m_LotSize.x, buildingData.m_LotSize.y, accessFlags, propertyData);

                if (!m_PrefabCatalog.TryGetValue(key, out List<PrefabCandidate> candidates))
                {
                    candidates = new List<PrefabCandidate>();
                    m_PrefabCatalog.Add(key, candidates);
                }

                candidates.Add(new PrefabCandidate(prefab, propertyData.m_ResidentialProperties));
                m_AvailableLevelMasks[key] = (byte)(m_AvailableLevelMasks.TryGetValue(key, out byte mask)
                    ? mask | (1 << spawnable.m_Level)
                    : 1 << spawnable.m_Level);
            }

            if (m_PrefabIndex < m_PrefabsBeingIndexed.Length)
                return;

            foreach (List<PrefabCandidate> candidates in m_PrefabCatalog.Values)
                candidates.Sort((left, right) => left.ResidentialProperties.CompareTo(right.ResidentialProperties));

            m_PrefabsBeingIndexed.Dispose();
            m_CatalogReady = true;
        }

        private bool TryChoosePrefab(Entity building, BuildingProfile profile, byte targetLevel, out Entity prefab)
        {
            prefab = Entity.Null;
            if (!m_CatalogReady
                || !m_PrefabCatalog.TryGetValue(profile.Key, out List<PrefabCandidate> candidates))
                return false;

            int compatibleCount = 0;
            int highestLowerCapacity = -1;
            int highestLowerCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                PrefabCandidate candidate = candidates[i];
                if (!EntityManager.Exists(candidate.Prefab)
                    || !EntityManager.TryGetComponent(candidate.Prefab, out SpawnableBuildingData spawnable)
                    || spawnable.m_Level != targetLevel)
                    continue;

                if (candidate.ResidentialProperties >= profile.ResidentialProperties)
                    compatibleCount++;
                else if (candidate.ResidentialProperties > highestLowerCapacity)
                {
                    highestLowerCapacity = candidate.ResidentialProperties;
                    highestLowerCount = 1;
                }
                else if (candidate.ResidentialProperties == highestLowerCapacity)
                {
                    highestLowerCount++;
                }
            }

            uint selector = unchecked((uint)(building.Index * 397) ^ targetLevel);
            if (compatibleCount > 0)
            {
                int selected = (int)(selector % (uint)compatibleCount);
                for (int i = 0; i < candidates.Count; i++)
                {
                    PrefabCandidate candidate = candidates[i];
                    if (!EntityManager.Exists(candidate.Prefab)
                        || !EntityManager.TryGetComponent(candidate.Prefab, out SpawnableBuildingData spawnable)
                        || spawnable.m_Level != targetLevel
                        || candidate.ResidentialProperties < profile.ResidentialProperties)
                        continue;

                    if (selected-- == 0)
                    {
                        prefab = candidate.Prefab;
                        return true;
                    }
                }
            }

            if (targetLevel >= profile.CurrentLevel || highestLowerCount == 0)
                return false;

            int selectedLower = (int)(selector % (uint)highestLowerCount);
            for (int i = 0; i < candidates.Count; i++)
            {
                PrefabCandidate candidate = candidates[i];
                if (!EntityManager.Exists(candidate.Prefab)
                    || !EntityManager.TryGetComponent(candidate.Prefab, out SpawnableBuildingData spawnable)
                    || spawnable.m_Level != targetLevel
                    || candidate.ResidentialProperties != highestLowerCapacity)
                    continue;

                if (selectedLower-- == 0)
                {
                    prefab = candidate.Prefab;
                    return true;
                }
            }

            return false;
        }
    }
}
