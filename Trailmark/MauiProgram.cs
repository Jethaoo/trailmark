using System.Reflection;

using Microsoft.Maui.Storage;
using Trailmark.Data;
using Trailmark.Helpers;
using Trailmark.Services;
using Trailmark.ViewModels;
using Trailmark.Views;

namespace Trailmark;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();


        builder.Services.AddSingleton<ITrailmarkDatabase>(_ =>
            new TrailmarkDatabase(Path.Combine(FileSystem.AppDataDirectory, "gpxviewer.db")));
        builder.Services.AddSingleton<ITrackStatisticsCalculator, TrackStatisticsCalculator>();
        builder.Services.AddSingleton<IGpxParser, GpxParser>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<ILocalFileStorage, LocalFileStorage>();
        builder.Services.AddSingleton<IGpxImportService, GpxImportService>();
        builder.Services.AddSingleton<IShareService, ShareService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ILocationService, LocationService>();
        builder.Services.AddSingleton<ITrackService, TrackService>();
        builder.Services.AddSingleton(new MapTilerOptions(ReadMapTilerApiKey()));
        builder.Services.AddSingleton<IMapTilerService, MapTilerService>();
        builder.Services.AddSingleton<IMapService, MapService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<TrackLibraryViewModel>();
        builder.Services.AddTransient<TrackDetailsViewModel>();
        builder.Services.AddTransient<MapViewModel>();
        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<TrackLibraryPage>();
        builder.Services.AddTransient<TrackDetailsPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<ImportPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }

    private static string ReadMapTilerApiKey() => typeof(MauiProgram).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "MapTilerApiKey")?.Value ?? string.Empty;
}
