//---------------------------------------------------------------------------- 
//
// <copyright file="Helper.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//      Implements some helper functions. 
//
// History: 
//  07/30/03: [....]:       Add header and some new functions.
//
//---------------------------------------------------------------------------
 
using MS.Internal.Utility;
using System; 
using System.Collections; 
using System.Collections.ObjectModel; // Collection<T>
using System.ComponentModel; 

using System.Diagnostics;
using System.IO.Packaging;
 
using System.Reflection;
using System.Security.Permissions; 
using System.Windows; 
using System.Windows.Data; // BindingBase
using System.Windows.Markup; // IProvideValueTarget 
using System.Windows.Media;
using System.Security;

using MS.Internal.AppModel; 
using System.Windows.Threading;
 
namespace MS.Internal 
{
 
    // Miscellaneous (and internal) helper functions.
    internal static class Helper
    {
 
        internal static object ResourceFailureThrow(object key)
        { 
            FindResourceHelper helper = new FindResourceHelper(key); 
            return helper.TryCatchWhen();
        } 

        private class FindResourceHelper
        {
            internal object TryCatchWhen() 
            {
                Dispatcher.CurrentDispatcher.WrappedInvoke(new DispatcherOperationCallback(DoTryCatchWhen), 
                                                                                            null, 
                                                                                            1,
                                                                                            new DispatcherOperationCallback(CatchHandler)); 
                return _resource;
            }

            private object DoTryCatchWhen(object arg) 
            {
                throw new ResourceReferenceKeyNotFoundException(SR.Get(SRID.MarkupExtensionResourceNotFound, _name), _name); 
            } 

            private object CatchHandler(object arg) 
            {
                _resource = DependencyProperty.UnsetValue;
                return null;
            } 

            public FindResourceHelper(object name) 
            { 
                _name = name;
                _resource = null; 
            }

            private object _name;
            private object _resource; 
        }
 
 
        // Find a data template (or table template) resource
        internal static object FindTemplateResourceFromAppOrSystem(DependencyObject target, ArrayList keys, int exactMatch, ref int bestMatch) 
        {
            object resource = null;
            int k;
 
            // Comment out below three lines code.
            // For now, we will always get the resource from Application level 
            // if the resource exists. 
            //
            // But we do need to have a right design in the future that can make 
            // sure the tree get the right resource updated while the Application
            // level resource is changed later dynamically.
            //
            // 

 
 

 
            Application app = Application.Current;
            if (app != null)
            {
                // If the element is rooted to a Window and App exists, defer to App. 
                for (k = 0;  k < bestMatch;  ++k)
                { 
                    object appResource = Application.Current.FindResourceInternal(keys[k]); 
                    if (appResource != null)
                    { 
                        bestMatch = k;
                        resource = appResource;

                        if (bestMatch < exactMatch) 
                            return resource;
                    } 
                } 
            }
 
            // if best match is not found from the application level,
            // try it from system level.
            if (bestMatch >= exactMatch)
            { 
                // Try the system resource collection.
                for (k = 0;  k < bestMatch;  ++k) 
                { 
                    object sysResource = SystemResources.FindResourceInternal(keys[k]);
                    if (sysResource != null) 
                    {
                        bestMatch = k;
                        resource = sysResource;
 
                        if (bestMatch < exactMatch)
                            return resource; 
                    } 
                }
            } 

            return resource;
        }
 
            //
/* 
        // Returns the absolute root of the tree by walking through frames. 
        internal static DependencyObject GetAbsoluteRoot(DependencyObject iLogical)
        { 
            DependencyObject currentRoot = iLogical;
            Visual visual;
            Visual parentVisual;
            bool bDone = false; 

            if (currentRoot == null) 
            { 
                return null;
            } 

            while (!bDone)
            {
                // Try logical parent. 
                DependencyObject parent = LogicalTreeHelper.GetParent(currentRoot);
 
                if (parent != null) 
                {
                    currentRoot = parent; 
                }
                else
                {
                    // Try visual parent 
                    Visual visual = currentRoot as Visual;
                    if (visual != null) 
                    { 
                        Visual parentVisual = VisualTreeHelper.GetParent(visual);
                        if (parentVisual != null) 
                        {
                            currentRoot = parentVisual;
                            continue;
                        } 
                    }
 
                    // No logical or visual parent, so we're done. 
                    bDone = true;
                } 
            }

            return currentRoot;
        } 
*/
 
