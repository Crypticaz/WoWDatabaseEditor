﻿using System;
using WDE.DatabaseEditors.Models;
using WDE.MVVM;
using WDE.MVVM.Observable;

namespace WDE.DatabaseEditors.ViewModels.Template
{
    public class DatabaseCellViewModel : ObservableBase
    {
        public DatabaseRowViewModel Parent { get; }
        public DatabaseEntity ParentEntity { get; }
        public IDatabaseField? TableField { get; }
        public IParameterValue ParameterValue { get; }
        public bool IsVisible { get; private set; } = true;
        public bool IsModified { get; private set; }
        public string? OriginalValueTooltip { get; private set; }
        public bool CanBeNull => Parent.CanBeNull;
        public bool CanBeSetToNull => CanBeNull && !Parent.IsReadOnly;
        public bool CanBeReverted => !Parent.IsReadOnly;
        private bool inConstructor = true;

        public DatabaseCellViewModel(DatabaseRowViewModel parent, 
            DatabaseEntity parentEntity, 
            IDatabaseField tableField, 
            IParameterValue parameterValue, 
            IObservable<bool>? cellIsVisible)
        {
            Link(tableField, tf => tf.IsModified, () => IsModified);
            ParentEntity = parentEntity;
            Parent = parent;
            TableField = tableField;
            ParameterValue = parameterValue;

            if (cellIsVisible != null)
                AutoDispose(cellIsVisible.Subscribe(v =>
                {
                    IsVisible = v;
                    RaisePropertyChanged(nameof(IsVisible));
                }));

            AutoDispose(parameterValue.ToObservable().SubscribeAction(_ =>
            {
                if (!inConstructor)
                  parent.AnyFieldModified();
                OriginalValueTooltip =
                    tableField.IsModified ? "Original value: " + parameterValue.OriginalString : null;
                RaisePropertyChanged(nameof(OriginalValueTooltip));
                RaisePropertyChanged(nameof(AsBoolValue));
            }));
            inConstructor = false;
        }

        public DatabaseCellViewModel(DatabaseRowViewModel parent, 
            DatabaseEntity parentEntity,
            IParameterValue parameterValue)
        {
            ParentEntity = parentEntity;
            Parent = parent;
            ParameterValue = parameterValue;

            AutoDispose(parameterValue.ToObservable().SubscribeAction(_ =>
            {
                if (!inConstructor)
                    parent.AnyFieldModified();
            }));
            inConstructor = false;
        }

        public bool? AsBoolValue
        {
            get =>  ParameterValue.IsNull ? null : ((ParameterValue as ParameterValue<long>)?.Value ?? 0) != 0;
            set
            {
                if (ParameterValue is ParameterValue<long> longParam)
                {
                    if (value == null)
                        longParam.SetNull();
                    else
                        longParam.Value = value.Value ? 1 : 0;
                }
            }
        }
    }
}