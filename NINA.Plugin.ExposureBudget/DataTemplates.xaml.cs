using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.ExposureBudget {

    /// <summary>
    /// Dictionnaire de ressources exporté vers N.I.N.A. : il contient le DataTemplate
    /// associé au panneau du plugin et il est fusionné dans les ressources de
    /// l'application par le chargeur de plugins.
    /// </summary>
    [Export(typeof(ResourceDictionary))]
    public partial class DataTemplates : ResourceDictionary {

        public DataTemplates() {
            InitializeComponent();
        }
    }
}
