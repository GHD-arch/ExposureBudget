using System.Windows.Controls;

namespace NINA.Plugin.ExposureBudget.View {

    /// <summary>
    /// Vue du panneau Exposure Budget. Le DataContext est le
    /// <see cref="ViewModel.ExposureBudgetVM"/> fourni par N.I.N.A.
    /// </summary>
    public partial class ExposureBudgetView : UserControl {

        public ExposureBudgetView() {
            InitializeComponent();
        }
    }
}
