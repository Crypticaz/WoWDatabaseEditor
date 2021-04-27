using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using DynamicData;
using Prism.Commands;
using Prism.Events;
using WDE.Common;
using WDE.Common.Database;
using WDE.Common.Events;
using WDE.Common.History;
using WDE.Common.Managers;
using WDE.Common.Parameters;
using WDE.Common.Providers;
using WDE.Common.Services;
using WDE.Common.Services.MessageBox;
using WDE.Common.Solution;
using WDE.Common.Tasks;
using WDE.Common.Utils;
using WDE.DatabaseEditors.Data.Structs;
using WDE.DatabaseEditors.Extensions;
using WDE.DatabaseEditors.History;
using WDE.DatabaseEditors.Loaders;
using WDE.DatabaseEditors.Models;
using WDE.DatabaseEditors.QueryGenerators;
using WDE.DatabaseEditors.Solution;
using WDE.MVVM;

namespace WDE.DatabaseEditors.ViewModels.MultiRow
{
    public class MultiRowDbTableEditorViewModel : ObservableBase, ISolutionItemDocument
    {
        private readonly IItemFromListProvider itemFromListProvider;
        private readonly IMessageBoxService messageBoxService;
        private readonly ISolutionManager solutionManager;
        private readonly IParameterFactory parameterFactory;
        private readonly ISolutionTasksService solutionTasksService;
        private readonly ISolutionItemNameRegistry solutionItemName;
        private readonly IMySqlExecutor mySqlExecutor;
        private readonly IQueryGenerator queryGenerator;

        private readonly DatabaseTableSolutionItem solutionItem;
        private readonly IDatabaseTableDataProvider tableDataProvider;

        public MultiRowDbTableEditorViewModel(DatabaseTableSolutionItem solutionItem,
            IDatabaseTableDataProvider tableDataProvider, IItemFromListProvider itemFromListProvider,
            IHistoryManager history, ITaskRunner taskRunner, IMessageBoxService messageBoxService,
            IEventAggregator eventAggregator, ISolutionManager solutionManager, 
            IParameterFactory parameterFactory, ISolutionTasksService solutionTasksService,
            ISolutionItemNameRegistry solutionItemName, IMySqlExecutor mySqlExecutor,
            IQueryGenerator queryGenerator)
        {
            SolutionItem = solutionItem;
            this.itemFromListProvider = itemFromListProvider;
            this.solutionItem = solutionItem;
            this.tableDataProvider = tableDataProvider;
            this.messageBoxService = messageBoxService;
            this.solutionManager = solutionManager;
            this.parameterFactory = parameterFactory;
            this.solutionTasksService = solutionTasksService;
            this.solutionItemName = solutionItemName;
            this.mySqlExecutor = mySqlExecutor;
            this.queryGenerator = queryGenerator;
            History = history;
            tableDefinition = null!;
            
            IsLoading = true;
            title = solutionItemName.GetName(solutionItem);
            
            taskRunner.ScheduleTask($"Loading {title}..", LoadTableDefinition);

            undoCommand = new DelegateCommand(History.Undo, CanUndo);
            redoCommand = new DelegateCommand(History.Redo, CanRedo);
            OpenParameterWindow = new AsyncAutoCommand<DatabaseCellViewModel>(EditParameter);
            Save = new DelegateCommand(SaveSolutionItem);

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

            AutoDispose(eventAggregator.GetEvent<EventRequestGenerateSql>()
                .Subscribe(ExecuteSql));

            RemoveTemplateCommand = new DelegateCommand<DatabaseCellViewModel?>(RemoveTemplate, vm => vm != null);
            RevertCommand = new AsyncAutoCommand<DatabaseCellViewModel?>(Revert, cell => cell is DatabaseCellViewModel vm && vm.CanBeReverted && vm.IsModified);
            SetNullCommand = new DelegateCommand<DatabaseCellViewModel?>(SetToNull, vm => vm != null && vm.CanBeSetToNull);
            AddNewCommand = new AsyncAutoCommand(AddNewEntity);
        }

        private void ExecuteSql(EventRequestGenerateSqlArgs args)
        {
            if (args.Item is not DatabaseTableSolutionItem dbEditItem) 
                return;
            
            if (!solutionItem.Equals(dbEditItem)) 
                return;
            
            args.Sql = queryGenerator.GenerateQuery(new DatabaseTableData(tableDefinition, Entities));
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
            
            if (!view.ParentEntity.ExistInDatabase)
                return;
            
            if (!mySqlExecutor.IsConnected)
                return;

            if (!await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                .SetTitle("Reverting")
                .SetMainInstruction("Do you want to revert field in the database?")
                .SetContent(
                    "Reverted field will become unmodified field and unmodified fields are not generated in query. Therefore if you want to revert the field in the database, it can be done now.\n\nDo you want to revert the field in the database now (this will execute query)?")
                .SetIcon(MessageBoxIcon.Information)
                .WithYesButton(true)
                .WithNoButton(false)
                .Build()))
                return;

            var query = queryGenerator.GenerateUpdateFieldQuery(tableDefinition, view.ParentEntity, view.TableField);
            await mySqlExecutor.ExecuteSql(query);
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

        private bool CanRedo() => History.CanRedo;
        private bool CanUndo() => History.CanUndo;

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

