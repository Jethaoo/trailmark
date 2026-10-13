using Trailmark.ViewModels;

namespace Trailmark.Views;

public partial class TrackLibraryPage : ContentPage
{
    private readonly TrackLibraryViewModel viewModel;

    public TrackLibraryPage(TrackLibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
