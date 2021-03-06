﻿using LinqToDB;
using LinqToDB.Data;

namespace WDE.TrinityMySqlDatabase.Models
{
    public class TrinityDatabase : DataConnection
    {
        public TrinityDatabase() : base("Trinity")
        {
        }

        public ITable<MySqlAreaTriggerScript> AreaTriggerScript => GetTable<MySqlAreaTriggerScript>();
        public ITable<MySqlCreatureTemplate> CreatureTemplate => GetTable<MySqlCreatureTemplate>();
        public ITable<MySqlCreature> Creature => GetTable<MySqlCreature>();
        public ITable<MySqlSmartScriptLine> SmartScript => GetTable<MySqlSmartScriptLine>();
        public ITable<MySqlGameObjectTemplate> GameObjectTemplate => GetTable<MySqlGameObjectTemplate>();
        public ITable<MySqlGameObject> GameObject => GetTable<MySqlGameObject>();
        public ITable<MySqlQuestTemplate> QuestTemplate => GetTable<MySqlQuestTemplate>();
        public ITable<MySqlQuestTemplateAddon> QuestTemplateAddon => GetTable<MySqlQuestTemplateAddon>();
        public ITable<MySqlQuestTemplateAddonWithScriptName> QuestTemplateAddonWithScriptName => GetTable<MySqlQuestTemplateAddonWithScriptName>();
        public ITable<MySqlAreaTriggerTemplate> AreaTriggerTemplate => GetTable<MySqlAreaTriggerTemplate>();
        public ITable<MySqlGameEvent> GameEvents => GetTable<MySqlGameEvent>();
        public ITable<MySqlConversationTemplate> ConversationTemplate => GetTable<MySqlConversationTemplate>();
        public ITable<MySqlConditionLine> Conditions => GetTable<MySqlConditionLine>();
        public ITable<MySqlSpellScriptName> SpellScriptNames => GetTable<MySqlSpellScriptName>();
        public ITable<MySqlGossipMenuLine> GossipMenus => GetTable<MySqlGossipMenuLine>();
        public ITable<MySqlNpcText> NpcTexts => GetTable<MySqlNpcText>();
        public ITable<MySqlCreatureClassLevelStat> CreatureClassLevelStats => GetTable<MySqlCreatureClassLevelStat>();
        public ITable<MySqlBroadcastText> BroadcastTexts => GetTable<MySqlBroadcastText>();
    }
}