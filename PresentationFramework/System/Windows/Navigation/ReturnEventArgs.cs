//---------------------------------------------------------------------------- 
//
// <copyright file=ReturnEventArgs.cs company=Microsoft>
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//              ReturnEventArgs is a generic flavor of EventArgs, 
//              that enable setting, getting a typed value that's
//              passed between Pagefunctions. 
//
// History:
//  05/09/03:      marka     Created.
//  07/08/04:      weibz     Rename and make it compliant with guideline. 
//
//--------------------------------------------------------------------------- 
 
using System;
namespace System.Windows.Navigation 
{
    ///<summary>
    ///     ReturnEventArgs is a generic flavor of EventArgs,
    ///     that enable setting, getting a typed value that's 
    ///     passed between Pagefunctions.
    ///</summary> 
    public class ReturnEventArgs<T> : System.EventArgs 
    {
 
        //-----------------------------------------------------
        //
        //  Constructors
        // 
        //-----------------------------------------------------
 
        #region Constructors 

        ///<summary> 
        /// ReturnEventArgs constructor
        ///</summary>
        public ReturnEventArgs()
        { 

        } 
        ///<summary> 
        /// ReturnEventArgs constructor. Supplied value is assigned to Result value
        ///</summary> 
        ///<param name="result">Assigned to Result property</param>
        public ReturnEventArgs( T result)
        {
            _result = result; 
        }
 
        #endregion Constructors 

        //------------------------------------------------------ 
        //
        //  Public Properties
        //
        //----------------------------------------------------- 

        #region Public Properties 
 
        ///<summary>
        ///     The Result property indicates the result that a PageFunction is 
        ///     returning to it's caller, at the end of a Structured Navigation.
        ///</summary>
        public T Result
        { 
            get
            { 
                return _result; 
            }
            set 
            {
                _result = value;
            }
        } 

        #endregion Public Properties 
 
        //------------------------------------------------------
        // 
        //  Private Fields
        //
        //------------------------------------------------------
 
        #region Private Fields
 
        private T _result; 

        #endregion Private Fields 


    }
 
}

