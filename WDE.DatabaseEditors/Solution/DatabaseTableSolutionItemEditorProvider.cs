using System;
using Prism.Ioc;
using WDE.Common.Managers;
using WDE.Common.Solution;
using WDE.Common.Tasks;
using WDE.DatabaseEditors.Data.Interfaces;
using WDE.DatabaseEditors.Loaders;
using WDE.DatabaseEditors.ViewModels;
using WDE.DatabaseEditors.ViewModels.MultiRow;
using WDE.DatabaseEditors.ViewModels.Template;
using WDE.Module.Attributes;

namespace WDE.DatabaseEditors.Solution
{
    [AutoRegister]
    public class DatabaseTableSolutionItemEditorProvider : ISolutionItemEditorProvider<DatabaseTableSolutionItem>
    {
        private readonly IDatabaseTableDataProvider tableDataProvider;
        private readonly IContainerProvider containerRegistry;
        private readonly ITableDefinitionProvider tableDefinitionProvider;
        
        public DatabaseTableSolutionItemEditorProvider(IDatabaseTableDataProvider tableDataProvider, 
            IContainerProvider containerRegistry, ITableDefinitionProvider tableDefinitionProvider)
        {
            this.tableDataProvider = tableDataProvider;
            this.containerRegistry = containerRegistry;
            this.tableDefinitionProvider = tableDefinitionProvider;
        }
        
        public IDocument GetEditor(DatabaseTableSolutionItem item)
        {
            var definition = tableDefinitionProvider.GetDefinition(item.DefinitionId);
            if (definition == null)
                throw new Exception("Cannot find table editor with definition " + item.DefinitionId);

            if (definition.IsMultiRecord)
                return  containerRegistry.Resolve<MultiRowDbTableEditorViewModel>((typeof(DatabaseTableSolutionItem), item));
            return containerRegistry.Resolve<TemplateDbTableEditorViewModel>((typeof(DatabaseTableSolutionItem), item));
        }
    }
}