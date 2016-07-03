using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Gms.Common;
using Android.Locations;
using Plugin.Geolocator.Abstractions;

namespace Plugin.Geolocator
{
    class GoogleConnectionHandler : Java.Lang.Object, GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener, Android.Gms.Location.ILocationListener
    {
        public int REQUEST_CHECK_SETTINGS = 11000;

        public GoogleApiClient GoogleApiClient
        {
            get;
        }

        public LocationRequest LocationRequest
        {
            get;
        }

        public event EventHandler<PositionEventArgs> PositionChanged;

        public GoogleConnectionHandler()
        {


            // Create an instance of GoogleAPIClient.
            GoogleApiClient = new GoogleApiClient.Builder(Application.Context)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .AddApi(LocationServices.API)
                .Build();

            LocationRequest = new LocationRequest();
            LocationRequest.SetInterval(1000);
            LocationRequest.SetFastestInterval(250);
            LocationRequest.SetPriority(LocationRequest.PriorityHighAccuracy);

            LocationSettingsRequest.Builder builder = new LocationSettingsRequest.Builder()
                .AddLocationRequest(LocationRequest);

            PendingResult result =
                    LocationServices.SettingsApi.CheckLocationSettings(GoogleApiClient,
                            builder.Build());
            result.SetResultCallback<LocationSettingsResult>(CheckLocationSettingsCallback);
        }

        public Position GetPosition()
        {
            Location loc = LocationServices.FusedLocationApi.GetLastLocation(GoogleApiClient);
            if (loc != null)
                return LocationToPosition(loc);
            else
                return null;
        }

        public void OnStart()
        {
            GoogleApiClient.Connect();
        }

        public void OnStop()
        {
            GoogleApiClient.Disconnect();
        }

        /// <inheritdoc/>
        public void OnConnected(Bundle connectionHint)
        {

            LocationServices.FusedLocationApi.RequestLocationUpdates(
                    GoogleApiClient, LocationRequest, this);

        }

        /// <inheritdoc/>
        public void OnConnectionFailed(ConnectionResult result)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void OnConnectionSuspended(int cause)
        {
            throw new NotImplementedException();
        }


        private void CheckLocationSettingsCallback(LocationSettingsResult result)
        {
            Statuses status = result.Status;
            LocationSettingsStates states = result.LocationSettingsStates;
            switch (status.StatusCode)
            {
                case LocationSettingsStatusCodes.Success:
                    // All location settings are satisfied. The client can
                    // initialize location requests here.

                    break;
                case LocationSettingsStatusCodes.ResolutionRequired:
                    // Location settings are not satisfied, but this can be fixed
                    // by showing the user a dialog.
                    try
                    {
                        // Show the dialog by calling startResolutionForResult(),
                        // and check the result in onActivityResult().
                        status.StartResolutionForResult(
                            (Activity)Application.Context,
                            REQUEST_CHECK_SETTINGS);
                    }
                    catch (IntentSender.SendIntentException e)
                    {
                        // Ignore the error.
                    }
                    break;
                case LocationSettingsStatusCodes.SettingsChangeUnavailable:
                    // Location settings are not satisfied. However, we have no way
                    // to fix the settings so we won't show the dialog.

                    break;
            }

        }
        

        public void OnLocationChanged(Location location)
        {
            Position p = LocationToPosition(location);

            PositionChanged?.Invoke(this, new PositionEventArgs(p));
        }

        protected Position LocationToPosition(Location location)
        {
            return new Position()
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Altitude = location.Altitude,
                Speed = location.Speed,
                Heading = location.Bearing,
                Timestamp = new DateTime(location.Time)
            };
        }
    }
}