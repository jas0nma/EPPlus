/*************************************************************************************************
  Required Notice: Copyright (C) EPPlus Software AB. 
  This software is licensed under PolyForm Noncommercial License 1.0.0 
  and may only be used for noncommercial purposes 
  https://polyformproject.org/licenses/noncommercial/1.0.0/

  A commercial license to use this software can be purchased at https://epplussoftware.com
 *************************************************************************************************
  Date               Author                       Change
 *************************************************************************************************
  01/27/2020         EPPlus Software AB       Initial release EPPlus 5
 *************************************************************************************************/
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Drawing.Chart.Style;
using OfficeOpenXml.Drawing.Interfaces;
using OfficeOpenXml.Packaging;
using OfficeOpenXml.Table.PivotTable;
using OfficeOpenXml.Utils;
using OfficeOpenXml.Utils.Extentions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
#if !NET35 && !NET40
using System.Threading.Tasks;
#endif
using System.Xml;

namespace OfficeOpenXml.Drawing
{
    /// <summary>
    /// Collection for Drawing objects.
    /// </summary>
    public class ExcelDrawings : IEnumerable<ExcelDrawing>, IDisposable, IPictureRelationDocument
    {
        private XmlDocument _drawingsXml = new XmlDocument();
        internal Dictionary<string, int> _drawingNames;
        private List<ExcelDrawing> _drawings;
        internal class ImageCompare
        {
            internal byte[] image { get; set; }
            internal string relID { get; set; }

            internal bool Comparer(byte[] compareImg)
            {
                if (compareImg.Length != image.Length)
                {
                    return false;
                }

                for (int i = 0; i < image.Length; i++)
                {
                    if (image[i] != compareImg[i])
                    {
                        return false;
                    }
                }
                return true; //Equal
            }
        }
        //internal List<ImageCompare> _pics = new List<ImageCompare>();
        internal Dictionary<string, HashInfo> _hashes = new Dictionary<string, HashInfo>();
        internal ExcelPackage _package;
        internal Packaging.ZipPackageRelationship _drawingRelation = null;
        internal string _seriesTemplateXml;
        internal ExcelDrawings(ExcelPackage xlPackage, ExcelWorksheet sheet)
        {
            _drawingsXml = new XmlDocument();
            _drawingsXml.PreserveWhitespace = false;
            _drawings = new List<ExcelDrawing>();
            _drawingNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _package = xlPackage;
            Worksheet = sheet;
            CreateNSM();
            XmlNode node = sheet.WorksheetXml.SelectSingleNode("//d:drawing", sheet.NameSpaceManager);
            if (node != null && sheet !=null)
            {
                _drawingRelation = sheet.Part.GetRelationship(node.Attributes["r:id"].Value);
                _uriDrawing = UriHelper.ResolvePartUri(sheet.WorksheetUri, _drawingRelation.TargetUri);

                _part = xlPackage.Package.GetPart(_uriDrawing);
                XmlHelper.LoadXmlSafe(_drawingsXml, _part.GetStream());

                AddDrawings();
            }
        }
        internal ExcelWorksheet Worksheet { get; set; }
        /// <summary>
        /// A reference to the drawing xml document
        /// </summary>
        public XmlDocument DrawingXml
        {
            get
            {
                return _drawingsXml;
            }
        }
        private void AddDrawings()
        {
            XmlNodeList list = _drawingsXml.SelectNodes("//*[self::xdr:oneCellAnchor or self::xdr:twoCellAnchor or self::xdr:absoluteAnchor]", NameSpaceManager);

            foreach (XmlNode node in list)
            {

                ExcelDrawing dr;
                switch (node.LocalName)
                {
                    case "oneCellAnchor":
                    case "twoCellAnchor":
                    case "absoluteAnchor":
                        dr = ExcelDrawing.GetDrawing(this, node);
                        break;
                    default:
                        dr = null;
                        break;
                }
                if (dr != null)
                {
                    _drawings.Add(dr);
                    if (!_drawingNames.ContainsKey(dr.Name))
                    {
                        _drawingNames.Add(dr.Name, _drawings.Count - 1);
                    }
                }
            }
        }


