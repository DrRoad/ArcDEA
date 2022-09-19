using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Collections.ObjectModel;
using ArcGIS.Desktop.Mapping.Events;
using ArcDEA.Classes;
using System.Net.Http;
using System.IO;
using ArcGIS.Desktop.Framework.Controls;

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
        //private static readonly object _lockQueryCollections = new object();
        protected QuickCaptureViewModel()
        {
            BindingOperations.EnableCollectionSynchronization(QueryAreaLayers, _lockQueryAreaLayers);
            //BindingOperations.EnableCollectionSynchronization(QueryCollections, _lockQueryCollections);

            // TODO: do all your populate list functions here
            // its how esri do it

            // Initialise dockpanel tabs
            PrimaryMenuList.Add(new TabControl() { Text = "Landsat", Tooltip = "Landsat Products" });
            PrimaryMenuList.Add(new TabControl() { Text = "Sentinel", Tooltip = "Sentinel Products" });
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

        #region tabs (to be finished)
        // TODO: finish this
        // https://github.com/Esri/arcgis-pro-sdk-community-samples/tree/master/Framework/CustomCatalog
        // DockPanel with header and a stackpanel binding to panel1, panel2
        // in dockpanel with header vm, set current page to panel based on tab clicked
        // panels are defined in a folder seperately as one xaml and cs each.
        private List<TabControl> _primaryMenuList = new List<TabControl>();
        public List<TabControl> PrimaryMenuList
        {
            get { return _primaryMenuList; }
        }
        private int _selectedPanelHeaderIndex = 0;
        public int SelectedPanelHeaderIndex
        {
            get { return _selectedPanelHeaderIndex; }
            set
            {
                SetProperty(ref _selectedPanelHeaderIndex, value, () => SelectedPanelHeaderIndex);
                //if (_selectedPanelHeaderIndex == 0)
                    //CurrentPage = _paneH1VM;
                //if (_selectedPanelHeaderIndex == 1)
                    //CurrentPage = _paneH2VM;
            }
        }
        //private PanelViewModelBase _currentPage;
        //public PanelViewModelBase CurrentPage
        //{
        //    get { return _currentPage; }
        //    set
        //    {
        //        SetProperty(ref _currentPage, value, () => CurrentPage);
        //    }
        //}
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
        /// List of available DEA data collections.
        /// </summary>
        private List<Helpers.CollectionItem> _queryCollections = Helpers.PopulateCollectionItems();
        public List<Helpers.CollectionItem> QueryCollections
        {
            get { return _queryCollections; }
            set { SetProperty(ref _queryCollections, value, () => QueryCollections); }
        }

        /// <summary>
        /// Tracks CollectionItems that have been selected in listbox. Binds
        /// ListItem IsSelected to CollectionItem IsCollectionSelected property.
        /// </summary>
        private bool _isCollectionSelected;
        public bool IsCollectionSelected
        {
            get { return _isCollectionSelected; }
            set { SetProperty(ref _isCollectionSelected, value, () => IsCollectionSelected); }
        }

        /// <summary>
        /// Tracks whether user has requested slc-off data or not.
        /// </summary>
        private bool _queryIncludeSlcOff = false;
        public bool QueryIncludeSlcOff
        {
            get { return _queryIncludeSlcOff; }
            set { SetProperty(ref _queryIncludeSlcOff, value, () => QueryIncludeSlcOff); }
        }
        #endregion

        #region QueryAsset controls
        /// <summary>
        /// Value of currently selected tab control index.
        /// </summary>
        private int _selectedAssetTabIndex = 0;
        public int SelectedAssetTabIndex
        {
            get { return _selectedAssetTabIndex; }
            set { SetProperty(ref _selectedAssetTabIndex, value, () => SelectedAssetTabIndex); }
        }

        /// <summary>
        /// List of available raw assets (i.e., bands) for current DEA data collection.
        /// </summary>
        private List<Helpers.AssetRawItem> _queryRawAssets = Helpers.PopulateRawAssetItems();
        public List<Helpers.AssetRawItem> QueryRawAssets
        {
            get { return _queryRawAssets; }
            set { SetProperty(ref _queryRawAssets, value, () => QueryRawAssets); }
        }

        /// <summary>
        /// Tracks AssetRawItems that have been selected in listbox. Binds
        /// ListItem IsSelected to AssetItem IsRawAssetSelected property.
        /// </summary>
        private bool _isRawAssetSelected;
        public bool IsRawAssetSelected
        {
            get { return _isRawAssetSelected; }
            set { SetProperty(ref _isRawAssetSelected, value, () => IsRawAssetSelected); }
        }

        /// <summary>
        /// List of available index assets (i.e., bands) that users can generate.
        /// </summary>
        private List<Helpers.AssetIndexItem> _queryIndexAssets = Helpers.PopulateIndexAssetItems();
        public List<Helpers.AssetIndexItem> QueryIndexAssets
        {
            get { return _queryIndexAssets; }
            set { SetProperty(ref _queryIndexAssets, value, () => QueryIndexAssets); }
        }

        /// <summary>
        /// Tracks AssetIndexItems that have been selected in listbox. Binds
        /// ListItem IsSelected to AssetItem IsIndexAssetSelected property.
        /// </summary>
        private bool _isIndexAssetSelected;
        public bool IsIndexAssetSelected
        {
            get { return _isIndexAssetSelected; }
            set { SetProperty(ref _isIndexAssetSelected, value, () => IsIndexAssetSelected); }
        }
        #endregion

        #region QueryMaskValues controls
        // TODO: rethink name for maskvalues
        /// <summary>
        /// List of available quality fmask values for current DEA data collection.
        /// </summary>
        private List<Helpers.MaskValueItem> _queryMaskValues = Helpers.PopulateMaskValueItems();
        public List<Helpers.MaskValueItem> QueryMaskValues
        {
            get { return _queryMaskValues; }
            set { SetProperty(ref _queryMaskValues, value, () => QueryMaskValues); }
        }

        /// <summary>
        /// Tracks MaskValueItem that have been selected in listbox. Binds
        /// ListItem IsSelected to AssetItem IsMaskValueSelected property.
        /// </summary>
        private bool _isMaskValueSelected;
        public bool IsMaskValueSelected
        {
            get { return _isMaskValueSelected; }
            set { SetProperty(ref _isMaskValueSelected, value, () => IsMaskValueSelected); }
        }
        #endregion

        // TODO: set this to QueryInvalidPercent
        #region QueryCloud controls
        /// <summary>
        /// Percentage cloud cover slider control.
        /// </summary>
        private float _queryCloudCover = 0;
        public float QueryCloudCover
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
        private int _progressValue = 1;
        public int ProgressValue
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
        /// Progress message for display in textbox.
        /// </summary>
        private string _progressMessage;
        public string ProgressMessage
        {
            get { return _progressMessage; }
            set { SetProperty(ref _progressMessage, value, () => ProgressMessage); }
        }

        /// <summary>
        /// Progress percentage for display in textbox.
        /// </summary>
        private string _progressPercentage;
        public string ProgressPercentage
        {
            get { return _progressPercentage; }
            set { SetProperty(ref _progressPercentage, value, () => ProgressPercentage); }
        }

        /// <summary>
        /// Sets whether the progress bar progresses or interminates.
        /// </summary>
        private bool _isProgressInterminate = false;
        public bool IsProgressInterminate
        {
            get { return _isProgressInterminate; }
            set { SetProperty(ref _isProgressInterminate, value, () => IsProgressInterminate); }
        }
        public void RefreshProgressBar(int min, int max, string message, bool isInterminate)
        {
            ProgressValue = min;
            MaxProgressValue = max;
            ProgressMessage = message;
            IsProgressInterminate = isInterminate;
        }
        #endregion

        #region Processing controls
        /// <summary>
        /// Sets whether the process is running or not.
        /// </summary>
        private bool _isPocessing = false;
        public bool IsProcessing
        {
            get { return _isPocessing; }
            set { SetProperty(ref _isPocessing, value, () => IsProcessing); }
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
                    #region General initialisation
                    // Set that process is running
                    // TODO: setup processing switch
                    //IsProcessing = true;

                    // Set progressor
                    RefreshProgressBar(0, 100, "Initialising...", true);
                    ProgressPercentage = "";
                    IProgress<int> progressValue = new Progress<int>(e => ProgressValue = e);
                    IProgress<string> progressPercent = new Progress<string>(e => ProgressPercentage = e);

                    // Register GDAL and set config options
                    OSGeo.GDAL.Gdal.AllRegister();
                    OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                    OSGeo.GDAL.Gdal.SetConfigOption("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR");
                    OSGeo.GDAL.Gdal.SetConfigOption("CPL_VSIL_CURL_ALLOWED_EXTENSIONS", "tif");
                    OSGeo.GDAL.Gdal.SetConfigOption("VSI_CACHE", "TRUE");
                    OSGeo.GDAL.Gdal.SetConfigOption("VSI_CACHE_SIZE ", Math.Pow(10, 9).ToString());
                    OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_MULTIRANGE", "YES");
                    OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_MERGE_CONSECUTIVE_RANGES", "YES");
                    OSGeo.GDAL.Gdal.SetConfigOption("AWS_NO_SIGN_REQUEST", "YES");

                    // Get the ArcGIS temporary folder path
                    string tmpFolder = Path.GetTempPath();

                    // Set download and processing num cores
                    var numCores = new ParallelOptions { MaxDegreeOfParallelism = 15 };  // TODO: make this dynamic

                    // Open a HTTP client with 30 minute timeout
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(30);
                    #endregion

                    #region Get and check parameters from UI
                    // Get bounding box of graphic in wgs84 and albers
                    double[] bboxWgs84;
                    double[] bboxAlbers;
                    if (SelectedQueryAreaLayer != null)
                    {
                        bboxWgs84 = await Helpers.GraphicToBoundingBoxAsync(SelectedQueryAreaLayer, 4326);
                        bboxAlbers = await Helpers.GraphicToBoundingBoxAsync(SelectedQueryAreaLayer, 3577);
                    }
                    else
                    {
                        return;
                    }

                    // Get start and end dates
                    string startDate = QueryStartDate.ToString("yyyy'-'MM'-'dd");
                    string endDate = QueryEndDate.ToString("yyyy'-'MM'-'dd");

                    // Get selected collection (as raw collection name)
                    List<string> collections = QueryCollections.Where(e => e.IsCollectionSelected).Select(e => e.RawName).ToList();
                    if (collections.Count == 0)
                    {
                        return;
                    }

                    // Get selected asset(s) based on currently selected asset tab
                    var assets = new List<string>();
                    if (SelectedAssetTabIndex == 0)
                    {
                        assets = QueryRawAssets.Where(e => e.IsRawAssetSelected).Select(e => e.RawName).ToList();
                    }
                    else if (SelectedAssetTabIndex == 1)
                    {
                        assets = QueryIndexAssets.Where(e => e.IsIndexAssetSelected).Select(e => e.Bands).FirstOrDefault();
                    }
                    else if (SelectedAssetTabIndex == 2)
                    {
                        // TODO: calculator
                    }

                    // Ensure we have a QA mask band at the end
                    if (assets.Count == 0)
                    {
                        return;
                    }
                    else if (!assets.Contains("oa_fmask"))
                    {
                        assets.Add("oa_fmask");
                    }

                    // Setup valid classes and minimum valid pixels
                    List<int> validPixels = QueryMaskValues.Where(e => e.IsMaskValueSelected).Select(e => e.Value).ToList();

                    // Ensure we have a at least one valid pixel value
                    if (validPixels.Count == 0)
                    {
                        return;
                    }

                    // Get minimum valid percentage based on max invalid
                    float minValid = Convert.ToSingle(1.0 - (QueryCloudCover / 100));

                    // Setup nodata value
                    // TODO: add this to UI
                    Int16 noDataValue = -999;

                    // Set user's output folder path
                    string outputFolder = OutputFolderPath;
                    //if (outputFolder == null || !Directory.Exists(outputFolder))
                    //{
                        //return;
                    //}

                    // Set number of download retries
                    // TODO: implement this in UI
                    //int numRetries = 5;
                    #endregion

                    #region Construct STAC results and download structure
                    // Set progressor
                    RefreshProgressBar(1, 100, "Querying STAC endpoint...", true);

                    // Construct STAC url, roots and flatten them into one
                    Root root = await QueuedTask.Run(async () =>
                    {
                        List<Root> roots = new List<Root>();
                        foreach (string collection in collections)
                        {
                            string url = Data.ConstructStacUrl(collection, startDate, endDate, bboxWgs84, 500);
                            Root root = await Data.GetStacFromUrlAsync(url);
                            roots.Add(root);
                        }

                        return Data.FlattenStacRoots(roots);
                    });
                    if (root.Features.Count == 0)
                    {
                        return;
                    }

                    // Convert STAC to download structure
                    var items = Data.ConvertStacToDownloads(root, assets, bboxAlbers);

                    // Remove Landsat 7 data where SLC-off if requested
                    if (QueryIncludeSlcOff == false)
                    {
                        items = Data.RemoveSlcOffData(items);
                    }

                    // Sort by datetime, group dates by solar day
                    items = Data.SortByDateTime(items);
                    items = Data.GroupBySolarDay(items);
                    #endregion

                    #region Download and assess fmask data
                    // Set progressor
                    RefreshProgressBar(1, items.Count, "Downloading and assessing fmask data...", false);

                    int i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, numCores, async (item, token) =>
                    {
                        // Download mask geotiff, get num valid pixels, flag item as valid (or not)
                        await item.SetValidViaWcsMaskAsync(minValid, validPixels, client);

                        // Increment progress 
                        i = i + 1;
                        if (i % 5 == 0)
                        {
                            progressValue.Report(i);
                            progressPercent.Report($"{Convert.ToInt32(i / MaxProgressValue * 100)}%");
                        }
                    }));

                    // Subset items to only those flagged as valid
                    items = items.Where(e => e.Valid == true).ToList();
                    #endregion

                    #region Stream valid data, set invalid pixels to NoData, save to folder
                    // Set progressor
                    RefreshProgressBar(1, items.Count, "Downloading and processing satellite data...", false);
                    ProgressPercentage = "";

                    i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, numCores, async (item, token) =>
                    {
                        if (SelectedAssetTabIndex == 0)
                        {
                            // Download raw raster bands requested by user
                            await item.DownloadAndProcessViaFullAsync(outputFolder, validPixels, noDataValue, client);
                        }
                        else if (SelectedAssetTabIndex == 1)
                        {
                            // Download and process raster bands into index requested by user
                            string index = QueryIndexAssets.Where(e => e.IsIndexAssetSelected).Select(e => e.ShortName).FirstOrDefault();
                            await item.DownloadAndProcessViaIndexAsync(index, outputFolder, validPixels, noDataValue, client);
                        }
                        else if (SelectedAssetTabIndex == 2)
                        {
                            // TODO: calculator
                        }

                        // Increment progress
                        i = i + 1;
                        progressValue.Report(i);
                        progressPercent.Report($"{Convert.ToInt32(i / MaxProgressValue * 100)}%");
                    }));
                    #endregion

                    // Final message
                    RefreshProgressBar(1, 1, "Finished.", false);
                    ProgressPercentage = "";
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
