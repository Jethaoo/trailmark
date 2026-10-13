using Trailmark.ViewModels;

namespace Trailmark.Views;

public partial class TrackDetailsPage : ContentPage
{
    public TrackDetailsPage(TrackDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
