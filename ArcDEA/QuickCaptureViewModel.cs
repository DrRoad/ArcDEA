using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Collections.ObjectModel;
using ArcGIS.Desktop.Mapping.Events;
using ArcDEA.Classes;
using static ArcDEA.Classes.DataStructures;
using System.Threading;
using System.Windows.Threading;
using System.Net.Http;
using System.IO;

namespace ArcDEA
{
    internal class QuickCaptureViewModel : DockPane
    {
        private const string _dockPaneID = "ArcDEA_QuickCapture";

        #region Collections synchronisation
        /// <summary>
        /// Enable collections synchronisation.
        /// </summary>
        private static readonly object _lockQueryAreaLayers = new object();
        private static readonly object _lockQueryCollections = new object();
        protected QuickCaptureViewModel()
        {
            BindingOperations.EnableCollectionSynchronization(QueryAreaLayers, _lockQueryAreaLayers);
            //BindingOperations.EnableCollectionSynchronization(QueryCollections, _lockQueryCollections);
        }
        #endregion

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Subscribe events to various methods. 
        /// </summary>
        protected override async Task InitializeAsync()
        {
            await QueuedTask.Run(() =>
            {
                // Subscribe QueryArea controls
                ActiveMapViewChangedEvent.Subscribe(OnMapViewChanged);
                LayersAddedEvent.Subscribe(OnLayerAdded);
                LayersRemovedEvent.Subscribe(OnLayerRemoved);
            });
        }

        #region Heading controls
        /// <summary>
        /// Temporary text heading on DockPane.
        /// </summary>
        private string _heading = "Temporary";
        public string Heading
        {
            get { return _heading; }
            set { SetProperty(ref _heading, value, () => Heading); }
        }
        #endregion

        #region QueryArea controls
        /// <summary>
        /// Observable list of available query area graphics on contents pane.
        /// </summary>
        private ObservableCollection<GraphicsLayer> _queryAreaLayers = new ObservableCollection<GraphicsLayer>();
        public ObservableCollection<GraphicsLayer> QueryAreaLayers
        {
            get { return _queryAreaLayers; }
            set { SetProperty(ref _queryAreaLayers, value, () => QueryAreaLayers); }
        }

        /// <summary>
        /// Selected query area graphics layer from combobox.
        /// </summary>
        private GraphicsLayer _selectedQueryAreaLayer;
        public GraphicsLayer SelectedQueryAreaLayer
        {
            get { return _selectedQueryAreaLayer; }
            set { SetProperty(ref _selectedQueryAreaLayer, value, () => SelectedQueryAreaLayer); }
        }

        /// <summary>
        /// Event to refresh the query area combobox when active map changes.
        /// </summary>
        private void OnMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            // Clear the current query area list
            QueryAreaLayers.Clear();

            // Skip if no new active map
            if (args.IncomingView != null)
            {
                // Get active mapview
                MapView map = args.IncomingView;

                if (map != null)
                {
                    // Get list of all grapic layers on current map, if exist
                    var graphicsList = map.Map.GetLayersAsFlattenedList().OfType<GraphicsLayer>();

                    // Fill query area combobox with graphic layers
                    foreach (GraphicsLayer graphic in graphicsList)
                    {
                        QueryAreaLayers.Add(graphic);
                    }

                    // Sort alphabetically by graphic layer name
                    QueryAreaLayers.OrderBy(e => e.Name);
                }
            }
        }

        /// <summary>
        /// Event to refresh the query area combobox if new graphic layer added to active map.
        /// </summary>
        private void OnLayerAdded(LayerEventsArgs args)
        {
            // Iterate layers
            foreach (var layer in args.Layers)
            {
                // Add to query area combobox if graphic type
                if (layer is GraphicsLayer)
                {
                    QueryAreaLayers.Add(layer as GraphicsLayer);
                }
            }

            // Sort alphabetically by graphic layer name
            QueryAreaLayers.OrderBy(e => e.Name);
        }

        /// <summary>
        /// Event to refresh the query area combobox if graphic layer removed from active map.
        /// </summary>
        private void OnLayerRemoved(LayerEventsArgs args)
        {
            // Iterate layers
            foreach (var layer in args.Layers)
            {
                // Remove from query area combobox if graphic type
                if (layer is GraphicsLayer)
                {
                    QueryAreaLayers.Remove(layer as GraphicsLayer);
                }
            }

            // Sort alphabetically by graphic layer name
            QueryAreaLayers.OrderBy(e => e.Name);
        }

        // TODO: handle name changes
        // TODO: combobox will not populate when project start + dockpane in tabs but not visible

        /// <summary>
        /// Button to call the query area drawing map tool.
        /// </summary>
        public ICommand CmdDrawQueryArea
        {
            get
            {
                return new RelayCommand(() =>
                {
                    var plugin = FrameworkApplication.GetPlugInWrapper("ArcDEA_DrawQueryAreaTool");
                    if (plugin is ICommand cmdPlugin)
                    {
                        cmdPlugin.Execute(null);
                    }
                });
            }
        }
        #endregion