        private async Task LoadTableDefinition()
        {
            var data = await tableDataProvider.Load(solutionItem.DefinitionId, solutionItem.Entries.Select(e => e.Key).ToArray()) as DatabaseTableData;

            if (data == null)
            {
                await messageBoxService.ShowDialog(new MessageBoxFactory<bool>().SetTitle("Error!")
                    .SetMainInstruction($"Editor failed to load data from database!")
                    .SetIcon(MessageBoxIcon.Error)
                    .WithOkButton(true)
                    .Build());
                return;
            }

            solutionItem.UpdateEntitiesWithOriginalValues(data.Entities);
            
            {
                rows.Clear();
                Entities.Clear();
                tableDefinition = data.TableDefinition;
                columns = tableDefinition.Groups.SelectMany(g => g.Fields).ToList();
                Columns.Clear();
                Columns.AddRange(columns.Select(c => new DatabaseColumnHeaderViewModel(c)));
                
                await AsyncAddEntities(data.Entities);
            }
            

            SetupHistory();
            IsLoading = false;
        }

        private void SetupHistory()
        {
            var historyHandler = AutoDispose(new MultiRowTableEditorHistoryHandler(this));
            History.PropertyChanged += (_, _) =>
            {
                undoCommand.RaiseCanExecuteChanged();
                redoCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(IsModified));
            };
            History.AddHandler(historyHandler);
        }

        private List<EntityOrigianlField>? GetOriginalFields(DatabaseEntity entity)
        {
            var modified = entity.Fields.Where(f => f.IsModified).ToList();
            if (modified.Count == 0)
                return null;
            
            return modified.Select(f => new EntityOrigianlField()
                {ColumnName = f.FieldName, OriginalValue = f.OriginalValue}).ToList();
        }

        private void SaveSolutionItem()
        {
            solutionItem.Entries = Entities.Select(e =>
                new SolutionItemDatabaseEntity(e.Key, e.ExistInDatabase, GetOriginalFields(e))).ToList();
            solutionManager.Refresh(solutionItem);
            solutionTasksService.SaveSolutionToDatabaseTask(solutionItem);
            History.MarkAsSaved();
            Title = solutionItemName.GetName(solutionItem);
        }
        
        public ObservableCollection<DatabaseEntity> Entities { get; } = new();
        public ReadOnlyObservableCollection<DatabaseEntitiesGroupViewModel> Rows { get; }
        public SourceList<DatabaseEntityViewModel> rows { get; } = new();

        public async Task<bool> RemoveEntity(DatabaseEntity entity)
        {
            if (!await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                .SetTitle("Delete entity")
                .SetMainInstruction($"Do you want to delete entity with key {entity.Key} from the editor?")
                .SetContent(
                    "It will be removed only from the project editor, it will not be removed from the database.")
                .WithYesButton(true)
                .WithNoButton(false).Build()))
                return false;

            return ForceRemoveEntity(entity);
        }

        public bool ForceRemoveEntity(DatabaseEntity entity)
        {
            var indexOfEntity = Entities.IndexOf(entity);
            if (indexOfEntity == -1)
                return false;
            
            Entities.RemoveAt(indexOfEntity);
            foreach (var row in rows.Items)
                row.Cells.RemoveAt(indexOfEntity);

            return true;
        }
        
        public async Task<bool> AddEntity(DatabaseEntity entity)
        {
            return ForceInsertEntity(entity, Entities.Count);
        }

        public bool ForceInsertEntity(DatabaseEntity entity, int index)
        {
            var name = parameterFactory.Factory(tableDefinition.Picker).ToString(entity.Key);
            var row = new DatabaseEntityViewModel(entity, name);
            
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

                var cellViewModel = AutoDispose(new DatabaseCellViewModel(column, row, entity, cell, parameterValue));
                row.Cells.Add(cellViewModel);
            }

            Entities.Insert(index, entity);
            rows.Add(row);
            return true;
        }

        private IList<DbEditorTableGroupFieldJson> columns = new List<DbEditorTableGroupFieldJson>();
        public ObservableCollection<DatabaseColumnHeaderViewModel> Columns { get; } = new();
        private DatabaseTableDefinitionJson tableDefinition;

        private async Task AsyncAddEntities(IList<DatabaseEntity> tableDataEntities)
        {
            List<DatabaseEntity> finalList = new();
            foreach (var entity in tableDataEntities)
            {
                if (await AddEntity(entity))
                    finalList.Add(entity);
            }
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            internal set => SetProperty(ref isLoading, value);
        }
        
        public AsyncAutoCommand AddNewCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> RemoveTemplateCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel?> RevertCommand { get; }
        public DelegateCommand<DatabaseCellViewModel?> SetNullCommand { get; }
        public AsyncAutoCommand<DatabaseCellViewModel> OpenParameterWindow { get; }
        private DelegateCommand undoCommand;
        private DelegateCommand redoCommand;
        
        private string title;
        public string Title
        {
            get => title;
            set => SetProperty(ref title, value);
        }
        public ICommand Undo => undoCommand;
        public ICommand Redo => redoCommand;
        public ICommand Copy => AlwaysDisabledCommand.Command;
        public ICommand Cut => AlwaysDisabledCommand.Command;
        public ICommand Paste => AlwaysDisabledCommand.Command;
        public ICommand Save { get; }
        public IAsyncCommand? CloseCommand { get; set; } = null;
        public bool CanClose { get; } = true;
        public bool IsModified => !History.IsSaved;
        public IHistoryManager History { get; }
        public ISolutionItem SolutionItem { get; }
    }
}