        /// <summary> 
        ///     This method finds the mentor by looking up the InheritanceContext
        ///     links starting from the given node until it finds an FE/FCE. This 
        ///     mentor will be used to do a FindResource call while evaluating this
        ///     expression.
        /// </summary>
        /// <remarks> 
        ///     This method is invoked by the ResourceReferenceExpression
        ///     and BindingExpression 
        /// </remarks> 
        internal static DependencyObject FindMentor(DependencyObject d)
        { 
            // Find the nearest FE/FCE InheritanceContext
            while (d != null)
            {
                FrameworkElement fe; 
                FrameworkContentElement fce;
                Helper.DowncastToFEorFCE(d, out fe, out fce, false); 
 
                if (fe != null)
                { 
                    return fe;
                }
                else if (fce != null)
                { 
                    return fce;
                } 
                else 
                {
                    d = d.InheritanceContext; 
                }
            }

            return null; 
        }
 
        /// <summary> 
        /// Return true if the given property is not set locally or from a style
        /// </summary> 
        internal static bool HasDefaultValue(DependencyObject d, DependencyProperty dp)
        {
            return HasDefaultOrInheritedValueImpl(d, dp, false, true);
        } 

        /// <summary> 
        /// Return true if the given property is not set locally or from a style or by inheritance 
        /// </summary>
        internal static bool HasDefaultOrInheritedValue(DependencyObject d, DependencyProperty dp) 
        {
            return HasDefaultOrInheritedValueImpl(d, dp, true, true);
        }
 
        /// <summary>
        /// Return true if the given property is not set locally or from a style 
        /// </summary> 
        internal static bool HasUnmodifiedDefaultValue(DependencyObject d, DependencyProperty dp)
        { 
            return HasDefaultOrInheritedValueImpl(d, dp, false, false);
        }

        /// <summary> 
        /// Return true if the given property is not set locally or from a style or by inheritance
        /// </summary> 
        internal static bool HasUnmodifiedDefaultOrInheritedValue(DependencyObject d, DependencyProperty dp) 
        {
            return HasDefaultOrInheritedValueImpl(d, dp, true, false); 
        }

        /// <summary>
        /// Return true if the given property is not set locally or from a style 
        /// </summary>
        private static bool HasDefaultOrInheritedValueImpl(DependencyObject d, DependencyProperty dp, 
                                                                bool checkInherited, 
                                                                bool ignoreModifiers)
        { 
            PropertyMetadata metadata = dp.GetMetadata(d);
            bool hasModifiers;
            BaseValueSourceInternal source = d.GetValueSource(dp, metadata, out hasModifiers);
 
            if (source == BaseValueSourceInternal.Default ||
                (checkInherited && source == BaseValueSourceInternal.Inherited)) 
            { 
                if (ignoreModifiers)
                { 
                    // ignore modifiers on FE/FCE, for back-compat
                    if (d is FrameworkElement || d is FrameworkContentElement)
                    {
                        hasModifiers = false; 
                    }
                } 
 
                // a default or inherited value might be animated or coerced.  We should
                // return false in that case - the hasModifiers flag tests this. 
                // (An expression modifier can't apply to a default or inherited value.)
                return !hasModifiers;
            }
 
            return false;
        } 
 
        /// <summary>
        /// Downcast the given DependencyObject into FrameworkElement or 
        /// FrameworkContentElement, as appropriate.
        /// </summary>
        internal static void DowncastToFEorFCE(DependencyObject d,
                                    out FrameworkElement fe, out FrameworkContentElement fce, 
                                    bool throwIfNeither)
        { 
            if (FrameworkElement.DType.IsInstanceOfType(d)) 
            {
                fe = (FrameworkElement)d; 
                fce = null;
            }
            else if (FrameworkContentElement.DType.IsInstanceOfType(d))
            { 
                fe = null;
                fce = (FrameworkContentElement)d; 
            } 
            else if (throwIfNeither)
            { 
                throw new InvalidOperationException(SR.Get(SRID.MustBeFrameworkDerived, d.GetType()));
            }
            else
            { 
                fe = null;
                fce = null; 
            } 
        }
 

