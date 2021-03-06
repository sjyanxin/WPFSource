//------------------------------------------------------------------------------ 
//  Microsoft Avalon
//  Copyright (c) Microsoft Corporation, All Rights Reserved
//
//  File: BitmapCodecInfoInternal.cs 
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
using MS.Win32; 
using System.Diagnostics;
using System.Globalization; 
using System.Runtime.InteropServices;
using System.Xml;
using System.IO;
using System.Windows.Media.Imaging; 
using System.Text;
 
namespace System.Windows.Media.Imaging 
{
    #region BitmapCodecInfoInternal 

    /// <summary>
    /// Codec info for a given Encoder/Decoder
    /// </summary> 
    internal class BitmapCodecInfoInternal : BitmapCodecInfo
    { 
        #region Constructors 

        /// <summary> 
        /// Constructor
        /// </summary>
        private BitmapCodecInfoInternal()
        { 
        }
 
        /// <summary> 
        /// Internal Constructor
        /// </summary> 
        internal BitmapCodecInfoInternal(SafeMILHandle codecInfoHandle) :
            base(codecInfoHandle)
        {
        } 

        #endregion 
 
    }
 
    #endregion
}