        #region NamespaceManager
        /// <summary>
        /// Creates the NamespaceManager. 
        /// </summary>
        private void CreateNSM()
        {
            NameTable nt = new NameTable();
            NameSpaceManager = new XmlNamespaceManager(nt);
            NameSpaceManager.AddNamespace("a", ExcelPackage.schemaDrawings);
            NameSpaceManager.AddNamespace("xdr", ExcelPackage.schemaSheetDrawings);
            NameSpaceManager.AddNamespace("c", ExcelPackage.schemaChart);
            NameSpaceManager.AddNamespace("r", ExcelPackage.schemaRelationships);
            NameSpaceManager.AddNamespace("cs", ExcelPackage.schemaChartStyle);
            NameSpaceManager.AddNamespace("mc", ExcelPackage.schemaMarkupCompatibility);
            NameSpaceManager.AddNamespace("c14", ExcelPackage.schemaChart14);
        }
        internal XmlNamespaceManager NameSpaceManager { get; private set; } = null;
        #endregion
        #region IEnumerable Members
        /// <summary>
        /// Get the enumerator
        /// </summary>
        /// <returns>The enumerator</returns>
        public IEnumerator GetEnumerator()
        {
            return (_drawings.GetEnumerator());
        }
        #region IEnumerable<ExcelDrawing> Members

        IEnumerator<ExcelDrawing> IEnumerable<ExcelDrawing>.GetEnumerator()
        {
            return (_drawings.GetEnumerator());
        }

        #endregion

        /// <summary>
        /// Returns the drawing at the specified position.  
        /// </summary>
        /// <param name="PositionID">The position of the drawing. 0-base</param>
        /// <returns></returns>
        public ExcelDrawing this[int PositionID]
        {
            get
            {
                return (_drawings[PositionID]);
            }
        }

