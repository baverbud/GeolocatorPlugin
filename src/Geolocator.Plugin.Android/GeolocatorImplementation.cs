//
//  Copyright 2011-2013, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
using Plugin.Geolocator.Abstractions;
using System;
using System.Threading.Tasks;
using Android.Locations;
using System.Threading;
using Android.App;
using Android.OS;
using System.Linq;
using Android.Content;
using Android.Content.PM;
using Plugin.Permissions;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Gms.Common;

namespace Plugin.Geolocator
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class GeolocatorImplementation : IGeolocator
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public GeolocatorImplementation()
        {
            DesiredAccuracy = 100;

            gpsManager = new GoogleConnectionHandler();
            manager = (LocationManager)Application.Context.GetSystemService(Context.LocationService);
            providers = manager.GetProviders(enabledOnly: false).Where(s => s != LocationManager.PassiveProvider).ToArray();
        }
        /// <inheritdoc/>
        public event EventHandler<PositionErrorEventArgs> PositionError;
        /// <inheritdoc/>
        public event EventHandler<PositionEventArgs> PositionChanged;
        /// <inheritdoc/>
        public bool IsListening
        {
            get { return listener != null; }
        }
        /// <inheritdoc/>
        public double DesiredAccuracy
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public bool SupportsHeading
        {
            get
            {
                return true; //Kind of, you should use the  Compass plugin for better results
            }
        }
        /// <inheritdoc/>
        public bool IsGeolocationAvailable
        {
            get { return providers.Length > 0; }
        }
        /// <inheritdoc/>
        public bool IsGeolocationEnabled
        {
            get { return providers.Any(manager.IsProviderEnabled); }
        }
        

        /// <inheritdoc/>
        public async Task<Position> GetPositionAsync(int timeoutMilliseconds = Timeout.Infinite, CancellationToken? cancelToken = null, bool includeHeading = false)
        {
            var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permissions.Abstractions.Permission.Location).ConfigureAwait(false);
            if (status != Permissions.Abstractions.PermissionStatus.Granted)
            {
                Console.WriteLine("Currently does not have Location permissions, requesting permissions");

                var request = await CrossPermissions.Current.RequestPermissionsAsync(Permissions.Abstractions.Permission.Location);

                if (request[Permissions.Abstractions.Permission.Location] != Permissions.Abstractions.PermissionStatus.Granted)
                {
                    Console.WriteLine("Location permission denied, can not get positions async.");
                    return null;
                }
                providers = manager.GetProviders(enabledOnly: false).Where(s => s != LocationManager.PassiveProvider).ToArray();
            }

            if (providers.Length == 0)
            {
                providers = manager.GetProviders(enabledOnly: false).Where(s => s != LocationManager.PassiveProvider).ToArray();
            }


            if (timeoutMilliseconds <= 0 && timeoutMilliseconds != Timeout.Infinite)
                throw new ArgumentOutOfRangeException("timeoutMilliseconds", "timeout must be greater than or equal to 0");
            

            // Not an async call - google play services responds with last location, does not wait for next one.
            return gpsManager.GetPosition();
        }

        /// <inheritdoc/>
        public async Task<bool> StartListeningAsync(int minTime, double minDistance, bool includeHeading = false, ListenerSettings settings = null)
        {
            var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permissions.Abstractions.Permission.Location).ConfigureAwait(false);
            if (status != Permissions.Abstractions.PermissionStatus.Granted)
            {
                Console.WriteLine("Currently does not have Location permissions, requesting permissions");

                var request = await CrossPermissions.Current.RequestPermissionsAsync(Permissions.Abstractions.Permission.Location);

                if (request[Permissions.Abstractions.Permission.Location] != Permissions.Abstractions.PermissionStatus.Granted)
                {
                    Console.WriteLine("Location permission denied, can not get positions async.");
                    return false;
                }
                providers = manager.GetProviders(enabledOnly: false).Where(s => s != LocationManager.PassiveProvider).ToArray();
            }

            if (providers.Length == 0)
            {
                providers = manager.GetProviders(enabledOnly: false).Where(s => s != LocationManager.PassiveProvider).ToArray();
            }

            if (minTime < 0)
                throw new ArgumentOutOfRangeException("minTime");
            if (minDistance < 0)
                throw new ArgumentOutOfRangeException("minDistance");
            if (IsListening)
                throw new InvalidOperationException("This Geolocator is already listening");

            gpsManager.OnStart();
            gpsManager.PositionChanged += OnListenerPositionChanged;
            
            return true;
        }
        /// <inheritdoc/>
        public Task<bool> StopListeningAsync()
        {
            gpsManager.OnStop();
            gpsManager.PositionChanged -= OnListenerPositionChanged;
            return Task.FromResult(true);
        }



        private string[] providers;
        private readonly LocationManager manager;
        private readonly GoogleConnectionHandler gpsManager; // Google Play Services location manager
        private string headingProvider;

        private GeolocationContinuousListener listener;

        private readonly object positionSync = new object();
        private Position lastPosition;
        /// <inheritdoc/>
        private void OnListenerPositionChanged(object sender, PositionEventArgs e)
        {
            if (!IsListening) // ignore anything that might come in afterwards
                return;

            lock (positionSync)
            {
                lastPosition = e.Position;

                var changed = PositionChanged;
                if (changed != null)
                    changed(this, e);
            }
        }
        /// <inheritdoc/>
        private async void OnListenerPositionError(object sender, PositionErrorEventArgs e)
        {
            await StopListeningAsync();

            var error = PositionError;
            if (error != null)
                error(this, e);
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        internal static DateTimeOffset GetTimestamp(Location location)
        {
            return new DateTimeOffset(Epoch.AddMilliseconds(location.Time));
        }
    }
}