//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
// File: Image.cs 
//
// Description: Contains the Image class. 
//              Layout Spec at http://avalon/layout/Specs/Image.xml 
//
// History: 
//  06/02/2003 : greglett  - Added to WCP branch, layout functionality (was Image.cs in old branch)
//
//---------------------------------------------------------------------------
 
using MS.Internal;
using MS.Internal.PresentationFramework; 
using MS.Utility; 
using System.Diagnostics;
using System.ComponentModel; 
#if OLD_AUTOMATION
using System.Windows.Automation.Provider;
#endif
using System.Windows.Threading; 
using System.Windows.Media;
using System.Windows.Media.Imaging; 
using System.Windows.Documents; 
using System.Windows.Markup;
using System.Windows.Navigation; 

using System;

namespace System.Windows.Controls 
{
    /// <summary> 
    /// </summary> 
    [Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)]
#if OLD_AUTOMATION 
    [Automation(AccessibilityControlType = "Image")]
#endif
    public class Image : FrameworkElement, IUriContext, IProvidePropertyFallback
    { 
        //-------------------------------------------------------------------
        // 
        //  Constructors 
        //
        //------------------------------------------------------------------- 

        #region Constructors

        /// <summary> 
        ///     Default DependencyObject constructor
        /// </summary> 
        /// <remarks> 
        ///     Automatic determination of current Dispatcher. Use alternative constructor
        ///     that accepts a Dispatcher for best performance. 
        /// </remarks>
        public Image() : base()
        {
        } 

        #endregion 
 
        //--------------------------------------------------------------------
        // 
        //  Public Methods
        //
        //-------------------------------------------------------------------
 
        //--------------------------------------------------------------------
        // 
        //  Public Properties 
        //
        //-------------------------------------------------------------------- 

        #region Public Properties

        /// <summary> 
        /// Gets/Sets the Source on this Image.
        /// 
        /// The Source property is the ImageSource that holds the actual image drawn. 
        /// </summary>
        public ImageSource Source 
        {
            get { return (ImageSource) GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        } 

        /// <summary> 
        /// Gets/Sets the Stretch on this Image. 
        /// The Stretch property determines how large the Image will be drawn.
        /// </summary> 
        /// <seealso cref="Image.StretchProperty" />
        public Stretch Stretch
        {
            get { return (Stretch) GetValue(StretchProperty); } 
            set { SetValue(StretchProperty, value); }
        } 
 
        /// <summary>
        /// Gets/Sets the stretch direction of the Viewbox, which determines the restrictions on 
        /// scaling that are applied to the content inside the Viewbox.  For instance, this property
        /// can be used to prevent the content from being smaller than its native size or larger than
        /// its native size.
        /// </summary> 
        /// <seealso cref="Viewbox.StretchDirectionProperty" />
        public StretchDirection StretchDirection 
        { 
            get { return (StretchDirection)GetValue(StretchDirectionProperty); }
            set { SetValue(StretchDirectionProperty, value); } 
        }

        /// <summary>
        /// DependencyProperty for Image Source property. 
        /// </summary>
        /// <seealso cref="Image.Source" /> 
        [CommonDependencyProperty] 
        public static readonly DependencyProperty SourceProperty =
                DependencyProperty.Register( 
                        "Source",
                        typeof(ImageSource),
                        typeof(Image),
                        new FrameworkPropertyMetadata( 
                                null,
                                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, 
                                new PropertyChangedCallback(OnSourceChanged), 
                                null),
                        null); 


        /// <summary>
        /// DependencyProperty for Stretch property. 
        /// </summary>
        /// <seealso cref="Viewbox.Stretch" /> 
        [CommonDependencyProperty] 
        public static readonly DependencyProperty StretchProperty =
                Viewbox.StretchProperty.AddOwner(typeof(Image)); 

        /// <summary>
        /// DependencyProperty for StretchDirection property.
        /// </summary> 
        /// <seealso cref="Viewbox.Stretch" />
        public static readonly DependencyProperty StretchDirectionProperty = 
                Viewbox.StretchDirectionProperty.AddOwner(typeof(Image)); 

 
        /// <summary>
        /// ImageFailedEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent ImageFailedEvent = 
            EventManager.RegisterRoutedEvent(
                            "ImageFailed", 
                            RoutingStrategy.Bubble, 
                            typeof(EventHandler<ExceptionRoutedEventArgs>),
                            typeof(Image)); 

        /// <summary>
        /// Raised when there is a failure in image.
        /// </summary> 
        public event EventHandler<ExceptionRoutedEventArgs> ImageFailed
        { 
            add { AddHandler(ImageFailedEvent, value); } 
            remove { RemoveHandler(ImageFailedEvent, value); }
        } 

        #endregion

        //------------------------------------------------------------------- 
        //
        //  Protected Methods 
        // 
        //--------------------------------------------------------------------
 
        #region Protected Methods

        /// <summary>
        /// Creates AutomationPeer (<see cref="UIElement.OnCreateAutomationPeer"/>) 
        /// </summary>
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() 
        { 
            return new System.Windows.Automation.Peers.ImageAutomationPeer(this);
        } 

        /// <summary>
        /// Updates DesiredSize of the Image.  Called by parent UIElement.  This is the first pass of layout.
        /// </summary> 
        /// <remarks>
        /// Image will always return its natural size, if it fits within the constraint.  If not, it will return 
        /// as large a size as it can.  Remember that image can later arrange at any size and stretch/align. 
        /// </remarks>
        /// <param name="constraint">Constraint size is an "upper limit" that Image should not exceed.</param> 
        /// <returns>Image's desired size.</returns>
        protected override Size MeasureOverride(Size constraint)
        {
            return MeasureArrangeHelper(constraint); 
        }
 
        /// <summary> 
        /// Override for <seealso cref="FrameworkElement.ArrangeOverride" />.
        /// </summary> 
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            return MeasureArrangeHelper(arrangeSize);
        } 

        // 
        // protected override void OnArrange(Size arrangeSize) 
        // Because Image does not have children and it is inexpensive to compute it's alignment/size,
        // it does not need an OnArrange override.  It will simply use its own RenderSize (set when its 
        // Arrange is called) in OnRender.
        //

        /// <summary> 
        /// The Stretch property determines how large the Image will be drawn. The values for Stretch are:
        /// <DL><DT>None</DT>        <DD>Image draws at natural size and will clip / overdraw if too large.</DD> 
        ///     <DT>Fill</DT>        <DD>Image will draw at RenderSize.  Aspect Ratio may be distorted.</DD> 
        ///     <DT>Uniform</DT>     <DD>Image will scale uniformly up or down to fit within RenderSize.</DD>
        ///     <DT>UniformFill</DT> <DD>Image will scale uniformly to fill RenderSize, clipping / overdrawing in the larger dimension.</DD></DL> 
        /// AlignmentX and AlignmentY properties are used to position the Image when its size
        /// is different the RenderSize.
        /// </summary>
        protected override void OnRender(DrawingContext dc) 
        {
            ImageSource imageSource = Source; 
 
            if (imageSource == null)
            { 
                return;
            }

            //computed from the ArrangeOverride return size 
            dc.DrawImage(imageSource, new Rect(new Point(), RenderSize));
        } 
 
        #endregion Protected Methods
 
        #region IUriContext implementation
        /// <summary>
        ///     Accessor for the base uri of the Image
        /// </summary> 
        Uri IUriContext.BaseUri
        { 
            get 
            {
                return BaseUri; 
            }
            set
            {
                BaseUri = value; 
            }
        } 
 
        /// <summary>
        ///    Implementation for BaseUri 
        /// </summary>
        protected virtual Uri BaseUri
        {
            get 
            {
                return (Uri)GetValue(BaseUriHelper.BaseUriProperty); 
            } 
            set
            { 
                SetValue(BaseUriHelper.BaseUriProperty, value);
            }
        }
 
        #endregion IUriContext implementation
 
        //------------------------------------------------------------------- 
        //
        //  Private Methods 
        //
        //-------------------------------------------------------------------

        #region Private Methods 

        /// <summary> 
        /// Contains the code common for MeasureOverride and ArrangeOverride. 
        /// </summary>
        /// <param name="inputSize">input size is the parent-provided space that Image should use to "fit in", according to other properties.</param> 
        /// <returns>Image's desired size.</returns>
        private Size MeasureArrangeHelper(Size inputSize)
        {
            ImageSource imageSource = Source; 
            Size naturalSize = new Size();
 
            if (imageSource == null) 
            {
                return naturalSize; 
            }

            try
            { 
                UpdateBaseUri(this, imageSource);
 
                naturalSize = imageSource.Size; 
            }
            catch(Exception e) 
            {
                SetCurrentValue(SourceProperty, null);
                RaiseEvent(new ExceptionRoutedEventArgs(ImageFailedEvent, this, e));
            } 

            //get computed scale factor 
            Size scaleFactor = Viewbox.ComputeScaleFactor(inputSize, 
                                                          naturalSize,
                                                          this.Stretch, 
                                                          this.StretchDirection);

            // Returns our minimum size & sets DesiredSize.
            return new Size(naturalSize.Width * scaleFactor.Width, naturalSize.Height * scaleFactor.Height); 
        }
 
        // 
        //  This property
        //  1. Finds the correct initial size for the _effectiveValues store on the current DependencyObject 
        //  2. This is a performance optimization
        //
        internal override int EffectiveValuesInitialSize
        { 
            get { return 19; }
        } 
 
        #endregion Private Methods
 
        //-------------------------------------------------------------------
        //
        //  Private Fields
        // 
        //--------------------------------------------------------------------
        #region Private Fields 
 
        private WeakBitmapSourceEvents _weakBitmapSourceEvents;
 
        #endregion Private Fields


        //------------------------------------------------------------------- 
        //
        //  Static Constructors & Delegates 
        // 
        //--------------------------------------------------------------------
 
        #region Static Constructors & Delegates

        static Image()
        { 
            Style style = CreateDefaultStyles();
            StyleProperty.OverrideMetadata(typeof(Image), new FrameworkPropertyMetadata(style)); 
 
            //
            // The Stretch & StretchDirection properties are AddOwner'ed from a class which is not 
            // base class for Image so the metadata with flags get lost. We need to override them
            // here to make it work again.
            //
            StretchProperty.OverrideMetadata( 
                typeof(Image),
                new FrameworkPropertyMetadata( 
                    Stretch.Uniform, 
                    FrameworkPropertyMetadataOptions.AffectsMeasure
                    ) 
                );

            StretchDirectionProperty.OverrideMetadata(
                typeof(Image), 
                new FrameworkPropertyMetadata(
                    StretchDirection.Both, 
                    FrameworkPropertyMetadataOptions.AffectsMeasure 
                    )
                ); 
        }

        private static Style CreateDefaultStyles()
        { 
            Style style = new Style(typeof(Image), null);
            style.Setters.Add (new Setter(FlowDirectionProperty, FlowDirection.LeftToRight)); 
            style.Seal(); 
            return style;
        } 

        private void OnSourceDownloaded(object sender, EventArgs e)
        {
            InvalidateMeasure(); 
            InvalidateVisual(); //ensure re-rendering
        } 
 
        private void OnSourceFailed(object sender, ExceptionEventArgs e)
        { 
            SetCurrentValue(SourceProperty, null);
            RaiseEvent(new ExceptionRoutedEventArgs(ImageFailedEvent, this, e.ErrorException));
        }
 
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            if (!e.IsASubPropertyChange) 
            {
                Image image = (Image)d; 
                ImageSource oldValue = (ImageSource)e.OldValue;
                ImageSource newValue = (ImageSource)e.NewValue;

                UpdateBaseUri(d, newValue); 

                // Detach existing bitmap source events. 
                if (image._weakBitmapSourceEvents != null) 
                {
                    image._weakBitmapSourceEvents.Dispose(); 
                    image._weakBitmapSourceEvents = null;
                }

                BitmapSource newBitmapSource = newValue as BitmapSource; 
                if (newBitmapSource != null &&
                    !newBitmapSource.IsFrozen) 
                { 
                    image._weakBitmapSourceEvents = new WeakBitmapSourceEvents(image, newBitmapSource);
                } 
            }
        }

        private static void UpdateBaseUri(DependencyObject d, ImageSource source) 
        {
            if ((source is IUriContext) && (!source.IsFrozen) && (((IUriContext)source).BaseUri == null)) 
            { 
                Uri baseUri = BaseUriHelper.GetBaseUriCore(d);
                if (baseUri != null) 
                {
                    ((IUriContext)source).BaseUri = BaseUriHelper.GetBaseUriCore(d);
                }
            } 
        }
 
        #endregion 

        #region IProvidePropertyFallback 

        /// <summary>
        /// Says if the type can provide fallback value for the given property
        /// </summary> 
        bool IProvidePropertyFallback.CanProvidePropertyFallback(string property)
        { 
            if (String.CompareOrdinal(property, "Source") == 0) 
            {
                return true; 
            }

            return false;
        } 

        /// <summary> 
        /// Returns the fallback value for the given property. 
        /// </summary>
        object IProvidePropertyFallback.ProvidePropertyFallback(string property, Exception cause) 
        {
            if (String.CompareOrdinal(property, "Source") == 0)
            {
                RaiseEvent(new ExceptionRoutedEventArgs(ImageFailedEvent, this, cause)); 
            }
 
            // For now we do not have a static that represents a bad-image, so just return a null. 
            return null;
        } 

        #endregion IProvidePropertyFallback

        #region WeakBitmapSourceEvents 

        /// <summary> 
        ///     WeakBitmapSourceEvents acts as a proxy between events on BitmapSource 
        ///     and handlers on Image.  It is used to respond to events on BitmapSource
        ///     without preventing Image's to be collected. 
        /// </summary>
        private class WeakBitmapSourceEvents : WeakReference
        {
            public WeakBitmapSourceEvents(Image image, BitmapSource bitmapSource) : base(image) 
            {
                _bitmapSource = bitmapSource; 
                _bitmapSource.DownloadCompleted += this.OnSourceDownloaded; 
                _bitmapSource.DownloadFailed += this.OnSourceFailed;
                _bitmapSource.DecodeFailed += this.OnSourceFailed; 
            }

            public void OnSourceDownloaded(object sender, EventArgs e)
            { 
                Image image = this.Target as Image;
                if (null != image) 
                { 
                    image.OnSourceDownloaded(image, e);
                } 
                else
                {
                    Dispose();
                } 
            }
 
            public void OnSourceFailed(object sender, ExceptionEventArgs e) 
            {
                Image image = this.Target as Image; 
                if (null != image)
                {
                    image.OnSourceFailed(image, e);
                } 
                else
                { 
                    Dispose(); 
                }
            } 

            public void Dispose()
            {
                // We can't remove hanlders from frozen sources. 
                if (!_bitmapSource.IsFrozen)
                { 
                    _bitmapSource.DownloadCompleted -= this.OnSourceDownloaded; 
                    _bitmapSource.DownloadFailed -= this.OnSourceFailed;
                    _bitmapSource.DecodeFailed -= this.OnSourceFailed; 
                }
            }

            private BitmapSource _bitmapSource; 
        }
 
        #endregion WeakBitmapSourceEvents 
    }
} 



 
