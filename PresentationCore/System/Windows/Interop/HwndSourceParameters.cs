//------------------------------------------------------------------------------ 
//  Microsoft Avalon
//  Copyright (c) Microsoft Corporation, 2004
//
//  File: HwndSourceParameter.cs 
//-----------------------------------------------------------------------------
 
using MS.Win32; 
using System.Windows.Media;
using System.Windows.Input; 

namespace System.Windows.Interop
{
    /// <summary> 
    ///     Base class for HwndSource Creation Parameters.
    ///     This allows flexibility and control of parameters to HwndSource's 
    ///     Constructor without many different overloaded constructors. 
    /// </summary>
    public struct HwndSourceParameters 
    {
        /// <summary>
        ///     Simple Ctor w/ just a WindowName
        /// </summary> 
        public HwndSourceParameters(string name): this()
        { 
            // Initialize some fields to useful default values 
            _styleBits  = NativeMethods.WS_VISIBLE;
            _styleBits |= NativeMethods.WS_CAPTION; 
            _styleBits |= NativeMethods.WS_SYSMENU;
            _styleBits |= NativeMethods.WS_THICKFRAME;
            _styleBits |= NativeMethods.WS_MINIMIZEBOX;
            _styleBits |= NativeMethods.WS_MAXIMIZEBOX; 
            _styleBits |= NativeMethods.WS_CLIPCHILDREN;
 
            // The Visual Manager has a hard time creating 
            // a surface with zero pixels.
            _width  = 1; 
            _height = 1;

            _x = NativeMethods.CW_USEDEFAULT;
            _y = NativeMethods.CW_USEDEFAULT; 

            WindowName = name; 
        } 

        /// <summary> 
        ///     Ctor.  w/ WindowName and Size.
        /// </summary>
        /// <param name="name">  Name of the window </param>
        /// <param name="width">  Width of the window </param> 
        /// <param name="height">  Height of the window </param>
        public HwndSourceParameters(string name, int width, int height): this(name) 
        { 
            Width = width;
            Height = height; 
        }

        /// <summary>
        ///     Returns the hashcode for this struct. 
        /// </summary>
        /// <returns>hashcode</returns> 
        public override int GetHashCode( ) 
        {
            return base.GetHashCode(); 
        }

        /// <summary>
        ///  The Window Class Style Property 
        /// </summary>
        public int WindowClassStyle 
        { 
            get{ return _classStyleBits; }
            set{ _classStyleBits = value; } 
        }

        /// <summary>
        /// Allow the app to set the Style bits. 
        /// The Clip Children bit must always be set on a Standard Window.
        /// </summary> 
        public int WindowStyle 
        {
            get{ 
                return _styleBits;
            }

            set{ 
                _styleBits = value | NativeMethods.WS_CLIPCHILDREN;
            } 
        } 

        /// <summary> 
        /// The Extended Style bits.
        /// </summary>
        public int ExtendedWindowStyle
        { 
            get{ return _extendedStyleBits; }
            set{ _extendedStyleBits = value; } 
        } 

        /// <summary> 
        ///     Set the X,Y Position of HwndSource Creation Parameters.
        /// </summary>
        public void SetPosition(int x, int y)
        { 
            _x = x;
            _y = y; 
        } 

        /// <summary> 
        ///     The X position of the HwndSource Property.
        /// </summary>
        public int PositionX
        { 
            get{ return _x; }
            set{ _x = value; } 
        } 

        /// <summary> 
        ///     The Y position of the HwndSource Property.
        /// </summary>
        public int PositionY
        { 
            get{ return _y; }
            set{ _y = value; } 
        } 

        /// <summary> 
        ///     Set the Width and Height of HwndSource Creation Parameters.
        /// </summary>
        public void SetSize(int width, int height)
        { 
            _width = width;
            _height = height; 
            _hasAssignedSize = true; 
        }
 
        /// <summary>
        ///     The Width Property of the HwndSource.
        /// </summary>
        public int Width 
        {
            get{ return _width; } 
            set{ 
                _width = value;
                _hasAssignedSize = true; 
            }
        }

        /// <summary> 
        ///     The Height Property of the HwndSource.
        /// </summary> 
        public int Height 
        {
            get{ return _height; } 
            set{
                _height = value;
                _hasAssignedSize = true;
            } 
        }
 
        /// <summary> 
        ///     Was the Size assigned or did we just default.
        /// </summary> 
        public bool HasAssignedSize
        {
            get { return _hasAssignedSize; }
        } 

        /// <summary> 
        ///     The Window Name Property. 
        /// </summary>
        public string WindowName 
        {
            get{ return _name; }
            set{ _name = value; }
        } 

        /// <summary> 
        ///     The ParentWindow Property. 
        /// </summary>
        public IntPtr ParentWindow 
        {
            get{ return _parent; }
            set{ _parent = value; }
        } 

        /// <summary> 
        ///     The HwndSourceHook Property.  This allows a message hook to 
        ///     process window messages to the window.  A Hook provided in the
        ///     HwndSourceParameters will be installed before the call to 
        ///     CreateWindow and this hook will see the window creation msgs.
        /// </summary>
        public HwndSourceHook HwndSourceHook
        { 
            get{ return _hwndSourceHook; }
            set{ this._hwndSourceHook = value; } 
        } 

