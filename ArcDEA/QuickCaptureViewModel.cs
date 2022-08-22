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
using ArcGIS.Core.Data.Raster;
using System.Collections.Concurrent;

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

        // TODO: Make progress status for percent only, and make a regular text box out of here for messages
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
        /// Percentage of progress for display in textbox.
        /// </summary>
        private string _progressStatus;
        public string ProgressStatus
        {
            get { return _progressStatus; }
            set { SetProperty(ref _progressStatus, value, () => ProgressStatus); }
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
                    // Reset progressor
                    ProgressValue = 0;
                    MaxProgressValue = 100;
                    IProgress<int> progress = new Progress<int>(e => ProgressValue = e);

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
                    // TODO: make collection a list
                    string collection = SelectedQueryCollection.RawName;
                    if (collection == null)
                    {
                        return;
                    }

                    // Get selected asset(s)
                    List<string> assets = QueryAssets.Where(e => e.IsAssetSelected).Select(e => e.RawName).ToList();
                    if (assets.Count == 0)
                    {
                        return;
                    }

                    // Setup valid classes and minimum valid pixels
                    // TODO: make dynamic add to UI
                    List<int> validClasses = new List<int> { 1, 4, 5 };  
                    double minValid = 1.0 - (QueryCloudCover / 100);

                    // Set user's output folder path
                    string outputFolder = OutputFolderPath;
                    //if (outputFolder == null || !Directory.Exists(outputFolder))
                    //{
                    //return;
                    //}
                    #endregion                    

                    #region Construct STAC results and download structure
                    // Construct STAC url from provided parameters, abort if nothing
                    // TODO: put this into an iterator for multiple collections
                    Root root = await QueuedTask.Run(async () =>
                    {
                        string url = Data.ConstructStacUrl(collection, startDate, endDate, bboxWgs84, 500);
                        return await Data.GetStacFromUrlAsync(url);
                    });
                    if (root.Features.Count == 0)
                    {
                        return;
                    }

                    // Convert STAC to download structure, sort by datetime, group dates by solar day
                    var items = Data.ConvertStacToDownloads(root, assets, bboxAlbers);
                    items = Data.SortByDateTime(items);
                    items = Data.GroupBySolarDay(items);
                    #endregion


                    // Initialise progressor
                    ProgressValue = 0;
                    MaxProgressValue = items.Count;
                    progress = new Progress<int>(e => ProgressValue = e);

                    #region Download fmask data to temporary folder
                    // Get the ArcGIS temporary folder path
                    string tmpFolder = Path.GetTempPath();

                    // Open a HTTP client with 60 minute timeout
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(60);

                    // Set up number of cores for download (max available - 1)
                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    // Download fmask geotiffs to temporary folder
                    int i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, options, async (item, token) =>
                    {
                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Started download: {item.Date}");

                        // Create output filename and path for current item
                        string filepath = Path.Join(tmpFolder, item.MaskFilename);

                        // Query WCS mask URL and ensure success
                        var response = await client.GetAsync(item.MaskWcsUrl);
                        response.EnsureSuccessStatusCode();

                        // Download temporary file to temporary folder
                        // TODO: make sure access is available
                        // TODO: make sure file doesnt already exist
                        using (FileStream fs = new FileStream(filepath, FileMode.CreateNew))
                        {
                            await response.Content.CopyToAsync(fs);
                        }

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Finished download: {item.Date}");

                        // Increment progress bar
                        i = i + 1;
                        progress.Report(i);
                    }));

                    //TODO: clean up
                    //
                    #endregion

                    // Re-initialise progressor
                    ProgressValue = 0;
                    MaxProgressValue = items.Count;
                    progress = new Progress<int>(e => ProgressValue = e);

                    #region Check and remove fmask data for invalid scenes
                    // Create connection to temporary folder file store
                    Uri folderUri = new Uri(tmpFolder);
                    FileSystemConnectionPath conn = new FileSystemConnectionPath(folderUri, FileSystemDatastoreType.Raster);

                    //
                    i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, options, async (item, token) =>
                    {
                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Checking mask pixels: {item.Date}");

                        // Open new store
                        FileSystemDatastore store = new FileSystemDatastore(conn);

                        // Read raster from store
                        RasterDataset rasterDataset = store.OpenDataset<RasterDataset>(item.MaskFilename);
                        Raster raster = rasterDataset.CreateFullRaster();

                        // Get a pixel block for quicker reading and read from pixel top left pixel
                        PixelBlock block = raster.CreatePixelBlock(raster.GetWidth(), raster.GetHeight());
                        raster.Read(0, 0, block);

                        // Read 2-dimensional pixel values into 1-dimensional byte array
                        Array pixels2D = block.GetPixelData(0, false);
                        byte[] pixels1D = new byte[pixels2D.Length];
                        Buffer.BlockCopy(pixels2D, 0, pixels1D, 0, pixels2D.Length);

                        // Get distinct pixel values and their counts
                        var counts = pixels1D.GroupBy(e => e).Select(x => new { key = x.Key, val = x.Count() }).ToList();

                        // Get the total of all pixels excluding unclassified (i.e., overlap boundary areas, or 0)
                        double totalPixels = counts.Where(e => e.key != 0).Sum(e => e.val);

                        // Count percentage of valid pixels and keep if > minimum allowed
                        double validPixels = pixels1D.Where(e => validClasses.Contains(e)).ToArray().Length;

                        if ((validPixels / totalPixels) < minValid)
                        {
                            item.Valid = false;
                        }
                        else
                        {
                            item.Valid = true;
                        }

                        // TODO: Delete image for current item
                        //

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Checked mask pixel: {item.Date}");

                        // Increment progressor bar
                        i = i + 1;
                        progress.Report(i);
                    }));
                    #endregion

                    // Remove invalid dates
                    items = items.Where(e => e.Valid == true).ToList();


                    //TODO: dispose conn, store
                    //


                    // Initialise progressor
                    ProgressValue = 0;
                    MaxProgressValue = items.Count;
                    progress = new Progress<int>(e => ProgressValue = e);

                    #region Download valid data to temporary folder
                    // Download fmask geotiffs to temporary folder
                    i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, options, async (item, token) =>
                    {
                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Started download: {item.Date}");

                        // Create output filename and path for current item
                        string filepath = Path.Join(tmpFolder, item.FullFilename);

                        // Query WCS full URL and ensure success
                        var response = await client.GetAsync(item.FullWcsUrl);
                        response.EnsureSuccessStatusCode();

                        // Download temporary file to temporary folder
                        // TODO: make sure access is available
                        // TODO: make sure file doesnt already exist
                        using (FileStream fs = new FileStream(filepath, FileMode.CreateNew))
                        {
                            await response.Content.CopyToAsync(fs);
                        }

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Finished download: {item.Date}");

                        // Increment progress bar
                        i = i + 1;
                        progress.Report(i);
                    }));

                    //TODO: clean up
                    //
                    #endregion

                    // Re-initialise progressor
                    ProgressValue = 0;
                    MaxProgressValue = items.Count;
                    progress = new Progress<int>(e => ProgressValue = e);

                    #region Remove invalid pixels and save to final output folder
                    // Create connection to final output folder file store
                    conn = new FileSystemConnectionPath(new Uri(tmpFolder), FileSystemDatastoreType.Raster);
                    var outConn = new FileSystemConnectionPath(new Uri(outputFolder), FileSystemDatastoreType.Raster);

                    // Set nodata value
                    Int16 noDataValue = -1;

                    i = 0;
                    await QueuedTask.Run(() => Parallel.ForEachAsync(items, options, async (item, token) =>
                    {
                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Started final download: {item.Date}");

                        // Create full output folder path
                        string tmpFilepath = Path.Join(outputFolder, item.FullFilename);
                        string outFilepath = Path.Join(outputFolder, item.FinalFilename);

                        // Create connection to file
                        FileSystemDatastore store = new FileSystemDatastore(conn);

                        // Read raster from store
                        RasterDataset rasterDataset = store.OpenDataset<RasterDataset>(item.FullFilename);
                        Raster raster = rasterDataset.CreateFullRaster();

                        // Get a pixel block for quicker reading and read from pixel top left pixel
                        int blockHeight = raster.GetHeight();
                        int blockWidth = raster.GetWidth();
                        PixelBlock block = raster.CreatePixelBlock(blockWidth, blockHeight);
                        raster.Read(0, 0, block);

                        // 
                        var maskIndex = rasterDataset.GetBandIndex("oa_fmask");

                        //
                        Array maskPixels = block.GetPixelData(maskIndex, true);
                        for (int plane = 0; plane < block.GetPlaneCount() - 1; plane++)  // -1 to ignore mask band
                        {
                            System.Diagnostics.Debug.WriteLine($"Working on band: {plane}");

                            Array bandPixels = block.GetPixelData(plane, true);
                            for (int i = 0; i < block.GetHeight(); i++)
                            {
                                for (int j = 0; j < block.GetWidth(); j++)
                                {
                                    Int16 maskValue = Convert.ToInt16(maskPixels.GetValue(j, i));
                                    if (maskValue == 0)
                                    {
                                        bandPixels.SetValue(noDataValue, j, i);
                                    }
                                }
                            }

                            // Update block with new values
                            block.SetPixelData(plane, bandPixels);

                            // Notify
                            System.Diagnostics.Debug.WriteLine($"Finished on band: {plane}");
                        }

                        // Set store for output folder
                        FileSystemDatastore outStore = new FileSystemDatastore(outConn);
                        
                        // Write raster to folder
                        raster.Write(0, 0, block);
                        raster.SetNoDataValue(noDataValue);
                        raster.SaveAs("_" + item.FinalFilename, outStore, "TIFF");

                        //Unclassified-> 0.
                        //Clear-> 1.
                        //Cloud-> 2.
                        //Cloud Shadow -> 3.
                        //Snow-> 4.
                        //Water-> 5.

                        // Notify
                        System.Diagnostics.Debug.WriteLine($"Ended final download: {item.Date}");

                        // Increment progressor bar
                        i = i + 1;
                        progress.Report(i);
                    }));
                    #endregion
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
