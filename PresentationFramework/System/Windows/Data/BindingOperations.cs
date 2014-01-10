//---------------------------------------------------------------------------- 
//
// <copyright file="BindingOperations.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Helper operations for data bindings. 
// 
// See spec at http://avalon/connecteddata/Specs/Data%20Binding.mht
// 
//---------------------------------------------------------------------------


using System; 
using System.Collections;
using System.Diagnostics; 
using System.ComponentModel; 
using System.Globalization;
using System.Threading; 
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading; 

using MS.Internal.Data; 
 
namespace System.Windows.Data
{ 
    /// <summary>
    /// Operations to manipulate data bindings.
    /// </summary>
    public static class BindingOperations 
    {
        //----------------------------------------------------- 
        // 
        //  Public Methods
        // 
        //-----------------------------------------------------

        /// <summary>
        /// Attach a BindingExpression to a property. 
        /// </summary>
        /// <remarks> 
        /// A new BindingExpression is created from the given description, and attached to 
        /// the given property of the given object.  This method is the way to
        /// attach a Binding to an arbitrary DependencyObject that may not expose 
        /// its own SetBinding method.
        /// </remarks>
        /// <param name="target">object on which to attach the Binding</param>
        /// <param name="dp">property to which to attach the Binding</param> 
        /// <param name="binding">description of the Binding</param>
        /// <exception cref="ArgumentNullException"> target and dp and binding cannot be null </exception> 
        public static BindingExpressionBase SetBinding(DependencyObject target, DependencyProperty dp, BindingBase binding) 
        {
            if (target == null) 
                throw new ArgumentNullException("target");
            if (dp == null)
                throw new ArgumentNullException("dp");
            if (binding == null) 
                throw new ArgumentNullException("binding");
//            target.VerifyAccess(); 
 
            BindingExpressionBase bindExpr = binding.CreateBindingExpression(target, dp);
 
            //

            target.SetValue(dp, bindExpr);
 
            return bindExpr;
        } 
 

        /// <summary> 
        /// Retrieve a BindingBase.
        /// </summary>
        /// <remarks>
        /// This method returns null if no Binding has been set on the given 
        /// property.
        /// </remarks> 
        /// <param name="target">object from which to retrieve the binding</param> 
        /// <param name="dp">property from which to retrieve the binding</param>
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception> 
        public static BindingBase GetBindingBase(DependencyObject target, DependencyProperty dp)
        {
            BindingExpressionBase b = GetBindingExpressionBase(target, dp);
            return (b != null) ? b.ParentBindingBase : null; 
        }
 
        /// <summary> 
        /// Retrieve a Binding.
        /// </summary> 
        /// <remarks>
        /// This method returns null if no Binding has been set on the given
        /// property.
        /// </remarks> 
        /// <param name="target">object from which to retrieve the binding</param>
        /// <param name="dp">property from which to retrieve the binding</param> 
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception> 
        public static Binding GetBinding(DependencyObject target, DependencyProperty dp)
        { 
            return GetBindingBase(target, dp) as Binding;
        }

        /// <summary> 
        /// Retrieve a PriorityBinding.
        /// </summary> 
        /// <remarks> 
        /// This method returns null if no Binding has been set on the given
        /// property. 
        /// </remarks>
        /// <param name="target">object from which to retrieve the binding</param>
        /// <param name="dp">property from which to retrieve the binding</param>
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception> 
        public static PriorityBinding GetPriorityBinding(DependencyObject target, DependencyProperty dp)
        { 
            return GetBindingBase(target, dp) as PriorityBinding; 
        }
 
        /// <summary>
        /// Retrieve a MultiBinding.
        /// </summary>
        /// <remarks> 
        /// This method returns null if no Binding has been set on the given
        /// property. 
        /// </remarks> 
        /// <param name="target">object from which to retrieve the binding</param>
        /// <param name="dp">property from which to retrieve the binding</param> 
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception>
        public static MultiBinding GetMultiBinding(DependencyObject target, DependencyProperty dp)
        {
            return GetBindingBase(target, dp) as MultiBinding; 
        }
 
        /// <summary> 
        /// Retrieve a BindingExpressionBase.
        /// </summary> 
        /// <remarks>
        /// This method returns null if no Binding has been set on the given
        /// property.
        /// </remarks> 
        /// <param name="target">object from which to retrieve the BindingExpression</param>
        /// <param name="dp">property from which to retrieve the BindingExpression</param> 
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception> 
        public static BindingExpressionBase GetBindingExpressionBase(DependencyObject target, DependencyProperty dp)
        { 
            if (target == null)
                throw new ArgumentNullException("target");
            if (dp == null)
                throw new ArgumentNullException("dp"); 
//            target.VerifyAccess();
 
            Expression expr = StyleHelper.GetExpression(target, dp); 
            return expr as BindingExpressionBase;
        } 

        /// <summary>
        /// Retrieve a BindingExpression.
        /// </summary> 
        /// <remarks>
        /// This method returns null if no Binding has been set on the given 
        /// property. 
        /// </remarks>
        /// <param name="target">object from which to retrieve the BindingExpression</param> 
        /// <param name="dp">property from which to retrieve the BindingExpression</param>
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception>
        public static BindingExpression GetBindingExpression(DependencyObject target, DependencyProperty dp)
        { 
            BindingExpressionBase expr = GetBindingExpressionBase(target, dp);
 
            PriorityBindingExpression pb = expr as PriorityBindingExpression; 
            if (pb != null)
                expr = pb.ActiveBindingExpression; 

            return expr as BindingExpression;
        }
 
