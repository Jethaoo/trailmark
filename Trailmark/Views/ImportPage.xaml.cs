using Trailmark.ViewModels;

namespace Trailmark.Views;

public partial class ImportPage : ContentPage
{
    public ImportPage(ImportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
