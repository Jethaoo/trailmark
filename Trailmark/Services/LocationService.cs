using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Trailmark.Models;

namespace Trailmark.Services;

public interface ILocationService
{
    Task<LocationResult> GetCurrentLocationAsync(
        bool requestPermission = true,
        CancellationToken cancellationToken = default);
}

public sealed class LocationService : ILocationService
{
    public async Task<LocationResult> GetCurrentLocationAsync(
        bool requestPermission = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);
            if (permission != PermissionStatus.Granted && requestPermission)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);

            if (permission != PermissionStatus.Granted)
                return new LocationResult(LocationStatus.PermissionDenied, ErrorMessage: "Location permission was not granted.");

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));
            var location = await Geolocation.Default.GetLocationAsync(request, cancellationToken).ConfigureAwait(false);
            return location is null
                ? new LocationResult(LocationStatus.Unavailable, ErrorMessage: "A current location is not available.")
                : new LocationResult(
                    LocationStatus.Success,
                    location.Latitude,
                    location.Longitude,
                    location.Accuracy,
                    location.Timestamp.UtcDateTime);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LocationResult(LocationStatus.Unavailable, ErrorMessage: "The location request timed out.");
        }
        catch (FeatureNotEnabledException)
        {
            return new LocationResult(LocationStatus.Disabled, ErrorMessage: "Location services are disabled.");
        }
        catch (FeatureNotSupportedException)
        {
            return new LocationResult(LocationStatus.Unavailable, ErrorMessage: "Location is not supported on this device.");
        }
        catch (PermissionException)
        {
            return new LocationResult(LocationStatus.PermissionDenied, ErrorMessage: "Location permission was denied.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new LocationResult(LocationStatus.Error, ErrorMessage: exception.Message);
        }
    }
}
