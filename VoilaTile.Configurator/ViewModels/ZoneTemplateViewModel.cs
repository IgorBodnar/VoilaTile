namespace VoilaTile.Configurator.ViewModels
{
    using System.Collections.ObjectModel;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Configurator.Models;

    /// <summary>
    /// ViewModel for a zone template, enabling projection of normalized zones into specific pixel bounds.
    /// </summary>
    public partial class ZoneTemplateViewModel : ObservableObject
    {
        public string Name { get; set; }

        public ZoneTemplate Template { get; private set; }

        public ObservableCollection<DividerModel> VerticalDividers { get; } = new ObservableCollection<DividerModel>();

        public ObservableCollection<DividerModel> HorizontalDividers { get; } = new ObservableCollection<DividerModel>();

        public ZoneTemplateViewModel(ZoneTemplate template)
        {
            this.UpdateFromTemplate(template);
        }

        public void UpdateFromTemplate(ZoneTemplate template)
        {
            this.Template = template;
            this.Name = template.Name;

            this.VerticalDividers.Clear();
            this.HorizontalDividers.Clear();

            foreach (var divider in template.Dividers)
            {
                if (divider.IsVertical)
                {
                    this.VerticalDividers.Add(divider);
                }
                else
                {
                    this.HorizontalDividers.Add(divider);
                }
            }
        }
    }
}
