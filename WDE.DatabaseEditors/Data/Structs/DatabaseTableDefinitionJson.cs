﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace WDE.DatabaseEditors.Data.Structs
{
    [ExcludeFromCodeCoverage]
    public class DatabaseTableDefinitionJson
    {
        [JsonProperty(PropertyName = "name")] 
        public string Name { get; set; } = "";
        
        [JsonProperty(PropertyName = "table_name")]
        public string TableName { get; set; } = "";
        
        [JsonProperty(PropertyName = "table_index_name")]
        public string TablePrimaryKeyColumnName { get; set; } = "";
        
        [JsonProperty(PropertyName = "multi_record")]
        public bool IsMultiRecord { get; set; }
        
        [JsonProperty(PropertyName = "table_name_source_field")]
        public string TableNameSource { get; set; } = "";
        
        [JsonProperty(PropertyName = "groups")]
        public IList<DatabaseColumnsGroupJson> Groups { get; set; } = new List<DatabaseColumnsGroupJson>();
    }

    [ExcludeFromCodeCoverage]
    public struct DatabaseColumnsGroupJson
    {
        [JsonProperty(PropertyName = "group_name")]
        public string Name { get; set; }
        
        [JsonProperty(PropertyName = "group_sort_field")]
        public string? GroupSortField { get; set; }

        [JsonProperty(PropertyName = "fields")]
        public IList<DbEditorTableGroupFieldJson> Fields { get; set; }
    }

    public struct DbEditorTableGroupFieldJson
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        
        [JsonProperty(PropertyName = "db_column_name")]
        public string DbColumnName { get; set; }
        
        [JsonProperty(PropertyName = "value_type")]
        public string ValueType { get; set; }
        
        [JsonProperty(PropertyName = "read_only")]
        public bool IsReadOnly { get; set; }
    }
}