using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using WDE.Parameters;
using WDE.Parameters.Models;

namespace WDE.DatabaseEditors.Avalonia.Helpers
{
    public class FieldValueTemplateSelector : IDataTemplate
    {
        public DataTemplate? GenericTemplate { get; set; }
        public DataTemplate? BoolTemplate { get; set; }

        public IControl Build(object param)
        {
            if (param is ParameterValueHolder<long> holder && holder.Parameter is BoolParameter)
                return BoolTemplate!.Build(param);
            return GenericTemplate!.Build(param);
        }

        public bool Match(object data)
        {
            return data is IParameterValueHolder;
        }
    }
}