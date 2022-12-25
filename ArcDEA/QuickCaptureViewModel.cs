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
using System.Diagnostics;

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
        private static readonly object _lockQueryDatasets = new object();
        protected QuickCaptureViewModel()
        {
            BindingOperations.EnableCollectionSynchronization(QueryAreaLayers, _lockQueryAreaLayers);
            BindingOperations.EnableCollectionSynchronization(QueryDatasets, _lockQueryDatasets);

            // TODO: do all your populate list functions here
            // its how esri do it

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
            set 
            { 
                SetProperty(ref _selectedQueryAreaLayer, value, () => SelectedQueryAreaLayer);
                ShowDatasetsControls = "Visible";
            }
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

        #region QueryDataset controls
        /// <summary>
        /// Control to show or hide dataset controls when study area set.
        /// </summary>
        private string _showDatasetsControls = "Collapsed";
        public string ShowDatasetsControls
        {
            get { return _showDatasetsControls; }
            set { SetProperty(ref _showDatasetsControls, value, () => ShowDatasetsControls); }
        }

        /// <summary>
        /// Control to show or hide slc-off control when landsat set.
        /// </summary>
        private string _showSlcOffControl = "Collapsed";
        public string ShowSlcOffControl
        {
            get { return _showSlcOffControl; }
            set { SetProperty(ref _showSlcOffControl, value, () => ShowSlcOffControl); }
        }

        /// <summary>
        /// List of available query datasets on contents pane.
        /// </summary>
        private List<Helpers.DatasetItem> _queryDatasets = Helpers.PopulateDatasetItems();
        public List<Helpers.DatasetItem> QueryDatasets
        {
            get { return _queryDatasets; }
            set { SetProperty(ref _queryDatasets, value, () => QueryDatasets); }
        }

        /// <summary>
        /// Selected query dataset from combobox.
        /// </summary>
        private Helpers.DatasetItem _selectedQueryDataset;
        public Helpers.DatasetItem SelectedQueryDataset
        {
            get { return _selectedQueryDataset; }
            set 
            { 
                SetProperty(ref _selectedQueryDataset, value, () => SelectedQueryDataset);

                // Update collections control
                if (_selectedQueryDataset != null)
                {
                    QueryCollections = Helpers.PopulateCollectionItems(_selectedQueryDataset.Name);
                    QueryRawAssets = Helpers.PopulateRawAssetItems(_selectedQueryDataset.Name);

                    ShowDatesControls = "Visible"; 
                    ShowCollectionsAndAssetsControls = "Visible";  // TODO: put in if statement check ls or s2, if so, show dates and assets, else just assets

                    if (SelectedQueryDataset.Name == "Landsat")
                    {
                        ShowSlcOffControl = "Visible";
                        QueryResolution = "30";
                    }
                    else if (SelectedQueryDataset.Name == "Sentinel")
                    {
                        ShowSlcOffControl = "Collapsed";
                        QueryResolution = "10";
                    }

                    ShowResamplingControls = "Visible";
                    ShowQualityOptionsControls = "Visible";
                };
            }
        }
        #endregion

        #region QueryDates controls
        // 
        private string _showDatesControls = "Hidden";
        public string ShowDatesControls
        {
            get { return _showDatesControls; }
            set { SetProperty(ref _showDatesControls, value, () => ShowDatesControls); }
        }

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
        // 
        private string _showCollectionsAndAssetsControls = "Hidden";
        public string ShowCollectionsAndAssetsControls
        {
            get { return _showCollectionsAndAssetsControls; }
            set { SetProperty(ref _showCollectionsAndAssetsControls, value, () => ShowCollectionsAndAssetsControls); }
        }

        /// <summary>
        /// List of available DEA data collections.
        /// </summary>
        private List<Helpers.CollectionItem> _queryCollections = null;
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
        private List<Helpers.AssetRawItem> _queryRawAssets = null;
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
        // 
        private string _showQualityOptionsControls = "Hidden";
        public string ShowQualityOptionsControls
        {
            get { return _showQualityOptionsControls; }
            set { SetProperty(ref _showQualityOptionsControls, value, () => ShowQualityOptionsControls); }
        }

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

        #region QueryResampling controls
        /// <summary>
        /// Set resampling options.
        /// </summary>
        private string _showResamplingControls = "Hidden";
        public string ShowResamplingControls
        {
            get { return _showResamplingControls; }
            set { SetProperty(ref _showResamplingControls, value, () => ShowResamplingControls); }
        }

        //
        private string _queryResolution = "30";
        public string QueryResolution
        {
            get { return _queryResolution; }
            set { SetProperty(ref _queryResolution, value, () => QueryResolution); }
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
        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get { return _isProgressIndeterminate; }
            set { SetProperty(ref _isProgressIndeterminate, value, () => IsProgressIndeterminate); }
        }
        public void RefreshProgressBar(int min, int max, string message, bool isInterminate)
        {
            ProgressValue = min;
            MaxProgressValue = max;
            ProgressMessage = message;
            IsProgressIndeterminate = isInterminate;
        }
        #endregion

        #region Processing controls
        /// <summary>
        /// Sets whether the process is running or not.
        /// </summary>

        /// <summary>
        /// Processing messages for display in textbox.
        /// </summary>
        private string _processingMessages;
        public string ProcessingMessages
        {
            get { return _processingMessages; }
            set { SetProperty(ref _processingMessages, value, () => ProcessingMessages); }
        }


        private bool _isNotPocessing = true;
        public bool IsNotProcessing
        {
            get { return _isNotPocessing; }
            set { SetProperty(ref _isNotPocessing, value, () => IsNotProcessing); }
        }
        #endregion


        private string _processingPanelVisbility = "Hidden";
        public string ProcessingPanelVisbility
        {
            get { return _processingPanelVisbility; }
            set { SetProperty(ref _processingPanelVisbility, value, () => ProcessingPanelVisbility); }
        }

        private string _processingPanelHeight = "*";
        public string ProcessingPanelHeight
        {
            get { return _processingPanelHeight; }
            set { SetProperty(ref _processingPanelHeight, value, () => ProcessingPanelHeight); }
        }



        public ICommand CmdHideProcessing
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    if (ProcessingPanelVisbility == "Hidden")
                    {
                        ProcessingPanelVisbility = "Visible";
                        //ProcessingPanelHeight = "Auto";
                        ProcessingPanelHeight = "144";
                    }
                    else
                    {
                        ProcessingPanelVisbility = "Hidden";
                        ProcessingPanelHeight = "54";
                    }
                });
            }
        }


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
                    // Start stop watch
                    var timer = Stopwatch.StartNew();

                    // Flag that process is now running
                    IsNotProcessing = false;

                    // Set starting time message
                    ProcessingMessages = $"Start Time: {DateTime.Now.ToString("dddd, dd MMMM yyyy")}" + Environment.NewLine;

                    // Set progressor
                    RefreshProgressBar(0, 100, "Initialising...", true);
                    ProgressPercentage = "";
                    IProgress<int> progressValue = new Progress<int>(e => ProgressValue = e);
                    IProgress<string> progressPercent = new Progress<string>(e => ProgressPercentage = e);
                    ProcessingMessages += Environment.NewLine + "Initializing..." + Environment.NewLine;

                    // Get the ArcGIS temporary folder path
                    string tmpFolder = Path.GetTempPath();

                    // Set download and processing num cores
                    int availableCores = Environment.ProcessorCount - 1;
                    //availableCores = availableCores * 2; // TODO: temp

                    var numCores = new ParallelOptions { MaxDegreeOfParallelism = availableCores };
                    System.Diagnostics.Debug.WriteLine($"Using {availableCores} cores.");
                    ProcessingMessages += $"Using {availableCores} cores." + Environment.NewLine;

                    // TESTING
                    numCores.MaxDegreeOfParallelism = 1;

                    // Open a HTTP client with 30 minute timeout
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(30);
                    #endregion

                    #region Get and check parameters from UI
                    // Get bounding box of graphic (in wgs84)
                    if (SelectedQueryAreaLayer == null)
                    {
                        return;
                    }
                    double[] inBoundingBox = await Helpers.GraphicToBoundingBoxAsync(SelectedQueryAreaLayer, 4326);

                    // Get start and end dates
                    string startDate = QueryStartDate.ToString("yyyy'-'MM'-'dd");
                    string endDate = QueryEndDate.ToString("yyyy'-'MM'-'dd");

                    // Get selected collection (as raw collection name)
                    string[] collections = QueryCollections.Where(e => e.IsSelected).Select(e => e.RawName).ToArray();
                    if (collections.Length == 0)
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
                    if (validPixels.Count == 0)
                    {
                        return;
                    }

                    // Get minimum valid percentage based on max invalid
                    float minValid = Convert.ToSingle(1.0 - (QueryCloudCover / 100));

                    // Setup epsg value
                    // TODO: add this to UI
                    int epsg = 3577;

                    // Setup resolution value todo error handling
                    int resolution = Convert.ToInt32(QueryResolution);

                    // Setup nodata value
                    // TODO: add this to UI
                    //Int16 noDataValue = -999;

                    // TODO: set this on UI
                    bool dropMaskBand = true;

                    // Set user's output folder path
                    string outputFolder = OutputFolderPath;
                    if (outputFolder == null || !Directory.Exists(outputFolder))
                    {
                        return;
                    }

                    // Set number of download retries
                    // TODO: implement this in UI
                    //int numRetries = 5;
                    #endregion

                    #region Construct STAC results and download structure
                    // Set progressor
                    RefreshProgressBar(1, 100, "Querying STAC endpoint...", true);
                    ProcessingMessages += Environment.NewLine + "Querying STAC endpoint..." + Environment.NewLine;

                    // Initialise stac endpoint with relevant query details and populate via download
                    Stac stac = new Stac(collections, assets.ToArray(), startDate, endDate, inBoundingBox, epsg, resolution);
                    await QueuedTask.Run(async () => {
                        await stac.GetFeaturesAsync(client);
                    });

                    // Check we got something
                    if (stac.Features.Count == 0)
                    {
                        ProcessingMessages += $"No images found." + Environment.NewLine;
                        return;
                    }
                    else
                    {
                        ProcessingMessages += $"Found {stac.Features.Count} images." + Environment.NewLine;
                    }

                    // Remove any scenes where too many pixels outside bounding bix (4326)
                    double userOverlapPercentage = 15.0;  // todo: move this to ui
                    stac.RemoveOvershootFeatures(inBoundingBox, userOverlapPercentage, 4326);  // todo clean this up inside


                    // Remove Landsat 7 data where SLC-off if requested
                    // todo: if sentinel 2, ignore
                    if (QueryIncludeSlcOff == false)
                    {
                        stac.RemoveSlcOffFeatures();
                    }

                    // Group by solar day (will sort), do another sort by date time (ascending) 
                    stac.GroupBySolarDay();
                    stac.SortFeaturesByDate();

                    // Convert to list of download items (one per date)
                    List<Download> downloads = stac.ConvertFeaturesToDownloads();
                    #endregion

                    #region Download and assess fmask data
                    // Set progressor
                    RefreshProgressBar(1, downloads.Count, "Downloading and assessing fmask data...", false);
                    ProcessingMessages += Environment.NewLine + "Downloading and assessing fmask data..." + Environment.NewLine;

                    int i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(downloads, numCores, async (download, token) =>
                    {
                        // Download mask geotiff, get num valid pixels, flag item as valid (or not)
                        await download.CheckValidityViaMaskAsync(validPixels, minValid);

                        // Update message
                        ProcessingMessages += $"> Downloaded ID: {download.Id}" + Environment.NewLine;

                        // Increment progress 
                        i = i + 1;
                        if (i % 5 == 0)
                        {
                            progressValue.Report(i);
                            progressPercent.Report($"{Convert.ToInt32(i / MaxProgressValue * 100)}%");
                        }
                    }));

                    // Subset downloads to only those flagged as valid
                    downloads = downloads.Where(e => e.IsValid == true).ToList();

                    // Check we got something
                    if (downloads.Count == 0)
                    {
                        ProcessingMessages += "No valid images remain after fmask assessment." + Environment.NewLine;
                        return;
                    }
                    else
                    {
                        ProcessingMessages += Environment.NewLine + $"Assessment of fmask found {downloads.Count} valid images." + Environment.NewLine;
                    }
                    #endregion

                    #region Download valid data, set invalid pixels to NoData, save to folder
                    // Set progressor
                    RefreshProgressBar(1, downloads.Count, "Downloading and processing valid satellite data...", false);
                    ProgressPercentage = "";
                    ProcessingMessages += Environment.NewLine + "Downloading and processing valid satellite data..." + Environment.NewLine;

                    i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(downloads, numCores, async (download, token) =>
                    {
                        if (SelectedAssetTabIndex == 0)
                        {
                            // Download full set of raster bands requested by user
                            await download.DownloadAndProcessFullAsync(outputFolder, validPixels, dropMaskBand);
                        }
                        else if (SelectedAssetTabIndex == 1)
                        {
                            // Download and process raster bands into index requested by user
                            string index = QueryIndexAssets.Where(e => e.IsIndexAssetSelected).Select(e => e.ShortName).FirstOrDefault();
                            await download.DownloadAndProcessIndexAsync(outputFolder, index, validPixels, dropMaskBand);
                        }
                        else if (SelectedAssetTabIndex == 2)
                        {
                            // TODO: calculator
                        }

                        // Update message
                        ProcessingMessages += $"> Downloaded ID: {download.Id}" + Environment.NewLine;

                        // Increment progress
                        i = i + 1;
                        progressValue.Report(i);
                        progressPercent.Report($"{Convert.ToInt32(i / MaxProgressValue * 100)}%");
                    }));
                    #endregion

                    // Final message
                    RefreshProgressBar(1, 1, "Finished.", false);
                    ProgressPercentage = "";
                    ProcessingMessages += Environment.NewLine + "Finished." + Environment.NewLine;

                    // Set success date time message
                    ProcessingMessages += Environment.NewLine + $"Succeeded at {DateTime.Now.ToString("dddd, dd MMMM yyyy")}" + " " + Environment.NewLine;

                    // Set success duration
                    timer.Stop();
                    var duration = Math.Round(timer.Elapsed.TotalMilliseconds, 2);
                    ProcessingMessages += $"(Elapsed Time: {duration} seconds).";

                    // Turn processing flag off
                    IsNotProcessing = true;

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
