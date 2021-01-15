﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LinqToDB;
using LinqToDB.Configuration;
using LinqToDB.Data;
using MySqlX.XDevAPI;
using WDE.Common.Database;
using WDE.Common.Tasks;
using WDE.Module.Attributes;
using WDE.TrinityMySqlDatabase.Data;
using WDE.TrinityMySqlDatabase.Models;
using WDE.TrinityMySqlDatabase.Providers;
using WDE.TrinityMySqlDatabase.Services;

namespace WDE.TrinityMySqlDatabase
{
    [AutoRegister]
    [SingleInstance]
    public class TrinityMysqlDatabaseProvider : IDatabaseProvider
    {
        private List<MySqlCreatureTemplate>? creatureTemplateCache;
        private Dictionary<uint, MySqlCreatureTemplate> creatureTemplateByEntry = new();

        private List<MySqlGameObjectTemplate>? gameObjectTemplateCache;
        private Dictionary<uint, MySqlGameObjectTemplate> gameObjectTemplateByEntry = new();

        private List<MySqlQuestTemplate>? questTemplateCache;
        private Dictionary<uint, MySqlQuestTemplate> questTemplateByEntry = new();

        private readonly TrinityDatabase? model;

        public TrinityMysqlDatabaseProvider(IConnectionSettingsProvider settings,
            DatabaseLogger databaseLogger,
            ITaskRunner taskRunner)
        {
            string? host = settings.GetSettings().Host;
            if (string.IsNullOrEmpty(host))
            {
                model = null;
                return;
            }
            
            try
            {
                DataConnection.TurnTraceSwitchOn();
                DataConnection.WriteTraceLine = databaseLogger.Log;
                DataConnection.DefaultSettings = new MySqlSettings(settings.GetSettings());
                model = new TrinityDatabase();
                GetCreatureTemplate(0);
                taskRunner.ScheduleTask(new DatabaseCacheTask(this));
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(host))
                    MessageBox.Show($"Cannot connect to MySql database: {e.Message} Check your settings.");
                model = null;
            }
        }

        private class DatabaseCacheTask : IAsyncTask
        {
            private readonly TrinityMysqlDatabaseProvider database;
            public string Name => "Database cache";
            public bool WaitForOtherTasks => false;

            public DatabaseCacheTask(TrinityMysqlDatabaseProvider database)
            {
                this.database = database;
            }

            public async Task Run(ITaskProgress progress)
            {
                var model = new TrinityDatabase();
                progress.Report(0, 3, "Loading creatures");
                database.creatureTemplateCache = await model.CreatureTemplate.OrderBy(t => t.Entry).ToListAsync();

                progress.Report(1, 3, "Loading gameobjects");
                database.gameObjectTemplateCache = await model.GameObjectTemplate.OrderBy(t => t.Entry).ToListAsync();

                progress.Report(2, 3, "Loading quests");
                
                database.questTemplateCache = await (from t in model.QuestTemplate
                    join addon in model.QuestTemplateAddon on t.Entry equals addon.Entry into adn
                    from subaddon in adn.DefaultIfEmpty()
                    orderby t.Entry
                    select t.SetAddon(subaddon)).ToListAsync();
                
                await model.CloseAsync();
                
                Dictionary<uint, MySqlCreatureTemplate> creatureTemplateByEntry = new();
                Dictionary<uint, MySqlGameObjectTemplate> gameObjectTemplateByEntry = new();
                Dictionary<uint, MySqlQuestTemplate> questTemplateByEntry = new();

                foreach (var entity in database.creatureTemplateCache)
                    creatureTemplateByEntry[entity.Entry] = entity;
                
                foreach (var entity in database.gameObjectTemplateCache)
                    gameObjectTemplateByEntry[entity.Entry] = entity;
                
                foreach (var entity in database.questTemplateCache)
                    questTemplateByEntry[entity.Entry] = entity;

                database.creatureTemplateByEntry = creatureTemplateByEntry;
                database.gameObjectTemplateByEntry = gameObjectTemplateByEntry;
                database.questTemplateByEntry = questTemplateByEntry;
            }
        }

        public ICreatureTemplate? GetCreatureTemplate(uint entry)
        {
            if (model == null)
                return null;

            if (creatureTemplateByEntry.TryGetValue(entry, out var item))
                return item;

            return model.CreatureTemplate.FirstOrDefault(ct => ct.Entry == entry);
        }

        public IEnumerable<ICreatureTemplate> GetCreatureTemplates()
        {
            if (model == null)
                return new List<ICreatureTemplate>();

            if (creatureTemplateCache == null)
                creatureTemplateCache = (from t in model.CreatureTemplate orderby t.Entry select t).ToList();

            return creatureTemplateCache;
        }

        public IEnumerable<ISmartScriptLine> GetScriptFor(int entryOrGuid, SmartScriptType type)
        {
            if (model == null)
                return new List<ISmartScriptLine>();

            return model.SmartScript.Where(line => line.EntryOrGuid == entryOrGuid && line.ScriptSourceType == (int) type).ToList();
        }

        public IEnumerable<IGameObjectTemplate> GetGameObjectTemplates()
        {
            if (model == null)
                return new List<IGameObjectTemplate>();

            if (gameObjectTemplateCache == null)
                gameObjectTemplateCache = (from t in model.GameObjectTemplate orderby t.Entry select t).ToList();

            return gameObjectTemplateCache;
        }

