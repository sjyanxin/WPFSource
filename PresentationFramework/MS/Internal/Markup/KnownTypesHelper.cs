//---------------------------------------------------------------------------- 
//
// <copyright file="KnownTypesHelper.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
//--------------------------------------------------------------------------- 
 
using System;
using System.IO; 
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
 
// These are the non-generated parts of the KnownTypes and TypeIndexer classes
 
#if PBTCOMPILER 
namespace MS.Internal.Markup
#else 
namespace System.Windows.Markup
#endif
{
    internal static partial class KnownTypes 
    {
        //  Keep Known WCP Types in a private array, accessed through an indexer 
        private static TypeIndexer _typeIndexer = new TypeIndexer((int)KnownElements.MaxElement); 
        internal static TypeIndexer Types
        { 
            get
            {
                return _typeIndexer;
            } 
        }
 
#if PBTCOMPILER 
        internal static void InitializeKnownTypes(Assembly asmFramework, Assembly asmCore, Assembly asmBase)
        { 
            _typeIndexer.Initialize(asmFramework, asmCore, asmBase);
        }
#endif
    } 

 
 
    internal partial class TypeIndexer
    { 
        public TypeIndexer(int size)
        {
            _typeTable =new Type[size];
        } 

        public System.Type this[int index] 
        { 
            get
            { 
                Type t = _typeTable[index];
                if (t == null)
                {
                    t = InitializeOneType((KnownElements)index); 
                }
                _typeTable[index] = t; 
                return t; 
            }
        } 

        private Type[] _typeTable;
    }
} 

