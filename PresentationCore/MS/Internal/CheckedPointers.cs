//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
// Description: Checked pointers for various types 
//
// History: 
//  05/09/2005: Garyyang Created the file 
//
//--------------------------------------------------------------------------- 

using System;
using System.Security;
using MS.Internal.Shaping; 
using MS.Internal.FontCache;
 
// 
// The file contains wrapper structs for various pointer types.
// This is to allow us passing these pointers safely in layout code and provides 
// some bound checking. Only construction and probing into these pointers are security critical.
//
namespace MS.Internal
{ 
    /// <summary>
    /// Checked pointer for (Char*) 
    /// </summary> 
    internal struct CheckedCharPointer
    { 
        /// <SecurityCritical>
        /// Critical - The method takes unsafe pointer
        /// </SecurityCritical>
        [SecurityCritical] 
        internal unsafe CheckedCharPointer(char * pointer, int length)
        { 
            _checkedPointer = new CheckedPointer(pointer, length * sizeof(char)); 
        }
 
        /// <SecurityCritical>
        /// Critical - The method returns unsafe pointer
        /// </SecurityCritical>
        [SecurityCritical] 
        internal unsafe char * Probe(int offset, int length)
        { 
            return (char*) _checkedPointer.Probe(offset * sizeof(char), length * sizeof(char)); 
        }
 
        private CheckedPointer _checkedPointer;
    }

    /// <summary> 
    /// Checked pointer for (int*)
    /// </summary> 
    internal struct CheckedIntPointer 
    {
        /// <SecurityCritical> 
        /// Critical - The method takes unsafe pointer
        /// </SecurityCritical>
        [SecurityCritical]
        internal unsafe CheckedIntPointer(int * pointer, int length) 
        {
            _checkedPointer = new CheckedPointer(pointer, length * sizeof(int)); 
        } 

        /// <SecurityCritical> 
        /// Critical - The method returns unsafe pointer
        /// </SecurityCritical>
        [SecurityCritical]
        internal unsafe int * Probe(int offset, int length) 
        {
            return (int *) _checkedPointer.Probe(offset * sizeof(int), length * sizeof(int)); 
        } 

        private CheckedPointer _checkedPointer; 
    }

    /// <summary>
    /// Checked pointer for (ushort*) 
    /// </summary>
    internal struct CheckedUShortPointer 
    { 
        /// <SecurityCritical>
        /// Critical - The method takes unsafe pointer 
        /// </SecurityCritical>
        [SecurityCritical]
        internal unsafe CheckedUShortPointer(ushort * pointer, int length)
        { 
            _checkedPointer = new CheckedPointer(pointer, length * sizeof(ushort));
        } 
 
        /// <SecurityCritical>
        /// Critical - The method returns unsafe pointer 
        /// </SecurityCritical>
        [SecurityCritical]
        internal unsafe ushort * Probe(int offset, int length)
        { 
            return (ushort *) _checkedPointer.Probe(offset * sizeof(ushort), length * sizeof(ushort));
        } 
 
        private CheckedPointer _checkedPointer;
    } 
}

