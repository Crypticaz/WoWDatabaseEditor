using System.Windows;
using System.Windows.Controls;
using WDE.DatabaseEditors.Models;
using WDE.DatabaseEditors.ViewModels;
using WDE.Parameters;
using WDE.Parameters.Models;

namespace WDE.DatabaseEditors.WPF.Helpers
{
    public class FieldValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GenericTemplate { get; set; }
        public DataTemplate BoolTemplate { get; set; }

        public override DataTemplate SelectTemplate(object param, DependencyObject container)
        {
            if (param is DatabaseCellViewModel vm && vm.ParameterValue is ParameterValue<long> holder && holder.Parameter is BoolParameter)
                return BoolTemplate;
            return GenericTemplate;
        }
    }
}