using System.Collections.Generic;
using WDE.Common.History;

namespace WDE.DatabaseEditors.Models
{
    public class DatabaseEntity
    {
        public Dictionary<string, IDatabaseField> Cells { get; }
        
        public IEnumerable<IDatabaseField> Fields => Cells.Values;

        public event System.Action<IHistoryAction>? OnAction;
        
        public bool ExistInDatabase { get; set; }
        
        public uint Key { get; }
        
        public DatabaseEntity(bool existInDatabase, uint key, Dictionary<string, IDatabaseField> cells)
        {
            ExistInDatabase = existInDatabase;
            Key = key;
            Cells = cells;
            foreach (var databaseField in Cells)
            {
                databaseField.Value.OnChanged += action =>
                {
                    OnAction?.Invoke(action);
                };
            }
        }

        public IDatabaseField? GetCell(string columnName)
        {
            if (Cells.TryGetValue(columnName, out var cell))
                return cell;
            return null;
        }
    }
}