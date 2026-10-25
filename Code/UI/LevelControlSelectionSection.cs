using Colossal.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using Game.UI.InGame;
using System.Collections;
using System.Reflection;
using Unity.Entities;

namespace Level_Control
{
    public sealed partial class LevelControlSelectionSection : InfoSectionBase
    {
        private int m_CurrentLevel;
        private int m_TargetLevel;
        private int[] m_AvailableLevels = System.Array.Empty<int>();
        private bool m_Locked;

        protected override string group => "Level_Control";

        protected override bool displayForUpgrades => true;

        protected override void OnCreate()
        {
            base.OnCreate();
            FieldInfo middleSectionsField = typeof(SelectedInfoUISystem).GetField("m_MiddleSections", BindingFlags.Instance | BindingFlags.NonPublic);
            if (middleSectionsField?.GetValue(m_InfoUISystem) is IList middleSections)
            {
                int levelSectionIndex = -1;
                for (int i = 0; i < middleSections.Count; i++)
                {
                    if (middleSections[i]?.GetType().FullName == "Game.UI.InGame.LevelSection")
                    {
                        levelSectionIndex = i;
                        break;
                    }
                }

                if (levelSectionIndex >= 0)
                    middleSections.Insert(levelSectionIndex + 1, this);
                else
                    middleSections.Add(this);
            }
            else
            {
                m_InfoUISystem.AddMiddleSection(this);
            }

            m_InfoUISystem.SetDirty();
            m_InfoUISystem.RequestUpdate();
            AddBinding(new TriggerBinding<int>("Level_Control", "SetLevel", SetLevel));
            AddBinding(new TriggerBinding("Level_Control", "ToggleLock", ToggleLock));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            visible = IsSupportedSelection();
        }

        protected override void Reset()
        {
            m_CurrentLevel = 0;
            m_TargetLevel = 0;
            m_AvailableLevels = System.Array.Empty<int>();
            m_Locked = false;
        }

        protected override void OnProcess()
        {
            LevelControlSystem system = LevelControlSystem.Instance;
            if (system == null || !system.TryGetCurrentLevel(selectedEntity, out m_CurrentLevel))
            {
                m_CurrentLevel = 0;
                m_TargetLevel = 0;
                m_AvailableLevels = System.Array.Empty<int>();
                m_Locked = false;
                return;
            }

            m_AvailableLevels = system.GetAvailableLevels(selectedEntity);
            m_TargetLevel = system.GetTargetLevel(selectedEntity, m_CurrentLevel);
            m_Locked = system.IsLocked(selectedEntity);
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("currentLevel");
            writer.Write(m_CurrentLevel);
            writer.PropertyName("targetLevel");
            writer.Write(m_TargetLevel);
            writer.PropertyName("availableLevels");
            writer.ArrayBegin(m_AvailableLevels.Length);
            for (int i = 0; i < m_AvailableLevels.Length; i++)
                writer.Write(m_AvailableLevels[i]);
            writer.ArrayEnd();
            writer.PropertyName("locked");
            writer.Write(m_Locked);
            writer.PropertyName("buildingEntityIndex");
            writer.Write(selectedEntity.Index);
            writer.PropertyName("buildingEntityVersion");
            writer.Write(selectedEntity.Version);
        }

        private bool IsSupportedSelection()
        {
            return selectedEntity != Entity.Null
                && EntityManager.Exists(selectedEntity)
                && EntityManager.HasComponent<Building>(selectedEntity)
                && EntityManager.TryGetComponent(selectedEntity, out PrefabRef prefabRef)
                && EntityManager.Exists(prefabRef.m_Prefab)
                && EntityManager.HasComponent<SpawnableBuildingData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingPropertyData>(prefabRef.m_Prefab)
                && EntityManager.HasComponent<BuildingSpawnGroupData>(prefabRef.m_Prefab);
        }

        private void SetLevel(int level)
        {
            LevelControlSystem.Instance?.RequestLevel(selectedEntity, level);
        }

        private void ToggleLock()
        {
            LevelControlSystem.Instance?.RequestToggleLock(selectedEntity);
        }
    }
}
