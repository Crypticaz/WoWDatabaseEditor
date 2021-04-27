using WDE.DatabaseEditors.Models;
using WDE.MVVM;

namespace WDE.DatabaseEditors.ViewModels
{
    public abstract class ViewModelBase : ObservableBase
    {
        public abstract bool ForceRemoveEntity(DatabaseEntity entity);

        public abstract bool ForceInsertEntity(DatabaseEntity entity, int index);
    }
}