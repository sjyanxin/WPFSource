// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
namespace System {
 
    using System; 
    using System.Diagnostics.Contracts;
 
    [System.Runtime.InteropServices.ComVisible(true)]
    [ContractClass(typeof(IFormattableContract))]
    public interface IFormattable
    { 
        [Pure]
        String ToString(String format, IFormatProvider formatProvider); 
    } 

    [ContractClassFor(typeof(IFormattable))] 
    internal abstract class IFormattableContract : IFormattable
    {
       String IFormattable.ToString(String format, IFormatProvider formatProvider)
       { 
           Contract.Ensures(Contract.Result<String>() != null);
 	       throw new NotImplementedException(); 
       } 
    }
} 