        /// <summary>
        /// Returns the drawing matching the specified name
        /// </summary>
        /// <param name="Name">The name of the worksheet</param>
        /// <returns></returns>
        public ExcelDrawing this[string Name]
        {
            get
            {
                if (_drawingNames.ContainsKey(Name))
                {
                    return _drawings[_drawingNames[Name]];
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// Number of items in the collection
        /// </summary>
        public int Count
        {
            get
            {
                if (_drawings == null)
                {
                    return 0;
                }
                else
                {
                    return _drawings.Count;
                }
            }
        }
        Packaging.ZipPackagePart _part = null;
        internal Packaging.ZipPackagePart Part
        {
            get
            {
                return _part;
            }
        }
        Uri _uriDrawing = null;
        internal int _nextChartStyleId = 100;
        /// <summary>
        /// The uri to the drawing xml file inside the package
        /// </summary>
        internal Uri UriDrawing
        {
            get
            {
                return _uriDrawing;
            }
        }
        ExcelPackage IPictureRelationDocument.Package => _package;

        Dictionary<string, HashInfo> IPictureRelationDocument.Hashes => _hashes;

        ZipPackagePart IPictureRelationDocument.RelatedPart => _part;

        Uri IPictureRelationDocument.RelatedUri => _uriDrawing;
        #endregion
        #region Add functions
        /// <summary>
        /// Adds a new chart to the worksheet.
        /// Do not support stock charts. 
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>
        /// <param name="DrawingType">The top element drawingtype. Default is OneCellAnchor for Pictures and TwoCellAnchor from Charts and Shapes</param>
        /// <returns>The chart</returns>
        public ExcelChart AddChart(string Name, eChartType ChartType, ExcelPivotTable PivotTableSource, eEditAs DrawingType = eEditAs.TwoCell)
        {
            if (_drawingNames.ContainsKey(Name))
            {
                throw new Exception("Name already exists in the drawings collection");
            }

            if (ChartType == eChartType.StockHLC ||
                ChartType == eChartType.StockOHLC ||
                ChartType == eChartType.StockVOHLC)
            {
                throw (new NotImplementedException("Chart type is not supported in the current version"));
            }
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Chart Worksheets can't have more than one chart");
            }
            XmlElement drawNode = CreateDrawingXml(DrawingType);

            ExcelChart chart = ExcelChart.GetNewChart(this, drawNode, ChartType, null, PivotTableSource);
            chart.Name = Name;
            _drawings.Add(chart);
            _drawingNames.Add(Name, _drawings.Count - 1);
            return chart;
        }
        /// <summary>
        /// Adds a new chart to the worksheet.
        /// Do not support Stock charts . 
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>
        public ExcelChart AddChart(string Name, eChartType ChartType)
        {
            return AddChart(Name, ChartType, null);
        }
        /// <summary>
        /// Add a new linechart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of linechart</param>
        /// <returns>The chart</returns>
        public ExcelLineChart AddLineChart(string Name, eLineChartType ChartType)
        {
            return (ExcelLineChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new linechart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelLineChart AddLineChart(string Name, eLineChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelLineChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Add a new area chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of linechart</param>
        /// <returns>The chart</returns>
        public ExcelAreaChart AddAreaChart(string Name, eAreaChartType ChartType)
        {
            return (ExcelAreaChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new area chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelAreaChart AddAreaChart(string Name, eAreaChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelAreaChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new barchart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of linechart</param>
        /// <returns>The chart</returns>
        public ExcelBarChart AddBarChart(string Name, eBarChartType ChartType)
        {
            return (ExcelBarChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new column- or bar- chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelBarChart AddBarChart(string Name, eLineChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelBarChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new pie chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelPieChart AddPieChart(string Name, ePieChartType ChartType)
        {
            return (ExcelPieChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new pie chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelPieChart AddPieChart(string Name, ePieChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelPieChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new doughnut chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelDoughnutChart AddDoughnutChart(string Name, eDoughnutChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelDoughnutChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new doughnut chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelDoughnutChart AddDoughnutChart(string Name, eDoughnutChartType ChartType)
        {
            return (ExcelDoughnutChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new line chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelOfPieChart AddOfPieChart(string Name, eOfPieChartType ChartType)
        {
            return (ExcelOfPieChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Add a new pie of pie or bar of pie chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelOfPieChart AddOfPieChart(string Name, eOfPieChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelOfPieChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new bubble chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelBubbleChart AddBubbleChart(string Name, eBubbleChartType ChartType)
        {
            return (ExcelBubbleChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new bubble chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelBubbleChart AddBubbleChart(string Name, eBubbleChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelBubbleChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new scatter chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelScatterChart AddScatterChart(string Name, eScatterChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelScatterChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new scatter chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelScatterChart AddScatterChart(string Name, eScatterChartType ChartType)
        {
            return (ExcelScatterChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new radar chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelRadarChart AddRadarChart(string Name, eRadarChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelRadarChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new radar chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelRadarChart AddRadarChart(string Name, eRadarChartType ChartType)
        {
            return (ExcelRadarChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a new surface chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <param name="PivotTableSource">The pivottable source for a pivotchart</param>    
        /// <returns>The chart</returns>
        public ExcelSurfaceChart AddSurfaceChart(string Name, eSurfaceChartType ChartType, ExcelPivotTable PivotTableSource)
        {
            return (ExcelSurfaceChart)AddChart(Name, (eChartType)ChartType, PivotTableSource);
        }
        /// <summary>
        /// Adds a new surface chart to the worksheet.
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ChartType">Type of chart</param>
        /// <returns>The chart</returns>    
        public ExcelSurfaceChart AddSurfaceChart(string Name, eSurfaceChartType ChartType)
        {
            return (ExcelSurfaceChart)AddChart(Name, (eChartType)ChartType, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="image">An image. Allways saved in then JPeg format</param>
        /// <returns></returns>
        public ExcelPicture AddPicture(string Name, Image image)
        {
            return AddPicture(Name, image, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Image">An image. Allways saved in then JPeg format</param>
        /// <param name="Hyperlink">Picture Hyperlink</param>
        /// <returns>A picture object</returns>
        public ExcelPicture AddPicture(string Name, Image Image, Uri Hyperlink)
        {
            if (Image != null)
            {
                if (_drawingNames.ContainsKey(Name))
                {
                    throw new Exception("Name already exists in the drawings collection");
                }
                XmlElement drawNode = CreateDrawingXml(eEditAs.OneCell);
                var pic = new ExcelPicture(this, drawNode, Image, Hyperlink);
                AddPicture(Name, pic);
                return pic;
            }
            throw (new Exception("AddPicture: Image can't be null"));
        }

        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ImageFile">The image file</param>
        /// <returns>A picture object</returns>
        public ExcelPicture AddPicture(string Name, FileInfo ImageFile)
        {
            return AddPicture(Name, ImageFile, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ImageFile">The image file</param>
        /// <param name="Hyperlink">Picture Hyperlink</param>
        /// <returns>A picture object</returns>
        public ExcelPicture AddPicture(string Name, FileInfo ImageFile, Uri Hyperlink)
        {
            ValidatePictureFile(Name, ImageFile);
            XmlElement drawNode = CreateDrawingXml(eEditAs.OneCell);
            var type = PictureStore.GetPictureType(ImageFile.Extension);
            var pic = new ExcelPicture(this, drawNode, Hyperlink);
            pic.LoadImage(new FileStream(ImageFile.FullName, FileMode.Open, FileAccess.Read), type);
            AddPicture(Name, pic);
            return pic;
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="PictureStream">An stream image.</param>
        /// <param name="PictureType">The type of image</param>
        /// <returns>A picture object</returns>
        public ExcelPicture AddPicture(string Name, Stream PictureStream, ePictureType PictureType)
        {
            return AddPicture(Name, PictureStream, PictureType, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="pictureStream">An stream image.</param>
        /// <param name="pictureType">The type of image</param>
        /// <param name="Hyperlink">Picture Hyperlink</param>
        /// <returns>A picture object</returns>
        public ExcelPicture AddPicture(string Name, Stream pictureStream, ePictureType pictureType, Uri Hyperlink)
        {
            if (pictureStream == null)
            {
                throw (new ArgumentNullException("Stream can not be null"));
            }
            if (!pictureStream.CanRead || !pictureStream.CanSeek)
            {
                throw (new IOException("Stream must be readable and seekable"));
            }

            XmlElement drawNode = CreateDrawingXml(eEditAs.OneCell);
            var pic = new ExcelPicture(this, drawNode, Hyperlink);
            pic.LoadImage(pictureStream, pictureType);
            AddPicture(Name, pic);
            return pic;
        }
#region AddPictureAsync
#if !NET35 && !NET40
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ImageFile">The image file</param>
        /// <returns>A picture object</returns>
        public async Task<ExcelPicture> AddPictureAsync(string Name, FileInfo ImageFile)
        {
            return await AddPictureAsync(Name, ImageFile, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="ImageFile">The image file</param>
        /// <param name="Hyperlink">Picture Hyperlink</param>
        /// <returns>A picture object</returns>
        public async Task<ExcelPicture> AddPictureAsync(string Name, FileInfo ImageFile, Uri Hyperlink)
        {
            ValidatePictureFile(Name, ImageFile);
            XmlElement drawNode = CreateDrawingXml(eEditAs.OneCell);
            var type = PictureStore.GetPictureType(ImageFile.Extension);
            var pic = new ExcelPicture(this, drawNode, Hyperlink);
            await pic.LoadImageAsync(new FileStream(ImageFile.FullName, FileMode.Open, FileAccess.Read), type);
            AddPicture(Name, pic);
            return pic;
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="PictureStream">An stream image.</param>
        /// <param name="PictureType">The type of image</param>
        /// <returns>A picture object</returns>
        public async Task<ExcelPicture> AddPictureAsync(string Name, Stream PictureStream, ePictureType PictureType)
        {
            return await AddPictureAsync(Name, PictureStream, PictureType, null);
        }
        /// <summary>
        /// Adds a picture to the worksheet
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="pictureStream">An stream image.</param>
        /// <param name="pictureType">The type of image</param>
        /// <param name="Hyperlink">Picture Hyperlink</param>
        /// <returns>A picture object</returns>
        public async Task<ExcelPicture> AddPictureAsync(string Name, Stream pictureStream, ePictureType pictureType, Uri Hyperlink)
        {
            if (pictureStream == null)
            {
                throw (new ArgumentNullException("Stream can not be null"));
            }
            if (!pictureStream.CanRead || !pictureStream.CanSeek)
            {
                throw (new IOException("Stream must be readable and seekable"));
            }

            XmlElement drawNode = CreateDrawingXml(eEditAs.OneCell);
            var pic = new ExcelPicture(this, drawNode, Hyperlink);
            await pic.LoadImageAsync(pictureStream, pictureType);
            AddPicture(Name, pic);
            return pic;
        }
#endif
#endregion
        private void AddPicture(string Name, ExcelPicture pic)
        {
            pic.Name = Name;
            _drawings.Add(pic);
            _drawingNames.Add(Name, _drawings.Count - 1);
        }

        private void ValidatePictureFile(string Name, FileInfo ImageFile)
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Chart worksheets can't have more than one drawing");
            }
            if (ImageFile == null)
            {
                throw (new Exception("AddPicture: ImageFile can't be null"));
            }
            if (!ImageFile.Exists)
            {
                throw new FileNotFoundException("Cant find file.", ImageFile.FullName);
            }

            if (_drawingNames.ContainsKey(Name))
            {
                throw new Exception("Name already exists in the drawings collection");
            }
        }
    
        /// <summary>
        /// Adds a new chart using an crtx template
        /// </summary>
        /// <param name="crtxFile">The crtx file</param>
        /// <param name="name">The name of the chart</param>
        /// <returns>The new chart</returns>
        public ExcelChart AddChartFromTemplate(FileInfo crtxFile, string name)
        {
            return AddChartFromTemplate(crtxFile, name, null);
        }
        /// <summary>
        /// Adds a new chart using an crtx template
        /// </summary>
        /// <param name="crtxFile">The crtx file</param>
        /// <param name="name">The name of the chart</param>
        /// <param name="pivotTableSource">Pivot table source, if the chart is a pivottable</param>
        /// <returns>The new chart</returns>
        public ExcelChart AddChartFromTemplate(FileInfo crtxFile, string name, ExcelPivotTable pivotTableSource)
        {
            if(!crtxFile.Exists)
            {
                throw (new FileNotFoundException($"{crtxFile.FullName} can not be found."));
            }
            FileStream fs = null;
            try
            {
                fs = crtxFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                return AddChartFromTemplate(fs, name);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (fs!=null)
                    fs.Close();
            }
        }

        /// <summary>
        /// Adds a new chart using an crtx template
        /// </summary>
        /// <param name="crtxStream">The crtx file as a stream</param>
        /// <param name="name">The name of the chart</param>
        /// <returns>The new chart</returns>
        public ExcelChart AddChartFromTemplate(Stream crtxStream, string name)
        {
            return AddChartFromTemplate(crtxStream, name, null);
        }
        /// <summary>
        /// Adds a new chart using an crtx template
        /// </summary>
        /// <param name="crtxStream">The crtx file as a stream</param>
        /// <param name="name">The name of the chart</param>
        /// <param name="pivotTableSource">Pivot table source, if the chart is a pivottable</param>
        /// <returns>The new chart</returns>
        public ExcelChart AddChartFromTemplate(Stream crtxStream, string name, ExcelPivotTable pivotTableSource)
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Chart worksheets can't have more than one drawing");
            }
            CrtxTemplateHelper.LoadCrtx(crtxStream, out XmlDocument chartXml, out XmlDocument styleXml, out XmlDocument colorsXml, out ZipPackagePart themePart, "The crtx stream");
            if (chartXml == null)
            {
                throw new InvalidDataException("Crtx file is corrupt.");
            }
            var chartXmlHelper = XmlHelperFactory.Create(NameSpaceManager, chartXml.DocumentElement);
            var serNode = chartXmlHelper.GetNode("/c:chartSpace/c:chart/c:plotArea/*[substring(name(), string-length(name()) - 4) = 'Chart']/c:ser");
            if(serNode!=null)
            {
                _seriesTemplateXml = serNode.InnerXml;
                serNode.ParentNode.RemoveChild(serNode);
            }
            XmlElement drawNode = CreateDrawingXml(eEditAs.TwoCell);
            //ExcelChart chart = ExcelChart.CreateChartFromXml(this, drawNode, chartXml);            
            var chartType = ExcelChart.GetChartTypeFromNodeName(GetChartNodeName(chartXmlHelper));
            var chart = ExcelChart.GetNewChart(this, drawNode, chartType, null, pivotTableSource, chartXml);
            
            chart.Name = name;
            _drawings.Add(chart);
            _drawingNames.Add(name, _drawings.Count - 1);
            var chartStyle = chart.Style;
            if(chartStyle==eChartStyle.None)
            {
                chartStyle = eChartStyle.Style2;
            }
            if(themePart!=null)
            {
                chart.StyleManager.LoadThemeOverrideXml(themePart);
            }
            chart.StyleManager.LoadStyleXml(styleXml, chartStyle, colorsXml);

            return chart;
        }
        private string GetChartNodeName(XmlHelper xmlHelper)
        {
            var ploterareaNode = xmlHelper.GetNode(ExcelChart.plotAreaPath);
            foreach(XmlNode node in ploterareaNode?.ChildNodes)
            {
                if(node.LocalName.EndsWith("Chart"))
                {
                    return node.LocalName;
                }
            }
            return "";
        }
        /// <summary>
        /// Adds a new shape to the worksheet
        /// </summary>
        /// <param name="Name">Name</param>
        /// <param name="Style">Shape style</param>
        /// <returns>The shape object</returns>

        public ExcelShape AddShape(string Name, eShapeStyle Style)
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Chart worksheets can't have more than one drawing");
            }
            if (_drawingNames.ContainsKey(Name))
            {
                throw new Exception("Name already exists in the drawings collection");
            }
            XmlElement drawNode = CreateDrawingXml();

            ExcelShape shape = new ExcelShape(this, drawNode, Style);
            shape.Name = Name;
            _drawings.Add(shape);
            _drawingNames.Add(Name, _drawings.Count - 1);
            return shape;
        }
        ///// <summary>
        ///// Adds a line connectin two shapes
        ///// </summary>
        ///// <param name="Name">The Name</param>
        ///// <param name="Style">The connectorStyle</param>
        ///// <param name="StartShape">The starting shape to connect</param>
        ///// <param name="EndShape">The ending shape to connect</param>
        ///// <returns></returns>
        //public ExcelConnectionShape AddShape(string Name, eShapeConnectorStyle Style, ExcelShape StartShape, ExcelShape EndShape)
        //{
        //    if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
        //    {
        //        throw new InvalidOperationException("Chart worksheets can't have more than one drawing");
        //    }
        //    if (_drawingNames.ContainsKey(Name))
        //    {
        //        throw new Exception("Name already exists in the drawings collection");
        //    }
        //    var drawNode = CreateDrawingXml();

        //    var shape = new ExcelConnectionShape(this, drawNode, Style, StartShape, EndShape);

        //    shape.Name = Name;
        //    _drawings.Add(shape);
        //    _drawingNames.Add(Name, _drawings.Count - 1);
        //    return shape;
        //}

        /// <summary>
        /// Adds a new shape to the worksheet
        /// </summary>
        /// <param name="Name">Name</param>
        /// <param name="Source">Source shape</param>
        /// <returns>The shape object</returns>
        public ExcelShape AddShape(string Name, ExcelShape Source)
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Chart worksheets can't have more than one drawing");
            }
            if (_drawingNames.ContainsKey(Name))
            {
                throw new Exception("Name already exists in the drawings collection");
            }
            XmlElement drawNode = CreateDrawingXml();
            drawNode.InnerXml = Source.TopNode.InnerXml;

            ExcelShape shape = new ExcelShape(this, drawNode);
            shape.Name = Name;
            shape.Style = Source.Style;
            _drawings.Add(shape);
            _drawingNames.Add(Name, _drawings.Count - 1);
            return shape;
        }

        private XmlElement CreateDrawingXml(eEditAs topNodeType = eEditAs.TwoCell)
        {
            if (DrawingXml.DocumentElement == null)
            {
                DrawingXml.LoadXml(string.Format("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><xdr:wsDr xmlns:xdr=\"{0}\" xmlns:a=\"{1}\" />", ExcelPackage.schemaSheetDrawings, ExcelPackage.schemaDrawings));
                Packaging.ZipPackage package = Worksheet._package.Package;

                //Check for existing part, issue #100
                var id = Worksheet.SheetID;
                do
                {
                    _uriDrawing = new Uri(string.Format("/xl/drawings/drawing{0}.xml", id++), UriKind.Relative);
                }
                while (package.PartExists(_uriDrawing));

                _part = package.CreatePart(_uriDrawing, "application/vnd.openxmlformats-officedocument.drawing+xml", _package.Compression);

                StreamWriter streamChart = new StreamWriter(_part.GetStream(FileMode.Create, FileAccess.Write));
                DrawingXml.Save(streamChart);
                streamChart.Close();
                package.Flush();

                _drawingRelation = Worksheet.Part.CreateRelationship(UriHelper.GetRelativeUri(Worksheet.WorksheetUri, _uriDrawing), Packaging.TargetMode.Internal, ExcelPackage.schemaRelationships + "/drawing");
                XmlElement e = (XmlElement)Worksheet.CreateNode("d:drawing");
                e.SetAttribute("id", ExcelPackage.schemaRelationships, _drawingRelation.Id);

                package.Flush();
            }
            XmlNode colNode = _drawingsXml.SelectSingleNode("//xdr:wsDr", NameSpaceManager);
            XmlElement drawNode;

            var topElementname = $"{topNodeType.ToEnumString()}Anchor";
            drawNode = _drawingsXml.CreateElement("xdr", topElementname, ExcelPackage.schemaSheetDrawings);
            colNode.AppendChild(drawNode);
            if (topNodeType == eEditAs.OneCell || topNodeType == eEditAs.TwoCell)
            {
                //Add from position Element;
                XmlElement fromNode = _drawingsXml.CreateElement("xdr", "from", ExcelPackage.schemaSheetDrawings);
                drawNode.AppendChild(fromNode);
                fromNode.InnerXml = "<xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff>";
            }
            else
            {
                //Add from position Element;
                XmlElement posNode = _drawingsXml.CreateElement("xdr", "pos", ExcelPackage.schemaSheetDrawings);
                posNode.SetAttribute("x", "0");
                posNode.SetAttribute("y", "0");
                drawNode.AppendChild(posNode);
            }

            if (topNodeType == eEditAs.TwoCell)
            {
                //Add to position Element;
                XmlElement toNode = _drawingsXml.CreateElement("xdr", "to", ExcelPackage.schemaSheetDrawings);
                drawNode.AppendChild(toNode);
                toNode.InnerXml = "<xdr:col>10</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>10</xdr:row><xdr:rowOff>0</xdr:rowOff>";
            }
            else
            {
                //Add from position Element;
                XmlElement posNode = _drawingsXml.CreateElement("xdr", "ext", ExcelPackage.schemaSheetDrawings);
                posNode.SetAttribute("cx", "6072876");
                posNode.SetAttribute("cy", "9299263");
                drawNode.AppendChild(posNode);
            }

            return drawNode;
        }
        #endregion
        #region Remove methods
        /// <summary>
        /// Removes a drawing.
        /// </summary>
        /// <param name="Index">The index of the drawing</param>
        public void Remove(int Index)
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Can' remove charts from chart worksheets");
            }
            RemoveDrawing(Index);
        }

        internal void RemoveDrawing(int Index)
        {
            var draw = _drawings[Index];
            draw.DeleteMe();
            for (int i = Index + 1; i < _drawings.Count; i++)
            {
                _drawingNames[_drawings[i].Name]--;
            }
            _drawingNames.Remove(draw.Name);
            _drawings.Remove(draw);
        }
        /// <summary>
        /// Removes a drawing.
        /// </summary>
        /// <param name="Drawing">The drawing</param>
        public void Remove(ExcelDrawing Drawing)
        {
            Remove(_drawingNames[Drawing.Name]);
        }
        /// <summary>
        /// Removes a drawing.
        /// </summary>
        /// <param name="Name">The name of the drawing</param>
        public void Remove(string Name)
        {
            Remove(_drawingNames[Name]);
        }
        /// <summary>
        /// Removes all drawings from the collection
        /// </summary>
        public void Clear()
        {
            if (Worksheet is ExcelChartsheet && _drawings.Count > 0)
            {
                throw new InvalidOperationException("Can' remove charts from chart worksheets");
            }
            ClearDrawings();
        }

        internal void ClearDrawings()
        {
            while (Count > 0)
            {
                RemoveDrawing(0);
            }
        }
        #endregion
        #region BringToFront & SendToBack
        internal void BringToFront(ExcelDrawing drawing)
        {
            var index = _drawings.IndexOf(drawing);
            var endIndex = _drawings.Count - 1;
            if (index == endIndex)
            {
                return;
            }

            //Move in Xml
            var parentNode = drawing.TopNode.ParentNode;
            parentNode.RemoveChild(drawing.TopNode);
            parentNode.InsertAfter(drawing.TopNode, parentNode.LastChild);

            //Move in list 
            _drawings.RemoveAt(index);
            _drawings.Insert(endIndex, drawing);

            //Reindex dictionary
            _drawingNames[drawing.Name] = endIndex;
            for (int i = index+0; i < endIndex; i++)
            {
                _drawingNames[_drawings[i].Name]--;
            }
            }
        internal void SendToBack(ExcelDrawing drawing)
        {
            var index = _drawings.IndexOf(drawing);
            if(index==0)
            {
                return;
            }

            //Move in Xml
            var parentNode = drawing.TopNode.ParentNode;
            parentNode.RemoveChild(drawing.TopNode);
            parentNode.InsertBefore(drawing.TopNode, parentNode.FirstChild);

            //Move in list 
            _drawings.RemoveAt(index);
            _drawings.Insert(0, drawing);

            //Reindex dictionary
            _drawingNames[drawing.Name] = 0;
            for(int i=1;i<=index;i++)
            {
                _drawingNames[_drawings[i].Name]++;
            }
        }
        #endregion 
        internal void AdjustWidth(double[,] pos)
        {
            var ix = 0;
            //Now set the size for all drawings depending on the editAs property.
            foreach (OfficeOpenXml.Drawing.ExcelDrawing d in this)
            {
                if (d.EditAs != Drawing.eEditAs.TwoCell)
                {
                    if (d.EditAs == Drawing.eEditAs.Absolute)
                    {
                        d.SetPixelLeft(pos[ix, 0]);
                    }
                    d.SetPixelWidth(pos[ix, 1]);

                }
                ix++;
            }
        }
        internal void AdjustHeight(double[,] pos)
        {
            var ix = 0;
            //Now set the size for all drawings depending on the editAs property.
            foreach (OfficeOpenXml.Drawing.ExcelDrawing d in this)
            {
                if (d.EditAs != Drawing.eEditAs.TwoCell)
                {
                    if (d.EditAs == Drawing.eEditAs.Absolute)
                    {
                        d.SetPixelTop(pos[ix, 0]);
                    }
                    d.SetPixelHeight(pos[ix, 1]);

                }
                ix++;
            }
        }
        internal double[,] GetDrawingWidths()
        {
            double[,] pos = new double[Count, 2];
            int ix = 0;
            //Save the size for all drawings
            foreach (ExcelDrawing d in this)
            {
                pos[ix, 0] = d.GetPixelLeft();
                pos[ix++, 1] = d.GetPixelWidth();
            }
            return pos;
        }
        internal double[,] GetDrawingHeight()
        {
            double[,] pos = new double[Count, 2];
            int ix = 0;
            //Save the size for all drawings
            foreach (ExcelDrawing d in this)
            {
                pos[ix, 0] = d.GetPixelTop();
                pos[ix++, 1] = d.GetPixelHeight();
            }
            return pos;
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public void Dispose()
        {
            _drawingsXml = null;
            _hashes.Clear();
            _hashes = null;
            _part = null;
            _drawingNames.Clear();
            _drawingNames = null;
            _drawingRelation = null;
            foreach (var d in _drawings)
            {
                d.Dispose();
            }
            _drawings.Clear();
            _drawings = null;
        }

        internal ExcelDrawing GetById(int id)
        {
            foreach (var d in _drawings)
            {
                if (d.Id == id)
                {
                    return d;
                }
            }
            return null;
        }
    }
}
