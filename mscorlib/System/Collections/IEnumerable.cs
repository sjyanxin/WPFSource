// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Interface:  IEnumerable 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: Interface for classes providing IEnumerators
** 
**
===========================================================*/ 
namespace System.Collections { 
    using System;
    using System.Diagnostics.Contracts; 
    using System.Runtime.InteropServices;

    // Implement this interface if you need to support VB's foreach semantics.
    // Also, COM classes that support an enumerator will also implement this interface. 
    [ContractClass(typeof(IEnumerableContract))]
    [Guid("496B0ABE-CDEE-11d3-88E8-00902754C43A")] 
    [System.Runtime.InteropServices.ComVisible(true)] 
    public interface IEnumerable
    { 
        // Interfaces are not serializable
        // Returns an IEnumerator for this enumerable Object.  The enumerator provides
        // a simple way to access all the contents of a collection.
        [Pure] 
        [DispId(-4)]
        IEnumerator GetEnumerator(); 
    } 

    [ContractClassFor(typeof(IEnumerable))] 
    internal class IEnumerableContract : IEnumerable
    {
        [Pure]
        IEnumerator IEnumerable.GetEnumerator() 
        {
            Contract.Ensures(Contract.Result<IEnumerator>() != null); 
            return default(IEnumerator); 
        }
    } 
}

