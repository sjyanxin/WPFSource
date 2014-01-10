//------------------------------------------------------------------------------ 
//  Microsoft Avalon
//  Copyright (c) Microsoft Corporation, All Rights Reserved
//
//  File: BitmapMetadataBlob.cs 
//
//----------------------------------------------------------------------------- 
 
using System;
using System.Collections; 
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization; 
using System.Reflection;
using MS.Internal; 
using MS.Win32.PresentationCore; 
using System.Diagnostics;
using System.Globalization; 
using System.Runtime.InteropServices;
using System.Xml;
using System.IO;
using System.Security; 
using System.Security.Permissions;
using System.Windows.Media.Imaging; 
using System.Windows.Threading; 
using System.Text;
using MS.Internal.PresentationCore;                        // SecurityHelper 

namespace System.Windows.Media.Imaging
{
    #region BitmapMetadataBlob 

    /// <summary> 
    /// BitmapMetadataBlob class 
    /// </summary>
    public class BitmapMetadataBlob 
    {
        /// <summary>
        ///
        /// </summary> 
        public BitmapMetadataBlob(byte[] blob)
        { 
            _blob = blob; 
        }
 
        /// <summary>
        ///
        /// </summary>
        public byte[] GetBlobValue() 
        {
            return (byte[]) _blob.Clone(); 
        } 

        /// <summary> 
        ///
        /// </summary>
        internal byte[] InternalGetBlobValue()
        { 
            return  _blob;
        } 
 
        private byte[] _blob;
    } 

    #endregion
}

