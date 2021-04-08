﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WDE.Common;
using WDE.DatabaseEditors.Data;
using WDE.DatabaseEditors.Models;
using WDE.Module.Attributes;

namespace WDE.DatabaseEditors.Solution
{
    [AutoRegister]
    public class CreatureTemplateTableProvider : DbEditorsSolutionItemProvider
    {
        private readonly IDbEditorTableDataProvider tableDataProvider;
        private readonly ICreatureEntryProviderService creatureEntryProviderService;

        public CreatureTemplateTableProvider(IDbEditorTableDataProvider tableDataProvider, ICreatureEntryProviderService creatureEntryProviderService) : 
            base("Creature Template", "Edit or create data of creature.", "SmartScriptGeneric")
        {
            this.tableDataProvider = tableDataProvider;
            this.creatureEntryProviderService = creatureEntryProviderService;
        }

        public override async Task<ISolutionItem?> CreateSolutionItem()
        {
            var key = await creatureEntryProviderService.GetEntryFromService();
            if (key.HasValue)
            {
                var data = await tableDataProvider.LoadCreatureTemplateDataEntry(key.Value);
                if (data != null)
                    return new DbEditorsSolutionItem(key.Value, DbTableContentType.CreatureTemplate,
                        false, new Dictionary<string, DbTableSolutionItemModifiedField>());
            }

            return null;
        }
    }
}