        /// <summary> 
        ///     The AdjustSizingForNonClientArea Property.
        /// </summary>
        public bool AdjustSizingForNonClientArea
        { 
            get { return _adjustSizingForNonClientArea; }
            set { _adjustSizingForNonClientArea = value; } 
        } 

        // /// <summary> 
        // ///     Specifies the color to display as transparent.
        // /// </summary>
        // /// <remarks>
        // ///     Use null to indicate that no color should be transparent. 
        // /// </remarks>
        // public Nullable<Color> ColorKey 
        // { 
        //     get {return _colorKey;}
        //     set {_colorKey = value;} 
        // }

        // /// <summary>
        // ///     Specifies the constant opacity to apply to the window. 
        // /// </summary>
        // /// <remarks> 
        // ///     The valid values range from [0..1].  Values outside of this range are clamped. 
        // /// </remarks>
        // public double Opacity 
        // {
        //     get {return _opacitySpecified ? _opacity : 1.0;}
        //     set
        //     { 
        //         if(value < 0) value = 0;
        //         if(value > 1) value = 1; 
        //         _opacitySpecified = true; 
        //         _opacity = value;
        //     } 
        // }

        /// <summary>
        ///     Specifies whether or not the per-pixel opacity of the window content 
        ///     is respected.
        /// </summary> 
        /// <remarks> 
        ///     By enabling per-pixel opacity, the system will no longer draw the non-client area.
        /// </remarks> 
        public bool UsesPerPixelOpacity
        {
            get {return _usesPerPixelOpacity;}
            set {_usesPerPixelOpacity = value;} 
        }
 
        /// <summary> 
        ///     The RestoreFocusMode for the window.
        /// </summary> 
        public RestoreFocusMode RestoreFocusMode
        {
            get { return _restoreFocusMode ?? Keyboard.DefaultRestoreFocusMode; }
            set { _restoreFocusMode = value; } 
        }
 
        /// <summary> 
        ///     The AcquireHwndFocusInMenuMode setting for the window.
        /// </summary> 
        public bool AcquireHwndFocusInMenuMode
        {
            get { return _acquireHwndFocusInMenuMode ?? HwndSource.DefaultAcquireHwndFocusInMenuMode; }
            set { _acquireHwndFocusInMenuMode = value; } 
        }
 
        /// <summary> 
        /// Whether an HwndSource should be given messages straight off the message loop to preprocess,
        /// like top-level ones do normally. 
        /// </summary>
        /// <remarks> Used for RootBrowserWindow. </remarks>
        internal bool TreatAsInputRoot
        { 
            get { return _treatAsInputRoot ?? ((uint)_styleBits & NativeMethods.WS_CHILD) == 0; }
            set { _treatAsInputRoot = value; } 
        } 

        /// <summary> 
        /// == operator
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param> 
        /// <returns></returns>
        public static bool operator==(HwndSourceParameters a, HwndSourceParameters b) 
        { 
            return a.Equals(b);
        } 

        /// <summary>
        /// != operator
        /// </summary> 
        /// <param name="a"></param>
        /// <param name="b"></param> 
        /// <returns></returns> 
        public static bool operator!=(HwndSourceParameters a, HwndSourceParameters b)
        { 
            return !a.Equals(b);
        }

        /// <summary> 
        /// Compare two HwndSourceParameters blocks.
        /// </summary> 
        /// <param name="obj"></param> 
        /// <returns></returns>
        public override bool Equals(object obj) 
        {
            if (obj == null)
            {
                return false; 
            }
 
            return Equals( (HwndSourceParameters)obj ); 
        }
 
        /// <summary>
        /// Compare two HwndSourceParameters blocks.
        /// </summary>
        /// <param name="obj"></param> 
        /// <returns></returns>
        public bool Equals(HwndSourceParameters obj) 
        { 
            return ((this._classStyleBits    == obj._classStyleBits)
                 && (this._styleBits         == obj._styleBits) 
                 && (this._extendedStyleBits == obj._extendedStyleBits)
                 && (this._x == obj._x)
                 && (this._y == obj._y)
                 && (this._width  == obj._width) 
                 && (this._height == obj._height)
                 && (this._name   == obj._name) 
                 && (this._parent == obj._parent) 
                 && (this._hwndSourceHook  == obj._hwndSourceHook)
                 && (this._adjustSizingForNonClientArea == obj._adjustSizingForNonClientArea) 
                 && (this._hasAssignedSize == obj._hasAssignedSize)
                 // && (this._colorKey == obj._colorKey)
                 // && (this._opacity == obj._opacity)
                 // && (this._opacitySpecified == obj._opacitySpecified) 
                 && (this._usesPerPixelOpacity == obj._usesPerPixelOpacity)
                  ); 
        } 

        private int _classStyleBits; 
        private int _styleBits;
        private int _extendedStyleBits;
        private int _x;
        private int _y; 
        private int _width;
        private int _height; 
        private string _name; 
        private IntPtr _parent;
        private HwndSourceHook _hwndSourceHook; 

        private bool _adjustSizingForNonClientArea;
        private bool _hasAssignedSize;
        // private Nullable<Color> _colorKey; 
        // private double _opacity;
        // private bool _opacitySpecified; // default value for opacity needs to be 1.0 
        private bool _usesPerPixelOpacity; 
        private bool? _treatAsInputRoot;
        private RestoreFocusMode? _restoreFocusMode; 
        private bool? _acquireHwndFocusInMenuMode;

    }
} 