        /// <summary>
        /// Issue a trace message if both the xxxStyle and xxxStyleSelector
        /// properties are set on the given element. 
        /// </summary>
        internal static void CheckStyleAndStyleSelector(string name, 
                                                        DependencyProperty styleProperty, 
                                                        DependencyProperty styleSelectorProperty,
                                                        DependencyObject d) 
        {
            // Issue a trace message if user defines both xxxStyle and xxxStyleSelector
            // (bugs 1007020, 1019240).  Only explicit local values or resource
            // references count;  data-bound or styled values don't count. 
            // Do not throw here (bug 1434271), because it's very confusing if the
            // user tries to continue from this exception. 
            if (TraceData.IsEnabled) 
            {
                object styleSelector = d.ReadLocalValue(styleSelectorProperty); 

                if (styleSelector != DependencyProperty.UnsetValue &&
                    (styleSelector is System.Windows.Controls.StyleSelector || styleSelector is ResourceReferenceExpression))
                { 
                    object style = d.ReadLocalValue(styleProperty);
 
                    if (style != DependencyProperty.UnsetValue && 
                        (style is Style || style is ResourceReferenceExpression))
                    { 
                        TraceData.Trace(TraceEventType.Error, TraceData.StyleAndStyleSelectorDefined(name), d);
                    }
                }
            } 
        }
 
        /// <summary> 
        /// Issue a trace message if both the xxxTemplate and xxxTemplateSelector
        /// properties are set on the given element. 
        /// </summary>
        internal static void CheckTemplateAndTemplateSelector(string name,
                                                        DependencyProperty templateProperty,
                                                        DependencyProperty templateSelectorProperty, 
                                                        DependencyObject d)
        { 
            // Issue a trace message if user defines both xxxTemplate and xxxTemplateSelector 
            // (bugs 1007020, 1019240).  Only explicit local values or resource
            // references count;  data-bound or templated values don't count. 
            // Do not throw here (bug 1434271), because it's very confusing if the
            // user tries to continue from this exception.
            if (TraceData.IsEnabled)
            { 
                if (IsTemplateSelectorDefined(templateSelectorProperty, d))
                { 
                    if (IsTemplateDefined(templateProperty, d)) 
                    {
                        TraceData.Trace(TraceEventType.Error, TraceData.TemplateAndTemplateSelectorDefined(name), d); 
                    }
                }
            }
        } 

        /// <summary> 
        /// Check whether xxxTemplateSelector property is set on the given element. 
        /// Only explicit local values or resource references count;  data-bound or templated values don't count.
        /// </summary> 
        internal static bool IsTemplateSelectorDefined(DependencyProperty templateSelectorProperty, DependencyObject d)
        {
            // Check whether xxxTemplateSelector property is set on the given element.
            object templateSelector = d.ReadLocalValue(templateSelectorProperty); 
            // the checks for UnsetValue and null are for perf:
            // they're redundant to the type checks, but they're cheaper 
            return (templateSelector != DependencyProperty.UnsetValue && 
                    templateSelector != null &&
                   (templateSelector is System.Windows.Controls.DataTemplateSelector || 
                    templateSelector is ResourceReferenceExpression));
        }

        /// <summary> 
        /// Check whether xxxTemplate property is set on the given element.
        /// Only explicit local values or resource references count;  data-bound or templated values don't count. 
        /// </summary> 
        internal static bool IsTemplateDefined(DependencyProperty templateProperty, DependencyObject d)
        { 
            // Check whether xxxTemplate property is set on the given element.
            object template = d.ReadLocalValue(templateProperty);
            // the checks for UnsetValue and null are for perf:
            // they're redundant to the type checks, but they're cheaper 
            return (template != DependencyProperty.UnsetValue &&
                    template != null && 
                    (template is FrameworkTemplate || 
                    template is ResourceReferenceExpression));
        } 