        public IEnumerable<IQuestTemplate> GetQuestTemplates()
        {
            if (model == null)
                return new List<IQuestTemplate>();

            if (questTemplateCache == null)
                questTemplateCache = (from t in model.QuestTemplate
                    join addon in model.QuestTemplateAddon on t.Entry equals addon.Entry into adn
                    from subaddon in adn.DefaultIfEmpty()
                    orderby t.Entry
                    select t.SetAddon(subaddon)).ToList();

            return questTemplateCache;
        }

        public IGameObjectTemplate? GetGameObjectTemplate(uint entry)
        {
            if (model == null)
                return null;

            if (gameObjectTemplateByEntry.TryGetValue(entry, out var item))
                return item;

            return model.GameObjectTemplate.FirstOrDefault(g => g.Entry == entry);
        }

        public IQuestTemplate? GetQuestTemplate(uint entry)
        {
            if (model == null)
                return null;

            if (questTemplateByEntry.TryGetValue(entry, out var item))
                return item;

            MySqlQuestTemplateAddon? addon = model.QuestTemplateAddon.FirstOrDefault(addon => addon.Entry == entry);
            return model.QuestTemplate.FirstOrDefault(q => q.Entry == entry)?.SetAddon(addon);
        }

        public async Task InstallScriptFor(int entryOrGuid, SmartScriptType type, IEnumerable<ISmartScriptLine> script)
        {
            if (model == null)
                return;

            await model.BeginTransactionAsync();
            await model.SmartScript.Where(x => x.EntryOrGuid == entryOrGuid && x.ScriptSourceType == (int) type).DeleteAsync();
            if (type == SmartScriptType.Creature)
            {
                await model.CreatureTemplate.Where(p => p.Entry == (uint) entryOrGuid)
                    .Set(p => p.AIName, "SmartAI")
                    .Set(p => p.ScriptName, "")
                    .UpdateAsync();
            }

            await model.SmartScript.BulkCopyAsync(script.Select(l => new MySqlSmartScriptLine(l)));

            await model.CommitTransactionAsync();
        }

        public async Task InstallConditions(IEnumerable<IConditionLine> conditionLines,
            IDatabaseProvider.ConditionKeyMask keyMask,
            IDatabaseProvider.ConditionKey? manualKey = null)
        {
            if (model == null)
                return;

            var conditions = conditionLines?.ToList() ?? new List<IConditionLine>();
            List<(int SourceType, int? SourceGroup, int? SourceEntry, int? SourceId)> keys = conditions.Select(c =>
                    (c.SourceType, keyMask.HasFlag(IDatabaseProvider.ConditionKeyMask.SourceGroup) ? (int?) c.SourceGroup : null,
                        keyMask.HasFlag(IDatabaseProvider.ConditionKeyMask.SourceEntry) ? (int?) c.SourceEntry : null,
                        keyMask.HasFlag(IDatabaseProvider.ConditionKeyMask.SourceId) ? (int?) c.SourceId : null))
                .Union(manualKey.HasValue
                    ? new[]
                    {
                        (manualKey.Value.SourceType, manualKey.Value.SourceGroup, manualKey.Value.SourceEntry,
                            manualKey.Value.SourceId)
                    }
                    : Array.Empty<(int, int?, int?, int?)>())
                .Distinct()
                .ToList();

            await model.BeginTransactionAsync();

            foreach (var key in keys)
                await model.Conditions.Where(x => x.SourceType == key.SourceType &&
                                                  (!key.SourceGroup.HasValue || x.SourceGroup == key.SourceGroup.Value) &&
                                                  (!key.SourceEntry.HasValue || x.SourceEntry == key.SourceEntry.Value) &&
                                                  (!key.SourceId.HasValue || x.SourceId == key.SourceId.Value))
                    .DeleteAsync();

            if (conditions.Count > 0)
                await model.Conditions.BulkCopyAsync(conditions.Select(line => new MySqlConditionLine(line)));

            await model.CommitTransactionAsync();
        }

        public IEnumerable<IConditionLine> GetConditionsFor(int sourceType, int sourceEntry, int sourceId)
        {
            if (model == null)
                return new List<IConditionLine>();

            return model.Conditions.Where(line =>
                    line.SourceType == sourceType && line.SourceEntry == sourceEntry && line.SourceId == sourceId)
                .ToList();
        }
    }

    public class ConnectionStringSettings : IConnectionStringSettings
    {
        public string ConnectionString { get; set; } = "";
        public string Name { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public bool IsGlobal => false;
    }

    public class MySqlSettings : ILinqToDBSettings
    {
        public MySqlSettings(DbAccess access)
        {
            ConnectionStrings = new[]
            {
                new ConnectionStringSettings
                {
                    Name = "Trinity",
                    ProviderName = "MySqlConnector",
                    ConnectionString =
                        $"Server={access.Host};Port={access.Port ?? 3306};Database={access.Database};Uid={access.User};Pwd={access.Password};AllowUserVariables=True"
                }
            };
        }

        public IEnumerable<IDataProviderSettings> DataProviders => Enumerable.Empty<IDataProviderSettings>();

        public string DefaultConfiguration => "MySqlConnector";
        public string DefaultDataProvider => "MySqlConnector";

        public IEnumerable<IConnectionStringSettings> ConnectionStrings { get; }
    }
}