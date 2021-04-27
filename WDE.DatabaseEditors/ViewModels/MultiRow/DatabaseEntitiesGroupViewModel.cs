using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using DynamicData.Binding;

namespace WDE.DatabaseEditors.ViewModels.MultiRow
{
    public class DatabaseEntitiesGroupViewModel : ObservableCollectionExtended<DatabaseEntityViewModel>, IGrouping<uint, DatabaseEntityViewModel>, IDisposable
    {
        private readonly IDisposable disposable;
            
        public DatabaseEntitiesGroupViewModel(IGroup<DatabaseEntityViewModel, uint> group) 
        {
            Key = group.GroupKey;
            disposable = group.List
                .Connect()
//                .Sort(Comparer<DatabaseEntityViewModel>.Create((x, y) => x.Order.CompareTo(y.Order)))
                .Bind(this)
                .Subscribe();
        }

        public uint GroupOrder => this[0].Key;
        public string GroupName => this[0].Name;
        public uint Key { get; private set; }
        public void Dispose() => disposable.Dispose();
    }
}