        ///<summary>
        ///     Helper method to find an object by name inside a template
        ///</summary> 
        internal static object FindNameInTemplate(string name, DependencyObject templatedParent)
        { 
            FrameworkElement fe = templatedParent as FrameworkElement; 
            Debug.Assert( fe != null );
 
            return fe.TemplateInternal.FindName(name, fe);
        }

        /// <summary> 
        /// Find the IGeneratorHost that is responsible (possibly indirectly)
        /// for the creation of the given DependencyObject. 
        /// </summary> 
        internal static MS.Internal.Controls.IGeneratorHost GeneratorHostForElement(DependencyObject element)
        { 
            DependencyObject d = null;
            DependencyObject parent = null;

            // 1. Follow the TemplatedParent chain to the end.  This should be 
            // the ItemContainer.
            while (element != null) 
            { 
                while (element != null)
                { 
                    d = element;
                    element= GetTemplatedParent(element);

                    // Special case to display the selected item in a ComboBox, when 
                    // the items are XmlNodes and the DisplayMemberPath is an XPath
                    // that uses namespace prefixes (Dev10 bug 459976).  We need an 
                    // XmlNamespaceManager to map prefixes to namespaces, and in this 
                    // special case we should use the ComboBox itself, rather than any
                    // surrounding ItemsControl.  There's no elegant way to detect 
                    // this situation;  the following code is a child of necessity.
                    // It relies on the fact that the "selection box" is implemented
                    // by a ContentPresenter in the ComboBox's control template, and
                    // any ContentPresenter whose TemplatedParent is a ComboBox is 
                    // playing the role of "selection box".
                    if (d is System.Windows.Controls.ContentPresenter) 
                    { 
                        System.Windows.Controls.ComboBox cb = element as System.Windows.Controls.ComboBox;
                        if (cb != null) 
                        {
                            return cb;
                        }
                    } 
                }
 
                Visual v = d as Visual; 
                if (v != null)
                { 
                    parent = VisualTreeHelper.GetParent(v);

                    // In ListView, we should rise through a GridView*RowPresenter
                    // even though it is not the TemplatedParent (bug 1937470) 
                    element = parent as System.Windows.Controls.Primitives.GridViewRowPresenterBase;
                } 
                else 
                {
                    parent = null; 
                }
            }

            // 2. In an ItemsControl, the container's parent is the "ItemsHost" 
            // panel, from which we get to the ItemsControl by public API.
            if (parent != null) 
            { 
                System.Windows.Controls.ItemsControl ic = System.Windows.Controls.ItemsControl.GetItemsOwner(parent);
                if (ic != null) 
                    return ic;
            }

            return null; 
        }
 
        internal static DependencyObject GetTemplatedParent(DependencyObject d) 
        {
            FrameworkElement fe; 
            FrameworkContentElement fce;
            DowncastToFEorFCE(d, out fe, out fce, false);
            if (fe != null)
                return fe.TemplatedParent; 
            else if (fce != null)
                return fce.TemplatedParent; 
 
            return null;
        } 

        /// <summary>
        /// Find the XmlDataProvider (if any) that is associated with the
        /// given DependencyObject. 
        /// This method only works when the DO is part of the generated content
        /// of an ItemsControl or TableRowGroup. 
        /// </summary> 
        internal static System.Windows.Data.XmlDataProvider XmlDataProviderForElement(DependencyObject d)
        { 
            MS.Internal.Controls.IGeneratorHost host = Helper.GeneratorHostForElement(d);
            System.Windows.Controls.ItemCollection ic = (host != null) ? host.View : null;
            ICollectionView icv = (ic != null) ? ic.CollectionView : null;
            MS.Internal.Data.XmlDataCollection xdc = (icv != null) ? icv.SourceCollection as MS.Internal.Data.XmlDataCollection : null; 

            return (xdc != null) ? xdc.ParentXmlDataProvider : null; 
        } 

#if CF_Envelope_Activation_Enabled 
        /// <summary>
        /// Indicates whether our content is inside an old-style container
        /// </summary>
        /// <value></value> 
        ///<SecurityNote>
        /// Critical as it accesses the container object 
        /// TreatAsSafe as it only returns safe data 
        ///</SecurityNote>
 
