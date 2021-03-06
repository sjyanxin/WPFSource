//---------------------------------------------------------------------------- 
//
// <copyright file="CapacityStreamGeometryContext.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This class is used by the StreamGeometry class to generate an inlined, 
// flattened geometry stream. 
//
//--------------------------------------------------------------------------- 

using MS.Internal;
using MS.Internal.PresentationCore;
using System; 
using System.Collections;
using System.Collections.Generic; 
using System.Runtime.InteropServices; 
using System.Windows.Threading;
using System.Windows; 
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Composition;
using System.Windows.Media.Effects; 
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D; 
using System.Diagnostics; 
using SR=MS.Internal.PresentationCore.SR;
using SRID=MS.Internal.PresentationCore.SRID; 
using System.Security;
using System.Security.Permissions;

namespace System.Windows.Media 
{
    /// <summary> 
    ///     CapacityStreamGeometryContext 
    /// </summary>
    internal abstract class CapacityStreamGeometryContext : StreamGeometryContext 
    {
        internal virtual void SetFigureCount(int figureCount) {}
        internal virtual void SetSegmentCount(int segmentCount) {}
    } 
}