        /// <summary>
        /// Retrieve a MultiBindingExpression. 
        /// </summary> 
        /// <remarks>
        /// This method returns null if no MultiBinding has been set on the given 
        /// property.
        /// </remarks>
        /// <param name="target">object from which to retrieve the MultiBindingExpression</param>
        /// <param name="dp">property from which to retrieve the MultiBindingExpression</param> 
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception>
        public static MultiBindingExpression GetMultiBindingExpression(DependencyObject target, DependencyProperty dp) 
        { 
            return GetBindingExpressionBase(target, dp) as MultiBindingExpression;
        } 

        /// <summary>
        /// Retrieve a PriorityBindingExpression.
        /// </summary> 
        /// <remarks>
        /// This method returns null if no PriorityBinding has been set on the given 
        /// property. 
        /// </remarks>
        /// <param name="target">object from which to retrieve the PriorityBindingExpression</param> 
        /// <param name="dp">property from which to retrieve the PriorityBindingExpression</param>
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception>
        public static PriorityBindingExpression GetPriorityBindingExpression(DependencyObject target, DependencyProperty dp)
        { 
            return GetBindingExpressionBase(target, dp) as PriorityBindingExpression;
        } 
 
        /// <summary>
        /// Remove data Binding (if any) from a property. 
        /// </summary>
        /// <remarks>
        /// If the given property is data-bound, via a Binding, PriorityBinding or MultiBinding,
        /// the BindingExpression is removed, and the property's value changes to what it 
        /// would be as if no local value had ever been set.
        /// If the given property is not data-bound, this method has no effect. 
        /// </remarks> 
        /// <param name="target">object from which to remove Binding</param>
        /// <param name="dp">property from which to remove Binding</param> 
        /// <exception cref="ArgumentNullException"> target and dp cannot be null </exception>
        public static void ClearBinding(DependencyObject target, DependencyProperty dp)
        {
            if (target == null) 
                throw new ArgumentNullException("target");
            if (dp == null) 
                throw new ArgumentNullException("dp"); 
//            target.VerifyAccess();
 
            if (IsDataBound(target, dp))
                target.ClearValue(dp);
        }
 
        /// <summary>
        /// Remove all data Binding (if any) from a DependencyObject. 
        /// </summary> 
        /// <param name="target">object from which to remove bindings</param>
        /// <exception cref="ArgumentNullException"> DependencyObject target cannot be null </exception> 
        public static void ClearAllBindings(DependencyObject target)
        {
            if (target == null)
                throw new ArgumentNullException("target"); 
//            target.VerifyAccess();
 
            LocalValueEnumerator lve = target.GetLocalValueEnumerator(); 

            // Batch properties that have BindingExpressions since clearing 
            // during a local value enumeration is illegal
            ArrayList batch = new ArrayList(8);

            while (lve.MoveNext()) 
            {
                LocalValueEntry entry = lve.Current; 
                if (IsDataBound(target, entry.Property)) 
                {
                    batch.Add(entry.Property); 
                }
            }

            // Clear all properties that are storing BindingExpressions 
            for (int i = 0; i < batch.Count; i++)
            { 
                target.ClearValue((DependencyProperty)batch[i]); 
            }
        } 

        /// <summary>Return true if the property is currently data-bound</summary>
        /// <exception cref="ArgumentNullException"> DependencyObject target cannot be null </exception>
        public static bool IsDataBound(DependencyObject target, DependencyProperty dp) 
        {
            if (target == null) 
                throw new ArgumentNullException("target"); 
            if (dp == null)
                throw new ArgumentNullException("dp"); 
//            target.VerifyAccess();

            object o = StyleHelper.GetExpression(target, dp);
            return (o is BindingExpressionBase); 
        }
 
 
        //------------------------------------------------------
        // 
        //  Internal Methods
        //
        //-----------------------------------------------------
 
        // return false if this is an invalid value for UpdateSourceTrigger
        internal static bool IsValidUpdateSourceTrigger(UpdateSourceTrigger value) 
        { 
            switch (value)
            { 
                case UpdateSourceTrigger.Default:
                case UpdateSourceTrigger.PropertyChanged:
                case UpdateSourceTrigger.LostFocus:
                case UpdateSourceTrigger.Explicit: 
                    return true;
 
                default: 
                    return false;
            } 
        }

        // The following properties and methods have no internal callers.  They
        // can be called by suitably privileged external callers via reflection. 
        // They are intended to be used by test programs and the DRT.
 
        // Enable or disable the cleanup pass.  For use by tests that measure 
        // perf, to avoid noise from the cleanup pass.
        internal static bool IsCleanupEnabled 
        {
            get { return DataBindEngine.CurrentDataBindEngine.CleanupEnabled; }
            set { DataBindEngine.CurrentDataBindEngine.CleanupEnabled = value; }
        } 

        // Force a cleanup pass (even if IsCleanupEnabled is true).  For use 
        // by leak-detection tests, to avoid false leak reports about objects 
        // held by the DataBindEngine that can be cleaned up.  Returns true
        // if something was actually cleaned up. 
        internal static bool Cleanup()
        {
            return DataBindEngine.CurrentDataBindEngine.Cleanup();
        } 

        // Print various interesting statistics 
        internal static void PrintStats() 
        {
            DataBindEngine.CurrentDataBindEngine.AccessorTable.PrintStats(); 
        }

        // Trace the size of the accessor table after each generation
        internal static bool TraceAccessorTableSize 
        {
            get { return DataBindEngine.CurrentDataBindEngine.AccessorTable.TraceSize; } 
            set { DataBindEngine.CurrentDataBindEngine.AccessorTable.TraceSize = value; } 
        }
    } 
}