        internal static bool IsContainer
        {
            [SecurityCritical, SecurityTreatAsSafe]
            get 
            {
                return BindUriHelper.Container != null; 
            } 
        }
#endif 

        internal static void SetMeasureDataOnChild(UIElement element, UIElement child, Size childConstraint)
        {
            MeasureData measureData = element.MeasureData; 

            if (measureData != null) 
            { 
                MeasureData childData = new MeasureData(measureData);
                childData.AvailableSize = childConstraint; 
                child.MeasureData = childData;
            }
            else
            { 
                child.MeasureData = null;
            } 
        } 

        /// <summary> 
        /// Measure a simple element with a single child.
        /// </summary>
        internal static Size MeasureElementWithSingleChild(UIElement element, Size constraint)
        { 
            UIElement child = (VisualTreeHelper.GetChildrenCount(element) > 0) ? VisualTreeHelper.GetChild(element, 0) as UIElement : null;
 
            if (child != null) 
            {
 
                Helper.SetMeasureDataOnChild(element, child, constraint);  // pass along MeasureData so it continues down the tree.
                child.Measure(constraint);
                return child.DesiredSize;
            } 

            return new Size(); 
        } 

 
        /// <summary>
        /// Arrange a simple element with a single child.
        /// </summary>
        internal static Size ArrangeElementWithSingleChild(UIElement element, Size arrangeSize) 
        {
            UIElement child = (VisualTreeHelper.GetChildrenCount(element) > 0) ? VisualTreeHelper.GetChild(element, 0) as UIElement : null; 
 
            if (child != null)
            { 
                child.Arrange(new Rect(arrangeSize));
            }

            return arrangeSize; 
        }
 
        /// <summary> 
        /// Helper method used for double parameter validation.  Returns false
        /// if the value is either Infinity (positive or negative) or NaN. 
        /// </summary>
        /// <param name="value">The double value to test</param>
        /// <returns>Whether the value is a valid double.</returns>
        internal static bool IsDoubleValid(double value) 
        {
            return !(Double.IsInfinity(value) || Double.IsNaN(value)); 
        } 

