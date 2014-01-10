//---------------------------------------------------------------------------- 
//
// <copyright file="LineSegment.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This file was generated, please do not edit it directly. 
// 
// Please see http://wiki/default.aspx/Microsoft.Projects.Avalon/MilCodeGen.html for more information.
// 
//---------------------------------------------------------------------------

using MS.Internal;
using MS.Internal.KnownBoxes; 
using MS.Internal.Collections;
using MS.Internal.PresentationCore; 
using MS.Utility; 
using System;
using System.Collections; 
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization; 
using System.Reflection;
using System.Runtime.InteropServices; 
using System.ComponentModel.Design.Serialization; 
using System.Text;
using System.Windows; 
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation; 
using System.Windows.Media.Composition;
using System.Windows.Media.Imaging; 
using System.Windows.Markup; 
using System.Windows.Media.Converters;
using System.Security; 
using System.Security.Permissions;
using SR=MS.Internal.PresentationCore.SR;
using SRID=MS.Internal.PresentationCore.SRID;
// These types are aliased to match the unamanaged names used in interop 
using BOOL = System.UInt32;
using WORD = System.UInt16; 
using Float = System.Single; 

namespace System.Windows.Media 
{


 
    sealed partial class LineSegment : PathSegment
    { 
        //----------------------------------------------------- 
        //
        //  Public Methods 
        //
        //-----------------------------------------------------

        #region Public Methods 

        /// <summary> 
        ///     Shadows inherited Clone() with a strongly typed 
        ///     version for convenience.
        /// </summary> 
        public new LineSegment Clone()
        {
            return (LineSegment)base.Clone();
        } 

        /// <summary> 
        ///     Shadows inherited CloneCurrentValue() with a strongly typed 
        ///     version for convenience.
        /// </summary> 
        public new LineSegment CloneCurrentValue()
        {
            return (LineSegment)base.CloneCurrentValue();
        } 

 
 

        #endregion Public Methods 

        //------------------------------------------------------
        //
        //  Public Properties 
        //
        //----------------------------------------------------- 
 

 

        #region Public Properties

        /// <summary> 
        ///     Point - Point.  Default value is new Point().
        /// </summary> 
        public Point Point 
        {
            get 
            {
                return (Point) GetValue(PointProperty);
            }
            set 
            {
                SetValueInternal(PointProperty, value); 
            } 
        }
 
        #endregion Public Properties

        //------------------------------------------------------
        // 
        //  Protected Methods
        // 
        //------------------------------------------------------ 

        #region Protected Methods 

        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.CreateInstanceCore">Freezable.CreateInstanceCore</see>.
        /// </summary> 
        /// <returns>The new Freezable.</returns>
        protected override Freezable CreateInstanceCore() 
        { 
            return new LineSegment();
        } 



        #endregion ProtectedMethods 

        //----------------------------------------------------- 
        // 
        //  Internal Methods
        // 
        //------------------------------------------------------

        #region Internal Methods
 

 
 

 



        #endregion Internal Methods 

        //----------------------------------------------------- 
        // 
        //  Internal Properties
        // 
        //-----------------------------------------------------

        #region Internal Properties
 
        //
        //  This property finds the correct initial size for the _effectiveValues store on the 
        //  current DependencyObject as a performance optimization 
        //
        //  This includes: 
        //    Point
        //
        internal override int EffectiveValuesInitialSize
        { 
            get
            { 
                return 1; 
            }
        } 



        #endregion Internal Properties 

        //----------------------------------------------------- 
        // 
        //  Dependency Properties
        // 
        //------------------------------------------------------

        #region Dependency Properties
 
        /// <summary>
        ///     The DependencyProperty for the LineSegment.Point property. 
        /// </summary> 
        public static readonly DependencyProperty PointProperty;
 
        #endregion Dependency Properties

        //-----------------------------------------------------
        // 
        //  Internal Fields
        // 
        //------------------------------------------------------ 

        #region Internal Fields 



 

        internal static Point s_Point = new Point(); 
 
        #endregion Internal Fields
 


        #region Constructors
 
        //------------------------------------------------------
        // 
        //  Constructors 
        //
        //----------------------------------------------------- 

        static LineSegment()
        {
            // We check our static default fields which are of type Freezable 
            // to make sure that they are not mutable, otherwise we will throw
            // if these get touched by more than one thread in the lifetime 
            // of your app.  (Windows OS Bug #947272) 
            //
 

            // Initializations
            Type typeofThis = typeof(LineSegment);
            PointProperty = 
                  RegisterProperty("Point",
                                   typeof(Point), 
                                   typeofThis, 
                                   new Point(),
                                   null, 
                                   null,
                                   /* isIndependentlyAnimated  = */ false,
                                   /* coerceValueCallback */ null);
        } 

 
 
        #endregion Constructors
 
    }
}