        #region QueryDates controls
        /// <summary>
        /// DateTime of query start date control (default 2015-01-01).
        /// </summary>
        private DateTime _queryStartDate = new DateTime(2015, 1, 1);
        public DateTime QueryStartDate
        {
            get { return _queryStartDate; }
            set { SetProperty(ref _queryStartDate, value, () => QueryStartDate); }
        }

        /// <summary>
        /// DateTime of query end date control (default now).
        /// </summary>
        private DateTime _queryEndDate = DateTime.Now;
        public DateTime QueryEndDate
        {
            get { return _queryEndDate; }
            set { SetProperty(ref _queryEndDate, value, () => QueryEndDate); }
        }
        #endregion

        #region QueryCollection controls
        /// <summary>
        /// Observable list of available DEA data collections.
        /// </summary>
        private ObservableCollection<CollectionItem> _queryCollections = new ObservableCollection<CollectionItem>() { new CollectionItem("ga_ls8c_ard_3", "Landsat 8 ARD 3") };
        public ObservableCollection<CollectionItem> QueryCollections
        {
            get { return _queryCollections; }
            set { SetProperty(ref _queryCollections, value, () => QueryCollections); }
        }

        /// <summary>
        /// Selected collection item from combobox.
        /// </summary>
        private CollectionItem _selectedQueryCollection;
        public CollectionItem SelectedQueryCollection
        {
            get { return _selectedQueryCollection; }
            set
            {
                SetProperty(ref _selectedQueryCollection, value, () => SelectedQueryCollection);
            }
        }
        #endregion

        #region QueryAsset controls
        /// <summary>
        /// List of available assets (i.e., bands) for current DEA data collection.
        /// </summary>
        private List<AssetItem> _queryAssets = new List<AssetItem>() { new AssetItem("nbart_blue",   "Blue",   false),
                                                                       new AssetItem("nbart_green",  "Green",  false),
                                                                       new AssetItem("nbart_red",    "Red",    false),
                                                                       new AssetItem("nbart_nir",    "NIR",    false),
                                                                       new AssetItem("nbart_swir_1", "SWIR 1", false),
                                                                       new AssetItem("nbart_swir_2", "SWIR 2", false),
                                                                       new AssetItem("oa_fmask",     "FMask",  false)};
        public List<AssetItem> QueryAssets
        {
            get { return _queryAssets; }
            set { SetProperty(ref _queryAssets, value, () => QueryAssets); }
        }

        /// <summary>
        /// Tracks AssetItems that have been selected in listbox. Binds
        /// ListItem IsSelected to AssetItem IsAssetSelected property.
        /// </summary>
        private bool _isAssetSelected;
        public bool IsAssetSelected
        {
            get { return _isAssetSelected; }
            set { SetProperty(ref _isAssetSelected, value, () => IsAssetSelected); }
        }
        #endregion

        #region QueryCloud controls
        /// <summary>
        /// Percentage cloud cover slider control.
        /// </summary>
        private double _queryCloudCover = 0;
        public double QueryCloudCover
        {
            get { return _queryCloudCover; }
            set { SetProperty(ref _queryCloudCover, value, () => QueryCloudCover); }
        }
        #endregion

        #region Output folder controls
        /// <summary>
        /// Output folder path for textbox control.
        /// </summary>
        private string _outputFolderPath;
        public string OutputFolderPath
        {
            get { return _outputFolderPath; }
            set { SetProperty(ref _outputFolderPath, value, () => OutputFolderPath); }
        }

        /// <summary>
        /// Button to open folder selection dialog for output folder.
        /// </summary>
        public ICommand CmdOpenFolderDialog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    // Create a filter for selecting a folder (workspace)
                    BrowseProjectFilter foldersFilter = new BrowseProjectFilter("esri_browseDialogFilters_workspaces_all");

                    // Set various parameters for the filter
                    foldersFilter.Name = "Folder";
                    foldersFilter.Excludes.Add("esri_browsePlaces_Online");

                    // Apply the filter to the open item dialog
                    OpenItemDialog dialog = new OpenItemDialog()
                    {
                        Title = "Select Output Folder",
                        MultiSelect = false,
                        BrowseFilter = foldersFilter
                    };

