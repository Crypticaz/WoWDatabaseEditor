﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WDE.Module.Attributes;

namespace WDE.Common.Database
{
    [UniqueProvider]
    public interface IDatabaseProvider
    {
        bool IsConnected { get; }
        
        ICreatureTemplate? GetCreatureTemplate(uint entry);
        IEnumerable<ICreatureTemplate> GetCreatureTemplates();

        IGameObjectTemplate? GetGameObjectTemplate(uint entry);
        IEnumerable<IGameObjectTemplate> GetGameObjectTemplates();

        IEnumerable<IAreaTriggerTemplate> GetAreaTriggerTemplates();

        IEnumerable<ICreatureClassLevelStat> GetCreatureClassLevelStats();

        IQuestTemplate? GetQuestTemplate(uint entry);
        IEnumerable<IQuestTemplate> GetQuestTemplates();

        IEnumerable<IGameEvent> GetGameEvents();
        IEnumerable<IConversationTemplate> GetConversationTemplates();
        IEnumerable<IGossipMenu> GetGossipMenus();
        IEnumerable<INpcText> GetNpcTexts();

        IEnumerable<ISmartScriptLine> GetScriptFor(int entryOrGuid, SmartScriptType type);

        Task InstallScriptFor(int entryOrGuid, SmartScriptType type, IEnumerable<ISmartScriptLine> script);
        Task InstallConditions(IEnumerable<IConditionLine> conditions, ConditionKeyMask keyMask, ConditionKey? manualKey = null);

        IEnumerable<IConditionLine> GetConditionsFor(int sourceType, int sourceEntry, int sourceId);

        Task<IList<IConditionLine>> GetConditionsForAsync(ConditionKeyMask keyMask, ConditionKey manualKey);
        
        IEnumerable<ISpellScriptName> GetSpellScriptNames(int spellId);

        Task<IList<int>> GetSmartScriptEntriesByType(SmartScriptType scriptType);

        IEnumerable<ISmartScriptProjectItem> GetProjectItems();
        IEnumerable<ISmartScriptProject> GetProjects();

        IBroadcastText? GetBroadcastTextByText(string text);

        ICreature? GetCreatureByGuid(uint guid);
        IGameObject? GetGameObjectByGuid(uint guid);
        
        [Flags]
        public enum ConditionKeyMask
        {
            SourceGroup = 1,
            SourceEntry = 2,
            SourceId = 4,
            All = SourceGroup | SourceEntry | SourceId,
            None = 0
        }

        public struct ConditionKey
        {
            public readonly int SourceType;
            public readonly int? SourceGroup;
            public readonly int? SourceEntry;
            public readonly int? SourceId;

            public ConditionKey(int sourceType, int? sourceGroup, int? sourceEntry, int? sourceId)
            {
                SourceType = sourceType;
                SourceGroup = sourceGroup;
                SourceEntry = sourceEntry;
                SourceId = sourceId;
            }
        }
    }
    
}