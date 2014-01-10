//------------------------------------------------------------------------------ 
//  Microsoft Avalon
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.
//
//  File: InteropBitmap.cs 
//
//----------------------------------------------------------------------------- 
 

using System; 
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel; 
using System.ComponentModel.Design.Serialization;
using System.Reflection; 
using MS.Internal; 
using MS.Win32.PresentationCore;
using System.Security; 
using System.Security.Permissions;
using System.Diagnostics;
using System.Windows.Media;
using System.Globalization; 
using System.Runtime.InteropServices;
using System.Windows.Media.Animation; 
using System.Windows.Media.Composition; 
using MS.Internal.PresentationCore;                        // SecurityHelper
using SR=MS.Internal.PresentationCore.SR; 
using SRID=MS.Internal.PresentationCore.SRID;
using System.Windows.Media.Imaging;

namespace System.Windows.Interop 
{
    #region InteropBitmap 
 
    /// <summary>
    /// InteropBitmap provides caching functionality for a BitmapSource. 
    /// </summary>
    public sealed class InteropBitmap : BitmapSource
    {
        /// <summary> 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - Indirectly sets critical resources 
        /// TreatAsSafe - No inputs, does not touch any critical data with external input.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        private InteropBitmap() : base(true)
        {
            SecurityHelper.DemandUnmanagedCode(); 
        }
 
        /// <summary> 
        /// Construct a BitmapSource from an HBITMAP.
        /// </summary> 
        /// <param name="hbitmap"></param>
        /// <param name="hpalette"></param>
        /// <param name="sourceRect"></param>
        /// <param name="sizeOptions"></param> 
        /// <param name="alphaOptions"></param>
        /// <SecurityNote> 
        /// Critical - access unsafe code, accepts handle parameters 
        /// </SecurityNote>
        [SecurityCritical] 
        internal InteropBitmap(IntPtr hbitmap, IntPtr hpalette, Int32Rect sourceRect, BitmapSizeOptions sizeOptions, WICBitmapAlphaChannelOption alphaOptions)
            : base(true) // Use virtuals
        {
            _bitmapInit.BeginInit(); 

            using (FactoryMaker myFactory = new FactoryMaker()) 
            { 
                HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapFromHBITMAP(
                        myFactory.ImagingFactoryPtr, 
                        hbitmap,
                        hpalette,
                        alphaOptions,
                        out _unmanagedSource)); 
                Debug.Assert (_unmanagedSource != null && !_unmanagedSource.IsInvalid);
            } 
 
            _unmanagedSource.CalculateSize();
            _sizeOptions = sizeOptions; 
            _sourceRect = sourceRect;
            _syncObject = _unmanagedSource;

            _bitmapInit.EndInit(); 

            FinalizeCreation(); 
        } 

        /// <summary> 
        /// Construct a BitmapSource from an HICON.
        /// </summary>
        /// <param name="hicon"></param>
        /// <param name="sourceRect"></param> 
        /// <param name="sizeOptions"></param>
        /// <SecurityNote> 
        /// Critical - access unmanaged objects/resources, accepts unmanaged handle as argument 
        /// TreatAsSafe - demands unmanaged code permission
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        internal InteropBitmap(IntPtr hicon, Int32Rect sourceRect, BitmapSizeOptions sizeOptions)
            : base(true) // Use virtuals
        { 
            SecurityHelper.DemandUnmanagedCode();
 
            _bitmapInit.BeginInit(); 

            using (FactoryMaker myFactory = new FactoryMaker()) 
            {
                HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapFromHICON(
                    myFactory.ImagingFactoryPtr,
                    hicon, 
                    out _unmanagedSource));
                Debug.Assert (_unmanagedSource != null && !_unmanagedSource.IsInvalid); 
            } 

            _unmanagedSource.CalculateSize(); 
            _sourceRect = sourceRect;
            _sizeOptions = sizeOptions;
            _syncObject = _unmanagedSource;
 
