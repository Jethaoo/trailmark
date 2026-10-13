using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Devices.Sensors;
using Trailmark.ViewModels;

namespace Trailmark.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel viewModel;

    public MapPage(MapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Full-screen when opened for a specific track; otherwise keep the tab bar
        // so the map tab can never trap the user.
        Shell.SetTabBarIsVisible(this, !viewModel.IsPushedMap);
        WeakReferenceMessenger.Default.Register<MapPage, MapActionMessage>(this, static (page, message) => _ = page.HandleMapActionAsync(message.Value));
        WeakReferenceMessenger.Default.Register<MapPage, MapLocationMessage>(this, static (page, message) => _ = page.ShowLocationAsync(message.Value));
        if (viewModel.MapSource.Html is null)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnDisappearing();
    }

    private async void OnMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
            await HandleMapActionAsync(MapAction.ZoomToTrack);
    }

    private async Task HandleMapActionAsync(MapAction action)
    {
        string script = action == MapAction.ZoomToTrack
            ? "window.trailmark && window.trailmark.zoomToTrack();"
            : string.Empty;
        if (script.Length > 0)
            await MapWebView.EvaluateJavaScriptAsync(script);
    }

    private async Task ShowLocationAsync(Location location)
    {
        string latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        string longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
        await MapWebView.EvaluateJavaScriptAsync($"window.trailmark && window.trailmark.recenter({latitude},{longitude});");
    }
}
