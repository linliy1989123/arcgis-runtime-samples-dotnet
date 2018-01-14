﻿// Copyright 2018 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using Esri.ArcGISRuntime.LocalServices;
using Esri.ArcGISRuntime.Mapping;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Geoprocessing;

namespace ArcGISRuntime.WPF.Samples.LocalServerGeoprocessing
{
    public partial class LocalServerGeoprocessing
    {
        // Hold a reference to the local geoprocessingservice
        private LocalGeoprocessingService _myGpService;

        private GeoprocessingTask _gpTask;
        private GeoprocessingJob _gpJob;

        public LocalServerGeoprocessing()
        {
            InitializeComponent();

            // set up the sample 
            Initialize();
        }

        private async void Initialize()
        {
            // Create a map and add it to the view
            MyMapView.Map = new Map(Basemap.CreateLightGrayCanvasVector());

            // Load the tiled layer
            string rasterPath = await GetRasterPath();

            TileCache myTileCache = new TileCache(rasterPath);

            ArcGISTiledLayer tiledLayer = new ArcGISTiledLayer(myTileCache);

            // Try to load the tiled layer
            try
            {
                // Wait for the layer to load
                await tiledLayer.LoadAsync();

                // Zoom to extent of the tiled layer
                await MyMapView.SetViewpointGeometryAsync(tiledLayer.FullExtent);
            }
            catch (Exception)
            {
                MessageBox.Show("Couldn't load the tile package, ending sample load.");
                return;
            }

            // Add the layer to the map
            MyMapView.Map.OperationalLayers.Add(tiledLayer);

            try
            {
                // Listen for the shutdown and unloaded events so that the local server can be shut down
                this.Dispatcher.ShutdownStarted += ShutdownSample;
                this.Unloaded += ShutdownSample;

                // Show the loading UI

                // Start the local server instance
                await LocalServer.Instance.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Local Server failed to start, ending sample load.");
                return;
            }

            // Get the path to the geoprocessing task
            string gpServiceUrl = await GetGpPath();

            // Create the geoprocessing service
            _myGpService = new LocalGeoprocessingService(gpServiceUrl, GeoprocessingServiceType.AsynchronousSubmitWithMapServiceResult);

            // Take action once the service loads
            _myGpService.StatusChanged += MyGpServiceOnStatusChanged;

            try
            {
                // Start the service
                await _myGpService.StartAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("geoprocessing service failed to start.");
            }

        }

        private async void MyGpServiceOnStatusChanged(object sender, StatusChangedEventArgs statusChangedEventArgs)
        {
            if (statusChangedEventArgs.Status == LocalServerStatus.Started)
            {
                _gpTask = await GeoprocessingTask.CreateAsync(new Uri(_myGpService.Url + "/Contour"));

                // Update UI state
                MyResetButton.IsEnabled = true;
                MyUpdateContourButton.IsEnabled = true;
                MyLoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void GenerateContours()
        {
            // Show the progress bar
            MyLoadingIndicator.Visibility = Visibility.Visible;
            MyLoadingIndicator.IsIndeterminate = false;

            // Create the geoprocessing parameters
            GeoprocessingParameters gpParams = new GeoprocessingParameters(GeoprocessingExecutionType.AsynchronousSubmit);

            // Add the interval parameter to the geoprocessing parameters
            gpParams.Inputs["Interval"] = new GeoprocessingDouble(MyContourSlider.Value);

            // Create the job
            _gpJob = _gpTask.CreateJob(gpParams);

            // Update the UI when job progress changes
            _gpJob.ProgressChanged += (sender, args) => { MyLoadingIndicator.Value = _gpJob.Progress / 100.0; };

            // Be notified when the task completes (or other change happens)
            _gpJob.JobChanged += GpJobOnJobChanged;
            
            // Start the job
            _gpJob.Start();
        }

        private async void GpJobOnJobChanged(object o, EventArgs eventArgs)
        {
            if (_gpJob.Status == JobStatus.Succeeded)
            {
                // Get the URL to the map service with the geoprocessing results
                string gpServiceResultUrl = _myGpService.Url.ToString()
                    .Replace("GPServer", "MapServer/jobs/" + _gpJob.ServerJobId);

                // Create a map image layer to show the results
                ArcGISMapImageLayer myMapImageLayer = new ArcGISMapImageLayer(new Uri(gpServiceResultUrl));

                // Load the layer
                await myMapImageLayer.LoadAsync();

                // Add the layer to the map
                MyMapView.Map.OperationalLayers.Add(myMapImageLayer);

                // Hide the progress bar
                MyLoadingIndicator.Visibility = Visibility.Collapsed;

                // Disable the generate button
                MyUpdateContourButton.IsEnabled = false;

                // Enable the reset button
                MyResetButton.IsEnabled = true;
            }
            else if (_gpJob.Status == JobStatus.Failed)
            {
                MessageBox.Show("Job failed");
            }
        }

        private async void ShutdownSample(object sender, EventArgs e)
        {
            try
            {
                // Shut down the local server if it has started
                if (LocalServer.Instance.Status == LocalServerStatus.Started)
                {
                    await LocalServer.Instance.StopAsync();
                }
            }
            catch (InvalidOperationException)
            {
                // Local server isn't installed, just return
                return;
            }
        }

        private async Task<string> GetRasterPath()
        {
            #region offlinedata

            // The data manager provides a method to get the folder
            string folder = DataManager.GetDataFolder();

            // Get the full path
            string filepath = Path.Combine(folder, "SampleData", "LocalServerGeoprocessing", "RasterHillshade.tpk");

            // Check if the file exists
            if (!File.Exists(filepath))
            {
                // Download the file
                await DataManager.GetData("f7c7b4a30fb9415896ba0d1921fe014b", "LocalServerGeoprocessing");
            }

            return filepath;

            #endregion offlinedata
        }

        private async Task<string> GetGpPath()
        {
            #region offlinedata

            // The data manager provides a method to get the folder
            string folder = DataManager.GetDataFolder();

            // Get the full path
            string filepath = Path.Combine(folder, "SampleData", "LocalServerGeoprocessing", "Contour.gpk");

            // Check if the file exists
            if (!File.Exists(filepath))
            {
                // Download the file
                await DataManager.GetData("da9e565a52ca41c1937cff1a01017068", "LocalServerGeoprocessing");
            }

            return filepath;

            #endregion offlinedata
        }

        private void MyResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Remove the contour
            MyMapView.Map.OperationalLayers.RemoveAt(1);

            // Enable the generate button
            MyUpdateContourButton.IsEnabled = true;

            // Disable the reset button
            MyResetButton.IsEnabled = false;
        }

        private void MyUpdateContourButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Disable the generate button
            ((Button) sender).IsEnabled = false;
            GenerateContours();
        }
    }
}