        /// <summary> 
        /// Checks if the given IProvideValueTarget can receive
        /// a DynamicResource or Binding MarkupExtension.
        /// </summary>
        internal static void CheckCanReceiveMarkupExtension( 
                MarkupExtension     markupExtension,
                IProvideValueTarget provideValueTarget, 
            out DependencyObject    targetDependencyObject, 
            out DependencyProperty  targetDependencyProperty)
        { 
            targetDependencyObject = null;
            targetDependencyProperty = null;

            if (provideValueTarget == null) 
            {
                return; 
            } 

            object targetObject = provideValueTarget.TargetObject; 

            if (targetObject == null)
            {
                return; 
            }
 
            Type targetType = targetObject.GetType(); 
            object targetProperty = provideValueTarget.TargetProperty;
 
            if (targetProperty != null)
            {
                targetDependencyProperty = targetProperty as DependencyProperty;
                if (targetDependencyProperty != null) 
                {
                    // This is the DependencyProperty case 
 
                    targetDependencyObject = targetObject as DependencyObject;
                    Debug.Assert(targetDependencyObject != null, "DependencyProperties can only be set on DependencyObjects"); 
                }
                else
                {
                    MemberInfo targetMember = targetProperty as MemberInfo; 
                    if (targetMember != null)
                    { 
                        // This is the Clr Property case 

                        // Setters, Triggers, DataTriggers & Conditions are the special cases of 
                        // Clr properties where DynamicResource & Bindings are allowed. However
                        // StyleHelper.ProcessSharedPropertyValue avoids a call to ProvideValue
                        // in these cases and so there is no need for special code to handle them here.
 
                        // Find the MemberType
 
                        Debug.Assert(targetMember is PropertyInfo || targetMember is MethodInfo, 
                            "TargetMember is either a Clr property or an attached static settor method");
 
                        Type memberType;

                        PropertyInfo propertyInfo = targetMember as PropertyInfo;
                        if (propertyInfo != null) 
                        {
                            memberType = propertyInfo.PropertyType; 
                        } 
                        else
                        { 
                            MethodInfo methodInfo = (MethodInfo)targetMember;
                            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                            Debug.Assert(parameterInfos.Length == 2, "The signature of a static settor must contain two parameters");
                            memberType = parameterInfos[1].ParameterType; 
                        }
 
                        // Check if the MarkupExtensionType is assignable to the given MemberType 
                        // This check is to allow properties such as the following
                        // - DataTrigger.Binding 
                        // - Condition.Binding
                        // - HierarchicalDataTemplate.ItemsSource
                        // - GridViewColumn.DisplayMemberBinding
 
                        if (!typeof(MarkupExtension).IsAssignableFrom(memberType) ||
                             !memberType.IsAssignableFrom(markupExtension.GetType())) 
                        { 
                            throw new XamlParseException(SR.Get(SRID.MarkupExtensionDynamicOrBindingOnClrProp,
                                                                markupExtension.GetType().Name, 
                                                                targetMember.Name,
                                                                targetType.Name));
                        }
                    } 
                    else
                    { 
                        // This is the Collection ContentProperty case 
                        // Example:
                        // <DockPanel> 
                        //   <Button />
                        //   <DynamicResource ResourceKey="foo" />
                        // </DockPanel>
 
                        // Collection<BindingBase> used in MultiBinding is a special
                        // case of a Collection that can contain a Binding. 
 
                        if (!typeof(BindingBase).IsAssignableFrom(markupExtension.GetType()) ||
                            !typeof(Collection<BindingBase>).IsAssignableFrom(targetProperty.GetType())) 
                        {
                            throw new XamlParseException(SR.Get(SRID.MarkupExtensionDynamicOrBindingInCollection,
                                                                markupExtension.GetType().Name,
                                                                targetProperty.GetType().Name)); 
                        }
                    } 
                } 
            }
            else 
            {
                // This is the explicit Collection Property case
                // Example:
                // <DockPanel> 
                // <DockPanel.Children>
                //   <Button /> 
                //   <DynamicResource ResourceKey="foo" /> 
                // </DockPanel.Children>
                // </DockPanel> 

                // Collection<BindingBase> used in MultiBinding is a special
                // case of a Collection that can contain a Binding.
 
                if (!typeof(BindingBase).IsAssignableFrom(markupExtension.GetType()) ||
                    !typeof(Collection<BindingBase>).IsAssignableFrom(targetType)) 
                { 
                    throw new XamlParseException(SR.Get(SRID.MarkupExtensionDynamicOrBindingInCollection,
                                                        markupExtension.GetType().Name, 
                                                        targetType.Name));
                }
            }
        } 

        // build a format string suitable for String.Format from the given argument, 
        // by expanding the convenience form, if necessary 
        internal static string GetEffectiveStringFormat(string stringFormat)
        { 
            if (stringFormat.IndexOf('{') < 0)
            {
                // convenience syntax - build a composite format string with one parameter
                stringFormat = @"{0:" + stringFormat + @"}"; 
            }
 
            return stringFormat; 
        }
 
        // The following unused API has been removed (caught by FxCop).  If needed, recall from history.
        //
        // internal static Condition BuildCondition(DependencyProperty property, object value)
        // 
        // internal static MultiTrigger BuildMultiTrigger(ArrayList triggerCollection,
        //                                  string target, DP targetProperty, object targetValue) 
        // 
        // internal static Trigger BuildTrigger( DependencyProperty triggerProperty, object triggerValue,
        //                              string target, DependencyProperty targetProperty, object targetValue ) 
        //
        // internal static bool CheckWriteableContainerStatus()
        //
        // internal static bool IsCallerOfType(Type type) 
        //
        // public static bool IsInStaticConstructorOfType(Type t) 
        // 
        // private static bool IsInWindowCollection(object window)
        // 
        // internal static bool IsMetroContainer
        //
        // internal static bool IsRootElement(DependencyObject node)
 
        //-----------------------------------------------------
        // 
        //  Private Enums, Structs, Constants 
        //
        //----------------------------------------------------- 

        static readonly Type   NullableType = Type.GetType("System.Nullable`1");

    } 
}
 
 

