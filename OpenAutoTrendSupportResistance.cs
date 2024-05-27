#region Using declarations
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class PivotPoint
    {
        public int BarNumber { get; set; }
        public double Price { get; set; }
        public double Close { get; set; }
        public bool IsHigh { get; set; }
        public bool DisplayLevel { get; set; }
        public bool IsLevelTested { get; set; }

        public PivotPoint(int barNumber, double price, double close, bool isHigh)
        {
            BarNumber = barNumber;
            Price = price;
            Close = close;
            IsHigh = isHigh;
            DisplayLevel = true;
            IsLevelTested = false;
        }
    }

    public class OpenAutoTrendSupportResistance : Indicator
    {
        public const string GROUP_NAME = "Open Auto Trend Support Resistance";

        private Brush _trendLineColor;
        private Brush _levelLineColor;
        private Brush _testLineColor;

        private List<PivotPoint> _pivots = new List<PivotPoint>();
        private PivotPoint _currentPivot = null;
        private bool _isLookingForHigh = true;
        private bool _hasFirstPivot = false;
        private double _requiredTicksForBroken = 0;

        [NinjaScriptProperty]
        [Display(Name = "Right Offset", Description = "The offset for the lines from the right.", Order = 0, GroupName = GROUP_NAME)]
        public float RightOffset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Required Ticks for Broken", Description = "The required ticks to consider a level broken.", Order = 1, GroupName = GROUP_NAME)]
        public float RequiredTicksForBroken { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Trend Line", Description = "Enable to display the trend line.", Order = 2, GroupName = GROUP_NAME)]
        public bool DisplayTrendLine { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Levels", Description = "Enable to display the levels.", Order = 3, GroupName = GROUP_NAME)]
        public bool DisplayLevels { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Trend Line Color", Description = "The trend line color.", Order = 4, GroupName = GROUP_NAME)]
        public Brush TrendLineColor
        {
            get { return _trendLineColor; }
            set { _trendLineColor = value; }
        }

        [Browsable(false)]
        public string TrendLineColorSerialize
        {
            get { return Serialize.BrushToString(_trendLineColor); }
            set { _trendLineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Level Line Color", Description = "The level line color.", Order = 5, GroupName = GROUP_NAME)]
        public Brush LevelLineColor
        {
            get { return _levelLineColor; }
            set { _levelLineColor = value; }
        }

        [Browsable(false)]
        public string LevelLineColorSerialize
        {
            get { return Serialize.BrushToString(_levelLineColor); }
            set { _levelLineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Test Level Line Color", Description = "The test level line color.", Order = 6, GroupName = GROUP_NAME)]
        public Brush TestLevelLineColor
        {
            get { return _testLineColor; }
            set { _testLineColor = value; }
        }

        [Browsable(false)]
        public string TestLevelLineColorSerialize
        {
            get { return Serialize.BrushToString(_testLineColor); }
            set { _testLineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Trend Line Opacity", Description = "The opacity for the trend line. (0 to 255)", Order = 7, GroupName = GROUP_NAME)]
        public byte TrendLineOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Level Line Opacity", Description = "The opacity for the level line and test level line. (0 to 255)", Order = 8, GroupName = GROUP_NAME)]
        public byte LevelLineOpacity { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Automatically draws the trend, support and resistance based on high and low.";
                Name = "OpenAutoTrendSupportResistance";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 20;

                RightOffset = 15;
                RequiredTicksForBroken = 4;
                DisplayTrendLine = true;
                DisplayLevels = true;

                TrendLineOpacity = 200;
                LevelLineOpacity = 200;

                TrendLineColor = Brushes.DarkGoldenrod;
                LevelLineColor = Brushes.DarkCyan;
                TestLevelLineColor = Brushes.Crimson;
            }
            else if (State == State.DataLoaded)
            {
                _currentPivot = new PivotPoint(CurrentBar, 0, 0, false);
                _requiredTicksForBroken = RequiredTicksForBroken * TickSize;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToPlot) return;

            if (!IsFirstTickOfBar) return;

            int previousBarNumber = CurrentBar - 1;
            double previousOpen = Open[1];
            double previousClose = Close[1];
            double previousHigh = High[1];
            double previousLow = Low[1];

            if (!_hasFirstPivot)
            {
                if (previousOpen <= previousClose)
                {
                    // Set first pivot as low
                    _currentPivot = new PivotPoint(previousBarNumber, previousLow, previousClose, false);
                    _pivots.Add(_currentPivot);
                }
                else
                {
                    // Set first pivot as high
                    _currentPivot = new PivotPoint(previousBarNumber, previousHigh, previousClose, true);
                    _pivots.Add(_currentPivot);
                    _isLookingForHigh = false;
                }

                _hasFirstPivot = true;
                return;
            }

            // Check level tested
            foreach (var pivot in _pivots)
            {
                if (!pivot.DisplayLevel)
                {
                    continue;
                }

                // The price is within the threshold and considered as tested
                // Check for resistance
                if (pivot.IsHigh && previousHigh >= pivot.Price && previousHigh <= pivot.Price + _requiredTicksForBroken)
                {
                    pivot.IsLevelTested = true;
                }
                // Check for support
                else if (!pivot.IsHigh && previousLow <= pivot.Price && previousLow >= pivot.Price - _requiredTicksForBroken)
                {
                    pivot.IsLevelTested = true;
                }
                // The price is outside of the threshold and considered as broken
                // Check for resistance
                else if (pivot.IsHigh && previousHigh > pivot.Price + _requiredTicksForBroken)
                {
                    pivot.DisplayLevel = false;
                }
                // Check for support
                else if (!pivot.IsHigh && previousLow < pivot.Price - _requiredTicksForBroken)
                {
                    pivot.DisplayLevel = false;
                }
            }

            if (_isLookingForHigh)
            {
                // Update the high pivot until a lower high is found
                if (previousHigh > _currentPivot.Price)
                {
                    _currentPivot.BarNumber = previousBarNumber;
                    _currentPivot.Price = previousHigh;
                    _currentPivot.Close = previousClose;
                }
                else if (previousHigh < _currentPivot.Price)
                {
                    // High pivot found and switch to looking for a low pivot
                    _pivots.Add(new PivotPoint(_currentPivot.BarNumber, _currentPivot.Price, _currentPivot.Close, true));
                    _currentPivot = new PivotPoint(previousBarNumber, previousLow, previousClose, true);
                    _isLookingForHigh = false;
                }
            }
            else
            {
                // Update the low pivot until a higher low is found
                if (previousLow < _currentPivot.Price)
                {
                    _currentPivot.BarNumber = previousBarNumber;
                    _currentPivot.Price = previousLow;
                    _currentPivot.Close = previousClose;
                }
                else if (previousLow > _currentPivot.Price)
                {
                    // Low pivot found and switch to looking for a high pivot
                    _pivots.Add(new PivotPoint(_currentPivot.BarNumber, _currentPivot.Price, _currentPivot.Close, false));
                    _currentPivot = new PivotPoint(previousBarNumber, previousHigh, previousClose, false);
                    _isLookingForHigh = true;
                }
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            // Need at least two pivots to draw a line between them
            if (_pivots.Count < 2)
            {
                return;
            }

            var trendLineColor = ConvertToDxColor(TrendLineColor, TrendLineOpacity);
            var levelLineColor = ConvertToDxColor(LevelLineColor, LevelLineOpacity);
            var testLineColor = ConvertToDxColor(TestLevelLineColor, LevelLineOpacity);

            using (var levelLineBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, levelLineColor))
            using (var brokenLineBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, testLineColor))
            using (SharpDX.Direct2D1.SolidColorBrush dxBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, trendLineColor))
            {
                if (DisplayTrendLine)
                {
                    for (int i = 1; i < _pivots.Count; i++)
                    {
                        SharpDX.Vector2 startPoint = new SharpDX.Vector2(
                            chartControl.GetXByBarIndex(ChartBars, _pivots[i - 1].BarNumber),
                            chartScale.GetYByValue(_pivots[i - 1].Price)
                        );
                        SharpDX.Vector2 endPoint = new SharpDX.Vector2(
                            chartControl.GetXByBarIndex(ChartBars, _pivots[i].BarNumber),
                            chartScale.GetYByValue(_pivots[i].Price)
                        );
                        RenderTarget.DrawLine(startPoint, endPoint, dxBrush, 2);
                    }
                }

                if (DisplayLevels)
                {
                    foreach (var pivot in _pivots)
                    {
                        if (pivot.DisplayLevel)
                        {
                            DrawHorizontalLine(chartControl, chartScale, pivot, pivot.IsLevelTested ? brokenLineBrush : levelLineBrush);
                        }
                    }
                }
            }
        }

        private SharpDX.Color ConvertToDxColor(Brush brush, byte alpha)
        {
            var color = ((SolidColorBrush)brush).Color;
            return new SharpDX.Color(color.R, color.G, color.B, alpha);
        }

        private void DrawHorizontalLine(ChartControl chartControl, ChartScale chartScale, PivotPoint pivot, SharpDX.Direct2D1.SolidColorBrush brush)
        {
            float yValue = chartScale.GetYByValue(pivot.Price);
            float startX = chartControl.GetXByBarIndex(ChartBars, pivot.BarNumber) - RightOffset;
            float endX = chartControl.GetXByBarIndex(ChartBars, ChartBars.Count - 1) - RightOffset;

            var dashes = new float[] { 2.0f, 2.0f };
            var strokeStyleProperties = new SharpDX.Direct2D1.StrokeStyleProperties()
            {
                DashStyle = SharpDX.Direct2D1.DashStyle.Custom,
                DashOffset = 0
            };

            using (var strokeStyle = new SharpDX.Direct2D1.StrokeStyle(RenderTarget.Factory, strokeStyleProperties, dashes))
            {
                RenderTarget.DrawLine(new SharpDX.Vector2(startX, yValue), new SharpDX.Vector2(endX, yValue), brush, 2, strokeStyle);
            }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private OpenAutoTrendSupportResistance[] cacheOpenAutoTrendSupportResistance;
		public OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			return OpenAutoTrendSupportResistance(Input, rightOffset, requiredTicksForBroken, displayTrendLine, displayLevels, trendLineColor, levelLineColor, testLevelLineColor, trendLineOpacity, levelLineOpacity);
		}

		public OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(ISeries<double> input, float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			if (cacheOpenAutoTrendSupportResistance != null)
				for (int idx = 0; idx < cacheOpenAutoTrendSupportResistance.Length; idx++)
					if (cacheOpenAutoTrendSupportResistance[idx] != null && cacheOpenAutoTrendSupportResistance[idx].RightOffset == rightOffset && cacheOpenAutoTrendSupportResistance[idx].RequiredTicksForBroken == requiredTicksForBroken && cacheOpenAutoTrendSupportResistance[idx].DisplayTrendLine == displayTrendLine && cacheOpenAutoTrendSupportResistance[idx].DisplayLevels == displayLevels && cacheOpenAutoTrendSupportResistance[idx].TrendLineColor == trendLineColor && cacheOpenAutoTrendSupportResistance[idx].LevelLineColor == levelLineColor && cacheOpenAutoTrendSupportResistance[idx].TestLevelLineColor == testLevelLineColor && cacheOpenAutoTrendSupportResistance[idx].TrendLineOpacity == trendLineOpacity && cacheOpenAutoTrendSupportResistance[idx].LevelLineOpacity == levelLineOpacity && cacheOpenAutoTrendSupportResistance[idx].EqualsInput(input))
						return cacheOpenAutoTrendSupportResistance[idx];
			return CacheIndicator<OpenAutoTrendSupportResistance>(new OpenAutoTrendSupportResistance(){ RightOffset = rightOffset, RequiredTicksForBroken = requiredTicksForBroken, DisplayTrendLine = displayTrendLine, DisplayLevels = displayLevels, TrendLineColor = trendLineColor, LevelLineColor = levelLineColor, TestLevelLineColor = testLevelLineColor, TrendLineOpacity = trendLineOpacity, LevelLineOpacity = levelLineOpacity }, input, ref cacheOpenAutoTrendSupportResistance);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			return indicator.OpenAutoTrendSupportResistance(Input, rightOffset, requiredTicksForBroken, displayTrendLine, displayLevels, trendLineColor, levelLineColor, testLevelLineColor, trendLineOpacity, levelLineOpacity);
		}

		public Indicators.OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(ISeries<double> input , float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			return indicator.OpenAutoTrendSupportResistance(input, rightOffset, requiredTicksForBroken, displayTrendLine, displayLevels, trendLineColor, levelLineColor, testLevelLineColor, trendLineOpacity, levelLineOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			return indicator.OpenAutoTrendSupportResistance(Input, rightOffset, requiredTicksForBroken, displayTrendLine, displayLevels, trendLineColor, levelLineColor, testLevelLineColor, trendLineOpacity, levelLineOpacity);
		}

		public Indicators.OpenAutoTrendSupportResistance OpenAutoTrendSupportResistance(ISeries<double> input , float rightOffset, float requiredTicksForBroken, bool displayTrendLine, bool displayLevels, Brush trendLineColor, Brush levelLineColor, Brush testLevelLineColor, byte trendLineOpacity, byte levelLineOpacity)
		{
			return indicator.OpenAutoTrendSupportResistance(input, rightOffset, requiredTicksForBroken, displayTrendLine, displayLevels, trendLineColor, levelLineColor, testLevelLineColor, trendLineOpacity, levelLineOpacity);
		}
	}
}

#endregion
