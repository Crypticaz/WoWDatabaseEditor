using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Prism.Commands;
using Prism.Events;
using WDE.Common;
using WDE.Common.Database;
using WDE.Common.History;
using WDE.Common.Parameters;
using WDE.Common.Providers;
using WDE.Common.Services;
using WDE.Common.Services.MessageBox;
using WDE.Common.Solution;
using WDE.Common.Tasks;
using WDE.Common.Utils;
using WDE.DatabaseEditors.Data.Structs;
using WDE.DatabaseEditors.History;
using WDE.DatabaseEditors.Loaders;
using WDE.DatabaseEditors.Models;
using WDE.DatabaseEditors.QueryGenerators;
using WDE.DatabaseEditors.Solution;

namespace WDE.DatabaseEditors.ViewModels.MultiRow
{
    public class MultiRowDbTableEditorViewModel : ViewModelBase
    {
        private readonly IItemFromListProvider itemFromListProvider;
        private readonly IMessageBoxService messageBoxService;
        private readonly IParameterFactory parameterFactory;
        private readonly IMySqlExecutor mySqlExecutor;
        private readonly IQueryGenerator queryGenerator;
        private readonly IDatabaseTableDataProvider tableDataProvider;

        public ReadOnlyObservableCollection<DatabaseEntitiesGroupViewModel> Rows { get; }
        public SourceList<DatabaseEntityViewModel> rows { get; } = new();

        private IList<DbEditorTableGroupFieldJson> columns = new List<DbEditorTableGroupFieldJson>();
        public ObservableCollection<DatabaseColumnHeaderViewModel> Columns { get; } = new();

        private HashSet<uint> keys = new();

        public AsyncAutoCommand AddNewCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> RemoveTemplateCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel?> RevertCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> SetNullCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel> OpenParameterWindow { get; }

        public MultiRowDbTableEditorViewModel(DatabaseTableSolutionItem solutionItem,
            IDatabaseTableDataProvider tableDataProvider, IItemFromListProvider itemFromListProvider,
            IHistoryManager history, ITaskRunner taskRunner, IMessageBoxService messageBoxService,
            IEventAggregator eventAggregator, ISolutionManager solutionManager, 
            IParameterFactory parameterFactory, ISolutionTasksService solutionTasksService,
            ISolutionItemNameRegistry solutionItemName, IMySqlExecutor mySqlExecutor,
            IQueryGenerator queryGenerator) : base(history, solutionItem, solutionItemName, 
            solutionManager, solutionTasksService, eventAggregator, 
            queryGenerator, tableDataProvider, messageBoxService, taskRunner)
        {
            this.itemFromListProvider = itemFromListProvider;
            this.solutionItem = solutionItem;
            this.tableDataProvider = tableDataProvider;
            this.messageBoxService = messageBoxService;
            this.parameterFactory = parameterFactory;
            this.mySqlExecutor = mySqlExecutor;
            this.queryGenerator = queryGenerator;

            OpenParameterWindow = new AsyncAutoCommand<DatabaseCellViewModel>(EditParameter);

            var comparer = Comparer<DatabaseEntitiesGroupViewModel>.Create((x, y) => x.GroupOrder.CompareTo(y.GroupOrder));
            AutoDispose(rows.Connect()
                .GroupOn(t => t.Key)
                .Transform(GroupCreate)
                .DisposeMany()
                .Sort(comparer)
                .Bind(out ReadOnlyObservableCollection<DatabaseEntitiesGroupViewModel> groupedRows)
                .Subscribe(a =>
                {
                    
                }, b => throw b));
            Rows = groupedRows;

            RemoveTemplateCommand = new DelegateCommand<DatabaseCellViewModel?>(RemoveTemplate, vm => vm != null);
            RevertCommand = new AsyncAutoCommand<DatabaseCellViewModel?>(Revert, cell => cell is DatabaseCellViewModel vm && vm.CanBeReverted && vm.IsModified);
            SetNullCommand = new DelegateCommand<DatabaseCellViewModel?>(SetToNull, vm => vm != null && vm.CanBeSetToNull);
            AddNewCommand = new AsyncAutoCommand(AddNewEntity);
            
            ScheduleLoading();
        }

        private async Task AddNewEntity()
        {
            var parameter = parameterFactory.Factory(tableDefinition.Picker);
            var selected = await itemFromListProvider.GetItemFromList(parameter.Items, false);
            if (!selected.HasValue)
                return;

            var data = await tableDataProvider.Load(tableDefinition.Id, (uint) selected);
            if (data == null) 
                return;

            foreach (var entity in data.Entities)
            {
                if (ContainsEntity(entity))
                {
                    await messageBoxService.ShowDialog(new MessageBoxFactory<bool>().SetTitle("Entity already added")
                        .SetMainInstruction($"Entity {entity.Key} is already added to the editor")
                        .WithOkButton(false)
                        .SetIcon(MessageBoxIcon.Information)
                        .Build());
                    continue;
                }
                if (!entity.ExistInDatabase)
                {
                    if (!await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                        .SetTitle("Entity doesn't exist in database")
                        .SetMainInstruction($"Entity {entity.Key} doesn't exist in the database")
                        .SetContent(
                            "WoW Database Editor will be generating DELETE/INSERT query instead of UPDATE. Do you want to continue?")
                        .WithYesButton(true)
                        .WithNoButton(false).Build()))
                        continue;
                }
                await AddEntity(entity);
            }
        }
        
