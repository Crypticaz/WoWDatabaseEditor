using WDE.Common.History;
using WDE.DatabaseEditors.Models;
using WDE.DatabaseEditors.ViewModels.MultiRow;

namespace WDE.DatabaseEditors.History
{
    public class DatabaseLastRowRemovedHistoryAction : IHistoryAction
    {
        private readonly MultiRowDbTableEditorViewModel viewModel;
        private readonly DatabaseEntity entity;

        public DatabaseLastRowRemovedHistoryAction(MultiRowDbTableEditorViewModel viewModel,
            DatabaseEntity entity)
        {
            this.viewModel = viewModel;
            this.entity = entity;
        }
        
        public void Undo()
        {
            viewModel.UndoDeleteQuery(entity);
        }

        public void Redo()
        {
            viewModel.DoDeleteQuery(entity);
        }

        public string GetDescription()
        {
            return $"Delete query invoked for key {entity.Key}";
        }
    }
}