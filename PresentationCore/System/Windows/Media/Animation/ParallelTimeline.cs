//---------------------------------------------------------------------------- 
//
// <copyright file="ParallelTimeline.cs" company="Microsoft">
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
 
namespace System.Windows.Media.Animation
{ 



    partial class ParallelTimeline : TimelineGroup 
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
        public new ParallelTimeline Clone() 
        {
            return (ParallelTimeline)base.Clone();
        }
 
        /// <summary>
        ///     Shadows inherited CloneCurrentValue() with a strongly typed 
        ///     version for convenience. 
        /// </summary>
        public new ParallelTimeline CloneCurrentValue() 
        {
            return (ParallelTimeline)base.CloneCurrentValue();
        }
 

 
 
        #endregion Public Methods
 
        //------------------------------------------------------
        //
        //  Public Properties
        // 
        //-----------------------------------------------------
 
 

 
        #region Public Properties


 
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
            return new ParallelTimeline();
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

 



        #endregion Internal Properties 

        //----------------------------------------------------- 
        // 
        //  Dependency Properties
        // 
        //------------------------------------------------------

        #region Dependency Properties
 

 
        #endregion Dependency Properties 

        //----------------------------------------------------- 
        //
        //  Internal Fields
        //
        //------------------------------------------------------ 

        #region Internal Fields 
 

 



 
        #endregion Internal Fields
 
 

        #region Constructors 

        //------------------------------------------------------
        //
        //  Constructors 
        //
        //----------------------------------------------------- 
 

 

        #endregion Constructors

    } 
}