        private void SetToNull(DatabaseCellViewModel? view)
        {
            if (view != null && view.CanBeNull && !view.IsReadOnly) 
                view.ParameterValue.SetNull();
        }

        private async Task Revert(DatabaseCellViewModel? view)
        {
            if (view == null || view.IsReadOnly)
                return;
            
            view.ParameterValue.Revert();
        }

        private void RemoveTemplate(DatabaseCellViewModel? view)
        {
            if (view == null)
                return;

            RemoveEntity(view.ParentEntity);
        }

        private DatabaseEntitiesGroupViewModel GroupCreate(IGroup<DatabaseEntityViewModel, uint> @group)
        {
            return new (group);
        }

        private bool ContainsEntity(DatabaseEntity entity)
        {
            foreach (var e in Entities)
                if (e.Key == entity.Key)
                    return true;
            return false;
        }

        private async Task EditParameter(DatabaseCellViewModel cell)
        {
            var valueHolder = cell.ParameterValue as ParameterValue<long>;
            
            if (valueHolder == null)
                return;

            if (!valueHolder.Parameter.HasItems)
                return;

            var result = await itemFromListProvider.GetItemFromList(valueHolder.Parameter.Items,
                valueHolder.Parameter is FlagParameter, valueHolder.Value);
            if (result.HasValue)
                valueHolder.Value = result.Value; 
        }

        protected override ICollection<uint> GenerateKeys() => keys;

        protected override async Task InternalLoadData(DatabaseTableData data)
        {
            rows.Clear();
            columns = tableDefinition.Groups.SelectMany(g => g.Fields).ToList();
            Columns.Clear();
            Columns.AddRange(columns.Select(c => new DatabaseColumnHeaderViewModel(c)));
                
            await AsyncAddEntities(data.Entities);
            History.AddHandler(AutoDispose(new MultiRowTableEditorHistoryHandler(this)));
        }

        protected override void UpdateSolutionItem()
        {
            solutionItem.Entries = keys.Select(e =>
                new SolutionItemDatabaseEntity(e, false)).ToList();
        }

        public async Task<bool> RemoveEntity(DatabaseEntity entity)
        {
            var itemsWithSameKey = Entities.Count(e => e.Key == entity.Key);

            if (itemsWithSameKey == 1)
            {
                if (await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                    .SetTitle("Removing entity")
                    .SetMainInstruction($"Do you want to delete the key {entity.Key} from solution?")
                    .SetContent(
                        $"This entity is the last row with key {entity.Key}. You have to choose if you want to delete the key from solution as well.\n\nIf you delete it from the solution, DELETE FROM... will no longer be generated for this key.")
                    .WithYesButton(true)
                    .WithNoButton(false)
                    .SetIcon(MessageBoxIcon.Information)
                    .Build()))
                {
                    if (mySqlExecutor.IsConnected)
                    {
                        if (await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                            .SetTitle("Execute DELETE query?")
                            .SetMainInstruction("Do you want to execute DELETE query now?")
                            .SetContent(
                                "You have decided to remove the item from solution, therefore DELETE FROM query will not be generated for this key anymore, you we can execute DELETE with that key for that last time.")
                            .WithYesButton(true)
                            .WithNoButton(false)
                            .Build()))
                        {
                            await mySqlExecutor.ExecuteSql(queryGenerator.GenerateDeleteQuery(tableDefinition, entity));
                        }
                    }

                    keys.Remove(entity.Key);
                }
            }
            
            return ForceRemoveEntity(entity);
        }

        public override bool ForceRemoveEntity(DatabaseEntity entity)
        {
            var indexOfEntity = Entities.IndexOf(entity);
            if (indexOfEntity == -1)
                return false;
            
            Entities.RemoveAt(indexOfEntity);
            rows.RemoveAt(indexOfEntity);

            return true;
        }
        
        public async Task<bool> AddEntity(DatabaseEntity entity)
        {
            return ForceInsertEntity(entity, Entities.Count);
        }

        public override bool ForceInsertEntity(DatabaseEntity entity, int index)
        {
            var name = parameterFactory.Factory(tableDefinition.Picker).ToString(entity.Key);
            var row = new DatabaseEntityViewModel(entity, name);
            
            int columnIndex = 0;
            foreach (var column in columns)
            {
                var cell = entity.GetCell(column.DbColumnName);
                if (cell == null)
                    throw new Exception("this should never happen");

                IParameterValue parameterValue = null!;
                if (cell is DatabaseField<long> longParam)
                {
                    parameterValue = new ParameterValue<long>(longParam.Current, longParam.Original, parameterFactory.Factory(column.ValueType));
                }
                else if (cell is DatabaseField<string> stringParam)
                {
                    parameterValue = new ParameterValue<string>(stringParam.Current, stringParam.Original, StringParameter.Instance);
                }
                else if (cell is DatabaseField<float> floatParameter)
                {
                    parameterValue = new ParameterValue<float>(floatParameter.Current, floatParameter.Original, FloatParameter.Instance);
                }

                var cellViewModel = AutoDispose(new DatabaseCellViewModel(columnIndex, column, row, entity, cell, parameterValue));
                row.Cells.Add(cellViewModel);
                columnIndex++;
            }

            Entities.Insert(index, entity);
            keys.Add(entity.Key);
            rows.Insert(index, row);
            return true;
        }

        private async Task AsyncAddEntities(IList<DatabaseEntity> tableDataEntities)
        {
            List<DatabaseEntity> finalList = new();
            foreach (var entity in tableDataEntities)
            {
                if (await AddEntity(entity))
                    finalList.Add(entity);
            }
        }
    }
}