            _bitmapInit.EndInit();
 
            FinalizeCreation(); 
        }
 
        /// <summary>
        /// Construct a BitmapSource from a memory section.
        /// </summary>
        /// <param name="section"></param> 
        /// <param name="pixelWidth"></param>
        /// <param name="pixelHeight"></param> 
        /// <param name="format"></param> 
        /// <param name="stride"></param>
        /// <param name="offset"></param> 
        /// <SecurityNote>
        /// Critical - access unmanaged objects/resources, accepts unmanaged handle as argument
        /// TreatAsSafe - demands unmanaged code permission
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        internal InteropBitmap( 
            IntPtr section, 
            int pixelWidth,
            int pixelHeight, 
            PixelFormat format,
            int stride,
            int offset)
            : base(true) // Use virtuals 
        {
            SecurityHelper.DemandUnmanagedCode(); 
 
            _bitmapInit.BeginInit();
            if (pixelWidth <= 0) 
            {
                throw new ArgumentOutOfRangeException("pixelWidth", SR.Get(SRID.ParameterMustBeGreaterThanZero));
            }
 
            if (pixelHeight <= 0)
            { 
                throw new ArgumentOutOfRangeException("pixelHeight", SR.Get(SRID.ParameterMustBeGreaterThanZero)); 
            }
 
            Guid formatGuid = format.Guid;

            HRESULT.Check(UnsafeNativeMethods.WindowsCodecApi.CreateBitmapFromSection(
                    (uint)pixelWidth, 
                    (uint)pixelHeight,
                    ref formatGuid, 
                    section, 
                    (uint)stride,
                    (uint)offset, 
                    out _unmanagedSource
                    ));
            Debug.Assert (_unmanagedSource != null && !_unmanagedSource.IsInvalid);
 
            _unmanagedSource.CalculateSize();
            _sourceRect = Int32Rect.Empty; 
            _sizeOptions = null; 
            _syncObject = _unmanagedSource;
            _bitmapInit.EndInit(); 

            FinalizeCreation();
        }
 
        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.CreateInstanceCore">Freezable.CreateInstanceCore</see>. 
        /// </summary> 
        /// <returns>The new Freezable.</returns>
        /// <SecurityNote> 
        /// Critical - accesses critical code.
        /// TreatAsSafe - method only produces clone of original image.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        protected override Freezable CreateInstanceCore()
        { 
            return new InteropBitmap(); 
        }
 
        /// <summary>
        /// Common Copy method used to implement CloneCore() and CloneCurrentValueCore(),
        /// GetAsFrozenCore(), and GetCurrentValueAsFrozenCore().
        /// </summary> 
        /// <SecurityNote>
        /// Critical - calls unmanaged objects 
        /// </SecurityNote> 
        [SecurityCritical]
        private void CopyCommon(InteropBitmap sourceBitmapSource) 
        {
            // Avoid Animatable requesting resource updates for invalidations that occur during construction
            Animatable_IsResourceInvalidationNecessary = false;
            _unmanagedSource = sourceBitmapSource._unmanagedSource; 
            _sourceRect = sourceBitmapSource._sourceRect;
            _sizeOptions = sourceBitmapSource._sizeOptions; 
            InitFromWICSource(sourceBitmapSource.WicSourceHandle); 

            // The next invalidation will cause Animatable to register an UpdateResource callback 
            Animatable_IsResourceInvalidationNecessary = true;
        }

        /// <summary> 
        /// Implementation of <see cref="System.Windows.Freezable.CloneCore(Freezable)">Freezable.CloneCore</see>.
        /// </summary> 
        /// <SecurityNote> 
        /// Critical - accesses critical code.
        /// TreatAsSafe - method only produces clone of original image. 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        protected override void CloneCore(Freezable sourceFreezable)
        { 
            InteropBitmap sourceBitmapSource = (InteropBitmap)sourceFreezable;
 
            base.CloneCore(sourceFreezable); 

            CopyCommon(sourceBitmapSource); 
        }

        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.CloneCurrentValueCore(Freezable)">Freezable.CloneCurrentValueCore</see>. 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - accesses critical code. 
        /// TreatAsSafe - method only produces clone of original image.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        protected override void CloneCurrentValueCore(Freezable sourceFreezable)
        {
            InteropBitmap sourceBitmapSource = (InteropBitmap)sourceFreezable; 

            base.CloneCurrentValueCore(sourceFreezable); 
 
            CopyCommon(sourceBitmapSource);
        } 


        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.GetAsFrozenCore(Freezable)">Freezable.GetAsFrozenCore</see>. 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - accesses critical code. 
        /// TreatAsSafe - method only produces GetAsFrozen of original image.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        protected override void GetAsFrozenCore(Freezable sourceFreezable)
        {
            InteropBitmap sourceBitmapSource = (InteropBitmap)sourceFreezable; 

            base.GetAsFrozenCore(sourceFreezable); 
 
            CopyCommon(sourceBitmapSource);
        } 


        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.GetCurrentValueAsFrozenCore(Freezable)">Freezable.GetCurrentValueAsFrozenCore</see>. 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - accesses critical code. 
        /// TreatAsSafe - method only produces GetCurrentValueAsFrozen of original image.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        protected override void GetCurrentValueAsFrozenCore(Freezable sourceFreezable)
        {
            InteropBitmap sourceBitmapSource = (InteropBitmap)sourceFreezable; 

            base.GetCurrentValueAsFrozenCore(sourceFreezable); 
 
            CopyCommon(sourceBitmapSource);
        } 

        ///
        /// Create from WICBitmapSource
        /// 
        /// <SecurityNote>
        /// Critical - calls unmanaged objects 
        /// </SecurityNote> 
        [SecurityCritical]
        private void InitFromWICSource( 
                    SafeMILHandle wicSource
                    )
        {
            _bitmapInit.BeginInit(); 

            BitmapSourceSafeMILHandle bitmapSource = null; 
 
            lock (_syncObject)
            { 
                using (FactoryMaker factoryMaker = new FactoryMaker())
                {
                    HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapFromSource(
                            factoryMaker.ImagingFactoryPtr, 
                            wicSource,
                            WICBitmapCreateCacheOptions.WICBitmapCacheOnLoad, 
                            out bitmapSource)); 
                }
 
                bitmapSource.CalculateSize();
            }

            WicSourceHandle = bitmapSource; 
            _isSourceCached = true;
            _bitmapInit.EndInit(); 
 
            UpdateCachedSettings();
        } 

        /// <summary>
        /// Invalidates the bitmap source.
        /// </summary> 
        /// <SecurityNote>
        /// Critical - calls critical code, access unmanaged resources 
        /// PublicOK - demands unmanaged code permission 
        /// </SecurityNote>
        [SecurityCritical] 
        public void Invalidate()
        {
            SecurityHelper.DemandUnmanagedCode();
 
            WritePreamble();
 
            if (_unmanagedSource != null) 
            {
                // For supported DUCE formats 
                // RegisterForAsyncUpdateResource below will get us to draw the bitmap again but
                // BitmapSource will send the same pointer down resulting in no visual change. Sending
                // the invalidate packet will dirty the bitmap and ensure we copy the bits again.
                unsafe 
                {
                    for (int i = 0, numChannels = _duceResource.GetChannelCount(); i < numChannels; ++i) 
                    { 
                        DUCE.Channel channel = _duceResource.GetChannel(i);
 
                        DUCE.MILCMD_BITMAP_INVALIDATE data;
                        data.Type = MILCMD.MilCmdBitmapInvalidate;
                        data.Handle = _duceResource.GetHandle(channel);
 
                        channel.SendCommand((byte*)&data, sizeof(DUCE.MILCMD_BITMAP_INVALIDATE));
                    } 
                } 

                // For unsupported DUCE formats 
                // _needsUpdate will cause BitmapSource to throw out the old DUCECompatiblePtr. This means we
                // will create a new format converted bitmap and send that down. Since the render thread sees
                // a brand new bitmap, it will copy the bits out. The old bitmap was CacheOnLoad, and will not
                // reflect changes made to this InteropBitmapSource. 
                _needsUpdate = true;
 
                RegisterForAsyncUpdateResource(); 
            }
 
            WritePostscript();
        }

        // ISupportInitialize 

        /// 
        /// Create the unmanaged resources 
        ///
        /// <SecurityNote> 
        /// Critical - access unmanaged objects/resources
        /// </SecurityNote>
        [SecurityCritical]
        internal override void FinalizeCreation() 
        {
            BitmapSourceSafeMILHandle wicClipper = null; 
            BitmapSourceSafeMILHandle wicTransformer = null; 
            BitmapSourceSafeMILHandle transformedSource = _unmanagedSource;
 
            HRESULT.Check(UnsafeNativeMethods.WICBitmap.SetResolution(_unmanagedSource, 96, 96));

            using (FactoryMaker factoryMaker = new FactoryMaker())
            { 
                IntPtr wicFactory = factoryMaker.ImagingFactoryPtr;
 
                if (!_sourceRect.IsEmpty) 
                {
                    HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapClipper( 
                            wicFactory,
                            out wicClipper));

                    lock (_syncObject) 
                    {
                        HRESULT.Check(UnsafeNativeMethods.WICBitmapClipper.Initialize( 
                                wicClipper, 
                                transformedSource,
                                ref _sourceRect)); 
                    }

                    transformedSource = wicClipper;
                } 

                if (_sizeOptions != null) 
                { 
                    if (_sizeOptions.DoesScale)
                    { 
                        Debug.Assert(_sizeOptions.Rotation == Rotation.Rotate0);
                        uint width, height;

                        _sizeOptions.GetScaledWidthAndHeight( 
                            (uint)_sizeOptions.PixelWidth,
                            (uint)_sizeOptions.PixelHeight, 
                            out width, 
                            out height);
 
                        HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapScaler(
                                wicFactory,
                                out wicTransformer));
 
                        lock (_syncObject)
                        { 
                            HRESULT.Check(UnsafeNativeMethods.WICBitmapScaler.Initialize( 
                                    wicTransformer,
                                    transformedSource, 
                                    width,
                                    height,
                                    WICInterpolationMode.Fant));
                        } 
                    }
                    else if (_sizeOptions.Rotation != Rotation.Rotate0) 
                    { 
                        HRESULT.Check(UnsafeNativeMethods.WICImagingFactory.CreateBitmapFlipRotator(
                                wicFactory, 
                                out wicTransformer));

                        lock (_syncObject)
                        { 
                            HRESULT.Check(UnsafeNativeMethods.WICBitmapFlipRotator.Initialize(
                                    wicTransformer, 
                                    transformedSource, 
                                    _sizeOptions.WICTransformOptions));
                        } 
                    }

                    if (wicTransformer != null)
                    { 
                        transformedSource = wicTransformer;
                    } 
                } 

                WicSourceHandle = transformedSource; 

                // Since the original source is an HICON, HBITMAP or section, we know it's cached.
                // FlipRotate and Clipper do not affect our cache performance.
                _isSourceCached = true; 
            }
 
            CreationCompleted = true; 
            UpdateCachedSettings();
        } 

        /// <SecurityNote>
        /// Critical - unmanaged resource - not safe to hand out
        /// </SecurityNote> 
        [SecurityCritical]
        private BitmapSourceSafeMILHandle /* IWICBitmapSource */ _unmanagedSource = null; 
 
        private Int32Rect _sourceRect = Int32Rect.Empty;
        private BitmapSizeOptions _sizeOptions = null; 

    }

    #endregion // InteropBitmap 
}

