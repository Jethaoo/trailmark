namespace Trailmark.Views;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("track-details", typeof(TrackDetailsPage));
        Routing.RegisterRoute("map", typeof(MapPage));
        Routing.RegisterRoute("import", typeof(ImportPage));
    }
}