                    // Show the dialog and retrieve selection, if one
                    if (dialog.ShowDialog().Value)
                    {
                        // Get the user selected folder path
                        var selectedFolder = dialog.Items.First();
                        string outputFolderPath = selectedFolder.Path;

                        // Update output folder path textbox
                        OutputFolderPath = outputFolderPath;
                    }
                });
            }
        }
        #endregion

        #region Progress controls
        /// <summary>
        /// Current value of progress for incrementing progress bar.
        /// </summary>
        private double _progressValue = 1;
        public double ProgressValue
        {
            get { return _progressValue; }
            set { SetProperty(ref _progressValue, value, () => ProgressValue); }
        }

        /// <summary>
        /// Maximum value of progress for incrementing progress bar.
        /// </summary>
        private double _maxProgressValue = 100;
        public double MaxProgressValue
        {
            get { return _maxProgressValue; }
            set { SetProperty(ref _maxProgressValue, value, () => MaxProgressValue); }
        }

        /// <summary>
        /// Percentage of progress for display in textbox.
        /// </summary>
        private string _progressStatus;
        public string ProgressStatus
        {
            get { return _progressStatus; }
            set { SetProperty(ref _progressStatus, value, () => ProgressStatus); }
        }

        /// <summary>
        /// Method to track progress bar.
        /// </summary>
        private int _iProgressValue = -1;
        private int _iMaxProgressValue = -1;
        private string _iProgressStatus = String.Empty;
        private void ProgressUpdate(int iProgressValue, int iMaxProgressValue, string iProgressStatus)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                if (_iMaxProgressValue != iMaxProgressValue)
                {
                    MaxProgressValue = iMaxProgressValue;
                }

                if (_iProgressValue != iProgressValue)
                {
                    ProgressValue = iProgressValue;
                }

                if (_iProgressStatus != iProgressStatus)
                {
                    ProgressStatus = iProgressStatus;
                }
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
                {
                    if (_iMaxProgressValue != iMaxProgressValue)
                    {
                        MaxProgressValue = iMaxProgressValue;
                    }

                    if (_iProgressValue != iProgressValue)
                    {
                        ProgressValue = iProgressValue;
                    }

                    if (_iProgressStatus != iProgressStatus)
                    {
                        ProgressStatus = iProgressStatus;
                    }
                }));
            }
        }
        #endregion


        /// <summary>
        /// Button for running query and obtaining collection data.
        /// </summary>
        public ICommand CmdRun
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    // TODO: checks, etc.

                    // Notify progress status
                    ProgressUpdate(-1, -1, "Initialising...");

                    // Get bounding box of graphic in wgs84 and albers
                    double[] bboxWgs84 = await Helpers.GraphicToBoundingBoxAsync(SelectedQueryAreaLayer, 4326);
                    double[] bboxAlbers = await Helpers.GraphicToBoundingBoxAsync(SelectedQueryAreaLayer, 3577);

                    // Get start and end dates
                    string startDate = QueryStartDate.ToString("yyyy'-'MM'-'dd");
                    string endDate = QueryEndDate.ToString("yyyy'-'MM'-'dd");

                    // Get selected collection (as raw collection name)
                    string collection = SelectedQueryCollection.RawName;

                    // Get selected assets (as raw band names)
                    List<string> assets = QueryAssets.Where(e => e.IsAssetSelected).Select(e => e.RawName).ToList();


                    // Setup valid classes and minimum valid pixels
                    List<int> validClasses = new List<int> { 1, 4, 5 };  // TODO: make dynamic
                    double minValid = 1.0 - (QueryCloudCover / 100);

                    // Set user's output folder path
                    string outputFolder = OutputFolderPath;




                    // Initialize DEA STAC object
                    Stac stac = new Stac(
                        collection: collection,
                        startDate: startDate,
                        endDate: endDate,
                        boundingBoxWgs84: bboxWgs84,
                        boundingBoxAlbers: bboxAlbers,
                        limit: 250
                        );

                    // Notify progress status
                    ProgressUpdate(-1, -1, "Querying STAC endpoint...");

                    // Query STAC endpoint using above parameters
                    await stac.QueryStacAsync(timeout: 5);

                    // Reduce features down to first date in each solar day group
                    stac.GroupBySolarDay();

                    // Notify progress status
                    ProgressUpdate(-1, -1, "Removing invalid items...");

                    // Drop feature dates with too many invalid pixels
                    await stac.DropInvalidFeaturesAsync(validClasses, minValid);

                    // Convert all STAC items into Wcs urls for download
                    stac.GetWcs(assets);

                    // Open a HTTP client with 60 minute timeout
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(60);

                    var options = new ParallelOptions { 
                        MaxDegreeOfParallelism = Environment.ProcessorCount 
                    };

                    int i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(stac.Wcs, options, async (item, token) =>
                    {
                        string date = item.Key;
                        string url = item.Value[0];
                        string filename = item.Value[1];

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Started download: {date}");

                        // Create full output folder path
                        string filepath = Path.Join(outputFolder, filename);

                        // Query URL and ensure success
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();

                        // Download file to output folder
                        using (FileStream fs = new FileStream(filepath, FileMode.CreateNew))
                        {
                            await response.Content.CopyToAsync(fs);
                        }

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Finished download: {date}");

                        // Increment progress bar
                        int pct = Convert.ToInt16(((double)i / (double)stac.Result.Features.Count) * 100);
                        string msg = $"Downloading items ({pct}%)...";
                        ProgressUpdate(++i, stac.Result.Features.Count, msg);
                    }));

                    // Notify progress status
                    ProgressUpdate(100, 100, "Finished.");
                });
            }
        }
    }

    

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class QuickCapture_ShowButton : Button
    {
        protected override void OnClick()
        {
            QuickCaptureViewModel.Show();
        }
    }
}
